using System;
using System.Numerics;
namespace XLinUI;
internal readonly record struct ButtonRegion(int X,int Y,int Width,int Height,bool OutlineOnly=false,bool Glyph=false);
internal static class ButtonAtlasPolicy
{
    internal static byte[] Redraw(byte[] bgra,int width,int height,ButtonRegion[] regions,Vector3 accent)
    {
        if(width<1||height<1||width>4096||height>4096||bgra.Length<checked(width*height*4))throw new ArgumentException("Invalid button atlas");
        var rgba=new byte[width*height*4];
        for(var i=0;i<rgba.Length;i+=4){rgba[i]=bgra[i+2];rgba[i+1]=bgra[i+1];rgba[i+2]=bgra[i];rgba[i+3]=bgra[i+3];}
        for(var pass=0;pass<2;pass++)foreach(var r in regions)
        {
            if(r.Glyph!=(pass==0))continue;
            // Do not overwrite neighboring glyphs when a client supplies an invalid slice.
            if(r.Width<3||r.Height<3||r.X<0||r.Y<0||r.X+r.Width>width||r.Y+r.Height>height)continue;
            if(r.Glyph)
            {
                if(r.Width>512||r.Height>512)continue;
                var crop=new byte[r.Width*r.Height*4];
                for(var y=0;y<r.Height;y++)Array.Copy(bgra,((r.Y+y)*width+r.X)*4,crop,y*r.Width*4,r.Width*4);
                var glyph=FlatIconRasterizer.Redraw(crop,r.Width,r.Height);
                for(var y=0;y<r.Height;y++)Array.Copy(glyph,y*r.Width*4,rgba,((r.Y+y)*width+r.X)*4,r.Width*4);
                continue;
            }
            var sum=0f;var count=0;
            for(var y=r.Height/3;y<r.Height*2/3;y++)for(var x=r.Width/3;x<r.Width*2/3;x++)
            {var at=((r.Y+y)*width+r.X+x)*4;if(bgra[at+3]>64){sum+=(bgra[at]+bgra[at+1]+bgra[at+2])/765f;count++;}}
            var shade=count==0?0:sum/count;
            var fill=new Vector3(.07f,.095f,.12f)+new Vector3(shade*.18f);
            var stroke=Vector3.Clamp(accent*(.38f+shade*.3f)+new Vector3(.12f+shade*.16f),Vector3.Zero,Vector3.One);
            for(var y=0;y<r.Height;y++)for(var x=0;x<r.Width;x++)
            {
                var border=x==0||y==0||x==r.Width-1||y==r.Height-1;
                var color=border?stroke:fill;
                var i=((r.Y+y)*width+r.X+x)*4;
                rgba[i]=(byte)(color.X*255);rgba[i+1]=(byte)(color.Y*255);rgba[i+2]=(byte)(color.Z*255);rgba[i+3]=!border&&r.OutlineOnly?(byte)0:(byte)255;
            }
        }
        return rgba;
    }
}
