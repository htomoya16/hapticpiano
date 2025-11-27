using UnityEngine;
using UnityEngine.UI;
using Valve.VR;

public class CurlDebugUIOnCanvas : MonoBehaviour
{
    [Header("Trackers")]
    public HandCurlTracker leftHandTracker;
    public HandCurlTracker rightHandTracker;

    [Header("UI Texts")]
    public Text leftText;
    public Text rightText;

    private readonly string[] fingerNames = { "Thumb", "Index", "Middle", "Ring", "Pinky" };

    void Awake()
    {
        // Tracker が Inspector で設定されていなければ自動で探す
        if (leftHandTracker == null || rightHandTracker == null)
        {
            var trackers = FindObjectsOfType<HandCurlTracker>();
            foreach (var t in trackers)
            {
                if (t.hand == null) continue;

                if (t.hand.handType == SteamVR_Input_Sources.LeftHand)
                    leftHandTracker = t;
                else if (t.hand.handType == SteamVR_Input_Sources.RightHand)
                    rightHandTracker = t;
            }
        }
    }

    void Update()
    {
        if (leftText != null)
            leftText.text = BuildHandText("Left", leftHandTracker);

        if (rightText != null)
            rightText.text = BuildHandText("Right", rightHandTracker);
    }

    private string BuildHandText(string title, HandCurlTracker tracker)
    {
        if (tracker == null)
            return $"{title}: (no tracker)";

        var sb = new System.Text.StringBuilder();
        sb.AppendLine(title);

        for (int i = 0; i < 5; i++)
        {
            float c01 = (tracker.curl01 != null && tracker.curl01.Length > i)
                ? tracker.curl01[i]
                : 0f;

            short cFfb = (tracker.curlFfb != null && tracker.curlFfb.Length > i)
                ? tracker.curlFfb[i]
                : (short)0;

            sb.AppendLine($"{fingerNames[i]}: {c01:F3}  ({cFfb})");
        }

        return sb.ToString();
    }
}
