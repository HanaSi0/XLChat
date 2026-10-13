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
if (restored.Opacity != .88f || restored.Hotbar != 9 || restored.Channels.Count == 0) throw new Exception("Normalization failed");
Console.WriteLine("PASS: invalid settings recovery");

restored.ChatWidth=float.NaN;restored.ChatHeight=-10;restored.ChatSidebarWidth=999;restored.ChatFontScale=20;restored.Opacity=0;restored.Normalize();
if(restored.ChatWidth!=780||restored.ChatHeight!=240||restored.ChatSidebarWidth!=250||restored.ChatFontScale!=1.6f||restored.Opacity!=0)throw new Exception("Chat size bounds failed");
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
if(!transparent.ForceTransparentPanels||!transparent.ForceTransparentNative||!transparent.TextOutline||!transparent.NativeTextOutline||!transparent.HighContrastText||transparent.TextOnly||transparent.ShowEmptyStatuses)throw new Exception("Transparency defaults failed");
Console.WriteLine("PASS: default transparent mode keeps icon/bar mode and hides empty status placeholders");
transparent.ForceTransparentNative=false;transparent.ForceTransparentPanels=false;transparent.HudFontScale=1.3f;
var transparentCopy=JsonConvert.DeserializeObject<Configuration>(JsonConvert.SerializeObject(transparent))!;transparentCopy.Normalize();
if(transparentCopy.ForceTransparentPanels||transparentCopy.ForceTransparentNative||transparentCopy.HudFontScale!=1.3f)throw new Exception("Transparency choices did not persist");
Console.WriteLine("PASS: explicit transparency opt-outs and HUD font scale persist");
transparent.HudFontScale=float.PositiveInfinity;transparent.Normalize();if(transparent.HudFontScale!=1.05f)throw new Exception("HUD font bounds failed");
transparent.ApplyModernPreset();if(!transparent.ForceTransparentPanels||!transparent.ForceTransparentNative||transparent.TextOnly)throw new Exception("Updated preset failed");
Console.WriteLine("PASS: modern preset uses transparent backgrounds without dropping resource bars");
