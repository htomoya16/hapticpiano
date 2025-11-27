using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using Valve.VR;
using Valve.VR.InteractionSystem;

// Force Feedback 全体を管理するクラス。
// ・シーン内の Interactable に FFBClient をばらまく
// ・指の曲がり具合から FFB 用の値（0〜1000）を計算する
// ・左右それぞれの Named Pipe に値を送る
public class FFBManager : MonoBehaviour
{
    // シーン内に存在する Interactable の一覧
    private Interactable[] _interactables;

    // 左手／右手それぞれの Force Feedback プロバイダ
    private FFBProvider _ffbProviderLeft;
    private FFBProvider _ffbProviderRight;

    // デバッグ用: 最後に計算した指 curl 値 (0〜1000) を保存
    // [0]=親指, [1]=人差し指, ... [4]=小指
    [SerializeField] public short[] lastLeftFingerCurl  = new short[5];
    [SerializeField] public short[] lastRightFingerCurl = new short[5];

    // true の場合、Awake 時に全 Interactable に FFBClient コンポーネントを自動付与する
    public bool injectFfbProvider = true;

    private void Awake()
    {
        // 左右のコントローラ役割に対して FFBProvider を生成
        _ffbProviderLeft = new FFBProvider(ETrackedControllerRole.LeftHand);
        _ffbProviderRight = new FFBProvider(ETrackedControllerRole.RightHand);

        if (injectFfbProvider)
        {
            // シーン内の全 Interactable を検索
            _interactables = GameObject.FindObjectsOfType<Interactable>();

            // それぞれに FFBClient をアタッチすることで、
            // 手がホバーしたときに Force Feedback をトリガーできるようにする
            foreach (Interactable interactable in _interactables)
            {
                interactable.gameObject.AddComponent<FFBClient>();
            }
        }

        int count = _interactables != null ? _interactables.Length : 0;
        Debug.Log("Found: " + count + " Interactables");
    }

    // 実際に Force Feedback 値を左右どちらの手に送るかを振り分ける内部メソッド
    private void _SetForceFeedback(Hand hand, VRFFBInput input)
    {
        // handType を見て左手か右手か判定し、対応する FFBProvider に送信
        if (hand.handType == SteamVR_Input_Sources.LeftHand)
        {
            _ffbProviderLeft.SetFFB(input);
        }
        else
        {
            _ffbProviderRight.SetFFB(input);
        }
    }

    // Skeleton（指のボーン回転）から各指のカール量を推定し、
    // それを Force Feedback 用の値（0〜1000）に変換して送る。
    //
    // このメソッドがデフォルトの「FFB計算ロジック」であり、
    // FFBClient がホバーイベントを受けたときにここを呼んでいる。
    public void SetForceFeedbackFromSkeleton(Hand hand, SteamVR_Skeleton_Pose_Hand skeleton)
    {
        SteamVR_Skeleton_Pose_Hand openHand;   // 開いた手の基準ポーズ
        SteamVR_Skeleton_Pose_Hand closedHand; // グー（握りこぶし）の基準ポーズ

        // 左手／右手で使う参照ポーズを切り替える。
        if (hand.handType == SteamVR_Input_Sources.LeftHand)
        {
            // Resources フォルダ内の "ReferencePose_OpenHand" / "ReferencePose_Fist" から左手用ポーズを取得
            openHand = ((SteamVR_Skeleton_Pose)Resources.Load("ReferencePose_OpenHand")).leftHand;
            closedHand = ((SteamVR_Skeleton_Pose)Resources.Load("ReferencePose_Fist")).leftHand;
        }
        else
        {
            // 右手用ポーズ
            openHand = ((SteamVR_Skeleton_Pose)Resources.Load("ReferencePose_OpenHand")).rightHand;
            closedHand = ((SteamVR_Skeleton_Pose)Resources.Load("ReferencePose_Fist")).rightHand;
        }

        // 指ごと（親指〜小指）のカール値一覧を入れる配列（中身は List<float>）。
        // fingerCurlValues[0] = 親指に属するボーンの curl 値リスト、というイメージ。
        List<float>[] fingerCurlValues = new List<float>[5];

        for (int i = 0; i < fingerCurlValues.Length; i++)
            fingerCurlValues[i] = new List<float>();

        // Skeleton に含まれる全ボーンを走査し、それぞれのボーンの「開き→現在」「開き→グー」の角度差を計算する。
        for (int boneIndex = 0; boneIndex < skeleton.bonePositions.Length; boneIndex++)
        {
            // 1. 開いた手の回転から、現在のポーズ（skeleton）までの角度差
            float openToPoser = Quaternion.Angle(
                openHand.boneRotations[boneIndex],
                skeleton.boneRotations[boneIndex]
            );

            // 2. 開いた手の回転から、グーの回転までの角度差
            float openToClosed = Quaternion.Angle(
                openHand.boneRotations[boneIndex],
                closedHand.boneRotations[boneIndex]
            );

            // 3. 「グーに対してどれだけ近づいているか」を 0〜1 の比率で表現
            //    0   = 開いている
            //    1   = 完全にグー
            float curl = openToPoser / openToClosed;

            // このボーンがどの指に属するかを取得（親指〜小指のインデックス）
            int finger = SteamVR_Skeleton_JointIndexes.GetFingerForBone(boneIndex);

            // curl が NaN でない / 0 でない / 有効な指インデックスである場合のみ採用
            if (!float.IsNaN(curl) && curl != 0 && finger >= 0)
            {
                // 指ごとのリストに値を追加（後で平均を取る）
                fingerCurlValues[finger].Add(curl);
            }
        }

        // 0〜1000 の範囲で指ごとの平均 curl 値を保持する配列
        short[] fingerCurlAverages = new short[5];

        for (int i = 0; i < 5; i++)
        {
            float enumerator = 0;

            // 指 i に属する全ボーンの curl の合計を計算
            for (int j = 0; j < fingerCurlValues[i].Count; j++)
            {
                enumerator += fingerCurlValues[i][j];
            }

            // 平均値を算出（0〜1 の範囲を想定）
            float average = (fingerCurlValues[i].Count > 0)
                ? (enumerator / fingerCurlValues[i].Count)
                : 0f;

            // Force Feedback 側では「0 = 完全に自由」「1000 = 最大拘束」という解釈にしたいので、
            // 0〜1000 にスケーリングしたうえで反転している。
            fingerCurlAverages[i] = Convert.ToInt16(
                1000 - (Mathf.FloorToInt(average * 1000))
            );

            Debug.Log(fingerCurlAverages[i]);
        }

        // デバッグ用に、最後に計算した curl を保存しておく
        if (hand.handType == SteamVR_Input_Sources.LeftHand)
        {
            for (int i = 0; i < 5; i++)
            {
                lastLeftFingerCurl[i] = fingerCurlAverages[i];
            }
        }
        else // 右手
        {
            for (int i = 0; i < 5; i++)
            {
                lastRightFingerCurl[i] = fingerCurlAverages[i];
            }
        }

        // 親指〜小指の 5 本分の値を VRFFBInput に詰めて送信
        _SetForceFeedback(
            hand,
            new VRFFBInput(
                fingerCurlAverages[0],
                fingerCurlAverages[1],
                fingerCurlAverages[2],
                fingerCurlAverages[3],
                fingerCurlAverages[4]
            )
        );
    }

    // Force Feedback を完全に抜く（全指 0）ユーティリティ。
    // 手がホバーをやめて何も掴んでいないときに呼ばれる。
    public void RelaxForceFeedback(Hand hand)
    {
        VRFFBInput input = new VRFFBInput(0, 0, 0, 0, 0);
        _SetForceFeedback(hand, input);
    }

    // すでに「指ごとの curl 値」が計算済みの場合に、
    // そのまま Force Feedback を設定するためのメソッド。
    // ピアノ用に自前でカール値を計算したい場合は、こちらを直接使うイメージ。
    public void SetForceFeedbackByCurl(Hand hand, VRFFBInput input)
    {
        _SetForceFeedback(hand, input);
    }

    // FFBProvider のクリーンアップ処理
    private void Stop()
    {
        _ffbProviderLeft.Close();
        _ffbProviderRight.Close();
    }

    private void OnApplicationQuit()
    {
        // アプリ終了時にパイプを閉じる
        Stop();
    }

    private void OnDestroy()
    {
        // オブジェクト破棄時にも念のためクローズ
        Stop();
    }
}
