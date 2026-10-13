using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
namespace XLChat.Appearance;
public sealed class ChatBackgroundRenderer : IDisposable
{
    private readonly BackgroundProcessor processor=new();
    private readonly CancellationTokenSource cancellation=new();
    private Task<IDalamudTextureWrap>? pending;
    private IDalamudTextureWrap? texture;
    private (BackgroundMode Mode,string Path,int Blur)? requested,staged;
    private long changedAt;
    public string Status {get;private set;}="";
    public void Invalidate()=>requested=null;
    public void Update(AppearanceOptions options)
    {
        var next=(options.Mode,options.ImagePath,options.Mode==BackgroundMode.Glass?options.Blur:0);
        if(staged!=next){staged=next;changedAt=Environment.TickCount64;}
        if(pending is {IsCompleted:true})
        {
            if(pending.IsCompletedSuccessfully)
            {
                if(requested==next){var old=texture;texture=pending.Result;old?.Dispose();Status="背景已就绪。";}else pending.Result.Dispose();
            }
            else if(pending.IsFaulted)
            {
                var error=pending.Exception?.GetBaseException().Message;
                if(requested==next){texture?.Dispose();texture=null;Status="图片背景不可用，使用颜色背景："+error;}
            }
            pending=null;
        }
        if(requested==next||pending is not null||Environment.TickCount64-changedAt<300)return;
        requested=next;
        if(next.Mode==BackgroundMode.Solid||next.Mode==BackgroundMode.Picture&&next.ImagePath.Length==0){texture?.Dispose();texture=null;Status=next.Mode==BackgroundMode.Picture?"未导入图片，使用颜色背景。":"";return;}
        Status="正在准备缓存背景…";
        pending=Load(options.ImagePath,options.Mode==BackgroundMode.Glass,options.Blur,cancellation.Token);
    }
    private async Task<IDalamudTextureWrap> Load(string path,bool glass,int blur,CancellationToken ct)
    {
        var pixels=await Task.Run(()=>processor.Load(path,glass,blur,ct),ct);
        return await Plugin.TextureProvider.CreateFromRawAsync(RawImageSpecification.Rgba32(pixels.Width,pixels.Height),pixels.Data,"XLChat cached background",ct);
    }
    public void Draw(AppearanceOptions options,float percent,bool title)
    {
        var alpha=float.IsFinite(percent)?Math.Clamp(percent/100,0,1):1;if(alpha<=0)return;
        var start=ImGui.GetWindowPos();var end=start+ImGui.GetWindowSize();if(title)start.Y+=ImGui.GetFrameHeight();
        var draw=ImGui.GetWindowDrawList();var rounding=options.Rounded?8*ImGuiHelpers.GlobalScale:0;
        draw.PushClipRect(start,end,false);
        if(options.Mode==BackgroundMode.Solid||texture is null)draw.AddRectFilled(start,end,ImGui.ColorConvertFloat4ToU32(new Vector4(options.Color,alpha)),rounding);
        if(options.Mode!=BackgroundMode.Solid&&texture is not null)
        {
            var uv0=Vector2.Zero;var uv1=Vector2.One;
            if(options.Crop)
            {
                var target=(end.X-start.X)/Math.Max(1,end.Y-start.Y);var source=(float)texture.Width/texture.Height;
                if(source>target){var span=target/source;uv0.X=(1-span)/2;uv1.X=1-uv0.X;}else{var span=source/target;uv0.Y=(1-span)/2;uv1.Y=1-uv0.Y;}
            }
            var shade=options.Mode==BackgroundMode.Glass&&options.ImagePath.Length==0?new Vector4(Vector3.Clamp(options.Color*2+.1f*Vector3.One,Vector3.Zero,Vector3.One),alpha):new Vector4(1,1,1,alpha);
            var tint=Vector3.Lerp(Vector3.One,options.Color,options.ImageTint);
            shade=new Vector4(shade.X*tint.X,shade.Y*tint.Y,shade.Z*tint.Z,alpha);
            draw.AddImageRounded(texture.Handle,start,end,uv0,uv1,ImGui.ColorConvertFloat4ToU32(shade),rounding);
        }
        if(options.Mode==BackgroundMode.Glass)
        {
            // Static surface lighting: no framebuffer sampling or per-frame texture work.
            var inset=1*ImGuiHelpers.GlobalScale;
            var top=start+new Vector2(inset,inset);
            var bottom=end-new Vector2(inset,inset);
            draw.AddRect(top,bottom,ImGui.ColorConvertFloat4ToU32(new Vector4(1,1,1,.10f*alpha)),rounding);
            draw.AddLine(top+new Vector2(rounding,0),new Vector2(bottom.X-rounding,top.Y),ImGui.ColorConvertFloat4ToU32(new Vector4(1,1,1,.16f*alpha)));
        }
        draw.PopClipRect();
    }
    public void Dispose()
    {
        cancellation.Cancel();texture?.Dispose();
        if(pending is not null)_=pending.ContinueWith(task=>{if(task.IsCompletedSuccessfully)task.Result.Dispose();else _=task.Exception;},TaskScheduler.Default);
        cancellation.Dispose();
    }
}
