using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

// 各手にアタッチして、毎フレーム fingerCurls から curl を計算・保持するコンポーネントである。
public class HandCurlTracker : MonoBehaviour
{
    [Header("References")]
    public Hand hand;                                  // 左右どちらの手か
    public SteamVR_Behaviour_Skeleton skeleton;        // 対応する Skeleton

    [Header("Tuning")]
    [Tooltip("これ未満の小さな曲がりは 0 とみなす")]
    [Range(0f, 0.3f)]
    public float deadZone = 0.05f;

    [Tooltip("1 より大きいと、序盤の変化が緩やかになり、後半だけ曲がりやすくなる")]
    [Range(0.5f, 3.0f)]
    public float gain = 1.4f;

    [Header("Debug Values (Read Only)")]
    // 0〜1 に正規化した curl 値（親指〜小指）
    [SerializeField] public float[] curl01 = new float[5];
    // ForceFeedback 用に 0〜1000 に変換した curl 値（親指〜小指）
    [SerializeField] public short[] curlFfb = new short[5];

    private void Awake()
    {
        if (hand == null)
        {
            hand = GetComponent<Hand>();
        }

        if (skeleton == null)
        {
            skeleton = GetComponent<SteamVR_Behaviour_Skeleton>();
        }

        if (hand == null)
        {
            Debug.LogWarning("[HandCurlTracker] Hand がアタッチされていない。");
        }

        if (skeleton == null)
        {
            Debug.LogWarning("[HandCurlTracker] SteamVR_Behaviour_Skeleton がアタッチされていない。");
        }
    }

    private void Update()
    {
        if (skeleton == null || skeleton.skeletonAction == null)
        {
            return;
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
}
