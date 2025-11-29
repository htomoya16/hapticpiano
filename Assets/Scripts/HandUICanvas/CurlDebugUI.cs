using UnityEngine;
using UnityEngine.UI;

public class CurlDebugUI : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("このパネルで監視する HandCurlTracker")]
    public HandCurlTracker tracker;

    [Tooltip("値を表示する Text コンポーネント")]
    public TMPro.TMP_Text text;

    [Header("Panel Control")]
    [Tooltip("このデバッグパネル全体のルート (通常はこの Canvas 自身)")]
    public GameObject panelRoot;

    [Tooltip("デバッグパネルを表示するかどうか")]
    public bool debugEnabled = true;

    [Header("表示ラベル")]
    [Tooltip("先頭に付けるタイトル (例: Left, Right など)")]
    public string title = "Hand";

    private readonly string[] fingerNames = { "Thumb", "Index", "Middle", "Ring", "Pinky" };

    void Awake()
    {
        // panelRoot が未設定なら自分自身を使う
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        ApplyPanelActiveState();
    }

    // Inspector で値を変えたときにも反映
    void OnValidate()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }
        ApplyPanelActiveState();
    }

    void Update()
    {
        if (!debugEnabled)
        {
            return;
        }

        if (text == null)
        {
            return;
        }

        text.text = BuildHandText();
    }

    private void ApplyPanelActiveState()
    {
        if (panelRoot != null)
        {
            panelRoot.SetActive(debugEnabled);
        }
    }

    private string BuildHandText()
    {
        if (tracker == null)
        {
            return $"{title}: (no tracker)";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(title);
        sb.AppendLine("Finger   :   Curl    (FFB)");
        sb.AppendLine("---------------------------");

        for (int i = 0; i < 5; i++)
        {
            float c01 = (tracker.curl01 != null && tracker.curl01.Length > i)
                ? tracker.curl01[i]
                : 0f;

            short cFfb = (tracker.curlFfb != null && tracker.curlFfb.Length > i)
                ? tracker.curlFfb[i]
                : (short)0;

            // 名前は左詰め8桁、Curl は右詰め7桁、小数3桁、FFB は右詰め4桁
            string namePart = fingerNames[i].PadRight(8);
            string curlPart = c01.ToString("F3").PadLeft(7);
            string ffbPart  = cFfb.ToString().PadLeft(4);

            sb.AppendLine($"{namePart}: {curlPart}  ({ffbPart})");
        }

        return sb.ToString();
    }

}
