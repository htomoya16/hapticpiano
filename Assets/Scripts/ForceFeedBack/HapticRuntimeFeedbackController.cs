using UnityEngine;

/// <summary>
/// ランタイム触覚（追従モード）
/// - 通常: 指に追従しつつ「指の少し先」へサーボを動かす
/// - 鍵盤接触: ピアノ接触モードとして「指側へ寄せる」
/// - 底面: その指のサーボを停止（保持）
/// </summary>
[DisallowMultipleComponent]
public sealed class HapticRuntimeFeedbackController : MonoBehaviour
{
    private const int FingerCount = 5;
    private const int DefaultServoMax = 1000;

    [Header("References")]
    public HandCurlTracker curlTracker;
    public HapticCalibrationState calibrationState;
    public HapticSerialSender serialSender;
    public PianoFingerContactRegistry contactRegistry;

    [Header("UI Guard (optional)")]
    [Tooltip("設定パネルが開いている間は上書きしない（activeSelf==true）。")]
    public GameObject settingsPanelRoot;

    [Tooltip("設定画面オープナー（設定パネルが開いている間は上書きしない）。")]
    public SettingsOverlayOpener settingsOverlay;

    [Header("Hand")]
    public Handedness handedness = Handedness.Right;

    [Header("Mapping")]
    [Tooltip("true: t=1-curl01（反対対応）。false: t=curl01")]
    public bool invertCurl = true;

    [Range(0f, 1f)]
    [Tooltip("t（0-1）の強さ。1=そのまま、0.5=半分など。")]
    public float tStrength01 = 1.0f;

    [Tooltip("サーボ最大値（通常 1000）")]
    public int servoMax = DefaultServoMax;

    [Header("Gap (servo units, toward released)")]
    [Tooltip("通常時: 指に追従して計算した目標から、released 方向へこの値だけ離す（一定ギャップ）。\n0 でギャップなし。")]
    public int airGapUnits = 300;

    [Tooltip("鍵盤接触時: 指側へ寄せるため、通常より小さいギャップにする（0 推奨）。")]
    public int pianoGapUnits = 5;

    [Header("Bottom")]
    [Tooltip("底面ロック中はその指の値を保持する")]
    public bool freezeWhileBottomLocked = true;

    [Header("Stability")]
    [Tooltip("この差分未満の揺れは無視する（サーボ値）。")]
    public int deadbandUnits = 4;

    [Header("Debug (read-only)")]
    [SerializeField] private int[] currentTargets = new int[FingerCount];
    [SerializeField] private bool[] isTouchingKey = new bool[FingerCount];
    [SerializeField] private bool[] isBottomLocked = new bool[FingerCount];

    private readonly int[] _frozenTargets = new int[FingerCount];
    private readonly bool[] _isFrozen = new bool[FingerCount];

    private void Awake()
    {
        EnsureArrays();

        if (contactRegistry == null)
        {
            contactRegistry = FindObjectOfType<PianoFingerContactRegistry>();
        }

        if (settingsOverlay == null && settingsPanelRoot == null)
        {
            var openers = FindObjectsOfType<SettingsOverlayOpener>();
            if (openers != null && openers.Length == 1) settingsOverlay = openers[0];
        }
    }

    private void Update()
    {
        EnsureArrays();

        if (curlTracker == null || calibrationState == null || serialSender == null) return;
        if (IsSettingsOpen()) return;

        if (contactRegistry == null)
        {
            contactRegistry = FindObjectOfType<PianoFingerContactRegistry>();
        }

        if (!calibrationState.IsFullyCalibrated)
        {
            SetAllTargets(0);
            WriteTargetsToSender();
            ClearRuntimeState();
            return;
        }

        int max = Clamp1000(servoMax <= 0 ? DefaultServoMax : servoMax);
        int db = Mathf.Max(0, deadbandUnits);

        for (int i = 0; i < FingerCount; i++)
        {
            bool touching = false;
            bool bottom = false;
            if (contactRegistry != null)
            {
                contactRegistry.TryGetFingerState(handedness, (FingerId)i, out touching, out bottom, out _);
            }

            isTouchingKey[i] = touching;
            isBottomLocked[i] = bottom;

            calibrationState.TryGetReleasedServoValue(i, out int released);

            float curl = SafeGetCurl01(i);
            float t = invertCurl ? (1f - curl) : curl;
            t = Mathf.Clamp01(t * Mathf.Clamp01(tStrength01));

            int desired = Mathf.RoundToInt(Mathf.Lerp(released, max, t));
            desired = Clamp1000(desired);

            int gap = touching ? pianoGapUnits : airGapUnits;
            gap = Mathf.Max(0, gap);
            desired = Mathf.Max(released, desired - gap);
            desired = Clamp1000(desired);

            if (freezeWhileBottomLocked && bottom)
            {
                if (!_isFrozen[i])
                {
                    _isFrozen[i] = true;
                    _frozenTargets[i] = currentTargets[i];
                }
                currentTargets[i] = _frozenTargets[i];
                continue;
            }

            _isFrozen[i] = false;

            if (db > 0 && Mathf.Abs(desired - currentTargets[i]) < db)
            {
                desired = currentTargets[i];
            }

            currentTargets[i] = desired;
        }

        WriteTargetsToSender();
    }

    private bool IsSettingsOpen()
    {
        if (settingsPanelRoot != null) return settingsPanelRoot.activeSelf;
        if (settingsOverlay != null) return settingsOverlay.IsOpen;
        return false;
    }

    private void WriteTargetsToSender()
    {
        if (serialSender.currentFingerTargets == null || serialSender.currentFingerTargets.Length != FingerCount)
        {
            serialSender.currentFingerTargets = new int[FingerCount];
        }

        for (int i = 0; i < FingerCount; i++)
        {
            serialSender.currentFingerTargets[i] = currentTargets[i];
        }
    }

    private void SetAllTargets(int v)
    {
        for (int i = 0; i < FingerCount; i++)
        {
            currentTargets[i] = v;
        }
    }

    private void ClearRuntimeState()
    {
        for (int i = 0; i < FingerCount; i++)
        {
            isTouchingKey[i] = false;
            isBottomLocked[i] = false;
            _isFrozen[i] = false;
            _frozenTargets[i] = 0;
        }
    }

    private float SafeGetCurl01(int fingerIndex)
    {
        if (curlTracker == null) return 0f;
        if (curlTracker.curl01 == null || curlTracker.curl01.Length != FingerCount) return 0f;
        float v = curlTracker.curl01[fingerIndex];
        if (float.IsNaN(v)) return 0f;
        return Mathf.Clamp01(v);
    }

    private static int Clamp1000(int v)
    {
        if (v < 0) return 0;
        if (v > DefaultServoMax) return DefaultServoMax;
        return v;
    }

    private void EnsureArrays()
    {
        if (currentTargets == null || currentTargets.Length != FingerCount) currentTargets = new int[FingerCount];
        if (isTouchingKey == null || isTouchingKey.Length != FingerCount) isTouchingKey = new bool[FingerCount];
        if (isBottomLocked == null || isBottomLocked.Length != FingerCount) isBottomLocked = new bool[FingerCount];
    }
}
