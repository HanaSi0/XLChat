using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Statuses;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Lumina.Excel.Sheets;
using GameAction = Lumina.Excel.Sheets.Action;

namespace XLinUI;
internal sealed partial class UserInterface
{
    internal HudReady Ready { get; private set; }
    private void DrawCombatHud()
    {
        var viewport = ImGuiHelpers.MainViewport;
        if (C.ModernTarget && Plugin.Targets.Target is ICharacter target)
            DrawActorCard(target, false, new Vector2(viewport.Size.X / 2 - 210, 120));
        if (C.ModernFocus && Plugin.Targets.FocusTarget is ICharacter focus)
            DrawActorCard(focus, true, new Vector2(viewport.Size.X - 360, 180));
        if (C.ModernCast && Plugin.Objects.LocalPlayer is IBattleChara player && player.IsCasting)
            DrawPlayerCast(player);
        if (C.ModernStatuses && Plugin.Objects.LocalPlayer is IBattleChara owner)
        {
            if (C.ShowEmptyStatuses || owner.StatusList.Any(s => s.StatusId != 0)) DrawPlayerStatuses(owner);
            else Ready |= HudReady.Statuses;
        }
        if (C.ModernParty && Plugin.Party.Length > 0) DrawModernParty();
    }
    private bool BeginHud(string name, string id, Vector2 position, Vector2 size, params string[] native)
    {
        var dimensions = size * ImGuiHelpers.GlobalScale;
        var viewport = ImGuiHelpers.MainViewport;
        position = Vector2.Clamp(position, Vector2.Zero, Vector2.Max(Vector2.Zero, viewport.Size - dimensions));
        ImGui.SetNextWindowSize(dimensions, ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(viewport.Pos + position, ImGuiCond.FirstUseEver);
        ImportLayout(id, dimensions, native);
        if(id is "target" or "focus")ImGui.SetNextWindowSizeConstraints(new Vector2(280,220)*ImGuiHelpers.GlobalScale,ImGuiHelpers.MainViewport.Size);
        var visible = ImGui.Begin(name + "###XLinUI" + id, WindowFlags);
        if(visible)Appearance.DrawBackground();
        ImGui.SetWindowFontScale(C.HudFontScale);
        return visible;
    }
    private void DrawActorCard(ICharacter actor, bool focus, Vector2 position)
    {
        var id = focus ? "focus" : "target";
        var visible = BeginHud(focus ? "焦点" : "目标", id, position, new Vector2(440, 300), focus ? "_FocusTargetInfo" : "_TargetInfoMainTarget", focus ? "_FocusTargetInfo" : "_TargetInfo");
        try
        {
            if (!visible) return;
            var name = actor.Name.TextValue;
            ReadableText.TextDisabled($"{(focus ? "焦点" : "目标")} · Lv.{actor.Level}");
            if (ReadableText.Selectable(name + "##actor", Plugin.Targets.Target?.GameObjectId == actor.GameObjectId))
                Native(() => Plugin.Targets.Target = actor);
            if (ImGui.IsItemHovered() && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
            {
                C.ShowOriginalUi = true; plugin.Save();
                Native(() => GameBridge.OpenTargetContext(actor.Address));
            }
            ThinResource("HP", actor.CurrentHp, actor.MaxHp, C.Tint);
            if (Plugin.Objects.LocalPlayer is { } local)
                ReadableText.TextDisabled($"距离 {Vector3.Distance(local.Position, actor.Position):F1} yalms · {(actor.IsDead ? "无法战斗" : "")}");
            var targetOfTarget = Plugin.Objects.SearchById(actor.TargetObjectId);
            if (targetOfTarget is not null) ReadableText.TextDisabled("目标的目标 · " + targetOfTarget.Name.TextValue);
            if (actor is not IBattleChara battle) return; // Leave native UI for non-battle targets.
            var castReady = !battle.IsCasting || CastLine(battle);
            if (DrawEffectIcons(battle.StatusList, false) && castReady) Ready |= focus ? HudReady.Focus : HudReady.Target;
        }
        finally { EndPanel(); }
    }
    private void ThinResource(string label, uint current, uint maximum, Vector3 color)
    {
        ReadableText.TextUnformatted(label); ImGui.SameLine();
        ReadableText.TextDisabled($"{current:N0} / {maximum:N0}   {HudValues.Ratio(current, maximum) * 100:F0}%");
        if (C.TextOnly) return;
        var pos = ImGui.GetCursorScreenPos();
        var width = Math.Max(20, ImGui.GetContentRegionAvail().X);
        var height = 5 * ImGuiHelpers.GlobalScale;
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(pos - Vector2.One, pos + new Vector2(width + 1, height + 1), 0xB0000000);
        draw.AddRectFilled(pos, pos + new Vector2(width, height), 0x55FFFFFF);
        draw.AddRectFilled(pos, pos + new Vector2(width * HudValues.Ratio(current, maximum), height), ImGui.ColorConvertFloat4ToU32(new Vector4(color, 1)));
        ImGui.Dummy(new Vector2(width, height));
    }
    private bool CastLine(IBattleChara actor)
    {
        // Cast IDs refer to different sheets depending on ActionType.
        var action = actor.CastActionType switch
        {
            1 => Plugin.Data.GetExcelSheet<GameAction>().GetRowOrDefault(actor.CastActionId)?.Name.ToString(),
            2 => Plugin.Data.GetExcelSheet<Item>().GetRowOrDefault(actor.CastActionId % 1000000)?.Name.ToString(),
            _ => null,
        };
        if (string.IsNullOrWhiteSpace(action)) action = $"读条 {actor.CastActionId}";
        var total = actor.TotalCastTime;
        var elapsed = actor.CurrentCastTime;
        ReadableText.TextUnformatted(action);
        ImGui.SameLine(); ReadableText.TextDisabled($"{Math.Max(0, total - elapsed):F1}s" + (actor.IsCastInterruptible ? " · 可打断" : ""));
        if (!C.TextOnly)
        {
            ImGui.PushStyleColor(ImGuiCol.PlotHistogram, new Vector4(actor.IsCastInterruptible ? new Vector3(.95f, .65f, .35f) : C.Tint, 1));
            ImGui.ProgressBar(HudValues.Ratio(elapsed, total), new Vector2(-1, 6 * ImGuiHelpers.GlobalScale), "");
            ImGui.PopStyleColor();
        }
        return HudValues.ValidCast(elapsed, total) && actor.CastActionType is 1 or 2;
    }
    private void DrawPlayerCast(IBattleChara player)
    {
        var viewport = ImGuiHelpers.MainViewport;
        var visible = BeginHud("读条", "cast", new Vector2(viewport.Size.X / 2 - 180, viewport.Size.Y * .7f), new Vector2(350, 55), "_CastBar");
        try { if (visible && CastLine(player)) Ready |= HudReady.PlayerCast; }
        finally { EndPanel(); }
    }
    private void DrawPlayerStatuses(IBattleChara player)
    {
        var visible = BeginHud("状态", "effects", new Vector2(30, 80), new Vector2(440, 260), "_StatusEnhancements", "_Status");
        try
        {
            if (!visible) return;
            var all = player.StatusList.Where(s => s.StatusId != 0).ToArray();
            if (all.Length == 0) ReadableText.TextDisabled("当前无状态");
            if (DrawEffectIcons(all, true)) Ready |= HudReady.Statuses;
        }
        finally { EndPanel(); }
    }
    private sealed record EffectData(uint Icon,string Name,string Description,EffectGroup Group,bool CanCancel,bool Dispel,byte Stacks);
    private readonly System.Collections.Generic.Dictionary<uint,EffectData> effectCache=[];
    private EffectData? Effect(uint id)
    {
        if(effectCache.TryGetValue(id,out var cached))return cached;
        var row=Plugin.Data.GetExcelSheet<Lumina.Excel.Sheets.Status>().GetRowOrDefault(id);
        if(row is null)return null;var d=row.Value;
        var value=new EffectData(d.Icon,d.Name.ToString(),d.Description.ToString(),VisualPolicy.Group(d.StatusCategory,d.IsPermanent,d.IsFcBuff),d.CanStatusOff,d.CanDispel,d.MaxStacks);
        effectCache[id]=value;return value;
    }
    private readonly System.Collections.Generic.List<IStatus>[] effectGroups=[[],[],[]];
    private readonly System.Collections.Generic.Dictionary<(int Unit,int Value),string> durationLabels=[];
    private string EffectDuration(float seconds)
    {
        if(!float.IsFinite(seconds)||seconds<=0)return "∞";
        var unit=seconds>=3600?3600:seconds>=60?60:1;var value=(int)Math.Ceiling(seconds/unit);
        if(durationLabels.TryGetValue((unit,value),out var text))return text;
        if(durationLabels.Count>=256)durationLabels.Clear();text=HudValues.Duration(seconds);durationLabels[(unit,value)]=text;return text;
    }
    private bool DrawEffectIcons(System.Collections.Generic.IEnumerable<IStatus> statuses,bool allowCancel)
    {
        foreach(var bucket in effectGroups)bucket.Clear();var statusCount=0;
        foreach(var status in statuses)if(status.StatusId!=0){effectGroups[(int)(Effect(status.StatusId)?.Group??EffectGroup.Other)].Add(status);statusCount++;}
        if(statusCount==0)return true;
        var available=ImGui.GetContentRegionAvail();if(available.Y<15)return false;
        var shown=ImGui.BeginChild("classified-effects",new Vector2(0,Math.Max(35,available.Y)),false);
        var complete=shown;
        try
        {
            if(!shown)return false;
            for(var groupIndex=0;groupIndex<3;groupIndex++)
            {
                var group=(EffectGroup)groupIndex;var entries=effectGroups[groupIndex];if(entries.Count==0)continue;
                var color=group==EffectGroup.Debuff?new Vector4(1,.68f,.64f,1):group==EffectGroup.Buff?new Vector4(.72f,.95f,.84f,1):new Vector4(.8f,.84f,1,1);
                ReadableText.TextColored(color,(group==EffectGroup.Buff?"增益":group==EffectGroup.Debuff?"减益":"其他状态")+" · "+entries.Count);
                var size=C.StatusIconSize*ImGuiHelpers.GlobalScale;
                var columns=Math.Max(1,(int)(ImGui.GetContentRegionAvail().X/(size+ImGui.GetStyle().ItemSpacing.X)));
                for(var i=0;i<entries.Count;i++)
                {
                    if(i>0&&i%columns!=0)ImGui.SameLine();
                    var status=entries[i];var data=Effect(status.StatusId);ImGui.PushID(i+(int)group*1000);
                    try
                    {
                        var iconId=data?.Icon??0;
                        if(data is not null&&data.Stacks>1&&status.Param>1&&status.Param<=data.Stacks)iconId+=(uint)status.Param-1;
                        var icon=iconId==0?null:(C.FlatIcons?plugin.Flat.Get(iconId):null)??Plugin.Textures.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrDefault();
                        var dimensions=VisualPolicy.IconSize(icon?.Width??64,icon?.Height??80,size);
                        var pos=ImGui.GetCursorScreenPos();ImGui.InvisibleButton("effect",new Vector2(size,dimensions.Y+ImGui.GetFontSize()+3));
                        var draw=ImGui.GetWindowDrawList();
                        draw.AddRectFilled(pos-Vector2.One,pos+dimensions+Vector2.One,0xc01a1510);
                        if(icon is not null)draw.AddImage(icon.Handle,pos,pos+dimensions);else{ReadableText.Draw(draw,pos,0xffffffff,"?");complete=false;}
                        if(!C.HideBorders)draw.AddRect(pos,pos+dimensions,ImGui.ColorConvertFloat4ToU32(color),0f);
                        ReadableText.Draw(draw,pos+new Vector2(0,dimensions.Y),0xfff4f4f4,EffectDuration(status.RemainingTime));
                        if(data is not null&&data.Stacks>1&&status.Param>0)ReadableText.Draw(draw,pos+new Vector2(size-12,2),0xffffffff,status.Param.ToString());
                        if(data?.Dispel==true)draw.AddRectFilled(pos+new Vector2(2,-3),pos+new Vector2(Math.Max(3,size-2),-1),0xffffffff);
                        if(ImGui.IsItemHovered())ImGui.SetTooltip((data?.Name??"未知状态")+"\n"+(data?.Description??"")+$"\n剩余 {status.RemainingTime:F1}s"+(allowCancel&&data?.CanCancel==true?"\n右键取消":""));
                        if(allowCancel&&data?.CanCancel==true&&ImGui.IsItemClicked(ImGuiMouseButton.Right))Native(()=>GameBridge.CancelStatus(status.StatusId,status.SourceId));
                    }
                    finally{ImGui.PopID();}
                }
                ImGui.Spacing();
            }
        }
        finally{ImGui.EndChild();}
        return complete;
    }
    private void DrawModernParty()
    {
        var visible = BeginHud("队伍", "modernparty", new Vector2(30, 280), new Vector2(280, 530), "_PartyList");
        try
        {
            if (!visible) return;
            foreach (var member in Plugin.Party)
            {
                ImGui.PushID(member.EntityId.ToString());
                try
                {
                    var actor = member.GameObject;
                    if (ReadableText.Selectable($"{member.Name.TextValue} · Lv.{member.Level} {member.ClassJob.Value.Abbreviation}##member", actor is not null && Plugin.Targets.Target?.GameObjectId == actor.GameObjectId))
                    { if (actor is not null) Native(() => Plugin.Targets.Target = actor); }
                    ThinResource("HP", member.CurrentHP, member.MaxHP, C.Tint);
                    ReadableText.TextDisabled(actor is null ? "当前不在本地场景" : $"MP {member.CurrentMP:N0} / {member.MaxMP:N0}");
                    ImGui.Separator();
                }
                finally { ImGui.PopID(); }
            }
            // Keep the native list: its context menus, sorting, leadership and ready-check icons are not duplicated.
        }
        finally { EndPanel(); }
    }
}
