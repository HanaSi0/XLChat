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
    private readonly NativeTextState textOriginals = new();
    private long heartbeat;
    private bool rendering;
    private bool previousLive;
    private int nextStyleUnit;
    private bool frameLive;
    internal void Register()
    {
        Plugin.Addons.RegisterListener(AddonEvent.PreFinalize, OnFinalize);
    }
    internal void Rendered(bool value) { rendering = value; heartbeat = Stopwatch.GetTimestamp(); Update(); }
    private bool ShouldBeLive => rendering && Plugin.InstanceConfig.Enabled && Plugin.Objects.LocalPlayer is not null && !Plugin.GameGui.GameUiHidden && Stopwatch.GetElapsedTime(heartbeat).TotalMilliseconds < 500;
    internal void Update()
    {
        frameLive=ShouldBeLive;Plugin.NativeRedraw.SetEnabled(frameLive);
        Outlines.Clear(); ActiveBars.Clear();
        if(!frameLive&&!previousLive)return;
        var restoreAll=!frameLive&&previousLive;previousLive=frameLive;
        var module = RaptureAtkModule.Instance();
        if (module == null) return;
        var units = module->RaptureAtkUnitManager.AtkUnitManager.AllLoadedUnitsList;
        for (var i = 0; i < Math.Min((int)units.Count, 256); i++)
        {
            var addon = units.Entries[i].Value;
            if(addon!=null&&addon->RootNode!=null)
            {
                Apply(addon,frameLive,true);
                if(restoreAll)Apply(addon,false,false);
            }
        }
        if(frameLive&&units.Count>0)
        {
            for(int styled=0,attempts=0;styled<2&&attempts<Math.Min((int)units.Count,256);attempts++)
            {
                if(nextStyleUnit>=Math.Min((int)units.Count,256))nextStyleUnit=0;
                var addon=units.Entries[nextStyleUnit++].Value;
                if(addon==null||addon->RootNode==null||!addon->IsVisible)continue;
                using var timing=Plugin.Metrics.Measure(UiWork.NativeCallback);
                Apply(addon,true,false);styled++;
            }
        }
    }
    private void OnFinalize(AddonEvent type, AddonArgs args)
    {
        originals.Forget(args.AddonName);
        textOriginals.Forget(args.AddonName);
        ActiveBars.Remove(args.AddonName); StyledWindows.Remove(args.AddonName);
    }
    private void Apply(AtkUnitBase* addon, bool live, bool collect)
    {
        var c = Plugin.InstanceConfig;
        var name = Plugin.Names.Get(addon);
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
                if(slot.ComponentDragDrop!=null&&slot.ComponentDragDrop->AtkComponentIcon!=null)
                {
                    var nativeIcon=slot.ComponentDragDrop->AtkComponentIcon;
                    PaintIcon(addon,nativeIcon,live&&visible&&c.FlatNativeIcons&&!c.ExcludedNativeWindows.Contains(name));
                }
                // Never suppress a container; a client variant may place content under it.
                if (frame != null && frame->ChildNode == null && (frame->Type == NodeType.Image || frame->Type == NodeType.NineGrid))
                    Change(addon, frame, live && visible && c.SquareHotbars && !c.ExcludedNativeWindows.Contains(name), true);
                var icon = (AtkResNode*)slot.Icon;
                if (collect && live && visible && c.SquareHotbars && !c.ExcludedNativeWindows.Contains(name) && icon != null && icon->IsVisible())
                    AddOutline(name, icon, 0);
            }
        }
        if (name == "_PartyList")
        {
            var background = (AtkResNode*)((AddonPartyList*)addon)->BackgroundNineGridNode;
            if (background != null) Change(addon, background, live && visible && (c.StyleNativeWindows || c.ForceTransparentNative) && !c.ExcludedNativeWindows.Contains(name), false);
        }
        if (collect && live && visible && c.SquareMinimap && !c.ExcludedNativeWindows.Contains(name) && name == "_NaviMap")
        {
            var map = (AddonNaviMap*)addon;
            AddOutline(name, map->MapBase != null ? (AtkResNode*)map->MapBase : addon->RootNode, 0);
        }
        // Discovery is round-robin, never a full recursive traversal for every native draw callback.
        if(collect)return;
        if (!(live && visible && (c.StyleNativeWindows || c.ForceTransparentNative || c.NativeTextOutline || c.FlatNativeIcons || c.FlatNativeButtons)) && !originals.Contains(name) && !textOriginals.Contains(name)) return;
        var rootBudget=4096;
        WalkText(addon,&addon->UldManager,live&&visible&&c.NativeTextOutline&&!c.ExcludedNativeWindows.Contains(name),0,ref rootBudget);
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
                if (part == null || part->Type != NodeType.NineGrid || part->ChildNode != null) continue;
                var enabled = live && visible && (c.StyleNativeWindows || c.ForceTransparentNative) && !c.ExcludedNativeWindows.Contains(name);
                Change(addon, part, enabled, false);
                styled |= enabled;
            }
            if(styled)StyledWindows.Add(name);else StyledWindows.Remove(name);
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
            var saved = originals.Original(Plugin.Names.Get(addon), (nint)addon, (nint)node, current);
            var alpha = hide ? (byte)0 : SurfacePolicy.NativeAlpha(saved.Alpha, c.ForceTransparentNative, c.TextOnly, c.Glass, c.Opacity);
            desired = hide || c.ForceTransparentNative || !c.StyleNativeWindows ? saved with { Alpha = alpha } : new NativeAppearance(alpha,
                (byte)(45 + c.Tint.X * 45), (byte)(45 + c.Tint.Y * 45), (byte)(45 + c.Tint.Z * 45));
        }
        var change = originals.Change(Plugin.Names.Get(addon), (nint)addon, (nint)node, current, desired);
        if (change is { } value)
        {
            if (node->Color.A == value.Alpha && node->MultiplyRed == value.Red && node->MultiplyGreen == value.Green && node->MultiplyBlue == value.Blue) return;
            node->Color.A = value.Alpha; node->MultiplyRed = value.Red; node->MultiplyGreen = value.Green; node->MultiplyBlue = value.Blue;
            node->IsDirty = true;
        }
    }
    private void WalkText(AtkUnitBase* addon, AtkUldManager* manager, bool enabled, int depth, ref int budget,bool buttonScope=false)
    {
        if (depth > 6 || manager == null || manager->NodeList == null) return;
        var allowed=frameLive&&!Plugin.InstanceConfig.ExcludedNativeWindows.Contains(Plugin.Names.Get(addon));
        if(allowed&&Plugin.InstanceConfig.FlatNativeIcons)
            for(var a=0;manager->Assets!=null&&a<Math.Min((int)manager->AssetCount,1024);a++)Plugin.NativeRedraw.Icon(&manager->Assets[a]);
        for (var i = 0; i < Math.Min((int)manager->NodeListCount, 2048) && budget > 0; i++)
        {
            budget--;
            var node = manager->NodeList[i];
            if (node == null) continue;
            if(buttonScope&&allowed&&Plugin.InstanceConfig.FlatNativeButtons&&node->Type==NodeType.Image)
                if(Plugin.NativeRedraw.Glyph((AtkImageNode*)node))node->IsDirty=true;
            if (node->Type == NodeType.Text)
            {
                var text = (AtkTextNode*)node;
                var current = new TextAppearance(text->EdgeColor.RGBA, (ushort)text->TextFlags);
                var change = textOriginals.Change(Plugin.Names.Get(addon), (nint)addon, (nint)node, current, enabled);
                if (change is { } value && (text->EdgeColor.RGBA != value.EdgeColor || (ushort)text->TextFlags != value.Flags))
                {
                    text->EdgeColor.RGBA = value.EdgeColor; text->TextFlags = (TextFlags)value.Flags;
                    node->IsDirty = true;
                }
            }
            else if ((int)node->Type >= 1000)
            {
                var component = node->GetComponent();
                if(component != null)
                {
                    var kind=component->GetComponentType();
                    if(allowed&&Plugin.InstanceConfig.FlatNativeIcons&&(kind is ComponentType.Icon or ComponentType.DragDrop))
                    {
                        var icon=kind==ComponentType.Icon?(AtkComponentIcon*)component:((AtkComponentDragDrop*)component)->AtkComponentIcon;
                        if(icon!=null)PaintIcon(addon,icon,true);
                    }
                    var isButton=kind is ComponentType.Button or ComponentType.RadioButton or ComponentType.CheckBox or ComponentType.ListItemRenderer or ComponentType.HoldButton;
                    if(isButton)
                    {
                        var parts=component->UldManager.NodeList;
                        for(var j=0;parts!=null&&j<Math.Min((int)component->UldManager.NodeListCount,64);j++)
                        {
                            var part=parts[j];if(part!=null&&part->Type==NodeType.NineGrid&&part->ChildNode==null)
                            {
                                Change(addon,part,false,true);
                                if(frameLive&&Plugin.InstanceConfig.FlatNativeButtons&&!Plugin.InstanceConfig.ExcludedNativeWindows.Contains(Plugin.Names.Get(addon))&&Plugin.NativeRedraw.Button((AtkNineGridNode*)part))part->IsDirty=true;
                            }
                        }
                    }
                    WalkText(addon,&component->UldManager,enabled,depth+1,ref budget,buttonScope||isButton);
                }
            }
        }
    }
    private void PaintIcon(AtkUnitBase* addon,AtkComponentIcon* icon,bool enabled)
    {
        if(!enabled)return;
        var image=icon->IconImage;if(image==null||icon->Texture==null)return;
        if(Plugin.NativeRedraw.Icon(icon->Texture,icon->IconId))((AtkResNode*)image)->IsDirty=true;
        if(Plugin.InstanceConfig.FlatNativeButtons)
        {
            if(Plugin.NativeRedraw.Frame(icon->Frame))icon->Frame->IsDirty=true;
            if(Plugin.NativeRedraw.Frame(icon->ComboBorder))icon->ComboBorder->IsDirty=true;
        }
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
        Plugin.Addons.UnregisterListener(OnFinalize);
        originals.Clear(); textOriginals.Clear();
    }
}
internal readonly record struct NativeOutline(string Name, Vector2 Position, Vector2 Size, float Radius);
