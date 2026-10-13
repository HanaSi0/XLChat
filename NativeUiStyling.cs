using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace XLinUI;

// Keep native hit-testing, slot binding, drag/drop, cooldowns and tooltips.
// Stored pointers are identities only: restoration walks current live nodes first.
internal sealed unsafe class NativeUiStyling : IDisposable
{
    internal readonly List<NativeOutline> Outlines = [];
    internal readonly Dictionary<string, int> ActiveBars = [];
    internal readonly HashSet<string> StyledWindows = [];
    private readonly NativeAppearanceState originals = new();
    private long heartbeat;
    private bool rendering;
    internal void Register()
    {
        Plugin.Addons.RegisterListener(AddonEvent.PreDraw, OnDraw);
        Plugin.Addons.RegisterListener(AddonEvent.PreFinalize, OnFinalize);
    }
    internal void Rendered(bool value) { rendering = value; heartbeat = Stopwatch.GetTimestamp(); Update(); }
    private bool Live => rendering && Plugin.InstanceConfig.Enabled && Plugin.Objects.LocalPlayer is not null && !Plugin.GameGui.GameUiHidden && Stopwatch.GetElapsedTime(heartbeat).TotalMilliseconds < 500;
    internal void Update()
    {
        Outlines.Clear(); ActiveBars.Clear(); StyledWindows.Clear();
        var module = RaptureAtkModule.Instance();
        if (module == null) return;
        var units = module->RaptureAtkUnitManager.AtkUnitManager.AllLoadedUnitsList;
        for (var i = 0; i < Math.Min((int)units.Count, 256); i++)
        {
            var addon = units.Entries[i].Value;
            if (addon != null && addon->RootNode != null) Apply(addon, Live, true);
        }
    }
    private void OnDraw(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)Plugin.GameGui.GetAddonByName(args.AddonName).Address;
        if (addon != null && addon->RootNode != null) Apply(addon, Live, false);
    }
    private void OnFinalize(AddonEvent type, AddonArgs args)
    {
        originals.Forget(args.AddonName);
        ActiveBars.Remove(args.AddonName); StyledWindows.Remove(args.AddonName);
    }
    private void Apply(AtkUnitBase* addon, bool live, bool collect)
    {
        var c = Plugin.InstanceConfig;
        var name = addon->NameString;
        var visible = addon->IsVisible && addon->RootNode->IsVisible();
        if (HudGeometry.IsStandardHotbar(name))
        {
            var bar = (AddonActionBarBase*)addon;
            var count = Math.Min((int)bar->SlotCount, 12);
            if (bar->ActionBarSlotVector.LongCount < count) return;
            if (collect && visible) ActiveBars[name] = count;
            for (var i = 0; i < count; i++)
            {
                var slot = bar->ActionBarSlotVector[i];
                var frame = slot.IconFrame;
                // Never suppress a container; a client variant may place content under it.
                if (frame != null && frame->ChildNode == null && (frame->Type == NodeType.Image || frame->Type == NodeType.NineGrid))
                    Change(addon, frame, live && visible && c.SquareHotbars && !c.ExcludedNativeWindows.Contains(name), true);
                var icon = (AtkResNode*)slot.Icon;
                if (collect && live && visible && c.SquareHotbars && !c.ExcludedNativeWindows.Contains(name) && icon != null && icon->IsVisible())
                    AddOutline(name, icon, 0);
            }
        }
        if (collect && live && visible && c.SquareMinimap && !c.ExcludedNativeWindows.Contains(name) && name == "_NaviMap") AddOutline(name, addon->RootNode, 0);
        if (!(live && visible && c.StyleNativeWindows) && !originals.Contains(name)) return;
        // Only style standard Window components, never icons, gauges, list rows or collision nodes.
        var list = addon->UldManager.NodeList;
        for (var i = 0; list != null && i < Math.Min((int)addon->UldManager.NodeListCount, 2048); i++)
        {
            var node = list[i];
            if (node == null || (int)node->Type < 1000) continue;
            var component = node->GetComponent();
            if (component == null || component->GetComponentType() != ComponentType.Window) continue;
            var nodes = component->UldManager.NodeList;
            var styled = false;
            for (var j = 0; nodes != null && j < Math.Min((int)component->UldManager.NodeListCount, 256); j++)
            {
                var part = nodes[j];
                if (part == null || part->Type != NodeType.NineGrid) continue;
                var enabled = live && visible && c.StyleNativeWindows && !c.ExcludedNativeWindows.Contains(name);
                Change(addon, part, enabled, false);
                styled |= enabled;
            }
            if (styled && collect) StyledWindows.Add(name);
            if (styled && collect && !c.HideBorders && !c.TextOnly) AddOutline(name, node, c.Rounded ? 6 : 0);
        }
    }
    private void Change(AtkUnitBase* addon, AtkResNode* node, bool enabled, bool hide)
    {
        var current = new NativeAppearance(node->Color.A, node->MultiplyRed, node->MultiplyGreen, node->MultiplyBlue);
        NativeAppearance? desired = null;
        if (enabled)
        {
            var c = Plugin.InstanceConfig;
            var saved = originals.Original(addon->NameString, (nint)addon, (nint)node, current);
            var alpha = hide || c.TextOnly || !c.Glass ? (byte)0 : (byte)(saved.Alpha * c.Opacity);
            desired = hide ? saved with { Alpha = 0 } : new NativeAppearance(alpha,
                (byte)(45 + c.Tint.X * 45), (byte)(45 + c.Tint.Y * 45), (byte)(45 + c.Tint.Z * 45));
        }
        var change = originals.Change(addon->NameString, (nint)addon, (nint)node, current, desired);
        if (change is { } value)
        { node->Color.A = value.Alpha; node->MultiplyRed = value.Red; node->MultiplyGreen = value.Green; node->MultiplyBlue = value.Blue; }
    }
    private void AddOutline(string name, AtkResNode* node, float radius)
    {
        var sx = 1f; var sy = 1f; var parent = node;
        for (var depth = 0; parent != null && depth < 32; depth++, parent = parent->ParentNode)
        { sx *= parent->ScaleX; sy *= parent->ScaleY; }
        var size = new Vector2(node->Width * Math.Abs(sx), node->Height * Math.Abs(sy));
        var pos = new Vector2(node->ScreenX, node->ScreenY);
        if (HudGeometry.Valid(pos, size)) Outlines.Add(new NativeOutline(name, pos, size, radius));
    }
    public void Dispose()
    {
        rendering = false; Update();
        Plugin.Addons.UnregisterListener(OnDraw); Plugin.Addons.UnregisterListener(OnFinalize);
        originals.Clear();
    }
}
internal sealed record NativeOutline(string Name, Vector2 Position, Vector2 Size, float Radius);
