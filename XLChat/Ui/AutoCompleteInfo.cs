using System.Text;
using XLChat.Util;

namespace XLChat.Ui;

public class AutoCompleteInfo
{
    public string ToComplete;
    // Always UTF-16 offsets into the original input, never ImGui byte offsets.
    public int StartPos { get; }
    public int EndPos { get; }

    public AutoCompleteInfo(string toComplete, int startPos, int endPos)
    {
        ToComplete = toComplete;
        StartPos = startPos;
        EndPos = endPos;
    }

    public static AutoCompleteInfo FromUtf8(ReadOnlySpan<byte> input, int cursor)
    {
        var text = Encoding.UTF8.GetString(input);
        var end = TextInputOffsets.ToUtf16(input, cursor);
        var start = end == 0 ? 0 : text.LastIndexOf(' ', end - 1) + 1;
        return new AutoCompleteInfo(text[start..end], start, end);
    }
}
