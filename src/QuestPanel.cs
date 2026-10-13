using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
namespace XLinUI;
internal sealed partial class UserInterface
{
    private bool questsOpen;
    private string questSearch = "";
    private DateTime lastQuestRead;
    private List<AcceptedQuest> acceptedQuests = [];
    private void DrawQuests()
    {
        var visible = BeginPanel("任务", ref questsOpen, new Vector2(520, 540));
        try
        {
            if (!visible) return;
            if ((DateTime.UtcNow - lastQuestRead).TotalSeconds >= 1)
            { acceptedQuests = GameBridge.ReadAcceptedQuests(); lastQuestRead = DateTime.UtcNow; }
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##quest-filter", "搜索已接受任务…", ref questSearch, 100);
            if (ReadableText.Button("打开完整任务日志")) OpenGame(4, true);
            ImGui.SameLine(); ReadableText.TextDisabled($"已接受 {acceptedQuests.Count} 个普通任务");
            foreach (var entry in acceptedQuests.OrderByDescending(q => q.Priority))
            {
                var quest = Plugin.Data.GetExcelSheet<Quest>().GetRowOrDefault(0x10000u + entry.Id);
                var name = quest?.Name.ToString() ?? $"任务 #{entry.Id}";
                if (questSearch.Length > 0 && !name.Contains(questSearch, StringComparison.OrdinalIgnoreCase)) continue;
                ImGui.PushID(entry.Id);
                try
                {
                    if (ReadableText.Selectable((entry.Priority ? "★ " : "") + name + "##quest", false)) ImGui.SetClipboardText(name);
                    ReadableText.TextDisabled(entry.Hidden ? "已接受 · 原生追踪隐藏" : "已接受");
                    ImGui.Separator();
                }
                finally { ImGui.PopID(); }
            }
            ReadableText.TextWrapped("简洁任务概览；具体目标、地图定位、任务提交及理符／日常详情保留在原版日志和追踪器中。点击任务复制名称。");
        }
        finally { EndPanel(); }
    }
}
internal sealed record AcceptedQuest(ushort Id, bool Priority, bool Hidden);
