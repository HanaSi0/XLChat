using System;
using System.Numerics;
using System.Collections.Generic;
namespace XLinUI;
[Serializable]
public sealed class HotbarLayout
{
    public Vector2 Position {get;set;}
    public float Scale {get;set;}=1;
    public bool Enabled {get;set;}=true;
    public void Normalize()
    {
        Position=new(float.IsFinite(Position.X)?Math.Clamp(Position.X,0,32700):0,float.IsFinite(Position.Y)?Math.Clamp(Position.Y,0,32700):0);
        Scale=float.IsFinite(Scale)?Math.Clamp(Scale,.5f,2f):1;
    }
}
internal static class HotbarPolicy
{
    internal static string Name(int index)=>index==0?"_ActionBar":$"_ActionBar{index:00}";
    internal static Vector2 Position(Vector2 desired,Vector2 size,Vector2 viewport,float grid,bool snap)
    {
        desired=new(float.IsFinite(desired.X)?desired.X:0,float.IsFinite(desired.Y)?desired.Y:0);
        grid=float.IsFinite(grid)?Math.Clamp(grid,4,64):16;
        if(snap)desired=new(MathF.Round(desired.X/grid,MidpointRounding.AwayFromZero)*grid,MathF.Round(desired.Y/grid,MidpointRounding.AwayFromZero)*grid);
        var max=Vector2.Max(Vector2.Zero,viewport-size);
        return Vector2.Clamp(desired,Vector2.Zero,Vector2.Min(new Vector2(32700),max));
    }
    internal static bool Handle(Vector2 position,Vector2 size,Vector2 viewport,float width,float height,IReadOnlyList<(Vector2 Position,Vector2 Size)> blocked,out Vector2 handle)
    {
        foreach(var candidate in new[]{position+new Vector2(0,-height-2),position+new Vector2(0,size.Y+2),position+new Vector2(-width-2,0),position+new Vector2(size.X+2,0)})
        {
            if(candidate.X<0||candidate.Y<0||candidate.X+width>viewport.X||candidate.Y+height>viewport.Y)continue;
            var overlaps=false;
            foreach(var b in blocked)if(candidate.X<b.Position.X+b.Size.X&&candidate.X+width>b.Position.X&&candidate.Y<b.Position.Y+b.Size.Y&&candidate.Y+height>b.Position.Y){overlaps=true;break;}
            if(!overlaps){handle=candidate;return true;}
        }
        handle=default;return false;
    }
    internal static bool VisibilityCommand(int index,bool visible,out string command)
    {command="";if(index is <0 or >9)return false;command=$"/hotbar display {index+1} {(visible?"on":"off")}";return true;}
}
