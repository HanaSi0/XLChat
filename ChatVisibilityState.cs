using System.Collections.Generic;

namespace XLinUI;

// Kept independent of native memory so restoration behavior can be tested.
internal sealed class ChatVisibilityState
{
    private readonly Dictionary<string, Snapshot> snapshots = [];

    internal bool? Change(string name, nint addon, nint node, bool currentVisible, bool hide)
    {
        if (addon == 0 || node == 0) { snapshots.Remove(name); return null; }
        if (snapshots.TryGetValue(name, out var previous) &&
            (previous.Addon != addon || previous.Node != node)) snapshots.Remove(name);
        if (hide)
        {
            if (!snapshots.ContainsKey(name)) snapshots[name] = new Snapshot(addon, node, currentVisible);
            return false;
        }
        return snapshots.Remove(name, out var saved) ? saved.Visible : null;
    }

    internal void Forget(string name) => snapshots.Remove(name);
    private readonly record struct Snapshot(nint Addon, nint Node, bool Visible);
}
