using System;
using System.Globalization;

internal static class Layouts {
    internal static string Language(IntPtr layout) {
        if (layout == IntPtr.Zero) return "";
        try { return CultureInfo.GetCultureInfo((int)(layout.ToInt64() & 0xffff)).TwoLetterISOLanguageName; }
        catch (CultureNotFoundException) { return ""; }
    }
    static bool Installed(IntPtr layout, IntPtr[] installed) {
        return layout != IntPtr.Zero && Array.IndexOf(installed, layout) >= 0;
    }
    internal static IntPtr Select(string mode, IntPtr preferred, IntPtr current, IntPtr[] installed) {
        // A Chinese default IME provides both native and Latin input modes.
        // Preserve that user's IME for English instead of forcing a US keyboard.
        if (Installed(preferred, installed) && (Language(preferred) == mode || (mode == "en" && Language(preferred) == "zh"))) return preferred;
        if (Installed(current, installed) && Language(current) == mode) return current;
        foreach (IntPtr layout in installed) if (Language(layout) == mode) return layout;
        if (mode == "en") {
            if (Installed(current, installed) && Language(current) == "zh") return current;
            foreach (IntPtr layout in installed) if (Language(layout) == "zh") return layout;
        }
        return IntPtr.Zero;
    }
    internal static IntPtr Resolve(string mode, IntPtr window) {
        // Read afresh at every focus change, including settings changed while running.
        int count = Native.GetKeyboardLayoutList(0, null);
        if (count <= 0) return IntPtr.Zero;
        var installed = new IntPtr[count];
        int copied = Native.GetKeyboardLayoutList(count, installed);
        if (copied <= 0) return IntPtr.Zero;
        Array.Resize(ref installed, copied);
        IntPtr preferred;
        if (!Native.SystemParametersInfo(0x0059, 0, out preferred, 0)) preferred = IntPtr.Zero;
        uint pid;
        IntPtr current = Native.GetKeyboardLayout(Native.GetWindowThreadProcessId(window, out pid));
        return Select(mode, preferred, current, installed);
    }
    internal static void SelfTest() {
        // Synthetic installed handles retain locale IDs but use distinct device IDs.
        var traditional = new IntPtr((0x1111 << 16) | CultureInfo.GetCultureInfo("zh-TW").LCID);
        var simplified = new IntPtr((0x2222 << 16) | CultureInfo.GetCultureInfo("zh-CN").LCID);
        var british = new IntPtr((0x3333 << 16) | CultureInfo.GetCultureInfo("en-GB").LCID);
        var american = new IntPtr((0x4444 << 16) | CultureInfo.GetCultureInfo("en-US").LCID);
        var installed = new[] { american, simplified, traditional, british };
        if (Select("zh", traditional, simplified, installed) != traditional ||
            Select("en", british, american, installed) != british ||
            Select("en", traditional, american, installed) != traditional ||
            Select("zh", british, traditional, installed) != traditional ||
            Select("zh", traditional, british, new[] { british }) != IntPtr.Zero ||
            Select("en", IntPtr.Zero, simplified, new[] { simplified }) != simplified ||
            Select("en", traditional, british, new[] { british }) != british ||
            Select("en", IntPtr.Zero, IntPtr.Zero, new IntPtr[0]) != IntPtr.Zero)
            throw new Exception("Default/installed layout selection checks failed");
        Console.WriteLine("Default/installed layout selection checks passed.");
    }
}
