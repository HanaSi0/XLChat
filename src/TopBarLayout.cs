using System;
using System.Collections.Generic;
using System.Linq;
namespace XLinUI;
internal readonly record struct TopSlot(bool Right,int Index,float X,float Width,int Row);
internal sealed record TopLayout(IReadOnlyList<TopSlot> Slots,int Rows);
internal static class TopBarLayout
{
    internal static TopLayout Arrange(float available,float gap,IReadOnlyList<float> left,IReadOnlyList<float> right)
    {
        available=float.IsFinite(available)?Math.Max(1,available):320;gap=Math.Clamp(gap,0,available/10);
        float Safe(float width,float maximum)=>Math.Clamp(float.IsFinite(width)?width:60,1,Math.Max(1,maximum));
        var rightLimit=left.Count==0?available:available*.48f;
        var rightRows=new List<List<(int Index,float Width)>>();var row=new List<(int,float)>();float used=0;
        for(var i=0;i<right.Count;i++)
        {
            var width=Safe(right[i],rightLimit);
            if(row.Count>0&&used+gap+width>rightLimit){rightRows.Add(row);row=[];used=0;}
            if(row.Count>0)used+=gap;row.Add((i,width));used+=width;
        }
        if(row.Count>0)rightRows.Add(row);
        var rightWidth=rightRows.Count==0?0:rightRows.Max(r=>r.Sum(x=>x.Width)+gap*(r.Count-1));
        var leftWidth=Math.Max(1,available-rightWidth-(rightWidth>0&&left.Count>0?gap:0));
        // A single long map/DTR label must not force every shortcut into a wide column.
        var ordered=left.Select(w=>Safe(w,leftWidth)).OrderBy(w=>w).ToArray();
        var widest=ordered.Length==0?1:ordered[ordered.Length/2];
        var columns=Math.Max(1,(int)Math.Floor((leftWidth+gap)/(widest+gap)));
        var slots=new List<TopSlot>();var leftRows=0;
        for(var start=0;start<left.Count;start+=columns)
        {
            var count=Math.Min(columns,left.Count-start);var cell=Math.Max(1,(leftWidth-gap*(count-1))/count);
            for(var i=0;i<count;i++)slots.Add(new(false,start+i,i*(cell+gap),cell,leftRows));
            leftRows++;
        }
        for(var r=0;r<rightRows.Count;r++)
        {
            var width=rightRows[r].Sum(x=>x.Width)+gap*(rightRows[r].Count-1);var x=available-width;
            foreach(var item in rightRows[r]){slots.Add(new(true,item.Index,x,item.Width,r));x+=item.Width+gap;}
        }
        return new(slots,Math.Max(1,Math.Max(leftRows,rightRows.Count)));
    }
}
