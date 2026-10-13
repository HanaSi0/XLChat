using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Lumina.Data.Files;
namespace XLinUI;
internal sealed class FlatButtonAtlases : IDisposable
{
    private readonly Dictionary<string,Entry> cache=[];
    private readonly CancellationTokenSource cancellation=new();
    private Task<IDalamudTextureWrap>? pending;private Entry? pendingEntry;
    private int pendingRevision;private Vector3 color;
    private long textureBytes;
    internal int Count=>cache.Count;
    internal IDalamudTextureWrap? Get(string path,ButtonRegion region)
    {
        if(!cache.TryGetValue(path,out var entry))
        {
            if(cache.Count>=16)return null;
            entry=new Entry(path);cache[path]=entry;
        }
        if(entry.Regions.Add(region)){entry.Revision++;entry.Changed=DateTime.UtcNow;}
        return entry.Texture;
    }
    internal void Pump(bool active=true)
    {
        var desired=Plugin.InstanceConfig.Tint;
        if(color!=desired){color=desired;foreach(var entry in cache.Values){entry.Revision++;entry.Changed=DateTime.UtcNow;}}
        if(pending is {IsCompleted:true})
        {
            if(pending.IsCompletedSuccessfully)
            {
                var next=pending.Result;var previous=pendingEntry!.Texture;
                var nextBytes=(long)next.Width*next.Height*4;var previousBytes=previous is null?0:(long)previous.Width*previous.Height*4;
                if(textureBytes-previousBytes+nextBytes<=96*1024*1024){textureBytes+=nextBytes-previousBytes;previous?.Dispose();pendingEntry.Texture=next;}
                else{next.Dispose();Plugin.Log.Debug("菜单重绘达到 96 MB 缓存预算，保留原图集：{Path}",pendingEntry.Path);}
            }
            else if(pending.IsFaulted)Plugin.Log.Debug(pending.Exception,"菜单按钮图集重绘失败：{Path}",pendingEntry!.Path);
            pendingEntry!.Completed=pendingRevision;pendingEntry=null;pending=null;
        }
        if(!active||pending is not null)return;
        foreach(var entry in cache.Values)
        {
            if(entry.Completed==entry.Revision||(DateTime.UtcNow-entry.Changed).TotalMilliseconds<200)continue;
            pendingEntry=entry;pendingRevision=entry.Revision;
            pending=Load(entry.Path,System.Linq.Enumerable.ToArray(entry.Regions),color,cancellation.Token);break;
        }
    }
    private static async Task<IDalamudTextureWrap> Load(string path,ButtonRegion[] regions,Vector3 accent,CancellationToken ct)
    {
        var image=await Task.Run(()=>
        {
            ct.ThrowIfCancellationRequested();var file=Plugin.Data.GetFile<TexFile>(path)??throw new InvalidOperationException("Missing button atlas");
            return (Width:(int)file.Header.Width,Height:(int)file.Header.Height,Pixels:ButtonAtlasPolicy.Redraw(file.ImageData,file.Header.Width,file.Header.Height,regions,accent));
        },ct);
        return await Plugin.Textures.CreateFromRawAsync(RawImageSpecification.Rgba32(image.Width,image.Height),image.Pixels,"XLinUI flat menu atlas",ct);
    }
    public void Dispose()
    {
        cancellation.Cancel();foreach(var e in cache.Values)e.Texture?.Dispose();cache.Clear();
        if(pending is not null)_=pending.ContinueWith(t=>{if(t.IsCompletedSuccessfully)t.Result.Dispose();else _=t.Exception;},TaskScheduler.Default);
        cancellation.Dispose();
    }
    private sealed class Entry(string path)
    {
        internal readonly string Path=path;internal readonly HashSet<ButtonRegion> Regions=[];
        internal int Revision,Completed;internal DateTime Changed;internal IDalamudTextureWrap? Texture;
    }
}
