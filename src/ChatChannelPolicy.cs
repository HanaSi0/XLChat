using System;
using System.Collections.Generic;
using System.Linq;
namespace XLinUI;
internal sealed record ChatChannelGroup(string Key,string Name,ushort[] Types);
internal sealed record ChatChannelPreset(string Name,ushort[] Types,string Route,bool All=false);
internal static class ChatChannelPolicy
{
    internal static readonly ChatChannelGroup[] Groups=
    [
        new("public","附近与公共",[10,11,27,28,29,30]),
        new("party","小队与团队",[14,15,32,36]),
        new("tell","私信",[12,13]),
        new("company","部队",[24,69,70]),
        new("linkshell","通讯贝",[16,17,18,19,20,21,22,23]),
        new("cross","跨服通讯贝",[37,101,102,103,104,105,106,107]),
        new("system","系统与通知",[1,2,3,54,55,56,57,60,64,68,72,73,74,75,76,77,78,79]),
        new("activity","任务、掉落与生产",[59,61,62,65,66,67,71]),
        new("battle","战斗日志",[41,42,43,44,45,46,47,48,49,58]),
        new("gm","游戏管理消息",[80,81,82,83,84,85,86,87,88,89,90,91,92,93,94]),
    ];
    internal static readonly ChatChannelPreset[] Presets=
    [
        new("全部消息",[],"say",true),
        new("玩家交流",[10,11,12,13,14,15,16,17,18,19,20,21,22,23,24,27,28,29,30,32,36,37,101,102,103,104,105,106,107],"say"),
        new("小队与团队",[14,15,32,36],"party"),
        new("私信",[12,13],"tell"),
        new("部队与通讯贝",[16,17,18,19,20,21,22,23,24,37,69,70,101,102,103,104,105,106,107],"fc"),
        new("系统与活动",[2,3,54,55,56,57,59,60,61,62,64,65,66,67,68,69,70,71,72,73,74,75,76,77,78,79],"echo"),
    ];
    internal static bool All(Channel channel)=>!channel.MatchNone&&channel.Types.Count==0;
    internal static bool Selected(Channel channel,ushort type)=>!channel.MatchNone&&(All(channel)||channel.Types.Contains(type))&&!channel.ExcludedTypes.Contains(type);
    internal static void SetSelection(Channel channel,IEnumerable<ushort> types,bool selected,IReadOnlyCollection<ushort> catalog)
    {
        if(All(channel))channel.Types=catalog.Distinct().ToList();
        foreach(var type in types.Distinct())
        {
            if(selected){if(!channel.Types.Contains(type))channel.Types.Add(type);channel.ExcludedTypes.Remove(type);}
            else {channel.Types.Remove(type);channel.Relations.Remove(type);}
        }
        channel.MatchNone=channel.Types.Count==0;
    }
    internal static void Apply(Channel channel,ChatChannelPreset preset)
    {
        channel.Types=preset.Types.ToList();channel.MatchNone=false;channel.ExcludedTypes.Clear();channel.Relations.Clear();channel.SendRoute=preset.Route;
    }
    internal static void BlockBattle(Channel channel,bool block)
    {
        foreach(var type in Groups.First(g=>g.Key=="battle").Types)
            if(block){if(!channel.ExcludedTypes.Contains(type))channel.ExcludedTypes.Add(type);}else channel.ExcludedTypes.Remove(type);
    }
    internal static string Summary(Channel channel)
    {
        if(channel.MatchNone)return "未选择接收频道";
        var basis=All(channel)?"全部频道":$"{channel.Types.Count} 个频道";
        return basis+(channel.ExcludedTypes.Count>0?$" · 屏蔽 {channel.ExcludedTypes.Count} 项":"")+(channel.Relations.Count>0?" · 关系过滤":"");
    }
    internal static string Badge(int unread)=>unread>99?"99+":Math.Max(0,unread).ToString();
}
