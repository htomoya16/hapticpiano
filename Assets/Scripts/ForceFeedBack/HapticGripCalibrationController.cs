using System.Collections;
using UnityEngine;

/// <summary>
/// キャリブレーション（現行）:
/// - 「軽く握ってください」「キャリブレーション中は指の力を抜いてください」を案内し、10 秒カウントダウン
/// - 親指→人差し指→中指→薬指→小指の順に、対象指のみを 0→1000 へ段階的に動かす
/// - 指ごとに sensorRaw が基準レンジから外れた瞬間の 1 ステップ前のサーボ値を保存する
/// </summary>
[DisallowMultipleComponent]
public class HapticGripCalibrationController : MonoBehaviour
{
    private const int FingerCount = 5;

    [Header("References")]
    public HandCurlTracker curlTracker;
    public HapticSerialSender serialSender;
    public HapticCalibrationState calibrationState;

    [Header("Sweep")]
    [Tooltip("スイープ開始サーボ値")]
    public int startServoValue = 0;

    [Tooltip("スイープ終了サーボ値")]
    public int endServoValue = 1000;

    [Tooltip("サーボ値のステップ幅（大きいほど速いが精度は落ちる）")]
    public int stepSize = 20;

    [Tooltip("1ステップごとの待機（リアルタイム秒）。設定画面で TimeScale=0 でも動くように Realtime を使う")]
    public float stepIntervalSeconds = 0.05f;

    [Header("Detection (sensorRaw)")]
    [Tooltip("基準（握って力を抜いた状態）からの許容レンジ（±）。この範囲を外れたら『外れた』とみなす")]
    public int allowedBaselineDeviation = 120;

    [Tooltip("レンジ外が連続でこの回数続いたら確定する（ノイズ対策）")]
    public int requiredConsecutiveOutOfRange = 3;

    [Tooltip("基準値を取るときのサンプル数")]
    public int baselineSamples = 10;

    [Tooltip("基準値サンプルの間隔（リアルタイム秒）")]
    public float baselineSampleIntervalSeconds = 0.02f;

    [Header("Flow")]
    [Tooltip("開始前の案内カウントダウン秒数（10,9,...,1）。0 以下で無効。")]
    public int initialCountdownSeconds = 10;

    [Tooltip("各指開始前のカウントダウン秒数（3,2,1）。0 以下で無効。")]
    public int perFingerCountdownSeconds = 3;

    [Tooltip("スイープで変化が検知できない場合のタイムアウト（0以下で無効）")]
    public float timeoutSeconds = 6.0f;

    [Header("Status (read-only)")]
    [SerializeField] private bool isRunning;
    [SerializeField] private int currentFingerIndex = -1;
    [SerializeField] private int currentServoValue;
    [SerializeField] private string statusMessage;

    public bool IsRunning => isRunning;
    public int CurrentFingerIndex => currentFingerIndex;
    public int CurrentServoValue => currentServoValue;
    public string StatusMessage => statusMessage;

    private Coroutine _routine;
    private bool _previousSenderEnableSend;
    private int[] _scratchTargets = new int[FingerCount];
    private int[] _holdTargets = new int[FingerCount];
    private bool _wasCancelled;

    public void StartCalibration()
    {
        if (_routine != null) return;

        if (curlTracker == null || serialSender == null || calibrationState == null)
        {
            Debug.LogWarning("[HapticGripCalibrationController] References missing.");
            return;
        }

        _routine = StartCoroutine(CalibrationRoutine());
    }

    public void CancelCalibration()
    {
        if (_routine == null) return;
        _wasCancelled = true;
        StopCoroutine(_routine);
        _routine = null;
        CleanupAfterRoutine();
        statusMessage = "Calibration cancelled.";
    }

    public void ResetCalibration()
    {
        if (calibrationState != null)
        {
            calibrationState.ResetAll();
        }
    }

    private IEnumerator CalibrationRoutine()
    {
        isRunning = true;
        _wasCancelled = false;
        currentFingerIndex = -1;
        currentServoValue = 0;
        statusMessage = "軽く握ってください。\nキャリブレーション中は指の力を抜いてください。";

        _previousSenderEnableSend = serialSender.enableSend;
        serialSender.enableSend = false;

        GetSweepRangeAscending(out int start, out int end, out int step);
        EnsureScratchTargets();
        EnsureHoldTargets();
        for (int i = 0; i < FingerCount; i++)
        {
            _scratchTargets[i] = start;
            _holdTargets[i] = start;
        }
        serialSender.TrySendNow(_scratchTargets, bypassGuards: true);
        ApplyHoldTargetsToSender();

        // 開始前のカウントダウン（10,9,...,1）
        if (initialCountdownSeconds > 0)
        {
            for (int s = initialCountdownSeconds; s >= 1; s--)
            {
                statusMessage = $"軽く握ってください。\nキャリブレーション中は指の力を抜いてください。\n開始まで {s}";
                yield return new WaitForSecondsRealtime(1f);
                if (_wasCancelled) yield break;
            }
        }

        bool[] succeeded = new bool[FingerCount];

        for (int finger = 0; finger < FingerCount; finger++)
        {
            if (_wasCancelled) yield break;

            currentFingerIndex = finger;
            currentServoValue = start;

            // 指ごとの案内 + カウントダウン（3,2,1）
            if (perFingerCountdownSeconds > 0)
            {
                for (int s = perFingerCountdownSeconds; s >= 1; s--)
                {
                    statusMessage = $"{FingerNameJapanese(finger)}が動きます。\n開始まで {s}";
                    yield return new WaitForSecondsRealtime(1f);
                    if (_wasCancelled) yield break;
                }
            }
            else
            {
                statusMessage = $"{FingerNameJapanese(finger)}が動きます。";
            }

            yield return CalibrateSingleFingerByOutOfRange(finger, succeeded, start, end, step);

            // 次の指に移る前に、全指を start に戻す
            // （確定済みの指は released を維持する）
            serialSender.TrySendNow(_holdTargets, bypassGuards: true);
            ApplyHoldTargetsToSender();
        }

        currentFingerIndex = -1;
        currentServoValue = 0;
        statusMessage = calibrationState.IsFullyCalibrated ? "キャリブレーション完了" : "キャリブレーション完了（未完了あり）";

        CleanupAfterRoutine();
    }

    private IEnumerator CalibrateSingleFingerByOutOfRange(int finger, bool[] succeeded, int start, int end, int step)
    {
        if (succeeded[finger])
        {
            statusMessage = $"{FingerNameJapanese(finger)} は保存済みです。";
            yield break;
        }

        // 指ごとの基準（軽く握って力を抜いた状態）の sensorRaw を取得
        int samples = Mathf.Max(1, baselineSamples);
        int sum = 0;
        for (int s = 0; s < samples; s++)
        {
            sum += GetSensorRawSafe(finger);
            if (baselineSampleIntervalSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(baselineSampleIntervalSeconds);
            }
            if (_wasCancelled) yield break;
        }

        int baseline = sum / samples;
        int dev = Mathf.Max(0, allowedBaselineDeviation);
        int min = baseline - dev;
        int max = baseline + dev;

        int required = Mathf.Max(1, requiredConsecutiveOutOfRange);
        int outOfRangeCount = 0;
        int prevServoValue = start;
        float elapsed = 0f;

        // 既に確定した指は released 値を維持し、未確定は start にする
        EnsureHoldTargets();
        for (int i = 0; i < FingerCount; i++)
        {
            if (calibrationState.TryGetReleasedServoValue(i, out int released))
            {
                _holdTargets[i] = released;
            }
            else
            {
                _holdTargets[i] = start;
            }
        }
        serialSender.TrySendNow(_holdTargets, bypassGuards: true);

        for (int v = start; v <= end; v += step)
        {
            if (_wasCancelled) yield break;

            if (timeoutSeconds > 0f && elapsed > timeoutSeconds)
            {
                break;
            }

            int clamped = Clamp1000(v);
            // 他指は _holdTargets（確定済み released / 未確定 start）で維持し、対象指だけスイープ
            for (int i = 0; i < FingerCount; i++) _scratchTargets[i] = _holdTargets[i];
            _scratchTargets[finger] = clamped;

            serialSender.TrySendNow(_scratchTargets, bypassGuards: true);
            currentServoValue = clamped;

            if (stepIntervalSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(stepIntervalSeconds);
                elapsed += stepIntervalSeconds;
            }

            int now = GetSensorRawSafe(finger);
            bool outOfRange = now < min || now > max;
            outOfRangeCount = outOfRange ? (outOfRangeCount + 1) : 0;

            statusMessage = $"{FingerNameJapanese(finger)} キャリブ中... {clamped}";

            if (outOfRangeCount >= required)
            {
                // レンジ外に出た「直前」の値を released として保存し、即座にその指へ反映する。
                calibrationState.SetReleasedServoValue(finger, prevServoValue);
                succeeded[finger] = true;

                // 確定値を維持（以降の指キャリブ中も released を保持する）
                _holdTargets[finger] = prevServoValue;
                serialSender.TrySendNow(_holdTargets, bypassGuards: true);
                ApplyHoldTargetsToSender();

                statusMessage = $"{FingerNameJapanese(finger)} 保存: {prevServoValue}（反映）";
                yield break;
            }

            prevServoValue = clamped;
        }

        statusMessage = $"{FingerNameJapanese(finger)} 失敗（レンジ外にならない）";
    }

    private void CleanupAfterRoutine()
    {
        // コルーチンが自然終了した場合も、次回開始できるよう参照を解放する
        _routine = null;
        isRunning = false;
        currentFingerIndex = -1;
        currentServoValue = 0;

        if (serialSender != null)
        {
            EnsureScratchTargets();
            GetSweepRangeAscending(out int start, out _, out _);
            EnsureHoldTargets();

            if (_wasCancelled)
            {
                // キャンセル時は全指 start
                for (int i = 0; i < FingerCount; i++) _scratchTargets[i] = start;
                serialSender.TrySendNow(_scratchTargets, bypassGuards: true);
            }
            else
            {
                // 終了時は、保存済み指は released を維持し、未保存は start に戻す
                for (int i = 0; i < FingerCount; i++)
                {
                    if (calibrationState != null && calibrationState.TryGetReleasedServoValue(i, out int released))
                    {
                        _holdTargets[i] = released;
                    }
                    else
                    {
                        _holdTargets[i] = start;
                    }
                }
                serialSender.TrySendNow(_holdTargets, bypassGuards: true);
                ApplyHoldTargetsToSender();
            }

            serialSender.enableSend = _previousSenderEnableSend;
        }
    }

    private int GetSensorRawSafe(int finger)
    {
        if (curlTracker == null || curlTracker.sensorRaw == null || curlTracker.sensorRaw.Length != FingerCount)
        {
            return 0;
        }

        if (finger < 0 || finger >= FingerCount) return 0;
        return curlTracker.sensorRaw[finger];
    }

    private void EnsureScratchTargets()
    {
        if (_scratchTargets == null || _scratchTargets.Length != FingerCount)
        {
            _scratchTargets = new int[FingerCount];
        }
    }

    private void EnsureHoldTargets()
    {
        if (_holdTargets == null || _holdTargets.Length != FingerCount)
        {
            _holdTargets = new int[FingerCount];
        }
    }

    private void ApplyHoldTargetsToSender()
    {
        if (serialSender == null) return;
        if (serialSender.currentFingerTargets == null || serialSender.currentFingerTargets.Length != FingerCount)
        {
            serialSender.currentFingerTargets = new int[FingerCount];
        }

        for (int i = 0; i < FingerCount; i++)
        {
            serialSender.currentFingerTargets[i] = _holdTargets[i];
        }
    }

    private void GetSweepRangeAscending(out int start, out int end, out int step)
    {
        step = Mathf.Max(1, stepSize);
        start = Clamp1000(startServoValue);
        end = Clamp1000(endServoValue);
        if (end < start)
        {
            int tmp = start;
            start = end;
            end = tmp;
        }
    }

    private static int Clamp1000(int v)
    {
        return v < 0 ? 0 : (v > 1000 ? 1000 : v);
    }

    private static string FingerNameJapanese(int index)
    {
        switch (index)
        {
            case 0: return "親指";
            case 1: return "人差し指";
            case 2: return "中指";
            case 3: return "薬指";
            case 4: return "小指";
            default: return "?";
        }
    }
}
