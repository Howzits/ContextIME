using System;

// An input-method request is not evidence of its actual state or user intent.
internal sealed class ManualPreferenceTracker {
    const int SettleMilliseconds = 700;
    const int StableMilliseconds = 250;
    string expected, baseline, candidate;
    long readyAt, candidateSince;

    internal void Reset() { expected = baseline = candidate = null; }

    internal void Start(string target, string observed, long now) {
        Reset();
        expected = target;
        readyAt = now + SettleMilliseconds;
        // Only arm after an actual observation confirms the automatic target.
        if (observed == target) baseline = observed;
    }

    internal string Update(string observed, long now) {
        if (observed != "en" && observed != "zh") { candidate = null; return null; }
        if (baseline == null) {
            if (observed == expected) baseline = observed;
            return null;
        }
        if (observed == baseline) { candidate = null; return null; }
        if (candidate != observed) { candidate = observed; candidateSince = now; return null; }
        if (now < readyAt || now - candidateSince < StableMilliseconds) return null;
        baseline = observed;
        candidate = null;
        return observed;
    }

    static void Check(bool passed, string message) {
        if (!passed) throw new Exception("Manual preference: " + message);
    }
    internal static void SelfTest() {
        var tracker = new ManualPreferenceTracker();
        tracker.Start("zh", "zh", 0);
        Check(tracker.Update("en", 200) == null, "early manual change must settle");
        Check(tracker.Update("en", 500) == null, "automatic settling period must elapse");
        Check(tracker.Update("en", 700) == "en", "early manual change must not become an unrecorded baseline");
        Check(tracker.Update("en", 1000) == null, "unchanged preference must not repeat");
        Check(tracker.Update("zh", 1100) == null && tracker.Update("zh", 1350) == "zh", "later manual reversal must be remembered");

        tracker.Start("zh", "en", 0);
        Check(tracker.Update("en", 800) == null && tracker.Update("en", 1100) == null, "failed automatic request must not imply manual preference");
        Check(tracker.Update("zh", 1500) == null && tracker.Update("zh", 1800) == null, "delayed automatic result must not be recorded as manual");
        Check(tracker.Update("en", 2000) == null && tracker.Update("en", 2250) == "en", "change after target confirmation must be recorded");

        tracker.Start("zh", "zh", 0);
        Check(tracker.Update("en", 800) == null && tracker.Update("zh", 900) == null, "single transient observation must be discarded");
        Check(tracker.Update("en", 1000) == null && tracker.Update(null, 1250) == null, "unknown state breaks consecutive observations");
        Check(tracker.Update("en", 1500) == null && tracker.Update("en", 1750) == "en", "unknown interval must not count as stable time");
        tracker.Reset();
        Check(tracker.Update("zh", 2000) == null && tracker.Update("en", 2500) == null, "focus loss or clear must discard tracking");
        tracker.Start("en", "en", 0);
        Check(tracker.Update("zh", 700) == null && tracker.Update("zh", 950) == "zh", "explicit English preference can be manually changed back");
        Console.WriteLine("Manual preference tracking checks passed.");
    }
}
