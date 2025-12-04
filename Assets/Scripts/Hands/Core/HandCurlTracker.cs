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

    [Header("Debug Values")]
    // 0〜1 に正規化した curl 値（親指〜小指）
    [SerializeField] public float[] curl01 = new float[FingerCount];
    // ForceFeedback 用に 0〜1000 に変換した curl 値（親指〜小指）
    [SerializeField] public short[] curlFfb = new short[FingerCount];

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
    public void ApplyPreset(ScriptableObject p) { /* no-op (preset removed) */ }

    private void UpdateFromSensor()
    {
        EnsureCurlArrays();
        EnsureSensorArrays();

        for (int i = 0; i < FingerCount; i++)
        {
            int raw = sensorRaw[i];

            float c = NormalizeCurl(raw);
            curl01[i] = c;

            // FFB 側と互換性を保つため、
            // 1000 = 指が開いている / 0 = 完全に握っている、という向きに反転する。
            short ffbValue = (short)(1000 - Mathf.RoundToInt(c * 1000f));
            curlFfb[i] = ffbValue;
        }
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
