using System;
using System.Numerics;
namespace XLinUI;
internal enum EffectGroup { Buff, Debuff, Other }
internal static class VisualPolicy
{
    internal static EffectGroup Group(byte category,bool permanent,bool fc)=>category==2?EffectGroup.Debuff:category==1&&!permanent&&!fc?EffectGroup.Buff:EffectGroup.Other;
    internal static Vector2 IconSize(int width,int height,float baseWidth)
    {return width>0&&height>0?new(baseWidth,baseWidth*Math.Clamp((float)height/width,.25f,2f)):new(baseWidth);}
    internal static Vector2 SquareSize(Vector2 current,Vector2 desired)
    {
        var delta=Vector2.Abs(desired-current);var side=delta.X>delta.Y?desired.X:desired.Y;
        return new Vector2(float.IsFinite(side)?Math.Max(1,side):Math.Max(1,current.X));
    }
    internal static Vector2 MapUv(Vector2 world,float factor,Vector2 offset)=>Vector2.One*.5f+(world+offset)*(factor/100f/2048f);
    internal static (Vector2 Min,Vector2 Max) Crop(Vector2 center,float zoom,float aspect=1)
    {
        var span=Math.Clamp(.35f/Math.Clamp(zoom,.5f,4f),.02f,1f);
        aspect=float.IsFinite(aspect)?Math.Clamp(aspect,.25f,4):1;
        var extent=aspect>=1?new Vector2(span,span/aspect):new Vector2(span*aspect,span);
        var min=Vector2.Clamp(center-extent/2,Vector2.Zero,Vector2.One-extent);return(min,min+extent);
    }
}
