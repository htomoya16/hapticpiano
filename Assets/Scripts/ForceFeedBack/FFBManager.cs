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

    // すでに「指ごとの curl 値」が計算済みの場合に、
    // そのまま Force Feedback を設定するためのメソッド。
    public void SetForceFeedbackByCurl(Hand hand, VRFFBInput input)
    {
        _SetForceFeedback(hand, input);
    }

    // Force Feedback を完全に抜く（全指 0）ユーティリティ。
    // 手がホバーをやめて何も掴んでいないときに呼ばれる。
    public void RelaxForceFeedback(Hand hand)
    {
        VRFFBInput input = new VRFFBInput(0, 0, 0, 0, 0);
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
