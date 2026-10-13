using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace XLinUI;

// Keep native hit-testing, slot binding, drag/drop, cooldowns and tooltips.
// Stored pointers are identities only: restoration walks current live nodes first.
internal sealed unsafe class NativeUiStyling : IDisposable
{
    internal readonly List<NativeOutline> Outlines = [];
    internal readonly Dictionary<string, int> ActiveBars = [];
    internal readonly HashSet<string> StyledWindows = [];
    internal readonly List<NativeIconPaint> FlatPaint=[];
    private readonly Dictionary<string,List<NativeIconPaint>> windowPaint=[];
    private readonly HashSet<string> knownWindows=[];
    private readonly List<(string Name,Vector2 Position,Vector2 Size)> windowRects=[];
    private bool Occluded(string owner,Vector2 pos,Vector2 size)
    {
        foreach(var w in windowRects)
            if(w.Name!=owner&&pos.X<w.Position.X+w.Size.X&&pos.X+size.X>w.Position.X&&pos.Y<w.Position.Y+w.Size.Y&&pos.Y+size.Y>w.Position.Y)return true;
        return false;
    }
    private readonly NativeAppearanceState originals = new();
    private readonly NativeTextState textOriginals = new();
    private long heartbeat;
    private bool rendering;
    private ulong budgetFrame;
    private int walks;
    private readonly Dictionary<string,long> nextWalk=[];
    private readonly HashSet<string> inspectedWindows=[];
    private bool CanWalk(string name,bool live)
    {
        if(!live)return true;
        var frame=Plugin.Interface.UiBuilder.FrameCount;if(frame!=budgetFrame){budgetFrame=frame;walks=0;}
        var now=Stopwatch.GetTimestamp();
        if(walks>=2||(nextWalk.TryGetValue(name,out var due)&&now<due))return false;
        walks++;nextWalk[name]=now+Stopwatch.Frequency/10;return true;
    }
    internal void Register()
    {
        Plugin.Addons.RegisterListener(AddonEvent.PreDraw, OnDraw);
        Plugin.Addons.RegisterListener(AddonEvent.PreFinalize, OnFinalize);
    }
    internal void Rendered(bool value) { rendering = value; heartbeat = Stopwatch.GetTimestamp(); Update(); }
    private bool Live => rendering && Plugin.InstanceConfig.Enabled && Plugin.Objects.LocalPlayer is not null && !Plugin.GameGui.GameUiHidden && Stopwatch.GetElapsedTime(heartbeat).TotalMilliseconds < 500;
    internal void Update()
    {
        Outlines.Clear(); ActiveBars.Clear();FlatPaint.Clear();windowRects.Clear();
        var module = RaptureAtkModule.Instance();
        if (module == null) return;
        var units = module->RaptureAtkUnitManager.AtkUnitManager.AllLoadedUnitsList;
        for(var i=0;i<Math.Min((int)units.Count,256);i++)
        {
            var addon=units.Entries[i].Value;if(addon==null||addon->RootNode==null||!addon->IsVisible||!addon->RootNode->IsVisible())continue;
            var name=addon->NameString;
            if(inspectedWindows.Add(name))
            {
                var nodes=addon->UldManager.NodeList;
                for(var j=0;nodes!=null&&j<Math.Min((int)addon->UldManager.NodeListCount,48);j++)
                {var node=nodes[j];if(node==null||(int)node->Type<1000)continue;var component=node->GetComponent();if(component!=null&&component->GetComponentType()==ComponentType.Window){knownWindows.Add(name);break;}}
            }
            if(knownWindows.Contains(name))windowRects.Add((name,new(addon->X,addon->Y),new Vector2(addon->RootNode->Width,addon->RootNode->Height)*addon->GetScale()));
        }
        for (var i = 0; i < Math.Min((int)units.Count, 256); i++)
        {
            var addon = units.Entries[i].Value;
            if(addon!=null&&addon->RootNode!=null)
            {
                Apply(addon,Live,true);
                if(Live&&addon->IsVisible&&addon->RootNode->IsVisible()&&windowPaint.TryGetValue(addon->NameString,out var painted))
                    foreach(var icon in painted)if(!Occluded(icon.Name,icon.Position,icon.Size))FlatPaint.Add(icon);
            }
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
        textOriginals.Forget(args.AddonName);
        windowPaint.Remove(args.AddonName);knownWindows.Remove(args.AddonName);inspectedWindows.Remove(args.AddonName);
        nextWalk.Remove(args.AddonName);
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
            var module=RaptureHotbarModule.Instance();
            for (var i = 0; i < count; i++)
            {
                var slot = bar->ActionBarSlotVector[i];
                var frame = slot.IconFrame;
                if(slot.ComponentDragDrop!=null&&slot.ComponentDragDrop->AtkComponentIcon!=null)
                {
                    var nativeIcon=slot.ComponentDragDrop->AtkComponentIcon;
                    var key=collect&&slot.ControlHintTextNode!=null&&slot.ControlHintTextNode->IsVisible()?slot.ControlHintTextNode->NodeText.ToString():"";
                    var cooldown=0;var hotbarSlot=module==null?null:module->GetSlotById(bar->RaptureHotbarId,(uint)i);
                    if(collect&&hotbarSlot!=null&&!hotbarSlot->IsEmpty)hotbarSlot->GetSlotActionCooldownPercentage(&cooldown);
                    PaintIcon(addon,nativeIcon,live&&visible&&c.FlatNativeIcons&&!c.TextOnly&&!c.ExcludedNativeWindows.Contains(name),key,cooldown,collect?FlatPaint:null);
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
            var budget = 4096;
            if(!collect&&CanWalk(name,live))WalkText(addon, &addon->UldManager, live && visible && c.NativeTextOutline && !c.ExcludedNativeWindows.Contains(name), 0, ref budget);
        }
        if (collect && live && visible && c.SquareMinimap && !c.ExcludedNativeWindows.Contains(name) && name == "_NaviMap")
        {
            var map = (AddonNaviMap*)addon;
            AddOutline(name, map->MapBase != null ? (AtkResNode*)map->MapBase : addon->RootNode, 0);
        }
        // The collect pass reads hotbar geometry only. Deep styling is budgeted in the native PreDraw callback.
        if(collect)return;
        if(!knownWindows.Contains(name)&&!originals.Contains(name)&&!textOriginals.Contains(name))return;
        if(!CanWalk(name,live))return;
        if(windowPaint.TryGetValue(name,out var oldPaint))oldPaint.Clear();
        if (!(live && visible && (c.StyleNativeWindows || c.ForceTransparentNative || c.NativeTextOutline || c.FlatNativeIcons || c.FlatNativeButtons)) && !originals.Contains(name) && !textOriginals.Contains(name)) return;
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
            var textBudget = 4096;
            WalkText(addon, &component->UldManager, live && visible && c.NativeTextOutline && !c.ExcludedNativeWindows.Contains(name), 0, ref textBudget);
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
            var saved = originals.Original(addon->NameString, (nint)addon, (nint)node, current);
            var alpha = hide ? (byte)0 : SurfacePolicy.NativeAlpha(saved.Alpha, c.ForceTransparentNative, c.TextOnly, c.Glass, c.Opacity);
            desired = hide || c.ForceTransparentNative || !c.StyleNativeWindows ? saved with { Alpha = alpha } : new NativeAppearance(alpha,
                (byte)(45 + c.Tint.X * 45), (byte)(45 + c.Tint.Y * 45), (byte)(45 + c.Tint.Z * 45));
        }
        var change = originals.Change(addon->NameString, (nint)addon, (nint)node, current, desired);
        if (change is { } value)
        {
            if (node->Color.A == value.Alpha && node->MultiplyRed == value.Red && node->MultiplyGreen == value.Green && node->MultiplyBlue == value.Blue) return;
            node->Color.A = value.Alpha; node->MultiplyRed = value.Red; node->MultiplyGreen = value.Green; node->MultiplyBlue = value.Blue;
            node->IsDirty = true;
        }
    }
    private void WalkText(AtkUnitBase* addon, AtkUldManager* manager, bool enabled, int depth, ref int budget)
    {
        if (depth > 6 || manager == null || manager->NodeList == null) return;
        for (var i = 0; i < Math.Min((int)manager->NodeListCount, 2048) && budget > 0; i++)
        {
            budget--;
            var node = manager->NodeList[i];
            if (node == null) continue;
            if (node->Type == NodeType.Text)
            {
                var text = (AtkTextNode*)node;
                var current = new TextAppearance(text->EdgeColor.RGBA, (ushort)text->TextFlags);
                var change = textOriginals.Change(addon->NameString, (nint)addon, (nint)node, current, enabled);
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
                    // Generic lists may scroll or overlap between budgeted scans. Keep their native
                    // artwork until an addon-specific draw lifecycle can guarantee exact clipping.
                    if(kind==ComponentType.Button)
                    {
                        var parts=component->UldManager.NodeList;
                        for(var j=0;parts!=null&&j<Math.Min((int)component->UldManager.NodeListCount,64);j++)
                        {
                            var part=parts[j];if(part!=null&&part->Type==NodeType.NineGrid&&part->ChildNode==null)
                                Change(addon,part,Live&&Plugin.InstanceConfig.FlatNativeButtons&&!Plugin.InstanceConfig.ExcludedNativeWindows.Contains(addon->NameString),true);
                        }
                    }
                    WalkText(addon,&component->UldManager,enabled,depth+1,ref budget);
                }
            }
        }
    }
    private void PaintIcon(AtkUnitBase* addon,AtkComponentIcon* icon,bool enabled,string key,int cooldown,List<NativeIconPaint>? destination)
    {
        var image=icon->IconImage;if(image==null)return;
        var node=(AtkResNode*)image;var pos=new Vector2(node->ScreenX,node->ScreenY);var sx=1f;var sy=1f;var visible=addon->IsVisible;
        var parent=node;
        for(var depth=0;parent!=null&&depth<32;parent=parent->ParentNode,depth++){sx*=parent->ScaleX;sy*=parent->ScaleY;visible&=parent->IsVisible();}
        var size=new Vector2(node->Width*Math.Abs(sx),node->Height*Math.Abs(sy));
        // Scrolled/clipped or overlapping native windows keep native art and all native overlays.
        enabled&=visible&&node->ClipCount==0&&HudGeometry.Valid(pos,size)&&!Occluded(addon->NameString,pos,size);
        var texture=enabled?Plugin.FlatCache.Get(icon->IconId):null;
        var saved=originals.Original(addon->NameString,(nint)addon,(nint)node,new(node->Color.A,node->MultiplyRed,node->MultiplyGreen,node->MultiplyBlue));
        Change(addon,node,texture is not null,true);
        if(texture is null||destination is null)return;
        var quantity=icon->QuantityText!=null&&icon->QuantityText->IsVisible()?icon->QuantityText->NodeText.ToString():"";
        var tint=new Vector4(Math.Clamp(node->MultiplyRed/100f,0,1),Math.Clamp(node->MultiplyGreen/100f,0,1),Math.Clamp(node->MultiplyBlue/100f,0,1),saved.Alpha/255f);
        destination.Add(new(addon->NameString,icon->IconId,pos,size,tint,quantity,key,Math.Clamp(cooldown,0,100),icon->ComboBorder!=null&&icon->ComboBorder->IsVisible()));
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
        originals.Clear(); textOriginals.Clear();
    }
}
internal readonly record struct NativeOutline(string Name, Vector2 Position, Vector2 Size, float Radius);
