using UnityEngine;

/// <summary>
/// シリアルから受信した 1 行のセンサ文字列を HandCurlTracker に渡す受信専用コンポーネント。
/// SerialPortAdapter を共有して利用し、フォーマットのデコード/検証は SerialPacketCodec に委譲する。
/// </summary>
public class HandSensorReceiver : MonoBehaviour
{
    [Header("Serial")]
    [Tooltip("共通のポートアダプタ（必須）")]
    public SerialPortAdapter serialAdapter;

    [Tooltip("ボーレート。アダプタ未接続時の TryOpen に使用")]
    public int baudRate = 115200;

    [Tooltip("ポート名。アダプタが未オープンのときに TryOpen するためのデフォルト。")]
    public string portName = "COM5";

    [Header("Target")]
    public HandCurlTracker targetTracker;

    [Header("Calibration Flag (K)")]
    [Tooltip("現在 K フラグを受信しているか（キャリブレーション中）")]
    public bool isCalibrating = false;

    [Header("Logging")]
    public bool logErrors = true;
    [Tooltip("受信した生データを保持する（デバッグ表示用）")]
    [SerializeField] private string lastReceivedLine;
    [Tooltip("受信行をログ出力する")]
    public bool logRawInput = false;

    private string _latestLine;

    private void Start()
    {
        // アダプタが未オープンなら起動時に開いておく
        if (serialAdapter != null && !serialAdapter.IsOpen)
        {
            serialAdapter.TryOpen(portName, baudRate);
        }
    }

    private void Update()
    {
        if (serialAdapter == null || targetTracker == null) return;
        if (!serialAdapter.IsOpen) return;

        // 1 フレームで最新行を取得
        if (!serialAdapter.TryReadLatestLine(out _latestLine)) return;

        if (string.IsNullOrEmpty(_latestLine)) return;

        lastReceivedLine = _latestLine;
        if (logRawInput)
        {
            Debug.Log($"[HandSensorReceiver] RAW: {_latestLine}");
        }

        // K フラグ判定
        bool hasK = _latestLine.IndexOf('K') >= 0 || _latestLine.IndexOf('k') >= 0;
        isCalibrating = hasK;
        if (hasK) return; // キャリブレーション中は更新しない

        // デコード（A####B####C####D####E####）
        if (!SerialPacketCodec.TryDecode(_latestLine, out int[] decoded))
        {
            if (logErrors)
            {
                Debug.LogWarning($"[HandSensorReceiver] Invalid format: {_latestLine}");
            }
            return;
        }

        // HandCurlTracker へ渡す（デコード済み値をコピー）
        targetTracker.UpdateSensorFromDecodedValues(decoded);
    }

    /// <summary>
    /// ランタイムに COM ポート名を変更し、必要なら再接続する。
    /// </summary>
    public void SetPortNameAndReconnect(string newPortName)
    {
        if (string.IsNullOrWhiteSpace(newPortName))
        {
            if (logErrors) Debug.LogWarning("[HandSensorReceiver] newPortName が空です。");
            return;
        }

        portName = newPortName.Trim();

        if (serialAdapter == null)
        {
            if (logErrors) Debug.LogWarning("[HandSensorReceiver] serialAdapter が未設定のため再接続できません。");
            return;
        }

        serialAdapter.Close();
        serialAdapter.TryOpen(portName, baudRate);
    }
}
