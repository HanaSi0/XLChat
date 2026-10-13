using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
namespace XLinUI;
internal sealed partial class UserInterface
{
    private static string ChatEllipsis(string text,float width)
    {
        if(width<=0)return "";
        if(ImGui.CalcTextSize(text).X<=width)return text;
        const string tail="…";
        if(ImGui.CalcTextSize(tail).X>width)return "";
        var low=0;var high=text.Length;
        while(low<high){var mid=(low+high+1)/2;if(ImGui.CalcTextSize(text[..mid]+tail).X<=width)low=mid;else high=mid-1;}
        if(low>0&&char.IsHighSurrogate(text[low-1]))low--;
        return text[..low]+tail;
    }
    private void DrawChatCategory(Channel channel)
    {
        var scale=ImGuiHelpers.GlobalScale;var active=plugin.ActiveChannel==channel.Id;
        var unread=C.ChatShowUnreadBadges?plugin.Unread.GetValueOrDefault(channel.Id):0;
        var start=ImGui.GetCursorScreenPos();
        var size=new Vector2(Math.Max(1,ImGui.GetContentRegionAvail().X),Math.Max(C.ChatSidebarItemHeight*scale,ImGui.GetFontSize()+12*scale));
        ImGui.PushID(channel.Id);
        try
        {
            if(ImGui.InvisibleButton("category",size))SelectChatChannel(channel);
            var hovered=ImGui.IsItemHovered();var draw=ImGui.GetWindowDrawList();
            var accent=channel.CustomColor?channel.Color:C.Tint;
            if(C.HighContrastText)accent=SurfacePolicy.ReadableColor(accent);
            if(active||hovered)draw.AddRectFilled(start,start+size,ImGui.ColorConvertFloat4ToU32(new Vector4(accent,active?.18f:.09f)),C.Rounded?5*scale:0);
            if(active)draw.AddRectFilled(start+new Vector2(0,5)*scale,start+new Vector2(3*scale,size.Y-5*scale),ImGui.ColorConvertFloat4ToU32(new Vector4(accent,1)));
            var badge=ChatChannelPolicy.Badge(unread);
            var badgeWidth=unread>0?ImGui.CalcTextSize(badge).X+12*scale:0;
            var label=ChatEllipsis(channel.Name,size.X-18*scale-badgeWidth-(unread>0?8*scale:0));
            ReadableText.Draw(draw,start+new Vector2(10*scale,(size.Y-ImGui.GetFontSize())/2),ImGui.GetColorU32(ImGuiCol.Text),label);
            if(unread>0)
            {
                var badgeHeight=ImGui.GetFontSize()+4*scale;
                var pos=start+new Vector2(size.X-badgeWidth-4*scale,(size.Y-badgeHeight)/2);
                draw.AddRectFilled(pos,pos+new Vector2(badgeWidth,badgeHeight),ImGui.ColorConvertFloat4ToU32(new Vector4(accent,.22f)),C.Rounded?badgeHeight/2:0);
                ReadableText.Draw(draw,pos+new Vector2(6,2)*scale,ImGui.ColorConvertFloat4ToU32(new Vector4(accent,1)),badge);
            }
            if(hovered)ImGui.SetTooltip(channel.Name+"\n"+ChatChannelPolicy.Summary(channel)+"\n右键查看分类菜单");
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
        finally{ImGui.PopID();}
    }
    private void DrawChatFilterSummary(Channel channel)
    {
        if(!C.ChatShowFilterSummary)return;
        var width=Math.Max(1,ImGui.GetContentRegionAvail().X);
        var title=channel.Name+" · "+ChatChannelPolicy.Summary(channel);
        ReadableText.TextColored(new Vector4(C.ForegroundColor*.8f,1),ChatEllipsis(title,width));
        if(ImGui.IsItemHovered())ImGui.SetTooltip(title+"\n接收筛选不改变发送目的地。");
        ImGui.Separator();
    }
}
