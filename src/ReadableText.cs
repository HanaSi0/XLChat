using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
namespace XLinUI;
// Shadow geometry is emitted before the actual text and follows each child's clip rectangle.
internal static class ReadableText
{
    internal static void TextUnformatted(string text) { Shadow(text, false); ImGui.TextUnformatted(text); }
    internal static void TextWrapped(string text) { Shadow(text, true); ImGui.TextWrapped(text); }
    internal static void TextWrapped(string text,float width)
    {
        width=Math.Max(1,width);
        if(Plugin.InstanceConfig.TextOutline)Outline(ImGui.GetCursorScreenPos(),text,width);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX()+width);
        try{ImGui.TextUnformatted(text);}finally{ImGui.PopTextWrapPos();}
    }
    internal static void TextDisabled(string text)
    {
        Shadow(text, false);
        ImGui.TextDisabled(text);
    }
    internal static void TextColoredWrapped(Vector4 color,string text,float width)
    {
        if(Plugin.InstanceConfig.HighContrastText)color=new Vector4(SurfacePolicy.ReadableColor(new Vector3(color.X,color.Y,color.Z)),color.W);
        ImGui.PushStyleColor(ImGuiCol.Text,color);
        try{TextWrapped(text,width);}finally{ImGui.PopStyleColor();}
    }
    internal static void TextColored(Vector4 color, string text)
    {
        if (Plugin.InstanceConfig.HighContrastText)
            color = new Vector4(SurfacePolicy.ReadableColor(new Vector3(color.X, color.Y, color.Z)), color.W);
        Shadow(text, false); ImGui.TextColored(color, text);
    }
    internal static void Draw(ImDrawListPtr draw, Vector2 position, uint color, string text)
    {
        if (Plugin.InstanceConfig.TextOutline)
        {
            draw.AddText(position + new Vector2(-1, 0), 0xDF000000, text);
            draw.AddText(position + new Vector2(1, 0), 0xDF000000, text);
            draw.AddText(position + new Vector2(0, -1), 0xDF000000, text);
            draw.AddText(position + new Vector2(0, 1), 0xDF000000, text);
        }
        draw.AddText(position, color, text);
    }
    internal static bool Button(string label, Vector2 size = default)
    {
        ControlShadow(label, size, false);
        return ImGui.Button(label, size);
    }
    internal static bool SmallButton(string label)
    { ControlShadow(label, default, true); return ImGui.SmallButton(label); }
    internal static bool Selectable(string label, bool selected = false, ImGuiSelectableFlags flags = ImGuiSelectableFlags.None, Vector2 size = default)
    { Shadow(Label(label), false); return ImGui.Selectable(label, selected, flags, size); }
    internal static bool Checkbox(string label, ref bool value)
    {
        if (Plugin.InstanceConfig.TextOutline)
            Outline(ImGui.GetCursorScreenPos() + new Vector2(ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X, ImGui.GetStyle().FramePadding.Y), Label(label), 0);
        return ImGui.Checkbox(label, ref value);
    }
    private static string Label(string label)
    { var end = label.IndexOf("##", StringComparison.Ordinal); return end < 0 ? label : label[..end]; }
    private static void ControlShadow(string label, Vector2 size, bool small)
    {
        if (!Plugin.InstanceConfig.TextOutline) return;
        var text = Label(label); var measured = ImGui.CalcTextSize(text);
        var padding = ImGui.GetStyle().FramePadding;
        if (small) padding.Y = 0;
        var width = size.X == 0 ? measured.X + padding.X * 2 : size.X < 0 ? Math.Max(1, ImGui.GetContentRegionAvail().X + size.X) : size.X;
        var height = size.Y == 0 ? measured.Y + padding.Y * 2 : size.Y;
        Outline(ImGui.GetCursorScreenPos() + new Vector2(Math.Max(0, (width - measured.X) / 2), Math.Max(0, (height - measured.Y) / 2)), text, 0);
    }
    private static void Outline(Vector2 pos, string text, float wrap)
    {
        var draw = ImGui.GetWindowDrawList(); var font = ImGui.GetFont(); var size = ImGui.GetFontSize();
        var c=Plugin.InstanceConfig;
        if(!c.TextOnly&&c.ReadabilityShade>0){var bounds=ImGui.CalcTextSize(text,false,wrap);draw.AddRectFilled(pos-new Vector2(1),pos+bounds+new Vector2(1),ImGui.ColorConvertFloat4ToU32(new Vector4(0,0,0,c.ReadabilityShade)));}
        draw.AddText(font, size, pos + new Vector2(-1, 0), 0xDF000000, text, wrap);
        draw.AddText(font, size, pos + new Vector2(1, 0), 0xDF000000, text, wrap);
        draw.AddText(font, size, pos + new Vector2(0, -1), 0xDF000000, text, wrap);
        draw.AddText(font, size, pos + new Vector2(0, 1), 0xDF000000, text, wrap);
    }
    private static void Shadow(string text, bool wrapped)
    {
        if (!Plugin.InstanceConfig.TextOutline || string.IsNullOrEmpty(text)) return;
        var pos = ImGui.GetCursorScreenPos();
        Outline(pos, text, wrapped ? Math.Max(1, ImGui.GetContentRegionAvail().X) : 0);
    }
}
