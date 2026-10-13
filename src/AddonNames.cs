using System;
using System.Collections.Generic;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace XLinUI;
// Identity-only cache. The pointer is dereferenced only when supplied by a live addon traversal.
internal sealed unsafe class AddonNames : IDisposable
{
    private readonly Dictionary<nint,string> names=[];
    internal void Register()=>Plugin.Addons.RegisterListener(AddonEvent.PreFinalize,OnFinalize);
    internal string Get(AtkUnitBase* addon)
    {
        var id=(nint)addon;if(names.TryGetValue(id,out var name))return name;
        name=addon->NameString;names[id]=name;return name;
    }
    private void OnFinalize(AddonEvent type,AddonArgs args)
    {
        nint found=0;foreach(var item in names)if(item.Value==args.AddonName){found=item.Key;break;}
        if(found!=0)names.Remove(found);
    }
    public void Dispose(){Plugin.Addons.UnregisterListener(OnFinalize);names.Clear();}
}
