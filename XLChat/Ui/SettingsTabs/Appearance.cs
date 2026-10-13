using XLChat.Appearance;
using Dalamud.Bindings.ImGui;
namespace XLChat.Ui.SettingsTabs;
public sealed class Appearance(Plugin owner,Configuration mutable) : ISettingsTab
{
    public string Name=>"外观与背景###appearance";
    private Task<string>? import;
    private string status="";
    public void Draw(bool changed)
    {
        if(import is {IsCompleted:true})
        {
            if(import.IsCompletedSuccessfully){mutable.Appearance.ImagePath=import.Result;status="图片已导入，点击保存应用。";}else if(import.IsFaulted)status="导入失败："+import.Exception?.GetBaseException().Message;
            import=null;
        }
        var options=mutable.Appearance??=new();
        var mode=(int)options.Mode;
        if(ImGui.Combo("背景样式",ref mode,new[]{"纯色","毛玻璃（静态缓存）","上传图片"},3))options.Mode=(BackgroundMode)mode;
        ImGui.ColorEdit3("背景色盘",ref options.Color);
        ImGui.SliderFloat("背景不透明度",ref mutable.WindowAlpha,0,100,"%.0f%%");
        ImGui.Checkbox("圆角背景",ref options.Rounded);
        if(options.Mode!=BackgroundMode.Solid)
        {
            ImGui.BeginDisabled(import is not null);
            if(ImGui.Button("从电脑导入图片"))
                Plugin.FileDialogManager.OpenFileDialog("导入背景图片","图片{.png,.jpg,.jpeg,.bmp}",(bool ok,string path)=>
                {if(ok)import=Task.Run(()=>Import(path));});
            ImGui.EndDisabled();
            ImGui.TextWrapped(options.ImagePath.Length==0?"未上传图片：毛玻璃使用静态纹理，图片模式使用颜色背景。":Path.GetFileName(options.ImagePath));
            if(ImGui.Button("移除图片"))options.ImagePath="";
            ImGui.Checkbox("保持比例居中裁切",ref options.Crop);
            ImGui.SliderFloat("图片染色强度",ref options.ImageTint,0,1,"%.2f");
            if(options.Mode==BackgroundMode.Glass)ImGui.SliderInt("模糊强度",ref options.Blur,1,32);
        }
        ImGui.TextWrapped("毛玻璃不采样游戏画面、不读回 GPU。素材解码和模糊在 CPU 后台进行，结果缓存；调整颜色和透明度不重新生成纹理。主窗和弹出分类共用背景，弹出分类可保留独立透明度。");
        if(ImGui.Button("重新生成当前背景"))owner.Background.Invalidate();
        if(owner.Background.Status.Length>0)ImGui.TextWrapped(owner.Background.Status);
        if(import is not null)ImGui.TextWrapped("正在导入…");
        if(status.Length>0)ImGui.TextWrapped(status);
    }
    private static string Import(string path)
    {
        var file=new FileInfo(path);var extension=file.Extension.ToLowerInvariant();
        if(extension is not (".png" or ".jpg" or ".jpeg" or ".bmp")||!file.Exists||file.Length<1||file.Length>32*1024*1024)throw new IOException("请使用不超过 32 MB 的 PNG/JPG/BMP 图片。");
        var bytes=File.ReadAllBytes(path);BackgroundProcessor.Decode(bytes);
        var folder=Path.Combine(Plugin.Interface.ConfigDirectory.FullName,"Backgrounds");Directory.CreateDirectory(folder);
        var target=Path.Combine(folder,Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))+extension);
        File.WriteAllBytes(target,bytes);return target;
    }
}
