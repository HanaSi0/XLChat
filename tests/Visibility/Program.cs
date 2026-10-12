using System;
using XLinUI;
var s=new ChatVisibilityState();
void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS: "+name);}
Check(s.Change("chat",1,2,true,true)==false,"hide visible window");
s.Change("chat",1,2,false,true);
Check(s.Change("chat",1,2,false,false)==true,"restore original visibility after repeated frames");
Check(s.Change("chat",1,2,true,false)==null,"do not touch unowned windows");
s.Change("chat",1,2,false,true);
Check(s.Change("chat",1,2,false,false)==false,"preserve originally hidden window");
s.Change("chat",1,2,true,true);
Check(s.Change("chat",3,4,false,false)==null,"never apply stale snapshot to recreated addon");
s.Change("chat",1,2,true,true);s.Forget("chat");
Check(s.Change("chat",1,2,false,false)==null,"finalized addon releases snapshot");
s.Change("chat",1,2,true,true);s.Change("chat",0,0,false,true);
Check(s.Change("chat",1,2,false,false)==null,"missing addon releases snapshot");

var all = new ChatVisibilityState();
all.Change("one",11,12,true,true);all.Change("two",21,22,false,true);
Check(all.Change("one",11,12,false,false)==true && all.Change("two",21,22,false,false)==false,"restore mixed original window states");
var chat = new ChatVisibilityState();
var current=all.Change("chat",1,2,true,true)!.Value;
current=all.Change("chat",1,2,current,false)!.Value;
current=chat.Change("chat",1,2,current,true)!.Value;
Check(chat.Change("chat",1,2,current,false)==true,"release global owner before chat-only hiding");

Check(!GameUiVisibilityPolicy.ShouldHide("_ActionBar",true,true,true,true) && !GameUiVisibilityPolicy.ShouldHide("_ActionBar09",true,true,true,true),"preserve multiple native hotbars");
Check(!GameUiVisibilityPolicy.ShouldHide("_Status",true,true,true,true) && !GameUiVisibilityPolicy.ShouldHide("_JobHudSAM0",true,true,true,true),"preserve buffs and job gauges");
Check(!GameUiVisibilityPolicy.ShouldHide("_ActionCross",true,true,true,true) && !GameUiVisibilityPolicy.ShouldHide("_PartyList",true,true,true,true),"preserve cross hotbars and party");
Check(GameUiVisibilityPolicy.ShouldHide("_Parameter",false,true,true,false),"replace only the native resource panel");
Check(!GameUiVisibilityPolicy.ShouldHide("_Parameter",false,true,false,false),"restore resources when replacement disabled");
Check(GameUiVisibilityPolicy.ShouldHide("_DTR",false,true,false,true),"replace native server info with custom toolbar");
Check(!GameUiVisibilityPolicy.ShouldHide("Inventory",false,true,true,true),"keep ordinary native windows in original UI mode");
Check(GameUiVisibilityPolicy.ShouldHide("_ActionBar",true,false,false,false),"legacy full-hide remains opt-in");
