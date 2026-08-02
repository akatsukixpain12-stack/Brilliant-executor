using System;
using System.IO;
using System.Windows;
using System.Runtime.InteropServices;

namespace RblxExecutorUI;

public partial class App : Application
{
    // ── DPI shim: tell Win32 this process is Per-Monitor-V2 DPI aware
    // This is a belt-and-suspenders fix on top of the manifest declaration.
    // Needed because some .NET 6+ hosts reset DPI awareness.
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

    // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = -4
    private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = new IntPtr(-4);

    [DllImport("kernel32.dll")]
    static extern bool AllocConsole();

    public App()
    {
        // Set DPI awareness as early as possible — before any window is created
        try { SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2); }
        catch { /* older Windows — manifest fallback is fine */ }

        this.DispatcherUnhandledException += (s, e) =>
        {
            // Get the real inner exception message
            var ex = e.Exception;
            while (ex?.InnerException != null) ex = ex.InnerException;

            LogException(e.Exception, "Dispatcher");
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            LogException(args.ExceptionObject as Exception, "AppDomain");
        };
    }

    public static void LogException(Exception? ex, string source)
    {
        if (ex == null) return;

        // Unwrap to real cause
        var inner = ex;
        while (inner?.InnerException != null) inner = inner.InnerException;

        string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash_log.txt");
        string content = $"[CRASH-{source}] {DateTime.Now}\n" +
                         $"Outer: {ex.Message}\n{ex.StackTrace}\n\n" +
                         $"Root cause: {inner?.Message}\n{inner?.StackTrace}";
        try { File.AppendAllText(logPath, content + "\n\n"); } catch { }

        Console.WriteLine($"[CRASH-{source}] {inner?.Message}");

        // Show real error, not the wrapping "target of invocation" noise
        MessageBox.Show($"Error [{source}]:\n{inner?.Message ?? ex.Message}",
                        "Brilliant Executor Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
    }
}
