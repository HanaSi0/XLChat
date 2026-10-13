using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Config;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Game;
using Lumina.Excel.Sheets;

namespace XLinUI;

internal sealed partial class UserInterface
{
    private bool effectsOpen, tribesOpen, onlineOpen, jobsOpen, equipmentOpen, currencyOpen, volumeOpen;
    private List<GearsetSummary> gearsets = [];
    private uint? repairMaterialId;
    private string selectedCommand = "";
    private DateTime currencyRead;
    private List<CurrencySummary> currencies = [];
    private (int Acquired, int Limit) weeklyTomes;

    private void OpenAction(uint id)
    {
        C.ShowOriginalUi = true; plugin.Save();
        Native(() => GameBridge.UseGeneralAction(id));
    }

    private void OpenPersonalInfo()
    {
        C.ShowOriginalUi = true; plugin.Save();
        Native(() =>
        {
            if (!GameBridge.OpenSearchInfo()) notice = "个人信息尚未就绪：请先打开原版社交窗口，再重试。";
        });
    }

    private void EndPanel()
    {
        if (!C.LockWindows && ImGui.IsWindowHovered(ImGuiHoveredFlags.RootAndChildWindows) && !ImGui.IsAnyItemHovered() && !ImGui.IsAnyItemActive() && ImGui.IsMouseDragging(ImGuiMouseButton.Left))
            ImGui.SetWindowPos(ImGui.GetWindowPos() + ImGui.GetIO().MouseDelta);
        ImGui.End();
    }

    private void DrawVolume()
    {
        var visible = BeginPanel("音量", ref volumeOpen, new Vector2(330, 160));
        try
        {
            if (!visible) return;
            if (!Plugin.GameConfig.System.TryGet("SoundMaster", out uint current)) return;
            var volume = (int)Math.Clamp(current, 0, 100);
            ImGui.BeginDisabled(!C.NativeInteractions);
            if (ImGui.SliderInt("主音量", ref volume, 0, 100, "%d%%"))
                Native(() => Plugin.GameConfig.System.Set("SoundMaster", (uint)volume));
            ImGui.EndDisabled();
            if (ReadableText.Button("完整声音设置")) OpenGame(19);
        }
        finally { EndPanel(); }
    }

    private bool BeginPanel(string title, ref bool open, Vector2 size)
    {
        ImGui.SetNextWindowSize(size * ImGuiHelpers.GlobalScale, ImGuiCond.FirstUseEver);
        var visible = ImGui.Begin(title + "###XLinUI" + title, WindowFlags);
        if (visible)
        {
            Appearance.DrawBackground();
            ReadableText.TextUnformatted(title); ImGui.SameLine();
            if (ReadableText.SmallButton("关闭")) open = false;
            ImGui.Separator();
        }
        return visible && open;
    }

    private void DrawUtilityPanels()
    {
        if (volumeOpen) DrawVolume();
        if (effectsOpen) DrawEffects();
        if (tribesOpen) DrawTribes();
        if (onlineOpen) DrawOnline();
        if (jobsOpen) DrawJobs();
        if (equipmentOpen) DrawEquipment();
        if (currencyOpen) DrawCurrency();
    }

    private void DrawEffects()
    {
        var visible = BeginPanel("特效与召唤兽", ref effectsOpen, new Vector2(410, 470));
        try
        {
            if (!visible) return;
            ReadableText.TextDisabled("读取游戏设置；启用游戏交互后可修改。");
            Effect("自身特效", UiConfigOption.BattleEffectSelf);
            Effect("队友特效", UiConfigOption.BattleEffectParty);
            Effect("其他玩家特效", UiConfigOption.BattleEffectOther);
            Effect("PvP 敌方特效", UiConfigOption.BattleEffectPvPEnemyPc);
            ImGui.Separator();
            Pet("巴哈姆特", UiConfigOption.BahamutSize);
            Pet("凤凰", UiConfigOption.PhoenixSize);
            Pet("迦楼罗", UiConfigOption.GarudaSize);
            Pet("泰坦", UiConfigOption.TitanSize);
            Pet("伊弗利特", UiConfigOption.IfritSize);
            Pet("魔兽", UiConfigOption.SolBahamutSize);
        }
        finally { EndPanel(); }
    }

    private void Effect(string name, UiConfigOption option)
    {
        if (!Plugin.GameConfig.UiConfig.TryGet(option.ToString(), out uint value)) { ReadableText.TextDisabled(name + "：当前客户端不支持"); return; }
        var index = 2 - (int)Math.Clamp(value, 0, 2);
        ImGui.BeginDisabled(!C.NativeInteractions);
        if (ImGui.SliderInt(name, ref index, 0, 2, new[] { "关闭", "简单", "完全" }[index]))
            Native(() => Plugin.GameConfig.UiConfig.Set(option.ToString(), (uint)(2 - index)));
        ImGui.EndDisabled();
    }

    private void Pet(string name, UiConfigOption option)
    {
        if (!Plugin.GameConfig.UiConfig.TryGet(option.ToString(), out uint value)) { ReadableText.TextDisabled(name + "：当前客户端不支持"); return; }
        var index = (int)Math.Clamp(value, 0, 2);
        ImGui.BeginDisabled(!C.NativeInteractions);
        if (ImGui.SliderInt(name, ref index, 0, 2, new[] { "小", "中", "大" }[index]))
            Native(() => Plugin.GameConfig.UiConfig.Set(option.ToString(), (uint)index));
        ImGui.EndDisabled();
    }

    private void DrawTribes()
    {
        var visible = BeginPanel("部族友好度", ref tribesOpen, new Vector2(380, 650));
        try
        {
            if (!visible) return;
            ReadableText.TextUnformatted($"剩余部族任务次数：{GameBridge.TribeAllowance()}");
            uint? lastExpansion = null;
            foreach (var tribe in Plugin.Data.GetExcelSheet<BeastTribe>())
            {
                if (tribe.RowId == 0 || tribe.RowId > byte.MaxValue) continue;
                var rep = GameBridge.ReadReputation((byte)tribe.RowId);
                var rank = (byte)(rep.Rank & 0x7F);
                if (rank == 0) continue;
                if (lastExpansion != tribe.Expansion.RowId)
                {
                    lastExpansion = tribe.Expansion.RowId;
                    var expansion = Plugin.Data.GetExcelSheet<ExVersion>().GetRowOrDefault(tribe.Expansion.RowId);
                    ReadableText.TextDisabled(expansion?.Name.ToString() ?? $"资料片 {tribe.Expansion.RowId}");
                }
                ImGui.PushID((int)tribe.RowId);
                GameIcon(tribe.IconReputation, 32); ImGui.SameLine();
                ImGui.BeginGroup();
                ReadableText.TextUnformatted(tribe.Name.ToString());
                var rankRow = Plugin.Data.GetExcelSheet<BeastReputationRank>().GetRowOrDefault(rank);
                var rankName = rankRow?.Name.ToString() ?? rank.ToString();
                ReadableText.TextDisabled($"{rankName} ({rank}/{tribe.MaxRank})");
                ImGui.EndGroup();
                ImGui.ProgressBar(rep.Required == 0 ? 1 : Math.Clamp((float)rep.Current / rep.Required, 0, 1), new Vector2(-1, 10), rep.Required == 0 ? "最高等级" : $"{rep.Current} / {rep.Required}");
                var currency = tribe.CurrencyItem.Value;
                if (currency.RowId != 0) ReadableText.TextDisabled($"{currency.Name}：{GameBridge.ItemCount(currency.RowId)}");
                ImGui.Separator(); ImGui.PopID();
            }
        }
        finally { EndPanel(); }
    }

    private void DrawOnline()
    {
        var visible = BeginPanel("在线状态", ref onlineOpen, new Vector2(330, 490));
        try
        {
            if (!visible) return;
            // Only user-selectable states from the supplied reference; never system or GM states.
            foreach (var id in new uint[] { 47, 17, 12, 22, 21, 27, 28, 30, 29 })
            {
                var row = Plugin.Data.GetExcelSheet<OnlineStatus>().GetRowOrDefault(id);
                if (row is null) continue;
                GameIcon(row.Value.Icon, 28); ImGui.SameLine();
                if (ReadableText.Selectable(row.Value.Name.ToString() + "###online" + id)) Native(() => GameBridge.SetOnlineStatus(id));
            }
            ReadableText.TextWrapped("可用的指导者状态和权限由游戏校验。需启用游戏交互。");
            if (ReadableText.Button("打开个人信息")) OpenPersonalInfo();
        }
        finally { EndPanel(); }
    }

    private void DrawJobs()
    {
        var visible = BeginPanel("职业与装备套装", ref jobsOpen, new Vector2(820, 670));
        try
        {
            if (!visible) return;
            if (ReadableText.Button("刷新套装")) gearsets = GameBridge.ReadGearsets();
            ReadableText.TextDisabled("点击切换已保存装备套装；等级来自游戏，装等来自套装记录。");
            if (ImGui.BeginTable("job-columns", 3, ImGuiTableFlags.SizingStretchSame))
            {
                try
                {
                    ImGui.TableNextRow();
                    string[][] columns = [["防护职业", "治疗职业"], ["近战职业", "远程物理职业", "远程魔法职业"], ["大地使者", "能工巧匠", "其他职业"]];
                    for (var column = 0; column < columns.Length; column++)
                    {
                        ImGui.TableSetColumnIndex(column);
                        foreach (var group in columns[column])
                        {
                            var matching = gearsets.Where(g => JobGroup(g.Job) == group).ToArray();
                            if (matching.Length == 0) continue;
                            ReadableText.TextColored(new Vector4(C.Tint, 1), group);
                            foreach (var gear in matching)
                            {
                                var job = Plugin.Data.GetExcelSheet<ClassJob>().GetRowOrDefault(gear.Job);
                                if (job is null) continue;
                                var level = Plugin.Player.GetClassJobLevel(job.Value);
                                ImGui.PushID(gear.Index);
                                GameIcon((uint)(62100 + gear.Job), 30); ImGui.SameLine();
                                var pos = ImGui.GetCursorScreenPos();
                                var width = ImGui.GetContentRegionAvail().X;
                                if (ReadableText.Selectable("##gearset", false, ImGuiSelectableFlags.None, new Vector2(width, 40 * ImGuiHelpers.GlobalScale)))
                                    Native(() => GameBridge.EquipGearset(gear.Index));
                                var draw = ImGui.GetWindowDrawList();
                                draw.AddText(pos, 0xFFF4EBE5, job.Value.Name.ToString());
                                draw.AddText(pos + new Vector2(0, 18 * ImGuiHelpers.GlobalScale), 0xFF90CBE9, $"Lv.{level}");
                                var ilvl = gear.ItemLevel.ToString();
                                draw.AddText(pos + new Vector2(Math.Max(0, width - ImGui.CalcTextSize(ilvl).X - 4), 0), 0xFFF4EBE5, ilvl);
                                if (ImGui.IsItemHovered()) ImGui.SetTooltip(gear.Name);
                                ImGui.PopID();
                            }
                            ImGui.Spacing();
                        }
                    }
                }
                finally { ImGui.EndTable(); }
            }
            if (gearsets.Count == 0) ReadableText.TextDisabled("未读取到保存的装备套装；可在原版角色界面保存。");
            if (ReadableText.Button("原版角色／套装界面")) OpenGame(2);
        }
        finally { EndPanel(); }
    }

    private string JobGroup(byte id)
    {
        var row = Plugin.Data.GetExcelSheet<ClassJob>().GetRowOrDefault(id);
        if (id is >= 8 and <= 15) return "能工巧匠";
        if (id is >= 16 and <= 18) return "大地使者";
        return row?.Role switch { 1 => "防护职业", 2 => "近战职业", 3 => row?.PrimaryStat == 4 ? "远程魔法职业" : "远程物理职业", 4 => "治疗职业", _ => "其他职业" };
    }

    private void DrawEquipment()
    {
        var visible = BeginPanel("装备耐久与精炼度", ref equipmentOpen, new Vector2(480, 620));
        try
        {
            if (!visible) return;
            foreach (var item in GameBridge.ReadInventory(InventoryType.EquippedItems))
            {
                var row = Plugin.Data.GetExcelSheet<Item>().GetRowOrDefault(item.Id);
                if (row is null) continue;
                GameIcon(row.Value.Icon, 28); ImGui.SameLine();
                ReadableText.TextUnformatted($"{row.Value.Name}   {item.Condition:F0}% / {item.Spiritbond:F0}%");
            }
            ImGui.Separator();
            ReadableText.TextDisabled("百分比：耐久／精炼度。耐久可超过 100%。");
            if (repairMaterialId is null)
                repairMaterialId = Plugin.Data.GetExcelSheet<Item>().FirstOrDefault(i => i.Name.ToString() is "8级暗物质" or "Grade 8 Dark Matter").RowId;
            if (repairMaterialId.Value != 0) ReadableText.TextDisabled($"8级暗物质 × {GameBridge.ItemCount(repairMaterialId.Value)}");
            if (ReadableText.Button("修理")) OpenAction(6);
            ImGui.SameLine(); if (ReadableText.Button("精制魔晶石")) OpenAction(14);
            ImGui.SameLine(); if (ReadableText.Button("镶嵌魔晶石")) OpenAction(12);
            if (ReadableText.Button("镶嵌魔晶石 II")) OpenAction(13);
        }
        finally { EndPanel(); }
    }

    private void DrawCurrency()
    {
        var visible = BeginPanel("货币", ref currencyOpen, new Vector2(430, 620));
        try
        {
            if (!visible) return;
            ReadableText.TextUnformatted($"金币：{gil:N0}");
            if ((DateTime.UtcNow - currencyRead).TotalSeconds >= 1)
            {
                var nativeIds = GameBridge.ReadInventory(InventoryType.Currency).Select(i => i.Id).ToHashSet();
                var tomes = Plugin.Data.GetExcelSheet<TomestonesItem>().Where(t => t.Tomestones.RowId != 0).ToDictionary(t => t.Item.RowId, t => t.Tomestones.RowId);
                currencies.Clear();
                foreach (var item in Plugin.Data.GetExcelSheet<Item>().Where(i => i.ItemUICategory.RowId == 63 && i.RowId != 1))
                {
                    var count = GameBridge.ItemCount(item.RowId);
                    if (count == 0 && !nativeIds.Contains(item.RowId) && !tomes.ContainsKey(item.RowId)) continue;
                    var cap = item.RowId is >= 20 and <= 22 ? GameBridge.CompanySealCap((byte)(item.RowId - 19)) : item.StackSize;
                    currencies.Add(new CurrencySummary(item.RowId, count, cap, tomes.GetValueOrDefault(item.RowId) == 3));
                }
                weeklyTomes = GameBridge.WeeklyTomestones();
                currencyRead = DateTime.UtcNow;
            }
            foreach (var currency in currencies)
            {
                var row = Plugin.Data.GetExcelSheet<Item>().GetRow(currency.Id);
                GameIcon(row.Icon, 26); ImGui.SameLine();
                var weekly = currency.Limited ? $" ({weeklyTomes.Acquired:N0} / {weeklyTomes.Limit:N0} 本周)" : "";
                var cap = currency.Cap == 0 ? "--" : currency.Cap.ToString("N0");
                ReadableText.TextUnformatted($"{row.Name}   {currency.Quantity:N0} / {cap}{weekly}");
            }
            ReadableText.TextWrapped("货币类物品从游戏表读取上限，军票和本周神典石上限读取专用接口。专用钱包仍可查看原版窗口。");
            if (ReadableText.Button("打开完整货币窗口")) OpenGame(66);
        }
        finally { EndPanel(); }
    }

    private sealed record CurrencySummary(uint Id, int Quantity, uint Cap, bool Limited);

    private void DrawCommands()
    {
        var visible = BeginPanel("指令收纳", ref commandsOpen, new Vector2(640, 540));
        try
        {
            if (!visible) return;
            ImGui.InputTextWithHint("##command-filter", "搜索指令／帮助", ref commandSearch, 128);
            var groups = Plugin.Commands.Commands.ToArray().Where(p => p.Key.Contains(commandSearch, StringComparison.OrdinalIgnoreCase) || p.Value.HelpMessage.Contains(commandSearch, StringComparison.OrdinalIgnoreCase))
                .GroupBy(p => p.Value.Handler.Method.DeclaringType?.Assembly.GetName().Name ?? "未知来源").OrderBy(g => g.Key);
            var child = ImGui.BeginChild("command-list", new Vector2(0, -90 * ImGuiHelpers.GlobalScale), false);
            try
            {
                if (child)
                    foreach (var group in groups)
                        if (ImGui.TreeNode(group.Key))
                        {
                            foreach (var entry in group)
                            {
                                if (ReadableText.Selectable(entry.Key, selectedCommand == entry.Key)) selectedCommand = entry.Key;
                                if (ImGui.IsItemHovered()) ImGui.SetTooltip(entry.Value.HelpMessage);
                            }
                            ImGui.TreePop();
                        }
            }
            finally { ImGui.EndChild(); }
            ReadableText.TextUnformatted("已选：" + selectedCommand);
            ImGui.InputText("参数", ref commandArguments, 256);
            if (ReadableText.Button("执行所选指令") && Plugin.Commands.Commands.ContainsKey(selectedCommand))
            {
                try { Plugin.Commands.ProcessCommand(selectedCommand + (commandArguments.Length == 0 ? "" : " " + commandArguments)); }
                catch (Exception ex) { Plugin.Log.Error(ex, "插件指令执行失败"); }
            }
        }
        finally { EndPanel(); }
    }

    private void RefreshTeleports()
    {
        try { teleportRows = GameBridge.ReadTeleports(); }
        catch (Exception ex) { Plugin.Log.Error(ex, "传送列表读取失败"); teleportRows = []; }
    }

    private void DrawTeleports()
    {
        var visible = BeginPanel("传送面板", ref teleportOpen, new Vector2(520, 550));
        try
        {
            if (!visible) return;
            ImGui.InputTextWithHint("##teleport-filter", "搜索水晶／地区", ref teleportSearch, 128);
            ImGui.SameLine(); if (ReadableText.Button("刷新")) RefreshTeleports();
            foreach (var item in teleportRows)
            {
                var row = Plugin.Data.GetExcelSheet<Aetheryte>().GetRowOrDefault(item.Id);
                var name = row?.PlaceName.Value.Name.ToString() ?? $"水晶 {item.Id}";
                if (!name.Contains(teleportSearch, StringComparison.OrdinalIgnoreCase)) continue;
                if (ReadableText.Selectable($"{name}    {item.Cost:N0} 金币###tp{item.Id}:{item.SubIndex}"))
                    Native(() => GameBridge.Teleport(item.Id, item.SubIndex));
            }
            ReadableText.TextWrapped("点击请求游戏传送；费用、传送券与可用性以游戏最终确认结果为准。");
        }
        finally { EndPanel(); }
    }

    private void GameIcon(uint id, float size)
    {
        if (C.TextOnly) { ImGui.Dummy(new Vector2(0, ImGui.GetTextLineHeight())); return; }
        var texture = Plugin.Textures.GetFromGameIcon(new GameIconLookup(id)).GetWrapOrDefault();
        if (texture is null) ImGui.Dummy(new Vector2(size) * ImGuiHelpers.GlobalScale);
        else ImGui.Image(texture.Handle, new Vector2(size) * ImGuiHelpers.GlobalScale);
    }
}
