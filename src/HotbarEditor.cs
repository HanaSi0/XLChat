using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
namespace XLinUI;
internal sealed partial class UserInterface
{
    private bool hotbarEditing;
    private int selectedHotbar;
    private readonly HashSet<string> selectedBars=[];
    private readonly Dictionary<string,Vector2> dragOrigins=[];
    private Vector2 dragMouse;
    private bool hotbarDragged;
    private void ImportHotbars()
    {
        plugin.Hotbars.Release();plugin.Hotbars.Update(false);
        C.HotbarLayouts.Clear();
        foreach(var bar in plugin.Hotbars.Bars)C.HotbarLayouts[bar.Name]=new(){Position=bar.Position,Scale=bar.Scale};
        C.NativeCombatWidgets=true;C.UseHotbarLayouts=C.HotbarLayouts.Count>0;plugin.Save();
    }
    private void CloseHotbarEditing()
    {
        var save=hotbarEditing||hotbarDragged;
        hotbarEditing=false;hotbarDragged=false;selectedBars.Clear();dragOrigins.Clear();
        if(save)plugin.Save();
    }
    private bool CanEditHotbars=>C.Enabled&&C.NativeInteractions&&Plugin.Objects.LocalPlayer is not null&&!Plugin.GameGui.GameUiHidden&&!plugin.Hotbars.NativeEditorActive&&plugin.Hotbars.Failure.Length==0;
    private void DrawHotbarSettings()
    {
        ReadableText.TextWrapped("热键栏日常使用不显示拖柄与编辑选项。进入编辑模式后，在二级菜单调整所有热键栏。");
        ImGui.BeginDisabled(!CanEditHotbars);
        try
        {
            if(ReadableText.Button(hotbarEditing?"返回编辑菜单":"打开编辑模式"))
            {
                if(!C.UseHotbarLayouts)ImportHotbars();
                if(C.UseHotbarLayouts){hotbarEditing=true;SettingsOpen=false;}
            }
        }
        finally{ImGui.EndDisabled();}
        if(!CanEditHotbars)ReadableText.TextWrapped(plugin.Hotbars.Failure.Length>0?plugin.Hotbars.Failure:"请登录游戏并启用插件与游戏交互；原版 HUD 编辑完成后再打开此模式。");
    }
    private void DrawHotbarEditMenu()
    {
        var open=hotbarEditing;var changed=false;
        var visible=BeginPanel("热键栏编辑",ref open,new Vector2(610,650));
        try
        {
            if(!visible)return;
            if(ReadableText.Button("完成编辑")){CloseHotbarEditing();return;}
            ImGui.SameLine();
            if(ReadableText.Button("返回设置")){CloseHotbarEditing();SettingsOpen=true;return;}
            ReadableText.TextWrapped("拖柄仅在此模式显示。Shift 网格吸附，Ctrl 点击多选；完成或关闭菜单后隐藏全部拖柄，保留当前布局。");
            DrawHotbarEditOptions(ref changed);
        }
        finally
        {
            EndPanel();
            if(changed)plugin.Save();
            if(!open)CloseHotbarEditing();
        }
    }
    private void DrawHotbarEditOptions(ref bool changed)
    {
        changed|=Toggle("所有已显示热键栏采用直角边框",C.SquareHotbars,v=>C.SquareHotbars=v);
        changed|=Toggle("使用 XLinUI 保存的热键栏位置／缩放",C.UseHotbarLayouts,v=>C.UseHotbarLayouts=v);
        ChatSlider("Shift 吸附网格（游戏画面像素）",C.HotbarGrid,4,64,v=>C.HotbarGrid=v,ref changed);
        changed|=Toggle("按 Shift 时显示参考网格",C.ShowHotbarGrid,v=>C.ShowHotbarGrid=v);
        if(plugin.Hotbars.Failure.Length>0)ReadableText.TextWrapped(plugin.Hotbars.Failure);
        ReadableText.TextDisabled("游戏交互关闭时不应用布局；原版 HUD 编辑器打开后自动暂停 XLinUI 布局控制。");
        ImGui.BeginDisabled(!C.NativeInteractions||plugin.Hotbars.NativeEditorActive||plugin.Hotbars.Failure.Length>0);
        try
        {
            if(ReadableText.Button("读取当前原版布局")){ImportHotbars();changed=true;}
        }
        finally{ImGui.EndDisabled();}
        if(ReadableText.Button("原版 HUD 布局／横竖排列"))
        {
            CloseHotbarEditing();C.UseHotbarLayouts=false;C.ShowOriginalUi=true;plugin.Hotbars.Release();plugin.Save();Native(GameBridge.OpenHudEditor);changed=true;
        }
        ImGui.SameLine();if(ReadableText.Button("打开技能与特性")){C.ShowOriginalUi=true;plugin.Save();Native(GameBridge.OpenSkillList);}
        ReadableText.TextWrapped("从原版“技能与特性”窗口直接拖入格子即可。若游戏已锁定技能格，可在下方解除原版热键栏锁定；不会自动解除锁定。12×1、6×2、4×3、3×4、2×6、1×12 排列、共享栏与键位继续使用原版设置。");
        if(Plugin.GameConfig.UiConfig.TryGet("HotbarLock",out uint locked))
        {
            var isLocked=locked!=0;
            if(ReadableText.Checkbox("原版技能格锁定",ref isLocked))Native(()=>Plugin.GameConfig.UiConfig.Set("HotbarLock",isLocked?1u:0u));
        }
        var names=Enumerable.Range(1,10).Select(i=>$"热键栏 {i}").ToArray();
        ImGui.SetNextItemWidth(150*ImGuiHelpers.GlobalScale);ImGui.Combo("选择热键栏",ref selectedHotbar,names,10);
        var name=HotbarPolicy.Name(selectedHotbar);var bar=plugin.Hotbars.Bars.FirstOrDefault(b=>b.Name==name);
        if(bar.Name is null){ReadableText.TextDisabled("该栏尚未加载；可用下面的显示按钮启用，或进入原版 HUD 编辑器。");}
        else
        {
            ReadableText.TextUnformatted($"{bar.Shape} · 原版缩放 {bar.Scale:P0} · {(bar.Visible?"已显示":"已隐藏")}");
            if(C.HotbarLayouts.TryGetValue(name,out var layout))
            {
                changed|=Toggle("此栏使用自定义位置／缩放",layout.Enabled,v=>layout.Enabled=v);
                var position=layout.Position;
                if(ImGui.DragFloat2("位置 X / Y",ref position,1,0,32700,"%.0f"))
                {layout.Position=HotbarPolicy.Position(position,bar.Size,ImGuiHelpers.MainViewport.Size,C.HotbarGrid,ImGui.GetIO().KeyShift);changed=true;}
                var scale=layout.Scale;if(ImGui.SliderFloat("独立连续缩放",ref scale,.5f,2f,"%.2f x")){layout.Scale=scale;changed=true;}
                foreach(var (label,delta) in new[]{("←",new Vector2(-1,0)),("→",new Vector2(1,0)),("↑",new Vector2(0,-1)),("↓",new Vector2(0,1))})
                {
                    if(ReadableText.SmallButton(label+"###hotbar-nudge"+label))
                    {layout.Position=HotbarPolicy.Position(layout.Position+delta*(ImGui.GetIO().KeyShift?C.HotbarGrid:1),bar.Size,ImGuiHelpers.MainViewport.Size,C.HotbarGrid,false);changed=true;}
                    ImGui.SameLine();
                }
                ReadableText.TextDisabled("1 像素微调；Shift 为一个网格。");
                if(ReadableText.SmallButton("此栏恢复原版位置／缩放")){C.HotbarLayouts.Remove(name);selectedBars.Remove(name);changed=true;}
            }
            else if(ReadableText.Button("读取并单独控制此栏"))
            {C.HotbarLayouts[name]=new(){Position=bar.Position,Scale=bar.Scale};C.UseHotbarLayouts=true;changed=true;}
        }
        ReadableText.TextDisabled("显示／隐藏命令暂时停用，请使用原版 HUD 编辑器调整。");
        ImGui.BeginDisabled(true);
        try
        {
            if(ReadableText.SmallButton("显示此栏"))Native(()=>GameBridge.SetHotbarVisible(selectedHotbar,true));
            ImGui.SameLine();if(ReadableText.SmallButton("隐藏此栏"))Native(()=>GameBridge.SetHotbarVisible(selectedHotbar,false));
        }
        finally{ImGui.EndDisabled();}
        if(ReadableText.Button("全部恢复原版位置／缩放"))
        {CloseHotbarEditing();C.UseHotbarLayouts=false;C.HotbarLayouts.Clear();selectedBars.Clear();plugin.Hotbars.Release();changed=true;}
    }
    private void DrawHotbarEditor()
    {
        if(!hotbarEditing)return;
        if(!CanEditHotbars){CloseHotbarEditing();return;}
        DrawHotbarEditMenu();
        if(!hotbarEditing||!C.UseHotbarLayouts)return;
        var viewport=ImGuiHelpers.MainViewport;var io=ImGui.GetIO();var scale=ImGuiHelpers.GlobalScale;
        if(io.KeyShift&&C.ShowHotbarGrid)
        {
            var draw=ImGui.GetBackgroundDrawList();var step=C.HotbarGrid;
            for(var x=0f;x<viewport.Size.X;x+=step)draw.AddLine(viewport.Pos+new Vector2(x,0),viewport.Pos+new Vector2(x,viewport.Size.Y),0x307ed4ff);
            for(var y=0f;y<viewport.Size.Y;y+=step)draw.AddLine(viewport.Pos+new Vector2(0,y),viewport.Pos+new Vector2(viewport.Size.X,y),0x307ed4ff);
        }
        foreach(var bar in plugin.Hotbars.Bars)
        {
            if(!bar.Visible||!C.HotbarLayouts.TryGetValue(bar.Name,out var layout)||!layout.Enabled||!HudGeometry.Valid(bar.Position,bar.Size))continue;
            var outline=ImGui.GetForegroundDrawList();var pos=viewport.Pos+bar.Position;
            outline.AddRect(pos,pos+bar.Size,selectedBars.Contains(bar.Name)?0xff80e0ff:0x90ffffff,0,ImDrawFlags.None,1);
            var width=105*scale;var height=22*scale;
            var blocked=plugin.Hotbars.Bars.Where(b=>b.Visible).Select(b=>(b.Position,b.Size)).ToArray();
            if(!HotbarPolicy.Handle(bar.Position,bar.Size,viewport.Size,width,height,blocked,out var handle))continue;
            ImGui.SetNextWindowPos(viewport.Pos+handle);ImGui.SetNextWindowSize(new(width,height));
            ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding,Vector2.Zero);ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize,0);
            ImGui.PushStyleColor(ImGuiCol.WindowBg,new Vector4(.06f,.1f,.15f,.9f));
            var visible=ImGui.Begin("##XLinHotbarHandle"+bar.Name,ImGuiWindowFlags.NoDecoration|ImGuiWindowFlags.NoMove|ImGuiWindowFlags.NoSavedSettings|ImGuiWindowFlags.NoNav|ImGuiWindowFlags.NoScrollbar);
            try
            {
                if(!visible)continue;
                ImGui.InvisibleButton("drag",new(width,height));
                if(ImGui.IsItemActivated())
                {
                    selectedHotbar=bar.Index;
                    if(io.KeyCtrl){if(!selectedBars.Add(bar.Name))selectedBars.Remove(bar.Name);}
                    else if(!selectedBars.Contains(bar.Name)){selectedBars.Clear();selectedBars.Add(bar.Name);}
                    dragOrigins.Clear();
                    foreach(var selected in selectedBars)
                    {
                        var current=plugin.Hotbars.Bars.FirstOrDefault(b=>b.Name==selected);
                        if(current.Name is not null&&C.HotbarLayouts.TryGetValue(selected,out var selectedLayout)&&selectedLayout.Enabled)dragOrigins[selected]=current.Position;
                    }
                    dragMouse=io.MousePos;hotbarDragged=false;
                }
                if(ImGui.IsItemActive()&&ImGui.IsMouseDragging(ImGuiMouseButton.Left)&&dragOrigins.TryGetValue(bar.Name,out var origin))
                {
                    var delta=io.MousePos-dragMouse;
                    if(io.KeyShift)delta=HotbarPolicy.Position(origin+delta,bar.Size,viewport.Size,C.HotbarGrid,true)-origin;
                    foreach(var (selected,start) in dragOrigins)
                    {
                        var current=plugin.Hotbars.Bars.FirstOrDefault(b=>b.Name==selected);
                        if(current.Name is null||!C.HotbarLayouts.TryGetValue(selected,out var selectedLayout))continue;
                        selectedLayout.Position=HotbarPolicy.Position(start+delta,current.Size,viewport.Size,C.HotbarGrid,false);
                    }
                    hotbarDragged=true;
                }
                if(ImGui.IsItemDeactivated()&&hotbarDragged){plugin.Save();hotbarDragged=false;}
                var text=$"栏 {bar.Index+1} · 拖动";var draw=ImGui.GetWindowDrawList();
                draw.AddText(ImGui.GetWindowPos()+new Vector2(5*scale,(height-ImGui.GetFontSize())/2),0xfff3eee6,text);
                if(ImGui.IsItemHovered())ImGui.SetTooltip("拖动整栏；Shift 网格吸附；Ctrl 点击多选。技能格保留原版拖放。");
            }
            finally{ImGui.End();ImGui.PopStyleColor();ImGui.PopStyleVar(2);}
        }
    }
}
