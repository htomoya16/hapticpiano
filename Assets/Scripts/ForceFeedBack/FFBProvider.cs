using Valve.VR;

// 左右それぞれの手に対応する Force Feedback プロバイダ。
// 内部的には NamedPipesProvider を持ち、VRFFBInput を送信する。
class FFBProvider
{
    private NamedPipesProvider _namedPipeProvider;
    public ETrackedControllerRole controllerRole;

    public FFBProvider(ETrackedControllerRole controllerRole)
    {
        this.controllerRole = controllerRole;

        // コントローラの役割（左手／右手）に応じた Named Pipe に接続する
        _namedPipeProvider = new NamedPipesProvider(controllerRole);
        _namedPipeProvider.Connect();
    }

    // Force Feedback の値を送信
    public bool SetFFB(VRFFBInput input)
    {
        return _namedPipeProvider.Send(input);
    }

    // パイプ切断
    public void Close()
    {
        _namedPipeProvider.Disconnect();
    }
}
