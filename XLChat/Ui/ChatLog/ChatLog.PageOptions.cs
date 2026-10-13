using XLChat.Code;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace XLChat.Ui.ChatLog;
public partial class ChatLog
{
    private Guid? revealSidebarPage;

    // Display pinning never changes config indices or the active conversation.
    public static IEnumerable<int> OrderedPageIndices(IReadOnlyList<Tab> tabs)
    {
        for (var group = 0; group < 2; group++)
            for (var i = 0; i < tabs.Count; i++)
                if (tabs[i].IsPinned == (group == 0)) yield return i;
    }

    private void DrawCurrentPageOptions(Tab tab, ref bool changed)
    {
        using var options = ImRaii.Menu("当前标签页设置");
        if (!options.Success) return;
        ImGui.TextDisabled("仅应用到当前右击的标签页");
        if (ImGui.Checkbox("新消息提醒（关闭即静默）", ref tab.NotifyNewMessages))
        {
            tab.Unread = 0;
            if (tab.NotifyNewMessages && tab.UnreadMode == UnreadMode.None) tab.UnreadMode = UnreadMode.All;
            changed = true;
        }
        changed |= ImGui.Checkbox("显示时间", ref tab.DisplayTimestamp);
        changed |= ImGui.Checkbox("禁止输入", ref tab.InputDisabled);
        changed |= ImGui.Checkbox("独立不透明度（弹出窗口）", ref tab.IndependentOpacity);
        if (tab.IndependentOpacity) changed |= ImGui.SliderFloat("不透明度", ref tab.Opacity, 0, 100, "%.0f%%");
        // Fixed private conversations keep their recipient and strict per-person filter.
        if (tab.TellTarget.IsSet())
        {
            ImGui.TextWrapped("私聊对象：" + tab.TellTarget.ToTargetString());
            return;
        }
        using var filters = ImRaii.Menu("接收频道");
        if (!filters.Success) return;
        foreach (var type in Enum.GetValues<ChatType>())
        {
            var enabled = tab.SelectedChannels.ContainsKey(type);
            if (!ImGui.Checkbox(type.Name(), ref enabled)) continue;
            if (enabled) tab.SelectedChannels[type] = (ChatSourceExt.All, ChatSourceExt.All);
            else tab.SelectedChannels.Remove(type);
            changed = true;
            Plugin.MessageManager.FilterAllTabsAsync();
        }
    }
}
