using System.Runtime.Loader;
using XLChat;
using XLChat.Code;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Newtonsoft.Json;
using Microsoft.Data.Sqlite;
var dalamudHome=Environment.GetEnvironmentVariable("DALAMUD_HOME")??throw new Exception("Set DALAMUD_HOME to the API 15 DLL directory.");
AssemblyLoadContext.Default.Resolving+=(context,name)=>File.Exists(Path.Combine(dalamudHome,name.Name+".dll"))?context.LoadFromAssemblyPath(Path.Combine(dalamudHome,name.Name+".dll")):null;
var configuration=new Configuration();
if(configuration.Version!=6||configuration.PrintChangelog||configuration.WebinterfaceAutoStart)throw new Exception("Fork defaults wrong");
configuration.Tabs.Add(new Tab{Name="亲友",SelectedChannels=new(){[ChatType.Party]=(ChatSource.PartyMember,ChatSource.PartyMember)}});
var restored=JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(configuration));
if(restored.Tabs.Count!=1||restored.Tabs[0].Name!="亲友")throw new Exception("Source config roundtrip failed");
Plugin.Config=configuration;
var code=new ChatCode(XivChatType.Party,XivChatRelationKind.PartyMember,XivChatRelationKind.OtherPlayer);
var message=new Message(Guid.NewGuid(),0,0,DateTimeOffset.UtcNow,code,[],[],new SeString(),new SeString(),Guid.Empty);
if(!message.Matches(configuration.Tabs[0].SelectedChannels,false,[])||message.Matches([],false,[]))throw new Exception("Source channel filter failed");
using var connection=new SqliteConnection("Data Source=:memory:");connection.Open();using var command=connection.CreateCommand();command.CommandText="SELECT sqlite_version()";Console.WriteLine("SQLite native dependency: "+command.ExecuteScalar());
Console.WriteLine("PASS: actual XLChat source config, tab filters and bundled SQLite runtime");
var appearance=new XLChat.Appearance.AppearanceOptions{Mode=XLChat.Appearance.BackgroundMode.Glass,ImagePath="picture.png",Blur=17};
configuration.Appearance=appearance;var mutable=new Configuration();mutable.UpdateFrom(configuration,false);mutable.Appearance.Blur=3;
if(configuration.Appearance.Blur!=17)throw new Exception("Appearance editing changed live config before save");
var copy=JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(configuration));
if(copy.Appearance.Mode!=XLChat.Appearance.BackgroundMode.Glass||copy.Appearance.ImagePath!="picture.png"||copy.Appearance.Blur!=17||!copy.EnsureChineseFont)throw new Exception("Appearance persistence failed");
var chinese="中文聊天字体修复龘㐀，。Ａ";
foreach(var ch in chinese)
{
 bool supported=false;var ranges=XLChat.Appearance.ChineseFontPolicy.Ranges;
 for(var i=0;i<ranges.Length-1;i+=2)if(ch>=ranges[i]&&ch<=ranges[i+1])supported=true;
 if(!supported)throw new Exception("Mandatory Chinese range missing "+ch);
}
var glass=XLChat.Appearance.BackgroundProcessor.Glass(12);
if(glass.Width!=256||glass.Height!=256||glass.Data.Length!=256*256*4)throw new Exception("Static glass size failed");
for(var i=3;i<glass.Data.Length;i+=4)if(glass.Data[i]!=255)throw new Exception("Glass alpha unexpectedly changed");
Console.WriteLine("PASS: appearance edit isolation/serialization, mandatory Chinese ranges, cached CPU glass generation");

var samples=new string[]{"iVBORw0KGgoAAAANSUhEUgAAAAIAAAACCAIAAAD91JpzAAAAFklEQVR4nGMMqFjAwMDAxMDAwMDAAAAQugFsZnyF3gAAAABJRU5ErkJggg==","/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAgGBgcGBQgHBwcJCQgKDBQNDAsLDBkSEw8UHRofHh0aHBwgJC4nICIsIxwcKDcpLDAxNDQ0Hyc5PTgyPC4zNDL/2wBDAQkJCQwLDBgNDRgyIRwhMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjIyMjL/wAARCAACAAIDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwChRRRXsHln/9k=","Qk1GAAAAAAAAADYAAAAoAAAAAgAAAAIAAAABABgAAAAAABAAAADEDgAAxA4AAAAAAAAAAAAAoHhQoHhQAACgeFCgeFAAAA=="};
foreach(var sample in samples)
{
 var pixels=XLChat.Appearance.BackgroundProcessor.Decode(Convert.FromBase64String(sample));
 if(pixels.Width!=2||pixels.Height!=2||pixels.Data.Length!=16)throw new Exception("PNG/JPEG/BMP background decode failed");
}
var path=Path.GetTempFileName();
try
{
 File.WriteAllBytes(path,Convert.FromBase64String(samples[0]));var processor=new XLChat.Appearance.BackgroundProcessor();
 var before=processor.Load(path,false,12,CancellationToken.None);File.Delete(path);
 var cached=processor.Load(path,true,12,CancellationToken.None);
 if(cached.Data.Length!=16||ReferenceEquals(before.Data,cached.Data))throw new Exception("Background source cache or blur ownership failed");
 using var cancellation=new CancellationTokenSource();cancellation.Cancel();bool stopped=false;
 try{processor.Load("",true,12,cancellation.Token);}catch(OperationCanceledException){stopped=true;}
 if(!stopped)throw new Exception("Background cancellation ignored");
}finally{File.Delete(path);}
var badBitmap=Convert.FromBase64String(samples[2]);BitConverter.GetBytes(500000).CopyTo(badBitmap,18);BitConverter.GetBytes(500000).CopyTo(badBitmap,22);
bool rejected=false;try{XLChat.Appearance.BackgroundProcessor.Decode(badBitmap);}catch(IOException){rejected=true;}
if(!rejected)throw new Exception("Oversized image decoded before header validation");
Console.WriteLine("PASS: PNG/JPEG/BMP decode, cache reuse, alpha ownership, cancellation and oversized image rejection");
var unknownTell=new Message(Guid.NewGuid(),0,0,DateTimeOffset.UtcNow,new ChatCode(XivChatType.TellIncoming,XivChatRelationKind.OtherPlayer,XivChatRelationKind.OtherPlayer),[],[],new SeString(),new SeString(),Guid.Empty);
var peer=new XLChat.GameFunctions.Types.TellTarget("好友",1,0,XLChat.GameFunctions.Types.TellReason.Direct);
if(unknownTell.MatchTellTarget(peer,false))throw new Exception("Unknown zero IDs merged unrelated private messages");
var knownTell=new Message(Guid.NewGuid(),0,123,DateTimeOffset.UtcNow,new ChatCode(XivChatType.TellIncoming,XivChatRelationKind.OtherPlayer,XivChatRelationKind.OtherPlayer),[],[],new SeString(),new SeString(),Guid.Empty);
peer.ContentId=123;
if(!knownTell.MatchTellTarget(peer,false))throw new Exception("Known friend message missing");
peer.ContentId=456;
if(knownTell.MatchTellTarget(peer,false))throw new Exception("Different friends merged");
var style=new XLChat.Appearance.AppearanceOptions{IncomingBubble=new(float.NaN,2,-1,.4f),BubbleRounding=float.NaN};style.Normalize();
if(style.IncomingBubble.X!=0||style.IncomingBubble.Y!=1||style.IncomingBubble.Z!=0||style.BubbleRounding!=10)throw new Exception("Invalid bubble settings not normalized");
Console.WriteLine("PASS: private recipient isolation and bubble settings");
if(XLChat.Ui.ChatLog.ChatLog.ClampSidebarWidth(0,500)!=96 || XLChat.Ui.ChatLog.ChatLog.ClampSidebarWidth(900,500)!=300 || XLChat.Ui.ChatLog.ChatLog.ClampSidebarWidth(float.NaN,500)!=160 || XLChat.Ui.ChatLog.ChatLog.ClampSidebarWidth(50,100)!=96)throw new Exception("Sidebar minimum width or conversation reservation failed");
Console.WriteLine("PASS: sidebar minimum width, invalid settings and conversation space reservation");
