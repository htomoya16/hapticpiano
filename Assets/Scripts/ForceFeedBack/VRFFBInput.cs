// VRFFBInput.cs
using System.Runtime.InteropServices;

// 指ごとの Force Feedback 入力値（親指〜小指）。
// 0〜1000 の short で表現される。
[StructLayout(LayoutKind.Sequential)]
public struct VRFFBInput
{
    // コンストラクタ
    // thumbCurl〜pinkyCurl に 0〜1000 の値を渡す。
    public VRFFBInput(short thumbCurl, short indexCurl, short middleCurl, short ringCurl, short pinkyCurl)
    {
        this.thumbCurl = thumbCurl;
        this.indexCurl = indexCurl;
        this.middleCurl = middleCurl;
        this.ringCurl = ringCurl;
        this.pinkyCurl = pinkyCurl;
    }

    // 親指
    public short thumbCurl;
    // 人差し指
    public short indexCurl;
    // 中指
    public short middleCurl;
    // 薬指
    public short ringCurl;
    // 小指
    public short pinkyCurl;
}
