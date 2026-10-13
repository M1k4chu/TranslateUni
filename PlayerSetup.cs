namespace TranslateUni;
public sealed partial class GameApp
{
    int modeIndex,avatarIndex;bool avatarFocus;
    string playerName="";
    public PlayMode SelectedMode {get;private set;}
    public PlayerIdentity? PlayerProfile {get;private set;}
    public bool NetworkRequested {get;private set;}
    public void FinishNetwork(){NetworkRequested=false;ReturnMain();}
    public Canvas Render(int width,int height)
    {
        if(Game==null||PlayerProfile==null||width<140)return RenderCore(width,height);
        int cw=Math.Min(140,width-32),ch=Math.Min(46,height),left=(width-cw-32)/2,top=(height-ch)/2;
        var c=new Canvas(width,height);c.Paste(RenderCore(cw,ch),left+32,top);
        c.Text(left+1,top+2,"PLAYER PROFITS",ConsoleColor.Cyan);
        for(int row=0;row<4;row++)c.Text(left+1,top+5+row,PlayerAvatars.All[PlayerProfile.Avatar][row],ConsoleColor.Cyan);
        c.Text(left+7,top+5,PlayerProfile.Name,ConsoleColor.Cyan);
        c.Text(left+1,top+11,Game.LastReport==null?"Month profit: --":$"Month profit: ${Game.LastReport.Profit:N0}");
        c.Text(left+1,top+13,$"Total: ${Game.Reports.Sum(r=>r.Profit):N0}",ConsoleColor.Green);
        return c;
    }
    bool HandlePlayerSetup(Input input)
    {
        if(Screen==Screen.Modes)
        {
            if(input.Key==ConsoleKey.Escape){ReturnMain();return true;}
            modeIndex=Navigate(input.Key,modeIndex,4);int digit=Digit(input.Key);if(digit is >=0 and <4)modeIndex=digit;
            if(input.X>=0){var hit=inputFrame?.Buttons.LastOrDefault(b=>b.Action=="mode"&&input.X>=b.X&&input.X<b.X+b.Width&&input.Y==b.Y);if(hit!=null){modeIndex=hit.Index;input=new(ConsoleKey.Enter);}}
            if(input.Key==ConsoleKey.Enter){SelectedMode=(PlayMode)modeIndex;Screen=Screen.PlayerSetup;playerName=PlayerProfile?.Name??"";avatarIndex=PlayerProfile?.Avatar??0;avatarFocus=false;}
            return true;
        }
        if(Screen!=Screen.PlayerSetup)return false;
        if(input.Key==ConsoleKey.Escape){Screen=Screen.Modes;return true;}
        if(input.Key==ConsoleKey.Tab)avatarFocus=!avatarFocus;
        if(avatarFocus)avatarIndex=Navigate(input.Key,avatarIndex,PlayerAvatars.All.Length);
        else if(input.Key==ConsoleKey.Backspace&&playerName.Length>0)playerName=playerName[..^1];
        else if(playerName.Length<14 && input.Character!=0 && (char.IsLetterOrDigit(input.Character)||input.Character is ' ' or '_' or '-'))playerName+=input.Character;
        if(input.Key==ConsoleKey.Enter){PlayerProfile=new(string.IsNullOrWhiteSpace(playerName)?"Player":playerName.Trim(),avatarIndex);if(SelectedMode==PlayMode.Singleplayer){browsing=false;Screen=Screen.Courses;}else NetworkRequested=true;}
        return true;
    }
    void DrawModes(Canvas c)
    {
        Header(c,"CHOOSE A PLAY MODE","Singleplayer, LAN or online multiplayer hosted by a player");
        string[] names=["Singleplayer","CO-OP","Rivalry (2 player)","Rivalry CO-OP"];
        for(int i=0;i<4;i++)c.Button(8,8+i*4,names[i],"mode",i,modeIndex==i);
        c.Wrapped(8,26,"CO-OP shares one company and votes on choices. Rivalry uses separate companies. Rivalry CO-OP puts two voting teams against each other.",c.Width-16,4);
        Footer(c,"WASD / 1-4: Mode | ENTER: Profile | ESC: Main menu");
    }
    void DrawPlayerSetup(Canvas c)
    {
        Header(c,"YOUR PLAYER PROFILE","Name and a small 4 x 4 ASCII character");
        c.Text(8,7,$"Name: {playerName}_",!avatarFocus?ConsoleColor.Yellow:ConsoleColor.Gray);
        c.Text(8,10,"TAB: Switch between name and avatar. Arrows select avatar.");
        for(int i=0;i<PlayerAvatars.All.Length;i++){int x=8+i*12;for(int row=0;row<4;row++)c.Text(x,14+row,PlayerAvatars.All[i][row],i==avatarIndex?ConsoleColor.Magenta:ConsoleColor.DarkCyan);c.Text(x,19,(i+1).ToString());}
        c.Text(8,23,"ENTER: Continue. Names are limited to 14 characters.");Footer(c,"Type name | TAB / arrows: Avatar | ENTER: Continue | ESC: Modes");
    }
}
