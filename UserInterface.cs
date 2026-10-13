using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Interface;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Addon.Events.EventDataTypes;
using Dalamud.Game.Config;
using Lumina.Excel.Sheets;

namespace XLinUI;

internal sealed partial class UserInterface(Plugin plugin)
{
    internal bool SettingsOpen;
    internal bool ChatVisible { get; private set; }
    private string search = "";
    private readonly HashSet<string> importedLayouts = [];
    private string draft = "";
    private string notice = "";
    private int sendChannel;
    private bool follow = true;
    private int lastCount;
    private readonly string[] sendNames = ["说话", "小队", "部队", "喊话", "呼喊"];
    private readonly string[] sendCommands = ["/say", "/party", "/fc", "/yell", "/shout"];
    private Configuration C => plugin.Config;
    private bool commandsOpen;
    private bool teleportOpen;
    private string commandSearch = "";
    private string commandArguments = "";
    private string teleportSearch = "";
    private List<TeleportDestination> teleportRows = [];
    private uint gil;
    private byte weather;
    private long serverTime;
    private DateTime lastTelemetry;
    private bool telemetryAvailable;
    private float minimumDurability;
    private float maximumSpiritbond;
    private bool applyChatSize;
    private bool chatSizeChanged;

    private ImGuiWindowFlags WindowFlags => ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoCollapse |
        (C.LockWindows ? ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoResize : ImGuiWindowFlags.None);


    internal void ClearInput()
    {
        draft = search = ""; importedLayouts.Clear();
        bagSlots.Clear(); bagCapacity = 0; bagAvailable = false; lastInventoryRead = default;
        acceptedQuests.Clear(); lastQuestRead = default;
        gearsets.Clear(); currencies.Clear(); currencyRead = default;
        teleportRows.Clear(); telemetryAvailable = false; lastTelemetry = default;
        Ready = HudReady.None;
    }
    internal void Draw(bool loggedIn)
    {
        ChatVisible = false;
        Ready = HudReady.None;
        using var theme = new Theme(C);
        if (C.Enabled && loggedIn && !Plugin.GameGui.GameUiHidden)
        {
            DrawNativeOutlines();
            DrawCombatHud();
            if (C.ShowTop || !C.ShowOriginalUi) DrawTop();
            if (C.ShowStatus) DrawStatus();
            if (C.ShowParty && !C.NativeCombatWidgets) DrawParty();
            if (C.ShowChat) DrawChat();
            // Native hotbars retain all game interactions; the obsolete single-bar mirror is no longer drawn.
            DrawUtilityPanels();
            if (questsOpen) DrawQuests();
            if (inventoryOpen) DrawInventory();
            if (gameWindowsOpen) DrawGameWindows();
            if (commandsOpen) DrawCommands();
            if (teleportOpen) DrawTeleports();
        }
        if (SettingsOpen) DrawSettings();
    }

    private void DrawTop()
    {
        if ((DateTime.UtcNow - lastTelemetry).TotalSeconds >= 1)
        {
            try
            {
                (gil, weather, serverTime) = GameBridge.ReadTopData();
                var equipment = GameBridge.ReadInventory(FFXIVClientStructs.FFXIV.Client.Game.InventoryType.EquippedItems);
                minimumDurability = equipment.Count == 0 ? 0 : equipment.Min(i => i.Condition);
                maximumSpiritbond = equipment.Count == 0 ? 0 : equipment.Max(i => i.Spiritbond);
                telemetryAvailable = true;
            }
            catch (Exception ex) { Plugin.Log.Debug(ex, "顶部状态暂不可用"); }
            lastTelemetry = DateTime.UtcNow;
        }
        var player = Plugin.Objects.LocalPlayer;
        var map = Plugin.Data.GetExcelSheet<Map>().GetRowOrDefault(Plugin.Client.MapId);
        var mapName = map?.PlaceName.Value.Name.ToString();
        if (string.IsNullOrWhiteSpace(mapName)) mapName = "地图";
        var onlineName = player?.OnlineStatus.Value.Name.ToString();
        if (string.IsNullOrWhiteSpace(onlineName)) onlineName = "在线";
        var jobName = player?.ClassJob.Value.Name.ToString();
        if (string.IsNullOrWhiteSpace(jobName)) jobName = "职业";
        var volumeText = Plugin.GameConfig.System.TryGet("SoundMaster", out uint volume) ? $"音量 {volume}%" : "音量";
        var items = new List<TopItem>
        {
            new("特效", () => effectsOpen = !effectsOpen),
            new("部族", () => tribesOpen = !tribesOpen),
            new(onlineName, () => onlineOpen = !onlineOpen, "左键：在线状态；右键：个人信息", OpenPersonalInfo),

            new(telemetryAvailable ? $"{minimumDurability:F0}% / {maximumSpiritbond:F0}%" : "装备", () => equipmentOpen = !equipmentOpen, "最低装备耐久／最高装备精炼度；右键打开修理", () => OpenAction(6)),
            new(jobName, () => { jobsOpen = !jobsOpen; if (jobsOpen) gearsets = GameBridge.ReadGearsets(); }),
            new("雇员", () => OpenGame(5), "打开游戏内任务情报"),
            new(telemetryAvailable ? $"金币 {gil:N0}" : "金币 --", () => currencyOpen = !currencyOpen, "左键：货币；右键：背包", () => OpenGame(10)),
            new("传送面板", () => { teleportOpen = true; RefreshTeleports(); }),
            new(mapName, () => OpenGame(16)),
            new($"插件收纳 {Plugin.Interface.InstalledPlugins.Count(p => p.IsLoaded)}", () => Plugin.Interface.OpenPluginInstallerTo(PluginInstallerOpenKind.InstalledPlugins)),
            new("指令收纳", () => commandsOpen = true),
            new("背包", () => inventoryOpen = true),
            new("任务", () => questsOpen = true),
            new($"区域 {Plugin.Client.TerritoryType} / 地图 {Plugin.Client.MapId} / 天气 {weather}"),
            new($"FPS {ImGui.GetIO().Framerate:F0}"),
        };
        var topKeys = new[] { "effects", "tribes", "online", "equipment", "jobs", "timers", "currency", "teleport", "map", "plugins", "commands", "inventory", "quests", "zone", "fps" };
        for (var i = 0; i < items.Count; i++) items[i] = items[i] with { Key = topKeys[i] };
        foreach (var entry in Plugin.Dtr.Entries.Where(e => e.Shown && !e.UserHidden))
        {
            var current = entry;
            var label = current.Text?.TextValue;
            if (string.IsNullOrWhiteSpace(label)) label = current.Title;
            items.Add(new TopItem(label,
                current.HasClickAction ? () => InvokeDtr(current, MouseClickType.Left) : null,
                current.Tooltip?.TextValue,
                current.HasClickAction ? () => InvokeDtr(current, MouseClickType.Right) : null, "dtr"));
        }
        var et = TimeSpan.FromSeconds(serverTime * (3600.0 / 175));
        items.AddRange([
            new TopItem(volumeText, () => volumeOpen = !volumeOpen, Key: "sound"),
            new TopItem($"LT {DateTime.Now:HH:mm} / ET {et.Hours:00}:{et.Minutes:00}", Key: "clock"),
            new TopItem(C.LockWindows ? "解锁布局" : "锁定布局", () => { C.LockWindows = !C.LockWindows; plugin.Save(); }),
            new TopItem("游戏功能", () => gameWindowsOpen = !gameWindowsOpen, Key: "gamewindows"),
            new TopItem("设置", () => SettingsOpen = true),
            new TopItem(C.ShowOriginalUi ? "隐藏原版 UI" : "恢复原版 UI", () => { C.ShowOriginalUi = !C.ShowOriginalUi; plugin.Save(); }),
        ]);
        items.RemoveAll(item => item.Key is not null && !C.TopSections.GetValueOrDefault(item.Key, true));
        var scale = ImGuiHelpers.GlobalScale;
        var viewport = ImGuiHelpers.MainViewport;
        var available = Math.Max(100, viewport.Size.X - 24 * scale);
        var rows = new List<List<TopItem>> { new() };
        var widths = new List<float> { 0 };
        foreach (var item in items)
        {
            var width = ImGui.CalcTextSize(item.Label).X + 16 * scale;
            if (widths[^1] + width > available && rows[^1].Count > 0) { rows.Add([]); widths.Add(0); }
            rows[^1].Add(item); widths[^1] += width;
        }
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 5) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, 2) * scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 4) * scale);
        ImGui.SetNextWindowPos(viewport.Pos);
        ImGui.SetNextWindowSize(new Vector2(viewport.Size.X, rows.Count * (ImGui.GetFontSize() + 8 * scale) + 10 * scale));
        var visible = ImGui.Begin("##XLinUITop", ImGuiWindowFlags.NoDecoration | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings);
        try
        {
            if (!visible) return;
            Ready |= HudReady.Top;
            var index = 0;
            for (var r = 0; r < rows.Count; r++)
            {
                ImGui.SetCursorPosX(Math.Max(8 * scale, (viewport.Size.X - widths[r]) / 2));
                for (var itemIndex = 0; itemIndex < rows[r].Count; itemIndex++)
                {
                    var item = rows[r][itemIndex];
                    ImGui.PushID(index++);
                    if (item.Click is not null)
                    {
                        if (ImGui.SmallButton(item.Label)) item.Click();
                    }
                    else { ImGui.AlignTextToFramePadding(); ImGui.TextUnformatted(item.Label); }
                    if (ImGui.IsItemHovered())
                    {
                        if (!string.IsNullOrEmpty(item.Tooltip)) ImGui.SetTooltip(item.Tooltip);
                        if (item.RightClick is not null && ImGui.IsMouseClicked(ImGuiMouseButton.Right)) item.RightClick();
                    }
                    ImGui.PopID();
                    if (itemIndex < rows[r].Count - 1) ImGui.SameLine();
                }
            }
        }
        finally { ImGui.End(); ImGui.PopStyleVar(3); }
    }

    private void OpenGame(uint command, bool forceNative = false)
    {
        if (!forceNative && command == 4 && C.ModernQuests) { questsOpen = true; return; }
        if (!forceNative && command == 10 && C.ModernInventory) { inventoryOpen = true; return; }
        C.ShowOriginalUi = true; plugin.Save();
        Native(() => GameBridge.Open(command));
    }

    private static void InvokeDtr(IReadOnlyDtrBarEntry entry, MouseClickType click)
    {
        try
        {
            var io = ImGui.GetIO();
            entry.OnClick?.Invoke(new DtrInteractionEvent
            {
                ClickType = click, Position = ImGui.GetMousePos(),
                ModifierKeys = (io.KeyCtrl ? ClickModifierKeys.Ctrl : 0) | (io.KeyAlt ? ClickModifierKeys.Alt : 0) | (io.KeyShift ? ClickModifierKeys.Shift : 0),
            });
        }
        catch (Exception ex) { Plugin.Log.Error(ex, "DTR 插件入口调用失败：{Title}", entry.Title); }
    }

    private sealed record TopItem(string Label, System.Action? Click = null, string? Tooltip = null, System.Action? RightClick = null, string? Key = null);

    private void ImportLayout(string key, params string[] names) => ImportLayout(key, null, names);
    private void ImportLayout(string key, Vector2? sizeOverride, params string[] names)
    {
        if (!C.FollowGameLayout || importedLayouts.Contains(key)) return;
        foreach (var name in names)
            if (plugin.GameUi.Layouts.TryGetValue(name, out var layout))
            {
                var viewport = ImGuiHelpers.MainViewport;
                var size = sizeOverride ?? (key == "chat" ? new Vector2(C.ChatWidth, C.ChatHeight) * ImGuiHelpers.GlobalScale : new Vector2(330, 180) * ImGuiHelpers.GlobalScale);
                var position = new Vector2(Math.Clamp(layout.X, 0, Math.Max(0, viewport.Size.X - size.X)), Math.Clamp(layout.Y, 0, Math.Max(0, viewport.Size.Y - size.Y)));
                ImGui.SetNextWindowPos(viewport.Pos + position);
                importedLayouts.Add(key);
                break;
            }
    }

    private void DrawStatus()
    {
        ImGui.SetNextWindowSize(new Vector2(330, 115) * ImGuiHelpers.GlobalScale, ImGuiCond.FirstUseEver);
        ImportLayout("status", "_Parameter", "Parameter");
        var visible = ImGui.Begin("状态###XLinUIStatus", WindowFlags);
        try
        {
            if (!visible || Plugin.Objects.LocalPlayer is not { } player) return;
            Ready |= HudReady.Resources;
            ImGui.TextColored(new Vector4(C.Tint, 1), player.Name.TextValue);
            ImGui.SameLine(); ImGui.TextDisabled($"Lv.{player.Level}");
            ThinResource("HP", player.CurrentHp, player.MaxHp, C.Tint);
            if (player.ClassJob.RowId is >= 16 and <= 18) Resource("GP", player.CurrentGp, player.MaxGp);
            else if (player.ClassJob.RowId is >= 8 and <= 15) Resource("CP", player.CurrentCp, player.MaxCp);
            else ThinResource("MP", player.CurrentMp, player.MaxMp, new Vector3(.65f, .72f, .9f));
            if (!C.NativeCombatWidgets && Plugin.Targets.Target is ICharacter target)
            {
                ImGui.Separator();
                ImGui.TextUnformatted(target.Name.TextValue);
                Resource("目标 HP", target.CurrentHp, target.MaxHp);
            }
        }
        finally { EndPanel(); }
    }

    private void DrawParty()
    {
        if (Plugin.Party.Length == 0) return;
        ImGui.SetNextWindowSize(new Vector2(270, 300) * ImGuiHelpers.GlobalScale, ImGuiCond.FirstUseEver);
        ImportLayout("party", "_PartyList", "PartyList");
        var visible = ImGui.Begin("队伍###XLinUIParty", WindowFlags);
        try
        {
            if (!visible) return;
            foreach (var member in Plugin.Party)
            {
                ImGui.TextUnformatted(member.Name.TextValue);
                Resource("HP", member.CurrentHP, member.MaxHP);
                ImGui.Spacing();
            }
        }
        finally { EndPanel(); }
    }

    private void Resource(string label, uint current, uint maximum)
    {
        if (!C.TextOnly) ImGui.ProgressBar(maximum == 0 ? 0 : Math.Clamp((float)current / maximum, 0, 1), new Vector2(-1, 12 * ImGuiHelpers.GlobalScale), "");
        ImGui.TextDisabled($"{label}   {current:N0} / {maximum:N0}");
    }

    private void DrawChat()
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.SetNextWindowSize(new Vector2(C.ChatWidth, C.ChatHeight) * scale, applyChatSize ? ImGuiCond.Always : ImGuiCond.FirstUseEver);
        applyChatSize = false;
        ImGui.SetNextWindowSizeConstraints(new Vector2(380, Math.Max(240, 220 * C.ChatFontScale)) * scale, new Vector2(float.MaxValue));
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(30, 150), ImGuiCond.FirstUseEver);
        ImportLayout("chat", "ChatLog");
        var open = C.ShowChat;
        var visible = ImGui.Begin("XLinUI · 聊天###XLinUIChat", ref open, WindowFlags | ImGuiWindowFlags.NoScrollbar);
        try
        {
            if (!visible || !open) return;
            ChatVisible = true;
            ImGui.SetWindowFontScale(C.ChatFontScale);
            var chatSize = ImGui.GetWindowSize() / scale;
            if (!C.LockWindows && (Math.Abs(C.ChatWidth - chatSize.X) > 1 || Math.Abs(C.ChatHeight - chatSize.Y) > 1))
            { C.ChatWidth = chatSize.X; C.ChatHeight = chatSize.Y; chatSizeChanged = true; }
            if (chatSizeChanged && !ImGui.IsMouseDown(ImGuiMouseButton.Left)) { chatSizeChanged = false; plugin.Save(); }
            if (C.Channels.All(c => c.Id != plugin.ActiveChannel)) plugin.ActiveChannel = C.Channels[0].Id;
            var channelNow = C.Channels.First(c => c.Id == plugin.ActiveChannel);
            var sidebar = ImGui.BeginChild("categories", new Vector2(Math.Min(C.ChatSidebarWidth * scale, ImGui.GetContentRegionAvail().X * .35f), 0), !C.HideBorders && !C.TextOnly);
            try
            {
                if (sidebar)
                {
                    ImGui.TextColored(new Vector4(C.Tint, 1), "会话");
                    ImGui.TextDisabled("自定义频道分类");
                    ImGui.Separator();
                    foreach (var channel in C.Channels)
                    {
                        var unread = plugin.Unread.GetValueOrDefault(channel.Id);
                        var label = channel.Name + (unread > 0 ? $"  {unread}" : "") + "###" + channel.Id;
                        if (ImGui.Selectable(label, plugin.ActiveChannel == channel.Id, ImGuiSelectableFlags.None, new Vector2(0, 32 * scale)))
                        {
                            plugin.ActiveChannel = channel.Id;
                            plugin.Unread[channel.Id] = 0;
                            lastCount = -1;
                            channelNow = channel;
                        }
                    }
                    ImGui.Spacing();
                    if (ImGui.Button("编辑分类", new Vector2(-1, 0))) SettingsOpen = true;
                    ImGui.Spacing();
                    var hide = C.HideOriginalChat;
                    if (ImGui.Checkbox("隐藏原版聊天", ref hide)) { C.HideOriginalChat = hide; plugin.Save(); }
                    ImGui.TextWrapped(C.ShowOriginalUi ? "关闭或折叠此窗口会恢复原版聊天。" : "全局隐藏已开启；顶部按钮可恢复原版 UI。");
                }
            }
            finally { ImGui.EndChild(); }
            ImGui.SameLine();
            var conversation = ImGui.BeginChild("conversation", Vector2.Zero, false);
            try
            {
                if (!conversation) { ChatVisible = false; return; }
                ImGui.TextColored(new Vector4(C.Tint, 1), channelNow.Name);
                ImGui.SameLine(); ImGui.TextDisabled(!C.ShowOriginalUi || C.HideOriginalChat ? "· 原版聊天已隐藏" : "· 原版聊天保留");
                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint("##history-search", "搜索发言人或消息", ref search, 128);
                var height = Math.Max(80 * scale, ImGui.GetContentRegionAvail().Y - 110 * scale * C.ChatFontScale);
                var content = ImGui.BeginChild("messages", new Vector2(0, height), !C.HideBorders && !C.TextOnly);
                try
                {
                    if (content)
                    {
                        var wasBottom = ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 8;
                        var rows = plugin.History.Where(m => (channelNow.Types.Count == 0 || channelNow.Types.Contains(m.Type)) &&
                            (search.Length == 0 || m.Sender.Contains(search, StringComparison.OrdinalIgnoreCase) || m.Text.Contains(search, StringComparison.OrdinalIgnoreCase))).ToArray();
                        if (rows.Length == 0) ImGui.TextDisabled(search.Length == 0 ? "等待新消息…" : "没有匹配的消息。");
                        ChatLine? previous = null;
                        foreach (var line in rows)
                        {
                            var group = previous is not null && previous.Sender == line.Sender && previous.Type == line.Type && (line.Time - previous.Time).TotalMinutes < 2;
                            if (!group)
                            {
                                if (previous is not null) { ImGui.Spacing(); if (!C.Compact) ImGui.Separator(); }
                                ImGui.TextColored(new Vector4(C.Tint, 1), string.IsNullOrWhiteSpace(line.Sender) ? "系统" : line.Sender);
                                ImGui.SameLine(); ImGui.TextDisabled($"{ChannelName(line.Type)} · {line.Time:HH:mm}");
                            }
                            ImGui.TextWrapped(line.Text);
                            if (!C.Compact) ImGui.Spacing();
                            previous = line;
                        }
                        if (follow && (wasBottom || lastCount == -1) && lastCount != plugin.HistoryRevision) ImGui.SetScrollHereY(1);
                        lastCount = plugin.HistoryRevision;
                        plugin.Unread[channelNow.Id] = 0;
                    }
                }
                finally { ImGui.EndChild(); }
                ImGui.SetNextItemWidth(95 * scale);
                ImGui.Combo("##send-channel", ref sendChannel, sendNames, sendNames.Length);
                ImGui.SameLine(); ImGui.Checkbox("跟随消息", ref follow);
                ImGui.SetNextItemWidth(Math.Max(80, ImGui.GetContentRegionAvail().X - 70 * scale));
                var enter = ImGui.InputTextWithHint("##message", "输入消息，Enter 发送", ref draft, 500, ImGuiInputTextFlags.EnterReturnsTrue);
                ImGui.SameLine();
                if (ImGui.Button("发送") || enter) Send();
                ImGui.TextDisabled("链接操作仍需使用原版聊天；可随时取消隐藏。");
                if (notice.Length > 0) ImGui.TextWrapped(notice);
            }
            finally { ImGui.EndChild(); }
        }
        finally
        {
            EndPanel();
            if (C.ShowChat != open) { C.ShowChat = open; plugin.Save(); ChatVisible = false; }
        }
    }

    private static string ChannelName(ushort type) => type switch
    {
        10 => "说话", 11 => "呼喊", 12 => "私信发出", 13 => "私信", 14 => "小队", 15 => "团队",
        24 => "部队", 30 => "喊话", 56 => "回声", 57 => "系统", 60 => "错误",
        _ => ((XivChatType)type).ToString(),
    };

    private void Send()
    {
        if (!C.NativeInteractions)
        { notice = "请先在设置中启用游戏交互。原版聊天可正常使用。"; return; }
        var text = draft.Trim();
        if (text.Length == 0) return;
        var submitted = false;
        Native(() => submitted = GameBridge.Send(sendCommands[sendChannel], text));
        if (submitted)
        {
            draft = ""; notice = "已提交给游戏；发送结果及权限提示以原版聊天为准。";
        }
        else notice = "未提交：消息须为单行且不超过 450 个 UTF-8 字节，或游戏 UI 尚未就绪。";
    }

    private void DrawNativeOutlines()
    {
        if (C.TextOnly) return;
        var draw = ImGui.GetBackgroundDrawList();
        var color = ImGui.ColorConvertFloat4ToU32(new Vector4(C.Tint, .7f));
        foreach (var item in plugin.NativeStyle.Outlines)
        {
            if (item.Name != "_NaviMap" && C.HideBorders && !HudGeometry.IsStandardHotbar(item.Name)) continue;
            var pos = ImGuiHelpers.MainViewport.Pos + item.Position;
            draw.AddRect(pos, pos + item.Size, color, item.Radius, ImDrawFlags.None, 1);
        }
    }

    private void DrawSettings()
    {
        ImGui.SetNextWindowSize(new Vector2(600, 650) * ImGuiHelpers.GlobalScale, ImGuiCond.FirstUseEver);
        var visible = ImGui.Begin("XLinUI · 设置###XLinUISettings", ref SettingsOpen, WindowFlags);
        var changed = false;
        try
        {
            if (!visible) return;
            if (ImGui.Button("关闭设置")) SettingsOpen = false;
            ImGui.SameLine();
            if (ImGui.Button("应用现代风格预设"))
            {
                C.ApplyModernPreset();
                importedLayouts.Clear(); changed = true;
            }

            ImGui.TextWrapped("/xlinui restore 恢复原版界面。/xlinui off 关闭新面板并恢复原版。技能 UI 固定直角。");
            changed |= Toggle("启用 XLinUI", C.Enabled, v => C.Enabled = v);
            changed |= Toggle("显示原游戏 UI（关闭为实验性全隐藏）", C.ShowOriginalUi, v => C.ShowOriginalUi = v);
            ImGui.TextWrapped("已提供背包和任务概览；市场、任务对话等复杂操作仍使用原版窗口。隐藏全部原版 UI 后，可用顶部恢复按钮或 /xlinui restore 返回；打开快捷入口也会先恢复原版。");
            changed |= Toggle("游戏原版热键栏与状态组件", C.NativeCombatWidgets, v => C.NativeCombatWidgets = v);
            ImGui.TextWrapped("沿用原版所有热键栏、Buff/Debuff、职业量谱、目标和队伍布局与交互，不重画原版图标。HP/MP 使用简洁条。");
            changed |= Toggle("简洁生命/魔力状态条", C.ShowStatus, v => C.ShowStatus = v);
            changed |= Toggle("简洁目标面板（替换原版）", C.ModernTarget, v => C.ModernTarget = v);
            changed |= Toggle("简洁焦点目标（替换原版）", C.ModernFocus, v => C.ModernFocus = v);
            changed |= Toggle("简洁自身读条（替换原版）", C.ModernCast, v => C.ModernCast = v);
            changed |= Toggle("简洁任务概览入口", C.ModernQuests, v => C.ModernQuests = v);
            changed |= Toggle("简洁背包入口（保留原版完整操作）", C.ModernInventory, v => C.ModernInventory = v);
            changed |= Toggle("简洁增益／减益（替换原版）", C.ModernStatuses, v => C.ModernStatuses = v);
            changed |= Toggle("简洁队伍概览（保留原版队伍菜单）", C.ModernParty, v => C.ModernParty = v);
            var iconSize = C.StatusIconSize;
            if (ImGui.SliderFloat("状态图标大小", ref iconSize, 20, 48, "%.0f")) { C.StatusIconSize = iconSize; changed = true; }
            ImGui.TextWrapped("目标、焦点、读条与状态只在替代面板成功绘制后隐藏原版。队伍概览暂保留原版名单的上下文菜单、排序与队长操作。右键状态需开启游戏交互，由游戏判定是否可取消。");

            if (!C.NativeCombatWidgets) changed |= Toggle("备用队伍面板", C.ShowParty, v => C.ShowParty = v);
            changed |= Toggle("采用读取到的原版布局", C.FollowGameLayout, v => C.FollowGameLayout = v);
            if (ImGui.Button("重新应用已读取布局")) importedLayouts.Clear();
            ImGui.TextDisabled($"已读取 {plugin.GameUi.Layouts.Count} 个已加载窗口，游戏 UI 比例 {plugin.GameUi.GameScale:F2}");
            if (ImGui.TreeNode("已读取的原版窗口设置"))
            {
                foreach (var item in plugin.GameUi.Layouts.OrderBy(i => i.Key))
                    ImGui.TextUnformatted($"{item.Key}: ({item.Value.X:F0}, {item.Value.Y:F0}) · 比例 {item.Value.Scale:F2} · 原先{(item.Value.Visible ? "显示" : "隐藏")}");
                ImGui.TreePop();
            }
            changed |= Toggle("居中顶部状态栏", C.ShowTop, v => C.ShowTop = v);
            changed |= Toggle("锁定窗口位置与尺寸", C.LockWindows, v => C.LockWindows = v);
            changed |= Toggle("隐藏边框", C.HideBorders, v => C.HideBorders = v);
            changed |= Toggle("纯文字模式（透明背景、隐藏边框）", C.TextOnly, v => C.TextOnly = v);
            var cw = C.ChatWidth; var ch = C.ChatHeight;
            if (ImGui.SliderFloat("聊天宽度", ref cw, 380, 1800, "%.0f")) { C.ChatWidth = cw; applyChatSize = true; changed = true; }
            if (ImGui.SliderFloat("聊天高度", ref ch, 240, 1200, "%.0f")) { C.ChatHeight = ch; applyChatSize = true; changed = true; }
            var sw = C.ChatSidebarWidth; var fs = C.ChatFontScale;
            if (ImGui.SliderFloat("频道栏宽度", ref sw, 80, 250, "%.0f")) { C.ChatSidebarWidth = sw; changed = true; }
            if (ImGui.SliderFloat("聊天字号比例", ref fs, .7f, 1.6f, "%.2f")) { C.ChatFontScale = fs; changed = true; }
            changed |= Toggle("分类聊天", C.ShowChat, v => C.ShowChat = v);
            changed |= Toggle("显示插件聊天时隐藏原版聊天", C.HideOriginalChat, v => C.HideOriginalChat = v);
            ImGui.TextDisabled("折叠、关闭插件聊天或停用插件时恢复原版聊天。");
            changed |= Toggle("所有已显示热键栏采用直角边框", C.SquareHotbars, v => C.SquareHotbars = v);
            changed |= Toggle("小地图方形外框", C.SquareMinimap, v => C.SquareMinimap = v);
            ImGui.TextWrapped("自动追踪 1–10 栏的原生槽位坐标与大小，无须选择单栏；保留原生冷却、拖放、快捷键与提示。小地图仍使用原生圆形内容，方框不改变地图裁切。");
            changed |= Toggle("原生标准窗口统一色调与透明度（实验）", C.StyleNativeWindows, v => C.StyleNativeWindows = v);
            ImGui.TextDisabled($"已显示热键栏 {plugin.NativeStyle.ActiveBars.Count} 个，已换肤窗口 {plugin.NativeStyle.StyledWindows.Count} 个");
            if (ImGui.TreeNode("原生窗口适配审查／逐窗排除"))
            {
                foreach (var entry in plugin.GameUi.Layouts.OrderBy(i => i.Key))
                {
                    var excluded = C.ExcludedNativeWindows.Contains(entry.Key);
                    if (ImGui.Checkbox($"保留原版外观##{entry.Key}", ref excluded))
                    { if (excluded) C.ExcludedNativeWindows.Add(entry.Key); else C.ExcludedNativeWindows.Remove(entry.Key); changed = true; }
                    ImGui.SameLine(); ImGui.TextUnformatted(entry.Key + (plugin.NativeStyle.StyledWindows.Contains(entry.Key) ? " · 标准窗口已适配" : HudGeometry.IsStandardHotbar(entry.Key) ? " · 原生热键栏" : " · 原生内容保留"));
                }
                ImGui.TreePop();
            }
            changed |= Toggle("其他 UI 使用圆角", C.Rounded, v => C.Rounded = v);
            changed |= Toggle("显示面板背景", C.Glass, v => C.Glass = v);
            ImGui.TextDisabled("本版无背景模糊；玻璃开关影响背景透明度。");
            var opacity = C.Opacity;
            if (ImGui.SliderFloat("背景不透明度", ref opacity, 0, 1, "%.2f")) { C.Opacity = opacity; changed = true; }
            var tint = C.Tint;
            if (ImGui.ColorEdit3("自定义色调", ref tint)) { C.Tint = tint; changed = true; }
            changed |= Toggle("紧凑聊天间距", C.Compact, v => C.Compact = v);
            ImGui.Separator();
            ImGui.TextWrapped("游戏交互包括打开游戏窗口、发送聊天及读取/点击热键栏。国服 soil 接口须在本机验证；推荐用实际安装目录重新编译。");
            changed |= Toggle("启用游戏交互", C.NativeInteractions, v => C.NativeInteractions = v);

            if (ImGui.CollapsingHeader("顶部入口"))
                foreach (var entry in Configuration.TopLabels)
                    changed |= Toggle(entry.Value, C.TopSections.GetValueOrDefault(entry.Key, true), v => C.TopSections[entry.Key] = v);
            if (ImGui.CollapsingHeader("聊天分类"))
            {
                ImGui.TextWrapped("每个分类可选择多个游戏频道。不选频道表示接收全部；删除分类不删除消息。");
                Channel? remove = null;
                foreach (var channel in C.Channels)
                {
                    ImGui.PushID(channel.Id);
                    try
                    {
                        var name = channel.Name;
                        if (ImGui.InputText("分类名称", ref name, 32)) { channel.Name = name; changed = true; }
                        if (C.Channels.Count > 1)
                        {
                            ImGui.SameLine(); if (ImGui.Button("删除")) remove = channel;
                        }
                        if (ImGui.TreeNode("关联游戏频道"))
                        {
                            foreach (var type in Enum.GetValues<XivChatType>().Distinct())
                            {
                                var id = (ushort)type;
                                var selected = channel.Types.Contains(id);
                                if (ImGui.Checkbox($"{type} ({id})", ref selected))
                                {
                                    if (selected) channel.Types.Add(id); else channel.Types.Remove(id);
                                    changed = true;
                                }
                            }
                            ImGui.TreePop();
                        }
                    }
                    finally { ImGui.PopID(); }
                }
                if (remove is not null)
                { C.Channels.Remove(remove); plugin.Unread.Remove(remove.Id); changed = true; }
                if (ImGui.Button("添加分类")) { C.Channels.Add(new Channel("新分类", [])); changed = true; }
            }
            ImGui.Separator();
            ImGui.TextWrapped("聊天目前以纯文本显示，物品/地图/玩家链接仍需在原版聊天点击；支持普通频道发送，暂不支持指定私信收件人。聊天最多保留 1000 条，不写入磁盘，退出角色后清空。");
            if (ImGui.Button("恢复默认设置")) { plugin.Reset(); changed = false; }
        }
        finally
        {
            EndPanel();
            if (changed) plugin.Save();
        }
    }

    private static bool Toggle(string name, bool value, Action<bool> setter)
    {
        if (!ImGui.Checkbox(name, ref value)) return false;
        setter(value); return true;
    }

    private void Native(System.Action action)
    {
        if (!C.NativeInteractions) { SettingsOpen = true; return; }
        if (Plugin.Objects.LocalPlayer is null) return;
        try { action(); }
        catch (Exception ex)
        {
            C.NativeInteractions = false; plugin.Save();
            notice = "游戏接口调用失败，已关闭游戏交互。请查看 /xllog。";
            Plugin.Log.Error(ex, "XLinUI 游戏接口调用失败");
        }
    }

    private sealed class Theme : IDisposable
    {
        internal Theme(Configuration c)
        {
            ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, c.Rounded ? 10 : 0);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, c.Rounded ? 8 : 0);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, c.Rounded ? 6 : 0);
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(16, 14));
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 6));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(10, 8));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, c.HideBorders || c.TextOnly ? 0 : 1);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, c.HideBorders || c.TextOnly ? 0 : 1);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, c.HideBorders || c.TextOnly ? 0 : 1);
            var tint = c.Tint;
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(new Vector3(.035f, .045f, .065f) + tint * .04f, c.TextOnly || !c.Glass ? 0 : c.Opacity));
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(tint * .24f, c.TextOnly ? 0 : .85f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(tint * .45f, 1));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(tint * .6f, 1));
            ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(tint * .35f, .8f));
            ImGui.PushStyleColor(ImGuiCol.CheckMark, new Vector4(tint, 1));
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(.24f, .29f, .35f, .65f));
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(.91f, .94f, .98f, 1));
            ImGui.PushStyleColor(ImGuiCol.TextDisabled, new Vector4(.59f, .65f, .73f, 1));
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(.08f, .10f, .14f, c.TextOnly ? 0 : .95f));
            ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(.055f, .07f, .10f, .95f));
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(.08f, .10f, .14f, c.TextOnly ? 0 : .95f));
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(tint, .9f));
            ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(.24f, .29f, .35f, c.TextOnly ? 0 : .6f));
        }
        public void Dispose() { ImGui.PopStyleColor(15); ImGui.PopStyleVar(9); }
    }
}
