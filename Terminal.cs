using System.Runtime.InteropServices;
using System.Text;

namespace TranslateUni;

public sealed record Input(ConsoleKey Key = ConsoleKey.NoName, int X = -1, int Y = -1);
public sealed record HitBox(int X, int Y, int Width, int Height, string Action, int Index = -1);

public sealed class Canvas
{
    public int Width { get; }
    public int Height { get; }
    readonly char[,] cells;
    readonly ConsoleColor[,] colors;
    public List<HitBox> Buttons { get; } = [];
    public Canvas(int width, int height)
    {
        Width = width; Height = height;
        cells = new char[height, width]; colors = new ConsoleColor[height, width];
        for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
        { cells[y, x] = ' '; colors[y, x] = ConsoleColor.Gray; }
    }
    public void Text(int x, int y, string text, ConsoleColor color = ConsoleColor.Gray)
    {
        if (y < 0 || y >= Height) return;
        for (int i = 0; i < text.Length; i++) if (x + i >= 0 && x + i < Width)
        { cells[y, x + i] = text[i]; colors[y, x + i] = color; }
    }
    public void Center(int y, string text, ConsoleColor color = ConsoleColor.Gray)
        => Text(Math.Max(0, (Width - text.Length) / 2), y, text, color);
    public void Rule(int y, ConsoleColor color = ConsoleColor.DarkCyan)
        => Text(2, y, new string('-', Math.Max(0, Width - 4)), color);
    public void Box(int x, int y, int width, int height, ConsoleColor color = ConsoleColor.Gray)
    {
        Text(x, y, "+" + new string('-', width - 2) + "+", color);
        for (int row = 1; row < height - 1; row++) Text(x, y + row, "|" + new string(' ', width - 2) + "|", color);
        Text(x, y + height - 1, "+" + new string('-', width - 2) + "+", color);
    }
    public void Button(int x, int y, string label, string action, int index = -1, bool selected = false)
    {
        string text = "[ " + label + " ]";
        Text(x, y, text, selected ? ConsoleColor.Yellow : ConsoleColor.Cyan);
        Buttons.Add(new(x, y, text.Length, 1, action, index));
    }
    public int Wrapped(int x, int y, string text, int width, int maxLines = 5, ConsoleColor color = ConsoleColor.Gray)
    {
        int row = y; string line = "";
        foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length + word.Length + 1 > width && line.Length > 0)
            { Text(x, row++, line, color); line = ""; if (row - y == maxLines) return row; }
            line += (line.Length == 0 ? "" : " ") + word;
        }
        if (line.Length > 0 && row - y < maxLines) Text(x, row++, line, color);
        return row;
    }
    public string[] Lines() => Enumerable.Range(0, Height).Select(y =>
        new string(Enumerable.Range(0, Width).Select(x => cells[y, x]).ToArray())).ToArray();
    public void Recolor(int x, int y, int width, ConsoleColor color)
    { if (y < 0 || y >= Height) return; for(int i=Math.Max(0,x);i<Math.Min(Width,x+width);i++) colors[y,i]=color; }
    public ConsoleColor ColorAt(int x, int y) => colors[y, x];
    public void Paste(Canvas source, int left, int top)
    {
        for (int y = 0; y < source.Height && y + top < Height; y++)
            for (int x = 0; x < source.Width && x + left < Width; x++)
            { cells[y + top, x + left] = source.cells[y, x]; colors[y + top, x + left] = source.colors[y, x]; }
        Buttons.AddRange(source.Buttons.Select(b => b with { X = b.X + left, Y = b.Y + top }));
    }
    public void Flush()
    {
        for (int y = 0; y < Height; y++)
        {
            Console.SetCursorPosition(Console.WindowLeft, Console.WindowTop + y);
            ConsoleColor? last = null;
            var text = new StringBuilder();
            // Reserve the final column, preventing terminal wrapping/scrolling.
            for (int x = 0; x < Width - 1; x++)
            {
                if (colors[y, x] != last)
                {
                    if (text.Length > 0) { Console.Write(text.ToString()); text.Clear(); }
                    GameTheme.Foreground(colors[y, x]); last = colors[y, x];
                }
                text.Append(cells[y, x]);
            }
            if (text.Length > 0) Console.Write(text.ToString());
        }
        Console.ResetColor();
    }
    public void FlushRegions(IEnumerable<HitBox> regions)
    {
        foreach (HitBox region in regions)
        for (int y = Math.Max(0, region.Y); y < Math.Min(Height, region.Y + region.Height); y++)
        {
            int left = Math.Max(0, region.X), right = Math.Min(Width - 1, region.X + region.Width);
            if (right <= left) continue;
            Console.SetCursorPosition(Console.WindowLeft + left, Console.WindowTop + y);
            ConsoleColor? last = null; var text = new StringBuilder();
            for (int x = left; x < right; x++)
            {
                if (colors[y, x] != last)
                {
                    if (text.Length > 0) { Console.Write(text.ToString()); text.Clear(); }
                    GameTheme.Foreground(colors[y, x]); last = colors[y, x];
                }
                text.Append(cells[y, x]);
            }
            if (text.Length > 0) Console.Write(text.ToString());
        }
        Console.ResetColor();
    }
}

public sealed class TerminalInput : IDisposable
{
    readonly IntPtr handle;
    readonly uint originalMode;
    readonly bool mouseEnabled;
    readonly bool cursorVisible;
    bool leftDown;
    readonly InputReleaseGuard releaseGuard = new();
    public TerminalInput()
    {
        cursorVisible = OperatingSystem.IsWindows() ? Console.CursorVisible : true;
        Console.CursorVisible = false;
        if (!OperatingSystem.IsWindows() || Console.IsInputRedirected) return;
        handle = GetStdHandle(-10);
        if (!GetConsoleMode(handle, out originalMode)) return;
        mouseEnabled = SetConsoleMode(handle, (originalMode | 0x0010u | 0x0080u | 0x0008u) & ~0x0040u);
    }
    public Input Read()
    {
        if (!mouseEnabled) return new(Console.ReadKey(true).Key);
        while (true)
        {
            if (!ReadConsoleInputW(handle, out InputRecord record, 1, out uint count)) return new(Console.ReadKey(true).Key);
            if (count == 0) continue;
            if (record.Type == 4) return new(); // Resize: repaint the current screen.
            if (record.Type == 1)
            { ConsoleKey key=(ConsoleKey)record.VirtualKey; if(releaseGuard.Accept(key,record.KeyDown != 0)) return new(key); continue; }
            if (record.Type != 2) continue;
            bool pressed = (record.Buttons & 1) != 0;
            bool click = pressed && !leftDown && record.MouseFlags == 0;
            leftDown = pressed;
            if (click) return new(X: record.MouseX - Console.WindowLeft, Y: record.MouseY - Console.WindowTop);
        }
    }
    public void DiscardPending()
    {
        if (OperatingSystem.IsWindows() && handle != IntPtr.Zero && FlushConsoleInputBuffer(handle))
        {
            releaseGuard.Block(Enum.GetValues<ConsoleKey>().Where(key => (GetAsyncKeyState((int)key) & 0x8000) != 0));
            leftDown = (GetAsyncKeyState(1) & 0x8000) != 0;
            return;
        }
        while (Console.KeyAvailable) Console.ReadKey(true);
    }
    public string VerifyQueueBoundary()
    {
        if(!mouseEnabled) throw new InvalidOperationException("Native input mode is unavailable for this test.");
        var queued=new[] { new InputRecord { Type=1, KeyDown=1, RepeatCount=5, VirtualKey=(ushort)ConsoleKey.Enter, Character='\r' }, new InputRecord { Type=1, KeyDown=1, RepeatCount=5, VirtualKey=(ushort)ConsoleKey.D1, Character='1' } };
        if(!WriteConsoleInputW(handle,queued,(uint)queued.Length,out _)) throw new IOException("Could not queue input test records.");
        DiscardPending();
        var fresh=new[] { new InputRecord { Type=1, KeyDown=1, RepeatCount=1, VirtualKey=(ushort)ConsoleKey.F24 } };
        if(!WriteConsoleInputW(handle,fresh,1,out _)) throw new IOException("Could not write fresh input record.");
        Input next=Read(); DiscardPending();
        if(next.Key!=ConsoleKey.F24) throw new InvalidOperationException("A stale input survived the screen boundary: "+next.Key);
        return "PASS: queued Enter and number repeats discarded; fresh key accepted.";
    }
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] static extern bool WriteConsoleInputW(IntPtr input,InputRecord[] records,uint count,out uint written);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool FlushConsoleInputBuffer(IntPtr input);
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    public void Dispose()
    {
        if (mouseEnabled) SetConsoleMode(handle, originalMode);
        Console.ResetColor(); Console.CursorVisible = cursorVisible;
    }
    [StructLayout(LayoutKind.Explicit, Size = 20)]
    struct InputRecord
    {
        [FieldOffset(0)] public ushort Type;
        [FieldOffset(4)] public int KeyDown;
        [FieldOffset(8)] public ushort RepeatCount;
        [FieldOffset(10)] public ushort VirtualKey;
        [FieldOffset(14)] public char Character;
        [FieldOffset(4)] public short MouseX;
        [FieldOffset(6)] public short MouseY;
        [FieldOffset(8)] public uint Buttons;
        [FieldOffset(16)] public uint MouseFlags;
    }
    [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int id);
    [DllImport("kernel32.dll")] static extern bool GetConsoleMode(IntPtr handle, out uint mode);
    [DllImport("kernel32.dll")] static extern bool SetConsoleMode(IntPtr handle, uint mode);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool ReadConsoleInputW(IntPtr handle, out InputRecord record, uint length, out uint count);
}

