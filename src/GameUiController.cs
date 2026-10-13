using System;
using System.Collections.Generic;
using System.Diagnostics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace XLinUI;

internal sealed unsafe class GameUiController : IDisposable
{
    internal readonly Dictionary<string, GameLayout> Layouts = [];
    internal float GameScale = 1;
    internal HudReady Ready;
    private readonly ChatVisibilityState state = new();
    private bool hide;
    private long lastDraw;
    private bool active;
    private bool rendered;
    private bool globalDraw;
    private static readonly string[] ReplacedAddons=["_Parameter","Parameter","_DTR","DTR","_TargetInfo","_TargetInfoMainTarget","_TargetInfoCastBar","_TargetInfoBuffDebuff","_FocusTargetInfo","_CastBar","_Status","_StatusEnhancements","_StatusAilments","_StatusOther","_PartyList","_NaviMap"];
    private void RegisterDraw(bool all)
    {
        globalDraw=all;
        if(all)Plugin.Addons.RegisterListener(AddonEvent.PreDraw,OnDraw);
        else foreach(var name in ReplacedAddons)Plugin.Addons.RegisterListener(AddonEvent.PreDraw,name,OnDraw);
    }

    internal void Register()
    {
        RegisterDraw(false);
        Plugin.Addons.RegisterListener(AddonEvent.PreFinalize, OnFinalize);
    }

    internal void SetHide(bool value, bool uiRendered = false)
    {
        if(globalDraw!=value){Plugin.Addons.UnregisterListener(OnDraw);RegisterDraw(value);}
        hide = value; rendered = uiRendered; lastDraw = Stopwatch.GetTimestamp(); Update();
    }

    internal void Update()
    {
        var module = RaptureAtkModule.Instance();
        if (module == null) return;
        var manager = &module->RaptureAtkUnitManager.AtkUnitManager;
        GameScale = manager->UiBaseScale;
        var loggedIn = Plugin.Objects.LocalPlayer is not null;
        var live = rendered && loggedIn && !Plugin.GameGui.GameUiHidden && Stopwatch.GetElapsedTime(lastDraw).TotalMilliseconds < 500;
        var desired = live && hide;
        var units = manager->AllLoadedUnitsList;
        for (var i = 0; i < Math.Min((int)units.Count, 256); i++)
        {
            var addon = units.Entries[i].Value;
            if (addon == null || addon->RootNode == null) continue;
            Apply(addon, live, desired, loggedIn);

        }
        active = live && (hide || Plugin.InstanceConfig.ShowStatus || Plugin.InstanceConfig.ShowTop || Ready != HudReady.None);
        if (!loggedIn) Layouts.Clear();
    }

    private void OnDraw(AddonEvent type, AddonArgs args)
    {
        if (!hide && !active) return;
        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon == null || addon->RootNode == null) return;
        var live = rendered && Plugin.Objects.LocalPlayer is not null && !Plugin.GameGui.GameUiHidden && Stopwatch.GetElapsedTime(lastDraw).TotalMilliseconds < 500;
        Apply(addon, live, live && hide, Plugin.Objects.LocalPlayer is not null);
    }

    private void Apply(AtkUnitBase* addon, bool live, bool hideAll, bool loggedIn)
    {
        var name = Plugin.Names.Get(addon);
        var node = addon->RootNode;
        if (loggedIn)
        {
            if (!Layouts.TryGetValue(name, out var layout))
                Layouts[name] = new GameLayout(addon->X, addon->Y, addon->Scale, node->Width, node->Height, node->IsVisible());
            else if (layout.X != addon->X || layout.Y != addon->Y || layout.Scale != addon->Scale || layout.Width != node->Width || layout.Height != node->Height)
                Layouts[name] = new GameLayout(addon->X, addon->Y, addon->Scale, node->Width, node->Height, layout.Visible);
        }
        var config = Plugin.InstanceConfig;
        var desired = live && (HudReplacementPolicy.ShouldHide(name, Ready) || GameUiVisibilityPolicy.ShouldHide(name, hideAll, config.NativeCombatWidgets, false, false, config.SquareHotbars, config.SquareMinimap));
        if (!desired && !active) return;
        var visible = state.Change(name, (nint)addon, (nint)node, node->IsVisible(), desired);
        if (visible.HasValue && node->IsVisible()!=visible.Value) node->ToggleVisibility(visible.Value);
    }
    private void OnFinalize(AddonEvent type, AddonArgs args){var name=args.AddonName;state.Forget(name);Layouts.Remove(name);}

    public void Dispose()
    {
        Plugin.Addons.UnregisterListener(OnDraw);
        Plugin.Addons.UnregisterListener(OnFinalize);
        hide = false; rendered = false; Update();
    }
}

internal sealed record GameLayout(float X, float Y, float Scale, float Width, float Height, bool Visible);
