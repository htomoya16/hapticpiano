using UnityEngine;
using Valve.VR.InteractionSystem;

// 各 Interactable にアタッチされ、ホバー開始/終了をトリガーに FFB を送るクライアントである。
public class FFBClient : MonoBehaviour
{
    private FFBManager _ffbManager;

    private void Awake()
    {
        _ffbManager = FindObjectOfType<FFBManager>();

        if (_ffbManager == null)
        {
            Debug.LogError("[FFBClient] シーン内に FFBManager が見つからない。");
        }
    }

    // 手がこのオブジェクト上にホバーし始めたとき
    private void OnHandHoverBegin(Hand hand)
    {
        if (_ffbManager == null)
        {
            return;
        }

        Debug.Log("[FFBClient] Hand hover begin.");

        // 手の GameObject から HandCurlTracker を取得
        var curlTracker = hand.GetComponent<HandCurlTracker>();
        if (curlTracker == null)
        {
            Debug.LogWarning("[FFBClient] HandCurlTracker が Hand に見つからない。Force Feedback は送信しない。");
            return;
        }

        // 現在の curl から FFB 入力を構築
        VRFFBInput input = curlTracker.GetCurrentFfbInput();

        // curl 値をそのまま FFBManager に渡して送信
        _ffbManager.SetForceFeedbackByCurl(hand, input);
    }

    // 手がこのオブジェクトからホバーをやめたとき
    private void OnHandHoverEnd(Hand hand)
    {
        if (_ffbManager == null)
        {
            return;
        }

        // 何か掴んでいる場合は掴み側のロジックに任せる
        if (!hand.currentAttachedObject)
        {
            // 何も掴んでいないなら力を抜く
            _ffbManager.RelaxForceFeedback(hand);
        }
    }
}
