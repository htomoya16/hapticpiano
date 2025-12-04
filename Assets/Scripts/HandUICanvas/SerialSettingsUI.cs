using UnityEngine;
using TMPro;

/// <summary>
/// キーボード操作で開閉できる簡易シリアル設定パネル。
/// 左右それぞれの HandSerialInput の COM ポート名を
/// ランタイム中に変更して再接続できる。
/// </summary>
public class SerialSettingsUI : MonoBehaviour
{
    [Header("Toggle")]
    [Tooltip("このキーを押すと設定パネルの表示/非表示を切り替える")]
    public KeyCode toggleKey = KeyCode.F1;

    [Tooltip("設定パネルのルート GameObject")]
    public GameObject panelRoot;

    [Tooltip("通常時に表示しておくヒント（例: \"Press F1 to open COM settings\"）")]
    public GameObject hintRoot;

    [Header("Targets")]
    [Tooltip("左手側の HandSerialInput")]
    public HandSerialInput leftSerialInput;

    [Tooltip("右手側の HandSerialInput")]
    public HandSerialInput rightSerialInput;

    [Header("UI")]
    [Tooltip("左手 COM ポート入力")]
    public TMP_InputField leftPortInput;

    [Tooltip("右手 COM ポート入力")]
    public TMP_InputField rightPortInput;

    private void Start()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(false); // 最初は閉じておく
        }

        if (hintRoot != null)
        {
            hintRoot.SetActive(true); // 最初はヒントを表示しておく
        }

        // 初期値を UI に反映
        if (leftSerialInput != null && leftPortInput != null)
        {
            leftPortInput.text = leftSerialInput.portName;
        }

        if (rightSerialInput != null && rightPortInput != null)
        {
            rightPortInput.text = rightSerialInput.portName;
        }
    }

    private void Update()
    {
        if (panelRoot == null)
        {
            return;
        }

        // 設定パネルの開閉
        if (Input.GetKeyDown(toggleKey))
        {
            panelRoot.SetActive(!panelRoot.activeSelf);
        }

        // パネルの表示状態に応じてヒントを ON/OFF
        if (hintRoot != null)
        {
            hintRoot.SetActive(!panelRoot.activeSelf);
        }

        if (!panelRoot.activeSelf)
        {
            return;
        }

        // Tab キーで left/right のフォーカスをトグル
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (leftPortInput != null && leftPortInput.isFocused)
            {
                // 左にフォーカス中なら右へ
                if (rightPortInput != null)
                {
                    rightPortInput.Select();
                    rightPortInput.ActivateInputField();
                }
            }
            else if (rightPortInput != null && rightPortInput.isFocused)
            {
                // 右にフォーカス中なら左へ
                if (leftPortInput != null)
                {
                    leftPortInput.Select();
                    leftPortInput.ActivateInputField();
                }
            }
            else
            {
                // どちらにもフォーカスがない場合は左から開始
                if (leftPortInput != null)
                {
                    leftPortInput.Select();
                    leftPortInput.ActivateInputField();
                }
            }
        }

        // Enter キーで設定を Apply し、パネルを閉じる
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            // フォーカスに依存せず、両方の入力欄を確認して再接続を試みる
            ApplyLeftPort();
            ApplyRightPort();

            // 入力元に関わらず、Enter が押されたらパネルを閉じる
            panelRoot.SetActive(false);
            if (hintRoot != null)
            {
                hintRoot.SetActive(true);
            }
        }
    }

    public void ApplyLeftPort()
    {
        if (leftSerialInput == null || leftPortInput == null)
        {
            return;
        }

        leftSerialInput.SetPortNameAndReconnect(leftPortInput.text);
    }

    public void ApplyRightPort()
    {
        if (rightSerialInput == null || rightPortInput == null)
        {
            return;
        }

        rightSerialInput.SetPortNameAndReconnect(rightPortInput.text);
    }
}
