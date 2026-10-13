namespace TranslateUni;

public sealed class PlayerCountSelection
{
    public int First=2,Second=2,Focus;
    public int[] Slots(PlayMode mode)=>mode==PlayMode.Rivalry?[1,1]:mode==PlayMode.Coop?[First]:[First,Second];
    public void Draw(Canvas c,PlayMode mode,bool joining,int joinTeam)
    {
        int x=Math.Max(3,(c.Width-72)/2);
        c.Box(x,9,72,10,ConsoleColor.DarkCyan);
        c.Text(x+3,10,joining?"JOIN YOUR TEAM":"HOW MANY PLAYERS?",ConsoleColor.Cyan);
        if(joining)
        {
            c.Text(x+3,12,mode==PlayMode.Coop?"You will join the host's shared company.":mode==PlayMode.Rivalry?"You will play the second company.":"Connect first, then choose a team in the lobby.");
            c.Text(x+3,14,mode==PlayMode.RivalryCoop?"See who's on each team before choosing.":"The lobby assigns your company automatically.",ConsoleColor.Cyan);
            c.Text(x+3,16,"Enter their address / invite below, then connect.");return;
        }
        if(mode==PlayMode.Rivalry){c.Text(x+3,12,"Rivalry: one player per company (2 players total).");return;}
        int rows=mode==PlayMode.RivalryCoop&&!joining?2:1;
        for(int i=0;i<rows;i++)
        {
            int y=12+i*2,n=i==0?First:Second,min=mode==PlayMode.Coop?2:1,max=mode==PlayMode.Coop?8:4;
            c.Text(x+3,y,mode==PlayMode.Coop?"Shared company":joining?$"Your team (Team {joinTeam+1})":$"Team {i+1}",Focus==i?ConsoleColor.Yellow:ConsoleColor.Gray);
            c.Button(x+27,y,"-","countDown",i);c.Text(x+35,y,$"{n} players",ConsoleColor.Cyan);c.Button(x+49,y,"+","countUp",i);
            c.Text(x+57,y,$"{min}-{max} max",ConsoleColor.DarkGray);
        }
        c.Text(x+3,16,"Click +/- or use LEFT / RIGHT. UP / DOWN selects team.");
        c.Text(x+3,17,joining?"Include yourself. First teammate sets the team's count.":"Include yourself. All slots must be filled before starting.",ConsoleColor.DarkCyan);
    }
    public bool Handle(Input input,Canvas frame,PlayMode mode,bool joining)
    {
        if(mode==PlayMode.Rivalry||joining)return false;
        int rows=mode==PlayMode.RivalryCoop&&!joining?2:1;Focus=Math.Clamp(Focus,0,rows-1);
        if(input.Key is ConsoleKey.UpArrow or ConsoleKey.DownArrow){Focus=1-Focus;Focus=Math.Clamp(Focus,0,rows-1);return true;}
        int delta=input.Key==ConsoleKey.LeftArrow?-1:input.Key==ConsoleKey.RightArrow?1:0;
        var hit=frame.Buttons.LastOrDefault(b=>input.X>=b.X&&input.X<b.X+b.Width&&input.Y==b.Y);
        if(hit!=null&&hit.Action is "countDown" or "countUp"){Focus=hit.Index;delta=hit.Action=="countDown"?-1:1;}
        if(delta==0)return false;
        int min=mode==PlayMode.Coop?2:1,max=mode==PlayMode.Coop?8:4;
        if(Focus==0)First=Math.Clamp(First+delta,min,max);else Second=Math.Clamp(Second+delta,min,max);
        return true;
    }
}

public sealed partial class LanMatch
{
    bool Captain(LanMember member,int team)=>Members.Where(m=>m.Connected&&m.Team==team).OrderBy(m=>m.Id).FirstOrDefault()?.Id==member.Id;
    void HandleLobby(LanMember member,Input input,DateTimeOffset now)
    {
        if(Mode==PlayMode.RivalryCoop)
        {
            var frame=Render(member.Id,member.Width,member.Height,now);
            var hit=frame.Buttons.LastOrDefault(b=>input.X>=b.X&&input.X<b.X+b.Width&&input.Y==b.Y);
            int choice=input.Key==ConsoleKey.D1?0:input.Key==ConsoleKey.D2?1:hit?.Action=="joinTeam"?hit.Index:-1;
            if(choice>=0)
            {
                if(Members.Count(m=>m.Connected&&m.Team==choice&&m.Id!=member.Id)>=Slots[choice])member.Notice="That team is full. Ask its captain to add a slot.";
                else {member.Team=choice;member.Notice="";Revision++;}
            }
            if(hit?.Action is "lobbyCountDown" or "lobbyCountUp" && Captain(member,hit.Index))
            {
                int filled=Members.Count(m=>m.Connected&&m.Team==hit.Index);
                Slots[hit.Index]=Math.Clamp(Slots[hit.Index]+(hit.Action=="lobbyCountUp"?1:-1),Math.Max(1,filled),4);Revision++;
            }
        }
        if(member.Id==0&&input.Key==ConsoleKey.Enter&&!Start(now))member.Notice="Every player must choose a team, and every team slot must be filled.";
    }
    void DrawLobby(Canvas c,int id,int left,int top,int coreWidth,int coreHeight)
    {
        int x=left+35,w=coreWidth-6;
        c.Text(x,top+2,Mode==PlayMode.Coop?"CO-OP - ONE COMPANY, ONE TEAM":"RIVALRY - TWO COMPANIES",ConsoleColor.Cyan);
        c.Text(x,top+4,$"Course: {course.Name} | Difficulty: {difficulty}");
        c.Text(x,top+5,$"Topic: {course.Lessons[topic].Name}");
        int total=Members.Count(m=>m.Connected),unassigned=Members.Count(m=>m.Connected&&m.Team<0),missing=Math.Max(0,Slots.Sum()-total);
        bool ready=unassigned==0&&Slots.All(n=>n>0)&&Enumerable.Range(0,Slots.Length).All(t=>Members.Count(m=>m.Connected&&m.Team==t)==Slots[t]);
        c.Text(x,top+7,unassigned>0?$"CHOOSE YOUR TEAM - {unassigned} PLAYER{(unassigned==1?"":"S")} CHOOSING":ready?"EVERYONE IS HERE - READY TO START":$"WAITING FOR {missing} MORE PLAYER{(missing==1?"":"S")}",ready?ConsoleColor.Green:ConsoleColor.Yellow);
        c.Text(x,top+8,$"Connected: {total} / {Slots.Sum()} | You: {Members[id].Identity.Name}, "+(Members[id].Team<0?"no team yet":$"Team {Members[id].Team+1}"));
        int teams=Slots.Length,boxWidth=Math.Min(47,(w-3)/teams);
        for(int team=0;team<teams;team++)
        {
            int bx=x+team*(boxWidth+2);c.Box(bx,top+10,boxWidth,12,ConsoleColor.DarkCyan);
            var present=Members.Where(m=>m.Team==team&&m.Connected).ToArray();
            c.Text(bx+2,top+11,$"TEAM {team+1} - {present.Length} / {Slots[team]} joined",ConsoleColor.Cyan);
            for(int slot=0;slot<Slots[team];slot++)
            {
                var player=present.ElementAtOrDefault(slot);
                c.Text(bx+2,top+13+slot,player==null?$"{slot+1}. Waiting for player...":$"●{player.Color+1} {player.Identity.Name}"+(player.Id==id?" (you)":""),player==null?ConsoleColor.DarkGray:PlayerColors[player.Color]);
            }
            if(Mode==PlayMode.RivalryCoop)
            {
                c.Button(bx+2,top+18,$"{team+1} JOIN TEAM {team+1}","joinTeam",team,Members[id].Team==team);
                if(Captain(Members[id],team)){c.Button(bx+2,top+20,"- SLOT","lobbyCountDown",team);c.Button(bx+15,top+20,"+ SLOT","lobbyCountUp",team);}
            }
        }
        if(Mode==PlayMode.RivalryCoop)c.Text(x,top+22,"1 / 2 or click: choose team | Team captains: +/- slots",ConsoleColor.Cyan);
        int row=coreHeight-8;
        c.Text(x,top+row,Members[id].Notice.Length>0?Members[id].Notice:id==0?(ready?"ENTER: Start the game":"Share the address / invite, then wait for teams to fill."):(Members[id].Team<0?"You're connected. Choose a team above to join its company.":ready?"Ready! Waiting for the host to press ENTER.":"You're connected. Stay here while your teammates join."),ConsoleColor.Yellow);
        c.Text(x,top+row+2,"Q: Leave lobby",ConsoleColor.Cyan);
    }
}
