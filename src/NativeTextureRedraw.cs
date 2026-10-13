using System;
using System.Runtime.InteropServices;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Component.GUI;
using KernelTexture=FFXIVClientStructs.FFXIV.Client.Graphics.Kernel.Texture;
namespace XLinUI;
internal sealed unsafe class NativeTextureRedraw : IDisposable
{
    private readonly TextureSwapState state=new(new NativeAccess());
    private RedrawKind enabled;
    private readonly System.Collections.Generic.Dictionary<ulong,(nint Kernel,long Used)> bridges=[];
    private long sequence;
    private readonly System.Collections.Generic.Dictionary<(nint Resource,uint Hash),string> paths=[];
    internal int Count=>state.Count;
    internal string Failure {get;private set;}="";
    internal void SetEnabled(bool live)
    {
        var c=Plugin.InstanceConfig;
        var next=live&&Failure.Length==0?(c.FlatNativeIcons?RedrawKind.Icons:0)|(c.FlatNativeButtons?RedrawKind.Buttons:0):0;
        if((enabled&~next)!=0)state.Restore(enabled&~next);enabled=next;
    }
    internal bool Icon(AtkUldAsset* asset,uint id=0)
    {
        if((enabled&RedrawKind.Icons)==0||asset==null)return false;
        var atk=&asset->AtkTexture;
        if(atk->TextureType!=TextureType.Resource||atk->Resource==null)return false;
        if(id==0)id=atk->Resource->IconId;
        if(id==0)return false;
        var texture=Plugin.FlatCache.Get(id);return Apply(atk,texture,RedrawKind.Icons);
    }
    internal bool Button(AtkNineGridNode* node)=>node!=null&&Atlas(node->PartsList,node->PartId,false);
    internal bool Glyph(AtkImageNode* node)=>node!=null&&Atlas(node->PartsList,node->PartId,false,true);
    internal bool Frame(AtkResNode* node)
    {
        if(node==null)return false;
        if(node->Type==NodeType.NineGrid){var n=(AtkNineGridNode*)node;return Atlas(n->PartsList,n->PartId,true);}
        if(node->Type==NodeType.Image){var n=(AtkImageNode*)node;return Atlas(n->PartsList,n->PartId,true);}
        return false;
    }
    private bool Atlas(AtkUldPartsList* parts,uint index,bool outline,bool glyph=false)
    {
        if((enabled&RedrawKind.Buttons)==0||parts==null||parts->Parts==null||index>=parts->PartCount)return false;
        var part=&parts->Parts[index];var asset=part->UldAsset;if(asset==null)return false;
        var atk=&asset->AtkTexture;
        if(atk->TextureType!=TextureType.Resource||atk->Resource==null||atk->Resource->IconId!=0||atk->Resource->TexFileResourceHandle==null)return false;
        var pathKey=((nint)atk->Resource,atk->Resource->TexPathHash);
        if(!paths.TryGetValue(pathKey,out var path)){path=atk->Resource->TexFileResourceHandle->FileName.ToString();paths[pathKey]=path;}
        if(string.IsNullOrWhiteSpace(path)||!path.StartsWith("ui/",StringComparison.OrdinalIgnoreCase)||!path.EndsWith(".tex",StringComparison.OrdinalIgnoreCase))return false;
        var texture=Plugin.ButtonCache.Get(path,new(part->U,part->V,part->Width,part->Height,outline,glyph));
        return Apply(atk,texture,RedrawKind.Buttons);
    }
    private bool Apply(AtkTexture* atk,IDalamudTextureWrap? redraw,RedrawKind kind)
    {
        if(redraw is null||Failure.Length>0)return false;
        try
        {
            var kernel=atk->GetKernelTexture();if(kernel==null)return false;
            var view=KernelView(redraw);if(view==0)return false;
            return state.Apply((nint)kernel,view,kind);
        }
        catch(Exception ex)
        {
            Failure="原生纹理重绘接口失败，本次加载暂停重绘，请查看 /xllog。";
            Restore();enabled=0;Plugin.Log.Error(ex,"XLinUI 原生重绘已暂停并恢复纹理");return false;
        }
    }
    private nint KernelView(IDalamudTextureWrap wrap)
    {
        var id=wrap.Handle.Handle;
        if(bridges.TryGetValue(id,out var bridge)){bridges[id]=(bridge.Kernel,++sequence);return (nint)((KernelTexture*)bridge.Kernel)->D3D11ShaderResourceView;}
        // Public Dalamud API supplies the native texture, rather than assuming an ImGui ID is a raw view.
        var converted=Plugin.Textures.ConvertToKernelTexture(wrap,true);if(converted==0)return 0;
        if(bridges.Count>=1024)
        {
            ulong oldest=0;var stamp=long.MaxValue;foreach(var item in bridges)if(item.Value.Used<stamp){oldest=item.Key;stamp=item.Value.Used;}
            if(bridges.Remove(oldest,out var old))((KernelTexture*)old.Kernel)->DecRef();
        }
        bridges[id]=(converted,++sequence);return (nint)((KernelTexture*)converted)->D3D11ShaderResourceView;
    }
    internal void Restore()=>state.Restore(RedrawKind.Icons|RedrawKind.Buttons);
    public void Dispose()
    {
        Restore();foreach(var bridge in bridges.Values)((KernelTexture*)bridge.Kernel)->DecRef();bridges.Clear();paths.Clear();
    }
    private sealed class NativeAccess : ITextureAccess
    {
        public nint Read(nint texture)=>(nint)((KernelTexture*)texture)->D3D11ShaderResourceView;
        public void Write(nint texture,nint view)=>((KernelTexture*)texture)->D3D11ShaderResourceView=(void*)view;
        public void HoldTexture(nint texture)=>((KernelTexture*)texture)->IncRef();
        public void ReleaseTexture(nint texture)=>((KernelTexture*)texture)->DecRef();
        public void HoldView(nint view)=>Marshal.AddRef(view);
        public void ReleaseView(nint view)=>Marshal.Release(view);
    }
}
