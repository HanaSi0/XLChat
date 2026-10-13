using System;
namespace XLinUI;
// Reconstruct artwork with a compact adaptive palette and contour lines.
// Every output pixel is generated from shape/color regions; alpha remains exact.
internal static class FlatIconRasterizer
{
    internal static byte[] Redraw(byte[] bgra,int width,int height)
    {
        if(width<1||height<1||width>512||height>512||bgra.Length<width*height*4)throw new ArgumentException("Invalid icon image");
        var counts=new int[4096];var rs=new long[4096];var gs=new long[4096];var bs=new long[4096];
        for(var i=0;i<width*height*4;i+=4)
        {
            if(bgra[i+3]==0)continue;
            var key=((bgra[i+2]>>4)<<8)|((bgra[i+1]>>4)<<4)|(bgra[i]>>4);
            counts[key]++;rs[key]+=bgra[i+2];gs[key]+=bgra[i+1];bs[key]+=bgra[i];
        }
        var reds=new float[12];var greens=new float[12];var blues=new float[12];var selected=new bool[4096];var colors=0;
        for(var k=0;k<12;k++)
        {
            var best=-1;double score=-1;
            for(var bin=0;bin<4096;bin++)
            {
                if(counts[bin]==0||selected[bin])continue;
                var r=(float)rs[bin]/counts[bin];var g=(float)gs[bin]/counts[bin];var b=(float)bs[bin]/counts[bin];
                var distance=k==0?1:float.MaxValue;
                for(var j=0;j<k;j++)distance=Math.Min(distance,Distance(r,g,b,reds[j],greens[j],blues[j]));
                var value=distance*Math.Sqrt(counts[bin]);if(value>score){score=value;best=bin;}
            }
            if(best<0)break;selected[best]=true;reds[k]=(float)rs[best]/counts[best];greens[k]=(float)gs[best]/counts[best];blues[k]=(float)bs[best]/counts[best];colors++;
        }
        // Fit palette against histogram bins, avoiding per-pixel clustering passes.
        Span<double> sums=stackalloc double[48];
        for(var pass=0;pass<4;pass++)
        {
            sums.Clear();
            for(var bin=0;bin<4096;bin++)
            {
                if(counts[bin]==0)continue;var r=(float)rs[bin]/counts[bin];var g=(float)gs[bin]/counts[bin];var b=(float)bs[bin]/counts[bin];
                var nearest=Nearest(r,g,b,reds,greens,blues,colors);var at=nearest*4;
                sums[at]+=rs[bin];sums[at+1]+=gs[bin];sums[at+2]+=bs[bin];sums[at+3]+=counts[bin];
            }
            for(var k=0;k<colors;k++){var at=k*4;if(sums[at+3]==0)continue;reds[k]=(float)(sums[at]/sums[at+3]);greens[k]=(float)(sums[at+1]/sums[at+3]);blues[k]=(float)(sums[at+2]/sums[at+3]);}
        }
        var rgba=new byte[width*height*4];var labels=new byte[width*height];
        for(var pixel=0;pixel<labels.Length;pixel++)
        {
            var i=pixel*4;rgba[i+3]=bgra[i+3];if(bgra[i+3]==0)continue;
            var k=Nearest(bgra[i+2],bgra[i+1],bgra[i],reds,greens,blues,colors);labels[pixel]=(byte)k;
            rgba[i]=(byte)Math.Clamp((int)Math.Round(reds[k]),0,255);rgba[i+1]=(byte)Math.Clamp((int)Math.Round(greens[k]),0,255);rgba[i+2]=(byte)Math.Clamp((int)Math.Round(blues[k]),0,255);
        }
        for(var y=1;y<height-1;y++)for(var x=1;x<width-1;x++)
        {
            var p=y*width+x;var i=p*4;if(rgba[i+3]<128)continue;var k=labels[p];var contour=false;
            for(var n=0;n<2;n++)
            {
                var other=n==0?p+1:p+width;var j=labels[other];
                if(rgba[other*4+3]>128&&j!=k&&Distance(reds[k],greens[k],blues[k],reds[j],greens[j],blues[j])>10000&&reds[k]+greens[k]+blues[k]<reds[j]+greens[j]+blues[j])contour=true;
            }
            if(contour){rgba[i]=(byte)(rgba[i]*.8f);rgba[i+1]=(byte)(rgba[i+1]*.8f);rgba[i+2]=(byte)(rgba[i+2]*.8f);}
        }
        return rgba;
    }
    private static int Nearest(float r,float g,float b,float[] reds,float[] greens,float[] blues,int count)
    {
        var best=0;var distance=float.MaxValue;for(var k=0;k<count;k++){var d=Distance(r,g,b,reds[k],greens[k],blues[k]);if(d<distance){distance=d;best=k;}}return best;
    }
    private static float Distance(float r,float g,float b,float r2,float g2,float b2)=>2*(r-r2)*(r-r2)+3*(g-g2)*(g-g2)+(b-b2)*(b-b2);
}
