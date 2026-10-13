using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Lumina.Data.Files;
namespace XLinUI;
internal sealed class FlatIcons : IDisposable
{
    private readonly Dictionary<uint,IDalamudTextureWrap> cache=[];
    private readonly HashSet<uint> requested=[];
    private readonly HashSet<uint> failed=[];
    private readonly Dictionary<uint,long> used=[];
    private long useSequence;
    private readonly Queue<(uint Id,string Path)> queue=[];
    private readonly CancellationTokenSource cancellation=new();
    private Task<IDalamudTextureWrap>? pending;private uint pendingId;
    internal int Count=>cache.Count;
    internal int PendingCount=>queue.Count+(pending is null?0:1);
    internal IDalamudTextureWrap? Get(uint id)
    {
        if(id==0||id==uint.MaxValue)return null;
        if(cache.TryGetValue(id,out var texture)){used[id]=++useSequence;return texture;}
        if(failed.Contains(id))return null;
        if(!requested.Contains(id)&&requested.Count>=512&&cache.Count>0)
        {
            uint oldest=0;var stamp=long.MaxValue;
            foreach(var item in used)if(item.Value<stamp){oldest=item.Key;stamp=item.Value;}
            if(oldest!=0){cache[oldest].Dispose();cache.Remove(oldest);used.Remove(oldest);requested.Remove(oldest);}
        }
        if(requested.Count<512&&requested.Add(id))
        {
            try{queue.Enqueue((id,Plugin.Textures.GetIconPath(new GameIconLookup(id))));}
            catch(Exception ex){requested.Remove(id);if(failed.Count<256)failed.Add(id);Plugin.Log.Debug(ex,"扁平图标资源暂不可用：{Icon}",id);}
        }
        return null;
    }
    internal void Pump(bool active=true)
    {
        if(pending is {IsCompleted:true})
        {
            if(pending.IsCompletedSuccessfully){cache[pendingId]=pending.Result;used[pendingId]=++useSequence;}
            else if(pending.IsFaulted){Plugin.Log.Debug(pending.Exception,"扁平图标生成失败：{Icon}",pendingId);requested.Remove(pendingId);if(failed.Count<256)failed.Add(pendingId);}
            pending=null;
        }
        if(!active||pending is not null||!queue.TryDequeue(out var request))return;
        pendingId=request.Id;pending=Load(request.Path,cancellation.Token);
    }
    private static async Task<IDalamudTextureWrap> Load(string path,CancellationToken token)
    {
        var image=await Task.Run(()=>
        {
            token.ThrowIfCancellationRequested();
            var file=Plugin.Data.GetFile<TexFile>(path)??throw new InvalidOperationException("Icon texture is missing");
            var w=file.Header.Width;var h=file.Header.Height;
            return (Width:(int)w,Height:(int)h,Pixels:FlatIconRasterizer.Redraw(file.ImageData,w,h));
        },token);
        return await Plugin.Textures.CreateFromRawAsync(RawImageSpecification.Rgba32(image.Width,image.Height),image.Pixels,"XLinUI flat icon",token);
    }
    public void Dispose()
    {
        cancellation.Cancel();foreach(var texture in cache.Values)texture.Dispose();cache.Clear();queue.Clear();used.Clear();failed.Clear();
        if(pending is not null)_=pending.ContinueWith(t=>{if(t.IsCompletedSuccessfully)t.Result.Dispose();else _=t.Exception;},TaskScheduler.Default);
        cancellation.Dispose();
    }
}
