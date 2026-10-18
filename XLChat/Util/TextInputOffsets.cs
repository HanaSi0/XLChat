using System.Text;

namespace XLChat.Util;

public static class TextInputOffsets
{
    // ImGui uses UTF-8 bytes; .NET strings and preview selections use UTF-16.
    public static int ToUtf8(string text, int offset)
    {
        offset = Math.Clamp(offset, 0, text.Length);
        if (offset > 0 && offset < text.Length && char.IsHighSurrogate(text[offset - 1]) && char.IsLowSurrogate(text[offset]))
            offset--;
        return Encoding.UTF8.GetByteCount(text.AsSpan(0, offset));
    }

    public static int ToUtf16(ReadOnlySpan<byte> text, int offset)
    {
        offset = Math.Clamp(offset, 0, text.Length);
        while (offset > 0 && offset < text.Length && (text[offset] & 0xC0) == 0x80)
            offset--;
        return Encoding.UTF8.GetCharCount(text[..offset]);
    }
}
