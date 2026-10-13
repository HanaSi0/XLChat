using System;
using System.Collections.Generic;
namespace XLinUI;
internal static class TopIconPolicy
{
    // Lumina's default row is not readable: only invoke the icon getter for a real match.
    internal static uint Find<T>(IEnumerable<T> rows,Func<T,bool> matches,Func<T,int> icon)
    {
        foreach(var row in rows)if(matches(row))return (uint)Math.Max(0,icon(row));
        return 0;
    }
}
