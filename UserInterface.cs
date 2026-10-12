using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;

namespace XLinUI;

internal sealed class UserInterface(Plugin plugin)
{
    internal bool SettingsOpen;
    internal bool ChatVisible { get; private set; }
    private string search = "";
    private string draft = "";
    private string notice = "";
    private int sendChannel;
    private bool follow = true;
    private int lastCount;
    private readonly string[] sendNames = ["说话", "小队", "部队", "喊话", "呼喊"];
    private readonly string[] sendCommands = ["/say", "/party", "/fc", "/yell", "/shout"];
    private Configuration C => plugin.Config;

    internal void ClearInput() => draft = "";
    internal void Draw(bool loggedIn)
    {
        ChatVisible = false;
        using var theme = new Theme(C);
        if (C.Enabled && loggedIn)
        {
            if (C.ShowTop) DrawTop();
            if (C.ShowChat) DrawChat();
            if (C.ShowSkills) DrawSkills();
        }
        if (SettingsOpen) DrawSettings();
    }

    private void DrawTop()
    {
        var viewport = ImGuiHelpers.MainViewport;
        ImGui.SetNextWindowPos(viewport.Pos);
        ImGui.SetNextWindowSize(new Vector2(viewport.Size.X, 52 * ImGuiHelpers.GlobalScale));
        var visible = ImGui.Begin("XLinUI · 顶部###XLinUITop", ImGuiWindowFlags.NoDecoration |
            ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoScrollbar);
        try
        {
            if (!visible) return;
            ImGui.TextColored(new Vector4(C.Tint, 1), "XLinUI"); ImGui.SameLine();
            foreach (var shortcut in C.Shortcuts.Where(s => s.Enabled))
            {
                ImGui.PushID((int)shortcut.Command);
                try
                {
                    ImGui.BeginDisabled(!C.NativeInteractions);
                    if (ImGui.Button(shortcut.Name)) Native(() => GameBridge.Open(shortcut.Command));
                    ImGui.EndDisabled();
                }
                finally { ImGui.PopID(); }
                ImGui.SameLine();
            }
            if (ImGui.Button("设置")) SettingsOpen = true;
        }
        finally { ImGui.End(); }
    }

    private void DrawChat()
    {
        var scale = ImGuiHelpers.GlobalScale;
        ImGui.SetNextWindowSize(new Vector2(820, 500) * scale, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(550, 340) * scale, new Vector2(float.MaxValue));
        ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(30, 150), ImGuiCond.FirstUseEver);
        var open = C.ShowChat;
        var visible = ImGui.Begin("XLinUI · 聊天###XLinUIChat", ref open, ImGuiWindowFlags.NoScrollbar);
        try
        {
            if (!visible || !open) return;
            ChatVisible = true;
            if (C.Channels.All(c => c.Id != plugin.ActiveChannel)) plugin.ActiveChannel = C.Channels[0].Id;
            var channelNow = C.Channels.First(c => c.Id == plugin.ActiveChannel);
            var sidebar = ImGui.BeginChild("categories", new Vector2(155 * scale, 0), true);
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
                    ImGui.TextWrapped("关闭或折叠此窗口会恢复原版聊天。");
                }
            }
            finally { ImGui.EndChild(); }
            ImGui.SameLine();
            var conversation = ImGui.BeginChild("conversation", Vector2.Zero, false);
            try
            {
                if (!conversation) { ChatVisible = false; return; }
                ImGui.TextColored(new Vector4(C.Tint, 1), channelNow.Name);
                ImGui.SameLine(); ImGui.TextDisabled(C.HideOriginalChat ? "· 原版聊天已隐藏" : "· 原版聊天保留");
                ImGui.SetNextItemWidth(-1);
                ImGui.InputTextWithHint("##history-search", "搜索发言人或消息", ref search, 128);
                var height = Math.Max(80 * scale, ImGui.GetContentRegionAvail().Y - 100 * scale);
                var content = ImGui.BeginChild("messages", new Vector2(0, height), true);
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
            ImGui.End();
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

    private void DrawSkills()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 0);
        var visible = ImGui.Begin("XLinUI · 技能###XLinUISkills", ImGuiWindowFlags.AlwaysAutoResize);
        try
        {
            if (!visible) return;
            if (!C.NativeInteractions)
            {
                ImGui.TextUnformatted("方角技能栏：在设置中启用游戏交互后显示热键栏镜像。");
                return;
            }
            var size = C.SkillSize * ImGuiHelpers.GlobalScale;
            for (var i = 0; i < 12; i++)
            {
                if (i > 0 && i % 6 != 0) ImGui.SameLine();
                ImGui.PushID(i);
                try
                {
                    var slot = GameBridge.ReadSlot(C.Hotbar, i);
                    var pos = ImGui.GetCursorScreenPos();
                    var texture = slot.Icon == 0 ? null : Plugin.Textures.GetFromGameIcon(new GameIconLookup(slot.Icon)).GetWrapOrDefault();
                    var clicked = ImGui.Button("##slot", new Vector2(size));
                    if (texture != null)
                    {
                        var inset = new Vector2(3 * ImGuiHelpers.GlobalScale);
                        ImGui.GetWindowDrawList().AddImage(texture.Handle, pos + inset, pos + new Vector2(size) - inset);
                    }
                    if (slot.Cooldown > 0)
                    {
                        ImGui.GetWindowDrawList().AddRectFilled(pos, pos + new Vector2(size), 0x88000000);
                        ImGui.GetWindowDrawList().AddText(pos + new Vector2(5, 5), 0xFFFFFFFF, slot.Cooldown.ToString());
                    }
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip($"热键栏 {C.Hotbar + 1} · 槽位 {i + 1}\n{slot.Type ?? "空"} / {slot.Command}\n键盘快捷键继续使用游戏原版绑定。");
                    if (clicked && slot.Command != 0)
                    {
                        var index = i;
                        Native(() => GameBridge.ExecuteSlot(C.Hotbar, index));
                    }
                }
                finally { ImGui.PopID(); }
            }
        }
        catch (Exception ex)
        {
            C.NativeInteractions = false; plugin.Save();
            Plugin.Log.Error(ex, "热键栏接口不可用，已关闭游戏交互");
            ImGui.TextWrapped("热键栏接口不可用；已关闭游戏交互，请查看 /xllog。");
        }
        finally { ImGui.End(); ImGui.PopStyleVar(2); }
    }

    private void DrawSettings()
    {
        ImGui.SetNextWindowSize(new Vector2(600, 650) * ImGuiHelpers.GlobalScale, ImGuiCond.FirstUseEver);
        var visible = ImGui.Begin("XLinUI · 设置###XLinUISettings", ref SettingsOpen);
        var changed = false;
        try
        {
            if (!visible) return;
            ImGui.TextWrapped("原版 HUD 保留。/xlinui off 可关闭所有新面板。技能 UI 固定直角。");
            changed |= Toggle("启用 XLinUI", C.Enabled, v => C.Enabled = v);
            changed |= Toggle("顶部快捷栏", C.ShowTop, v => C.ShowTop = v);
            changed |= Toggle("分类聊天", C.ShowChat, v => C.ShowChat = v);
            changed |= Toggle("显示插件聊天时隐藏原版聊天", C.HideOriginalChat, v => C.HideOriginalChat = v);
            ImGui.TextDisabled("折叠、关闭插件聊天或停用插件时恢复原版聊天。");
            changed |= Toggle("方角技能栏", C.ShowSkills, v => C.ShowSkills = v);
            changed |= Toggle("其他 UI 使用圆角", C.Rounded, v => C.Rounded = v);
            changed |= Toggle("半透明玻璃风格", C.Glass, v => C.Glass = v);
            ImGui.TextDisabled("本版无背景模糊；玻璃开关影响背景透明度。");
            var opacity = C.Opacity;
            if (ImGui.SliderFloat("背景不透明度", ref opacity, .05f, 1, "%.2f")) { C.Opacity = opacity; changed = true; }
            var tint = C.Tint;
            if (ImGui.ColorEdit3("自定义色调", ref tint)) { C.Tint = tint; changed = true; }
            changed |= Toggle("紧凑聊天间距", C.Compact, v => C.Compact = v);
            ImGui.Separator();
            ImGui.TextWrapped("游戏交互包括打开游戏窗口、发送聊天及读取/点击热键栏。国服 soil 接口须在本机验证；推荐用实际安装目录重新编译。");
            changed |= Toggle("启用游戏交互", C.NativeInteractions, v => C.NativeInteractions = v);
            var bar = C.Hotbar + 1;
            if (ImGui.SliderInt("镜像热键栏", ref bar, 1, 10)) { C.Hotbar = bar - 1; changed = true; }
            var skillSize = C.SkillSize;
            if (ImGui.SliderFloat("技能格尺寸", ref skillSize, 32, 72, "%.0f")) { C.SkillSize = skillSize; changed = true; }
            if (ImGui.CollapsingHeader("顶部入口"))
                foreach (var shortcut in C.Shortcuts)
                    changed |= Toggle(shortcut.Name, shortcut.Enabled, v => shortcut.Enabled = v);
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
            ImGui.End();
            if (changed) plugin.Save();
        }
    }

    private static bool Toggle(string name, bool value, Action<bool> setter)
    {
        if (!ImGui.Checkbox(name, ref value)) return false;
        setter(value); return true;
    }

    private void Native(Action action)
    {
        if (!C.NativeInteractions || Plugin.Objects.LocalPlayer is null) return;
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
            var tint = c.Tint;
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(tint * .13f, c.Glass ? c.Opacity : 1));
            ImGui.PushStyleColor(ImGuiCol.ChildBg, Vector4.Zero);
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(tint * .24f, .85f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(tint * .45f, 1));
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(tint * .6f, 1));
            ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(tint * .35f, .8f));
            ImGui.PushStyleColor(ImGuiCol.CheckMark, new Vector4(tint, 1));
            ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(tint * .45f, .6f));
        }
        public void Dispose() { ImGui.PopStyleColor(8); ImGui.PopStyleVar(3); }
    }
}
