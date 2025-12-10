using UnityEngine;
using TMPro;

/// <summary>
/// COM ポート名をランタイム中に変更するための設定パネルロジック。
/// ここでは入力と適用のみ扱う。
/// </summary>
public class SerialSettingsUI : MonoBehaviour
{
    [Header("Panel")]
    [Tooltip("設定パネルのルート GameObject")]
    public GameObject panelRoot;

    [Header("Targets")]
    [Tooltip("左手側の HandSensorReceiver")]
    public HandSensorReceiver leftReceiver;

    [Tooltip("右手側の HandSensorReceiver")]
    public HandSensorReceiver rightReceiver;

    [Header("UI")]
    [Tooltip("左手 COM ポート入力")]
    public TMP_InputField leftPortInput;

    [Tooltip("右手 COM ポート入力")]
    public TMP_InputField rightPortInput;

    [Header("Behavior")]
    [Tooltip("Enter キー押下時に Apply を実行する")]
    public bool applyOnEnter = true;

    private void Start()
    {
        // 初期値を UI に反映
        if (leftReceiver != null && leftPortInput != null)
        {
            leftPortInput.text = leftReceiver.portName;
        }

        if (rightReceiver != null && rightPortInput != null)
        {
            rightPortInput.text = rightReceiver.portName;
        }
    }

    private void Update()
    {
        if (panelRoot == null || !panelRoot.activeSelf)
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
        if (applyOnEnter && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
        {
            // フォーカスに依存せず、両方の入力欄を確認して再接続を試みる
            ApplyLeftPort();
            ApplyRightPort();
        }
    }

    /// <summary>
    /// Apply ボタン用：左右とも適用し、設定に応じてパネルを閉じる。
    /// </summary>
    public void ApplyBothPorts()
    {
        ApplyLeftPort();
        ApplyRightPort();
    }

    public void ApplyLeftPort()
    {
        if (leftReceiver == null || leftPortInput == null)
        {
            return;
        }

        leftReceiver.SetPortNameAndReconnect(leftPortInput.text);
    }

    public void ApplyRightPort()
    {
        if (rightReceiver == null || rightPortInput == null)
        {
            return;
        }

        rightReceiver.SetPortNameAndReconnect(rightPortInput.text);
    }

}
