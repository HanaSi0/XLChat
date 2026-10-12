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
