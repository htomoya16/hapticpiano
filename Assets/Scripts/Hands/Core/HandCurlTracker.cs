using System.Collections;
using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

/// <summary>
/// 各手にアタッチして、LucidGloves のセンサ値
/// （またはオプションで SteamVR Skeleton の fingerCurls）から
/// 指ごとの curl を生成・保持するコンポーネントである。
/// </summary>
public class HandCurlTracker : MonoBehaviour
{
    private const int FingerCount = 5;

    [Header("References")]
    public Hand hand;                                   // 左右どちらの手か
    private SteamVR_Behaviour_Skeleton skeleton;        // 対応する Skeleton（旧入力経路）

    // この HandCurlTracker に紐づいている SteamVR_Behaviour_Skeleton への公開用プロパティ
    public SteamVR_Behaviour_Skeleton Skeleton => skeleton;

    [Header("Input Source")]
    [Tooltip("true の場合は SteamVR Skeleton の fingerCurl を使用する（旧モード）。false の場合はシリアルセンサ入力を使用する。")]
    public bool useSteamVRSkeleton = false;

    [Header("Sensor (Raw / Calibration)")]
    [Tooltip("シリアルから受け取った指ごとの生センサ値（A〜E）")]
    [SerializeField] public int[] sensorRaw = new int[FingerCount];
    [Tooltip("ホームポジション時の生センサ値")]
    [SerializeField] public int[] sensorRest = new int[FingerCount];
    [Tooltip("最大押し込み時の生センサ値")]
    [SerializeField] public int[] sensorPress = new int[FingerCount];

    [Header("Debug Values")]
    // 0〜1 に正規化した curl 値（親指〜小指）
    [SerializeField] public float[] curl01 = new float[FingerCount];
    // ForceFeedback 用に 0〜1000 に変換した curl 値（親指〜小指）
    [SerializeField] public short[] curlFfb = new short[FingerCount];

    [Header("Calibration Settings")]
    [Tooltip("キャリブレーション時に平均を取る秒数")]
    public float calibrationDuration = 0.8f;
    [Tooltip("キャリブレーションのログを出力するかどうか")]
    public bool logCalibration = true;

    [Header("Safety")]
    [Tooltip("Skeleton が未取得のフレームで curl を 0 に戻す（SteamVR モード時のみ有効）")]
    public bool zeroWhenUntracked = true;

    private Coroutine calibrationRoutine;

    private void Awake()
    {
        // Hand 参照を確保
        if (hand == null)
        {
            hand = GetComponent<Hand>();
        }

        // Hand が持っている skeleton を使う（旧入力経路用）
        if (skeleton == null && hand != null && hand.skeleton != null)
        {
            skeleton = hand.skeleton;
            // Debug.Log($"[HandCurlTracker] Hand.skeleton から Skeleton を取得: {skeleton.name}");
        }

        if (hand == null)
        {
            Debug.LogWarning("[HandCurlTracker] Hand がアタッチされていない。");
        }

        if (useSteamVRSkeleton && skeleton == null)
        {
            Debug.LogWarning("[HandCurlTracker] SteamVR_Behaviour_Skeleton が見つからない。curl は更新されない。");
        }

        EnsureSensorArrays();
        EnsureCurlArrays();
    }

    private void Update()
    {
        if (useSteamVRSkeleton)
        {
            UpdateFromSkeleton();
        }
        else
        {
            UpdateFromSensor();
        }
    }

    /// <summary>
    /// LucidGloves から届いた 1 フレーム分のエンコード文字列
    /// （例: A2301B2391C3431D3313E1234）をパースして sensorRaw を更新する。
    /// シリアル受信側のコンポーネントから呼び出すことを想定。
    /// </summary>
    public void UpdateSensorFromEncodedString(string encoded)
    {
        if (string.IsNullOrEmpty(encoded))
        {
            return;
        }

        EnsureSensorArrays();

        int length = encoded.Length;
        int index = 0;

        while (index < length)
        {
            char c = encoded[index];
            int fingerIndex = FingerCharToIndex(c);
            if (fingerIndex < 0)
            {
                index++;
                continue;
            }

            // 直後の数字列を取得
            index++;
            int start = index;
            while (index < length && char.IsDigit(encoded[index]))
            {
                index++;
            }

            if (index == start)
            {
                // 数字が 1 桁も無い場合はスキップ
                continue;
            }

            string numberStr = encoded.Substring(start, index - start);
            if (int.TryParse(numberStr, out int value))
            {
                sensorRaw[fingerIndex] = value;
            }
        }
    }

    /// <summary>
    /// ホームポジション用のキャリブレーションを開始する（UI ボタンから呼び出し想定）。
    /// 一定時間 sensorRaw をサンプリングし、平均値を sensorRest に保存する。
    /// </summary>
    public void StartCalibrateRest()
    {
        StartCalibration(CalibrationMode.Rest);
    }

    /// <summary>
    /// 最大押し込み用のキャリブレーションを開始する（UI ボタンから呼び出し想定）。
    /// 一定時間 sensorRaw をサンプリングし、平均値を sensorPress に保存する。
    /// </summary>
    public void StartCalibratePress()
    {
        StartCalibration(CalibrationMode.Press);
    }

    // 現在の curlFfb から VRFFBInput を構築して返すヘルパーである。
    public VRFFBInput GetCurrentFfbInput()
    {
        return new VRFFBInput(
            curlFfb[0],
            curlFfb[1],
            curlFfb[2],
            curlFfb[3],
            curlFfb[4]
        );
    }

    // プリセット適用ヘルパー（左右共通値で揃える）
    public void ApplyPreset(ScriptableObject p) { /* no-op (preset removed) */ }

    private void UpdateFromSensor()
    {
        EnsureCurlArrays();
        EnsureSensorArrays();

        for (int i = 0; i < FingerCount; i++)
        {
            int raw = sensorRaw[i];
            int rest = sensorRest[i];
            int press = sensorPress[i];

            float c = NormalizeCurl(raw, rest, press);
            curl01[i] = c;

            // FFB 側と互換性を保つため、
            // 1000 = 指が開いている / 0 = 完全に握っている、という向きに反転する。
            short ffbValue = (short)(1000 - Mathf.RoundToInt(c * 1000f));
            curlFfb[i] = ffbValue;
        }
    }

    private void UpdateFromSkeleton()
    {
        if (skeleton == null || skeleton.skeletonAction == null)
        {
            var fromHand = hand != null ? hand.skeleton : null;
            if (fromHand != null)
            {
                skeleton = fromHand;
                // Debug.Log($"[HandCurlTracker] Hand.skeleton から Skeleton を取得: {skeleton.name}");
            }
            if (skeleton == null)
            {
                // まだ null なら安全のためゼロリセットして終了
                if (zeroWhenUntracked)
                {
                    ResetCurlValues();
                }
                return;
            }
        }

        // SteamVR_Behaviour_Skeleton から fingerCurls（0〜1）を毎フレーム取得
        float[] curls = skeleton.skeletonAction.fingerCurls; // 親指〜小指 5 本分 (0〜1)
        if (curls == null || curls.Length < FingerCount)
        {
            return;
        }

        EnsureCurlArrays();

        for (int i = 0; i < FingerCount; i++)
        {
            float c = Mathf.Clamp01(curls[i]);

            // デバッグ用の 0〜1
            curl01[i] = c;

            // FFB 側と互換性を保つため、
            // 1000 = 指が開いている / 0 = 完全に握っている、という向きに反転する。
            short ffbValue = (short)(1000 - Mathf.RoundToInt(c * 1000f));
            curlFfb[i] = ffbValue;
        }
    }

    private void StartCalibration(CalibrationMode mode)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        if (calibrationRoutine != null)
        {
            StopCoroutine(calibrationRoutine);
        }
        calibrationRoutine = StartCoroutine(CalibrationCoroutine(mode));
    }

    private IEnumerator CalibrationCoroutine(CalibrationMode mode)
    {
        EnsureSensorArrays();

        if (logCalibration)
        {
            Debug.Log($"[HandCurlTracker] {mode} calibration started on {gameObject.name}.");
        }

        int[] accum = new int[FingerCount];
        int sampleCount = 0;
        float elapsed = 0f;

        while (elapsed < calibrationDuration)
        {
            for (int i = 0; i < FingerCount; i++)
            {
                accum[i] += sensorRaw[i];
            }

            sampleCount++;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (sampleCount > 0)
        {
            for (int i = 0; i < FingerCount; i++)
            {
                int avg = accum[i] / sampleCount;
                if (mode == CalibrationMode.Rest)
                {
                    sensorRest[i] = avg;
                }
                else
                {
                    sensorPress[i] = avg;
                }
            }
        }

        if (logCalibration)
        {
            Debug.Log($"[HandCurlTracker] {mode} calibration finished. samples={sampleCount}.");
        }

        calibrationRoutine = null;
    }

    private enum CalibrationMode
    {
        Rest,
        Press
    }

    private void EnsureCurlArrays()
    {
        if (curl01 == null || curl01.Length != FingerCount)
        {
            curl01 = new float[FingerCount];
        }

        if (curlFfb == null || curlFfb.Length != FingerCount)
        {
            curlFfb = new short[FingerCount];
        }
    }

    private void EnsureSensorArrays()
    {
        if (sensorRaw == null || sensorRaw.Length != FingerCount)
        {
            sensorRaw = new int[FingerCount];
        }

        if (sensorRest == null || sensorRest.Length != FingerCount)
        {
            sensorRest = new int[FingerCount];
        }

        if (sensorPress == null || sensorPress.Length != FingerCount)
        {
            sensorPress = new int[FingerCount];
        }
    }

    private float NormalizeCurl(int raw, int rest, int press)
    {
        int denominator = press - rest;
        if (denominator <= 0)
        {
            // キャリブレーションが未完了 or 異常値の場合は 0 とみなす
            return 0f;
        }

        float c = (raw - rest) / (float)denominator;
        return Mathf.Clamp01(c);
    }

    private int FingerCharToIndex(char c)
    {
        switch (char.ToUpperInvariant(c))
        {
            case 'A': return 0; // Thumb
            case 'B': return 1; // Index
            case 'C': return 2; // Middle
            case 'D': return 3; // Ring
            case 'E': return 4; // Pinky
            default:  return -1;
        }
    }

    private void ResetCurlValues()
    {
        EnsureCurlArrays();
        for (int i = 0; i < FingerCount; i++)
        {
            curl01[i] = 0f;
            curlFfb[i] = 1000; // 開いた状態
        }
    }
}
