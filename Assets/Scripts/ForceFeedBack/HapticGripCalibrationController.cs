using System.Collections;
using UnityEngine;

/// <summary>
/// キャリブレーション（現行）:
/// - 「写真のように握った状態を維持していてください」を案内し、開始前にカウントダウンする
/// - 全指を同時に 0→1000 へ段階的に動かす（未確定の指のみ）
/// - 指ごとに sensorRaw が基準レンジ（±許容）から外れた瞬間の 1 ステップ前のサーボ値を released として保存する
/// - 指が確定したら、その指は即座に released を送って固定し、残り指のキャリブレーションを継続する
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
    [Tooltip("サーボ値のステップ幅（大きいほど速いが精度は落ちる）。キャリブレーションは常に 0→1000 をスイープする。")]
    public int stepSize = 20;

    [Tooltip("1ステップごとの待機（リアルタイム秒）。設定画面で TimeScale=0 でも動くように Realtime を使う")]
    public float stepIntervalSeconds = 0.05f;

    [Header("Detection (sensorRaw)")]
    [Tooltip("基準（握って力を抜いた状態）からの許容レンジ（±）。この範囲を外れたら『外れた』とみなす")]
    public int allowedBaselineDeviation = 120;

    [Tooltip("レンジ外が連続でこの回数続いたら確定する（ノイズ対策）")]
    public int requiredConsecutiveOutOfRange = 3;

    [Tooltip("基準値を取るときのサンプル数")]
    [HideInInspector]
    public int baselineSamples = 10;

    [Tooltip("基準値サンプルの間隔（リアルタイム秒）")]
    [HideInInspector]
    public float baselineSampleIntervalSeconds = 0.02f;

    [Header("Flow")]
    [Tooltip("開始前の案内カウントダウン秒数（10,9,...,1）。0 以下で無効。")]
    public int initialCountdownSeconds = 10;

    [Tooltip("開始時にキャリブレーション状態をリセットする（2回目以降の再キャリブ用）。")]
    public bool resetStateOnStart = true;

    [Tooltip("保存する released 値に加算するオフセット（+で 1000 側＝張る方向、-で 0 側＝ゆるむ方向）。保存時に 0-1000 にクランプされる。")]
    public int releasedValueOffset = -135;

    [Tooltip("スイープで変化が検知できない場合のタイムアウト（0以下で無効）")]
    [HideInInspector]
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

        ForceAllFingersToZero();
        statusMessage = "キャリブレーションをリセットし、全指 0 を送信しました。";
    }

    private void ForceAllFingersToZero()
    {
        if (IsRunning)
        {
            CancelCalibration();
        }

        EnsureHoldTargets();
        for (int i = 0; i < FingerCount; i++)
        {
            _holdTargets[i] = 0;
        }

        if (serialSender != null)
        {
            serialSender.TrySendNow(_holdTargets, bypassGuards: true);
            ApplyHoldTargetsToSender();
        }

        statusMessage = "全指を 0 にリセットしました。";
    }

    private IEnumerator CalibrationRoutine()
    {
        isRunning = true;
        _wasCancelled = false;
        currentFingerIndex = -1;
        currentServoValue = 0;
        statusMessage = "写真のように握った状態を維持していてください。";

        _previousSenderEnableSend = serialSender.enableSend;
        serialSender.enableSend = false;

        if (resetStateOnStart && calibrationState != null)
        {
            calibrationState.ResetAll();
        }

        int start = 0;
        int end = 1000;
        int step = Mathf.Max(1, stepSize);
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
                statusMessage = $"写真のように握った状態を維持していてください。\n開始まで {s}";
                yield return new WaitForSecondsRealtime(1f);
                if (_wasCancelled) yield break;
            }
        }

        yield return CalibrateAllFingersByOutOfRange(start, end, step);

        currentFingerIndex = -1;
        currentServoValue = 0;
        statusMessage = calibrationState.IsFullyCalibrated ? "キャリブレーション完了" : "キャリブレーション完了（未完了あり）";

        CleanupAfterRoutine();
    }

    private IEnumerator CalibrateAllFingersByOutOfRange(int start, int end, int step)
    {
        int samples = Mathf.Max(1, baselineSamples);
        float sampleInterval = Mathf.Max(0f, baselineSampleIntervalSeconds);

        // 基準（軽く握った状態）の sensorRaw を指ごとに取得
        var baseline = new int[FingerCount];
        var sums = new int[FingerCount];
        for (int s = 0; s < samples; s++)
        {
            for (int i = 0; i < FingerCount; i++)
            {
                sums[i] += GetSensorRawSafe(i);
            }

            if (sampleInterval > 0f)
            {
                yield return new WaitForSecondsRealtime(sampleInterval);
            }

            if (_wasCancelled) yield break;
        }

        for (int i = 0; i < FingerCount; i++)
        {
            baseline[i] = sums[i] / samples;
        }

        int dev = Mathf.Max(0, allowedBaselineDeviation);
        var min = new int[FingerCount];
        var max = new int[FingerCount];
        for (int i = 0; i < FingerCount; i++)
        {
            min[i] = baseline[i] - dev;
            max[i] = baseline[i] + dev;
        }

        int required = Mathf.Max(1, requiredConsecutiveOutOfRange);
        var outOfRangeCount = new int[FingerCount];
        var candidatePrev = new int[FingerCount];
        var prevServoValue = new int[FingerCount];

        EnsureHoldTargets();
        for (int i = 0; i < FingerCount; i++)
        {
            if (calibrationState != null && calibrationState.TryGetReleasedServoValue(i, out int released))
            {
                _holdTargets[i] = released;
                prevServoValue[i] = released;
            }
            else
            {
                _holdTargets[i] = start;
                prevServoValue[i] = start;
            }
            candidatePrev[i] = prevServoValue[i];
            outOfRangeCount[i] = 0;
        }

        float elapsed = 0f;

        for (int v = start; v <= end; v += step)
        {
            if (_wasCancelled) yield break;
            if (calibrationState != null && calibrationState.IsFullyCalibrated) break;
            if (timeoutSeconds > 0f && elapsed > timeoutSeconds) break;

            int clamped = Clamp1000(v);
            currentServoValue = clamped;

            // 未確定の指だけスイープ、確定済みは released を固定
            EnsureScratchTargets();
            for (int i = 0; i < FingerCount; i++)
            {
                if (calibrationState != null && calibrationState.TryGetReleasedServoValue(i, out int released))
                {
                    _scratchTargets[i] = released;
                }
                else
                {
                    _scratchTargets[i] = clamped;
                }
            }

            serialSender.TrySendNow(_scratchTargets, bypassGuards: true);

            if (stepIntervalSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(stepIntervalSeconds);
                elapsed += stepIntervalSeconds;
            }

            bool anySavedThisStep = false;
            for (int i = 0; i < FingerCount; i++)
            {
                if (calibrationState != null && calibrationState.TryGetReleasedServoValue(i, out _))
                {
                    continue;
                }

                int now = GetSensorRawSafe(i);
                bool outOfRange = now < min[i] || now > max[i];
                if (outOfRange)
                {
                    if (outOfRangeCount[i] == 0)
                    {
                        // レンジ外に出た瞬間の「1ステップ前」を候補として記録
                        candidatePrev[i] = prevServoValue[i];
                    }

                    outOfRangeCount[i]++;
                    if (outOfRangeCount[i] >= required)
                    {
                        int saved = Clamp1000(candidatePrev[i] + releasedValueOffset);
                        if (calibrationState != null)
                        {
                            calibrationState.SetReleasedServoValue(i, saved);
                        }

                        _holdTargets[i] = saved;
                        anySavedThisStep = true;
                        currentFingerIndex = i;

                        // 保存した瞬間、その指の released を反映（他指は現状維持）
                        for (int f = 0; f < FingerCount; f++)
                        {
                            if (calibrationState != null && calibrationState.TryGetReleasedServoValue(f, out int released))
                            {
                                _scratchTargets[f] = released;
                            }
                            else
                            {
                                _scratchTargets[f] = clamped;
                            }
                        }
                        serialSender.TrySendNow(_scratchTargets, bypassGuards: true);
                        ApplyHoldTargetsToSender();
                    }
                }
                else
                {
                    outOfRangeCount[i] = 0;
                }

                prevServoValue[i] = clamped;
            }

            int afterSavedCount = 0;
            if (calibrationState != null)
            {
                for (int i = 0; i < FingerCount; i++)
                {
                    if (calibrationState.TryGetReleasedServoValue(i, out _)) afterSavedCount++;
                }
            }

            if (anySavedThisStep)
            {
                statusMessage = $"写真のように握った状態を維持していてください。\n保存 {afterSavedCount}/{FingerCount}（最新: {FingerNameJapanese(currentFingerIndex)}）";
            }
            else
            {
                statusMessage = $"写真のように握った状態を維持していてください。\n進捗 {afterSavedCount}/{FingerCount}  現在 {clamped}";
            }
        }
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
            int start = 0;
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
