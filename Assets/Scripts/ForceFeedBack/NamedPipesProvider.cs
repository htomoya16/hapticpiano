// NamedPipesProvider.cs
using System;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using UnityEngine;
using Valve.VR;

// 実際に Named Pipe で別プロセス（ドライバ側）と通信するクラス。
// パイプ名： "vrapplication/ffb/curl/left" または "vrapplication/ffb/curl/right"
class NamedPipesProvider
{
    private NamedPipeClientStream _pipe;
    private readonly ETrackedControllerRole _controllerRole;

    public NamedPipesProvider(ETrackedControllerRole controllerRole)
    {
        _controllerRole = controllerRole;
        _pipe = new NamedPipeClientStream(
            "vrapplication/ffb/curl/" +
            (controllerRole == ETrackedControllerRole.RightHand ? "right" : "left")
        );
    }

    public void Connect(int timeoutMs = 100)  // タイムアウト追加
    {
        try
        {
            Debug.Log($"[FFB] Connecting to pipe ({_controllerRole})");
            _pipe.Connect(timeoutMs);  // 100ms だけ待つ

            Debug.Log($"[FFB] Successfully connected to pipe ({_controllerRole})");
        }
        catch (TimeoutException)
        {
            Debug.LogWarning($"[FFB] Pipe connect timeout ({_controllerRole}). " +
                             "Assuming driver is not running; disabling FFB for this hand.");
            // ドライバがいない前提で動く。_pipe.IsConnected は false のまま。
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[FFB] Unable to connect to pipe ({_controllerRole}): {e}");
        }
    }

    // パイプ切断
    public void Disconnect()
    {
        if (_pipe.IsConnected)
        {
            _pipe.Dispose();
        }
    }

    // VRFFBInput をバイト列に変換してパイプ経由で送信
    public bool Send(VRFFBInput input)
    {
        if (_pipe.IsConnected)
        {
            Debug.Log("running task");

            // 構造体のサイズを取得して、その分のバッファを用意
            int size = Marshal.SizeOf(input);
            byte[] arr = new byte[size];

            // アンマネージドメモリを確保し、構造体をそこにコピー
            IntPtr ptr = Marshal.AllocHGlobal(size);
            Marshal.StructureToPtr(input, ptr, true);
            Marshal.Copy(ptr, arr, 0, size);
            Marshal.FreeHGlobal(ptr);

            // バイト配列をパイプに書き込む
            _pipe.Write(arr, 0, size);

            Debug.Log("Sent force feedback message.");

            return true;
        }

        // 接続されていない場合は false を返す
        return false;
    }
}
