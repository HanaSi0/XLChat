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
