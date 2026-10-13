using System;
namespace XLinUI;
internal static class ImageEffects
{
    // Readback rows can have padding; never treat Pitch as Width * 4.
    internal static byte[] Pack(byte[] source,int width,int height,int pitch)
    {
        if(width<1||height<1||width>4096||height>4096||pitch<width*4||source.Length<(long)(height-1)*pitch+width*4)throw new ArgumentException("无效的图像数据");
        var result=new byte[checked(width*height*4)];
        for(var y=0;y<height;y++)Buffer.BlockCopy(source,y*pitch,result,y*width*4,width*4);
        return result;
    }
    internal static byte[] Blur(byte[] rgba,int w,int h,int radius)
    {
        if(w<1||h<1||rgba.Length!=checked(w*h*4))throw new ArgumentException("无效的图像尺寸");
        radius=Math.Clamp(radius,0,32);var a=(byte[])rgba.Clone();if(radius==0)return a;
        for(var i=0;i<a.Length;i+=4)for(var c=0;c<3;c++)a[i+c]=(byte)((a[i+c]*a[i+3]+127)/255);
        var b=new byte[a.Length];
        for(var pass=0;pass<3;pass++){Box(a,b,w,h,radius,true);Box(b,a,w,h,radius,false);}
        for(var i=0;i<a.Length;i+=4)for(var c=0;c<3;c++)a[i+c]=a[i+3]==0?(byte)0:(byte)Math.Min(255,(a[i+c]*255+a[i+3]/2)/a[i+3]);
        return a;
    }
    private static void Box(byte[] a,byte[] b,int w,int h,int r,bool horizontal)
    {
        var lines=horizontal?h:w;var length=horizontal?w:h;var n=2*r+1;
        for(var line=0;line<lines;line++)for(var c=0;c<4;c++)
        {
            int Index(int p)=>(horizontal?line*w+Math.Clamp(p,0,w-1):Math.Clamp(p,0,h-1)*w+line)*4+c;
            var sum=0;for(var k=-r;k<=r;k++)sum+=a[Index(k)];
            for(var p=0;p<length;p++){b[Index(p)]=(byte)((sum+n/2)/n);sum+=a[Index(p+r+1)]-a[Index(p-r)];}
        }
    }
}
