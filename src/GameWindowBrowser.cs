using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;

namespace XLinUI;
internal sealed partial class UserInterface
{
    private bool gameWindowsOpen;
    private string gameWindowSearch = "";
    private void DrawGameWindows()
    {
        var visible = BeginPanel("游戏功能", ref gameWindowsOpen, new Vector2(520, 620));
        try
        {
            if (!visible) return;
            ReadableText.TextWrapped("读取当前客户端的主菜单入口。已有替代面板使用 XLinUI；其他功能打开原生窗口，标准窗口可使用统一换肤。任务交互、商店与副本机制窗口仍由游戏提供。");
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##game-function-search", "搜索角色、背包、任务、社交、系统…", ref gameWindowSearch, 100);
            if (ReadableText.Button("窗口适配审查")) SettingsOpen = true;
            foreach (var entry in Plugin.Data.GetExcelSheet<MainCommand>().OrderBy(x => x.RowId))
            {
                var label = entry.Name.ToString();
                if (string.IsNullOrWhiteSpace(label) || (gameWindowSearch.Length > 0 && !label.Contains(gameWindowSearch, StringComparison.OrdinalIgnoreCase))) continue;
                ImGui.PushID((int)entry.RowId);
                try
                {
                    var replacement = entry.RowId switch
                    { 4 => C.ModernQuests ? "简洁任务概览 + 原版日志" : "原版日志", 10 => C.ModernInventory ? "简洁背包概览 + 原版操作" : "原版背包", 5 => "原版任务情报", 35 => "简洁传送", 66 => "简洁货币", 2 => "装备 / 配装 + 原版角色", _ => C.StyleNativeWindows ? "原版功能 · 标准窗口换肤" : "原版功能" };
                    var custom = entry.RowId == 35 || entry.RowId == 66 || (entry.RowId == 10 && C.ModernInventory) || (entry.RowId == 4 && C.ModernQuests);
                    var enabled = custom || GameBridge.MainCommandEnabled(entry.RowId);
                    ImGui.BeginDisabled(!enabled);
                    if (ReadableText.Selectable(label + "##open", false))
                    {
                        switch (entry.RowId)
                        {
                            case 35: teleportOpen = true; RefreshTeleports(); break;
                            case 66: currencyOpen = true; break;
                            default: OpenGame(entry.RowId); break;
                        }
                    }
                    ImGui.EndDisabled();
                    ReadableText.TextDisabled(enabled ? replacement : "当前游戏状态下不可用");
                }
                finally { ImGui.PopID(); }
            }
        }
        finally { EndPanel(); }
    }
}
