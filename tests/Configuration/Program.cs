using System;
using XLinUI;
using Newtonsoft.Json;
var c = new Configuration();
c.Channels[0].Name = "我的频道";
c.ShowOriginalUi = false;
var restored = JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(c))!;
restored.Normalize();
if (restored.Channels.Count != c.Channels.Count || restored.Channels[0].Name != "我的频道" || restored.ShowOriginalUi) throw new Exception("Settings roundtrip failed");
Console.WriteLine("PASS: settings, custom channels and original UI option roundtrip");
if (!JsonConvert.DeserializeObject<Configuration>("{}")!.ShowOriginalUi) throw new Exception("Old settings unexpectedly hide original UI");
Console.WriteLine("PASS: legacy settings keep original UI visible");
restored.Opacity = float.NaN; restored.Hotbar = 99; restored.Channels.Clear(); restored.Normalize();
if (restored.Opacity != 1 || restored.Hotbar != 9 || restored.Channels.Count == 0) throw new Exception("Normalization failed");
Console.WriteLine("PASS: invalid settings recovery");

restored.ChatWidth=float.NaN;restored.ChatHeight=-10;restored.ChatSidebarWidth=999;restored.ChatFontScale=20;restored.Opacity=0;restored.Normalize();
if(restored.ChatWidth!=780||restored.ChatHeight!=200||restored.ChatSidebarWidth!=600||restored.ChatFontScale!=2.5f||restored.Opacity!=0)throw new Exception("Chat size bounds failed");
Console.WriteLine("PASS: resizable chat bounds and fully transparent background");
if(!JsonConvert.DeserializeObject<Configuration>("{}")!.NativeCombatWidgets)throw new Exception("Native combat widgets disabled on migration");
Console.WriteLine("PASS: native hotbars and combat widgets enabled by default");

var toolbar = new Configuration();toolbar.TopSections["sound"]=false;
var roundtrip = JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(toolbar))!;roundtrip.Normalize();
if(roundtrip.TopSections["sound"]||!roundtrip.TopSections["dtr"])throw new Exception("Toolbar sections failed to persist");
Console.WriteLine("PASS: independent toolbar sections persist");

var styled = new Configuration { StyleNativeWindows = true, SquareMinimap = false };styled.ExcludedNativeWindows.Add("Inventory");
var migrated=JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(styled))!;migrated.Normalize();
if(!migrated.StyleNativeWindows||migrated.SquareMinimap||!migrated.ExcludedNativeWindows.Contains("Inventory"))throw new Exception("Native style roundtrip failed");
Console.WriteLine("PASS: native style overrides and per-window exclusions persist");
var old=JsonConvert.DeserializeObject<Configuration>("{\"Version\":2,\"TopSections\":{\"sound\":false},\"ExcludedNativeWindows\":null}")!;old.Normalize();
if(!old.StyleNativeWindows||!old.SquareHotbars||!old.TopSections["gamewindows"]||old.TopSections["sound"]||old.ExcludedNativeWindows is null)throw new Exception("Style migration failed");
Console.WriteLine("PASS: v2 migration preserves disabled entries and new installations enable the modern native window theme");

var modern = new Configuration { NativeInteractions = false, ModernTarget = false, ShowOriginalUi = false };
modern.TopSections["sound"] = false;modern.ExcludedNativeWindows.Add("Inventory");modern.Channels[0].Name="私人";modern.ApplyModernPreset();
if(!modern.ModernTarget||!modern.ShowOriginalUi||!modern.ModernStatuses||!modern.HideOriginalChat||!modern.StyleNativeWindows||modern.NativeInteractions||modern.TopSections["sound"]||!modern.ExcludedNativeWindows.Contains("Inventory")||modern.Channels[0].Name!="私人")throw new Exception("Modern preset lost preferences");
Console.WriteLine("PASS: modern preset preserves interaction opt-in and personal channel/toolbar/exclusion choices");
modern.StatusIconSize=float.NaN;modern.Normalize();if(modern.StatusIconSize!=30)throw new Exception("Status icon bounds failed");
Console.WriteLine("PASS: invalid status icon size recovered");
var customModern=JsonConvert.DeserializeObject<Configuration>("{\"ModernTarget\":false,\"ModernFocus\":false,\"ModernStatuses\":false,\"StyleNativeWindows\":false}")!;customModern.Normalize();
if(customModern.ModernTarget||customModern.ModernFocus||customModern.ModernStatuses||customModern.StyleNativeWindows)throw new Exception("Explicit module opt-outs overwritten");
Console.WriteLine("PASS: existing explicit module opt-outs remain disabled on update");

var transparent=new Configuration();
if(transparent.ForceTransparentPanels||transparent.ForceTransparentNative||transparent.BackgroundOpacity!=1||transparent.Opacity!=1||!transparent.TextOutline||!transparent.NativeTextOutline||!transparent.HighContrastText||transparent.TextOnly||transparent.ShowEmptyStatuses)throw new Exception("Transparency defaults failed");
Console.WriteLine("PASS: default opaque mode keeps icon/bar mode and hides empty status placeholders");
transparent.ForceTransparentNative=false;transparent.ForceTransparentPanels=false;transparent.HudFontScale=1.3f;
var transparentCopy=JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(transparent))!;transparentCopy.Normalize();
if(transparentCopy.ForceTransparentPanels||transparentCopy.ForceTransparentNative||transparentCopy.HudFontScale!=1.3f)throw new Exception("Transparency choices did not persist");
Console.WriteLine("PASS: explicit transparency opt-outs and HUD font scale persist");
transparent.HudFontScale=float.PositiveInfinity;transparent.Normalize();if(transparent.HudFontScale!=1.05f)throw new Exception("HUD font bounds failed");
transparent.ApplyModernPreset();if(transparent.ForceTransparentPanels||transparent.ForceTransparentNative||transparent.BackgroundOpacity!=1||transparent.Opacity!=1||transparent.TextOnly)throw new Exception("Updated preset failed");
Console.WriteLine("PASS: modern preset uses opaque backgrounds without dropping resource bars");

var chat = new Configuration { ChatWidth = 1024, ChatHeight = 600, ChatSidebarWidth = 230, ChatNavigation = ChatNavigation.Right, ChatComposerHeight = 180, ChatHeaderFontScale = 1.3f, ChatInputWidth=220, ChatBackground = ChatBackground.Custom, ChatBackgroundOpacity=.3f, NativeInteractions=false };
var profile=ChatLayoutProfile.Capture(chat);chat.ChatWidth=400;chat.ChatInputWidth=0;chat.ChatSidebarWidth=80;chat.ChatNavigation=ChatNavigation.Hidden;profile.Values["NativeInteractions"]="true";profile.Values["ChatInputHeight"]="999";profile.Apply(chat);
if(chat.ChatWidth!=1024||chat.ChatInputWidth!=220||chat.ChatSidebarWidth!=230||chat.ChatNavigation!=ChatNavigation.Right||chat.ChatHeaderFontScale!=1.3f||chat.NativeInteractions||chat.ChatInputHeight!=100)throw new Exception("Chat layout profile failed");
Console.WriteLine("PASS: profile restores all chat layout values and clamps input without changing interaction authorization");
chat.ChatProfiles["宽屏"]=profile;var chatCopy=JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(chat))!;chatCopy.Normalize();
if(chatCopy.ChatProfiles.Count!=1||chatCopy.ChatComposerHeight!=180||chatCopy.ChatBackground!=ChatBackground.Custom||chatCopy.ChatBackgroundOpacity!=.3f)throw new Exception("Chat profile persistence failed");
Console.WriteLine("PASS: named layout profiles and independent background survive settings roundtrip");
var legacyChat=JsonConvert.DeserializeObject<Configuration>("{\"Version\":5,\"ChatWidth\":900,\"Channels\":[{\"Name\":\"小队\",\"Types\":[14,14]},{\"Name\":\"私信\",\"Types\":[13]}]}")!;legacyChat.Normalize();
if(legacyChat.ChatWidth!=900||legacyChat.Channels[0].Types.Count!=1||legacyChat.Channels[0].SendRoute!="party"||legacyChat.Channels[1].SendRoute!="tell")throw new Exception("Chat migration failed");
Console.WriteLine("PASS: old dimensions and custom filters migrate with valid send routes");
chat.ChatNavigation=(ChatNavigation)999;chat.ChatComposerHeight=float.NaN;chat.ChatInputHeight=-1;chat.ChatHistoryLimit=int.MaxValue;chat.Channels[1].Id=chat.Channels[0].Id;chat.Normalize();
if(chat.ChatNavigation!=ChatNavigation.Left||chat.ChatComposerHeight!=120||chat.ChatInputHeight!=24||chat.ChatHistoryLimit!=10000||chat.Channels[0].Id==chat.Channels[1].Id)throw new Exception("Chat recovery failed");
Console.WriteLine("PASS: invalid chat enums sizes capacity and duplicate IDs recover safely");

var selectedChat=new Configuration();selectedChat.ChatActiveChannelId=selectedChat.Channels[1].Id;selectedChat.ChatSendRoute="party";selectedChat.Normalize();
var selectedCopy=JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(selectedChat))!;selectedCopy.Normalize();
if(selectedCopy.ChatActiveChannelId!=selectedChat.Channels[1].Id||selectedCopy.ChatSendRoute!="party")throw new Exception("Selection persistence failed");
selectedCopy.ChatSendRoute="/teleport";selectedCopy.ChatActiveChannelId="missing";selectedCopy.Normalize();
if(selectedCopy.ChatSendRoute!="say"||selectedCopy.ChatActiveChannelId!=selectedCopy.Channels[0].Id)throw new Exception("Selection recovery failed");
Console.WriteLine("PASS: selected view and send route persist independently with invalid values recovered");

var appearanceConfig=new Configuration{Material=99,FontSize=float.PositiveInfinity,ImageBlur=-10,BackgroundOpacity=2,FontFace=-3};appearanceConfig.Normalize();
if(appearanceConfig.Material!=1||appearanceConfig.FontSize!=18||appearanceConfig.ImageBlur!=0||appearanceConfig.BackgroundOpacity!=1||appearanceConfig.FontFace!=0)throw new Exception("Appearance normalization failed");
Console.WriteLine("PASS: appearance values normalize safely");

var assetConfig=new Configuration{Material=2,BackgroundFile="C:/素材/背景.png",FontFile="C:/字体/中文.ttf",UseCustomFont=true,FontSize=24,BackgroundColor=new(.1f,.2f,.3f)};
var assetCopy=JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(assetConfig))!;assetCopy.Normalize();
if(assetCopy.BackgroundFile!=assetConfig.BackgroundFile||assetCopy.FontFile!=assetConfig.FontFile||!assetCopy.UseCustomFont||assetCopy.FontSize!=24||assetCopy.BackgroundColor!=assetConfig.BackgroundColor)throw new Exception("Asset settings roundtrip failed");
Console.WriteLine("PASS: local asset paths font and material colors persist");
if(JsonConvert.DeserializeObject<Configuration>("{}")!.Material!=1)throw new Exception("Legacy material changed unexpectedly");
Console.WriteLine("PASS: missing material setting selects solid background by default");

var topConfig=new Configuration{TopIconSize=float.NaN,TopGap=-1,TopMaxLabelWidth=9999};topConfig.TopSections["currency"]=false;topConfig.TopPinned["currency"]=true;topConfig.TopPinned["map"]=true;
var topCopy=JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(topConfig))!;topCopy.Normalize();
if(topCopy.TopSections["currency"]||!topCopy.TopPinned["currency"]||!topCopy.TopPinned["map"]||topCopy.TopIconSize!=20||topCopy.TopGap!=0||topCopy.TopMaxLabelWidth!=300)throw new Exception("Top customization persistence failed");
Console.WriteLine("PASS: visibility and right pinning remain independent with bounded toolbar dimensions");
if(!JsonConvert.DeserializeObject<Configuration>("{}")!.TopPinned["clock"])throw new Exception("Default pinned information missing");
Console.WriteLine("PASS: legacy configuration receives default right pinned information");

var noviceSetting=new Configuration{ChatShowNoviceJoin=false};var noviceCopy=JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(noviceSetting))!;noviceCopy.Normalize();
if(noviceCopy.ChatShowNoviceJoin)throw new Exception("Novice button visibility did not persist");
var noviceProfile=ChatLayoutProfile.Capture(noviceSetting);noviceCopy.ChatShowNoviceJoin=true;noviceProfile.Apply(noviceCopy);
if(noviceCopy.ChatShowNoviceJoin)throw new Exception("Novice button profile did not restore");
Console.WriteLine("PASS: novice join button visibility persists and follows chat layout profiles");

var savedBars=new Configuration{UseHotbarLayouts=true,HotbarGrid=float.NaN};savedBars.HotbarLayouts["_ActionBar"]=new(){Position=new(700,900),Scale=1.15f};savedBars.HotbarLayouts["_ActionBar09"]=new(){Position=new(-1,float.NaN),Scale=50,Enabled=false};savedBars.HotbarLayouts["_NotHotbar"]=new();
var barCopy=JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(savedBars))!;barCopy.Normalize();
if(barCopy.HotbarLayouts.Count!=2||barCopy.HotbarLayouts["_ActionBar"].Scale!=1.15f||barCopy.HotbarLayouts["_ActionBar09"].Scale!=2||barCopy.HotbarLayouts["_ActionBar09"].Position!=System.Numerics.Vector2.Zero||barCopy.HotbarLayouts["_ActionBar09"].Enabled||barCopy.HotbarGrid!=16)throw new Exception("Multi hotbar settings failed");
Console.WriteLine("PASS: multiple native layouts persist independently with invalid geometry recovered");
if(JsonConvert.DeserializeObject<Configuration>("{}")!.UseHotbarLayouts)throw new Exception("Legacy hotbars automatically overridden");
Console.WriteLine("PASS: existing installations keep native hotbar layouts until explicitly imported");

if(TopIconPolicy.Find(Array.Empty<Lumina.Excel.Sheets.MainCommand>(),r=>true,r=>r.Icon)!=0)throw new Exception("Empty icon sheet fallback failed");
Console.WriteLine("PASS: empty Lumina menu sheet uses fallback without dereferencing default row");
if(TopIconPolicy.Find(new[]{default(Lumina.Excel.Sheets.MainCommand)},r=>false,r=>r.Icon)!=0)throw new Exception("Unmatched menu fallback failed");
Console.WriteLine("PASS: no matching menu never reads Icon from an unreadable default Lumina row");
var iconRows=new[]{(Name:"其他菜单",Icon:11),(Name:"角色设置",Icon:22)};
if(TopIconPolicy.Find(iconRows,r=>r.Name=="角色设置",r=>r.Icon)!=22)throw new Exception("Matched menu icon changed");
Console.WriteLine("PASS: matching menu retains its actual icon");

var modernVisuals=new Configuration{MinimapSize=float.NaN,MinimapZoom=900,ReadabilityShade=-1};modernVisuals.Normalize();
if(modernVisuals.MinimapSize!=240||modernVisuals.MinimapZoom!=4||modernVisuals.ReadabilityShade!=0)throw new Exception("Invalid modern visual bounds");
Console.WriteLine("PASS: map dimensions zoom and readability reject invalid settings");
if(JsonConvert.SerializeObject(new Configuration()).Contains("LiveGlass"))throw new Exception("Removed live sampling persisted");
Console.WriteLine("PASS: continuous glass sampling defaults off");

var upgrade=new Configuration{Version=6,ForceTransparentPanels=true,ForceTransparentNative=true,TextOnly=true,Opacity=0};upgrade.Normalize();
if(upgrade.Version!=8||upgrade.ForceTransparentPanels||upgrade.ForceTransparentNative||upgrade.TextOnly||upgrade.Opacity!=1||upgrade.ChatBackground!=ChatBackground.Global)throw new Exception("Opaque upgrade failed");
Console.WriteLine("PASS: legacy background migration applies opaque startup without leaving hidden-background flags");
var customAlpha=new Configuration{OpaqueOnEnable=false,Opacity=.2f,BackgroundOpacity=.3f,ChatBackground=ChatBackground.Custom,ChatBackgroundOpacity=.4f};customAlpha.Normalize();
var alphaCopy=JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(customAlpha))!;alphaCopy.Normalize();
if(alphaCopy.OpaqueOnEnable||alphaCopy.Opacity!=.2f||alphaCopy.BackgroundOpacity!=.3f||alphaCopy.ChatBackgroundOpacity!=.4f)throw new Exception("Transparency preference lost");
Console.WriteLine("PASS: disabling opaque startup preserves custom transparency");

var import=ChatTwoImport.Parse("""{"Tabs":[{"Name":"队伍","Channel":"Party","SelectedChannels":{"Party":{"Item1":2,"Item2":4}}},{"Name":"空","SelectedChannels":{}}],"Password":"ignored"}""");
if(import.Channels.Count!=2||import.Channels[0].SendRoute!="party"||!ChatFilter.Accepts(import.Channels[0],14,1,2)||ChatFilter.Accepts(import.Channels[0],14,2,3)||!ChatFilter.Accepts(import.Channels[0],14,2,2)||!ChatFilter.Accepts(import.Channels[0],14,0,0)||ChatFilter.Accepts(import.Channels[1],10,0,0))throw new Exception("Import filter mismatch");
if(JsonConvert.SerializeObject(import.Channels).Contains("ignored"))throw new Exception("Import retained private fields");
var importedCopy=JsonConvert.DeserializeObject<System.Collections.Generic.List<Channel>>(JsonConvert.SerializeObject(import.Channels))!;
if(!ChatFilter.Accepts(importedCopy[0],14,1,2)||ChatFilter.Accepts(importedCopy[1],10,0,0))throw new Exception("Imported relation roundtrip failed");
var invalidImport=false;try{ChatTwoImport.Parse("""{"Tabs":[{"Name":"坏","SelectedChannels":{"Say":true}}]}""");}catch(JsonException){invalidImport=true;}
if(!invalidImport)throw new Exception("Invalid relation import accepted");
var legacyGlass=JsonConvert.DeserializeObject<Configuration>("""{"Version":7,"Material":3,"LiveGlass":true,"GlassRefresh":0.1,"OpaqueOnEnable":false,"Opacity":0.4,"BackgroundOpacity":0.4}""")!;legacyGlass.Normalize();
if(legacyGlass.Material!=1||legacyGlass.Opacity!=.4f)throw new Exception("Removed glass migration failed");
Console.WriteLine("PASS: Chat Two import keeps relation masks, empty filters and routes; excludes private fields; rejects malformed input; removed glass migrates safely");
