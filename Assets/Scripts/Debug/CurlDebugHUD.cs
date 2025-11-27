using UnityEngine;

public class CurlDebugHUD : MonoBehaviour
{
    // 手動でアサインできるように一応 SerializeField は残しておく
    [SerializeField] private FFBManager ffbManager;

    public bool showNormalized = false;

    private void Awake()
    {
        // Inspector で設定されていなければ、自動でシーン内から探す
        if (ffbManager == null)
        {
            ffbManager = FindObjectOfType<FFBManager>();

            if (ffbManager == null)
            {
                Debug.LogError("[CurlDebugHUD] シーン内に FFBManager が見つからない。");
            }
            else
            {
                Debug.Log("[CurlDebugHUD] 自動で FFBManager を取得した: " +
                          ffbManager.gameObject.name);
            }
        }
    }

    void OnGUI()
    {
        if (ffbManager == null) return;

        short[] left  = ffbManager.lastLeftFingerCurl;
        short[] right = ffbManager.lastRightFingerCurl;

        GUILayout.BeginArea(new Rect(10, 10, 300, 200), GUI.skin.box);

        GUILayout.Label("<b>Left Hand Curl</b>");
        for (int i = 0; i < 5; i++)
        {
            string fingerName = FingerName(i);
            string valueStr   = ValueToString(left, i);
            GUILayout.Label($"{fingerName}: {valueStr}");
        }

        GUILayout.Space(10);

        GUILayout.Label("<b>Right Hand Curl</b>");
        for (int i = 0; i < 5; i++)
        {
            string fingerName = FingerName(i);
            string valueStr   = ValueToString(right, i);
            GUILayout.Label($"{fingerName}: {valueStr}");
        }

        GUILayout.EndArea();
    }

    string FingerName(int index)
    {
        switch (index)
        {
            case 0: return "Thumb";
            case 1: return "Index";
            case 2: return "Middle";
            case 3: return "Ring";
            case 4: return "Pinky";
            default: return $"Finger{index}";
        }
    }

    string ValueToString(short[] arr, int index)
    {
        if (arr == null || arr.Length <= index)
            return "-";

        short raw = arr[index];

        if (!showNormalized)
        {
            // そのまま 0〜1000 表示
            return raw.ToString();
        }
        else
        {
            // 0〜1 に正規化して表示
            float norm = Mathf.Clamp01(raw / 1000f);
            return norm.ToString("F3");
        }
    }
}
