using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Utility;
using Lumina.Excel.Sheets;

namespace XLinUI;
internal sealed partial class UserInterface
{
    internal bool ChatAtBottom { get; private set; }
    private string chatRoute {get=>C.ChatSendRoute;set=>C.ChatSendRoute=value;}
    private string tellRecipient="";
    private readonly Dictionary<string,string> channelDrafts=[];
    private readonly List<ChatLine> filteredChat=[];
    private int chatCacheRevision=-1;
    private string chatCacheChannel="",chatCacheSearch="";
    private string profileName="我的布局";
    private string profileSelection="";
    private bool applyChatPosition;
    private bool composerUsable;
    private DateTime chatSubmitted;
    private DateTime noviceJoinSubmitted;
    private int expandedChatRows;
    private Dictionary<ushort,string>? chatLabels;
    private readonly Dictionary<long,(string Time,string Sender,string Prefix,string Stamp)> renderedChatText=[];
    private readonly Dictionary<long,(float Height,bool Grouped,bool HasPrevious)> chatRowHeights=[];
    private (bool Seconds,bool Time,bool Channel,ChatPresentation Presentation,float Header,float Stamp,float Space,float GroupSpace,bool Groups,string FontFile,int FontFace,float FontSize,bool CustomFont) chatTextKey;
    private (float Width,float Font,float Scale) chatHeightKey;
    private static readonly string[] sendRouteLabels=ChatPolicy.Routes.Select(r=>r.Label).ToArray();

    internal void ResetChatLayout(){applyChatSize=true;applyChatPosition=true;importedLayouts.Remove("chat");}
    private string ChannelName(ushort type)
    {
        chatLabels??=Plugin.Data.GetExcelSheet<LogFilter>().Where(r=>r.LogKind>0&&!string.IsNullOrWhiteSpace(r.Name.ToString())).GroupBy(r=>(ushort)r.LogKind).ToDictionary(g=>g.Key,g=>g.First().Name.ToString());
        return chatLabels.GetValueOrDefault(type,((Dalamud.Game.Text.XivChatType)type).ToString());
    }
    private void SelectChatChannel(Channel channel)
    {
        channelDrafts[plugin.ActiveChannel]=draft;
        plugin.ActiveChannel=channel.Id;C.ChatActiveChannelId=channel.Id;draft=channelDrafts.GetValueOrDefault(channel.Id,"");
        if(C.ChatAutoSendRoute)chatRoute=channel.SendRoute;
        lastCount=-1;chatCacheRevision=-1;expandedChatRows=0;plugin.Save();
    }
    private void DrawChat()
    {
        var scale=ImGuiHelpers.GlobalScale;
        var viewport=ImGuiHelpers.MainViewport;
        var requested=new Vector2(C.ChatWidth,C.ChatHeight)*scale;
        ImGui.SetNextWindowSize(requested,applyChatSize?ImGuiCond.Always:ImGuiCond.FirstUseEver);applyChatSize=false;
        ImGui.SetNextWindowSizeConstraints(new Vector2(320,200)*scale,Vector2.Max(new Vector2(320,200)*scale,viewport.Size));
        if(C.ChatHasPosition)
        {
            var p=Vector2.Clamp(C.ChatPosition*scale,Vector2.Zero,Vector2.Max(Vector2.Zero,viewport.Size-requested));
            ImGui.SetNextWindowPos(viewport.Pos+p,applyChatPosition?ImGuiCond.Always:ImGuiCond.FirstUseEver);
        }
        else { ImGui.SetNextWindowPos(viewport.Pos+new Vector2(30,150)*scale,applyChatPosition?ImGuiCond.Always:ImGuiCond.FirstUseEver);ImportLayout("chat","ChatLog"); }
        applyChatPosition=false;
        var alpha=C.ChatBackground switch {ChatBackground.Transparent=>0,ChatBackground.Custom=>C.ChatBackgroundOpacity,_=>C.Material!=0?0:SurfacePolicy.PanelAlpha(C.ForceTransparentPanels,C.TextOnly,C.Glass,C.Opacity)};
        ImGui.PushStyleColor(ImGuiCol.WindowBg,new Vector4(C.BackgroundColor,alpha));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,new Vector2(C.ChatPadding)*scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing,new Vector2(6,5)*scale);
        ImGui.PushStyleVar(ImGuiStyleVar.ScrollbarSize,C.ChatScrollbarWidth*scale);
        var open=C.ShowChat;
        var visible=ImGui.Begin("聊天###XLinUIChat",ref open,WindowFlags|ImGuiWindowFlags.NoScrollbar);
        try
        {
            if(!visible||!open){ChatAtBottom=false;return;}
            if(C.ChatBackground==ChatBackground.Global)Appearance.DrawBackground();
            ImGui.SetWindowFontScale(C.ChatFontScale);
            if(C.Channels.All(c=>c.Id!=plugin.ActiveChannel))SelectChatChannel(C.Channels[0]);
            var channel=C.Channels.First(c=>c.Id==plugin.ActiveChannel);
            var actualSize=ImGui.GetWindowSize()/scale;var actualPos=(ImGui.GetWindowPos()-viewport.Pos)/scale;
            if(!C.LockWindows&&(!C.ChatHasPosition||Vector2.Distance(new Vector2(C.ChatWidth,C.ChatHeight),actualSize)>1||Vector2.Distance(C.ChatPosition,actualPos)>1))
            {C.ChatWidth=actualSize.X;C.ChatHeight=actualSize.Y;C.ChatPosition=actualPos;C.ChatHasPosition=true;chatSizeChanged=true;}
            if(chatSizeChanged&&!ImGui.IsMouseDown(ImGuiMouseButton.Left)){chatSizeChanged=false;plugin.Save();}
            if(C.ChatNavigation==ChatNavigation.Tabs)DrawChatTabs();
            if(C.ChatNavigation==ChatNavigation.Hidden)
            {
                var current=C.Channels.IndexOf(channel);var names=C.Channels.Select(c=>c.Name).ToArray();
                ImGui.SetNextItemWidth(Math.Min(220*scale,ImGui.GetContentRegionAvail().X));
                if(ImGui.Combo("##view-channel",ref current,names,names.Length))SelectChatChannel(C.Channels[current]);
                ImGui.SameLine();if(ReadableText.SmallButton("设置"))SettingsOpen=true;
            }
            var side=C.ChatNavigation is ChatNavigation.Left or ChatNavigation.Right;
            var avail=ImGui.GetContentRegionAvail();
            var regions=ChatGeometry.Compute(avail.X,avail.Y,C.ChatSidebarWidth,C.ChatComposerHeight,scale,side);
            if(C.ChatNavigation==ChatNavigation.Left)
            {
                DrawChatNavigation(regions.Sidebar,avail.Y);ImGui.SameLine(0,0);
                ChatVerticalSplit(regions,avail.Y,false);ImGui.SameLine(0,0);
            }
            var content=ImGui.BeginChild("chat-conversation",new Vector2(regions.Messages,avail.Y),false);
            try { if(content)DrawConversation();else ChatAtBottom=false; }
            finally {ImGui.EndChild();}
            if(C.ChatNavigation==ChatNavigation.Right)
            {
                ImGui.SameLine(0,0);ChatVerticalSplit(regions,avail.Y,true);ImGui.SameLine(0,0);
                DrawChatNavigation(regions.Sidebar,avail.Y);
            }
        }
        finally
        {
            EndPanel();ImGui.PopStyleVar(3);ImGui.PopStyleColor();
            if(C.ShowChat!=open){C.ShowChat=open;plugin.Save();ChatVisible=false;}
        }
    }
    private void DrawChatNavigation(float width,float height)
    {
        var visible=ImGui.BeginChild("chat-navigation",new Vector2(width,height),false);
        try
        {
            if(!visible)return;
            ImGui.SetWindowFontScale(C.ChatFontScale*C.ChatSidebarFontScale);
            foreach(var channel in C.Channels)
            {
                var unread=plugin.Unread.GetValueOrDefault(channel.Id);
                if(ReadableText.Selectable(channel.Name+(unread>0?$" · {unread}":"")+"###"+channel.Id,plugin.ActiveChannel==channel.Id,ImGuiSelectableFlags.None,new Vector2(0,C.ChatSidebarItemHeight*ImGuiHelpers.GlobalScale)))SelectChatChannel(channel);
                if(ImGui.IsItemHovered())ImGui.SetTooltip(channel.Name+"\n右键查看分类菜单");
                if(ImGui.BeginPopupContextItem("category-menu"))
                {
                    try
                    {
                        if(ReadableText.Selectable("标为已读"))plugin.Unread[channel.Id]=0;
                        if(ReadableText.Selectable("编辑分类／布局"))SettingsOpen=true;
                    }
                    finally{ImGui.EndPopup();}
                }
            }
            ImGui.Separator();if(ReadableText.SmallButton("聊天设置"))SettingsOpen=true;
        }
        finally{ImGui.EndChild();}
    }
    private void DrawChatTabs()
    {
        if(!ImGui.BeginTabBar("ChatCategories"))return;
        try
        {
            foreach(var channel in C.Channels)
            {
                var unread=plugin.Unread.GetValueOrDefault(channel.Id);
                if(ImGui.BeginTabItem(channel.Name+(unread>0?$" ({unread})":"")+"###"+channel.Id))
                {try{if(plugin.ActiveChannel!=channel.Id)SelectChatChannel(channel);}finally{ImGui.EndTabItem();}}
            }
        }
        finally{ImGui.EndTabBar();}
    }
    private void ChatVerticalSplit(ChatRegions regions,float height,bool right)
    {
        var pos=ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##chat-width-split",new Vector2(regions.Splitter,Math.Max(1,height)));
        if(ImGui.IsItemHovered())ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw);
        if(!C.LockWindows&&ImGui.IsItemActive()&&ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {C.ChatSidebarWidth=Math.Clamp(regions.Sidebar/ImGuiHelpers.GlobalScale+ImGui.GetIO().MouseDelta.X/ImGuiHelpers.GlobalScale*(right?-1:1),40,600);chatSizeChanged=true;}
        if(ImGui.IsItemHovered()||ImGui.IsItemActive())ImGui.GetWindowDrawList().AddLine(pos+new Vector2(regions.Splitter/2,0),pos+new Vector2(regions.Splitter/2,height),0xAAE1E8EB);
    }
    private void DrawConversation()
    {
        ImGui.SetWindowFontScale(C.ChatFontScale);
        var scale=ImGuiHelpers.GlobalScale;
        var channel=C.Channels.First(c=>c.Id==plugin.ActiveChannel);
        if(C.ChatShowSearch)
        {
            ImGui.SetNextItemWidth(C.ChatSearchWidth>0?Math.Min(C.ChatSearchWidth*scale,ImGui.GetContentRegionAvail().X):-1);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,new Vector2(5,Math.Max(2,(C.ChatSearchHeight*scale-ImGui.GetFontSize())/2)));
            try{if(ImGui.InputTextWithHint("##chat-search","搜索发言人或消息",ref search,128))lastCount=-1;}
            finally{ImGui.PopStyleVar();}
        }
        var avail=ImGui.GetContentRegionAvail();
        var regions=ChatGeometry.Compute(avail.X,avail.Y,0,C.ChatComposerHeight,scale,false);
        var messages=ImGui.BeginChild("chat-feed",new Vector2(0,regions.Feed),false);
        try{if(messages)DrawChatMessages(channel);else ChatAtBottom=false;}
        finally{ImGui.EndChild();}
        var splitPos=ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton("##chat-input-split",new Vector2(Math.Max(1,ImGui.GetContentRegionAvail().X),6*scale));
        if(ImGui.IsItemHovered())ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNs);
        if(!C.LockWindows&&ImGui.IsItemActive()&&ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {C.ChatComposerHeight=Math.Clamp(regions.Composer/scale-ImGui.GetIO().MouseDelta.Y/scale,60,300);chatSizeChanged=true;}
        if(ImGui.IsItemHovered()||ImGui.IsItemActive())ImGui.GetWindowDrawList().AddLine(splitPos,splitPos+new Vector2(avail.X,0),0xAAE1E8EB);
        composerUsable=false;
        var composer=ImGui.BeginChild("chat-composer",Vector2.Zero,false);
        try{if(composer)DrawChatComposer();}
        finally{ImGui.EndChild();}
        ChatVisible=messages&&composer&&composerUsable;
    }
    private void DrawChatMessages(Channel channel)
    {
        ImGui.SetWindowFontScale(C.ChatFontScale);
        var wasBottom=ImGui.GetScrollY()>=ImGui.GetScrollMaxY()-8;
        if(chatCacheRevision!=plugin.HistoryRevision||chatCacheChannel!=channel.Id||chatCacheSearch!=search)
        {
            if(chatCacheChannel!=channel.Id||chatCacheSearch!=search)expandedChatRows=0;
            filteredChat.Clear();filteredChat.AddRange(plugin.History.Where(m=>ChatPolicy.Matches(m.Type,m.Sender,m.Text,channel.Types,search)));
            chatCacheRevision=plugin.HistoryRevision;chatCacheChannel=channel.Id;chatCacheSearch=search;
        }
        if(filteredChat.Count==0)ReadableText.TextDisabled(search.Length==0?"等待新消息…":"没有匹配的消息");
        var jumpLatest=false;var openedEarlier=false;
        if(filteredChat.Count>C.ChatPageSize)
        {
            if(ReadableText.SmallButton("加载更早")){expandedChatRows=Math.Min(filteredChat.Count,expandedChatRows+C.ChatPageSize);openedEarlier=true;}
            ImGui.SameLine();if(ReadableText.SmallButton("最新")){expandedChatRows=0;jumpLatest=true;}
            ReadableText.TextDisabled($"筛选结果 {filteredChat.Count} 条 · 当前展开 {Math.Min(filteredChat.Count,C.ChatPageSize+expandedChatRows)} 条");
        }
        var textKey=(C.ChatTimestampSeconds,C.ChatShowTimestamp,C.ChatShowChannelName,C.ChatPresentation,C.ChatHeaderFontScale,C.ChatTimestampFontScale,C.ChatLineSpacing,C.ChatGroupSpacing,C.ChatGroupMessages,C.FontFile,C.FontFace,C.FontSize,C.UseCustomFont);
        if(chatTextKey!=textKey||renderedChatText.Count>C.ChatHistoryLimit){chatTextKey=textKey;renderedChatText.Clear();chatRowHeights.Clear();}
        ChatLine? previous=null;
        var width=C.ChatMessageWidth>0?Math.Min(C.ChatMessageWidth*ImGuiHelpers.GlobalScale,ImGui.GetContentRegionAvail().X):ImGui.GetContentRegionAvail().X;
        var heightKey=(width,ImGui.GetFontSize(),ImGuiHelpers.GlobalScale);
        if(chatHeightKey!=heightKey){chatHeightKey=heightKey;chatRowHeights.Clear();}
        var clipTop=ImGui.GetWindowPos().Y;var clipBottom=clipTop+ImGui.GetWindowSize().Y;
        for(var rowIndex=Math.Max(0,filteredChat.Count-C.ChatPageSize-expandedChatRows);rowIndex<filteredChat.Count;rowIndex++)
        {
            var line=filteredChat[rowIndex];var rowStart=ImGui.GetCursorScreenPos().Y;
            var grouped=C.ChatGroupMessages&&previous is not null&&previous.Sender==line.Sender&&previous.Type==line.Type&&(line.Time-previous.Time).TotalMinutes<2;
            if(chatRowHeights.TryGetValue(line.Id,out var measured)&&measured.Grouped==grouped&&measured.HasPrevious==(previous is not null)&&(rowStart+measured.Height<clipTop-8||rowStart>clipBottom+8))
            {ImGui.Dummy(new Vector2(1,measured.Height));previous=line;continue;}
            ImGui.PushID((int)(line.Id&int.MaxValue));
            try
            {
                if(!renderedChatText.TryGetValue(line.Id,out var formatted))
                {
                    var timestamp=line.Time.ToString(C.ChatTimestampSeconds?"HH:mm:ss":"HH:mm");
                    var author=string.IsNullOrWhiteSpace(line.Sender)?"系统":line.Sender;
                    formatted=(timestamp,author,(C.ChatShowTimestamp?timestamp+" ":"")+(C.ChatShowChannelName?$"[{ChannelName(line.Type)}] ":""),(C.ChatShowChannelName?ChannelName(line.Type):"")+(C.ChatShowTimestamp?(C.ChatShowChannelName?" · ":"")+timestamp:""));renderedChatText[line.Id]=formatted;
                }
                var time=formatted.Time;var sender=formatted.Sender;
                var color=channel.CustomColor?channel.Color:C.Tint;
                if(C.ChatPresentation==ChatPresentation.Compact)
                {
                    var prefix=formatted.Prefix;
                    ReadableText.TextWrapped(prefix+sender+"："+line.Text,width);
                }
                else
                {
                    if(!grouped)
                    {
                        if(previous is not null)ImGui.Dummy(new Vector2(0,C.ChatGroupSpacing*ImGuiHelpers.GlobalScale));
                        ImGui.SetWindowFontScale(C.ChatFontScale*C.ChatHeaderFontScale);
                        var senderWidth=ImGui.CalcTextSize(sender).X;
                        ReadableText.TextColoredWrapped(new Vector4(color,1),sender,width);
                        if(C.ChatShowTimestamp||C.ChatShowChannelName)
                        {
                            ImGui.SetWindowFontScale(C.ChatFontScale*C.ChatTimestampFontScale);
                            var stamp=formatted.Stamp;
                            if(senderWidth+ImGui.CalcTextSize(stamp).X+ImGui.GetStyle().ItemSpacing.X<=width)ImGui.SameLine();
                            ReadableText.TextWrapped(stamp,width);
                        }
                    }
                    ImGui.SetWindowFontScale(C.ChatFontScale);
                    ReadableText.TextWrapped(line.Text,width);
                }
                DrawMessageContext(line);
                if(C.ChatLineSpacing>0)ImGui.Dummy(new Vector2(0,C.ChatLineSpacing*ImGuiHelpers.GlobalScale));
                chatRowHeights[line.Id]=(Math.Max(1,ImGui.GetCursorScreenPos().Y-rowStart-ImGui.GetStyle().ItemSpacing.Y),grouped,previous is not null);
                previous=line;
            }
            finally{ImGui.PopID();}
        }
        var scrolling=jumpLatest||!openedEarlier&&C.ChatFollow&&(wasBottom||lastCount==-1)&&lastCount!=plugin.HistoryRevision;
        if(scrolling)ImGui.SetScrollHereY(1);
        lastCount=plugin.HistoryRevision;
        ChatAtBottom=search.Length==0&&(wasBottom||scrolling);
        if(ChatAtBottom)plugin.Unread[channel.Id]=0;
    }
    private void DrawMessageContext(ChatLine line)
    {
        if(!ImGui.BeginPopupContextItem("message-actions"))return;
        try
        {
            if(ReadableText.Selectable("显示原版聊天（完整链接交互）")){C.ShowOriginalUi=true;C.HideOriginalChat=false;plugin.Save();}
            if(ReadableText.Selectable("复制消息"))ImGui.SetClipboardText(line.Text);
            if(ReadableText.Selectable("复制发言人"))ImGui.SetClipboardText(line.Sender);
            if(ReadableText.Selectable("引用到输入框"))draft="「"+line.Text.Replace('\n',' ').Replace('\r',' ')+"」 ";
            if(line.PlayerName.Length>0&&line.World>0&&ReadableText.Selectable("私信此玩家"))
            {var world=Plugin.Data.GetExcelSheet<World>().GetRowOrDefault(line.World);tellRecipient=line.PlayerName+"@"+(world?.Name.ToString()??"");chatRoute="tell";plugin.Save();}
            for(var i=0;i<line.Maps.Length;i++)
            {
                var map=line.Maps[i];
                if(ReadableText.Selectable($"打开地图位置 {i+1}"))
                {C.ShowOriginalUi=true;plugin.Save();Native(()=>Plugin.GameGui.OpenMapWithMapLink(new MapLinkPayload(map.Territory,map.Map,map.X,map.Y)));}
            }
            foreach(var link in line.Items)
            {
                var item=Plugin.Data.GetExcelSheet<Item>().GetRowOrDefault(link.Item);
                ReadableText.TextUnformatted((item?.Name.ToString()??$"物品 #{link.Item}")+(link.HighQuality?" HQ":""));
                if(ImGui.IsItemHovered())ImGui.SetTooltip(item?.Description.ToString()??"完整物品交互请使用原版聊天。");
            }
        }
        finally{ImGui.EndPopup();}
    }
    private void DrawChatComposer()
    {
        ImGui.SetWindowFontScale(C.ChatFontScale);
        var scale=ImGuiHelpers.GlobalScale;
        var index=Math.Max(0,ChatPolicy.Routes.FindIndex(r=>r.Key==chatRoute));
        ImGui.SetNextItemWidth(Math.Min(C.ChatSelectorWidth*scale,ImGui.GetContentRegionAvail().X));
        if(ImGui.Combo("##send-route",ref index,sendRouteLabels,sendRouteLabels.Length)){chatRoute=ChatPolicy.Routes[index].Key;plugin.Save();}
        if(ImGui.GetContentRegionAvail().X>100*scale){ImGui.SameLine();var followNow=C.ChatFollow;if(ReadableText.Checkbox("跟随",ref followNow)){C.ChatFollow=followNow;plugin.Save();}}
        if(C.ChatShowNoviceJoin)DrawNoviceJoin();
        if(chatRoute=="tell"){ImGui.SetNextItemWidth(-1);ImGui.InputTextWithHint("##tell-recipient","玩家名@世界名",ref tellRecipient,100);}
        var total=ImGui.GetContentRegionAvail().X;var button=C.ChatShowSendButton?Math.Min(C.ChatSendButtonWidth*scale,Math.Max(30*scale,total*.3f)):0;
        var input=Math.Max(20,total-button-(C.ChatShowSendButton?ImGui.GetStyle().ItemSpacing.X:0));
        if(C.ChatInputWidth>0)input=Math.Min(input,C.ChatInputWidth*scale);
        var inputHeight=Math.Max(ImGui.GetFontSize()+4,C.ChatInputHeight*scale);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,new Vector2(5,Math.Max(2,(inputHeight-ImGui.GetFontSize())/2)));
        try
        {
            ImGui.SetNextItemWidth(input);
            var enter=ImGui.InputTextWithHint("##chat-draft","输入消息，Enter 发送",ref draft,1500,ImGuiInputTextFlags.EnterReturnsTrue);
            composerUsable=ImGui.IsItemVisible()&&ImGui.GetItemRectMax().Y<=ImGui.GetWindowPos().Y+ImGui.GetWindowSize().Y;
            var clicked=false;
            if(C.ChatShowSendButton){ImGui.SameLine();clicked=ReadableText.Button(button<ImGui.CalcTextSize("发送").X+8?"→":"发送",new Vector2(button,Math.Max(ImGui.GetFontSize()+4,C.ChatSendButtonHeight*scale)));}
            if(clicked||enter)SendChat();
        }
        finally{ImGui.PopStyleVar();}
        ReadableText.TextDisabled($"{Encoding.UTF8.GetByteCount(draft)} 字节 · {ChatPolicy.Routes[index].Label}"+(chatRoute=="tell"?" → "+tellRecipient:""));
        if(plugin.LatestChatErrorTime>=chatSubmitted&&chatSubmitted!=default&&(DateTime.Now-chatSubmitted).TotalSeconds<8&&plugin.LatestChatError.Length>0)ReadableText.TextWrapped("游戏提示："+plugin.LatestChatError);
        else if(notice.Length>0)ReadableText.TextWrapped(notice);
    }
    private void DrawNoviceJoin()
    {
        bool? joined;
        try{joined=GameBridge.InNoviceNetwork();}catch{joined=null;}
        var waiting=(DateTime.UtcNow-noviceJoinSubmitted).TotalSeconds<5;
        var label=joined==true?"已加入新手频道":waiting?"加入中…":"加入新手频道";
        ImGui.SetWindowFontScale(C.ChatFontScale*.85f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding,new Vector2(4,2));
        try
        {
            var width=ImGui.CalcTextSize(label).X+8;
            if(ImGui.GetItemRectMax().X+ImGui.GetStyle().ItemSpacing.X+width<=ImGui.GetWindowPos().X+ImGui.GetWindowSize().X-ImGui.GetStyle().WindowPadding.X)ImGui.SameLine();
            var disabled=joined is null||joined==true||waiting||!C.NativeInteractions;
            ImGui.BeginDisabled(disabled);
            try
            {
                if(ReadableText.SmallButton(label))
                {
                    var submitted=false;
                    Native(()=>submitted=GameBridge.JoinNoviceNetwork());
                    if(submitted)
                    {
                        noviceJoinSubmitted=DateTime.UtcNow;chatSubmitted=DateTime.Now;plugin.LatestChatError="";
                        notice="已提交加入新手频道请求；资格、频道人数及加入结果以游戏提示为准。";
                    }
                    else if(C.NativeInteractions)notice="游戏频道入口暂未就绪，或已加入新手频道。";
                }
            }
            finally{ImGui.EndDisabled();}
            if(ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip(joined==true?"已加入新手频道；发送频道可选择“新人频道”。":!C.NativeInteractions?"请先在模块设置中启用游戏交互。":joined is null?"游戏新手频道接口暂未就绪。":waiting?"加入请求已提交，请等待游戏提示。":"请求加入原版新手频道；资格与频道容量由游戏判断。可在聊天设置中隐藏此按钮。");
        }
        finally{ImGui.PopStyleVar();ImGui.SetWindowFontScale(C.ChatFontScale);}
    }
    private void SendChat()
    {
        if(!ChatPolicy.TryBuild(chatRoute,tellRecipient,draft,out _,out var error)){notice=error;return;}
        if(!C.NativeInteractions){notice="请在模块设置中启用游戏交互。";return;}
        var submitted=false;chatSubmitted=DateTime.Now;plugin.LatestChatError="";Native(()=>submitted=GameBridge.Send(chatRoute,tellRecipient,draft));
        if(submitted){draft="";channelDrafts[plugin.ActiveChannel]="";notice="已提交；送达和权限结果以游戏提示为准。";}
        else notice="游戏聊天入口暂未就绪。";
    }
}
