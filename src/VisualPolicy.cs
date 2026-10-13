using System;
using System.Numerics;
namespace XLinUI;
internal enum EffectGroup { Buff, Debuff, Other }
internal static class VisualPolicy
{
    internal static EffectGroup Group(byte category,bool permanent,bool fc)=>category==2?EffectGroup.Debuff:category==1&&!permanent&&!fc?EffectGroup.Buff:EffectGroup.Other;
    internal static Vector2 IconSize(int width,int height,float baseWidth)
    {return width>0&&height>0?new(baseWidth,baseWidth*Math.Clamp((float)height/width,.25f,2f)):new(baseWidth);}
    internal static Vector2 MapUv(Vector2 world,float factor,Vector2 offset)=>Vector2.One*.5f+(world+offset)*(factor/100f/2048f);
    internal static (Vector2 Min,Vector2 Max) Crop(Vector2 center,float zoom)
    {
        var span=Math.Clamp(.35f/Math.Clamp(zoom,.5f,4f),.02f,1f);
        var min=Vector2.Clamp(center-new Vector2(span/2),Vector2.Zero,new Vector2(1-span));return(min,min+new Vector2(span));
    }
    internal static byte[] FlattenBgra(byte[] data,int width,int height)
    {
        if(width<1||height<1||width>512||height>512||data.Length<width*height*4)throw new ArgumentException("Invalid icon image");
        var rgba=new byte[width*height*4];
        // Keep the original silhouette, palette and alpha; remove fine gradients rather than inventing a new symbol.
        for(var i=0;i<rgba.Length;i+=4)
        {
            for(var c=0;c<3;c++){var value=data[i+2-c];rgba[i+c]=(byte)Math.Clamp(((value+16)/32)*32,0,255);}
            rgba[i+3]=data[i+3];
        }
        return rgba;
    }
}
