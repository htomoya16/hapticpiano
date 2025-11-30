using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

/// <summary>
/// 共通パラメータを左右で共有するためのプリセット。
/// HandVisualFromCurl でも使用する。
/// </summary>
[CreateAssetMenu(menuName = "HapticPiano/HandModelPreset")]
public class HandModelPreset : ScriptableObject
{
    [Header("Curl Processing")]
    [Range(0f, 0.3f)] public float deadZone = 0.05f;
    [Range(0.5f, 3.0f)] public float gain = 1.4f;
    [Range(0f, 1f)] public float smoothingFactor = 0.25f;

    [Header("Visual Angles (deg)")]
    public float thumbMaxAngle = 45f;
    public float indexMaxAngle = 70f;
    public float middleMaxAngle = 70f;
    public float ringMaxAngle = 70f;
    public float pinkyMaxAngle = 70f;

    [Header("Visual Curve")]
    [Range(0f, 0.5f)] public float visualDeadZone = 0.05f;
    [Range(0.5f, 3f)] public float visualGamma = 1.2f;
}

// 各手にアタッチして、毎フレーム fingerCurls から curl を計算・保持するコンポーネントである。
public class HandCurlTracker : MonoBehaviour
{
    [Header("References")]
    public Hand hand;                                  // 左右どちらの手か
    private SteamVR_Behaviour_Skeleton skeleton;        // 対応する Skeleton

    // この HandCurlTracker に紐づいている SteamVR_Behaviour_Skeleton への公開用プロパティ
    public SteamVR_Behaviour_Skeleton Skeleton => skeleton;

    [Header("Tuning")]
    [Tooltip("これ未満の小さな曲がりは 0 とみなす")]
    [Range(0f, 0.3f)]
    public float deadZone = 0.05f;

    [Tooltip("1 より大きいと、序盤の変化が緩やかになり、後半だけ曲がりやすくなる")]
    [Range(0.5f, 3.0f)]
    public float gain = 1.4f;

    [Header("Smoothing")]
    [Tooltip("true なら指数移動平均で滑らかにする")]
    public bool smoothingEnabled = true;
    [Range(0f, 1f)]
    [Tooltip("1 に近いほど即時反映、0 に近いほど強く平滑化")]
    public float smoothingFactor = 0.25f;

    [Header("Debug Values (Read Only)")]
    // 0〜1 に正規化した curl 値（親指〜小指）
    [SerializeField] public float[] curl01 = new float[5];
    // ForceFeedback 用に 0〜1000 に変換した curl 値（親指〜小指）
    [SerializeField] public short[] curlFfb = new short[5];

    [Header("Preset (optional)")]
    [Tooltip("左右で共有するパラメータプリセット。指定時は Awake で適用される。")]
    public HandModelPreset preset;
    public bool applyPresetOnAwake = true;

    [Header("Safety")]
    [Tooltip("Skeleton が未取得のフレームで curl を 0 に戻す")]
    public bool zeroWhenUntracked = true;

    private void Awake()
    {
        // プリセット適用（共有値で左右整合を取りやすくする）
        if (applyPresetOnAwake && preset != null)
        {
            ApplyPreset(preset);
        }

        // Hand 参照を確保
        if (hand == null)
        {
            hand = GetComponent<Hand>();
        }

        // Hand が持っている skeleton を使う
        if (skeleton == null && hand != null && hand.skeleton != null)
        {
            skeleton = hand.skeleton;
            // Debug.Log($"[HandCurlTracker] Hand.skeleton から Skeleton を取得: {skeleton.name}");
        }

        if (hand == null)
        {
            Debug.LogWarning("[HandCurlTracker] Hand がアタッチされていない。");
        }

        if (skeleton == null)
        {
            Debug.LogWarning("[HandCurlTracker] SteamVR_Behaviour_Skeleton が見つからない。curl は更新されない。");
        }
    }

    private void Update()
    {
        if (skeleton == null || skeleton.skeletonAction == null)
        {
            var fromHand = hand.skeleton; // ← さっきのプロパティ
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
        if (curls == null || curls.Length < 5)
        {
            return;
        }

        for (int i = 0; i < 5; i++)
        {
            float c = Mathf.Clamp01(curls[i]);

            // デッドゾーン処理
            if (c < deadZone)
            {
                c = 0f;
            }
            else
            {
                // deadZone から 1.0 の範囲を 0〜1 に再マッピング
                c = Mathf.InverseLerp(deadZone, 1f, c);
            }

            // ゲイン（非線形カーブ）適用
            if (gain != 1f)
            {
                c = Mathf.Pow(c, gain);
            }

            // 平滑化（指数移動平均）
            if (smoothingEnabled)
            {
                c = Mathf.Lerp(curl01[i], c, smoothingFactor);
            }

            // デバッグ用の 0〜1
            curl01[i] = c;

            // FFB 側と互換性を保つため、
            // 1000 = 指が開いている / 0 = 完全に握っている、という向きに反転する。
            short ffbValue = (short)(1000 - Mathf.RoundToInt(c * 1000f));
            curlFfb[i] = ffbValue;
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
    public void ApplyPreset(HandModelPreset p)
    {
        if (p == null) return;
        deadZone = p.deadZone;
        gain = p.gain;
        smoothingFactor = p.smoothingFactor;
    }

    private void ResetCurlValues()
    {
        for (int i = 0; i < 5; i++)
        {
            curl01[i] = 0f;
            curlFfb[i] = 1000; // 開いた状態
        }
    }
}
