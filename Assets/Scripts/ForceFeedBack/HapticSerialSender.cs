using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 指ごとの目標値をサーボに送信する送信専用コンポーネント。
/// - 共通 SerialPortAdapter を介して 1 フレーム 1 行送信（A####B####C####D####E####）。
/// - ポート Open は受信側（HandSensorReceiver）に一本化し、送信側では Open を試みない。
/// - 未キャリブレーション時は送信しない。
/// </summary>
public class HapticSerialSender : MonoBehaviour
{
    private const int FingerCount = 5;
    private static readonly int[] FingerIndexToChannelIndex = { 0, 4, 3, 2, 1 };

    [Header("Serial")]
    public SerialPortAdapter serialAdapter;

    [Tooltip("送信を有効化するフラグ（デバッグ用）")]
    public bool enableSend = true;

    [Header("Guards")]
    [Tooltip("キャリブレーション状態（推奨）。設定されていればこちらを優先する。")]
    public HapticCalibrationState calibrationState;

    [Header("Parameters")]
    [Tooltip("送信したい指ごとの目標値（0-1000）。順番は Thumb/Index/Middle/Ring/Pinky。A/B/C/D/E への割り当ては内部で変換する。")]
    [FormerlySerializedAs("currentServoTargets")]
    [HideInInspector]
    public int[] currentFingerTargets = new int[FingerCount];

    [Tooltip("警告ログを出す")]
    public bool logErrors = true;

    [Header("Rate Limit")]
    [Tooltip("送信レート上限（Hz）。0以下で無制限。目安: 30")]
    public float maxSendHz = 30f;

    [Tooltip("前回送信値から変化がない（または小さい）場合は送信しない")]
    public bool sendOnlyWhenChanged = true;

    [Tooltip("送信する最小変化量（指ごとの絶対差）。例: 2")]
    public int minDeltaToSend = 2;

    private bool _warnedNotCalibrated = false;
    private bool _wasOpen = false;
    private float _lastSentRealtime = -999f;
    private bool _hasLastSent;
    private int[] _lastSentFingerTargets = new int[FingerCount];

    [Header("Debug (read-only)")]
    [SerializeField] private string lastStatus;
    [SerializeField] private string lastEncodedLine;
    [SerializeField] private int[] lastTargets = new int[FingerCount];
    [SerializeField] private int[] lastChannelTargets = new int[FingerCount];
    [SerializeField] private int sendAttempts;
    [SerializeField] private int sendSucceeded;
    [SerializeField] private int lastSendFrame = -1;
    [SerializeField] private float lastSendRealtime;

    public string LastStatus => lastStatus;
    public string LastEncodedLine => lastEncodedLine;
    public int[] LastTargets => lastTargets;
    public int[] LastChannelTargets => lastChannelTargets;
    public int SendAttempts => sendAttempts;
    public int SendSucceeded => sendSucceeded;
    public int LastSendFrame => lastSendFrame;
    public float LastSendRealtime => lastSendRealtime;

    private bool _sentZeroOnShutdown = false;

    private void Update()
    {
        if (serialAdapter == null)
        {
            SetStatus("No SerialPortAdapter");
            return;
        }

        // 受信側が Open したタイミングで、全指 0 を 1 回だけ送る（安全初期化用）
        bool isOpenNow = serialAdapter.IsOpen;
        if (isOpenNow && !_wasOpen)
        {
            TrySendZeroOnOpen();
        }
        _wasOpen = isOpenNow;

        if (!enableSend)
        {
            SetStatus("Disabled");
            return;
        }

        if (!IsCalibrated())
        {
            if (logErrors && !_warnedNotCalibrated)
            {
                Debug.LogWarning("[HapticSerialSender] Not calibrated. Servo send is disabled.");
                _warnedNotCalibrated = true;
            }
            SetStatus("Not calibrated");
            return;
        }

        if (!serialAdapter.IsOpen)
        {
            SetStatus("Serial not open");
            return;
        }

        if (currentFingerTargets == null || currentFingerTargets.Length != FingerCount)
        {
            if (logErrors) Debug.LogWarning("[HapticSerialSender] targets length invalid.");
            SetStatus("Targets invalid");
            return;
        }

        if (maxSendHz > 0f)
        {
            float minInterval = 1f / Mathf.Max(0.0001f, maxSendHz);
            float now = Time.realtimeSinceStartup;
            if (now - _lastSentRealtime < minInterval)
            {
                SetStatus("Rate limited");
                return;
            }
        }

        if (sendOnlyWhenChanged && _hasLastSent)
        {
            int threshold = Mathf.Max(0, minDeltaToSend);
            if (!HasChangedEnough(currentFingerTargets, _lastSentFingerTargets, threshold))
            {
                SetStatus("No change");
                return;
            }
        }

        TrySendNow(currentFingerTargets, bypassGuards: false);
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;
        TrySendZeroOnShutdownIfPossible();
    }

    private void OnApplicationQuit()
    {
        TrySendZeroOnShutdownIfPossible();
    }

    private void TrySendZeroOnShutdownIfPossible()
    {
        if (_sentZeroOnShutdown) return;
        if (serialAdapter == null) return;
        if (!serialAdapter.IsOpen) return; // 終了時に新規 Open はしない

        var zeros = new int[FingerCount];
        bool ok = TrySendNow(zeros, bypassGuards: true);
        if (ok)
        {
            _sentZeroOnShutdown = true;
            SetStatus("Sent zero on shutdown");
        }
    }

    private void TrySendZeroOnOpen()
    {
        if (serialAdapter == null) return;
        if (!serialAdapter.IsOpen) return;
        TrySendNow(new int[FingerCount] { 0, 0, 0, 0, 0 }, bypassGuards: true);
        SetStatus("Sent zero on open");
    }

    /// <summary>
    /// 指順（Thumb/Index/Middle/Ring/Pinky）の配列を受け取り、チャンネル順（A/B/C/D/E）へ変換して送信する。
    /// チャンネル割り当て: A=Thumb, B=Pinky, C=Ring, D=Middle, E=Index
    /// </summary>
    public bool TrySendNow(int[] fingerTargets, bool bypassGuards)
    {
        sendAttempts++;

        if (serialAdapter == null)
        {
            SetStatus("No SerialPortAdapter");
            return false;
        }

        if (!bypassGuards)
        {
            if (!IsCalibrated())
            {
                SetStatus("Not calibrated");
                return false;
            }
        }

        if (!serialAdapter.IsOpen)
        {
            SetStatus("Serial not open");
            return false;
        }

        if (fingerTargets == null || fingerTargets.Length != FingerCount)
        {
            if (logErrors) Debug.LogWarning("[HapticSerialSender] targets length invalid.");
            SetStatus("Targets invalid");
            return false;
        }

        CopyTargets(fingerTargets);
        int[] channelTargets = RemapFingerTargetsToChannelTargets(fingerTargets);
        CopyChannelTargets(channelTargets);

        var encoded = SerialPacketCodec.Encode(channelTargets);
        if (encoded == null)
        {
            if (logErrors) Debug.LogWarning("[HapticSerialSender] encode failed.");
            SetStatus("Encode failed");
            return false;
        }

        lastEncodedLine = encoded;
        bool ok = serialAdapter.TryWriteLine(encoded);
        lastSendFrame = Time.frameCount;
        lastSendRealtime = Time.realtimeSinceStartup;
        _lastSentRealtime = lastSendRealtime;

        if (ok)
        {
            sendSucceeded++;
            RememberLastSent(fingerTargets);
            SetStatus("Sent");
        }
        else
        {
            SetStatus("Write failed");
        }

        return ok;
    }

    private bool IsCalibrated()
    {
        return calibrationState != null && calibrationState.IsFullyCalibrated;
    }

    private void SetStatus(string s)
    {
        lastStatus = s;
    }

    private void CopyTargets(int[] src)
    {
        if (lastTargets == null || lastTargets.Length != FingerCount)
        {
            lastTargets = new int[FingerCount];
        }

        for (int i = 0; i < FingerCount; i++)
        {
            lastTargets[i] = src[i];
        }
    }

    private void CopyChannelTargets(int[] src)
    {
        if (lastChannelTargets == null || lastChannelTargets.Length != FingerCount)
        {
            lastChannelTargets = new int[FingerCount];
        }

        for (int i = 0; i < FingerCount; i++)
        {
            lastChannelTargets[i] = src[i];
        }
    }

    private static int[] RemapFingerTargetsToChannelTargets(int[] fingerTargets)
    {
        var channel = new int[FingerCount];
        for (int fingerIndex = 0; fingerIndex < FingerCount; fingerIndex++)
        {
            int channelIndex = FingerIndexToChannelIndex[fingerIndex];
            channel[channelIndex] = fingerTargets[fingerIndex];
        }
        return channel;
    }

    private void RememberLastSent(int[] fingerTargets)
    {
        if (_lastSentFingerTargets == null || _lastSentFingerTargets.Length != FingerCount)
        {
            _lastSentFingerTargets = new int[FingerCount];
        }

        for (int i = 0; i < FingerCount; i++)
        {
            _lastSentFingerTargets[i] = fingerTargets[i];
        }
        _hasLastSent = true;
    }

    private static bool HasChangedEnough(int[] a, int[] b, int minDelta)
    {
        if (a == null || b == null) return true;
        if (a.Length != FingerCount || b.Length != FingerCount) return true;

        for (int i = 0; i < FingerCount; i++)
        {
            if (Mathf.Abs(a[i] - b[i]) >= minDelta) return true;
        }

        return false;
    }
}
