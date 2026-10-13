using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Utility;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
namespace XLinUI;
internal readonly record struct MinimapMarker(Vector2 World,uint Icon,string Label);
internal sealed partial class UserInterface
{
    private readonly List<MinimapMarker> minimapMarkers=[];
    private DateTime lastMapMarkers;
    private uint minimapId;
    private string minimapPath="",minimapTitle="";
    private float minimapFactor;
    private Vector2 minimapOffset;
    private bool minimapResourceReady;
    private unsafe bool ReadMinimap()
    {
        if(Plugin.Objects.LocalPlayer is null)return false;
        var agent=AgentMap.Instance();if(agent==null||agent->CurrentMapId!=Plugin.Client.MapId)return false;
        if(minimapId!=Plugin.Client.MapId)
        {
            minimapResourceReady=false;minimapMarkers.Clear();
            var row=Plugin.Data.GetExcelSheet<Map>().GetRowOrDefault(Plugin.Client.MapId);
            if(row is null||row.Value.SizeFactor==0)return false;
            var data=row.Value;var id=data.Id.ToString();
            if(string.IsNullOrWhiteSpace(id)||!id.Contains('/'))return false;
            minimapPath="ui/map/"+id+"/"+id.Replace("/","")+"_m.tex";
            minimapFactor=data.SizeFactor;minimapOffset=new(data.OffsetX,data.OffsetY);
            minimapTitle=data.PlaceName.IsValid?data.PlaceName.Value.Name.ToString():"小地图";
            minimapId=Plugin.Client.MapId;lastMapMarkers=default;
        }
        if((DateTime.UtcNow-lastMapMarkers).TotalMilliseconds>=100)
        {
            lastMapMarkers=DateTime.UtcNow;minimapMarkers.Clear();
            var markers=agent->MiniMapMarkers;
            for(var i=0;i<Math.Min((int)agent->MiniMapMarkerCount,markers.Length);i++)
            {
                var m=markers[i].MapMarker;if(m.IconId==0)continue;
                minimapMarkers.Add(new(new Vector2(m.X/16f,m.Y/16f),m.IconId,"地图标记"));
            }
            foreach(var gathering in agent->MiniMapGatheringMarkers)
                if(gathering.ShouldRender!=0&&gathering.MapMarker.IconId!=0)
                {var m=gathering.MapMarker;minimapMarkers.Add(new(new Vector2(m.X/16f,m.Y/16f),m.IconId,gathering.TooltipText.ToString()));}
            if(agent->FlagMarkerCount>0)
            {var flag=agent->FlagMapMarkers[0];if(flag.MapId==minimapId)minimapMarkers.Add(new(new(flag.XFloat,flag.YFloat),flag.MapMarker.IconId,"地图旗标"));}
        }
        return minimapId!=0;
    }
    private void DrawMinimap()
    {
        if(!C.RebuildMinimap||Plugin.Objects.LocalPlayer is not {} player)return;
        bool available;
        try{available=ReadMinimap();}catch(Exception ex){if(minimapResourceReady)Plugin.Log.Debug(ex,"小地图数据暂不可用");minimapResourceReady=false;return;}
        if(!available)return;
        var texture=Plugin.Textures.GetFromGame(minimapPath).GetWrapOrDefault();
        minimapResourceReady=texture is not null;if(texture is null)return;
        var scale=ImGuiHelpers.GlobalScale;var viewport=ImGuiHelpers.MainViewport;
        ImGui.SetNextWindowSize(new Vector2(C.MinimapSize,C.MinimapSize+72)*scale,ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(170,240)*scale,new Vector2(700,770)*scale);
        ImGui.SetNextWindowPos(viewport.Pos+new Vector2(viewport.Size.X-C.MinimapSize*scale-24,90*scale),ImGuiCond.FirstUseEver);
        ImportLayout("minimap",new Vector2(C.MinimapSize,C.MinimapSize+72)*scale,"_NaviMap");
        var visible=ImGui.Begin("小地图###XLinUIMinimap",WindowFlags);
        try
        {
            if(!visible)return;Appearance.DrawBackground();
            ReadableText.TextUnformatted(minimapTitle);ImGui.SameLine();
            if(ReadableText.SmallButton("−")){C.MinimapZoom=Math.Clamp(C.MinimapZoom-.2f,.5f,4);plugin.Save();}
            ImGui.SameLine();if(ReadableText.SmallButton("+")){C.MinimapZoom=Math.Clamp(C.MinimapZoom+.2f,.5f,4);plugin.Save();}
            var size=Math.Max(30,Math.Min(ImGui.GetContentRegionAvail().X,ImGui.GetContentRegionAvail().Y-ImGui.GetFontSize()-8));
            if(size<100)return;
            var pos=ImGui.GetCursorScreenPos();var dimension=new Vector2(size);
            ImGui.InvisibleButton("square-map",dimension);
            var hovered=ImGui.IsItemHovered();var draw=ImGui.GetWindowDrawList();
            var world=new Vector2(player.Position.X,player.Position.Z);
            var center=VisualPolicy.MapUv(world,minimapFactor,minimapOffset);
            var crop=VisualPolicy.Crop(center,C.MinimapZoom);
            draw.AddRectFilled(pos,pos+dimension,0xff211a12);draw.AddImage(texture.Handle,pos,pos+dimension,crop.Min,crop.Max);
            draw.PushClipRect(pos,pos+dimension,true);
            var mouse=ImGui.GetMousePos();MinimapMarker? selected=null;
            foreach(var marker in minimapMarkers)
            {
                var uv=VisualPolicy.MapUv(marker.World,minimapFactor,minimapOffset);
                var point=pos+(uv-crop.Min)/(crop.Max-crop.Min)*size;
                if(point.X<pos.X||point.Y<pos.Y||point.X>pos.X+size||point.Y>pos.Y+size)continue;
                var icon=(C.FlatIcons?plugin.Flat.Get(marker.Icon):null)??Plugin.Textures.GetFromGameIcon(new GameIconLookup(marker.Icon)).GetWrapOrDefault();
                if(icon is not null)draw.AddImage(icon.Handle,point-new Vector2(9*scale),point+new Vector2(9*scale));else draw.AddCircleFilled(point,3*scale,0xff95ddff);
                if(hovered&&Vector2.Distance(mouse,point)<12*scale)selected=marker;
            }
            var playerPoint=pos+(center-crop.Min)/(crop.Max-crop.Min)*size;
            var forward=new Vector2(MathF.Sin(player.Rotation),MathF.Cos(player.Rotation));
            var side=new Vector2(forward.Y,-forward.X);
            draw.AddTriangleFilled(playerPoint+forward*9*scale,playerPoint-forward*5*scale+side*5*scale,playerPoint-forward*5*scale-side*5*scale,0xffffffff);
            draw.AddText(pos+new Vector2(6,4),0xffffffff,"N");draw.PopClipRect();
            draw.AddRect(pos,pos+dimension,0xffb4b4b4,0f);
            if(hovered)
            {
                if(ImGui.GetIO().MouseWheel!=0){C.MinimapZoom=Math.Clamp(C.MinimapZoom+ImGui.GetIO().MouseWheel*.1f,.5f,4);plugin.Save();}
                if(selected is {} marker)ImGui.SetTooltip(marker.Label+"\n点击打开地图位置");
                if(ImGui.IsMouseClicked(ImGuiMouseButton.Left))
                {
                    if(selected is {} selectedMarker)
                    {var link=new MapLinkPayload(Plugin.Client.TerritoryType,minimapId,selectedMarker.World.X,selectedMarker.World.Y);Native(()=>Plugin.GameGui.OpenMapWithMapLink(link));}
                    else OpenGame(16,true);
                }
            }
            var coordinates=Vector2.One+(center*2048/minimapFactor*100)/50;
            ReadableText.TextDisabled($"X {coordinates.X:F1} · Y {coordinates.Y:F1} · 北向上");
            Ready|=HudReady.Minimap;
        }
        finally{EndPanel();}
    }
}
