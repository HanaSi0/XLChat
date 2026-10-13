using System;
namespace XLinUI;
[Flags]
internal enum HudReady { None = 0, Target = 1, Focus = 2, PlayerCast = 4, Statuses = 8, Party = 16, Resources = 32, Top = 64, Minimap = 128 }
internal static class HudReplacementPolicy
{
    internal static bool ShouldHide(string name, HudReady ready) => name switch
    {
        "_NaviMap" => (ready & HudReady.Minimap) != 0,
        "_Parameter" or "Parameter" => (ready & HudReady.Resources) != 0,
        "_DTR" or "DTR" => (ready & HudReady.Top) != 0,
        "_TargetInfo" or "_TargetInfoMainTarget" or "_TargetInfoCastBar" or "_TargetInfoBuffDebuff" => (ready & HudReady.Target) != 0,
        "_FocusTargetInfo" => (ready & HudReady.Focus) != 0,
        "_CastBar" => (ready & HudReady.PlayerCast) != 0,
        "_Status" or "_StatusEnhancements" or "_StatusAilments" or "_StatusOther" => (ready & HudReady.Statuses) != 0,
        "_PartyList" => (ready & HudReady.Party) != 0,
        _ => false,
    };
}
internal static class HudValues
{
    internal static float Ratio(float current, float maximum) =>
        float.IsFinite(current) && float.IsFinite(maximum) && maximum > 0 ? Math.Clamp(current / maximum, 0, 1) : 0;
    internal static bool ValidCast(float elapsed, float total) => float.IsFinite(elapsed) && float.IsFinite(total) && elapsed >= 0 && total > 0;
    internal static string Duration(float seconds) => !float.IsFinite(seconds) || seconds <= 0 ? "∞" :
        seconds >= 3600 ? $"{(int)Math.Ceiling(seconds / 3600)}h" : seconds >= 60 ? $"{(int)Math.Ceiling(seconds / 60)}m" : $"{(int)Math.Ceiling(seconds)}";
}
