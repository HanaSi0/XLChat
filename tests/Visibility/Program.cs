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

Check(HudGeometry.IsStandardHotbar("_ActionBar") && HudGeometry.IsStandardHotbar("_ActionBar01") && HudGeometry.IsStandardHotbar("_ActionBar09"), "detect primary and all numbered standard bars");
Check(!HudGeometry.IsStandardHotbar("_ActionBar10") && !HudGeometry.IsStandardHotbar("_ActionBarFoo") && !HudGeometry.IsStandardHotbar("_ActionBar00"), "reject unsupported addon names without unsafe casts");
Check(HudGeometry.Valid(new System.Numerics.Vector2(-20, 40), new System.Numerics.Vector2(44, 88)), "allow partially off-screen native slots");
Check(!HudGeometry.Valid(new System.Numerics.Vector2(float.NaN, 0), new System.Numerics.Vector2(44)) && !HudGeometry.Valid(System.Numerics.Vector2.Zero, new System.Numerics.Vector2(0, 40)), "reject malformed slot bounds");
Check(!GameUiVisibilityPolicy.ShouldHide("_ActionBar09",true,false,false,false,true),"styled native bars survive legacy hide mode");
Check(!GameUiVisibilityPolicy.ShouldHide("_NaviMap",true,false,false,false,false,true),"square minimap survives global hide");

var appearance = new NativeAppearanceState();
var initial = new NativeAppearance(255,100,110,120);var custom = new NativeAppearance(80,40,50,60);
appearance.Change("Inventory",1,2,initial,custom);appearance.Change("Inventory",1,2,custom,custom);
Check(appearance.Change("Inventory",1,2,custom,null)==initial,"restore original native color and alpha after repeated styling");
appearance.Change("Inventory",1,2,initial,custom);
Check(appearance.Change("Inventory",3,2,custom,null)==null,"do not restore colors across recreated addon identity");
appearance.Change("Inventory",1,2,initial,custom);appearance.Forget("Inventory");
Check(appearance.Change("Inventory",1,2,custom,null)==null,"forget colors before native addon finalization");
appearance.Change("A",1,2,initial,custom);appearance.Change("A",1,3,initial with {Alpha=0},custom);
Check(appearance.Change("A",1,3,custom,null)?.Alpha==0 && appearance.Change("A",1,2,custom,null)==initial,"restore separate nodes including originally transparent decorations");

Check(HudReplacementPolicy.ShouldHide("_TargetInfoMainTarget",HudReady.Target) && HudReplacementPolicy.ShouldHide("_TargetInfoBuffDebuff",HudReady.Target),"hide complete target UI only after its replacement draws");
Check(!HudReplacementPolicy.ShouldHide("_TargetInfo",HudReady.None) && !HudReplacementPolicy.ShouldHide("_FocusTargetInfo",HudReady.Target),"missing or unrelated replacement preserves native HUD");
Check(HudReplacementPolicy.ShouldHide("_CastBar",HudReady.PlayerCast) && !HudReplacementPolicy.ShouldHide("_CastBar",HudReady.Target),"player casting has independent readiness");
Check(HudReplacementPolicy.ShouldHide("_StatusAilments",HudReady.Statuses) && !HudReplacementPolicy.ShouldHide("_StatusCustom0",HudReady.Statuses),"standard status groups replaced while custom groups stay untouched");
Check(!HudReplacementPolicy.ShouldHide("_PartyList",HudReady.Target|HudReady.Statuses),"party remains native unless a full replacement declares readiness");
Check(HudReplacementPolicy.ShouldHide("_Parameter",HudReady.Resources) && !HudReplacementPolicy.ShouldHide("_Parameter",HudReady.None),"resources stay visible when custom resource window fails");
Check(HudReplacementPolicy.ShouldHide("_DTR",HudReady.Top) && !HudReplacementPolicy.ShouldHide("_DTR",HudReady.None),"server bar restoration follows actual toolbar rendering");
Check(HudValues.Ratio(200,100)==1 && HudValues.Ratio(1,0)==0 && HudValues.Ratio(float.NaN,10)==0,"bound HP and cast progress with invalid values");
Check(HudValues.Duration(59.1f)=="60" && HudValues.Duration(61)=="2m" && HudValues.Duration(0)=="∞","status duration handles seconds minutes and indefinite effects");
Check(!HudValues.ValidCast(float.NaN,2) && !HudValues.ValidCast(1,0) && HudValues.ValidCast(1,2),"invalid cast data preserves native cast display");
