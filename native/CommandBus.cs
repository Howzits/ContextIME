using System;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;
using System.Threading;

// All VS Code windows share the worker mutex. Route commands to its owner.
internal static class CommandBus {
    static readonly string PipeName = "ContextIME-" + System.Diagnostics.Process.GetCurrentProcess().SessionId + "-" + System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
    internal static void Listen(Action<string, Action<string>> receive, string suffix = "") {
        Task.Run(delegate {
            while (true) {
                try {
                    using (var server = new NamedPipeServerStream(PipeName + suffix, PipeDirection.InOut)) {
                        server.WaitForConnection();
                        // Commands contain no control text, paths, or user-entered content.
                        using (var reader = new StreamReader(server)) {
                            string command = reader.ReadLine();
                            if (command != null && command.Length < 32) {
                                string response = "command=timeout";
                                using (var ready = new ManualResetEventSlim(false)) {
                                    object gate = new object();
                                    bool completed = false;
                                    receive(command, delegate(string result) {
                                        lock (gate) { if (!completed) { response = result; ready.Set(); } }
                                    });
                                    ready.Wait(10000);
                                    lock (gate) { completed = true; }
                                    using (var writer = new StreamWriter(server)) { writer.WriteLine(response); writer.Flush(); }
                                }
                            }
                        }
                    }
                } catch (IOException) { }
            }
        });
    }
    internal static string Send(string command, string suffix = "") {
        try {
            using (var client = new NamedPipeClientStream(".", PipeName + suffix, PipeDirection.InOut)) {
                client.Connect(300);
                var writer = new StreamWriter(client);
                writer.WriteLine(command); writer.Flush();
                using (var reader = new StreamReader(client)) return reader.ReadLine() ?? "command=unavailable";
            }
        } catch (TimeoutException) { return "command=unavailable (reload all VS Code windows)"; }
          catch (IOException) { return "command=unavailable"; }
    }
    internal static void SelfTest() {
        string suffix = "-test-" + Guid.NewGuid().ToString("N");
        Listen(delegate(string request, Action<string> reply) { reply("response:" + request); }, suffix);
        if (Send("diagnose", suffix) != "response:diagnose") throw new Exception("Command pipe round trip failed");
        Console.WriteLine("Command pipe round trip passed.");
    }
}
