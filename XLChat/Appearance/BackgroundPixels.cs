using StbImageSharp;
namespace XLChat.Appearance;
public sealed record BackgroundPixels(int Width,int Height,byte[] Data);
public sealed class BackgroundProcessor
{
    private string sourcePath="";
    private BackgroundPixels? source;
    public BackgroundPixels Load(string path,bool glass,int radius,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(string.IsNullOrEmpty(path))return Glass(radius);
        if(source is null||sourcePath!=path)
        {
            var file=new FileInfo(path);
            if(!file.Exists||file.Length<1||file.Length>32*1024*1024)throw new IOException("图片不存在或超过 32 MB。");
            source=Decode(File.ReadAllBytes(path));sourcePath=path;
        }
        ct.ThrowIfCancellationRequested();
        var data=glass?ImageEffects.Blur(source.Data,source.Width,source.Height,Math.Clamp(radius,1,32)):source.Data;
        ct.ThrowIfCancellationRequested();return new(source.Width,source.Height,data);
    }
    public static BackgroundPixels Decode(byte[] bytes)
    {
        using var stream=new MemoryStream(bytes,false);
        var info=ImageInfo.FromStream(stream)??throw new IOException("无法识别图片。");
        if(info.Width<1||info.Height<1||(long)info.Width*info.Height>16*1024*1024)throw new IOException("请使用 1600 万像素以内的图片。");
        var image=ImageResult.FromMemory(bytes,ColorComponents.RedGreenBlueAlpha);
        var ratio=Math.Min(1,1024d/Math.Max(image.Width,image.Height));
        var w=Math.Max(1,(int)(image.Width*ratio));var h=Math.Max(1,(int)(image.Height*ratio));
        if(w==image.Width&&h==image.Height)return new(w,h,image.Data);
        var result=new byte[checked(w*h*4)];
        for(var y=0;y<h;y++)for(var x=0;x<w;x++)
        {var input=((int)((y+.5)*image.Height/h)*image.Width+(int)((x+.5)*image.Width/w))*4;Buffer.BlockCopy(image.Data,input,result,(y*w+x)*4,4);}
        return new(w,h,result);
    }
    public static BackgroundPixels Glass(int radius)
    {
        const int size=256;var pixels=new byte[size*size*4];
        for(var y=0;y<size;y++)for(var x=0;x<size;x++)
        {
            var dx=(x-60f)/120;var dy=(y-50f)/100;
            var glow=70*Math.Exp(-(dx*dx+dy*dy));
            var noise=((x*73+y*151)%17-8)*.35;
            var value=(byte)Math.Clamp(125+glow-y*.12+noise,0,255);
            var i=(y*size+x)*4;pixels[i]=pixels[i+1]=pixels[i+2]=value;pixels[i+3]=255;
        }
        return new(size,size,ImageEffects.Blur(pixels,size,size,Math.Clamp(radius,1,32)));
    }
}
