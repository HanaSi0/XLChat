using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Text;
namespace XLinUI;
internal sealed partial class UserInterface
{
    private readonly Dictionary<string,string> channelFilterSearch=[];
    private readonly Dictionary<string,int> channelFilterPresets=[];
    private static readonly string[] channelPresetNames=ChatChannelPolicy.Presets.Select(p=>p.Name).ToArray();
    private static readonly ushort[] nativeChatTypes=Enum.GetValues<XivChatType>().Select(t=>(ushort)t).Where(t=>t>0).Distinct().Order().ToArray();
    private void FilterChanged(Channel channel,ref bool changed)
    {
        chatCacheRevision=-1;changed=true;
        if(C.ChatAutoSendRoute&&plugin.ActiveChannel==channel.Id)chatRoute=channel.SendRoute;
    }
    private void DrawSmartChannelFilter(Channel channel,ref bool changed)
    {
        ImGui.Separator();
        ReadableText.TextDisabled("接收筛选 · "+ChatChannelPolicy.Summary(channel));
        var preset=channelFilterPresets.GetValueOrDefault(channel.Id);
        if(ImGui.Combo("快捷方案",ref preset,channelPresetNames,channelPresetNames.Length))channelFilterPresets[channel.Id]=preset;
        ReadableText.TextDisabled("应用方案会设置接收筛选和默认发送频道，并清除已有关系限制。");
        if(ReadableText.SmallButton("应用方案（替换当前筛选）"))
        {ChatChannelPolicy.Apply(channel,ChatChannelPolicy.Presets[preset]);FilterChanged(channel,ref changed);}
        var all=ChatChannelPolicy.All(channel);
        if(ReadableText.Checkbox("接收全部频道",ref all))
        {channel.Types.Clear();channel.Relations.Clear();channel.ExcludedTypes.Clear();channel.MatchNone=!all;FilterChanged(channel,ref changed);}
        var battle=ChatChannelPolicy.Groups.First(g=>g.Key=="battle").Types;
        var blocked=battle.All(channel.ExcludedTypes.Contains);
        if(ReadableText.Checkbox("屏蔽战斗刷屏（伤害、技能、状态日志）",ref blocked))
        {ChatChannelPolicy.BlockBattle(channel,blocked);FilterChanged(channel,ref changed);}
        var query=channelFilterSearch.GetValueOrDefault(channel.Id,"");
        ImGui.SetNextItemWidth(-1);
        if(ImGui.InputTextWithHint("##filter-search","搜索频道名或编号…",ref query,128))channelFilterSearch[channel.Id]=query;
        query=query.Trim();
        if(query.Length>0)ReadableText.TextDisabled("分组勾选只影响当前搜索结果。");
        var catalog=nativeChatTypes.Concat(channel.Types).Distinct().Order().ToArray();
        var assigned=ChatChannelPolicy.Groups.SelectMany(g=>g.Types).ToHashSet();
        var groups=ChatChannelPolicy.Groups.Append(new ChatChannelGroup("other","其他频道",catalog.Where(t=>!assigned.Contains(t)).ToArray()));
        var found=false;
        foreach(var group in groups)
        {
            var shown=group.Types.Where(t=>catalog.Contains(t)&&(query.Length==0||group.Name.Contains(query,StringComparison.OrdinalIgnoreCase)||ChannelName(t).Contains(query,StringComparison.OrdinalIgnoreCase)||t.ToString().Contains(query,StringComparison.Ordinal))).ToArray();
            if(shown.Length==0)continue;
            found=true;ImGui.PushID(group.Key);
            try
            {
                var count=shown.Count(t=>ChatChannelPolicy.Selected(channel,t));var selected=count==shown.Length;
                var toggle=ReadableText.Checkbox($"##group",ref selected);
                if(count>0&&count<shown.Length&&!toggle)
                {var p=ImGui.GetItemRectMin();var h=ImGui.GetFrameHeight();ImGui.GetWindowDrawList().AddLine(p+new System.Numerics.Vector2(h*.25f,h*.5f),p+new System.Numerics.Vector2(h*.75f,h*.5f),ImGui.GetColorU32(ImGuiCol.Text),2);}
                if(toggle){ChatChannelPolicy.SetSelection(channel,shown,selected,catalog);FilterChanged(channel,ref changed);}
                ImGui.SameLine();
                var flags=query.Length>0?ImGuiTreeNodeFlags.DefaultOpen:ImGuiTreeNodeFlags.None;
                if(query.Length>0)ImGui.SetNextItemOpen(true,ImGuiCond.Always);
                if(ImGui.TreeNodeEx($"{group.Name} ({count}/{shown.Length})###group-details",flags))
                {
                    foreach(var type in shown)
                    {
                        var enabled=ChatChannelPolicy.Selected(channel,type);
                        if(ReadableText.Checkbox(ChannelName(type)+$"##kind-{type}",ref enabled))
                        {ChatChannelPolicy.SetSelection(channel,[type],enabled,catalog);FilterChanged(channel,ref changed);}
                        if(ImGui.IsItemHovered())ImGui.SetTooltip($"频道编号：{type}"+(channel.Relations.ContainsKey(type)?"\n保留了 Chat Two 关系过滤":""));
                    }
                    ImGui.TreePop();
                }
            }
            finally{ImGui.PopID();}
        }
        if(!found)ReadableText.TextDisabled("没有匹配频道，可按编号搜索。");
        if(channel.Relations.Count>0)
        {
            ReadableText.TextWrapped("已保留 Chat Two 的发送者／目标关系过滤。修改分组不清除已有关系；取消某频道再勾选可移除其关系过滤。");
            if(ReadableText.SmallButton("清除关系限制（保留频道）")){channel.Relations.Clear();FilterChanged(channel,ref changed);}
        }
    }
}
