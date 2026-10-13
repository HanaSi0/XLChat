using System;
using System.IO;
using System.Threading;
using StbImageSharp;
namespace XLinUI;
internal sealed record BackgroundPixels(int Width,int Height,byte[] Rgba);
// Owns only managed CPU pixels. No viewport capture, native textures, or GPU readback.
internal sealed class CpuBackground
{
    private string cachedPath="";
    private BackgroundPixels? source;
    internal BackgroundPixels Load(string path,int radius,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(source is null||cachedPath!=path)
        {
            var file=new FileInfo(path);
            if(!file.Exists||file.Length<1||file.Length>32*1024*1024)throw new IOException("背景图片缺失或超过 32 MB。");
            var bytes=File.ReadAllBytes(path);ct.ThrowIfCancellationRequested();
            source=Decode(bytes,ct);cachedPath=path;
        }
        ct.ThrowIfCancellationRequested();
        var result=ImageEffects.Blur(source.Rgba,source.Width,source.Height,Math.Clamp(radius,1,32));
        ct.ThrowIfCancellationRequested();
        return new(source.Width,source.Height,result);
    }
    internal static BackgroundPixels Decode(byte[] bytes,CancellationToken ct)
    {
        using var input=new MemoryStream(bytes,false);
        var info=ImageInfo.FromStream(input)??throw new IOException("无法识别背景图片。");
        if(info.Width<1||info.Height<1||(long)info.Width*info.Height>16*1024*1024)throw new IOException("图片像素过大，请缩小到 1600 万像素以内。");
        ct.ThrowIfCancellationRequested();
        var decoded=ImageResult.FromMemory(bytes,ColorComponents.RedGreenBlueAlpha);
        ct.ThrowIfCancellationRequested();
        return Resize(decoded.Data,decoded.Width,decoded.Height);
    }
    internal static BackgroundPixels Resize(byte[] rgba,int width,int height)
    {
        if(width<1||height<1||(long)width*height>16*1024*1024||rgba.LongLength!=(long)width*height*4)throw new ArgumentException("无效背景尺寸。");
        var ratio=Math.Min(1,1024d/Math.Max(width,height));
        var w=Math.Max(1,(int)(width*ratio));var h=Math.Max(1,(int)(height*ratio));
        if(w==width&&h==height)return new(width,height,rgba);
        var output=new byte[checked(w*h*4)];
        for(var y=0;y<h;y++)for(var x=0;x<w;x++)
        {
            var input=((int)((y+.5)*height/h)*width+(int)((x+.5)*width/w))*4;
            Buffer.BlockCopy(rgba,input,output,(y*w+x)*4,4);
        }
        return new(w,h,output);
    }
}
