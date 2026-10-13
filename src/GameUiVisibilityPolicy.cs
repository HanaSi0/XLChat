using System;

namespace XLinUI;

internal static class GameUiVisibilityPolicy
{
    private static readonly string[] CombatPrefixes = ["ActionBar", "ActionCross", "ActionDoubleCross", "Status", "PartyList", "AllianceList", "TargetInfo", "CastBar", "JobHud", "EnemyList", "LimitBreak"];

    internal static bool ShouldHide(string name, bool hideAll, bool nativeCombat, bool simpleResources, bool customTop, bool squareHotbars = false, bool squareMinimap = false)
    {
        if (simpleResources && (name == "_Parameter" || name == "Parameter")) return true;
        if (customTop && (name == "_DTR" || name == "DTR")) return true;
        if (!hideAll) return false;
        if (squareHotbars && (HudGeometry.IsStandardHotbar(name) || name.StartsWith("_ActionCross", StringComparison.Ordinal) || name.StartsWith("_ActionDoubleCross", StringComparison.Ordinal))) return false;
        if (squareMinimap && name == "_NaviMap") return false;
        if (nativeCombat)
        {
            var normalized = name.TrimStart('_');
            foreach (var prefix in CombatPrefixes)
                if (normalized.StartsWith(prefix, StringComparison.Ordinal)) return false;
        }
        return true;
    }
}
