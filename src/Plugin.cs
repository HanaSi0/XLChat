using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Command;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace XLinUI;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface Interface { get; private set; } = null!;
    [PluginService] internal static ICommandManager Commands { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle Addons { get; private set; } = null!;
    [PluginService] internal static IClientState Client { get; private set; } = null!;
    [PluginService] internal static IPlayerState Player { get; private set; } = null!;
    [PluginService] internal static IDataManager Data { get; private set; } = null!;
    [PluginService] internal static IDtrBar Dtr { get; private set; } = null!;
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;
    [PluginService] internal static ITargetManager Targets { get; private set; } = null!;
    [PluginService] internal static IPartyList Party { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;
    [PluginService] internal static ITextureReadbackProvider Readback { get; private set; } = null!;
    [PluginService] internal static ITextureProvider Textures { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    internal static FlatButtonAtlases ButtonCache {get;private set;}=null!;
    internal static FlatIcons FlatCache {get;private set;}=null!;
    internal static Configuration InstanceConfig { get; private set; } = null!;
    internal Configuration Config { get; private set; }
    internal readonly List<ChatLine> History = [];
    private readonly ConcurrentQueue<ChatLine> pendingChat = new();
    private long chatSequence;
    internal readonly Dictionary<string, int> Unread = [];
    internal string ActiveChannel;
    private readonly UserInterface ui;
    private bool wasLoggedIn;
    internal int HistoryRevision;
    internal string LatestChatError="";
    internal DateTime LatestChatErrorTime;
    private readonly OriginalChatVisibility originalChat = new();
    internal readonly GameUiController GameUi = new();
    internal readonly NativeUiStyling NativeStyle = new();
    internal readonly NativeHotbarLayouts Hotbars = new();
    internal readonly FlatIcons Flat = new();
    internal readonly FlatButtonAtlases Buttons=new();
    internal readonly NativeTextureRedraw Redraw=new();
    internal static NativeTextureRedraw NativeRedraw {get;private set;}=null!;
    internal static AddonNames Names {get;}=new();
    internal static PerformanceMonitor Metrics {get;}=new();

    public Plugin()
    {
        Config = Interface.GetPluginConfig() as Configuration ?? new Configuration();
        Config.Normalize();
        InstanceConfig = Config;
        FlatCache=Flat;ButtonCache=Buttons;NativeRedraw=Redraw;
        ActiveChannel = Config.ChatActiveChannelId;
        ui = new UserInterface(this);
        Commands.AddHandler("/xlinui", new CommandInfo(OnCommand)
        { HelpMessage = "XLinUI 设置。/xlinui off 关闭全部；/xlinui on 开启；/xlinui restore 恢复原版聊天；/xlinui perf 耗时诊断。" });
        Chat.ChatMessageUnhandled += OnChat;
        Chat.ChatMessageHandled += OnHandledChat;
        Framework.Update += OnUpdate;
        Names.Register();
        originalChat.Register();
        GameUi.Register();
        NativeStyle.Register();
        Hotbars.Register();
        Interface.ActivePluginsChanged += OnActivePluginsChanged;
        Interface.UiBuilder.Draw += Draw;
        Interface.UiBuilder.OpenConfigUi += OpenSettings;
        Interface.UiBuilder.OpenMainUi += OpenSettings;
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLowerInvariant())
        {
            case "off": Config.Enabled = false; Save(); break;
            case "on": Config.Enabled = true; Save(); break;
            case "perf": ui.PerformanceOpen=true; Log.Information("{Report}",Metrics.Report()); break;
            case "restore": Config.HideOriginalChat = false; Config.ShowOriginalUi = true; Save(); break;
            default: OpenSettings(); break;
        }
    }

    private long lastPluginDraw=System.Diagnostics.Stopwatch.GetTimestamp(),lastWatchdog;
    private void OnUpdate(IFramework framework)
    {
        for(var drained=0;drained<128&&pendingChat.TryDequeue(out var line);drained++)
        {
            History.Add(line); HistoryRevision++;
            if(line.Type==60){LatestChatError=line.Text;LatestChatErrorTime=DateTime.Now;}
            foreach(var channel in Config.Channels.Where(c=>c.Types.Count==0||c.Types.Contains(line.Type)))
                if(channel.Id!=ActiveChannel||!Config.Enabled||!Config.ShowChat||!ui.ChatVisible||!ui.ChatAtBottom)
                    Unread[channel.Id]=Math.Min(9999,Unread.GetValueOrDefault(channel.Id)+1);
        }
        if(History.Count>Config.ChatHistoryLimit){History.RemoveRange(0,History.Count-Config.ChatHistoryLimit);HistoryRevision++;}
        // UI ownership is updated once after drawing; the watchdog only runs when drawing stops.
        if(System.Diagnostics.Stopwatch.GetElapsedTime(lastPluginDraw).TotalMilliseconds>500&&System.Diagnostics.Stopwatch.GetElapsedTime(lastWatchdog).TotalMilliseconds>500)
        {lastWatchdog=System.Diagnostics.Stopwatch.GetTimestamp();GameUi.Update();NativeStyle.Update();originalChat.Update();Hotbars.Release();}
    }

    private void OnActivePluginsChanged(IActivePluginsChangedEventArgs args)=>ui.InvalidatePluginCount();
    private void OpenSettings() => ui.SettingsOpen = true;
    internal void Save()
    {
        Config.Normalize();
        if(Config.Channels.All(c=>c.Id!=ActiveChannel))ActiveChannel=Config.ChatActiveChannelId;
        HistoryRevision++;
        Interface.SavePluginConfig(Config);
        if (!Config.Enabled || !Config.ShowChat || !Config.HideOriginalChat || !Config.ShowOriginalUi) originalChat.SetDesired(false);
        if (!Config.Enabled || Config.ShowOriginalUi) GameUi.SetHide(false);
    }
    internal void Reset()
    {
        Config = new Configuration();
        InstanceConfig = Config;
        ActiveChannel = Config.Channels[0].Id;
        Unread.Clear();
        ui.DisposeAssets();
        ui.ResetChatLayout();
        Save();
    }

    private void Draw()
    {
        Metrics.BeginFrame();
        lastPluginDraw=System.Diagnostics.Stopwatch.GetTimestamp();
        var loggedIn = Objects.LocalPlayer is not null;
        if (wasLoggedIn && !loggedIn)
        {
            History.Clear(); pendingChat.Clear(); Unread.Clear(); LatestChatError="";ui.ClearInput();
        }
        wasLoggedIn = loggedIn;
        var drawn = false;
        try { using(Metrics.Measure(UiWork.Icons)){Flat.Pump(loggedIn&&Config.Enabled);Buttons.Pump(loggedIn&&Config.Enabled);}using(Metrics.Measure(UiWork.Layouts))Hotbars.Update(loggedIn && Config.Enabled && !GameGui.GameUiHidden);ui.Draw(loggedIn); drawn = true; }
        finally
        {
            GameUi.Ready = drawn && Config.Enabled && !GameGui.GameUiHidden ? ui.Ready : HudReady.None;
            if(!drawn)Hotbars.Release();
            using(Metrics.Measure(UiWork.NativeScan))NativeStyle.Rendered(drawn && loggedIn && Config.Enabled && !GameGui.GameUiHidden);
            using var visibilityTiming=Metrics.Measure(UiWork.Visibility);
            var hideAll = drawn && loggedIn && Config.Enabled && !Config.ShowOriginalUi;
            if (hideAll)
            {
                originalChat.SetDesired(false);
                GameUi.SetHide(true, drawn && loggedIn && Config.Enabled);
            }
            else
            {
                GameUi.SetHide(false, drawn && loggedIn && Config.Enabled);
                originalChat.SetDesired(drawn && loggedIn && Config.Enabled && Config.ShowChat && Config.HideOriginalChat && ui.ChatVisible);
            }
            Metrics.EndFrame();
        }
    }

    // The API-15 distribution's event signature is validated during compilation.
    private void OnHandledChat(IChatMessage message){if(Config.ChatIncludeHandled)OnChat(message);}
    private void OnChat(IChatMessage message)
    {
        if(Objects.LocalPlayer is null)return;
        var timestamp=message.Timestamp>0?DateTimeOffset.FromUnixTimeSeconds(message.Timestamp).LocalDateTime:DateTime.Now;
        var person=message.Sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
        var maps=message.Message.Payloads.OfType<MapLinkPayload>().Select(m=>new ChatMapLink(m.TerritoryType.RowId,m.Map.RowId,m.RawX,m.RawY)).ToArray();
        var items=message.Message.Payloads.OfType<ItemPayload>().Select(i=>new ChatItemLink(i.ItemId,i.IsHQ)).ToArray();
        pendingChat.Enqueue(new ChatLine(Interlocked.Increment(ref chatSequence),(ushort)message.LogKind,message.Sender.TextValue,message.Message.TextValue,timestamp,person?.PlayerName??"",person?.World.RowId??0,maps,items));
        while(pendingChat.Count>Config.ChatHistoryLimit)pendingChat.TryDequeue(out _);
        // Copy owned strings/IDs only; leave the game's handled state unchanged.
    }

    public void Dispose()
    {
        Framework.Update -= OnUpdate;
        ui.DisposeAssets();
        Hotbars.Dispose();
        originalChat.Dispose();
        NativeStyle.Dispose();
        Redraw.Dispose();Buttons.Dispose();Flat.Dispose();
        GameUi.Dispose();
        Names.Dispose();
        Chat.ChatMessageUnhandled -= OnChat;
        Chat.ChatMessageHandled -= OnHandledChat;
        Interface.ActivePluginsChanged -= OnActivePluginsChanged;
        Interface.UiBuilder.Draw -= Draw;
        Interface.UiBuilder.OpenConfigUi -= OpenSettings;
        Interface.UiBuilder.OpenMainUi -= OpenSettings;
        Commands.RemoveHandler("/xlinui");
        History.Clear(); pendingChat.Clear(); Unread.Clear();
    }
}

internal sealed record ChatLine(long Id,ushort Type,string Sender,string Text,DateTime Time,string PlayerName,uint World,ChatMapLink[] Maps,ChatItemLink[] Items);
internal sealed record ChatMapLink(uint Territory,uint Map,int X,int Y);
internal sealed record ChatItemLink(uint Item,bool HighQuality);
