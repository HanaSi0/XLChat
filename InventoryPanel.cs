using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Lumina.Excel.Sheets;

namespace XLinUI;
internal sealed partial class UserInterface
{
    private bool inventoryOpen;
    private string inventorySearch = "";
    private bool inventoryList;
    private DateTime lastInventoryRead;
    private List<BagSlot> bagSlots = [];
    private int bagCapacity;
    private bool bagAvailable;
    private void DrawInventory()
    {
        var visible = BeginPanel("背包", ref inventoryOpen, new Vector2(660, 540));
        try
        {
            if (!visible) return;
            if ((DateTime.UtcNow - lastInventoryRead).TotalMilliseconds >= 500)
            {
                (bagSlots, bagCapacity, bagAvailable) = GameBridge.ReadBags();
                lastInventoryRead = DateTime.UtcNow;
            }
            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##bag-search", "搜索物品名称、分类或编号…", ref inventorySearch, 100);
            ImGui.Checkbox("列表视图", ref inventoryList); ImGui.SameLine();
            ImGui.TextDisabled($"{bagSlots.Count} / {bagCapacity} 槽位"); ImGui.SameLine();
            if (ImGui.Button("打开原版背包")) OpenGame(10, true);
            if (!bagAvailable) { ImGui.TextDisabled("背包数据尚未加载，请先打开原版背包。"); return; }
            var size = 48 * ImGuiHelpers.GlobalScale;
            var columns = Math.Max(1, (int)(ImGui.GetContentRegionAvail().X / (size + ImGui.GetStyle().ItemSpacing.X)));
            var index = 0;
            foreach (var slot in bagSlots)
            {
                var item = Plugin.Data.GetExcelSheet<Item>().GetRowOrDefault(slot.Item);
                var name = item?.Name.ToString() ?? $"物品 #{slot.Item}";
                var category = item?.ItemUICategory.Value.Name.ToString() ?? "";
                if (inventorySearch.Length > 0 && !name.Contains(inventorySearch, StringComparison.OrdinalIgnoreCase) && !category.Contains(inventorySearch, StringComparison.OrdinalIgnoreCase) && !slot.Item.ToString().Contains(inventorySearch, StringComparison.Ordinal)) continue;
                if (!inventoryList && index > 0 && index % columns != 0) ImGui.SameLine();
                ImGui.PushID(index++);
                try
                {
                    var label = name + (slot.HighQuality ? " HQ" : "");
                    if (inventoryList)
                    {
                        if (ImGui.Selectable(label + $" × {slot.Count}##item", false)) ImGui.SetClipboardText(name);
                    }
                    else
                    {
                        var pos = ImGui.GetCursorScreenPos();
                        if (ImGui.InvisibleButton("item", new Vector2(size))) ImGui.SetClipboardText(name);
                        var draw = ImGui.GetWindowDrawList();
                        var icon = item is null ? null : Plugin.Textures.GetFromGameIcon(new GameIconLookup(item.Value.Icon)).GetWrapOrDefault();
                        if (icon is not null) draw.AddImage(icon.Handle, pos, pos + new Vector2(size));
                        else draw.AddText(pos, 0xFFFFFFFF, slot.Item.ToString());
                        draw.AddRect(pos, pos + new Vector2(size), ImGui.ColorConvertFloat4ToU32(new Vector4(C.Tint, .55f)), 0f);
                        draw.AddText(pos + new Vector2(3, size - ImGui.GetFontSize()), 0xFFFFFFFF, slot.Count.ToString());
                        if (slot.HighQuality) draw.AddText(pos + new Vector2(3, 0), 0xFF8AE5ED, "HQ");
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip(label + $"\n{category} · 数量 {slot.Count}\n{item?.Description.ToString()}\n点击复制名称；使用、整理、转移与右键菜单请使用原版背包。");
                }
                finally { ImGui.PopID(); }
            }
            ImGui.Spacing(); ImGui.TextDisabled("实际背包只读概览 · 不自动使用、移动或丢弃物品");
        }
        finally { EndPanel(); }
    }
}
internal sealed record BagSlot(uint Item, int Count, bool HighQuality);
