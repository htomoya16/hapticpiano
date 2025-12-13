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
    /// デコード済みのセンサ値（A〜E, 0〜4095）を受け取り、sensorRaw を更新する。
    /// </summary>
    public void UpdateSensorFromDecodedValues(int[] decodedValues)
    {
        if (decodedValues == null || decodedValues.Length != FingerCount) return;

        EnsureSensorArrays();
        for (int i = 0; i < FingerCount; i++)
        {
            sensorRaw[i] = decodedValues[i];
        }
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

    // Skeleton 依存のリセット処理は削除し、
    // curl01 は UpdateFromSensor 側で常に上書き更新する方針とする。
}
