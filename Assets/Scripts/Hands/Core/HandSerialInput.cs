using System.IO.Ports;
using UnityEngine;

/// <summary>
/// 指定したシリアルポート（COM）から 1 行ごとのセンサ文字列を読み込み、
/// 同じオブジェクト上の HandCurlTracker に渡すコンポーネントである。
/// 左右の手で portName を変えることで、別 COM を扱える。
/// </summary>
public class HandSerialInput : MonoBehaviour
{
    [Header("Serial Port")]
    [Tooltip("使用するシリアルポート名（例: COM3, COM4）。COM10 以降は \"\\\\.\\COM10\" 形式を推奨。")]
    public string portName = "COM5";

    [Tooltip("ボーレート")]
    public int baudRate = 115200;

    [Tooltip("Start 時に自動でポートを開くかどうか")]
    public bool autoOpenOnStart = true;

    [Header("Target")]
    [Tooltip("センサ文字列を渡す HandCurlTracker")]
    public HandCurlTracker targetTracker;

    [Header("Request (ESP Trigger)")]
    [Tooltip("定期的に ESP にリクエストを送るかどうか")]
    public bool sendRequest = true;

    [Tooltip("リクエスト送信間隔 (秒)。例: 0.01 = 約 100Hz")]
    public float requestInterval = 0.01f;

    [Tooltip("ESP に送るコマンド。空文字")]
    public string requestCommand = "";

    [Header("Debug")]
    public bool logOpenClose = true;
    public bool logErrors = true;

    [Tooltip("最後に受信した生データ 1 行分（例: A2301B2391C3431D3313E1234）")]
    [SerializeField] private string lastEncodedLine;

    [Header("Calibrate Flag (K)")]
    [Tooltip("現在 K フラグを受信しているか（キャリブレーション中）")]
    public bool isCalibrating = false;

    private SerialPort _port;
    private string _latestLine;
    private float _lastRequestTime;

    private void Start()
    {
        if (autoOpenOnStart)
        {
            OpenPort();
        }
    }

    private void Update()
    {
        if (_port == null || !_port.IsOpen || targetTracker == null)
        {
            return;
        }

        // 一定間隔で ESP に改行付きコマンドを送信し、1 行返してもらう想定
        if (sendRequest && Time.time - _lastRequestTime >= requestInterval)
        {
            try
            {
                _port.WriteLine(requestCommand ?? string.Empty);
            }
            catch (System.Exception ex)
            {
                if (logErrors)
                {
                    Debug.LogWarning($"[HandSerialInput] Write error on {portName}: {ex.Message}");
                }
            }

            _lastRequestTime = Time.time;
        }

        try
        {
            // 1 フレーム中に複数行届いている場合は最後の行を使う
            while (_port.BytesToRead > 0)
            {
                string line = _port.ReadLine();
                if (!string.IsNullOrEmpty(line))
                {
                    _latestLine = line.Trim();
                }
            }
        }
        catch (System.Exception ex)
        {
            if (logErrors)
            {
                Debug.LogWarning($"[HandSerialInput] Read error on {portName}: {ex.Message}");
            }
        }

        if (!string.IsNullOrEmpty(_latestLine))
        {
            // インスペクタから生データを確認できるように保存
            lastEncodedLine = _latestLine;

            // K フラグが含まれている間は「キャリブレーション中」とみなし、
            // HandCurlTracker へのセンサ更新を止める（指の動きを凍結する）
            bool hasK = _latestLine.IndexOf('K') >= 0 || _latestLine.IndexOf('k') >= 0;
            isCalibrating = hasK;

            if (!hasK)
            {
                targetTracker.UpdateSensorFromEncodedString(_latestLine);
            }
        }
    }

    private void OnDestroy()
    {
        ClosePort();
    }

    public void OpenPort()
    {
        if (string.IsNullOrEmpty(portName))
        {
            if (logErrors)
            {
                Debug.LogWarning("[HandSerialInput] portName が設定されていない。");
            }
            return;
        }

        if (_port != null && _port.IsOpen)
        {
            return;
        }

        try
        {
            _port = new SerialPort(portName, baudRate)
            {
                NewLine = "\n",
                ReadTimeout = 5
            };
            _port.Open();

            if (logOpenClose)
            {
                Debug.Log($"[HandSerialInput] Opened {portName} @ {baudRate}");
            }
        }
        catch (System.Exception ex)
        {
            if (logErrors)
            {
                Debug.LogError($"[HandSerialInput] Failed to open {portName}: {ex.Message}");
            }
            _port = null;
        }
    }

    public void ClosePort()
    {
        if (_port == null)
        {
            return;
        }

        try
        {
            if (_port.IsOpen)
            {
                _port.Close();
            }

            if (logOpenClose)
            {
                Debug.Log($"[HandSerialInput] Closed {portName}");
            }
        }
        catch (System.Exception ex)
        {
            if (logErrors)
            {
                Debug.LogWarning($"[HandSerialInput] Failed to close {portName}: {ex.Message}");
            }
        }

        _port = null;
    }
}
