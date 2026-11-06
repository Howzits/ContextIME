using System;
using System.Runtime.InteropServices;

internal static class Native {
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] internal static extern bool GetGUIThreadInfo(uint id, ref GuiInfo info);
    [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] internal static extern IntPtr SendMessageTimeout(IntPtr h, uint m, IntPtr w, IntPtr l, uint flags, uint timeout, out UIntPtr result);
    [DllImport("user32.dll")] internal static extern IntPtr GetKeyboardLayout(uint thread);
    [DllImport("user32.dll")] internal static extern int GetKeyboardLayoutList(int count, [Out] IntPtr[] layouts);
    [DllImport("user32.dll", EntryPoint="SystemParametersInfoW")]
    internal static extern bool SystemParametersInfo(uint action, uint param, out IntPtr value, uint flags);
    [DllImport("imm32.dll")] internal static extern IntPtr ImmGetDefaultIMEWnd(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] internal struct Rect { public int L,T,R,B; }
    [StructLayout(LayoutKind.Sequential)] internal struct GuiInfo {
        public int size; public uint flags; public IntPtr active,focus,capture,menu,move,caret; public Rect rect;
    }
    internal static IntPtr Focus(IntPtr window) {
        uint pid;
        var info = new GuiInfo { size = Marshal.SizeOf(typeof(GuiInfo)) };
        if (!GetGUIThreadInfo(GetWindowThreadProcessId(window, out pid), ref info)) return IntPtr.Zero;
        return info.focus;
    }
}
