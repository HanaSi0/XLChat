using System.Collections.Generic;
namespace XLinUI;
internal readonly record struct NativeAppearance(byte Alpha, byte Red, byte Green, byte Blue);
// Indexed by addon name: a missing addon lookup never scans all saved icon/text nodes.
internal sealed class NativeAppearanceState
{
    private readonly Dictionary<string, Dictionary<nint, Entry>> originals = [];
    internal NativeAppearance? Change(string name, nint addon, nint node, NativeAppearance current, NativeAppearance? desired)
    {
        originals.TryGetValue(name,out var nodes);
        if(nodes is not null&&nodes.TryGetValue(node,out var old)&&old.Addon!=addon)nodes.Remove(node);
        if(desired.HasValue)
        {
            if(nodes is null){nodes=[];originals[name]=nodes;}
            nodes.TryAdd(node,new Entry(addon,current));
            return desired;
        }
        if(nodes is null||!nodes.Remove(node,out var saved))return null;
        if(nodes.Count==0)originals.Remove(name);
        return saved.Appearance;
    }
    internal NativeAppearance Original(string name,nint addon,nint node,NativeAppearance current)=>
        originals.TryGetValue(name,out var nodes)&&nodes.TryGetValue(node,out var entry)&&entry.Addon==addon?entry.Appearance:current;
    internal bool Contains(string name)=>originals.TryGetValue(name,out var nodes)&&nodes.Count>0;
    internal void Forget(string name)=>originals.Remove(name);
    internal void Clear()=>originals.Clear();
    private readonly record struct Entry(nint Addon,NativeAppearance Appearance);
}
