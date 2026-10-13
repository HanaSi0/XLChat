using System.Collections.Generic;
namespace XLinUI;
internal readonly record struct TextAppearance(uint EdgeColor, ushort Flags);
internal sealed class NativeTextState
{
    private readonly Dictionary<string,Dictionary<nint,Entry>> entries=[];
    internal TextAppearance? Change(string name,nint addon,nint node,TextAppearance current,bool enabled)
    {
        entries.TryGetValue(name,out var nodes);
        if(nodes is not null&&nodes.TryGetValue(node,out var old)&&old.Addon!=addon)nodes.Remove(node);
        if(enabled)
        {
            if(nodes is null){nodes=[];entries[name]=nodes;}
            nodes.TryAdd(node,new Entry(addon,current));
            return new TextAppearance(0xE8000000,(ushort)(current.Flags|(1<<3)));
        }
        if(nodes is null||!nodes.Remove(node,out var saved))return null;
        if(nodes.Count==0)entries.Remove(name);
        return new TextAppearance(saved.Appearance.EdgeColor,(ushort)((current.Flags&~(1<<3))|(saved.Appearance.Flags&(1<<3))));
    }
    internal bool Contains(string name)=>entries.TryGetValue(name,out var nodes)&&nodes.Count>0;
    internal void Forget(string name)=>entries.Remove(name);
    internal void Clear()=>entries.Clear();
    private readonly record struct Entry(nint Addon,TextAppearance Appearance);
}
