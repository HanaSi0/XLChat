using System.Collections.Generic;
using System.Numerics;
namespace XLinUI;
internal sealed record BarTransform(Vector2 Position,float Scale);
internal sealed class NativeHotbarState
{
    private readonly Dictionary<string,(nint Addon,nint Root,BarTransform Transform)> saved=[];
    internal List<string> Keys=>new(saved.Keys);
    internal void Original(string name,nint addon,nint root,Vector2 position,float scale)
    {
        if(!saved.TryGetValue(name,out var state)||state.Addon!=addon||state.Root!=root)saved[name]=(addon,root,new(position,scale));
    }
    internal BarTransform? Restore(string name,nint addon,nint root)
    {return saved.Remove(name,out var state)&&state.Addon==addon&&state.Root==root?state.Transform:null;}
    internal void Forget(string name)=>saved.Remove(name);
    internal void Clear()=>saved.Clear();
}
