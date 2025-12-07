using System.Collections;
using UnityEngine;
using Valve.VR.InteractionSystem;

/// <summary>
/// 各手にアタッチして、LucidGloves のセンサ値から
/// 指ごとの curl を生成・保持するコンポーネントである。
/// </summary>
public class HandCurlTracker : MonoBehaviour
{
    private const int FingerCount = 5;

    [Header("References")]
    public Hand hand; // 左右どちらの手か（主に関連コンポーネント探索用）

    [Header("Sensor (Raw)")]
    [Tooltip("シリアルから受け取った指ごとの生センサ値（A〜E）")]
    [SerializeField] public int[] sensorRaw = new int[FingerCount];

    [Header("Curl Values")]
    [Tooltip("フィルタ前の curl 値（0〜1, 親指〜小指）")]
    [SerializeField] public float[] curlRaw = new float[FingerCount];

    [Tooltip("フィルタ後の curl 値（0〜1, 親指〜小指）")]
    [SerializeField] public float[] curl01 = new float[FingerCount];

    [Tooltip("ForceFeedback 用に 0〜1000 に変換した curl 値（親指〜小指）")]
    [SerializeField] public short[] curlFfb = new short[FingerCount];

    [Header("Filtering")]
    [Tooltip("true のとき curlRaw にローパスフィルタをかけて curl01 を更新する。false ならフィルタなし。")]
    public bool useFiltering = true;

    [Range(0f, 1f)]
    [Tooltip("curlRaw から curl01 への追従係数（大きいほど追従が速くノイズ低減が弱い）。")]
    public float filterAlpha = 0.25f;

    [Range(0f, 1f)]
    [Tooltip("curlRaw と curl01 の差がこの値を超えたとき、一時的に強く追従させるスナップ閾値。")]
    public float snapThreshold = 0.35f;

    [Range(0f, 1f)]
    [Tooltip("curlRaw と curl01 の差がこの値未満のときはノイズとして無視する（値を保持する）。")]
    public float noiseThreshold = 0.01f;

    [Header("Preset (optional)")]
    [Tooltip("左右で共有する HandCurlTracker 用のフィルタ設定プリセット")]
    public HandCurlTrackerPreset preset;

    [Tooltip("Awake 時に preset の値を適用するかどうか")]
    public bool applyPresetOnAwake = true;

    private void Awake()
    {
        // Hand 参照を確保（必要なら他コンポーネントから利用する想定）
        if (hand == null)
        {
            hand = GetComponent<Hand>();
        }

        if (hand == null)
        {
            Debug.LogWarning("[HandCurlTracker] Hand がアタッチされていない。");
        }

        EnsureSensorArrays();
        EnsureCurlArrays();

        // 左右で共有するプリセットを適用
        if (preset != null && applyPresetOnAwake)
        {
            ApplyPreset(preset);
        }
    }

    private void Update()
    {
        // LucidGloves のセンサ値から curl を更新する
        UpdateFromSensor();
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
    public void ApplyPreset(HandCurlTrackerPreset p)
    {
        if (p == null) return;

        useFiltering = p.useFiltering;
        filterAlpha = p.filterAlpha;
        snapThreshold = p.snapThreshold;
        noiseThreshold = p.noiseThreshold;
    }

    private void UpdateFromSensor()
    {
        EnsureCurlArrays();
        EnsureSensorArrays();

        for (int i = 0; i < FingerCount; i++)
        {
            int raw = sensorRaw[i];

            // フィルタ前の curl（0〜1）
            float cRaw = NormalizeCurl(raw);
            curlRaw[i] = cRaw;

            float cFiltered;

            if (!useFiltering)
            {
                // フィルタ無効時はそのまま通す
                cFiltered = cRaw;
            }
            else
            {
                float previous = curl01[i];

                // 初期値や NaN の場合は生値にリセット
                if (float.IsNaN(previous) || previous < 0f || previous > 1f)
                {
                    previous = cRaw;
                }

                float alpha = Mathf.Clamp01(filterAlpha);
                float delta = Mathf.Abs(cRaw - previous);

                // ごく小さな変化はノイズとして無視（値をそのまま保持）
                float noise = Mathf.Clamp01(noiseThreshold);
                if (delta < noise)
                {
                    cFiltered = previous;
                }
                else
                {
                    // 大きなステップ変化は一時的に強く追従させる
                    if (delta > Mathf.Clamp01(snapThreshold))
                    {
                        alpha = 1f;
                    }

                    cFiltered = Mathf.Lerp(previous, cRaw, alpha);
                }
            }

            cFiltered = Mathf.Clamp01(cFiltered);
            curl01[i] = cFiltered;

            // FFB 側と互換性を保つため、
            // 1000 = 指が開いている / 0 = 完全に握っている、という向きに反転する。
            short ffbValue = (short)(1000 - Mathf.RoundToInt(cFiltered * 1000f));
            curlFfb[i] = ffbValue;
        }
    }

    private void EnsureCurlArrays()
    {
        if (curlRaw == null || curlRaw.Length != FingerCount)
        {
            curlRaw = new float[FingerCount];
        }

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
    }

    private float NormalizeCurl(int raw)
    {
        // ESP 側で 0〜4095 にキャリブ済みの前提で 0〜1 に正規化
        float c = raw / 4095.0f;
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

    // Skeleton 依存のリセット処理は削除し、
    // curl01 / curlFfb は UpdateFromSensor 側で常に上書き更新する方針とする。
}
