using System.Text.RegularExpressions;

/// <summary>
/// 5 チャンネル固定のシリアル行パケットをエンコード/デコードする共通ヘルパ。
/// 並び: A/B/C/D/E + 数字（1〜4桁）。K などのフラグや余分な文字が付いていても、
/// 先頭の A〜E 5チャネルを抜き出せば良い。
/// 例: A0234B0456C0789D0123E0999K / A0B4095C2047D0E2047F52...
/// </summary>
public static class SerialPacketCodec
{
    // A####B####C####D####E#### （#### は1〜4桁、先頭に余計な文字があってもよい）
    private static readonly Regex Pattern = new Regex(@"A(\d{1,4})B(\d{1,4})C(\d{1,4})D(\d{1,4})E(\d{1,4})", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Encode(int[] values)
    {
        if (values == null || values.Length != 5) return null;
        return $"A{ClampPad(values[0])}B{ClampPad(values[1])}C{ClampPad(values[2])}D{ClampPad(values[3])}E{ClampPad(values[4])}";
    }

    public static bool TryDecode(string line, out int[] values)
    {
        values = null;
        if (string.IsNullOrEmpty(line)) return false;

        var m = Pattern.Match(line.Trim());
        if (!m.Success) return false;

        values = new int[5];
        for (int i = 0; i < 5; i++)
        {
            values[i] = int.Parse(m.Groups[i + 1].Value);
        }
        return true;
    }

    private static string ClampPad(int v)
    {
        int clamped = v < 0 ? 0 : (v > 1000 ? 1000 : v);
        return clamped.ToString("D4");
    }
}
