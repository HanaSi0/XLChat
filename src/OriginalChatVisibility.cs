using System;
using System.Diagnostics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace XLinUI;

// Only hide rendering nodes. Do not close chat addons or suppress incoming messages.
internal sealed unsafe class OriginalChatVisibility : IDisposable
{
    private static readonly string[] Names = ["ChatLog", "ChatLogPanel_0", "ChatLogPanel_1", "ChatLogPanel_2", "ChatLogPanel_3"];
    private readonly ChatVisibilityState state = new();
    private bool desired;
    private long lastDraw;

    internal void Register()
    {
        foreach (var name in Names)
        {
            Plugin.Addons.RegisterListener(AddonEvent.PreDraw, name, OnPreDraw);
            Plugin.Addons.RegisterListener(AddonEvent.PreFinalize, name, OnFinalize);
        }
    }

    internal void SetDesired(bool hide)
    {
        desired = hide;
        lastDraw = Stopwatch.GetTimestamp();
        Update();
    }

    internal void Update()
    {
        // UiBuilder may stop drawing during cutscenes or when the game HUD is hidden.
        var hide = desired && Stopwatch.GetElapsedTime(lastDraw).TotalMilliseconds < 500 &&
            !Plugin.GameGui.GameUiHidden && Plugin.Objects.LocalPlayer is not null;
        foreach (var name in Names) Apply(name, hide);
    }

    private void OnPreDraw(AddonEvent type, AddonArgs args) => Apply(args.AddonName, desired && Stopwatch.GetElapsedTime(lastDraw).TotalMilliseconds < 500 && !Plugin.GameGui.GameUiHidden && Plugin.Objects.LocalPlayer is not null);
    private void OnFinalize(AddonEvent type, AddonArgs args) => state.Forget(args.AddonName);

    private void Apply(string name, bool hide)
    {
        var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(name).Address;
        if (addon == null || addon->RootNode == null)
        { state.Forget(name); return; }
        var node = addon->RootNode;
        var visible = state.Change(name, (nint)addon, (nint)node, node->IsVisible(), hide);
        if (visible.HasValue) node->ToggleVisibility(visible.Value);
    }

    public void Dispose()
    {
        Plugin.Addons.UnregisterListener(OnPreDraw);
        Plugin.Addons.UnregisterListener(OnFinalize);
        foreach (var name in Names) Apply(name, false);
    }

}
