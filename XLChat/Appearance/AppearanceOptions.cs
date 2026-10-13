using System.Numerics;
namespace XLChat.Appearance;
public enum BackgroundMode { Solid, Glass, Picture }
public sealed class AppearanceOptions
{
    public BackgroundMode Mode=BackgroundMode.Solid;
    public Vector3 Color=new(.07f,.09f,.13f);
    public string ImagePath="";
    public int Blur=12;
    public float ImageTint=.25f;
    public bool Crop=true;
    public bool Rounded=true;
    public AppearanceOptions Clone()=>(AppearanceOptions)MemberwiseClone();
    public void Normalize()
    {
        if(!Enum.IsDefined(Mode))Mode=BackgroundMode.Solid;
        Color=new(Clamp(Color.X),Clamp(Color.Y),Clamp(Color.Z));
        Blur=Math.Clamp(Blur,1,32);ImageTint=Clamp(ImageTint);ImagePath??="";
    }
    private static float Clamp(float value)=>float.IsFinite(value)?Math.Clamp(value,0,1):0;
}
public static class ChineseFontPolicy
{
    // Mandatory BMP Chinese ideographs, punctuation and full-width characters.
    public static readonly ushort[] Ranges=[0x3000,0x303f,0x3400,0x4dbf,0x4e00,0x9fff,0xff00,0xffef,0];
}
