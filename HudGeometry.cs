using System;
using System.Numerics;
namespace XLinUI;
internal static class HudGeometry
{
    internal static bool IsStandardHotbar(string name) => name == "_ActionBar" ||
        (name.Length == 12 && name.StartsWith("_ActionBar", StringComparison.Ordinal) &&
         int.TryParse(name.AsSpan(10), out var index) && index is >= 1 and <= 9);
    internal static bool Valid(Vector2 position, Vector2 size) =>
        float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(size.X) && float.IsFinite(size.Y) &&
        size.X is > 0 and < 8192 && size.Y is > 0 and < 8192;
}
