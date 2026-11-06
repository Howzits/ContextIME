using System;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;

internal static class Program {
    static volatile bool running = true;
    static bool otherInputsChinese = true;
    static int pollInterval = 250;
    // Use control types and structural IDs/classes, independent of chat provider.
    // Typed text, values and document contents are never read or logged.
    internal static Snapshot ReadFocus() {
        IntPtr window = Native.GetForegroundWindow();
        uint pid;
        Native.GetWindowThreadProcessId(window, out pid);
        string process;
        try { using (var p = Process.GetProcessById((int)pid)) process = p.ProcessName; } catch { return null; }
        if (!Regex.IsMatch(process, @"^Code( - Insiders)?$", RegexOptions.IgnoreCase)) return null;
        var element = AutomationElement.FocusedElement;
        if (element == null) return null;
        // Chromium accessibility providers can live in a renderer process.
        // Verify that provider belongs to Code as well as the foreground app.
        try {
            using (var provider = Process.GetProcessById(element.Current.ProcessId))
                if (!Regex.IsMatch(provider.ProcessName, @"^Code( - Insiders)?$", RegexOptions.IgnoreCase)) return null;
        } catch { return null; }
        var current = element.Current;
        string identity = window + ":" + String.Join(".", element.GetRuntimeId());
        bool editable = current.ControlType == ControlType.Edit;
        bool codeEditor = Regex.IsMatch(current.ClassName ?? "", @"\binputarea\b|\bnative-edit-context\b", RegexOptions.IgnoreCase);
        object pattern;
        if (element.TryGetCurrentPattern(ValuePattern.Pattern, out pattern)) editable = !((ValuePattern)pattern).Current.IsReadOnly;
        bool password = current.IsPassword;
        editable &= current.IsEnabled && !password;
        string metadata = "";
        bool terminalName = false;
        for (int depth = 0; element != null && depth < 20; depth++) {
            current = element.Current;
            // Never use the top-level window title to guess the control's purpose.
            if (current.ControlType == ControlType.Window) break;
            metadata += " " + current.AutomationId + " " + current.ClassName;
            // Terminal textarea accessible labels are stable and distinct from their value.
            if (depth == 0) terminalName = Regex.IsMatch(current.Name ?? "", @"^(Terminal input|终端输入|终端输入框)$", RegexOptions.IgnoreCase);
            element = TreeWalker.ControlViewWalker.GetParent(element);
        }
        return new Snapshot { Window = window, Focus = Native.Focus(window), Identity = identity,
            Mode = password ? null : Rules.Decide(process, metadata, editable, terminalName, codeEditor, otherInputsChinese) };
    }
    static bool StillFocused(Snapshot s) {
        if (Native.GetForegroundWindow() != s.Window || Native.Focus(s.Window) != s.Focus) return false;
        var fresh = ReadFocus();
        return fresh != null && fresh.Identity == s.Identity && fresh.Mode == s.Mode;
    }
    static void Apply(Snapshot s) {
        if (s.Focus == IntPtr.Zero || !StillFocused(s)) return;
        IntPtr layout = Layouts.Resolve(s.Mode, s.Window);
        if (layout == IntPtr.Zero) { Console.WriteLine("No configured input method available for: " + s.Mode + " (keeping current input method)"); return; }
        bool posted = Native.PostMessage(s.Focus, 0x0050, IntPtr.Zero, layout);
        Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + " target=" + s.Mode + " layoutRequest=" + posted);
        Thread.Sleep(150);
        if (!running || !StillFocused(s)) return;
        IntPtr ime = Native.ImmGetDefaultIMEWnd(s.Focus);
        if (ime == IntPtr.Zero) return;
        UIntPtr result;
        // Change only the native conversion flag, preserving width and other user flags.
        IntPtr read = Native.SendMessageTimeout(ime, 0x0283, new IntPtr(1), IntPtr.Zero, 2, 100, out result);
        if (read != IntPtr.Zero) {
            long flags = (long)(result.ToUInt64() & 0xffffffff);
            flags = s.Mode == "zh" ? flags | 1 : flags & ~1L;
            Native.SendMessageTimeout(ime, 0x0283, new IntPtr(2), new IntPtr(flags), 2, 100, out result);
        }
        Native.SendMessageTimeout(ime, 0x0283, new IntPtr(6), new IntPtr(s.Mode == "zh" ? 1 : 0), 2, 100, out result);
    }
    static int SelfTest() {
        Layouts.SelfTest();
        if (Rules.Decide("Code", "xterm-helper-textarea", true, false) != "en" ||
            Rules.Decide("Code", "search", true, false, false, false) != null ||
            Rules.Decide("Code", "chat-input", true, false, true, false) != "zh" ||
            Rules.Decide("Code", "Codex chatgpt.sidebarView", true, false) != "zh" ||
            Rules.Decide("Code", "Codex xterm", true, false) != "en" ||
            Rules.Decide("Code", "editor", true, false, true) != null ||
            Rules.Decide("Code", "unknown-provider-composer", true, false) != "zh" ||
            Rules.Decide("Code", "dialog input", true, false) != "zh" ||
            Rules.Decide("Code", "chat-input-editor inputarea", true, false, true) != "zh" ||
            Rules.Decide("Code", "xterm chat-input", true, false, true) != "en" ||
            Rules.Decide("Code", "readonly", false, false) != null ||
            Rules.Decide("Code", "Codex", false, false) != null ||
            Rules.Decide("WeChat", "Codex", true, false) != null ||
            Rules.Decide("Code - Insiders", "", true, true) != "en") return 1;
        Console.WriteLine("Focus classification checks passed."); return 0;
    }
    [MTAThread] static int Main(string[] args) {
        Console.OutputEncoding = Encoding.UTF8;
        foreach (string arg in args) {
            if (arg == "--other-inputs=preserve") otherInputsChinese = false;
            if (arg.StartsWith("--poll=")) {
                int parsed;
                if (Int32.TryParse(arg.Substring(7), out parsed)) pollInterval = Math.Max(150, Math.Min(1000, parsed));
            }
        }
        if (args.Length > 0 && args[0] == "--self-test") return SelfTest();
        if (args.Length > 0 && args[0] == "--check-layouts") {
            foreach (string mode in new[] { "en", "zh" }) {
                IntPtr selected = Layouts.Resolve(mode, Native.GetForegroundWindow());
                Console.WriteLine(mode + " -> " + (selected == IntPtr.Zero ? "not configured" : Layouts.Language(selected)));
            }
            return 0;
        }
        if (args.Length > 0 && args[0] == "--probe") {
            var s = ReadFocus();
            Console.WriteLine(s == null ? "No supported VS Code focus" : "VS Code focus: " + (s.Mode ?? "unmatched"));
            return 0;
        }
        // Parent closes stdin on extension disposal or crash; helper exits with it.
        Task.Run(delegate { try { while (Console.Read() != -1) {} } catch {} running = false; });
        bool owns = false;
        using (var mutex = new Mutex(false, "Local\\VSCode.AutoInput.Helper")) {
            string last = null;
            DateTime lastError = DateTime.MinValue;
            try {
                while (running) {
                    // Multiple VS Code windows share one active worker; others can take over.
                    if (!owns) { try { owns = mutex.WaitOne(0); } catch (AbandonedMutexException) { owns = true; } }
                    if (owns) {
                        try {
                            var s = ReadFocus();
                            if (s == null) { last = null; }
                            else if (s.Identity != last) {
                                last = s.Identity;
                                if (s.Mode != null) Apply(s);
                                else Console.WriteLine("focus=preserve");
                            }
                        } catch (Exception error) {
                            last = null;
                            if ((DateTime.UtcNow - lastError).TotalSeconds > 10) {
                                Console.WriteLine("Focus query unavailable: " + error.GetType().Name);
                                lastError = DateTime.UtcNow;
                            }
                        }
                    }
                    Thread.Sleep(pollInterval);
                }
            } finally { if (owns) mutex.ReleaseMutex(); }
        }
        return 0;
    }
}
