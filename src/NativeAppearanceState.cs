using System.Collections.Generic;
namespace XLinUI;
internal readonly record struct NativeAppearance(byte Alpha, byte Red, byte Green, byte Blue);
// Identity-checked restoration; numeric node addresses are never dereferenced by this state machine.
internal sealed class NativeAppearanceState
{
    private readonly Dictionary<(string Name, nint Node), Entry> originals = [];
    internal NativeAppearance? Change(string name, nint addon, nint node, NativeAppearance current, NativeAppearance? desired)
    {
        var key = (name, node);
        if (originals.TryGetValue(key, out var entry) && entry.Addon != addon) originals.Remove(key);
        if (desired.HasValue)
        {
            if (!originals.ContainsKey(key)) originals[key] = new Entry(addon, current);
            return desired;
        }
        return originals.Remove(key, out var saved) ? saved.Appearance : null;
    }
    internal NativeAppearance Original(string name, nint addon, nint node, NativeAppearance current) =>
        originals.TryGetValue((name, node), out var saved) && saved.Addon == addon ? saved.Appearance : current;
    internal bool Contains(string name)
    {
        foreach (var key in originals.Keys) if (key.Name == name) return true;
        return false;
    }
    internal void Forget(string name)
    {
        foreach (var key in new List<(string Name, nint Node)>(originals.Keys))
            if (key.Name == name) originals.Remove(key);
    }
    internal void Clear() => originals.Clear();
    private readonly record struct Entry(nint Addon, NativeAppearance Appearance);
}
