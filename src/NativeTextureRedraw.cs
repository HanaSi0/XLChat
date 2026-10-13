using System;
using FFXIVClientStructs.FFXIV.Component.GUI;
namespace XLinUI;
// Direct writes to shared kernel views are suspended until validated on the actual soil client.
// Plugin-owned textures continue to use Dalamud's managed texture provider.
internal sealed unsafe class NativeTextureRedraw : IDisposable
{
    internal int Count=>0;
    internal string Failure=>"本版暂停原生共享纹理改写；保留游戏原生图标与按钮。";
    internal void SetEnabled(bool live) { }
    internal bool Icon(AtkUldAsset* asset,uint id=0)=>false;
    internal bool Button(AtkNineGridNode* node)=>false;
    internal bool Glyph(AtkImageNode* node)=>false;
    internal bool Frame(AtkResNode* node)=>false;
    public void Dispose() { }
}
