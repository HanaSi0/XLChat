using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Configuration;
using Newtonsoft.Json;

namespace XLinUI;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 6;
    public int Material { get; set; } // 0 existing, 1 solid, 2 image, 3 sampled glass
    public string BackgroundFile { get; set; } = "";
    public Vector3 BackgroundColor { get; set; } = new(.035f,.045f,.065f);
    public Vector3 ForegroundColor { get; set; } = new(.95f,.97f,1);
    public float BackgroundOpacity { get; set; } = .85f;
    public float BackgroundTintOpacity { get; set; } = .35f;
    public float ImageBlur { get; set; }
    public int ImageFit { get; set; }
    public float GlassBlur { get; set; } = 20;
    public float GlassRefresh { get; set; } = 2;
    public bool UseCustomFont { get; set; }
    public string FontFile { get; set; } = "";
    public float FontSize { get; set; } = 18;
    public int FontFace { get; set; }

    public ChatNavigation ChatNavigation { get; set; }
    public ChatPresentation ChatPresentation { get; set; }
    public ChatBackground ChatBackground { get; set; }
    public float ChatBackgroundOpacity { get; set; } = .25f;
    public float ChatComposerHeight { get; set; } = 120;
    public float ChatSearchWidth { get; set; }
    public float ChatSearchHeight { get; set; } = 28;
    public float ChatHeaderFontScale { get; set; } = 1;
    public float ChatTimestampFontScale { get; set; } = .85f;
    public float ChatScrollbarWidth { get; set; } = 10;
    public float ChatSendButtonHeight { get; set; } = 32;
    public bool ChatShowNoviceJoin { get; set; } = true;
    public bool ChatShowSendButton { get; set; } = true;
    public float ChatInputHeight { get; set; } = 32;
    public float ChatInputWidth { get; set; }
    public float ChatSendButtonWidth { get; set; } = 60;
    public float ChatSelectorWidth { get; set; } = 110;
    public float ChatSidebarFontScale { get; set; } = 1;
    public float ChatSidebarItemHeight { get; set; } = 30;
    public float ChatMessageWidth { get; set; }
    public float ChatLineSpacing { get; set; } = 3;
    public float ChatGroupSpacing { get; set; } = 8;
    public float ChatPadding { get; set; } = 8;
    public bool ChatShowSearch { get; set; } = true;
    public bool ChatIncludeHandled { get; set; }
    public bool ChatShowTimestamp { get; set; } = true;
    public bool ChatTimestampSeconds { get; set; }
    public bool ChatShowChannelName { get; set; } = true;
    public bool ChatGroupMessages { get; set; } = true;
    public bool ChatFollow { get; set; } = true;
    public string ChatActiveChannelId { get; set; } = "";
    public string ChatSendRoute { get; set; } = "say";
    public bool ChatAutoSendRoute { get; set; }
    public bool ChatHasPosition { get; set; }
    public Vector2 ChatPosition { get; set; } = new(30,150);
    public int ChatHistoryLimit { get; set; } = 2000;
    public int ChatPageSize { get; set; } = 400;
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public Dictionary<string,ChatLayoutProfile> ChatProfiles { get; set; } = [];

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
    public bool TopIcons { get; set; } = true;
    public float TopIconSize { get; set; } = 20;
    public float TopGap { get; set; } = 8;
    public float TopRowPadding { get; set; } = 5;
    public float TopMaxLabelWidth { get; set; } = 180;
    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public Dictionary<string,bool> TopPinned { get; set; } = new() { ["currency"]=true,["equipment"]=true,["fps"]=true,["sound"]=true,["clock"]=true };
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
        ["lock"]="布局锁定", ["settings"]="设置", ["original"]="原版 UI 开关", ["inventory"]="背包概览", ["quests"]="任务概览", ["gamewindows"]="游戏功能", ["effects"]="特效", ["tribes"]="部族", ["online"]="在线状态", ["jobs"]="职业", ["equipment"]="装备", ["currency"]="货币", ["timers"]="雇员/任务情报", ["teleport"]="传送", ["map"]="地图", ["plugins"]="插件收纳", ["commands"]="指令收纳", ["zone"]="区域/地图/天气", ["fps"]="FPS", ["dtr"]="其他插件 DTR", ["sound"]="音量", ["clock"]="LT/ET",
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
        Material=Math.Clamp(Material,0,3);ImageFit=Math.Clamp(ImageFit,0,1);
        BackgroundFile??="";FontFile??="";
        BackgroundOpacity=Clamp(BackgroundOpacity);BackgroundTintOpacity=Clamp(BackgroundTintOpacity);
        BackgroundColor=new(Clamp(BackgroundColor.X),Clamp(BackgroundColor.Y),Clamp(BackgroundColor.Z));
        ForegroundColor=new(Clamp(ForegroundColor.X),Clamp(ForegroundColor.Y),Clamp(ForegroundColor.Z));
        ImageBlur=Bound(ImageBlur,0,32,0);GlassBlur=Bound(GlassBlur,1,64,20);GlassRefresh=Bound(GlassRefresh,.5f,4,2);
        FontSize=Bound(FontSize,12,36,18);FontFace=Math.Clamp(FontFace,0,20);

        var oldVersion=Version;
        Version = 6;
        ChatNavigation=Enum.IsDefined(ChatNavigation)?ChatNavigation:ChatNavigation.Left;
        ChatPresentation=Enum.IsDefined(ChatPresentation)?ChatPresentation:ChatPresentation.Grouped;
        ChatBackground=Enum.IsDefined(ChatBackground)?ChatBackground:ChatBackground.Global;
        ChatBackgroundOpacity=Bound(ChatBackgroundOpacity,0,1,.25f);
        ChatComposerHeight=Bound(ChatComposerHeight,60,300,120);
        ChatSearchWidth=Bound(ChatSearchWidth,0,3000,0);
        ChatSearchHeight=Bound(ChatSearchHeight,24,80,28);
        ChatHeaderFontScale=Bound(ChatHeaderFontScale,.6f,2,1);
        ChatTimestampFontScale=Bound(ChatTimestampFontScale,.6f,1.5f,.85f);
        ChatScrollbarWidth=Bound(ChatScrollbarWidth,8,30,10);
        ChatSendButtonHeight=Bound(ChatSendButtonHeight,24,100,32);
        ChatInputHeight=Bound(ChatInputHeight,24,100,32);
        ChatInputWidth=Bound(ChatInputWidth,0,3000,0);
        ChatSendButtonWidth=Bound(ChatSendButtonWidth,40,180,60);
        ChatSelectorWidth=Bound(ChatSelectorWidth,60,300,110);
        ChatSidebarFontScale=Bound(ChatSidebarFontScale,.6f,2,1);
        ChatSidebarItemHeight=Bound(ChatSidebarItemHeight,20,80,30);
        ChatMessageWidth=Bound(ChatMessageWidth,0,3000,0);
        ChatLineSpacing=Bound(ChatLineSpacing,0,30,3);
        ChatGroupSpacing=Bound(ChatGroupSpacing,0,50,8);
        ChatPadding=Bound(ChatPadding,0,30,8);
        ChatPosition=new Vector2(Bound(ChatPosition.X,0,16384,30),Bound(ChatPosition.Y,0,16384,150));
        ChatHistoryLimit=Math.Clamp(ChatHistoryLimit,200,10000);
        ChatPageSize=Math.Clamp(ChatPageSize,100,2000);
        ChatProfiles ??= [];
        if(!ChatPolicy.KnownRoute(ChatSendRoute))ChatSendRoute="say";

        HudFontScale = float.IsFinite(HudFontScale) ? Math.Clamp(HudFontScale, .8f, 1.6f) : 1.05f;
        ExcludedNativeWindows ??= [];
        StatusIconSize = float.IsFinite(StatusIconSize) ? Math.Clamp(StatusIconSize, 20, 48) : 30;
        TopIconSize=Bound(TopIconSize,14,32,20);TopGap=Bound(TopGap,0,24,8);TopRowPadding=Bound(TopRowPadding,2,14,5);TopMaxLabelWidth=Bound(TopMaxLabelWidth,60,300,180);
        TopPinned??=new() { ["currency"]=true,["equipment"]=true,["fps"]=true,["sound"]=true,["clock"]=true };
        TopSections ??= DefaultTopSections();
        foreach (var key in TopLabels.Keys) if (!TopSections.ContainsKey(key)) TopSections[key] = true;
        Opacity = float.IsFinite(Opacity) ? Math.Clamp(Opacity, 0, 1) : .88f;
        ChatWidth = float.IsFinite(ChatWidth) ? Math.Clamp(ChatWidth, 320, 3840) : 780;
        ChatHeight = float.IsFinite(ChatHeight) ? Math.Clamp(ChatHeight, 200, 2160) : 430;
        ChatSidebarWidth = float.IsFinite(ChatSidebarWidth) ? Math.Clamp(ChatSidebarWidth, 40, 600) : 145;
        ChatFontScale = float.IsFinite(ChatFontScale) ? Math.Clamp(ChatFontScale, .6f, 2.5f) : 1;
        SkillSize = float.IsFinite(SkillSize) ? Math.Clamp(SkillSize, 32, 72) : 44;
        Hotbar = Math.Clamp(Hotbar, 0, 9);
        Tint = new Vector3(Clamp(Tint.X), Clamp(Tint.Y), Clamp(Tint.Z));
        Shortcuts ??= DefaultShortcuts();
        Channels ??= DefaultChannels();
        Channels.RemoveAll(c => c is null);
        if (Channels.Count == 0) Channels = DefaultChannels();
        var ids=new HashSet<string>();
        foreach (var c in Channels)
        {
            c.Id = string.IsNullOrWhiteSpace(c.Id) ? Guid.NewGuid().ToString("N") : c.Id;
            c.Name = string.IsNullOrWhiteSpace(c.Name) ? "未命名" : c.Name;
            c.Types ??= [];
            if(!ids.Add(c.Id)){c.Id=Guid.NewGuid().ToString("N");ids.Add(c.Id);}
            c.Types=new List<ushort>(new HashSet<ushort>(c.Types));
            if(oldVersion<6)c.SendRoute=ChatPolicy.DefaultRoute(c.Types);
            if(!ChatPolicy.KnownRoute(c.SendRoute))c.SendRoute="say";
            c.Color=new Vector3(Clamp(c.Color.X),Clamp(c.Color.Y),Clamp(c.Color.Z));

        }
        if(!Channels.Exists(c=>c.Id==ChatActiveChannelId))ChatActiveChannelId=Channels[0].Id;
    }

    private static float Bound(float value,float min,float max,float fallback)=>float.IsFinite(value)?Math.Clamp(value,min,max):fallback;
    private static float Clamp(float value) => float.IsFinite(value) ? Math.Clamp(value, 0, 1) : .5f;
    public static List<Shortcut> DefaultShortcuts() =>
    [
        new("角色", 2), new("背包", 10), new("任务", 4),
        new("地图", 16), new("传送", 35), new("好友", 13),
    ];
    public static List<Channel> DefaultChannels() =>
    [
        new("全部", []), new("小队", [14, 15]) {SendRoute="party"},
        new("社交", [24, 16, 17, 18, 19, 20, 21, 22, 23, 37, 101, 102, 103, 104, 105, 106, 107]) {SendRoute="fc"},
        new("私信", [12, 13]) {SendRoute="tell"}, new("系统", [2, 3, 56, 57, 58, 59, 60, 62, 69, 70]) {SendRoute="echo"},
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
    public string SendRoute { get; set; } = "say";
    public bool CustomColor { get; set; }
    public Vector3 Color { get; set; } = new(.39f,.77f,.73f);

}
