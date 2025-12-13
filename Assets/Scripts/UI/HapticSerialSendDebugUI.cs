using TMPro;
using UnityEngine;

/// <summary>
/// HapticSerialSender の送信内容（最後に送った行/値/状態）を UI に表示するデバッグ用。
/// VR でも「送っているか」を確認できるようにする。
/// </summary>
[DisallowMultipleComponent]
public class HapticSerialSendDebugUI : MonoBehaviour
{
    [Header("Targets")]
    public HapticSerialSender rightSender;
    public HapticSerialSender leftSender;

    [Header("UI")]
    public TMP_Text rightText;
    public TMP_Text leftText;

    [Tooltip("右/左を 1 つの Text にまとめて出す場合に使用（rightText/leftText より優先）")]
    public TMP_Text combinedText;

    [Header("Behavior")]
    public bool autoRefresh = true;

    private void Start()
    {
        Refresh();
    }

    private void Update()
    {
        if (!autoRefresh) return;
        Refresh();
    }

    public void Refresh()
    {
        if (combinedText != null)
        {
            combinedText.text = $"{FormatLine("R", rightSender)}\n{FormatLine("L", leftSender)}";
            return;
        }

        if (rightText != null) rightText.text = FormatLine("R", rightSender);
        if (leftText != null) leftText.text = FormatLine("L", leftSender);
    }

    private static string FormatLine(string label, HapticSerialSender sender)
    {
        if (sender == null) return $"{label}: (null)";

        string status = string.IsNullOrEmpty(sender.LastStatus) ? "?" : sender.LastStatus;
        string line = string.IsNullOrEmpty(sender.LastEncodedLine) ? "-" : sender.LastEncodedLine;

        // A####... は長いので、まず status と最終行だけ出す
        return $"{label}: {status}  {line}";
    }
}

