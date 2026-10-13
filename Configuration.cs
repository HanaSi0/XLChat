using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;
using Newtonsoft.Json;

namespace XLinUI;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 5;
    public bool ForceTransparentPanels { get; set; } = true;
    public bool ForceTransparentNative { get; set; } = true;
    public bool TextOutline { get; set; } = true;
    public bool HighContrastText { get; set; } = true;
    public bool NativeTextOutline { get; set; } = true;
    public bool ShowEmptyStatuses { get; set; }
    public float HudFontScale { get; set; } = 1.05f;
    public bool SquareHotbars { get; set; } = true;
    public bool SquareMinimap { get; set; } = true;
    public bool ModernTarget { get; set; } = true;
    public bool ModernFocus { get; set; } = true;
    public bool ModernCast { get; set; } = true;
    public bool ModernStatuses { get; set; } = true;
    public bool ModernParty { get; set; }
    public float StatusIconSize { get; set; } = 30;
    public bool ModernInventory { get; set; } = true;
    public bool ModernQuests { get; set; } = true;
    public bool StyleNativeWindows { get; set; } = true;
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public HashSet<string> ExcludedNativeWindows { get; set; } = [];
    public bool NativeCombatWidgets { get; set; } = true;
    public bool TextOnly { get; set; }
    public bool HideBorders { get; set; } = true;
    public bool LockWindows { get; set; }
    public float ChatWidth { get; set; } = 780;
    public float ChatHeight { get; set; } = 430;
    public float ChatSidebarWidth { get; set; } = 145;
    public float ChatFontScale { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public bool ShowTop { get; set; } = true;
    public bool ShowChat { get; set; } = true;
    public bool HideOriginalChat { get; set; } = true;
    public bool ShowOriginalUi { get; set; } = true;
    public bool ShowStatus { get; set; } = true;
    public bool ShowParty { get; set; } = true;
    public bool FollowGameLayout { get; set; } = true;
    public bool ShowSkills { get; set; } = true;
    public bool Rounded { get; set; } = true;
    public bool Glass { get; set; } = true;
    public bool Compact { get; set; }
    public bool NativeInteractions { get; set; }
    public float Opacity { get; set; } = .88f;
    public Vector3 Tint { get; set; } = new(.39f, .77f, .73f);
    public int Hotbar { get; set; }
    public float SkillSize { get; set; } = 44;
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<Shortcut> Shortcuts { get; set; } = DefaultShortcuts();
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<Channel> Channels { get; set; } = DefaultChannels();

    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public Dictionary<string, bool> TopSections { get; set; } = DefaultTopSections();
    public static readonly Dictionary<string, string> TopLabels = new()
    {
        ["inventory"]="背包概览", ["quests"]="任务概览", ["gamewindows"]="游戏功能", ["effects"]="特效", ["tribes"]="部族", ["online"]="在线状态", ["jobs"]="职业", ["equipment"]="装备", ["currency"]="货币", ["timers"]="雇员/任务情报", ["teleport"]="传送", ["map"]="地图", ["plugins"]="插件收纳", ["commands"]="指令收纳", ["zone"]="区域/地图/天气", ["fps"]="FPS", ["dtr"]="其他插件 DTR", ["sound"]="音量", ["clock"]="LT/ET",
    };
    private static Dictionary<string, bool> DefaultTopSections()
    {
        var values=new Dictionary<string,bool>();
        foreach(var key in TopLabels.Keys) values[key]=true;
        return values;
    }

    public void ApplyModernPreset()
    {
        Enabled = ShowOriginalUi = NativeCombatWidgets = ShowTop = ShowStatus = ShowChat = true;
        ModernTarget = ModernFocus = ModernCast = ModernStatuses = ModernInventory = ModernQuests = true;
        StyleNativeWindows = SquareHotbars = SquareMinimap = HideOriginalChat = true;
        HideBorders = Glass = Rounded = true; TextOnly = false; Opacity = .88f;
        ForceTransparentPanels = ForceTransparentNative = TextOutline = NativeTextOutline = HighContrastText = true;
        ShowEmptyStatuses = false;
        // Preserve interaction opt-in, custom channels, toolbar choices and exclusions.
    }

    public void Normalize()
    {
        Version = 5;
        HudFontScale = float.IsFinite(HudFontScale) ? Math.Clamp(HudFontScale, .8f, 1.6f) : 1.05f;
        ExcludedNativeWindows ??= [];
        StatusIconSize = float.IsFinite(StatusIconSize) ? Math.Clamp(StatusIconSize, 20, 48) : 30;
        TopSections ??= DefaultTopSections();
        foreach (var key in TopLabels.Keys) if (!TopSections.ContainsKey(key)) TopSections[key] = true;
        Opacity = float.IsFinite(Opacity) ? Math.Clamp(Opacity, 0, 1) : .88f;
        ChatWidth = float.IsFinite(ChatWidth) ? Math.Clamp(ChatWidth, 380, 1800) : 780;
        ChatHeight = float.IsFinite(ChatHeight) ? Math.Clamp(ChatHeight, 240, 1200) : 430;
        ChatSidebarWidth = float.IsFinite(ChatSidebarWidth) ? Math.Clamp(ChatSidebarWidth, 80, 250) : 145;
        ChatFontScale = float.IsFinite(ChatFontScale) ? Math.Clamp(ChatFontScale, .7f, 1.6f) : 1;
        SkillSize = float.IsFinite(SkillSize) ? Math.Clamp(SkillSize, 32, 72) : 44;
        Hotbar = Math.Clamp(Hotbar, 0, 9);
        Tint = new Vector3(Clamp(Tint.X), Clamp(Tint.Y), Clamp(Tint.Z));
        Shortcuts ??= DefaultShortcuts();
        Channels ??= DefaultChannels();
        Channels.RemoveAll(c => c is null);
        if (Channels.Count == 0) Channels = DefaultChannels();
        foreach (var c in Channels)
        {
            c.Id = string.IsNullOrWhiteSpace(c.Id) ? Guid.NewGuid().ToString("N") : c.Id;
            c.Name = string.IsNullOrWhiteSpace(c.Name) ? "未命名" : c.Name;
            c.Types ??= [];
        }
    }

    private static float Clamp(float value) => float.IsFinite(value) ? Math.Clamp(value, 0, 1) : .5f;
    public static List<Shortcut> DefaultShortcuts() =>
    [
        new("角色", 2), new("背包", 10), new("任务", 4),
        new("地图", 16), new("传送", 35), new("好友", 13),
    ];
    public static List<Channel> DefaultChannels() =>
    [
        new("全部", []), new("小队", [14, 15]),
        new("社交", [24, 16, 17, 18, 19, 20, 21, 22, 23, 37, 101, 102, 103, 104, 105, 106, 107]),
        new("私信", [12, 13]), new("系统", [2, 3, 56, 57, 58, 59, 60, 62, 69, 70]),
    ];
}

[Serializable]
public sealed class Shortcut(string name, uint command)
{
    public string Name { get; set; } = name;
    public uint Command { get; set; } = command;
    public bool Enabled { get; set; } = true;
}

[Serializable]
public sealed class Channel(string name, List<ushort> types)
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = name;
    // Empty means all received messages. These values are LogKind IDs, not send commands.
    public List<ushort> Types { get; set; } = types;
}
