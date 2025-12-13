using UnityEngine;

/// <summary>
/// 指定した間隔で空文字（改行のみ）を送信するキープアライブ用コンポーネント。
/// ESP 側が改行トリガーでレスポンスを返す場合に使用する。
/// 他機能が送信する際はそちらが上書き送信するだけで共存可能。
/// </summary>
public class SerialEmptyLinePinger : MonoBehaviour
{
    [Tooltip("送信に使用する共有アダプタ")]
    public SerialPortAdapter serialAdapter;

    [Tooltip("送信間隔（秒）")]
    public float interval = 0.01f; // 100Hz 相当

    [Tooltip("有効化フラグ")]
    public bool enablePing = true;

    private float _lastSent;

    private void Update()
    {
        if (!enablePing) return;
        if (serialAdapter == null || !serialAdapter.IsOpen) return;

        if (Time.time - _lastSent >= interval)
        {
            serialAdapter.TryWriteLine(string.Empty);
            _lastSent = Time.time;
        }
    }
}
