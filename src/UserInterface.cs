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
    internal bool PerformanceOpen;
    private string performanceReport="";
    private DateTime nextPerformanceReport;
    private AppearanceAssets? appearance;
    private AppearanceAssets Appearance => appearance ??= new AppearanceAssets(C);
    internal void DisposeAssets(){appearance?.Dispose();appearance=null;}
    internal bool ChatVisible { get; private set; }
    private string search = "";
    private readonly HashSet<string> importedLayouts = [];
    private string draft = "";
    private string notice = "";

    private int lastCount;

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
        draft = search = tellRecipient = ""; importedLayouts.Clear();
        CloseHotbarEditing();
        minimapId=0;minimapMarkers.Clear();
        channelDrafts.Clear();filteredChat.Clear();renderedChatText.Clear();chatRowHeights.Clear();chatCacheRevision=-1;ChatAtBottom=false;chatLabels=null;chatSubmitted=default;noviceJoinSubmitted=default;notice="";expandedChatRows=0;
        bagSlots.Clear(); bagCapacity = 0; bagAvailable = false; lastInventoryRead = default;
        acceptedQuests.Clear(); lastQuestRead = default;
        gearsets.Clear(); currencies.Clear(); currencyRead = default;
        teleportRows.Clear(); telemetryAvailable = false; lastTelemetry = default;
        Ready = HudReady.None;
    }
    internal void Draw(bool loggedIn)
    {
        if(hotbarEditing&&(!C.Enabled||!loggedIn||Plugin.GameGui.GameUiHidden))CloseHotbarEditing();
        ChatVisible = false;
        Ready = HudReady.None;
        using(Plugin.Metrics.Measure(UiWork.Appearance))Appearance.Update(C.Enabled && loggedIn && !Plugin.GameGui.GameUiHidden && (C.ShowChat || C.ShowTop || C.ShowStatus || C.ModernTarget || C.ModernCast || C.ModernStatuses));
        using var customFont=Appearance.PushFont();
        using var theme = new Theme(C);
        if (C.Enabled && loggedIn && !Plugin.GameGui.GameUiHidden)
        {
            DrawNativeOutlines();

            using(Plugin.Metrics.Measure(UiWork.Combat))DrawCombatHud();
            using(Plugin.Metrics.Measure(UiWork.Map))DrawMinimap();
            DrawHotbarEditor();
            if (C.ShowTop || !C.ShowOriginalUi)using(Plugin.Metrics.Measure(UiWork.Top))DrawTop();
            if (C.ShowStatus) DrawStatus();
            if (C.ShowParty && !C.NativeCombatWidgets) DrawParty();
            if (C.ShowChat)using(Plugin.Metrics.Measure(UiWork.Chat))DrawChat();
            // Native hotbars retain all game interactions; the obsolete single-bar mirror is no longer drawn.
            DrawUtilityPanels();
            if (questsOpen) DrawQuests();
            if (inventoryOpen) DrawInventory();
            if (gameWindowsOpen) DrawGameWindows();
            if (commandsOpen) DrawCommands();
            if (teleportOpen) DrawTeleports();
        }
        if (SettingsOpen) DrawSettings();
        Appearance.DrawDialog();
        if(PerformanceOpen)
        {
            var visible=ImGui.Begin("耗时诊断###XLinUIPerformance",ref PerformanceOpen);
            try{if(visible){if(DateTime.UtcNow>=nextPerformanceReport){performanceReport=Plugin.Metrics.Report()+$"\n图标缓存 {plugin.Flat.Count} · 待生成 {plugin.Flat.PendingCount} · 菜单图集 {plugin.Buttons.Count} · 已替换纹理 {plugin.Redraw.Count}\n{plugin.Redraw.Failure}";nextPerformanceReport=DateTime.UtcNow.AddSeconds(.5);}ImGui.TextUnformatted(performanceReport);if(ImGui.Button("复制诊断"))ImGui.SetClipboardText(performanceReport);}}
            finally{ImGui.End();}
        }
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
        ImGui.SetNextWindowSize(new Vector2(330, 100) * ImGuiHelpers.GlobalScale, ImGuiCond.FirstUseEver);
        ImportLayout("status", "_Parameter", "Parameter");
        var visible = ImGui.Begin("状态###XLinUIStatus", WindowFlags);
        if(visible)Appearance.DrawBackground();
        try
        {
            if (!visible || Plugin.Objects.LocalPlayer is not { } player) return;
            ImGui.SetWindowFontScale(C.HudFontScale);
            Ready |= HudReady.Resources;
            ReadableText.TextColored(new Vector4(C.Tint, 1), player.Name.TextValue);
            ImGui.SameLine(); ReadableText.TextDisabled($"Lv.{player.Level}");
            ThinResource("HP", player.CurrentHp, player.MaxHp, C.Tint);
            if (player.ClassJob.RowId is >= 16 and <= 18) Resource("GP", player.CurrentGp, player.MaxGp);
            else if (player.ClassJob.RowId is >= 8 and <= 15) Resource("CP", player.CurrentCp, player.MaxCp);
            else ThinResource("MP", player.CurrentMp, player.MaxMp, new Vector3(.65f, .72f, .9f));
            if (!C.NativeCombatWidgets && Plugin.Targets.Target is ICharacter target)
            {
                ImGui.Separator();
                ReadableText.TextUnformatted(target.Name.TextValue);
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
        if(visible)Appearance.DrawBackground();
        try
        {
            if (!visible) return;
            foreach (var member in Plugin.Party)
            {
                ReadableText.TextUnformatted(member.Name.TextValue);
                Resource("HP", member.CurrentHP, member.MaxHP);
                ImGui.Spacing();
            }
        }
        finally { EndPanel(); }
    }

    private void Resource(string label, uint current, uint maximum)
    {
        if (!C.TextOnly) ImGui.ProgressBar(maximum == 0 ? 0 : Math.Clamp((float)current / maximum, 0, 1), new Vector2(-1, 12 * ImGuiHelpers.GlobalScale), "");
        ReadableText.TextDisabled($"{label}   {current:N0} / {maximum:N0}");
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
        if(visible)Appearance.DrawBackground();
        var changed = false;
        try
        {
            if (!visible) return;
            if (ReadableText.Button("关闭设置")) SettingsOpen = false;
            ImGui.SameLine();
            if (ReadableText.Button("应用现代风格预设"))
            {
                C.ApplyModernPreset();
                importedLayouts.Clear(); changed = true;
            }

            if (ImGui.BeginTabBar("XLinUISettingsTabs"))
            {
                try
                {
                if (ImGui.BeginTabItem("外观"))
                {
                    try
                    {
                    changed |= Toggle("锁定窗口位置与尺寸", C.LockWindows, v => C.LockWindows = v);
                    changed |= Toggle("隐藏边框", C.HideBorders, v => C.HideBorders = v);
                    changed |= Toggle("纯文字模式（透明背景、隐藏边框）", C.TextOnly, v => C.TextOnly = v);
                    changed |= Toggle("其他 UI 使用圆角", C.Rounded, v => C.Rounded = v);
                    changed |= Toggle("插件窗口强制无背景（保留图标与进度条）", C.ForceTransparentPanels, v => C.ForceTransparentPanels = v);
                    changed |= Toggle("原版已适配窗口强制无背景", C.ForceTransparentNative, v => C.ForceTransparentNative = v);
                    changed |= Toggle("文字细描边", C.TextOutline, v => C.TextOutline = v);
                    changed |= Toggle("原版窗口文字描边", C.NativeTextOutline, v => C.NativeTextOutline = v);
                    changed |= Toggle("提高次要文字对比度", C.HighContrastText, v => C.HighContrastText = v);
                    changed |= Toggle("无状态时仍显示占位面板", C.ShowEmptyStatuses, v => C.ShowEmptyStatuses = v);
                    var hudFont = C.HudFontScale;
                    if (ImGui.SliderFloat("战斗 HUD 字号比例", ref hudFont, .8f, 1.6f, "%.2f")) { C.HudFontScale = hudFont; changed = true; }
                    changed |= Toggle("显示面板背景", C.Glass, v => C.Glass = v);
                    ReadableText.TextDisabled("强制无背景时不透明度滑块不生效；进度条和图标仍保留。未启用强制无背景时可调背景透明度。");
                    var opacity = C.Opacity;
                    if (ImGui.SliderFloat("背景不透明度", ref opacity, 0, 1, "%.2f")) { C.Opacity = opacity; changed = true; }
                    var tint = C.Tint;
                    if (ImGui.ColorEdit3("自定义色调", ref tint)) { C.Tint = tint; changed = true; }
                    }
                    finally { ImGui.EndTabItem(); }
                }
                if (ImGui.BeginTabItem("素材与字体"))
                {
                    try { DrawAppearanceSettings(ref changed); }
                    finally { ImGui.EndTabItem(); }
                }
                if (ImGui.BeginTabItem("模块"))
                {
                    try
                    {
                    changed |= Toggle("启用 XLinUI", C.Enabled, v => C.Enabled = v);
                    changed |= Toggle("显示原游戏 UI（关闭为实验性全隐藏）", C.ShowOriginalUi, v => C.ShowOriginalUi = v);
                    changed |= Toggle("游戏原版热键栏与状态组件", C.NativeCombatWidgets, v => C.NativeCombatWidgets = v);
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

                    if (!C.NativeCombatWidgets) changed |= Toggle("备用队伍面板", C.ShowParty, v => C.ShowParty = v);
                    changed |= Toggle("顶部快捷与状态栏", C.ShowTop, v => C.ShowTop = v);
                    changed |= Toggle("启用游戏交互", C.NativeInteractions, v => C.NativeInteractions = v);
                    }
                    finally { ImGui.EndTabItem(); }
                }
                if (ImGui.BeginTabItem("聊天"))
                {
                    try
                    {
                        DrawChatCustomization(ref changed);
                    }
                    finally { ImGui.EndTabItem(); }
                }
                if(ImGui.BeginTabItem("热键栏"))
                {
                    try{DrawHotbarSettings();}
                    finally{ImGui.EndTabItem();}
                }
                if (ImGui.BeginTabItem("顶部"))
                {
                    try
                    {
                    changed |= Toggle("显示入口图标",C.TopIcons,v=>C.TopIcons=v);
                    ChatSlider("顶部图标大小",C.TopIconSize,14,32,v=>C.TopIconSize=v,ref changed);
                    ChatSlider("项目间距",C.TopGap,0,24,v=>C.TopGap=v,ref changed);
                    ChatSlider("行内上下留白",C.TopRowPadding,2,14,v=>C.TopRowPadding=v,ref changed);
                    ChatSlider("名称最大宽度（超长显示省略号）",C.TopMaxLabelWidth,60,300,v=>C.TopMaxLabelWidth=v,ref changed);
                    ReadableText.TextWrapped("快捷入口均匀分布，勾选右侧常驻的项目独立靠右；窄屏自动分行，悬停显示完整名称。常驻项目仍可取消显示。隐藏原版 UI 时保留设置与恢复入口。");
                    foreach (var entry in Configuration.TopLabels)
                    {
                        ImGui.PushID(entry.Key);
                        changed |= Toggle("显示："+entry.Value, C.TopSections.GetValueOrDefault(entry.Key, true), v => C.TopSections[entry.Key] = v);
                        ImGui.SameLine();
                        changed |= Toggle("右侧常驻", C.TopPinned.GetValueOrDefault(entry.Key,false),v=>C.TopPinned[entry.Key]=v);
                        ImGui.PopID();
                    }
                    }
                    finally { ImGui.EndTabItem(); }
                }
                if (ImGui.BeginTabItem("原版适配"))
                {
                    try
                    {
                    changed |= Toggle("小地图方形外框", C.SquareMinimap, v => C.SquareMinimap = v);
                    changed |= Toggle("方形地图替代原版小地图", C.RebuildMinimap, v => C.RebuildMinimap = v);
                    changed |= Toggle("插件图标扁平化", C.FlatIcons, v => C.FlatIcons = v);
                    changed |= Toggle("游戏图标平面重绘（实验）", C.FlatNativeIcons, v => C.FlatNativeIcons = v);
                    changed |= Toggle("菜单与技能按钮平面重绘（实验）", C.FlatNativeButtons, v => C.FlatNativeButtons = v);
                    var shade=C.ReadabilityShade; if(ImGui.SliderFloat("文字背衬强度", ref shade, 0, .8f)){C.ReadabilityShade=shade;changed=true;}
                    ReadableText.TextWrapped("方形地图铺满视口，读取游戏地图和标记。图标按原轮廓重绘色块与轮廓线；按钮生成新的底色与描边，保留游戏的冷却、拖放、裁切和状态反馈。纹理重绘会影响共享素材；逐窗排除控制窗口外观，图标与按钮使用全局开关。");
                    changed |= Toggle("原生标准窗口统一色调与透明度（实验）", C.StyleNativeWindows, v => C.StyleNativeWindows = v);
                    ReadableText.TextDisabled($"已显示热键栏 {plugin.NativeStyle.ActiveBars.Count} 个，已换肤窗口 {plugin.NativeStyle.StyledWindows.Count} 个，已替换纹理 {plugin.Redraw.Count} 个");
                    if(ReadableText.Button("打开耗时诊断"))PerformanceOpen=true;
                    if (ImGui.TreeNode("原生窗口适配审查／逐窗排除"))
                    {
                        foreach (var entry in plugin.GameUi.Layouts.OrderBy(i => i.Key))
                        {
                            var excluded = C.ExcludedNativeWindows.Contains(entry.Key);
                            if (ReadableText.Checkbox($"保留原版外观##{entry.Key}", ref excluded))
                            { if (excluded) C.ExcludedNativeWindows.Add(entry.Key); else C.ExcludedNativeWindows.Remove(entry.Key); changed = true; }
                            ImGui.SameLine(); ReadableText.TextUnformatted(entry.Key + (plugin.NativeStyle.StyledWindows.Contains(entry.Key) ? " · 标准窗口已适配" : HudGeometry.IsStandardHotbar(entry.Key) ? " · 原生热键栏" : " · 原生内容保留"));
                        }
                        ImGui.TreePop();
                    }
                    changed |= Toggle("采用读取到的原版布局", C.FollowGameLayout, v => C.FollowGameLayout = v);
                    if (ReadableText.Button("重新应用已读取布局")) importedLayouts.Clear();
                    ReadableText.TextDisabled($"已读取 {plugin.GameUi.Layouts.Count} 个已加载窗口，游戏 UI 比例 {plugin.GameUi.GameScale:F2}");
                    if (ImGui.TreeNode("已读取的原版窗口设置"))
                    {
                        foreach (var item in plugin.GameUi.Layouts.OrderBy(i => i.Key))
                            ReadableText.TextUnformatted($"{item.Key}: ({item.Value.X:F0}, {item.Value.Y:F0}) · 比例 {item.Value.Scale:F2} · 原先{(item.Value.Visible ? "显示" : "隐藏")}");
                        ImGui.TreePop();
                    }
                    }
                    finally { ImGui.EndTabItem(); }
                }
                }
                finally { ImGui.EndTabBar(); }
            }
            ImGui.Separator();
            ReadableText.TextDisabled("/xlinui off 完全恢复原版；/xlinui restore 取消全局与聊天隐藏。");
            if (ReadableText.Button("恢复默认设置")) { plugin.Reset(); changed = false; }
        }
        finally
        {
            EndPanel();
            if (changed) plugin.Save();
        }
    }

    private static bool Toggle(string name, bool value, Action<bool> setter)
    {
        if (!ReadableText.Checkbox(name, ref value)) return false;
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
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(8, 5));
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 6));
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8, 5));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, c.HideBorders || c.TextOnly ? 0 : 1);
            ImGui.PushStyleVar(ImGuiStyleVar.ChildBorderSize, c.HideBorders || c.TextOnly ? 0 : 1);
            ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, c.HideBorders || c.TextOnly ? 0 : 1);
            var tint = c.Tint;
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(c.BackgroundColor, c.Material!=0 ? 0 : SurfacePolicy.PanelAlpha(c.ForceTransparentPanels, c.TextOnly, c.Glass, c.Opacity)));
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(tint * .24f, c.ForceTransparentPanels || c.TextOnly ? 0 : .6f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(tint * .45f, 1));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(tint * .6f, 1));
            ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(tint * .35f, .8f));
            ImGui.PushStyleColor(ImGuiCol.CheckMark, new Vector4(tint, 1));
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(.24f, .29f, .35f, .65f));
            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(c.ForegroundColor, 1));
            ImGui.PushStyleColor(ImGuiCol.TextDisabled, c.HighContrastText ? new Vector4(.83f, .88f, .92f, 1) : new Vector4(.59f, .65f, .73f, 1));
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(.08f, .10f, .14f, c.ForceTransparentPanels || c.TextOnly ? .10f : .95f));
            ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(.055f, .07f, .10f, .95f));
            ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(.08f, .10f, .14f, c.ForceTransparentPanels || c.TextOnly ? .10f : .95f));
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(tint, .9f));
            ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(.24f, .29f, .35f, c.TextOnly ? 0 : .6f));
        }
        public void Dispose() { ImGui.PopStyleColor(15); ImGui.PopStyleVar(9); }
    }
}
