using System;
using System.Text.RegularExpressions;

internal sealed class Snapshot {
    internal IntPtr Window, Focus;
    internal string Identity, Mode;
}

internal static class Rules {
    internal static string Decide(string process, string metadata, bool editable, bool terminalName, bool codeEditor = false, bool otherInputsChinese = true) {
        if (!Regex.IsMatch(process, @"^Code( - Insiders)?$", RegexOptions.IgnoreCase)) return null;
        if (terminalName || Regex.IsMatch(metadata, @"xterm|terminal[ -]?(input|textarea)", RegexOptions.IgnoreCase)) return "en";
        // Chat extensions may embed Monaco too; semantic chat containers take priority.
        bool chatEditor = Regex.IsMatch(metadata, @"chat[-_]input|chat[-_]editor|interactive[-_]input|chatgpt|codex", RegexOptions.IgnoreCase);
        if (editable && (chatEditor || (!codeEditor && otherInputsChinese))) return "zh";
        return null;
    }
}
