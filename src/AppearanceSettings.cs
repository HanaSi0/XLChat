using Dalamud.Bindings.ImGui;
namespace XLinUI;
internal sealed partial class UserInterface
{
    private void DrawAppearanceSettings(ref bool changed)
    {
        ReadableText.TextWrapped("适用于 XLinUI 窗口。聊天独立背景选择“跟随全局”时使用这里的素材；只显示文字模式仍优先隐藏背景。原版游戏窗口不替换字体或图片。");
        var material=C.Material;
        if(ImGui.Combo("背景模式",ref material,new[]{"原有透明度设置","纯色","本机图片","动态模糊玻璃（采样）"},4)){C.Material=material;changed=true;}
        var color=C.BackgroundColor;if(ImGui.ColorEdit3("背景与玻璃颜色",ref color)){C.BackgroundColor=color;changed=true;}
        var text=C.ForegroundColor;if(ImGui.ColorEdit3("正文文字颜色",ref text)){C.ForegroundColor=text;changed=true;}
        var accent=C.Tint;if(ImGui.ColorEdit3("按钮与强调色",ref accent)){C.Tint=accent;changed=true;}
        ChatSlider("背景不透明度（0为透明）",C.BackgroundOpacity,0,1,v=>C.BackgroundOpacity=v,ref changed,"%.2f");
        ChatSlider("图片／玻璃颜色覆盖",C.BackgroundTintOpacity,0,1,v=>C.BackgroundTintOpacity=v,ref changed,"%.2f");
        if(ReadableText.Button("从电脑导入背景图片"))Appearance.Browse(false);
        ReadableText.TextWrapped(string.IsNullOrEmpty(C.BackgroundFile)?"未导入背景图片":System.IO.Path.GetFileName(C.BackgroundFile));
        var fit=C.ImageFit;if(ImGui.Combo("图片填充方式",ref fit,new[]{"保持比例并居中裁切","拉伸铺满"},2)){C.ImageFit=fit;changed=true;}
        ChatSlider("图片模糊半径",C.ImageBlur,0,32,v=>C.ImageBlur=v,ref changed);
        if(ReadableText.SmallButton("移除图片")){C.BackgroundFile="";if(C.Material==2)C.Material=0;changed=true;}
        ChatSlider("玻璃模糊强度",C.GlassBlur,1,64,v=>C.GlassBlur=v,ref changed);
        ChatSlider("玻璃更新次数／秒",C.GlassRefresh,.5f,4,v=>C.GlassRefresh=v,ref changed,"%.1f");
        ReadableText.TextWrapped("玻璃通过 Dalamud 公开接口采样游戏主窗口，低分辨率模糊，默认每秒 2 次；不包含 XLinUI 自身。采样失败或移动到其他显示器窗口时回退为颜色背景。关闭插件窗口时停止采样，画面不保存到文件。");
        ImGui.Separator();
        changed|=Toggle("使用导入字体",C.UseCustomFont,v=>C.UseCustomFont=v);
        if(ReadableText.Button("从电脑导入字体"))Appearance.Browse(true);
        ReadableText.TextWrapped(string.IsNullOrEmpty(C.FontFile)?"未导入字体":System.IO.Path.GetFileName(C.FontFile));
        ChatSlider("基础字体大小",C.FontSize,12,36,v=>C.FontSize=v,ref changed);
        var face=C.FontFace;if(ImGui.SliderInt("TTC 字体序号（通常为 0）",ref face,0,20)){C.FontFace=face;changed=true;}
        if(ReadableText.SmallButton("恢复默认字体")){C.UseCustomFont=false;changed=true;}
        ReadableText.TextWrapped("支持 PNG/JPG/BMP 图片和 TTF/OTF/TTC 字体，单个文件最大 32 MB。导入后复制到插件本机配置目录；缺失的中文和游戏符号使用 Dalamud 补充字形。字体构建失败时保留默认字体。");
        ReadableText.TextWrapped(Appearance.Status);
    }
}
