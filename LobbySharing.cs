using System.Runtime.InteropServices;
using System.Text;
namespace TranslateUni;

public static class GameClipboard
{
    public static bool Copy(string text)
    {
        if(!OperatingSystem.IsWindows())return false;
        byte[] bytes=Encoding.Unicode.GetBytes(text+'\0');
        IntPtr memory=GlobalAlloc(2,(UIntPtr)bytes.Length);if(memory==IntPtr.Zero)return false;
        bool opened=false,transferred=false;
        try
        {
            IntPtr pointer=GlobalLock(memory);if(pointer==IntPtr.Zero)return false;
            try{Marshal.Copy(bytes,0,pointer,bytes.Length);}finally{GlobalUnlock(memory);}
            for(int attempt=0;attempt<3&&!opened;attempt++){opened=OpenClipboard(GetConsoleWindow());if(!opened)Thread.Sleep(10);}
            if(!opened||!EmptyClipboard())return false;
            transferred=SetClipboardData(13,memory)!=IntPtr.Zero;return transferred;
        }
        finally{if(opened)CloseClipboard();if(!transferred)GlobalFree(memory);}
    }
    [DllImport("kernel32.dll")] static extern IntPtr GetConsoleWindow();
    [DllImport("kernel32.dll")] static extern IntPtr GlobalAlloc(uint flags,UIntPtr size);
    [DllImport("kernel32.dll")] static extern IntPtr GlobalLock(IntPtr memory);
    [DllImport("kernel32.dll")] static extern bool GlobalUnlock(IntPtr memory);
    [DllImport("kernel32.dll")] static extern IntPtr GlobalFree(IntPtr memory);
    [DllImport("user32.dll")] static extern bool OpenClipboard(IntPtr owner);
    [DllImport("user32.dll")] static extern bool CloseClipboard();
    [DllImport("user32.dll")] static extern bool EmptyClipboard();
    [DllImport("user32.dll")] static extern IntPtr SetClipboardData(uint format,IntPtr memory);
}

public sealed class LobbySharing(string address,string? invite)
{
    public bool Visible {get;private set;}
    public string Notice {get;private set;}="";
    public bool Handle(Input input,Canvas? frame,Func<string,bool>? copy=null)
    {
        var hit=frame?.Buttons.LastOrDefault(b=>input.X>=b.X&&input.X<b.X+b.Width&&input.Y>=b.Y&&input.Y<b.Y+b.Height);
        if(input.Key==ConsoleKey.F10||hit?.Action=="sharingToggle"){Visible=!Visible;Notice="";return true;}
        if(input.Key==ConsoleKey.F11||hit?.Action is "copyInvite" or "copyAddress")
        {
            string value=hit?.Action=="copyAddress"?address.Split(", ")[0]:invite??address.Split(", ")[0];
            Notice=(copy??GameClipboard.Copy)(value)?(value==invite?"Invite copied!":"Join address copied!"):"Clipboard unavailable. F10: Show; F9: Save online invite.";
            return true;
        }
        return false;
    }
    public void Draw(Canvas frame,int port)
    {
        int left=32+Math.Max(0,(frame.Width-Math.Min(172,frame.Width))/2),top=Math.Max(0,(frame.Height-Math.Min(46,frame.Height))/2);
        int x=left+3,width=Math.Max(20,Math.Min(140,frame.Width-32)-6);
        frame.Button(x,top+23,Visible?"F10 HIDE":"F10 SHOW","sharingToggle");
        frame.Button(x+16,top+23,invite==null?"F11 COPY ADDRESS":"F11 COPY INVITE",invite==null?"copyAddress":"copyInvite");
        if(invite!=null)
        {
            if(Visible)
            {
                for(int row=0;row<5&&row*width<invite.Length;row++)
                {
                    string line=invite.Substring(row*width,Math.Min(width,invite.Length-row*width));
                    frame.Text(x,top+24+row,line,ConsoleColor.Cyan);frame.Buttons.Add(new(x,top+24+row,line.Length,1,"copyInvite"));
                }
            }
            else frame.Text(x,top+25,"Online invite hidden. Copy works while hidden.",ConsoleColor.DarkCyan);
            frame.Text(x,top+29,"Click the invite to copy | F9: Save complete code",ConsoleColor.Cyan);
            frame.Wrapped(x,top+30,$"Share the full invite. Forward TCP {port} to this PC, or use a shared VPN.",width,3);
        }
        else frame.Text(x,top+25,"Copy your join address and share it with teammates.",ConsoleColor.DarkCyan);
        if(Notice.Length>0)frame.Text(x,top+33,Notice[..Math.Min(width,Notice.Length)],ConsoleColor.Green);
        frame.Text(3,frame.Height-3,"Join address: "+(Visible?address:"[hidden - F10 to show]")+" | Q: Exit",ConsoleColor.Cyan);
        if(Visible)frame.Buttons.Add(new(17,frame.Height-3,Math.Min(address.Length,frame.Width-18),1,"copyAddress"));
    }
}
