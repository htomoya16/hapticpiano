using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// F1 などのキーで設定パネルを開閉し、開いている間は一時停止・マウス操作を有効にする。
/// パネルがワールド空間 Canvas の場合は一時的に Overlay 表示へ切り替えられる。
/// </summary>
public class SettingsOverlayOpener : MonoBehaviour
{
    [Header("Toggle Key")]
    [SerializeField] private KeyCode openKey = KeyCode.F1;

    [Header("Targets")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private GameObject hintRoot;
    [SerializeField] private Canvas targetCanvas;

    [Header("Behavior")]
    [SerializeField] private bool pauseTimeOnOpen = true;
    [SerializeField] private bool unlockCursorOnOpen = true;

    [Tooltip("true のとき、設定を開いても Time.timeScale を変更しない（既定: 停止しない）")]
    [SerializeField] private bool disableTimePausing = true;

    [Header("Events")]
    public UnityEvent onOpen;
    public UnityEvent onClose;

    private float _savedTimeScale = 1f;
    private CursorLockMode _savedLock;
    private bool _savedVisible;
    private RenderMode _savedRenderMode;
    private bool _canvasSaved;

    public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

    private void Start()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
        if (hintRoot != null) hintRoot.SetActive(true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(openKey))
        {
            Toggle();
        }
    }

    private void OnDisable()
    {
        if (panelRoot != null && panelRoot.activeSelf)
        {
            SetPanelActive(false);
        }
    }

    public void Toggle()
    {
        if (panelRoot == null)
        {
            return;
        }

        SetPanelActive(!panelRoot.activeSelf);
    }

    public void Open()
    {
        SetPanelActive(true);
    }

    public void Close()
    {
        SetPanelActive(false);
    }

    private void SetPanelActive(bool active)
    {
        bool wasActive = panelRoot != null && panelRoot.activeSelf;

        if (panelRoot != null) panelRoot.SetActive(active);
        if (hintRoot != null) hintRoot.SetActive(!active);

        bool shouldPauseTime = pauseTimeOnOpen && !disableTimePausing;
        if (shouldPauseTime)
        {
            if (active && !wasActive)
            {
                _savedTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else if (!active && wasActive)
            {
                // 0 が保存されている場合でも再開できるように 1f にフォールバック
                Time.timeScale = _savedTimeScale > 0f ? _savedTimeScale : 1f;
            }
        }

        if (unlockCursorOnOpen)
        {
            if (active && !wasActive)
            {
                _savedLock = Cursor.lockState;
                _savedVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (!active && wasActive)
            {
                Cursor.lockState = _savedLock;
                Cursor.visible = _savedVisible;
            }
        }

        if (targetCanvas != null)
        {
            if (active && !wasActive)
            {
                _savedRenderMode = targetCanvas.renderMode;
                _canvasSaved = true;
                targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            else if (!active && wasActive && _canvasSaved)
            {
                targetCanvas.renderMode = _savedRenderMode;
            }
        }

        if (active) onOpen?.Invoke();
        else onClose?.Invoke();
    }
}
