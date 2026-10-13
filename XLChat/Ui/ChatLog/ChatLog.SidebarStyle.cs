using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace XLChat.Ui.ChatLog;

public partial class ChatLog
{
    // Reserve room for both navigation and conversation, including on small windows.
    public static float ClampSidebarWidth(float requested, float available)
        => Math.Clamp(float.IsFinite(requested) ? requested : 160, 96, Math.Max(96, available - 200));

    private static bool DrawSidebarPage(string label, string unread, int index, bool selected)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var width = Math.Max(1, ImGui.GetContentRegionAvail().X);
        var padding = 12 * scale;
        var text = label + unread;
        var textWidth = Math.Max(1, width - padding * 2);
        var textSize = ImGui.CalcTextSize(text, false, textWidth);
        var height = Math.Max(40 * scale, textSize.Y + padding * 2);
        var start = ImGui.GetCursorScreenPos();
        var clicked = ImGui.InvisibleButton($"log-tab-{index}", new Vector2(width, height));
        var hovered = ImGui.IsItemHovered();
        var draw = ImGui.GetWindowDrawList();
        if (selected || hovered)
        {
            var fill = selected ? new Vector4(.14f,.48f,.32f,.25f) : new Vector4(.5f,.6f,.65f,.10f);
            draw.AddRectFilled(start, start + new Vector2(width, height), ImGui.ColorConvertFloat4ToU32(fill), 8 * scale);
        }
        if (selected)
            draw.AddRectFilled(start + new Vector2(0, 10 * scale), start + new Vector2(3 * scale, height - 10 * scale), ImGui.ColorConvertFloat4ToU32(new Vector4(.3f,.85f,.55f,1)), 2 * scale);
        draw.PushClipRect(start, start + new Vector2(width, height), true);
        draw.AddText(ImGui.GetFont(), ImGui.GetFontSize(), start + new Vector2(padding, padding), ImGui.GetColorU32(ImGuiCol.Text), text, textWidth);
        draw.PopClipRect();
        return clicked;
    }
}
