using System;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.ManagedFontAtlas;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;
using Dalamud.Interface.Utility;
namespace XLinUI;
internal sealed class AppearanceAssets : IDisposable
{
    private readonly Configuration config;
    private readonly FileDialogManager dialog=new();
    private readonly CancellationTokenSource cancellation=new();
    private IFontAtlas? atlas;private IFontHandle? font;
    private IDalamudTextureWrap? image,glass;
    private Task<IDalamudTextureWrap>? imageTask,glassTask;
    private string imageKey="",fontKey="";
    private DateTime nextSample;private bool backgroundRequested;
    private string pendingFont="",pendingImage="";private DateTime fontChanged,imageChanged;
    internal string Status {get;private set;}="素材仅保存在本机，不上传。";
    internal AppearanceAssets(Configuration config)=>this.config=config;
    internal void Browse(bool isFont)=>dialog.OpenFileDialog(isFont?"导入本机字体":"导入本机背景",isFont?"字体{.ttf,.otf,.ttc}":"图片{.png,.jpg,.jpeg,.bmp}",(bool ok,string path)=>
    {
        if(!ok)return;
        try
        {
            var info=new FileInfo(path);var ext=info.Extension.ToLowerInvariant();
            if(!(isFont?new[]{".ttf",".otf",".ttc"}:new[]{".png",".jpg",".jpeg",".bmp"}).AsSpan().Contains(ext))throw new IOException("不支持的文件类型");
            if(info.Length==0||info.Length>32*1024*1024)throw new IOException("文件必须小于 32 MB");
            var bytes=File.ReadAllBytes(path);
            if(isFont)ValidateFont(bytes);
            var directory=Path.Combine(Plugin.Interface.GetPluginConfigDirectory(),"Assets");Directory.CreateDirectory(directory);
            var target=Path.Combine(directory,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))+ext);
            File.WriteAllBytes(target,bytes);
            if(isFont){config.FontFile=target;config.UseCustomFont=true;}else{config.BackgroundFile=target;config.Material=2;}
            Plugin.Interface.SavePluginConfig(config);Status="已导入 "+info.Name;
        }
        catch(Exception ex){Status="导入失败："+ex.Message;}
    });
    internal void Update(bool active)
    {
        Complete(ref imageTask,ref image);Complete(ref glassTask,ref glass);
        var nextKey=config.BackgroundFile+"|"+config.ImageBlur;
        if(pendingImage!=nextKey){pendingImage=nextKey;imageChanged=DateTime.UtcNow;}
        if(imageTask is null&&imageKey!=nextKey&&(DateTime.UtcNow-imageChanged).TotalMilliseconds>350)
        {
            imageKey=nextKey;
            if(string.IsNullOrEmpty(config.BackgroundFile)){image?.Dispose();image=null;}
            else {image?.Dispose();image=null;imageTask=LoadImage(config.BackgroundFile,(int)config.ImageBlur,cancellation.Token);}
        }
        var nextFont=config.UseCustomFont?config.FontFile+"|"+config.FontSize+"|"+config.FontFace:"";
        if(pendingFont!=nextFont){pendingFont=nextFont;fontChanged=DateTime.UtcNow;}
        if(fontKey!=nextFont&&(DateTime.UtcNow-fontChanged).TotalMilliseconds>350)
        {
            fontKey=nextFont;font?.Dispose();atlas?.Dispose();font=null;atlas=null;
            if(config.UseCustomFont&&!string.IsNullOrEmpty(config.FontFile))try
            {
                var bytes=ReadLimited(config.FontFile);ValidateFont(bytes);
                atlas=Plugin.Interface.UiBuilder.CreateFontAtlas(FontAtlasAutoRebuildMode.OnNewFrame,true,"XLinUI 字体");
                var size=config.FontSize;var face=config.FontFace;
                font=atlas.NewDelegateFontHandle(e=>e.OnPreBuild(tk=>
                {
                    var fc=new SafeFontConfig{SizePx=size,FontNo=face,GlyphRanges=new ushort[]{0x20,0xfffd,0}};
                    fc.MergeFont=tk.AddFontFromMemory(bytes,fc,"XLinUI 本机字体");
                    fc.GlyphRanges=null;fc.FontNo=0;
                    tk.AttachWindowsDefaultFont(System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),fc);
                    tk.AddGameSymbol(fc);tk.AttachExtraGlyphsForDalamudLanguage(fc);tk.Font=fc.MergeFont;
                }));
            }catch(Exception ex){Status="字体载入失败，使用默认字体："+ex.Message;}
        }
        if(font?.LoadException is {} error)Status="字体构建失败，使用默认字体："+error.Message;
        var needGlass=active&&config.Material==3&&backgroundRequested; backgroundRequested=false;
        if(needGlass&&glassTask is null&&(config.LiveGlass||glassNeedsRefresh)&&DateTime.UtcNow>=nextSample)
        {
            glassNeedsRefresh=false;nextSample=DateTime.UtcNow.AddSeconds(1/config.GlassRefresh);
            var viewport=ImGuiHelpers.MainViewport;
            glassTask=Capture(viewport.ID,viewport.Size,config.GlassBlur,cancellation.Token);
        }
        if(!needGlass){glass?.Dispose();glass=null;glassNeedsRefresh=true;}
    }
    internal IDisposable? PushFont()=>font?.Push();
    internal void DrawDialog()=>dialog.Draw();
    internal void RefreshGlass(){nextSample=DateTime.MinValue;glassNeedsRefresh=true;}
    private bool glassNeedsRefresh=true;
    private void Complete(ref Task<IDalamudTextureWrap>? task,ref IDalamudTextureWrap? current)
    {
        if(task is null||!task.IsCompleted)return;
        if(task.IsCompletedSuccessfully){current?.Dispose();current=task.Result;}
        else if(task.IsFaulted)Status="背景暂不可用，使用颜色背景："+task.Exception?.GetBaseException().Message;
        task=null;
    }
    private static void ValidateFont(byte[] bytes)
    {
        if(bytes.Length<64)throw new IOException("字体文件不完整");
        var magic=System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(bytes);
        if(magic!=0x00010000&&magic!=0x4f54544f&&magic!=0x74746366&&magic!=0x74727565)throw new IOException("不是有效的 TTF/OTF/TTC 字体文件");
    }
    private static byte[] ReadLimited(string path)
    {
        var info=new FileInfo(path);if(info.Length==0||info.Length>32*1024*1024)throw new IOException("文件大小超出限制");return File.ReadAllBytes(path);
    }
    private static async Task<IDalamudTextureWrap> LoadImage(string path,int blur,CancellationToken ct)
    {
        var bytes=await Task.Run(()=>ReadLimited(path),ct);
        var texture=await Plugin.Textures.CreateFromImageAsync(bytes,"XLinUI 背景",ct);
        if(blur==0)return texture;
        using(texture)return await BlurTexture(texture,Math.Min(1024,texture.Width),Math.Max(1,(int)(texture.Height*(double)Math.Min(1024,texture.Width)/texture.Width)),blur,false,ct);
    }
    private static async Task<IDalamudTextureWrap> Capture(uint id,Vector2 size,float radius,CancellationToken ct)
    {
        using var texture=await Plugin.Textures.CreateFromImGuiViewportAsync(new ImGuiViewportTextureArgs{ViewportId=id,TakeBeforeImGuiRender=true,AutoUpdate=false},"XLinUI 玻璃采样",ct);
        const int width=480;var height=Math.Clamp((int)(width*size.Y/Math.Max(1,size.X)),1,1024);
        return await BlurTexture(texture,width,height,Math.Max(1,(int)(radius*width/Math.Max(1,size.X))),true,ct);
    }
    private static async Task<IDalamudTextureWrap> BlurTexture(IDalamudTextureWrap source,int width,int height,int radius,bool opaque,CancellationToken ct)
    {
        height=Math.Min(2048,height);
        var (spec,raw)=await Plugin.Readback.GetRawImageAsync(source,new TextureModificationArgs{NewWidth=width,NewHeight=height,DxgiFormat=28,MakeOpaque=opaque},true,ct);
        var result=await Task.Run(()=>ImageEffects.Blur(ImageEffects.Pack(raw,spec.Width,spec.Height,spec.Pitch),spec.Width,spec.Height,radius),ct);
        return await Plugin.Textures.CreateFromRawAsync(RawImageSpecification.Rgba32(spec.Width,spec.Height),result,"XLinUI 模糊背景",ct);
    }
    internal void DrawBackground()
    {
        if(config.Material==0||config.TextOnly||config.ForceTransparentPanels||!config.Glass)return;
        if(config.Material==3)backgroundRequested=true;
        var pos=ImGui.GetWindowPos();var size=ImGui.GetWindowSize();var list=ImGui.GetWindowDrawList();var end=pos+size;
        var rounding=config.Rounded?10f*ImGuiHelpers.GlobalScale:0f;list.PushClipRect(pos,end,false);
        var texture=config.Material==2?image:config.Material==3?glass:null;
        if(texture is not null)
        {
            var uv0=Vector2.Zero;var uv1=Vector2.One;
            if(config.Material==3)
            {
                var v=ImGuiHelpers.MainViewport;
                if(ImGui.GetWindowViewport().ID!=v.ID)texture=null;
                else {uv0=Vector2.Clamp((pos-v.Pos)/v.Size,Vector2.Zero,Vector2.One);uv1=Vector2.Clamp((end-v.Pos)/v.Size,Vector2.Zero,Vector2.One);}
            }
            else if(config.ImageFit==0)
            {
                var target=size.X/Math.Max(1,size.Y);var source=(float)texture.Width/texture.Height;
                if(source>target){var span=target/source;uv0.X=(1-span)/2;uv1.X=1-uv0.X;}
                else {var span=source/target;uv0.Y=(1-span)/2;uv1.Y=1-uv0.Y;}
            }
            if(texture is not null)list.AddImageRounded(texture.Handle,pos,end,uv0,uv1,ImGui.ColorConvertFloat4ToU32(new Vector4(1,1,1,config.BackgroundOpacity)),rounding);
        }
        list.AddRectFilled(pos,end,ImGui.ColorConvertFloat4ToU32(new Vector4(config.BackgroundColor,texture is null?config.BackgroundOpacity:config.BackgroundTintOpacity*config.BackgroundOpacity)),rounding);
        list.PopClipRect();
    }
    public void Dispose()
    {
        cancellation.Cancel();dialog.Reset();font?.Dispose();atlas?.Dispose();image?.Dispose();glass?.Dispose();
        foreach(var task in new[]{imageTask,glassTask})if(task is not null)_=task.ContinueWith(t=>{if(t.IsCompletedSuccessfully)t.Result.Dispose();else _=t.Exception;},TaskScheduler.Default);
        cancellation.Dispose();
    }
}
