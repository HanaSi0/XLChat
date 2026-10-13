using System;
using System.Numerics;
namespace XLinUI;
internal static class SurfacePolicy
{
    internal static float PanelAlpha(bool forceTransparent, bool textOnly, bool glass, float opacity) =>
        forceTransparent || textOnly || !glass ? 0 : Math.Clamp(float.IsFinite(opacity) ? opacity : .88f, 0, 1);
    internal static byte NativeAlpha(byte original, bool forceTransparent, bool textOnly, bool glass, float opacity) =>
        (byte)(original * PanelAlpha(forceTransparent, textOnly, glass, opacity));
    internal static Vector3 ReadableColor(Vector3 color)
    {
        color = Vector3.Clamp(color, Vector3.Zero, Vector3.One);
        var lightness = .2126f * color.X + .7152f * color.Y + .0722f * color.Z;
        return lightness >= .72f ? color : Vector3.Lerp(color, Vector3.One, (.72f - lightness) / (1 - lightness));
    }
}
