using System;
using System.Collections.Generic;
using XLinUI;
internal sealed class FakeTextureAccess : ITextureAccess
{
    internal readonly Dictionary<nint,nint> Views=[];
    internal readonly Dictionary<nint,int> TextureRefs=[];
    internal readonly Dictionary<nint,int> ViewRefs=[];
    internal void Add(nint texture,nint original,nint redraw){Views[texture]=original;TextureRefs[texture]=1;ViewRefs[original]=1;ViewRefs[redraw]=1;}
    internal void GameReplace(nint texture,nint view){ReleaseView(Views[texture]);Views[texture]=view;ViewRefs[view]=1;}
    public nint Read(nint texture)=>Views[texture];
    public void Write(nint texture,nint view){if(ViewRefs.GetValueOrDefault(view)<1)throw new Exception("Binding a freed view");Views[texture]=view;}
    public void HoldTexture(nint texture)=>TextureRefs[texture]++;
    public void ReleaseTexture(nint texture){if(--TextureRefs[texture]<0)throw new Exception("Texture reference underflow");}
    public void HoldView(nint view)=>ViewRefs[view]=ViewRefs.GetValueOrDefault(view)+1;
    public void ReleaseView(nint view){if(--ViewRefs[view]<0)throw new Exception("View reference underflow");}
}
