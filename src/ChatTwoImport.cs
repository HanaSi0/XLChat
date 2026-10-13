using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Dalamud.Game.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace XLinUI;
public sealed class ChatRelationFilter
{
    public uint Source { get; set; }
    public uint Target { get; set; }
}
internal static class ChatFilter
{
    internal static bool Accepts(Channel channel,ushort type,int source,int target)
    {
        if(channel.MatchNone||(channel.Types.Count>0&&!channel.Types.Contains(type)))return false;
        if(!channel.Relations.TryGetValue(type,out var rule))return true;
        // Chat Two accepts system messages (source 0), or either matching relation.
        return source==0 || (source is >=0 and <32 && (rule.Source&(1u<<source))!=0) || (target is >=0 and <32 && (rule.Target&(1u<<target))!=0);
    }
}
internal sealed class ChatTwoImport
{
    internal List<Channel> Channels { get; }=[];
    internal List<string> Warnings { get; }=[];
    private static readonly Dictionary<string,ushort> Types=new(StringComparer.OrdinalIgnoreCase){{"Debug",1},{"Urgent",2},{"Notice",3},{"Say",10},{"Shout",11},{"TellOutgoing",12},{"TellIncoming",13},{"Party",14},{"Alliance",15},{"Linkshell1",16},{"Linkshell2",17},{"Linkshell3",18},{"Linkshell4",19},{"Linkshell5",20},{"Linkshell6",21},{"Linkshell7",22},{"Linkshell8",23},{"FreeCompany",24},{"NoviceNetwork",27},{"CustomEmote",28},{"StandardEmote",29},{"Yell",30},{"CrossParty",32},{"PvpTeam",36},{"CrossLinkshell1",37},{"Damage",41},{"Miss",42},{"Action",43},{"Item",44},{"Healing",45},{"GainBuff",46},{"GainDebuff",47},{"LoseBuff",48},{"LoseDebuff",49},{"GlamourNotifications",54},{"Alarm",55},{"Echo",56},{"System",57},{"BattleSystem",58},{"GatheringSystem",59},{"Error",60},{"NpcDialogue",61},{"LootNotice",62},{"Progress",64},{"LootRoll",65},{"Crafting",66},{"Gathering",67},{"NpcAnnouncement",68},{"FreeCompanyAnnouncement",69},{"FreeCompanyLoginLogout",70},{"RetainerSale",71},{"PeriodicRecruitmentNotification",72},{"Sign",73},{"RandomNumber",74},{"NoviceNetworkSystem",75},{"Orchestrion",76},{"PvpTeamAnnouncement",77},{"PvpTeamLoginLogout",78},{"MessageBook",79},{"GmTell",80},{"GmSay",81},{"GmShout",82},{"GmYell",83},{"GmParty",84},{"GmFreeCompany",85},{"GmLinkshell1",86},{"GmLinkshell2",87},{"GmLinkshell3",88},{"GmLinkshell4",89},{"GmLinkshell5",90},{"GmLinkshell6",91},{"GmLinkshell7",92},{"GmLinkshell8",93},{"GmNoviceNetwork",94},{"CrossLinkshell2",101},{"CrossLinkshell3",102},{"CrossLinkshell4",103},{"CrossLinkshell5",104},{"CrossLinkshell6",105},{"CrossLinkshell7",106},{"CrossLinkshell8",107}};
    internal static ChatTwoImport Read(string path)
    {
        var file=new FileInfo(path);
        if(!file.Exists)throw new IOException("未找到 ChatTwo.json，请先在 Chat Two 中保存设置。");
        if(file.Length>2*1024*1024)throw new IOException("设置文件超过 2 MB。");
        return Parse(File.ReadAllText(path));
    }
    internal static ChatTwoImport Parse(string json)
    {
        if(json.Length>2*1024*1024)throw new JsonException("设置文件过大。");
        using var text=new StringReader(json);
        using var reader=new JsonTextReader(text){MaxDepth=48,DateParseHandling=DateParseHandling.None};
        var root=JObject.Load(reader);
        if(reader.Read())throw new JsonException("设置文件包含多余内容。");
        if(root["Tabs"] is not JArray tabs)throw new JsonException("没有找到 Chat Two 的 Tabs 设置。");
        if(tabs.Count>64)throw new JsonException("分类超过 64 个，请减少分类后再导入。");
        var result=new ChatTwoImport();
        foreach(var tab in tabs.OfType<JObject>())
        {
            if(tab.Value<bool?>("IsTempTab")==true)continue;
            if(tab["SelectedChannels"] is not JObject selected){result.Warnings.Add("跳过缺少频道过滤的分类。");continue;}
            var name=(tab.Value<string>("Name")??"Chat Two").Trim();
            name=new string(name.Where(c=>!char.IsControl(c)).Take(80).ToArray());
            var channel=new Channel(name.Length==0?"Chat Two":name,[]){MatchNone=true};
            foreach(var property in selected.Properties())
            {
                if(!Types.TryGetValue(property.Name,out var type)&&!ushort.TryParse(property.Name,out type)){result.Warnings.Add($"{name}：忽略未知频道 {property.Name}。");continue;}
                if(type>=1000){result.Warnings.Add($"{name}：不导入 ExtraChat 自定义消息源。");continue;}
                if(property.Value is not JObject pair)throw new JsonException("频道关系过滤格式不兼容。");
                var source=Mask(pair["Item1"]);var target=Mask(pair["Item2"]);
                channel.Types.Add(type);channel.Relations[type]=new(){Source=source,Target=target};
            }
            channel.MatchNone=channel.Types.Count==0;
            channel.SendRoute=Route(tab["Channel"],channel.Types);
            result.Channels.Add(channel);
            if(tab.Value<bool?>("PopOut")==true)result.Warnings.Add($"{name}：导入为普通分类，不建立独立弹出窗口。");
            if(tab.Value<bool?>("ExtraChatAll")==true||tab["ExtraChatChannels"] is JArray{Count:>0})result.Warnings.Add($"{name}：ExtraChat 消息仍由原插件处理。");
        }
        return result;
    }
    private static uint Mask(JToken? token)
    {
        if(token is null)throw new JsonException("缺少频道关系过滤。");
        if(uint.TryParse(token.ToString(),out var value))return value;
        if(token.ToString()=="None")return 0;
        uint mask=0;
        foreach(var part in token.ToString().Split(','))
        {
            if(!Enum.TryParse<XivChatRelationKind>(part.Trim(),out var relation)||(int)relation is <0 or >=32)throw new JsonException("未识别的频道关系过滤。");
            mask|=1u<<(int)relation;
        }
        return mask;
    }
    private static string Route(JToken? token,List<ushort> types)
    {
        var value=token?.ToString()??"";
        if(!int.TryParse(value,out var id))
        {
            id=value switch{"Tell"=>0,"Say"=>1,"Party"=>2,"Alliance"=>3,"Yell"=>4,"Shout"=>5,"FreeCompany"=>6,"NoviceNetwork"=>8,_=>-1};
            for(var i=1;i<=8;i++){if(value=="CrossLinkshell"+i)id=8+i;if(value=="Linkshell"+i)id=18+i;}
        }
        return id switch{0=>"tell",1=>"say",2=>"party",3=>"alliance",4=>"yell",5=>"shout",6=>"fc",8=>"novice",>=9 and <=16=>"cwl"+(id-8),>=19 and <=26=>"ls"+(id-18),_=>ChatPolicy.DefaultRoute(types)};
    }
}
