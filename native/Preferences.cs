using System;
using System.Collections.Generic;

// Runtime IDs are meaningful only while the control is alive. Never persist them.
internal sealed class Preferences {
    readonly Dictionary<string, string> modes = new Dictionary<string, string>();
    readonly Queue<string> order = new Queue<string>();
    internal void Set(string identity, string mode) {
        if (identity == null || (mode != "en" && mode != "zh" && mode != "keep")) return;
        if (!modes.ContainsKey(identity)) order.Enqueue(identity);
        modes[identity] = mode;
        while (order.Count > 128) modes.Remove(order.Dequeue());
    }
    internal string Get(string identity, string fallback) {
        string mode;
        return identity != null && modes.TryGetValue(identity, out mode) ? mode : fallback;
    }
    internal void Clear() { modes.Clear(); order.Clear(); }
    internal static void SelfTest() {
        var prefs = new Preferences();
        prefs.Set("chat-a", "en");
        if (prefs.Get("chat-a", "zh") != "en" || prefs.Get("chat-b", "zh") != "zh") throw new Exception("Preference isolation failed");
        prefs.Set("chat-a", "keep");
        if (prefs.Get("chat-a", "zh") != "keep") throw new Exception("Keep preference failed");
        for (int i = 0; i < 129; i++) prefs.Set("field-" + i, "zh");
        if (prefs.Get("chat-a", "en") != "en") throw new Exception("Preference bound failed");
        prefs.Clear();
        if (prefs.Get("field-128", "en") != "en") throw new Exception("Preference reset failed");
        Console.WriteLine("Session preference checks passed.");
    }
}
