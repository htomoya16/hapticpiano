using UnityEngine;

/// <summary>
/// 指ごとの目標値をサーボに送信する送信専用コンポーネント。
/// - 共通 SerialPortAdapter を介して 1 フレーム 1 行送信（A####B####C####D####E####）。
/// - 未キャリブレーション / KeyMode.ForShow / COM 未設定 では送信しない。
/// </summary>
public class HapticSerialSender : MonoBehaviour
{
    [Header("Serial")]
    public SerialPortAdapter serialAdapter;
    public string portName = "COM6";
    public int baudRate = 115200;

    [Tooltip("送信を有効化するフラグ（デバッグ用）")]
    public bool enableSend = true;

    [Header("Guards")]
    [Tooltip("キャリブレーション済みかどうか（外部が設定）")]
    public bool isCalibrated = false;

    [Tooltip("KeyMode を外部から渡す。Physical のときだけ送信する。")]
    public KeyMode keyMode = KeyMode.Physical;

    [Header("Parameters")]
    [Tooltip("指の先行係数などを計算済みで渡す想定。0-1000 の配列。順番は A,B,C,D,E。")]
    public int[] currentServoTargets = new int[5];

    [Tooltip("未指定ポート時に警告する")]
    public bool logErrors = true;

    private void Start()
    {
        if (serialAdapter != null && !serialAdapter.IsOpen && !string.IsNullOrWhiteSpace(portName))
        {
            serialAdapter.TryOpen(portName, baudRate);
        }
    }

    private void Update()
    {
        if (!enableSend) return;
        if (serialAdapter == null) return;
        if (keyMode != KeyMode.Physical) return;
        if (!isCalibrated) return;
        if (!serialAdapter.IsOpen)
        {
            if (!string.IsNullOrWhiteSpace(portName))
            {
                serialAdapter.TryOpen(portName, baudRate);
            }
            return;
        }

        if (currentServoTargets == null || currentServoTargets.Length != 5)
        {
            if (logErrors) Debug.LogWarning("[HapticSerialSender] targets length invalid.");
            return;
        }

        var encoded = SerialPacketCodec.Encode(currentServoTargets);
        if (encoded == null)
        {
            if (logErrors) Debug.LogWarning("[HapticSerialSender] encode failed.");
            return;
        }

        serialAdapter.TryWriteLine(encoded);
    }
}
