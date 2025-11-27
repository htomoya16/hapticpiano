using UnityEngine;
using Valve.VR;
using Valve.VR.InteractionSystem;

// 各 Interactable（つかめる／触れるオブジェクト）にアタッチされる Force Feedback クライアント。
// 手がオブジェクトに「ホバー」したタイミングで、FFBManager に指の情報を渡す役割を担う。
public class FFBClient : MonoBehaviour
{
    // シーン内に1つだけ存在する Force Feedback 管理クラス
    private FFBManager _ffbManager;

    private void Awake()
    {
        // シーン内から FFBManager を探して保持しておく
        _ffbManager = GameObject.FindObjectOfType<FFBManager>();
    }

    // 手がこのオブジェクトの上にホバーし始めたときに呼ばれるコールバック
    // （SteamVR InteractionSystem のイベント）
    private void OnHandHoverBegin(Hand hand)
    {
        Debug.Log("Received Hand hover event");

        // このオブジェクトに設定されている SkeletonPose から、
        // 左右どちらの手で触っているかに応じて対応する Hand のポーズを取得する
        SteamVR_Skeleton_Pose_Hand skeletonPoseHand;

        if (hand.handType == SteamVR_Input_Sources.LeftHand)
        {
            // 左手でホバーしている場合：左手用ポーズを取得
            skeletonPoseHand = GetComponent<Interactable>().skeletonPoser.skeletonMainPose.leftHand;
        }
        else
        {
            // 右手でホバーしている場合：右手用ポーズを取得
            skeletonPoseHand = GetComponent<Interactable>().skeletonPoser.skeletonMainPose.rightHand;
        }

        // 取得した Skeleton 情報を FFBManager に渡し、
        // 指の曲がり具合から Force Feedback を計算・送信してもらう
        _ffbManager.SetForceFeedbackFromSkeleton(hand, skeletonPoseHand);
    }

    // 手がこのオブジェクトからホバーをやめたときに呼ばれるコールバック
    private void OnHandHoverEnd(Hand hand)
    {
        // もし現在この手で何かオブジェクトを「掴んでいる」なら、
        // Force Feedback はその掴み側で制御されるべきなので、ここでは何もしない。
        if (!hand.currentAttachedObject)
        {
            // 何も掴んでいない場合は、指の力を抜く（FFB を 0 にする）
            _ffbManager.RelaxForceFeedback(hand);
        }
    }
}
