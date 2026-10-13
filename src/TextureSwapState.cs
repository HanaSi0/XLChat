using System;
using System.Collections.Generic;
namespace XLinUI;
[Flags] internal enum RedrawKind { Icons=1, Buttons=2 }
internal interface ITextureAccess
{
    nint Read(nint texture);
    void Write(nint texture,nint view);
    void HoldTexture(nint texture);
    void ReleaseTexture(nint texture);
    void HoldView(nint view);
    void ReleaseView(nint view);
}
// A held native texture cannot be freed while it has an escrowed original view.
// Only our replacement is restored; a view changed by the game is never overwritten.
internal sealed class TextureSwapState(ITextureAccess access)
{
    private readonly Dictionary<nint,Entry> entries=[];
    internal int Count=>entries.Count;
    private long sequence;
    internal bool Apply(nint texture,nint replacement,RedrawKind kind)
    {
        if(texture==0||replacement==0)return false;
        var current=access.Read(texture);if(current==0)return false;
        if(entries.TryGetValue(texture,out var old))
        {
            if(current==old.Replacement)
            {
                if(current==replacement){entries[texture]=old with {Used=++sequence};return false;}
                access.HoldView(replacement);access.Write(texture,replacement);access.ReleaseView(current);
                entries[texture]=old with {Replacement=replacement,Kind=kind,Used=++sequence};return true;
            }
            // The game replaced and released its view; our original still belongs to escrow.
            access.ReleaseView(old.Original);access.ReleaseTexture(texture);entries.Remove(texture);
        }
        if(current==replacement)return false;
        if(entries.Count>=1024)
        {
            nint oldest=0;var stamp=long.MaxValue;foreach(var item in entries)if(item.Value.Used<stamp){oldest=item.Key;stamp=item.Value.Used;}
            if(oldest!=0)RestoreOne(oldest);
        }
        access.HoldTexture(texture);access.HoldView(replacement);
        entries[texture]=new(current,replacement,kind,++sequence);access.Write(texture,replacement);return true;
    }
    internal void Restore(RedrawKind kinds)
    {
        foreach(var key in new List<nint>(entries.Keys))
        {
            var e=entries[key];if((e.Kind&kinds)==0)continue;
            RestoreOne(key);
        }
    }
    private void RestoreOne(nint key)
    {
        var e=entries[key];
        if(access.Read(key)==e.Replacement){access.Write(key,e.Original);access.ReleaseView(e.Replacement);}
        else access.ReleaseView(e.Original);
        access.ReleaseTexture(key);entries.Remove(key);
    }
    private readonly record struct Entry(nint Original,nint Replacement,RedrawKind Kind,long Used);
}
