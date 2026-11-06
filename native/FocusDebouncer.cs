using System;

// Called by the focus loop only. Time comes from Stopwatch, never the wall clock.
internal sealed class FocusDebouncer {
    internal const int DelayMilliseconds = 80;
    Snapshot candidate;
    long since;
    bool committed;

    bool Matches(Snapshot focus) {
        return candidate != null && focus != null &&
            candidate.Identity == focus.Identity && candidate.Mode == focus.Mode &&
            candidate.Window == focus.Window && candidate.Focus == focus.Focus;
    }

    internal void Reset() { candidate = null; committed = false; }

    // Explicit preference commands have already waited for their 3-second capture.
    internal void Commit(Snapshot focus) { candidate = focus; committed = focus != null; }

    internal bool IsSettled(Snapshot focus) { return committed && Matches(focus); }

    internal bool Update(Snapshot focus, long now) {
        if (focus == null) { Reset(); return false; }
        if (!Matches(focus)) {
            candidate = focus;
            since = now;
            committed = false;
        }
        // Protected/noneditable controls cancel pending switching immediately.
        if (!committed && (focus.Mode == null || now - since >= DelayMilliseconds)) {
            committed = true;
            return true;
        }
        return false;
    }

    internal int NextCheck(long now, int pollInterval) {
        if (candidate == null || committed) return pollInterval;
        // Recheck at the deadline rather than waiting a whole extra polling period.
        return (int)Math.Max(1L, Math.Min(pollInterval, DelayMilliseconds - (now - since)));
    }

    static Snapshot Field(string identity, string mode) {
        return new Snapshot { Identity = identity, Mode = mode, Window = new IntPtr(1), Focus = new IntPtr(2) };
    }
    static void Check(bool passed, string message) {
        if (!passed) throw new Exception("Focus debounce: " + message);
    }
    internal static void SelfTest() {
        var gate = new FocusDebouncer();
        var chat = Field("chat", "zh");
        var terminal = Field("terminal", "en");
        Check(!gate.Update(chat, 0), "first sample must wait");
        Check(gate.NextCheck(0, 1000) == DelayMilliseconds, "large poll interval must not add another full interval");
        Check(!gate.Update(chat, DelayMilliseconds - 1), "must wait until deadline");
        Check(gate.Update(chat, DelayMilliseconds), "stable chat must settle");
        Check(!gate.Update(chat, 500) && gate.IsSettled(chat), "must not repeat or override a manual change");
        Check(gate.NextCheck(500, 250) == 250, "settled focus must use ordinary polling");

        // Rapid chat -> terminal -> chat must never commit the intermediate target.
        Check(!gate.Update(terminal, 600), "terminal must wait too");
        Check(!gate.Update(chat, 650), "return to previous control needs a fresh deadline");
        Check(!gate.Update(chat, 650 + DelayMilliseconds - 1), "old deadline must be discarded");
        Check(gate.Update(chat, 650 + DelayMilliseconds), "final stable target must settle");

        gate.Reset();
        Check(!gate.Update(terminal, 0), "new terminal candidate");
        Check(!gate.Update(null, 100), "outside app must cancel");
        Check(!gate.IsSettled(terminal) && gate.NextCheck(200, 250) == 250, "no pending request outside app");
        Check(!gate.Update(terminal, 300) && gate.Update(terminal, 450), "reentry must wait and then settle");

        foreach (string reason in new[] { "code-editor", "password", "readonly-or-noneditable", "scope-preserve" }) {
            gate.Reset();
            gate.Update(chat, 0);
            var protectedField = Field(reason, null);
            Check(gate.Update(protectedField, 50), "protected input must cancel immediately");
            Check(gate.IsSettled(protectedField) && !gate.Update(protectedField, 500), "protected input remains preserved");
        }

        gate.Reset();
        gate.Update(chat, 0);
        var changedMode = Field("chat", "en");
        Check(!gate.Update(changedMode, 100) && !gate.Update(changedMode, 100 + DelayMilliseconds - 1) && gate.Update(changedMode, 250), "same ID with changed rule needs new deadline");
        var changedHandle = Field("chat", "en");
        changedHandle.Focus = new IntPtr(3);
        Check(!gate.Update(changedHandle, 300) && gate.Update(changedHandle, 450), "native focus handle participates in identity");
        var changedWindow = Field("chat", "en");
        changedWindow.Window = new IntPtr(4);
        Check(!gate.Update(changedWindow, 500) && gate.Update(changedWindow, 650), "window participates in identity");

        gate.Commit(chat);
        Check(gate.IsSettled(chat) && !gate.Update(chat, 1000), "explicit preference must not trigger a duplicate switch");
        gate.Reset();
        Check(!gate.IsSettled(chat) && !gate.Update(chat, 1100), "clear/error must require fresh focus stability");
        Check(gate.NextCheck(1250, 250) == 1, "expired deadline must not cause a busy loop");
        Check(gate.NextCheck(1100, 50) == 50, "deadline must not lengthen configured polling");
        Console.WriteLine("Focus debounce checks passed.");
    }
}
