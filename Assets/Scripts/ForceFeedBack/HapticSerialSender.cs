using System;
using System.IO.Ports;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 指ごとの目標値をサーボに送信する送信専用コンポーネント。
/// - 共通 SerialPortAdapter を介して 1 フレーム 1 行送信（A####B####C####D####E####）。
/// - 未キャリブレーション / KeyMode.ForShow / COM 未設定 では送信しない。
/// </summary>
public class HapticSerialSender : MonoBehaviour
{
    private const int FingerCount = 5;
    private static readonly int[] FingerIndexToChannelIndex = { 0, 4, 3, 2, 1 };

    [Header("Serial")]
    public SerialPortAdapter serialAdapter;
    public string portName = "";
    public int baudRate = 115200;

    [Tooltip("送信を有効化するフラグ（デバッグ用）")]
    public bool enableSend = true;

    [Header("Guards")]
    [Tooltip("キャリブレーション状態（推奨）。設定されていればこちらを優先する。")]
    public HapticCalibrationState calibrationState;

    [Tooltip("KeyMode を外部から渡す。Physical のときだけ送信する。")]
    public KeyMode keyMode = KeyMode.Physical;

    [Header("Parameters")]
    [Tooltip("送信したい指ごとの目標値（0-1000）。順番は Thumb/Index/Middle/Ring/Pinky。A/B/C/D/E への割り当ては内部で変換する。")]
    [FormerlySerializedAs("currentServoTargets")]
    public int[] currentFingerTargets = new int[FingerCount];

    [Tooltip("未指定ポート時に警告する")]
    public bool logErrors = true;

    private bool _warnedNotCalibrated = false;
    private bool _warnedNoPort = false;
    private string _zeroSentOnPortName = null;
    private float _nextOpenAttemptRealtime = 0f;
    private bool _warnedPortMissing = false;

    [Header("Auto Connect")]
    [Tooltip("true のとき、Update でポート未オープン時に自動で TryOpen を試みる")]
    public bool autoConnect = true;

    [Tooltip("Open を再試行する最短間隔（秒）。毎フレームの TryOpen を避ける")]
    public float openRetryIntervalSeconds = 1.0f;

    [Tooltip("指定 COM が存在しない場合の再試行間隔（秒）")]
    public float missingPortRetryIntervalSeconds = 5.0f;

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

    private void Start()
    {
        if (serialAdapter != null && !serialAdapter.IsOpen && !string.IsNullOrWhiteSpace(portName))
        {
            serialAdapter.TryOpen(portName, baudRate);
        }
    }

    private void Update()
    {
        if (serialAdapter == null)
        {
            SetStatus("No SerialPortAdapter");
            return;
        }

        // 未キャリブ/無効化でも、「開けるなら開く」「開いたら 0 を送る」は実施する
        EnsureOpenIfPossible();
        TrySendZeroOnOpenIfNeeded();

        if (!enableSend)
        {
            SetStatus("Disabled");
            return;
        }

        if (keyMode != KeyMode.Physical)
        {
            SetStatus("KeyMode not Physical");
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

        TrySendNow(currentFingerTargets, bypassGuards: false);
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
            if (keyMode != KeyMode.Physical)
            {
                SetStatus("KeyMode not Physical");
                return false;
            }
            if (!IsCalibrated())
            {
                SetStatus("Not calibrated");
                return false;
            }
        }

        // 受信側が同じ SerialPortAdapter で既に Open 済みの場合は、sender 側の portName 未設定でも送信できる。
        if (!serialAdapter.IsOpen)
        {
            if (string.IsNullOrWhiteSpace(portName))
            {
                if (logErrors && !_warnedNoPort)
                {
                    Debug.LogWarning("[HapticSerialSender] portName is empty and serial is not open. Skip serial send.");
                    _warnedNoPort = true;
                }
                SetStatus("portName empty (serial closed)");
                return false;
            }

            if (!serialAdapter.TryOpen(portName, baudRate))
            {
                SetStatus("Open failed");
                return false;
            }
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
        // 初期 0 送信フラグ（開いているポート名に紐づけて 1 回だけ）
        RememberZeroSentPortNameIfZero(channelTargets);

        if (ok)
        {
            sendSucceeded++;
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

    private void EnsureOpenIfPossible()
    {
        if (serialAdapter.IsOpen) return;
        if (!autoConnect) return;
        if (string.IsNullOrWhiteSpace(portName)) return;

        float now = Time.realtimeSinceStartup;
        if (now < _nextOpenAttemptRealtime) return;

        // COM が存在しない場合は TryOpen 自体を呼ばず、ログスパムを防ぐ
        if (!PortExists(portName))
        {
            SetStatus("Port not found");
            _nextOpenAttemptRealtime = now + Mathf.Max(0.1f, missingPortRetryIntervalSeconds);

            if (logErrors && !_warnedPortMissing)
            {
                Debug.LogWarning($"[HapticSerialSender] Port '{portName}' not found. AutoConnect will retry.");
                _warnedPortMissing = true;
            }
            return;
        }

        _warnedPortMissing = false;
        bool ok = serialAdapter.TryOpen(portName, baudRate);
        _nextOpenAttemptRealtime = now + Mathf.Max(0.1f, openRetryIntervalSeconds);

        if (!ok)
        {
            SetStatus("Open failed");
        }
    }

    /// <summary>
    /// シリアルが開いたタイミングで、全指 0 を 1 回だけ送る（安全な初期化用）。
    /// </summary>
    private void TrySendZeroOnOpenIfNeeded()
    {
        if (serialAdapter == null) return;
        if (!serialAdapter.IsOpen) return;

        // 再接続やポート変更でポート名が変わった場合は再度 0 を送る
        string current = serialAdapter.CurrentPortName;
        if (!string.IsNullOrEmpty(current) && string.Equals(_zeroSentOnPortName, current, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        TrySendNow(new int[FingerCount] { 0, 0, 0, 0, 0 }, bypassGuards: true);
    }

    private void RememberZeroSentPortNameIfZero(int[] targets)
    {
        if (targets == null || targets.Length != FingerCount) return;
        for (int i = 0; i < FingerCount; i++)
        {
            if (targets[i] != 0) return;
        }

        if (serialAdapter != null && serialAdapter.IsOpen && !string.IsNullOrEmpty(serialAdapter.CurrentPortName))
        {
            _zeroSentOnPortName = serialAdapter.CurrentPortName;
        }
    }

    private static bool PortExists(string port)
    {
        if (string.IsNullOrWhiteSpace(port)) return false;

        try
        {
            string p = port.Trim();
            var ports = SerialPort.GetPortNames();
            for (int i = 0; i < ports.Length; i++)
            {
                if (string.Equals(ports[i], p, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
            // GetPortNames が失敗する環境もあるため、存在チェック不能なら「存在する扱い」にして TryOpen に委ねる
            return true;
        }

        return false;
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
}
