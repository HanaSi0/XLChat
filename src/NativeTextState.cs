using System.Collections.Generic;
namespace XLinUI;
internal readonly record struct TextAppearance(uint EdgeColor, ushort Flags);
internal sealed class NativeTextState
{
    private readonly Dictionary<(string Name, nint Node), Entry> entries = [];
    internal TextAppearance? Change(string name, nint addon, nint node, TextAppearance current, bool enabled)
    {
        var key = (name, node);
        if (entries.TryGetValue(key, out var old) && old.Addon != addon) entries.Remove(key);
        if (enabled)
        {
            if (!entries.ContainsKey(key)) entries[key] = new Entry(addon, current);
            return new TextAppearance(0xE8000000, (ushort)(current.Flags | (1 << 3)));
        }
        // Restore only the Edge bit: other flags may have been changed by the game during ownership.
        return entries.Remove(key, out var saved) ?
            new TextAppearance(saved.Appearance.EdgeColor, (ushort)((current.Flags & ~(1 << 3)) | (saved.Appearance.Flags & (1 << 3)))) : null;
    }
    internal bool Contains(string name)
    { foreach (var key in entries.Keys) if (key.Name == name) return true; return false; }
    internal void Forget(string name)
    { foreach (var key in new List<(string Name, nint Node)>(entries.Keys)) if (key.Name == name) entries.Remove(key); }
    internal void Clear() => entries.Clear();
    private readonly record struct Entry(nint Addon, TextAppearance Appearance);
}
