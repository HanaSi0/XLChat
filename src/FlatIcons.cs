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
    private readonly Queue<(uint Id,string Path)> queue=[];
    private readonly CancellationTokenSource cancellation=new();
    private Task<IDalamudTextureWrap>? pending;private uint pendingId;
    internal int Count=>cache.Count;
    internal IDalamudTextureWrap? Get(uint id)
    {
        if(id==0)return null;
        if(cache.TryGetValue(id,out var texture))return texture;
        if(requested.Count<512&&requested.Add(id))
        {
            try{queue.Enqueue((id,Plugin.Textures.GetIconPath(new GameIconLookup(id))));}
            catch(Exception ex){Plugin.Log.Debug(ex,"扁平图标资源暂不可用：{Icon}",id);}
        }
        return null;
    }
    internal void Pump()
    {
        if(pending is {IsCompleted:true})
        {
            if(pending.IsCompletedSuccessfully)cache[pendingId]=pending.Result;
            else if(pending.IsFaulted)Plugin.Log.Debug(pending.Exception,"扁平图标生成失败：{Icon}",pendingId);
            pending=null;
        }
        if(pending is not null||!queue.TryDequeue(out var request))return;
        pendingId=request.Id;pending=Load(request.Path,cancellation.Token);
    }
    private static async Task<IDalamudTextureWrap> Load(string path,CancellationToken token)
    {
        var image=await Task.Run(()=>
        {
            token.ThrowIfCancellationRequested();
            var file=Plugin.Data.GetFile<TexFile>(path)??throw new InvalidOperationException("Icon texture is missing");
            var w=file.Header.Width;var h=file.Header.Height;
            return (Width:(int)w,Height:(int)h,Pixels:VisualPolicy.FlattenBgra(file.ImageData,w,h));
        },token);
        return await Plugin.Textures.CreateFromRawAsync(RawImageSpecification.Rgba32(image.Width,image.Height),image.Pixels,"XLinUI flat icon",token);
    }
    public void Dispose()
    {
        cancellation.Cancel();foreach(var texture in cache.Values)texture.Dispose();cache.Clear();queue.Clear();
        if(pending is not null)_=pending.ContinueWith(t=>{if(t.IsCompletedSuccessfully)t.Result.Dispose();else _=t.Exception;},TaskScheduler.Default);
        cancellation.Dispose();
    }
}
