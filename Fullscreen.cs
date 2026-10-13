using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace TranslateUni;

public static class DisplayDiagnostics
{
    public static readonly string ReportPath = Path.Combine(AppContext.BaseDirectory, "data", "display-diagnostics.txt");
    static readonly StringBuilder report = new();
    public static void Add(string text) => report.AppendLine(text);
    public static void Save()
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)!); File.WriteAllText(ReportPath, report.ToString()); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
    public static string Capture()
    {
        report.Clear();
        Add($"TranslateUni display check - {DateTime.Now:O}");
        Add($"OS: {RuntimeInformation.OSDescription}; OS architecture: {RuntimeInformation.OSArchitecture}; process: {RuntimeInformation.ProcessArchitecture}");
        Add($"Runtime: {RuntimeInformation.FrameworkDescription}");
        Add($"Input redirected: {Console.IsInputRedirected}; output redirected: {Console.IsOutputRedirected}");
        if (!OperatingSystem.IsWindows()) { Add("Automatic fullscreen is supported on Windows only. Use your terminal's fullscreen command."); return report.ToString(); }
        NativeDisplay.EnableDpi();
        IntPtr window = NativeDisplay.GetConsoleWindow();
        bool visible = window != IntPtr.Zero && NativeDisplay.IsWindowVisible(window);
        Add($"Host: {(visible ? "visible Windows console" : Environment.GetEnvironmentVariable("WT_SESSION") != null ? "Windows Terminal / ConPTY" : "embedded terminal / ConPTY or no console")}");
        Add($"Windows Terminal executable: {FullscreenLauncher.FindTerminal() ?? "not found"}");
        int count = 0;
        NativeDisplay.MonitorCallback callback = (IntPtr monitor, IntPtr dc, ref NativeDisplay.Rect rect, IntPtr data) =>
        {
            var info = NativeDisplay.Info();
            if (NativeDisplay.GetMonitorInfoW(monitor, ref info))
            {
                count++;
                uint dpiX = 0, dpiY = 0;
                try { if (NativeDisplay.GetDpiForMonitor(monitor, 0, out dpiX, out dpiY) != 0) dpiX = dpiY = 0; }
                catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException) { }
                Add($"Monitor {count}: {info.Device} {(info.Flags == 1 ? "(primary)" : "")}; desktop resolution {info.Monitor.Width} x {info.Monitor.Height} pixels; bounds {info.Monitor}; work area {info.Work.Width} x {info.Work.Height}; DPI {(dpiX == 0 ? "unknown" : $"{dpiX} x {dpiY}, {dpiX * 100 / 96}% scale")}");
            }
            return true;
        };
        if (!NativeDisplay.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero)) Add($"Monitor enumeration failed: {Marshal.GetLastWin32Error()}");
        if (count == 0) Add("No monitor available in this desktop session.");
        Add("Monitor size above is pixel dimensions; physical inches are not inferred.");
        LogConsoleSize();
        return report.ToString();
    }
    public static void LogConsoleSize()
    {
        try { Add($"Console grid: {Console.WindowWidth} x {Console.WindowHeight}; largest grid at current font: {Console.LargestWindowWidth} x {Console.LargestWindowHeight}; game minimum: 108 x 40"); }
        catch (IOException ex) { Add("Console dimensions unavailable: " + ex.Message); }
    }
}

public static class FullscreenLauncher
{
    public static string? FindTerminal()
    {
        var candidates = new List<string> { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "WindowsApps", "wt.exe") };
        candidates.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => Path.Combine(p.Trim('"'), "wt.exe")));
        return candidates.FirstOrDefault(File.Exists);
    }
    public static bool RelaunchIfAvailable(string[] args)
    {
        if (!OperatingSystem.IsWindows() || args.Contains("--windowed") || args.Contains("--host-fullscreen") || args.Contains("--classic-host")) return false;
        // A visible classic console is handled directly. Embedded consoles need a dedicated host.
        IntPtr window = NativeDisplay.GetConsoleWindow();
        if (window != IntPtr.Zero && NativeDisplay.IsWindowVisible(window)) return false;
        string? terminal = FindTerminal();
        if (terminal != null && Launch(terminal, args, true)) return true;
        string classic = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "conhost.exe");
        if (File.Exists(classic) && Launch(classic, args, false)) return true;
        DisplayDiagnostics.Add("No dedicated fullscreen host could be launched. Continuing in the current terminal.");
        return false;
    }
    static bool Launch(string host, string[] args, bool terminal)
    {
        string handshake = Path.Combine(Path.GetTempPath(), "TranslateUni-display-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var start = new ProcessStartInfo(host) { UseShellExecute = false, WorkingDirectory = AppContext.BaseDirectory };
            if (terminal)
                foreach (string arg in new[] { "--window", "new", "--fullscreen", "new-tab", "--title", "TranslateUni", "--startingDirectory", AppContext.BaseDirectory }) start.ArgumentList.Add(arg);
            string executable = Environment.ProcessPath ?? "dotnet";
            start.ArgumentList.Add(executable);
            if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            foreach (string arg in args) start.ArgumentList.Add(arg);
            start.ArgumentList.Add(terminal ? "--host-fullscreen" : "--classic-host");
            start.ArgumentList.Add("--display-handshake=" + handshake);
            // Avoid identifying a classic child as the parent's Windows Terminal session.
            if (!terminal) start.Environment.Remove("WT_SESSION");
            using Process? process = terminal ? Process.Start(start) : StartDedicated(start);
            if (process == null) return false;
            var timer = Stopwatch.StartNew();
            while (timer.ElapsedMilliseconds < 8000)
            {
                if (File.Exists(handshake))
                {
                    string status = File.ReadAllText(handshake);
                    DisplayDiagnostics.Add($"Dedicated {(terminal ? "Windows Terminal" : "classic console")} startup: {status}");
                    if (status != "ready" && File.Exists(DisplayDiagnostics.ReportPath)) DisplayDiagnostics.Add("Child host report: " + File.ReadAllText(DisplayDiagnostics.ReportPath));
                    return status == "ready";
                }
                Thread.Sleep(50);
            }
            DisplayDiagnostics.Add($"Host launched but the game did not acknowledge startup: {host}");
            return false;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException or InvalidOperationException or UnauthorizedAccessException)
        { DisplayDiagnostics.Add("Host launch failed: " + ex.Message); return false; }
        finally { try { File.Delete(handshake); } catch (IOException) { } }
    }
    static Process? StartDedicated(ProcessStartInfo start)
    {
        // CREATE_NEW_CONSOLE prevents inheriting an IDE / pseudoconsole's hidden host.
        string command = Quote(start.FileName) + " " + string.Join(" ", start.ArgumentList.Select(Quote));
        var startup = new StartupInfo { Size = Marshal.SizeOf<StartupInfo>() };
        if (!CreateProcessW(start.FileName, new StringBuilder(command), IntPtr.Zero, IntPtr.Zero, false, 0x10, IntPtr.Zero, start.WorkingDirectory, ref startup, out var info))
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        try { return Process.GetProcessById((int)info.ProcessId); }
        finally { CloseHandle(info.Process); CloseHandle(info.Thread); }
    }
    static string Quote(string value)
    {
        var text = new StringBuilder("\"");
        int slashes = 0;
        foreach (char c in value)
        {
            if (c == '\\') { slashes++; continue; }
            text.Append('\\', c == '"' ? slashes * 2 + 1 : slashes); text.Append(c); slashes = 0;
        }
        return text.Append('\\', slashes * 2).Append('"').ToString();
    }
    [StructLayout(LayoutKind.Sequential)] struct StartupInfo
    { public int Size; public IntPtr Reserved, Desktop, Title; public uint X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags; public ushort ShowWindow, Reserved2Size; public IntPtr Reserved2, Input, Output, Error; }
    [StructLayout(LayoutKind.Sequential)] struct ProcessInfo { public IntPtr Process, Thread; public uint ProcessId, ThreadId; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool CreateProcessW(string application, StringBuilder command, IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles, uint flags, IntPtr environment, string directory, ref StartupInfo startup, out ProcessInfo process);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);

    public static void Acknowledge(string[] args, bool ready)
    {
        string? option = args.FirstOrDefault(a => a.StartsWith("--display-handshake=", StringComparison.Ordinal));
        if (option == null) return;
        try { File.WriteAllText(option["--display-handshake=".Length..], ready ? "ready" : "failed"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { DisplayDiagnostics.Add("Startup acknowledgement failed: " + ex.Message); }
    }
}

public sealed class ConsoleFullscreen : IDisposable
{
    readonly IntPtr window;
    readonly int originalStyle;
    readonly NativeDisplay.Rect originalRect;
    bool nativeMode, changed;
    EventHandler? exitHandler;
    public bool Applied { get; private set; }
    public ConsoleFullscreen(bool skip)
    {
        if (skip || !OperatingSystem.IsWindows()) { DisplayDiagnostics.Add("Native fullscreen skipped (windowed mode or terminal-managed fullscreen)."); return; }
        NativeDisplay.EnableDpi();
        window = NativeDisplay.GetConsoleWindow();
        if (window == IntPtr.Zero || !NativeDisplay.IsWindowVisible(window) || !NativeDisplay.GetWindowRect(window, out originalRect))
        { DisplayDiagnostics.Add("Cannot resize this host: its console window is hidden or unavailable."); return; }
        originalStyle = NativeDisplay.GetWindowLongW(window, -16);
        IntPtr monitor = NativeDisplay.MonitorFromWindow(window, 2);
        var info = NativeDisplay.Info();
        if (!NativeDisplay.GetMonitorInfoW(monitor, ref info)) { DisplayDiagnostics.Add("Cannot read selected monitor: " + Marshal.GetLastWin32Error()); return; }
        DisplayDiagnostics.Add($"Selected monitor: {info.Device}, {info.Monitor.Width} x {info.Monitor.Height}");
        FitFont(info.Monitor.Width, info.Monitor.Height);
        nativeMode = NativeDisplay.SetConsoleDisplayMode(NativeDisplay.GetStdHandle(-11), 1, out _);
        if (!nativeMode)
        {
            DisplayDiagnostics.Add("Native fullscreen API failed: " + Marshal.GetLastWin32Error() + "; requesting console Alt+Enter.");
            // Send only to our own visible console window, never to the foreground app.
            NativeDisplay.PostMessageW(window, 0x0104, new IntPtr(13), new IntPtr(0x201C0001));
            Thread.Sleep(300);
            nativeMode = NativeDisplay.GetWindowRect(window, out var fullBounds) && Covers(fullBounds, info.Monitor);
        }
        if (!nativeMode)
        {
            NativeDisplay.SetWindowLongW(window, -16, originalStyle & ~0x00CF0000);
            changed = NativeDisplay.SetWindowPos(window, IntPtr.Zero, info.Monitor.Left, info.Monitor.Top, info.Monitor.Width, info.Monitor.Height, 0x0020 | 0x0040);
        }
        Thread.Sleep(150); // Console hosts update their frame asynchronously.
        if (NativeDisplay.GetWindowRect(window, out var bounds)) DisplayDiagnostics.Add("Resulting window bounds: " + bounds);
        NativeDisplay.GetConsoleDisplayMode(out uint mode);
        Applied = (mode & 1) != 0 || (changed && NativeDisplay.GetWindowRect(window, out var actual) && Covers(actual, info.Monitor));
        DisplayDiagnostics.Add($"Fullscreen verified: {Applied}; native mode: {nativeMode}; display mode flags: {mode}");
        if (nativeMode || changed)
        { exitHandler = (_, _) => Dispose(); AppDomain.CurrentDomain.ProcessExit += exitHandler; }
        else NativeDisplay.SetWindowLongW(window, -16, originalStyle);
    }
    public static bool Covers(NativeDisplay.Rect actual, NativeDisplay.Rect monitor) =>
        actual.Left <= monitor.Left + 2 && actual.Top <= monitor.Top + 2 && actual.Right >= monitor.Right - 2 && actual.Bottom >= monitor.Bottom - 2;
    static void FitFont(int pixelsX, int pixelsY)
    {
        var font = new NativeDisplay.FontInfo { Size = (uint)Marshal.SizeOf<NativeDisplay.FontInfo>(), FaceName = "" };
        IntPtr output = NativeDisplay.GetStdHandle(-11);
        if (!NativeDisplay.GetCurrentConsoleFontEx(output, false, ref font)) return;
        int maxHeight = Math.Clamp(pixelsY / 44, 8, 32);
        int maxWidth = Math.Clamp(pixelsX / 122, 4, 16);
        if (font.FontSize.Y <= maxHeight && font.FontSize.X <= maxWidth) return;
        font.FaceName = "Consolas"; font.FontSize = new NativeDisplay.Coord { X = 0, Y = (short)Math.Min(maxHeight, maxWidth * 2) };
        if (!NativeDisplay.SetCurrentConsoleFontEx(output, false, ref font)) DisplayDiagnostics.Add("Font fitting failed: " + Marshal.GetLastWin32Error());
        else DisplayDiagnostics.Add("Reduced console font to fit the detected monitor resolution.");
    }
    public void Dispose()
    {
        if (!nativeMode && !changed) return;
        if (exitHandler != null) AppDomain.CurrentDomain.ProcessExit -= exitHandler;
        if (nativeMode && !NativeDisplay.SetConsoleDisplayMode(NativeDisplay.GetStdHandle(-11), 2, out _))
        { NativeDisplay.PostMessageW(window, 0x0104, new IntPtr(13), new IntPtr(0x201C0001)); Thread.Sleep(100); }
        NativeDisplay.SetWindowLongW(window, -16, originalStyle);
        NativeDisplay.SetWindowPos(window, IntPtr.Zero, originalRect.Left, originalRect.Top, originalRect.Width, originalRect.Height, 0x0020 | 0x0040 | 0x0004);
        nativeMode = changed = false;
    }
}

public static class NativeDisplay
{
    public static void EnableDpi()
    {
        try { if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 15063)) SetThreadDpiAwarenessContext(new IntPtr(-4)); }
        catch (EntryPointNotFoundException) { }
    }
    [StructLayout(LayoutKind.Sequential)] public struct Rect
    { public int Left, Top, Right, Bottom; public readonly int Width => Right - Left; public readonly int Height => Bottom - Top; public override readonly string ToString() => $"({Left},{Top})-({Right},{Bottom})"; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] public struct MonitorInfo
    { public int Size; public Rect Monitor, Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device; }
    public static MonitorInfo Info() => new() { Size = Marshal.SizeOf<MonitorInfo>(), Device = "" };
    [StructLayout(LayoutKind.Sequential)] public struct Coord { public short X, Y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] public struct FontInfo
    { public uint Size, Font; public Coord FontSize; public int Family, Weight; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string FaceName; }
    public delegate bool MonitorCallback(IntPtr monitor, IntPtr dc, ref Rect rect, IntPtr data);
    [DllImport("kernel32.dll")] public static extern IntPtr GetConsoleWindow();
    [DllImport("kernel32.dll")] public static extern IntPtr GetStdHandle(int id);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool SetConsoleDisplayMode(IntPtr output, uint flags, out Coord size);
    [DllImport("kernel32.dll", SetLastError = true)] public static extern bool GetConsoleDisplayMode(out uint flags);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool GetCurrentConsoleFontEx(IntPtr output, bool maximum, ref FontInfo font);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool SetCurrentConsoleFontEx(IntPtr output, bool maximum, ref FontInfo font);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool PostMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern int GetWindowLongW(IntPtr window, int index);
    [DllImport("user32.dll", SetLastError = true)] public static extern int SetWindowLongW(IntPtr window, int index, int value);
    [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] public static extern bool GetMonitorInfoW(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip, MonitorCallback callback, IntPtr data);
    [DllImport("user32.dll", SetLastError = true)] public static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr value);
    [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
}
