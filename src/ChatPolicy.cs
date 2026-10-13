using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace XLinUI;
public enum ChatNavigation { Left, Right, Tabs, Hidden }
public enum ChatPresentation { Grouped, Compact }
public enum ChatBackground { Global, Transparent, Custom }
internal sealed record ChatRoute(string Key, string Label, string Command);
internal static class ChatPolicy
{
    internal static readonly List<ChatRoute> Routes = BuildRoutes();
    private static List<ChatRoute> BuildRoutes()
    {
        var rows = new List<ChatRoute> { new("say","说话","/say"),new("party","小队","/party"),new("alliance","团队","/alliance"),new("fc","部队","/fc"),new("yell","喊话","/yell"),new("shout","呼喊","/shout"),new("tell","私信","/tell"),new("echo","回声（仅自己）","/echo"),new("novice","新人频道","/novicenetwork") };
        for(var i=1;i<=8;i++) rows.Add(new($"ls{i}",$"通讯贝 {i}",$"/linkshell{i}"));
        for(var i=1;i<=8;i++) rows.Add(new($"cwl{i}",$"跨服通讯贝 {i}",$"/cwlinkshell{i}"));
        return rows;
    }
    internal static bool KnownRoute(string key) => Routes.Any(r=>r.Key==key);
    internal static bool TryBuild(string route, string recipient, string message, out string entry, out string error)
    {
        entry="";error="";
        var target=Routes.FirstOrDefault(r=>r.Key==route);
        if(target is null){error="未识别的发送频道。";return false;}
        var text=message.Trim();
        if(text.Length==0){error="消息为空。";return false;}
        if(text.Any(char.IsControl)){error="消息须为单行，不能包含控制字符。";return false;}
        var prefix=target.Command;
        if(route=="tell")
        {
            recipient=recipient.Trim();
            if(recipient.Count(c=>c=='@')!=1 || recipient.Split('@').Any(s=>string.IsNullOrWhiteSpace(s)) || recipient.Any(c=>char.IsControl(c)||"/\\\";<>".Contains(c)))
            {error="私信收件人须为 玩家名@世界名。";return false;}
            prefix+=" "+recipient;
        }
        entry=prefix+" "+text;
        if(Encoding.UTF8.GetByteCount(entry)>450){entry="";error="完整发送内容超过 450 个 UTF-8 字节，请缩短消息。";return false;}
        return true;
    }
    internal static string DefaultRoute(IReadOnlyCollection<ushort> types) => types.Contains((ushort)14)?"party":types.Contains((ushort)24)?"fc":types.Contains((ushort)13)?"tell":types.Contains((ushort)57)?"echo":"say";
    internal static bool Matches(ushort kind,string sender,string text,IReadOnlyCollection<ushort> types,string search) =>
        (types.Count==0||types.Contains(kind))&&(search.Length==0||sender.Contains(search,StringComparison.OrdinalIgnoreCase)||text.Contains(search,StringComparison.OrdinalIgnoreCase));
}
internal readonly record struct ChatRegions(float Sidebar, float Messages, float Composer, float Feed, float Splitter);
internal static class ChatGeometry
{
    internal static ChatRegions Compute(float width,float height,float desiredSidebar,float desiredComposer,float scale, bool side)
    {
        width=Math.Max(1,width);height=Math.Max(1,height);scale=Math.Clamp(scale,.6f,3);
        var split=side?6*scale:0;
        var sidebar=side?Math.Clamp(desiredSidebar*scale,Math.Min(40*scale,width*.25f),Math.Max(40*scale,width-180*scale-split)):0;
        sidebar=Math.Min(sidebar,width*.55f);
        var composer=Math.Clamp(desiredComposer*scale,Math.Min(60*scale,height*.45f),Math.Max(60*scale,height-50*scale));
        composer=Math.Min(composer,height*.65f);
        return new ChatRegions(sidebar,Math.Max(1,width-sidebar-split),composer,Math.Max(1,height-composer-16*scale),split);
    }
}
