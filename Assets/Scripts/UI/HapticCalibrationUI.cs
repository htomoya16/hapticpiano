using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    [Tooltip("statusText に右手/左手を表示する")]
    public bool showHandSideInStatusText = true;

    [Header("VR Overlay (optional)")]
    [Tooltip("true のとき、VR でも statusText と grasp image を表示する（未指定なら実行時に複製生成する）。")]
    public bool enableVrOverlay = false;

    [Tooltip("VR 表示先 Canvas（指定すると、そこに statusText / grasp image を複製して出す）。")]
    public Canvas vrOverlayCanvas;

    [Tooltip("VR 表示を追従させる Transform（未指定なら Camera.main）。")]
    public Transform vrAttachTarget;

    [Tooltip("追従先からの相対位置（m）。")]
    public Vector3 vrLocalPosition = new Vector3(0f, -0.08f, 0.75f);

    [Tooltip("追従先からの相対回転（Euler）。")]
    public Vector3 vrLocalEulerAngles = Vector3.zero;

    [Tooltip("WorldSpace Canvas のスケール（既定: 0.001 = 1000px を 1m として扱う）。")]
    public float vrCanvasScale = 0.001f;

    [Tooltip("true のとき、キャリブ中のみ VR 表示を出す。")]
    public bool vrOnlyWhileRunning = true;

    [Header("VR UI Targets (optional)")]
    [Tooltip("VR に出す StatusText（未指定なら statusText を複製する / なければ自動生成）。")]
    public TMP_Text vrStatusText;

    [Tooltip("VR に出す grasp image ルート（未指定なら graspHintImageRoot を複製する）。")]
    public GameObject vrGraspHintImageRoot;

    private Canvas _autoVrCanvas;
    private Transform _autoVrRoot;
    private TMP_Text _autoVrStatusText;
    private GameObject _autoVrGraspHintImageRoot;

    private TMP_Text _vrRuntimeStatusText;
    private GameObject _vrRuntimeGraspHintImageRoot;

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
        EnsureVrOverlay();

        bool running = IsAnyRunning(out HandSide runningSide);

        if (startButtonRoot != null) startButtonRoot.SetActive(!running);
        if (cancelButtonRoot != null) cancelButtonRoot.SetActive(running);
        if (resetButtonRoot != null) resetButtonRoot.SetActive(!running);
        if (handSelectRoot != null && hideHandSelectWhileRunning) handSelectRoot.SetActive(!running);

        var displaySide = running ? runningSide : selectedHand;
        var active = GetController(displaySide);

        bool vrVisible = enableVrOverlay && (!vrOnlyWhileRunning || running);
        var vrStatus = GetVrStatusText();
        if (vrStatus != null) vrStatus.gameObject.SetActive(vrVisible);
        if (_autoVrRoot != null) _autoVrRoot.gameObject.SetActive(vrVisible);

        string msg = active != null ? active.StatusMessage : null;
        bool showGrasp = !string.IsNullOrEmpty(msg) && msg.Contains(GraspHintMessage);
        if (graspHintImageRoot != null) graspHintImageRoot.SetActive(showGrasp);
        var vrGrasp = GetVrGraspHintImageRoot();
        if (vrGrasp != null) vrGrasp.SetActive(vrVisible && showGrasp);

        UpdateTexts(active, displaySide, statusText, valuesText);
        UpdateTexts(active, displaySide, GetVrStatusText(), null);
    }

    private void UpdateTexts(HapticGripCalibrationController c, HandSide side, TMP_Text status, TMP_Text values)
    {
        if (status != null)
        {
            string msg = c != null ? (c.StatusMessage ?? string.Empty) : string.Empty;
            if (showHandSideInStatusText)
            {
                string sideLabel = side == HandSide.Left ? "左手" : "右手";
                status.text = string.IsNullOrEmpty(msg) ? sideLabel : $"{sideLabel}\n{msg}";
            }
            else
            {
                status.text = msg;
            }
        }

        if (values != null)
        {
            var state = c != null ? c.calibrationState : null;
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

    private TMP_Text GetVrStatusText()
    {
        return _vrRuntimeStatusText;
    }

    private GameObject GetVrGraspHintImageRoot()
    {
        return _vrRuntimeGraspHintImageRoot;
    }

    private void EnsureVrOverlay()
    {
        if (!enableVrOverlay)
        {
            CleanupAutoVrOverlay();
            return;
        }

        if (!IsVrActive())
        {
            CleanupAutoVrOverlay();
            return;
        }

        _vrRuntimeStatusText = null;
        _vrRuntimeGraspHintImageRoot = null;

        // 同一 UI を指定してしまった場合は「VR用としては使えない」ので自動複製にフォールバックする
        bool hasManualStatus = vrStatusText != null && vrStatusText != statusText;
        bool hasManualGrasp = vrGraspHintImageRoot != null && vrGraspHintImageRoot != graspHintImageRoot;

        if (vrOverlayCanvas != null)
        {
            // 既存 Canvas を使う（シーン側で Canvas の位置・追従を設定できるようにする）
            if (_autoVrCanvas != null)
            {
                Destroy(_autoVrCanvas.gameObject);
                _autoVrCanvas = null;
            }

            if (_autoVrRoot == null || _autoVrRoot.parent != vrOverlayCanvas.transform)
            {
                if (_autoVrRoot != null) Destroy(_autoVrRoot.gameObject);
                var root = new GameObject("HapticCalibrationUI_VROverlayRoot", typeof(RectTransform));
                root.hideFlags = HideFlags.DontSave;
                root.transform.SetParent(vrOverlayCanvas.transform, false);
                _autoVrRoot = root.transform;

                var rt = (RectTransform)root.transform;
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.sizeDelta = new Vector2(900f, 520f);
            }

            if (hasManualStatus)
            {
                _vrRuntimeStatusText = vrStatusText;
                if (!_vrRuntimeStatusText.transform.IsChildOf(vrOverlayCanvas.transform))
                {
                    Debug.LogWarning("[HapticCalibrationUI] vrStatusText が vrOverlayCanvas 配下にありません。Canvas 指定時は子に配置してください。");
                }

                if (!(_vrRuntimeStatusText is TextMeshProUGUI))
                {
                    Debug.LogWarning("[HapticCalibrationUI] vrStatusText は TextMeshProUGUI(UI) を推奨します（Canvas 上で確実に表示するため）。");
                }
            }
            else if (_autoVrStatusText == null)
            {
                _autoVrStatusText = TryCloneStatusText(_autoVrRoot) ?? CreateDefaultVrStatusText(_autoVrRoot);
            }

            if (hasManualGrasp)
            {
                _vrRuntimeGraspHintImageRoot = vrGraspHintImageRoot;
                if (!_vrRuntimeGraspHintImageRoot.transform.IsChildOf(vrOverlayCanvas.transform))
                {
                    Debug.LogWarning("[HapticCalibrationUI] vrGraspHintImageRoot が vrOverlayCanvas 配下にありません。Canvas 指定時は子に配置してください。");
                }
            }
            else if (_autoVrGraspHintImageRoot == null)
            {
                _autoVrGraspHintImageRoot = TryCloneGraspHintImage(_autoVrRoot);
                if (_autoVrGraspHintImageRoot != null)
                {
                    _autoVrGraspHintImageRoot.SetActive(false);
                }
            }

            return;
        }

        if (hasManualStatus) _vrRuntimeStatusText = vrStatusText;
        if (hasManualGrasp) _vrRuntimeGraspHintImageRoot = vrGraspHintImageRoot;

        if (_vrRuntimeStatusText != null && _vrRuntimeGraspHintImageRoot != null)
        {
            // 完全に手動割り当てなら、自動生成は不要
            return;
        }

        Transform attach = vrAttachTarget != null ? vrAttachTarget : (Camera.main != null ? Camera.main.transform : null);
        if (attach == null) return;

        if (_autoVrCanvas == null)
        {
            var root = new GameObject("HapticCalibrationUI_VROverlay", typeof(RectTransform), typeof(Canvas));
            root.hideFlags = HideFlags.DontSave;
            root.transform.SetParent(attach, false);
            root.transform.localPosition = vrLocalPosition;
            root.transform.localEulerAngles = vrLocalEulerAngles;
            root.transform.localScale = Vector3.one * Mathf.Max(0.0001f, vrCanvasScale);

            _autoVrCanvas = root.GetComponent<Canvas>();
            _autoVrCanvas.renderMode = RenderMode.WorldSpace;
            _autoVrCanvas.worldCamera = Camera.main;
            _autoVrCanvas.sortingOrder = 1000;
            _autoVrRoot = _autoVrCanvas.transform;

            var rt = (RectTransform)root.transform;
            rt.sizeDelta = new Vector2(900f, 520f);

            // 見やすさのための薄い背景
            var bg = new GameObject("Background", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bg.transform.SetParent(root.transform, false);
            var bgRt = (RectTransform)bg.transform;
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.pivot = new Vector2(0.5f, 0.5f);
            bgRt.anchoredPosition = Vector2.zero;
            bgRt.sizeDelta = Vector2.zero;
            var img = bg.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.45f);
            img.raycastTarget = false;
        }
        else
        {
            // 実行中に値を変えたときに追従する
            _autoVrCanvas.transform.SetParent(attach, false);
            _autoVrCanvas.transform.localPosition = vrLocalPosition;
            _autoVrCanvas.transform.localEulerAngles = vrLocalEulerAngles;
            _autoVrCanvas.transform.localScale = Vector3.one * Mathf.Max(0.0001f, vrCanvasScale);
            _autoVrCanvas.worldCamera = Camera.main;
            _autoVrRoot = _autoVrCanvas.transform;
        }

        if (_vrRuntimeStatusText == null && _autoVrStatusText == null)
        {
            _autoVrStatusText = TryCloneStatusText(_autoVrRoot) ?? CreateDefaultVrStatusText(_autoVrRoot);
        }

        if (_vrRuntimeGraspHintImageRoot == null && _autoVrGraspHintImageRoot == null)
        {
            _autoVrGraspHintImageRoot = TryCloneGraspHintImage(_autoVrRoot);
            if (_autoVrGraspHintImageRoot != null)
            {
                _autoVrGraspHintImageRoot.SetActive(false);
            }
        }

        if (_vrRuntimeStatusText == null) _vrRuntimeStatusText = _autoVrStatusText;
        if (_vrRuntimeGraspHintImageRoot == null) _vrRuntimeGraspHintImageRoot = _autoVrGraspHintImageRoot;
    }

    private TMP_Text TryCloneStatusText(Transform parent)
    {
        if (statusText == null) return null;
        var clone = Instantiate(statusText.gameObject, parent, false);
        clone.name = "StatusText_VR";

        var t = clone.GetComponent<TMP_Text>();
        if (t == null) return null;
        t.raycastTarget = false;

        if (clone.TryGetComponent(out RectTransform rt))
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -40f);
            rt.sizeDelta = new Vector2(860f, 220f);
        }

        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = true;
        return t;
    }

    private TMP_Text CreateDefaultVrStatusText(Transform parent)
    {
        var go = new GameObject("StatusText_VR", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);

        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -40f);
        rt.sizeDelta = new Vector2(860f, 220f);

        var t = go.GetComponent<TextMeshProUGUI>();
        t.text = string.Empty;
        t.fontSize = 46f;
        t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center;
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        return t;
    }

    private GameObject TryCloneGraspHintImage(Transform parent)
    {
        if (graspHintImageRoot == null) return null;
        var clone = Instantiate(graspHintImageRoot, parent, false);
        clone.name = "GraspHintImage_VR";

        if (clone.TryGetComponent(out RectTransform rt))
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 30f);
            rt.sizeDelta = new Vector2(860f, 240f);
        }

        return clone;
    }

    private void CleanupAutoVrOverlay()
    {
        if (_autoVrCanvas != null)
        {
            Destroy(_autoVrCanvas.gameObject);
        }

        if (_autoVrCanvas == null && _autoVrRoot != null)
        {
            Destroy(_autoVrRoot.gameObject);
        }

        _autoVrCanvas = null;
        _autoVrRoot = null;
        _autoVrStatusText = null;
        _autoVrGraspHintImageRoot = null;
        _vrRuntimeStatusText = null;
        _vrRuntimeGraspHintImageRoot = null;
    }

    private static bool IsVrActive()
    {
        // SteamVR/OpenVR でも XR が有効なら true になりやすい。Editor/非VR時の誤表示を避ける。
        return UnityEngine.XR.XRSettings.isDeviceActive || UnityEngine.XR.XRSettings.enabled;
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
