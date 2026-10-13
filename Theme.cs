using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
namespace TranslateUni;

public static class GameTheme
{
    public static readonly (int R,int G,int B)[] Palette =
    [(12,16,23),(75,96,120),(91,118,96),(94,123,128),(133,95,93),(122,103,134),(140,127,99),(174,184,195),
     (100,112,126),(126,156,184),(141,171,146),(129,166,171),(188,139,136),(171,150,182),(182,167,137),(210,216,223)];
    public static bool TrueColor { get; internal set; }
    public static ConsoleColor Statistic(string word) => word.ToLowerInvariant() switch
    {
        "cash" or "spend" or "income" or "profit" or "revenue" or "sales" or "borrowed" => ConsoleColor.Green,
        "debt" or "repay" or "pay" or "fee" or "interest" => ConsoleColor.Red,
        "price" or "unit price" => ConsoleColor.Cyan,
        "stock" or "inventory" or "restock" or "sold" => ConsoleColor.Blue,
        "cost" or "unit cost" => ConsoleColor.Magenta,
        "share" or "market share" => ConsoleColor.Yellow,
        "capacity" or "cap" => ConsoleColor.DarkCyan,
        "quality" => ConsoleColor.DarkGreen,
        "risk" => ConsoleColor.DarkRed,
        "service" or "demand" => ConsoleColor.DarkBlue,
        _ => ConsoleColor.Gray
    };
    public static void Foreground(ConsoleColor color)
    {
        if (!TrueColor) { Console.ForegroundColor = color; return; }
        var rgb = Palette[(int)color]; Console.Write($"\u001b[38;2;{rgb.R};{rgb.G};{rgb.B}m");
    }
    static readonly Regex Mention = new(@"(?<![a-zA-Z])(unit\s+cost|unit\s+price|market\s+share|cash|debt|price|inventory|restock|stock|cost|share|capacity|quality|risk|service|profit|revenue|income|sales|sold|spend|repay|pay|borrowed|fee|interest)(?![a-zA-Z])", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly Regex Value = new(@"^\s*(?:before reward\s*)?(?::|>=|<=)?\s*[+-]?\s*\$?\s*[+-]?\d[\d,.]*(?:[kKmM%])?", RegexOptions.Compiled);
    public static void ColorStatistics(Canvas canvas)
    {
        var lines = canvas.Lines();
        for(int row=0;row<canvas.Height;row++)
        foreach(Match match in Mention.Matches(lines[row]))
        {
            string metric = Regex.Replace(match.Value, @"\s+", " ");
            ConsoleColor color = Statistic(metric);
            canvas.Recolor(match.Index,row,match.Length,color);
            Match amount=Value.Match(lines[row][(match.Index+match.Length)..]);
            if(amount.Success) canvas.Recolor(match.Index+match.Length,row,amount.Length,color);
        }
    }
}
public sealed class TerminalTheme : IDisposable
{
    readonly IntPtr output; readonly uint oldMode; readonly bool changed;
    uint[]? originalColors;
    EventHandler? exitHandler;
    bool disposed;
    public bool PaletteApplied { get; private set; }
    static readonly int[] ansiIndices=[0,4,2,6,1,5,3,7,8,12,10,14,9,13,11,15];
    public TerminalTheme()
    {
        if(!OperatingSystem.IsWindows()) { GameTheme.TrueColor=!Console.IsOutputRedirected; return; }
        output=GetStdHandle(-11);
        if(GetConsoleMode(output,out oldMode)) { changed=SetConsoleMode(output,oldMode|5); GameTheme.TrueColor=changed; }
        if(changed) { exitHandler=(_,_)=>Dispose(); AppDomain.CurrentDomain.ProcessExit+=exitHandler; }
        IntPtr window=NativeDisplay.GetConsoleWindow();
        if(changed && window!=IntPtr.Zero && NativeDisplay.IsWindowVisible(window))
        {
            var info=new BufferInfo { Size=(uint)Marshal.SizeOf<BufferInfo>(),Colors=new uint[16] };
            if(GetConsoleScreenBufferInfoEx(output,ref info))
            {
                originalColors=info.Colors.ToArray();
                for(int i=0;i<16;i++) { var rgb=GameTheme.Palette[i]; SetPalette(i,rgb.R,rgb.G,rgb.B); }
                Console.Out.Flush();
                if(GetConsoleScreenBufferInfoEx(output,ref info))
                    PaletteApplied=Enumerable.Range(0,16).All(i => info.Colors[i] == (uint)(GameTheme.Palette[i].R | GameTheme.Palette[i].G<<8 | GameTheme.Palette[i].B<<16));
            }
        }
    }
    public void Dispose()
    {
        if(disposed) return; disposed=true;
        if(exitHandler!=null) AppDomain.CurrentDomain.ProcessExit-=exitHandler;
        if(originalColors!=null) for(int i=0;i<16;i++) { uint rgb=originalColors[i]; SetPalette(i,(int)(rgb&255),(int)((rgb>>8)&255),(int)((rgb>>16)&255)); }
        originalColors=null;
        if(GameTheme.TrueColor) Console.Write("\u001b[0m"); GameTheme.TrueColor=false; if(changed) SetConsoleMode(output,oldMode);
    }
    static void SetPalette(int index,int red,int green,int blue) => Console.Write($"\u001b]4;{ansiIndices[index]};rgb:{red:x2}/{green:x2}/{blue:x2}\u001b\\");
    [StructLayout(LayoutKind.Sequential)] struct SmallRect { public short Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct BufferInfo
    { public uint Size; public NativeDisplay.Coord Buffer,Cursor; public ushort Attributes; public SmallRect Window; public NativeDisplay.Coord Maximum; public ushort Popup; public int Fullscreen; [MarshalAs(UnmanagedType.ByValArray,SizeConst=16)] public uint[] Colors; }
    [DllImport("kernel32.dll")] static extern bool GetConsoleScreenBufferInfoEx(IntPtr output,ref BufferInfo info);
    [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int id);
    [DllImport("kernel32.dll")] static extern bool GetConsoleMode(IntPtr handle,out uint mode);
    [DllImport("kernel32.dll")] static extern bool SetConsoleMode(IntPtr handle,uint mode);
}

public sealed class InputReleaseGuard
{
    readonly HashSet<ConsoleKey> blocked = [];
    public void Block(IEnumerable<ConsoleKey> keys) { blocked.Clear(); blocked.UnionWith(keys); }
    public bool Accept(ConsoleKey key,bool down)
    { if(!down) { blocked.Remove(key); return false; } return !blocked.Contains(key); }
}
