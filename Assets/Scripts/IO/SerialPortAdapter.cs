using System;
using System.IO.Ports;
using UnityEngine;

/// <summary>
/// シリアルポートの開閉と行単位の読み書きを共通化する MonoBehaviour アダプタ。
/// 送受双方のコンポーネントから共有して使うことを想定する。
/// 空の GameObject にアタッチして利用する。
/// </summary>
public class SerialPortAdapter : MonoBehaviour
{
    [Tooltip("Open/Close 時のログを有効化")]
    public bool logOpenClose = true;

    [Tooltip("例外発生時のログを有効化")]
    public bool logErrors = true;

    private SerialPort _port;
    private string _currentPortName;
    private int _currentBaudRate;

    public bool IsOpen => _port != null && _port.IsOpen;
    public string CurrentPortName => _currentPortName;

    /// <summary>
    /// 指定したポートを開く。同じ設定で既に開いていれば何もしない。
    /// </summary>
    public bool TryOpen(string portName, int baudRate)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            if (logErrors) Debug.LogWarning("[SerialPortAdapter] portName が空です。");
            return false;
        }

        // 既に同じ設定で開いている場合は再接続しない
        if (IsOpen && string.Equals(_currentPortName, portName, StringComparison.OrdinalIgnoreCase) && _currentBaudRate == baudRate)
        {
            return true;
        }

        Close();

        try
        {
            _port = new SerialPort(portName.Trim(), baudRate)
            {
                NewLine = "\n",
                ReadTimeout = 5,
                WriteTimeout = 5
            };
            _port.Open();

            _currentPortName = portName.Trim();
            _currentBaudRate = baudRate;

            if (logOpenClose) Debug.Log($"[SerialPortAdapter] Opened {_currentPortName} @ {_currentBaudRate}");
            return true;
        }
        catch (Exception ex)
        {
            if (logErrors) Debug.LogError($"[SerialPortAdapter] Failed to open {portName}: {ex.Message}");
            _port = null;
            _currentPortName = null;
            return false;
        }
    }

    public void Close()
    {
        if (_port == null) return;

        try
        {
            if (_port.IsOpen)
            {
                _port.Close();
            }

            if (logOpenClose) Debug.Log($"[SerialPortAdapter] Closed {_currentPortName}");
        }
        catch (Exception ex)
        {
            if (logErrors) Debug.LogWarning($"[SerialPortAdapter] Close error on {_currentPortName}: {ex.Message}");
        }
        finally
        {
            _port = null;
            _currentPortName = null;
        }
    }

    /// <summary>
    /// 最新の 1 行を読み取る。複数行届いている場合は最後の行を返す。
    /// </summary>
    public bool TryReadLatestLine(out string latestLine)
    {
        latestLine = null;
        if (!IsOpen) return false;

        try
        {
            while (_port.BytesToRead > 0)
            {
                string line = _port.ReadLine();
                if (!string.IsNullOrEmpty(line))
                {
                    latestLine = line.Trim();
                }
            }
        }
        catch (Exception ex)
        {
            if (logErrors) Debug.LogWarning($"[SerialPortAdapter] Read error on {_currentPortName}: {ex.Message}");
            return false;
        }

        return !string.IsNullOrEmpty(latestLine);
    }

    /// <summary>
    /// 行を書き込む。開いていなければ false。
    /// </summary>
    public bool TryWriteLine(string line)
    {
        if (!IsOpen) return false;

        try
        {
            _port.WriteLine(line ?? string.Empty);
            return true;
        }
        catch (Exception ex)
        {
            if (logErrors) Debug.LogWarning($"[SerialPortAdapter] Write error on {_currentPortName}: {ex.Message}");
            return false;
        }
    }

    private void OnDestroy()
    {
        Close();
    }
}
