using TMPro;
using UnityEngine;

/// <summary>
/// 触覚キャリブレーションの UI 側ブリッジ（ボタンから呼ぶ薄いレイヤ）。
/// 実処理は ForceFeedBack/HapticGripCalibrationController に委譲する。
/// </summary>
[DisallowMultipleComponent]
public class HapticCalibrationUI : MonoBehaviour
{
    public enum HandSide
    {
        Right = 0,
        Left = 1,
    }

    [Header("Targets (Dual)")]
    [Tooltip("右手側コントローラ")]
    public HapticGripCalibrationController rightController;

    [Tooltip("左手側コントローラ")]
    public HapticGripCalibrationController leftController;

    [Tooltip("操作対象の手")]
    [SerializeField] private HandSide selectedHand = HandSide.Right;

    [Header("UI (optional)")]
    [Tooltip("例: 「右手の触覚フィードバックを調整中」")]
    public TMP_Text titleText;

    [Tooltip("選択中の手の状態表示（StatusMessage）")]
    public TMP_Text statusText;

    [Tooltip("選択中の手の値表示（released 値：親指/人差し指/中指/薬指/小指）")]
    public TMP_Text valuesText;

    [Header("UI (optional, visibility)")]
    [Tooltip("Start ボタンのルート（キャリブ中は非表示）")]
    public GameObject startButtonRoot;

    [Tooltip("Cancel ボタンのルート（キャリブ中のみ表示）")]
    public GameObject cancelButtonRoot;

    [Tooltip("右手/左手選択ボタン群のルート（キャリブ中は非表示にしたい場合）")]
    public GameObject handSelectRoot;

    [Tooltip("true のとき、キャリブ中は右手/左手の選択 UI を隠す")]
    public bool hideHandSelectWhileRunning = true;

    [Header("Behavior")]
    [Tooltip("Update で表示を自動更新する")]
    public bool autoRefresh = true;

    private void Start()
    {
        Refresh();
    }

    private void OnEnable()
    {
        Refresh();
    }

    private void Update()
    {
        if (!autoRefresh) return;
        Refresh();
    }

    public void StartCalibration()
    {
        if (IsAnyRunning(out _)) return;

        var c = GetController(selectedHand);
        if (c == null) return;
        c.StartCalibration();
        Refresh();
    }

    public void CancelCalibration()
    {
        // キャリブ中の手があればそれを優先してキャンセルする
        if (rightController != null && rightController.IsRunning)
        {
            rightController.CancelCalibration();
        }
        else if (leftController != null && leftController.IsRunning)
        {
            leftController.CancelCalibration();
        }
        else
        {
            var c = GetController(selectedHand);
            if (c != null) c.CancelCalibration();
        }
        Refresh();
    }

    public void ResetCalibration()
    {
        var c = GetController(selectedHand);
        if (c == null) return;
        c.ResetCalibration();
        Refresh();
    }

    public void SelectRight()
    {
        if (IsAnyRunning(out _)) return;
        selectedHand = HandSide.Right;
        Refresh();
    }

    public void SelectLeft()
    {
        if (IsAnyRunning(out _)) return;
        selectedHand = HandSide.Left;
        Refresh();
    }

    public void Refresh()
    {
        bool running = IsAnyRunning(out HandSide runningSide);

        if (startButtonRoot != null) startButtonRoot.SetActive(!running);
        if (cancelButtonRoot != null) cancelButtonRoot.SetActive(running);
        if (handSelectRoot != null && hideHandSelectWhileRunning) handSelectRoot.SetActive(!running);

        var displaySide = running ? runningSide : selectedHand;
        var active = GetController(displaySide);

        if (titleText != null)
        {
            titleText.text = displaySide == HandSide.Left
                ? "左手の触覚フィードバックを調整中"
                : "右手の触覚フィードバックを調整中";
        }

        UpdateTexts(active, statusText, valuesText);
    }

    private void UpdateTexts(HapticGripCalibrationController c, TMP_Text status, TMP_Text values)
    {
        if (c == null) return;

        if (status != null)
        {
            status.text = c.StatusMessage ?? string.Empty;
        }

        if (values != null)
        {
            var state = c.calibrationState;
            if (state == null)
            {
                values.text = "未キャリブレーション";
                return;
            }

            // 部分的でも見える化する（? は未保存）
            // state の順番は Thumb/Index/Middle/Ring/Pinky
            string th = state.TryGetReleasedServoValue(0, out int thv) ? thv.ToString() : "?";
            string ix = state.TryGetReleasedServoValue(1, out int ixv) ? ixv.ToString() : "?";
            string md = state.TryGetReleasedServoValue(2, out int mdv) ? mdv.ToString() : "?";
            string rg = state.TryGetReleasedServoValue(3, out int rgv) ? rgv.ToString() : "?";
            string pk = state.TryGetReleasedServoValue(4, out int pkv) ? pkv.ToString() : "?";
            string suffix = state.IsFullyCalibrated ? string.Empty : "（未完了）";
            values.text = $"親指:{th}  人差し指:{ix}  中指:{md}\n薬指:{rg}  小指:{pk}{suffix}";
        }
    }

    private HapticGripCalibrationController GetController(HandSide side)
    {
        return side == HandSide.Left ? leftController : rightController;
    }

    private bool IsAnyRunning(out HandSide runningSide)
    {
        if (rightController != null && rightController.IsRunning)
        {
            runningSide = HandSide.Right;
            return true;
        }

        if (leftController != null && leftController.IsRunning)
        {
            runningSide = HandSide.Left;
            return true;
        }

        runningSide = selectedHand;
        return false;
    }
}
