using System.Runtime.InteropServices;
using System.Text;
namespace TranslateUni;

// Verify the uniquely titled window created for this game. A ConPTY console HWND
// is not the visible Windows Terminal window and cannot confirm fullscreen.
public sealed class TerminalFullscreen : IDisposable
{
    IntPtr window;int style;NativeDisplay.Rect original;bool changed,wasTopmost;
    public bool Applied {get;}
    public TerminalFullscreen(string[] args)
    {
        if(!OperatingSystem.IsWindows()||!args.Contains("--host-fullscreen"))return;
        string? option=args.FirstOrDefault(a=>a.StartsWith("--display-window-title=",StringComparison.Ordinal));
        if(option==null){DisplayDiagnostics.Add("Terminal fullscreen cannot be verified without its launch identity.");return;}
        string title=option["--display-window-title=".Length..];NativeDisplay.EnableDpi();
        var timer=System.Diagnostics.Stopwatch.StartNew();
        while(window==IntPtr.Zero&&timer.ElapsedMilliseconds<3000)
        {
            EnumWindows((candidate,_)=>
            {
                if(!NativeDisplay.IsWindowVisible(candidate))return true;
                var caption=new StringBuilder(512);GetWindowTextW(candidate,caption,caption.Capacity);
                if(!caption.ToString().Contains(title,StringComparison.Ordinal))return true;
                var className=new StringBuilder(128);GetClassNameW(candidate,className,className.Capacity);
                if(!className.ToString().Contains("CASCADIA",StringComparison.OrdinalIgnoreCase))return true;
                window=candidate;return false;
            },IntPtr.Zero);
            if(window==IntPtr.Zero)Thread.Sleep(50);
        }
        if(window==IntPtr.Zero||!NativeDisplay.GetWindowRect(window,out original)){DisplayDiagnostics.Add("The game's dedicated Windows Terminal window was not found.");return;}
        var monitor=NativeDisplay.Info();if(!NativeDisplay.GetMonitorInfoW(NativeDisplay.MonitorFromWindow(window,2),ref monitor))return;
        style=NativeDisplay.GetWindowLongW(window,-16);wasTopmost=(NativeDisplay.GetWindowLongW(window,-20)&8)!=0;
        var settling=System.Diagnostics.Stopwatch.StartNew();
        while(settling.ElapsedMilliseconds<1000&&NativeDisplay.GetWindowRect(window,out var pending)&&!ConsoleFullscreen.Covers(pending,monitor.Monitor))Thread.Sleep(50);
        NativeDisplay.GetWindowRect(window,out var settled);
        if(!ConsoleFullscreen.Covers(settled,monitor.Monitor))
        {
            DisplayDiagnostics.Add("Windows Terminal did not apply its fullscreen flag; fitting this game's window to the monitor.");
            NativeDisplay.SetWindowLongW(window,-16,style&~0x00CF0000);
            changed=NativeDisplay.SetWindowPos(window,new IntPtr(-1),monitor.Monitor.Left,monitor.Monitor.Top,monitor.Monitor.Width,monitor.Monitor.Height,0x0020|0x0040);
            Thread.Sleep(200);
        }
        Applied=NativeDisplay.GetWindowRect(window,out var bounds)&&ConsoleFullscreen.Covers(bounds,monitor.Monitor);
        DisplayDiagnostics.Add($"Windows Terminal fullscreen verified: {Applied}; monitor {monitor.Device}; bounds {bounds}");
    }
    public void Dispose()
    {
        if(!changed)return;
        NativeDisplay.SetWindowLongW(window,-16,style);
        NativeDisplay.SetWindowPos(window,wasTopmost?new IntPtr(-1):new IntPtr(-2),original.Left,original.Top,original.Width,original.Height,0x0020|0x0040);changed=false;
    }
    delegate bool Callback(IntPtr window,IntPtr data);
    [DllImport("user32.dll")]static extern bool EnumWindows(Callback callback,IntPtr data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowTextW(IntPtr window,StringBuilder text,int maximum);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassNameW(IntPtr window,StringBuilder text,int maximum);
}
