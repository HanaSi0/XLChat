using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Textures;
using Dalamud.Game.Gui.Dtr;
using Dalamud.Game.Addon.Events.EventDataTypes;
using Lumina.Excel.Sheets;
namespace XLinUI;
internal sealed partial class UserInterface
{
    private List<TopItem>? cachedTopItems;
    private DateTime nextTopRefresh;
    private int pluginCountDirty=1;
    internal void InvalidatePluginCount()=>System.Threading.Interlocked.Exchange(ref pluginCountDirty,1);
    private int cachedPluginCount;
    private List<TopItem>? layoutTopItems;
    private List<TopItem> layoutLeft=[],layoutRight=[];
    private TopLayout? cachedTopLayout;
    private (float Width,float Font,float Scale,float Gap,float Icon,float Max,bool Icons) topLayoutKey;
    private readonly Dictionary<(TopItem Item,float Width),string> fittedTopLabels=[];
    private void DrawTop()
    {
        if(cachedTopItems is not null&&DateTime.UtcNow<nextTopRefresh){DrawTopItems(cachedTopItems);return;}
        nextTopRefresh=DateTime.UtcNow.AddMilliseconds(250);
        if ((DateTime.UtcNow - lastTelemetry).TotalSeconds >= 1)
        {
            try
            {
                (gil, weather, serverTime) = GameBridge.ReadTopData();
                (minimumDurability,maximumSpiritbond)=GameBridge.EquipmentSummary();
                telemetryAvailable = true;
            }
            catch (Exception ex) { Plugin.Log.Debug(ex, "顶部状态暂不可用"); }
            lastTelemetry = DateTime.UtcNow;
        }
        if(System.Threading.Interlocked.Exchange(ref pluginCountDirty,0)!=0)cachedPluginCount=Plugin.Interface.InstalledPlugins.Count(p=>p.IsLoaded);
        var player = Plugin.Objects.LocalPlayer;
        var map = Plugin.Data.GetExcelSheet<Map>().GetRowOrDefault(Plugin.Client.MapId);
        var mapName = map is {} currentMap&&currentMap.PlaceName.IsValid?currentMap.PlaceName.Value.Name.ToString():null;
        if (string.IsNullOrWhiteSpace(mapName)) mapName = "地图";
        var onlineName = player is not null&&player.OnlineStatus.IsValid?player.OnlineStatus.Value.Name.ToString():null;
        if (string.IsNullOrWhiteSpace(onlineName)) onlineName = "在线";
        var jobName = player is not null&&player.ClassJob.IsValid?player.ClassJob.Value.Name.ToString():null;
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
            new($"插件收纳 {cachedPluginCount}", () => Plugin.Interface.OpenPluginInstallerTo(PluginInstallerOpenKind.InstalledPlugins)),
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
            new TopItem(C.LockWindows ? "解锁布局" : "锁定布局", () => { C.LockWindows = !C.LockWindows; plugin.Save(); }, Key:"lock"),
            new TopItem("游戏功能", () => gameWindowsOpen = !gameWindowsOpen, Key: "gamewindows"),
            new TopItem("设置", () => SettingsOpen = true, Key:"settings"),
            new TopItem(C.ShowOriginalUi ? "隐藏原版 UI" : "恢复原版 UI", () => { C.ShowOriginalUi = !C.ShowOriginalUi; plugin.Save(); }, Key:"original"),
        ]);
        items.RemoveAll(item => item.Key is not null && !C.TopSections.GetValueOrDefault(item.Key, true) && (C.ShowOriginalUi || item.Key is not ("settings" or "original")));
        cachedTopItems=items;DrawTopItems(items);
    }

    private sealed record TopItem(string Label,System.Action? Click=null,string? Tooltip=null,System.Action? RightClick=null,string? Key=null);
    private readonly Dictionary<string,uint> topIconCache=[];
    private readonly HashSet<string> topIconFailures=[];
    private uint TopGameIcon(string key)
    {
        try{return ResolveTopGameIcon(key);}
        catch(Exception ex)
        {
            if(topIconFailures.Add(key))Plugin.Log.Warning(ex,"读取顶部图标失败，使用内置图标：{Key}",key);
            return 0;
        }
    }
    private uint ResolveTopGameIcon(string key)
    {
        if(key=="jobs")return Plugin.Objects.LocalPlayer is {} p&&p.ClassJob.RowId>0&&p.ClassJob.IsValid?62100+p.ClassJob.RowId:0;
        if(key=="online")return Plugin.Objects.LocalPlayer is {} onlinePlayer&&onlinePlayer.OnlineStatus.IsValid?onlinePlayer.OnlineStatus.Value.Icon:0;
        if(topIconCache.TryGetValue(key,out var cached))return cached;
        var command=key switch {"equipment"=>2u,"timers"=>5u,"currency"=>66u,"teleport"=>35u,"map" or "zone"=>16u,"inventory"=>10u,"quests"=>4u,_=>0u};
        uint icon=command==0?0:(uint)Math.Max(0,Plugin.Data.GetExcelSheet<MainCommand>().GetRowOrDefault(command)?.Icon??0);
        if(key=="currency")icon=Plugin.Data.GetExcelSheet<Item>().GetRowOrDefault(1)?.Icon??icon;
        if(key is "tribes" or "effects")
        {
            var names=key=="tribes"?new[]{"部族","蛮族","Tribal","Tribe"}:new[]{"角色设置","Character Configuration"};
            icon=TopIconPolicy.Find(Plugin.Data.GetExcelSheet<MainCommand>(),r=>names.Any(n=>r.Name.ToString().Contains(n,StringComparison.OrdinalIgnoreCase)),r=>r.Icon);
        }
        topIconCache[key]=icon;return icon;
    }
    private FontAwesomeIcon TopFallback(string key)=>key switch
    {
        "effects"=>FontAwesomeIcon.Magic,"tribes"=>FontAwesomeIcon.Users,"online"=>FontAwesomeIcon.User,
        "equipment"=>FontAwesomeIcon.Hammer,"jobs"=>FontAwesomeIcon.ShieldAlt,"timers" or "clock"=>FontAwesomeIcon.Clock,
        "currency"=>FontAwesomeIcon.Coins,"teleport"=>FontAwesomeIcon.Bolt,"map"=>FontAwesomeIcon.Map,
        "plugins" or "dtr"=>FontAwesomeIcon.PuzzlePiece,"commands"=>FontAwesomeIcon.Terminal,
        "inventory"=>FontAwesomeIcon.ShoppingBag,"quests"=>FontAwesomeIcon.Scroll,"zone"=>FontAwesomeIcon.Globe,
        "fps"=>FontAwesomeIcon.TachometerAlt,"sound"=>FontAwesomeIcon.VolumeUp,
        "lock"=>C.LockWindows?FontAwesomeIcon.Lock:FontAwesomeIcon.LockOpen,"original"=>FontAwesomeIcon.Eye,_=>FontAwesomeIcon.Cog,
    };
    private static string FitTopLabel(string text,float width)
    {
        if(ImGui.CalcTextSize(text).X<=width)return text;
        const string ellipsis="…";if(width<ImGui.CalcTextSize(ellipsis).X)return "";
        var boundaries=StringInfo.ParseCombiningCharacters(text);var low=0;var high=boundaries.Length;
        while(low<high){var mid=(low+high+1)/2;var end=mid==boundaries.Length?text.Length:boundaries[mid];if(ImGui.CalcTextSize(text[..end]+ellipsis).X<=width)low=mid;else high=mid-1;}
        return text[..(low==boundaries.Length?text.Length:boundaries[low])]+ellipsis;
    }
    private void DrawTopItems(List<TopItem> items)
    {
        var scale=ImGuiHelpers.GlobalScale;var viewport=ImGuiHelpers.MainViewport;
        var iconSize=C.TopIconSize*scale;var padding=8*scale;var gap=C.TopGap*scale;
        var key=(viewport.Size.X,ImGui.GetFontSize(),scale,gap,iconSize,C.TopMaxLabelWidth,C.TopIcons);
        if(!ReferenceEquals(layoutTopItems,items)||topLayoutKey!=key)
        {
            layoutTopItems=items;topLayoutKey=key;layoutLeft.Clear();layoutRight.Clear();fittedTopLabels.Clear();
            foreach(var item in items)(C.TopPinned.GetValueOrDefault(item.Key??"",false)?layoutRight:layoutLeft).Add(item);
            float Measure(TopItem item)=>Math.Min(ImGui.CalcTextSize(item.Label).X,C.TopMaxLabelWidth*scale)+padding*2+2*scale+(C.TopIcons?iconSize+5*scale:0);
            cachedTopLayout=TopBarLayout.Arrange(Math.Max(1,viewport.Size.X-padding*2),gap,layoutLeft.Select(Measure).ToArray(),layoutRight.Select(Measure).ToArray());
        }
        var left=layoutLeft;var right=layoutRight;var plan=cachedTopLayout!;
        var rowHeight=Math.Max(ImGui.GetFontSize(),C.TopIcons?iconSize:0)+C.TopRowPadding*2*scale;
        ImGui.SetNextWindowPos(viewport.Pos);ImGui.SetNextWindowSize(new(viewport.Size.X,plan.Rows*rowHeight+8*scale));
        var visible=ImGui.Begin("##XLinUITop",ImGuiWindowFlags.NoDecoration|ImGuiWindowFlags.NoMove|ImGuiWindowFlags.NoSavedSettings|ImGuiWindowFlags.NoScrollbar|ImGuiWindowFlags.NoScrollWithMouse);
        try
        {
            if(!visible)return;Appearance.DrawBackground();Ready|=HudReady.Top;
            foreach(var slot in plan.Slots)
            {
                var item=(slot.Right?right:left)[slot.Index];var pos=ImGui.GetWindowPos()+new Vector2(padding+slot.X,4*scale+slot.Row*rowHeight);
                ImGui.SetCursorScreenPos(pos);ImGui.PushID((slot.Right?1000:0)+slot.Index);
                var clicked=ImGui.InvisibleButton("##top",new Vector2(slot.Width,rowHeight));var hovered=ImGui.IsItemHovered();
                var draw=ImGui.GetWindowDrawList();
                if(hovered&&item.Click is not null)draw.AddRectFilled(pos,pos+new Vector2(slot.Width,rowHeight),ImGui.ColorConvertFloat4ToU32(new Vector4(C.Tint,.22f)),C.Rounded?4*scale:0);
                draw.PushClipRect(pos,pos+new Vector2(slot.Width,rowHeight),true);
                var availableWidth=Math.Max(0,slot.Width-padding*2-(C.TopIcons?iconSize+5*scale:0));
                if(!fittedTopLabels.TryGetValue((item,availableWidth),out var text)){text=FitTopLabel(item.Label,availableWidth);fittedTopLabels[(item,availableWidth)]=text;}
                var content=ImGui.CalcTextSize(text).X+(C.TopIcons?iconSize+5*scale:0);
                var x=pos.X+Math.Max(padding,(slot.Width-content)/2);
                if(C.TopIcons)
                {
                    var iconPos=new Vector2(x,pos.Y+(rowHeight-iconSize)/2);var id=TopGameIcon(item.Key??"");
                    var texture=id==0?null:(C.FlatIcons?plugin.Flat.Get(id):null)??Plugin.Textures.GetFromGameIcon(new GameIconLookup(id)).GetWrapOrDefault();
                    if(texture is not null)draw.AddImage(texture.Handle,iconPos,iconPos+new Vector2(iconSize));
                    else using(Plugin.Interface.UiBuilder.IconFontHandle.Push())
                    {
                        var symbol=TopFallback(item.Key??"").ToIconString();var iconWidth=ImGui.CalcTextSize(symbol).X;
                        draw.AddText(new Vector2(iconPos.X+(iconSize-iconWidth)/2,pos.Y+(rowHeight-ImGui.GetFontSize())/2),0xfff2e8dc,symbol);
                    }
                    x+=iconSize+5*scale;
                }
                ReadableText.Draw(draw,new Vector2(x,pos.Y+(rowHeight-ImGui.GetFontSize())/2),ImGui.ColorConvertFloat4ToU32(new Vector4(C.ForegroundColor,1)),text);
                draw.PopClipRect();
                if(hovered){ImGui.SetTooltip(string.IsNullOrEmpty(item.Tooltip)?item.Label:item.Label+"\n"+item.Tooltip);if(item.RightClick is not null&&ImGui.IsMouseClicked(ImGuiMouseButton.Right))item.RightClick();}
                if(clicked)item.Click?.Invoke();ImGui.PopID();
            }
        }
        finally{ImGui.End();}
    }
}
