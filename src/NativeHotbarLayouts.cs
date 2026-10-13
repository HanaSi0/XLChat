using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Interface.Utility;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
namespace XLinUI;
internal readonly record struct HotbarView(int Index,string Name,Vector2 Position,Vector2 Size,float Scale,bool Visible,string Shape);
// Move/scale whole native addons, preserving every slot component and drag/drop owner.
internal sealed unsafe class NativeHotbarLayouts : IDisposable
{
    internal readonly List<HotbarView> Bars=[];
    private readonly NativeHotbarState originals=new();
    private bool disposed;
    internal void Register()=>Plugin.Addons.RegisterListener(AddonEvent.PreFinalize,OnFinalize);
    private void OnFinalize(AddonEvent type,AddonArgs args)=>originals.Forget(args.AddonName);
    internal bool NativeEditorActive {get;private set;}
    internal string Failure {get;private set;}="";
    internal void Update(bool active)
    {
        if(Failure.Length>0)return;
        try{UpdateCore(active);}
        catch(Exception ex)
        {
            Failure="热键栏接口暂不可用，已暂停布局控制；请查看 /xllog 并重新加载插件。";
            Plugin.InstanceConfig.UseHotbarLayouts=false;Plugin.Interface.SavePluginConfig(Plugin.InstanceConfig);
            Release();Bars.Clear();Plugin.Log.Error(ex,"XLinUI 热键栏布局接口失败");
        }
    }
    private void UpdateCore(bool active)
    {
        Bars.Clear();if(disposed)return;
        if(!active){Release();NativeEditorActive=false;return;}
        if(Plugin.Objects.LocalPlayer is null){Release();NativeEditorActive=false;return;}
        var c=Plugin.InstanceConfig;var viewport=ImGuiHelpers.MainViewport.Size;
        var editor=AgentHUDLayout.Instance();NativeEditorActive=editor!=null&&editor->IsAgentActive();
        // Let the game editor take ownership, and require explicit import before reapplying.
        if(active&&NativeEditorActive&&c.UseHotbarLayouts)
        {c.UseHotbarLayouts=false;originals.Clear();Plugin.Interface.SavePluginConfig(c);}
        for(var index=0;index<10;index++)
        {
            var name=HotbarPolicy.Name(index);var addon=(AtkUnitBase*)Plugin.GameGui.GetAddonByName(name).Address;
            if(addon==null||addon->RootNode==null){originals.Forget(name);continue;}
            var root=addon->RootNode;var position=new Vector2(addon->X,addon->Y);var scale=addon->Scale;
            var size=new Vector2(root->Width,root->Height)*addon->GetScale();
            if(!float.IsFinite(scale)||scale<=0||!HudGeometry.Valid(position,size)){Restore(name,addon);continue;}
            var owned=active&&!NativeEditorActive&&c.NativeInteractions&&c.UseHotbarLayouts&&c.HotbarLayouts.TryGetValue(name,out var requested)&&requested.Enabled;
            if(owned)
            {
                var layout=c.HotbarLayouts[name];
                originals.Original(name,(nint)addon,(nint)root,position,scale);
                var adjustedSize=size*(layout.Scale/Math.Max(.01f,scale));
                var desired=HotbarPolicy.Position(layout.Position,adjustedSize,viewport,c.HotbarGrid,false);
                if(Math.Abs(scale-layout.Scale)>.001f)addon->SetScale(layout.Scale,false);
                if(Math.Abs(position.X-desired.X)>=1||Math.Abs(position.Y-desired.Y)>=1)addon->SetPosition((short)desired.X,(short)desired.Y);
                position=desired;scale=layout.Scale;size=adjustedSize;
            }
            else Restore(name,addon);
            position=new(addon->X,addon->Y);scale=addon->Scale;size=new Vector2(root->Width,root->Height)*addon->GetScale();
            var shape=index==0?((AddonActionBar*)addon)->ActionBarLayout:((AddonActionBarX*)addon)->ActionBarLayout;
            Bars.Add(new(index,name,position,size,scale,addon->IsVisible&&root->IsVisible(),shape.ToString().Replace("Layout","")));
        }
    }
    private void Restore(string name,AtkUnitBase* addon)
    {
        var saved=originals.Restore(name,(nint)addon,(nint)addon->RootNode);if(saved is null)return;
        addon->SetScale(saved.Scale,false);addon->SetPosition((short)saved.Position.X,(short)saved.Position.Y);
    }
    internal void Release()
    {
        foreach(var name in originals.Keys)
        {
            try
            {
                var addon=(AtkUnitBase*)Plugin.GameGui.GetAddonByName(name).Address;
                if(addon!=null&&addon->RootNode!=null)Restore(name,addon);else originals.Forget(name);
            }
            catch(Exception ex){originals.Forget(name);Plugin.Log.Error(ex,"XLinUI 恢复原生热键栏失败：{Name}",name);}
        }
    }
    public void Dispose(){Plugin.Addons.UnregisterListener(OnFinalize);Release();disposed=true;Bars.Clear();}
}
