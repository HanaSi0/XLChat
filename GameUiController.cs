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
    private readonly ChatVisibilityState state = new();
    private bool hide;
    private long lastDraw;
    private bool active;

    internal void Register()
    {
        Plugin.Addons.RegisterListener(AddonEvent.PreDraw, OnDraw);
        Plugin.Addons.RegisterListener(AddonEvent.PreFinalize, OnFinalize);
    }

    internal void SetHide(bool value)
    {
        hide = value; lastDraw = Stopwatch.GetTimestamp(); Update();
    }

    internal void Update()
    {
        var module = RaptureAtkModule.Instance();
        if (module == null) return;
        var manager = &module->RaptureAtkUnitManager.AtkUnitManager;
        GameScale = manager->UiBaseScale;
        var loggedIn = Plugin.Objects.LocalPlayer is not null;
        var desired = hide && loggedIn && !Plugin.GameGui.GameUiHidden && Stopwatch.GetElapsedTime(lastDraw).TotalMilliseconds < 500;
        var units = manager->AllLoadedUnitsList;
        for (var i = 0; i < Math.Min((int)units.Count, 256); i++)
        {
            var addon = units.Entries[i].Value;
            if (addon == null || addon->RootNode == null) continue;
            Apply(addon, desired, loggedIn);

        }
        active = desired;
        if (!loggedIn) Layouts.Clear();
    }

    private void OnDraw(AddonEvent type, AddonArgs args)
    {
        if (!hide && !active) return;
        var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(args.AddonName).Address;
        if (addon == null || addon->RootNode == null) return;
        var desired = hide && Plugin.Objects.LocalPlayer is not null && !Plugin.GameGui.GameUiHidden && Stopwatch.GetElapsedTime(lastDraw).TotalMilliseconds < 500;
        Apply(addon, desired, Plugin.Objects.LocalPlayer is not null);
    }

    private void Apply(AtkUnitBase* addon, bool desired, bool loggedIn)
    {
        var name = addon->NameString;
        var node = addon->RootNode;
        if (loggedIn && !Layouts.ContainsKey(name))
            Layouts[name] = new GameLayout(addon->X, addon->Y, addon->Scale, node->Width, node->Height, node->IsVisible());
        if (!desired && !active) return;
        var visible = state.Change(name, (nint)addon, (nint)node, node->IsVisible(), desired);
        if (visible.HasValue) node->ToggleVisibility(visible.Value);
    }
    private void OnFinalize(AddonEvent type, AddonArgs args) => state.Forget(args.AddonName);

    public void Dispose()
    {
        Plugin.Addons.UnregisterListener(OnDraw);
        Plugin.Addons.UnregisterListener(OnFinalize);
        hide = false; Update();
    }
}

internal sealed record GameLayout(float X, float Y, float Scale, float Width, float Height, bool Visible);
