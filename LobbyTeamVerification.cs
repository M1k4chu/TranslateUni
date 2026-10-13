using System.Net;
using System.Net.Sockets;
namespace TranslateUni;
public static partial class Verification
{
    static void CheckLobbyTeams(List<Course> courses,bool network)
    {
        var now=DateTimeOffset.Parse("2026-10-13T12:00:00Z");
        var match=new LanMatch(courses,PlayMode.RivalryCoop,[2,2],0,0,Difficulty.Easy,42);
        for(int i=0;i<4;i++)match.Add(new("Lobby"+i),-1);
        Check(!match.Start(now),"connected but unassigned players cannot start the match");
        Canvas choosing=match.Render(1,237,67,now);var team2=choosing.Buttons.Single(b=>b.Action=="joinTeam"&&b.Index==1);
        match.Input(1,new(X:team2.X,Y:team2.Y),now);
        Check(match.Members[1].Team==1&&!match.Started,"guest chooses a team through the live lobby mouse controls");
        match.Input(0,new(ConsoleKey.D1),now);match.Input(2,new(ConsoleKey.D2),now);match.Input(3,new(ConsoleKey.D2),now);
        Check(match.Members[3].Team==-1&&match.Members[3].Notice.Contains("full"),"full team refuses an extra player without disconnecting them");
        match.Input(3,new(ConsoleKey.D1),now);Check(match.Start(now),"host can start once both chosen teams are filled");
        match.Input(1,new(ConsoleKey.D1),now);Check(match.Members[1].Team==1,"team membership is locked during the match");
        var captain=new LanMatch(courses,PlayMode.RivalryCoop,[2,2],0,0,Difficulty.Easy,42);captain.Add(new("Captain"),-1);captain.Add(new("Teammate"),-1);
        captain.Input(0,new(ConsoleKey.D2),now);captain.Input(1,new(ConsoleKey.D2),now);
        Canvas controls=captain.Render(0,172,46,now);var plus=controls.Buttons.Single(b=>b.Action=="lobbyCountUp");
        captain.Input(1,new(X:plus.X,Y:plus.Y),now);Check(captain.Slots[1]==2,"only the team's first joined player can change its slot count");
        captain.Input(0,new(X:plus.X,Y:plus.Y),now);Check(captain.Slots[1]==3,"captain adds a team slot from inside the lobby");
        foreach(var mode in new[]{PlayMode.Coop,PlayMode.Rivalry})
        {
            var lobby=new LanMatch(courses,mode,mode==PlayMode.Coop?[2]:[1,1],0,0,Difficulty.Easy,42);lobby.Add(new("Host"),0);lobby.Add(new("Guest"),mode==PlayMode.Coop?0:1);
            Check(!lobby.Render(1,172,46,now).Buttons.Any(b=>b.Action=="joinTeam"),"no team-selection controls in "+mode);
            Canvas prejoin=new(172,46);new PlayerCountSelection().Draw(prejoin,mode,true,0);Check(prejoin.Buttons.Count==0,"joining screen has no team or captain selection in "+mode);
        }
        Check(LanMultiplayer.HostingPort("")==45873&&LanMultiplayer.HostingPort("45874")==45874,"LAN hosting accepts default or custom port");
        if(!network)return;
        var first=LanMultiplayer.StartListener(0,false,localOnly:true);int occupied=((IPEndPoint)first.LocalEndpoint).Port;
        try
        {
            Check(((IPEndPoint)first.LocalEndpoint).Address.Equals(IPAddress.Loopback),"integration port tests listen only on loopback");
            var fallback=LanMultiplayer.StartListener(occupied,true,localOnly:true);
            try{Check(((IPEndPoint)fallback.LocalEndpoint).Port!=occupied,"occupied LAN port automatically selects a free alternative");}finally{fallback.Stop();}
            bool rejected=false;try{var duplicate=LanMultiplayer.StartListener(occupied,false,localOnly:true);duplicate.Stop();}catch(SocketException ex){rejected=ex.SocketErrorCode==SocketError.AddressAlreadyInUse;}
            Check(rejected,"online hosting preserves the invite port and reports an occupied port");
        }
        finally{first.Stop();}
        var reopened=LanMultiplayer.StartListener(occupied,false,localOnly:true);reopened.Stop();Check(true,"stopped lobby listener releases its port for another host");
    }
}
