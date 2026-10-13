using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;

namespace TranslateUni;

public static class FullscreenLauncher
{
    // A dedicated Windows Terminal window supports real fullscreen without changing user settings.
    public static bool RelaunchIfAvailable(string[] args)
    {
        if (!OperatingSystem.IsWindows() || args.Contains("--windowed") || args.Contains("--host-fullscreen")) return false;
        string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "wt.exe");
        if (!File.Exists(path)) return false;
        try
        {
            var process = new ProcessStartInfo(path) { UseShellExecute = true };
            foreach (string argument in new[] { "--window", "new", "--fullscreen", "new-tab", "--title", "TranslateUni", "--startingDirectory", AppContext.BaseDirectory })
                process.ArgumentList.Add(argument);
            string executable = Environment.ProcessPath ?? "dotnet";
            process.ArgumentList.Add(executable);
            if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                process.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            foreach (string argument in args) process.ArgumentList.Add(argument);
            process.ArgumentList.Add("--host-fullscreen");
            using Process? launched = Process.Start(process);
            return launched != null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException)
        { return false; }
    }
}

public sealed class ConsoleFullscreen : IDisposable
{
    IntPtr window;
    int originalStyle;
    Rect originalRect;
    bool changed;
    EventHandler? exitHandler;
    public ConsoleFullscreen(bool skip)
    {
        if (skip || !OperatingSystem.IsWindows()) return;
        window = GetConsoleWindow();
        // ConPTY has an invisible proxy window; never resize another foreground application.
        if (window == IntPtr.Zero || !IsWindowVisible(window) || !GetWindowRect(window, out originalRect)) return;
        originalStyle = GetWindowLongW(window, -16);
        IntPtr monitor = MonitorFromWindow(window, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfoW(monitor, ref info)) return;
        SetWindowLongW(window, -16, originalStyle & ~0x00CF0000); // Remove the caption and frame.
        changed = SetWindowPos(window, IntPtr.Zero, info.Monitor.Left, info.Monitor.Top,
            info.Monitor.Right - info.Monitor.Left, info.Monitor.Bottom - info.Monitor.Top, 0x0020 | 0x0040);
        if (!changed) SetWindowLongW(window, -16, originalStyle);
        else
        {
            exitHandler = (_, _) => Dispose();
            AppDomain.CurrentDomain.ProcessExit += exitHandler;
        }
    }
    public void Dispose()
    {
        if (!changed) return;
        changed = false;
        if (exitHandler != null) AppDomain.CurrentDomain.ProcessExit -= exitHandler;
        SetWindowLongW(window, -16, originalStyle);
        SetWindowPos(window, IntPtr.Zero, originalRect.Left, originalRect.Top,
            originalRect.Right - originalRect.Left, originalRect.Bottom - originalRect.Top, 0x0020 | 0x0040 | 0x0004);
    }
    [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo { public int Size; public Rect Monitor, Work; public uint Flags; }
    [DllImport("kernel32.dll")] static extern IntPtr GetConsoleWindow();
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] static extern int GetWindowLongW(IntPtr window, int index);
    [DllImport("user32.dll")] static extern int SetWindowLongW(IntPtr window, int index, int value);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfoW(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
}
