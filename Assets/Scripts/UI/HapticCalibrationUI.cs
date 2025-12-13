using TMPro;
using UnityEngine;

/// <summary>
/// 触覚キャリブレーションの UI 側ブリッジ（ボタンから呼ぶ薄いレイヤ）。
/// 実処理は ForceFeedBack/HapticGripCalibrationController に委譲する。
/// </summary>
[DisallowMultipleComponent]
public class HapticCalibrationUI : MonoBehaviour
{
    private const string GraspHintMessage = "写真のように握った状態を維持していてください";
    private const int TableStartPosPx = 120;
    private const int TableColStepPx = 70;

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
    [Tooltip("選択中の手の状態表示（StatusMessage）")]
    public TMP_Text statusText;

    [Tooltip("選択中の手の値表示（手の左右 + released 値：親指/人差し指/中指/薬指/小指）")]
    public TMP_Text valuesText;

    [Header("UI (optional, visibility)")]
    [Tooltip("Start ボタンのルート（キャリブ中は非表示）")]
    public GameObject startButtonRoot;

    [Tooltip("Cancel ボタンのルート（キャリブ中のみ表示）")]
    public GameObject cancelButtonRoot;

    [Tooltip("Reset ボタンのルート（キャリブ中は非表示）")]
    public GameObject resetButtonRoot;

    [Tooltip("握り方の写真（キャリブ案内表示中のみ表示）")]
    public GameObject graspHintImageRoot;

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
        if (resetButtonRoot != null) resetButtonRoot.SetActive(!running);
        if (handSelectRoot != null && hideHandSelectWhileRunning) handSelectRoot.SetActive(!running);

        var displaySide = running ? runningSide : selectedHand;
        var active = GetController(displaySide);

        if (graspHintImageRoot != null)
        {
            string msg = active != null ? active.StatusMessage : null;
            graspHintImageRoot.SetActive(!string.IsNullOrEmpty(msg) && msg.Contains(GraspHintMessage));
        }

        UpdateTexts(active, displaySide, statusText, valuesText);
    }

    private void UpdateTexts(HapticGripCalibrationController c, HandSide side, TMP_Text status, TMP_Text values)
    {
        if (c == null) return;

        if (status != null)
        {
            status.text = c.StatusMessage ?? string.Empty;
        }

        if (values != null)
        {
            var state = c.calibrationState;
            string sideLabel = side == HandSide.Left ? "左手" : "右手";
            int c0 = TableStartPosPx;
            int c1 = c0 + TableColStepPx;
            int c2 = c1 + TableColStepPx;
            int c3 = c2 + TableColStepPx;
            int c4 = c3 + TableColStepPx;

            static string Cell(string s)
            {
                if (string.IsNullOrEmpty(s)) return "—";
                return s == "—" ? "—" : s;
            }

            if (state == null)
            {
                values.text =
                    $"{sideLabel}<pos={c0}>親<pos={c1}>人<pos={c2}>中<pos={c3}>薬<pos={c4}>小\n" +
                    $"<pos={c0}>—<pos={c1}>—<pos={c2}>—<pos={c3}>—<pos={c4}>—";
                return;
            }

            // 部分的でも見える化する（? は未保存）
            // state の順番は Thumb/Index/Middle/Ring/Pinky
            string th = state.TryGetReleasedServoValue(0, out int thv) ? thv.ToString() : "—";
            string ix = state.TryGetReleasedServoValue(1, out int ixv) ? ixv.ToString() : "—";
            string md = state.TryGetReleasedServoValue(2, out int mdv) ? mdv.ToString() : "—";
            string rg = state.TryGetReleasedServoValue(3, out int rgv) ? rgv.ToString() : "—";
            string pk = state.TryGetReleasedServoValue(4, out int pkv) ? pkv.ToString() : "—";

            values.text =
                $"{sideLabel}<pos={c0}>親<pos={c1}>人<pos={c2}>中<pos={c3}>薬<pos={c4}>小\n" +
                $"<pos={c0}>{Cell(th)}<pos={c1}>{Cell(ix)}<pos={c2}>{Cell(md)}<pos={c3}>{Cell(rg)}<pos={c4}>{Cell(pk)}";
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
