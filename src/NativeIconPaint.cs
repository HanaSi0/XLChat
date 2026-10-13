using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
namespace XLinUI;
internal readonly record struct NativeIconPaint(string Name,uint Icon,Vector2 Position,Vector2 Size,Vector4 Tint,string Quantity,string Key,int Cooldown,bool Combo);
internal sealed partial class UserInterface
{
    private void DrawNativeFlatIcons()
    {
        if(!C.FlatNativeIcons||C.TextOnly)return;
        var draw=ImGui.GetBackgroundDrawList();var offset=ImGuiHelpers.MainViewport.Pos;
        foreach(var icon in plugin.NativeStyle.FlatPaint)
        {
            var texture=plugin.Flat.Get(icon.Icon);if(texture is null)continue;
            var pos=offset+icon.Position;var size=icon.Size;
            draw.PushClipRect(pos-Vector2.One,pos+size+Vector2.One,true);
            draw.AddRectFilled(pos-Vector2.One,pos+size+Vector2.One,0xe0201610);
            draw.AddImage(texture.Handle,pos,pos+size,Vector2.Zero,Vector2.One,ImGui.ColorConvertFloat4ToU32(icon.Tint));
            if(icon.Cooldown>0&&icon.Cooldown<100)
                draw.AddRectFilled(pos,pos+new Vector2(size.X,size.Y*(1-icon.Cooldown/100f)),0x99000000);
            if(icon.Combo)draw.AddRect(pos,pos+size,0xff79dfff,0f,ImDrawFlags.None,2);
            if(icon.Key.Length>0)ReadableText.Draw(draw,pos+new Vector2(2,1),0xffffffff,icon.Key);
            if(icon.Quantity.Length>0)ReadableText.Draw(draw,pos+new Vector2(Math.Max(0,size.X-ImGui.CalcTextSize(icon.Quantity).X-2),Math.Max(0,size.Y-ImGui.GetFontSize())),0xffffffff,icon.Quantity);
            draw.PopClipRect();
        }
    }
}
