using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

/// <summary>
// 各手にアタッチして、毎フレーム fingerCurls から curl を保持するコンポーネントである（加工なし）。
public class HandCurlTracker : MonoBehaviour
{
    [Header("References")]
    public Hand hand;                                  // 左右どちらの手か
    private SteamVR_Behaviour_Skeleton skeleton;        // 対応する Skeleton

    // この HandCurlTracker に紐づいている SteamVR_Behaviour_Skeleton への公開用プロパティ
    public SteamVR_Behaviour_Skeleton Skeleton => skeleton;

    [Header("Debug Values (Read Only)")]
    // 0〜1 に正規化した curl 値（親指〜小指）
    [SerializeField] public float[] curl01 = new float[5];
    // ForceFeedback 用に 0〜1000 に変換した curl 値（親指〜小指）
    [SerializeField] public short[] curlFfb = new short[5];

    [Header("Safety")]
    [Tooltip("Skeleton が未取得のフレームで curl を 0 に戻す")]
    public bool zeroWhenUntracked = true;

    private void Awake()
    {
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
    public void ApplyPreset(ScriptableObject p) { /* no-op (preset removed) */ }

    private void ResetCurlValues()
    {
        for (int i = 0; i < 5; i++)
        {
            curl01[i] = 0f;
            curlFfb[i] = 1000; // 開いた状態
        }
    }
}
