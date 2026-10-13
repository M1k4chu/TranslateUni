using System.Net;
using System.Net.Sockets;
namespace TranslateUni;

public static partial class Verification
{
    static void CheckMultiplayer(List<Course> courses)
    {
        DateTimeOffset now=DateTimeOffset.Parse("2026-10-13T12:00:00Z");
        Check(PlayerAvatars.All.All(a=>a.Length==4&&a.All(row=>row.Length==4&&row.All(c=>c<128))),"every avatar is exactly 4 x 4 ASCII");
        var profile=new GameApp(courses,new Storage(Path.Combine(Path.GetTempPath(),"TranslateUni-profile-test-"+Guid.NewGuid().ToString("N"))));
        Key(profile,ConsoleKey.D1);Check(profile.Screen==Screen.Modes,"student play opens mode selection");
        Key(profile,ConsoleKey.D2);Key(profile,ConsoleKey.Enter);Check(profile.Screen==Screen.PlayerSetup,"co-op asks for a profile");
        foreach(char ch in "Mikka")profile.Handle(new(Character:ch),profile.Render(120,42));
        Key(profile,ConsoleKey.Tab);Key(profile,ConsoleKey.RightArrow);Key(profile,ConsoleKey.Enter);
        Check(profile.NetworkRequested&&profile.SelectedMode==PlayMode.Coop&&profile.PlayerProfile==new PlayerIdentity("Mikka",1),"name and avatar enter LAN setup");
        var ballot=new TeamBallot();var one=new VoteChoice("play",0);var two=new VoteChoice("play",1);int[] voters=[0,1,2];
        ballot.Cast(0,one,now);Check(ballot.Deadline==now.AddSeconds(20)&&!ballot.Resolve(voters,now.AddSeconds(19)).Done,"first vote starts a twenty-second window");
        ballot.Cast(1,one,now);ballot.Cast(2,two,now);
        Check(ballot.Resolve(voters,now).Winner==one&&ballot.Deadline==null,"all votes resolve majority early");
        ballot.Cast(0,one,now);ballot.Cast(1,two,now);var tie=ballot.Resolve([0,1],now);
        Check(!tie.Done&&ballot.Revote&&ballot.Deadline==now.AddSeconds(10),"tie opens ten-second revote");
        Check(!ballot.Cast(0,new("skip"),now)&&ballot.Cast(0,one,now)&&ballot.Cast(1,two,now),"revote accepts tied choices only");
        Check(ballot.Resolve([0,1],now).Skipped&&!ballot.Revote,"second tie skips and clears ballot");
        ballot.Cast(0,new("skip"),now);Check(ballot.Resolve(voters,now.AddSeconds(20)).Winner?.Action=="skip","skip can win a timed vote");
        ballot.Cast(0,one,now);Check(ballot.Resolve([1],now.AddSeconds(20)).Skipped,"disconnected votes do not count");

        LanMatch Match(PlayMode mode,int[] slots,int seed=42)=>new(courses,mode,slots,0,0,Difficulty.Easy,seed);
        var coop=Match(PlayMode.Coop,[2]);coop.Add(new("Alex",0),0);
        Check(!coop.Start(now),"host cannot start an incomplete lobby");coop.Add(new("Sam",1),0);
        bool duplicate=false;try{coop.Add(new("Alex"),0);}catch(InvalidOperationException){duplicate=true;}Check(duplicate,"full or duplicate lobby entries rejected");
        Check(coop.Start(now)&&coop.DecisionDeadline==now.AddMinutes(3),"full lobby starts a three-minute decision phase");
        Check(ReferenceEquals(coop.Members[0].View.Game,coop.Members[1].View.Game)&&coop.Members[0].Color!=coop.Members[1].Color,"co-op shares company and has distinct player colors");
        int rerolls=coop.Teams[0].Run.Rerolls;
        coop.Input(0,new(ConsoleKey.R),now);Check(coop.Teams[0].Run.Rerolls==rerolls,"one co-op vote does not execute a reroll");
        coop.Input(1,new(ConsoleKey.R),now);Check(coop.Teams[0].Run.Rerolls==rerolls-1&&coop.Members.All(m=>m.Animation.Count>0),"matching votes execute one reroll and queue animations for both players");
        int actions=coop.Teams[0].Run.Actions;coop.Input(0,new(ConsoleKey.E),now);
        Check(coop.Teams[0].Run.Actions==actions&&coop.Teams[0].Ballot.Votes.Count==0,"animation ignores queued actions");
        coop.Tick(now.AddSeconds(3));Check(coop.Members.All(m=>m.Animation.Count==0),"animations finish without stopping host clock");
        coop.Input(0,new(ConsoleKey.D1),now.AddSeconds(3));coop.Input(0,new(ConsoleKey.Enter),now.AddSeconds(3));
        coop.Input(1,new(ConsoleKey.D2),now.AddSeconds(3));coop.Input(1,new(ConsoleKey.Enter),now.AddSeconds(3));
        Check(coop.Teams[0].Ballot.Revote,"different card votes trigger team revote");
        coop.Input(0,new(ConsoleKey.Enter),now.AddSeconds(4));coop.Input(1,new(ConsoleKey.Enter),now.AddSeconds(4));
        Check(coop.Teams[0].Run.Actions==actions&&coop.Teams[0].Ballot.Deadline==null,"tied revote skips the vote without spending a decision");
        coop.Tick(now.AddMinutes(3));Check(coop.Teams[0].Run.Phase==Phase.Events&&coop.DecisionDeadline==null,"decision timeout safely enters events");
        coop.Input(0,new(ConsoleKey.E),now.AddMinutes(3));coop.Input(1,new(ConsoleKey.E),now.AddMinutes(3));
        Check(coop.Teams[0].Run.Reports.Count==1,"co-op settles month once");
        Canvas standings=coop.Render(0,172,46,now.AddMinutes(3));
        Check(standings.Lines().Any(l=>l.StartsWith(" MONTHLY PROFIT"))&&standings.Lines().Any(l=>l.Contains("Alex"))&&standings.Lines().Any(l=>l.Contains("Sam")),"left panel displays player profiles and profits");
        Check(standings.Lines()[39].Contains("CASH")&&standings.Lines()[39].Contains("DEBT"),"LAN layout preserves statistics board");
        foreach(int width in new[]{140,172,237})foreach(int height in new[]{42,46,60})
        {Canvas frame=coop.Render(0,width,height,now.AddMinutes(3));Check(frame.Width==width&&frame.Height==height,"LAN frame fits terminal at supported dimensions");}

        var rival=Match(PlayMode.Rivalry,[1,1]);rival.Add(new("One"),0);rival.Add(new("Two"),1);rival.Start(now);
        Check(!ReferenceEquals(rival.Teams[0].Run,rival.Teams[1].Run)&&rival.Teams[0].Run.Hand.SequenceEqual(rival.Teams[1].Run.Hand),"rivals have independent companies with equal seeded starting hands");
        rival.Input(0,new(ConsoleKey.E),now);Check(rival.Teams[0].Run.Phase==Phase.Events&&rival.DecisionDeadline!=null,"first finished rival waits at decision barrier");
        rival.Tick(now.AddSeconds(3));rival.Input(0,new(ConsoleKey.E),now.AddSeconds(3));Check(rival.Teams[0].Run.Reports.Count==0,"waiting rival cannot run events early");
        rival.Input(1,new(ConsoleKey.E),now.AddSeconds(3));Check(rival.DecisionDeadline==null,"both finished rivals advance early");
        rival.Tick(now.AddSeconds(6));
        foreach(var team in rival.Teams){team.Run.SkipEvents();team.Run.ContinueResult();team.Run.AcknowledgeCompanyEvent();team.Run.SkipBooster();}
        foreach(var member in rival.Members)member.View.AttachLanRun(rival.Teams[member.Team].Run);
        rival.Tick(now.AddSeconds(6));Check(rival.ShopDeadline==now.AddSeconds(126),"shop phase starts two-minute shared clock");
        rival.Input(0,new(ConsoleKey.Spacebar),now.AddSeconds(7));Check(rival.Teams.All(t=>t.Run.Month==1),"one shop-ready rival waits for the other");
        rival.Input(1,new(ConsoleKey.Spacebar),now.AddSeconds(8));Check(rival.Teams.All(t=>t.Run.Month==2)&&rival.DecisionDeadline==now.AddSeconds(188),"both shop-ready rivals advance early together");
        rival.Tick(now.AddSeconds(188));
        foreach(var team in rival.Teams){team.Run.SkipEvents();team.Run.ContinueResult();team.Run.AcknowledgeCompanyEvent();team.Run.SkipBooster();}
        foreach(var member in rival.Members)member.View.AttachLanRun(rival.Teams[member.Team].Run);
        rival.Tick(now.AddSeconds(189));rival.Tick(now.AddSeconds(309));
        Check(rival.Teams.All(t=>t.Run.Month==3||t.Run.Phase is Phase.Won or Phase.Lost),"shop timeout advances or finishes every rival safely");
        rival.Disconnect(1);rival.Tick(now.AddSeconds(310));Check(rival.Teams[1].Run.Phase==Phase.Lost,"team with no connected players forfeits");
        var varied=Match(PlayMode.RivalryCoop,[1,3]);var captain=varied.Add(new("Captain"),0);
        foreach(int i in Enumerable.Range(0,3))varied.Add(new("TeamMate"+i),1);
        Check(varied.Start(now)&&varied.Members.Count==4,"teams can choose unequal player counts");
        var reconnect=Match(PlayMode.Coop,[2]);reconnect.Add(new("Host"),0);var old=reconnect.Add(new("Guest"),0);reconnect.Disconnect(old.Id);reconnect.Add(new("Guest"),0);
        Check(reconnect.Start(now),"disconnected lobby slots can be replaced");
        var loans=Match(PlayMode.Coop,[2]);loans.Add(new("Borrower"),0);loans.Add(new("Partner"),0);loans.Start(now);
        foreach(int id in new[]{0,1})loans.Input(id,new(ConsoleKey.L),now);
        foreach(int id in new[]{0,1}){loans.Input(id,new(ConsoleKey.D2),now);loans.Input(id,new(ConsoleKey.Enter),now);}
        Check(loans.Teams[0].Run.HasActiveLoan&&loans.Members[0].View.Screen==Screen.Loans,"co-op votes execute exactly one loan and leave repayment visible");
        double balance=loans.Teams[0].Run.Debt;loans.Tick(now.AddSeconds(4));
        foreach(int id in new[]{0,1}){loans.Input(id,new(ConsoleKey.L),now.AddSeconds(4));loans.Input(id,new(ConsoleKey.P),now.AddSeconds(4));}
        Check(loans.Teams[0].Run.Debt<balance,"loan repayments require team agreement");
        var mouse=Match(PlayMode.Coop,[2]);mouse.Add(new("Clicker"),0);mouse.Add(new("Pointer"),0);mouse.Start(now);
        int priorActions=mouse.Teams[0].Run.Actions;
        foreach(int id in new[]{0,1})
        {
            mouse.Input(id,new(ConsoleKey.D1),now);Canvas ui=mouse.Render(id,172,46,now);
            var play=ui.Buttons.Single(b=>b.Action=="play");mouse.Input(id,new(X:play.X,Y:play.Y),now);
        }
        Check(mouse.Teams[0].Run.Actions==priorActions-1,"mouse PLAY button votes for the selected card");
        var reward=Match(PlayMode.Coop,[2]);reward.Add(new("Buyer"),0);reward.Add(new("Saver"),0);reward.Start(now);
        var shopRun=reward.Teams[0].Run;shopRun.FinishDecisions();shopRun.SkipEvents();shopRun.ContinueResult();shopRun.AcknowledgeCompanyEvent();shopRun.SkipBooster();
        reward.Tick(now);
        foreach(var member in reward.Members){member.View.AttachLanRun(shopRun);member.View.LanAction("shopBack",-1);}
        reward.Input(0,new(ConsoleKey.Enter),now);
        Check(shopRun.Month==1&&reward.Teams[0].Ballot.Votes.Count==1,"reward ENTER cannot bypass co-op next-month vote");
        reward.Input(1,new(ConsoleKey.Enter),now);Check(shopRun.Month==2,"reward next-month vote advances only after both players agree");
        var abandoned=Match(PlayMode.Rivalry,[1,1]);abandoned.Add(new("Remaining"),0);abandoned.Add(new("Leaving"),1);abandoned.Start(now);abandoned.Disconnect(1);abandoned.Tick(now);
        Check(abandoned.Render(1,172,46,now).Lines().Any(l=>l.Contains("RUN OVER")),"disconnect before first settlement renders a valid end screen");
        var endless=Match(PlayMode.Rivalry,[1,1]);endless.Add(new("WinnerA"),0);endless.Add(new("WinnerB"),1);endless.Start(now);
        for(int i=0;i<2;i++){endless.Teams[i]=new(AutoPlay(courses[0],0,Difficulty.Easy,73));endless.Members[i].View.AttachLanRun(endless.Teams[i].Run);}
        endless.Tick(now);endless.Input(0,new(ConsoleKey.E),now);
        Check(endless.Teams[0].EndlessReady&&!endless.Teams[0].Run.Endless,"first winning team waits for shared endless consent");
        endless.Input(1,new(ConsoleKey.E),now);
        Check(endless.Teams.All(t=>t.Run.Endless&&t.Run.Phase==Phase.Booster&&t.Run.MaxShopTier==5),"all winning teams unlock endless and five shop tiers together");
        var wire=LanPeer.Frame(standings,0,7);Canvas decoded=LanPeer.Canvas(wire);
        Check(decoded.Lines().SequenceEqual(standings.Lines())&&decoded.ColorAt(1,1)==standings.ColorAt(1,1),"network frames preserve text and colors");
        var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        try
        {
            using var client=new TcpClient();client.Connect(IPAddress.Loopback,((IPEndPoint)listener.LocalEndpoint).Port);
            using var serverPeer=new LanPeer(listener.AcceptTcpClient());using var clientPeer=new LanPeer(client);
            clientPeer.Send(new("hello",new("NetworkPlayer",2),Team:1,TeamSize:3));
            Check(SpinWait.SpinUntil(()=>!serverPeer.Inbox.IsEmpty,3000),"LAN TCP receives a real client handshake");
            serverPeer.Inbox.TryDequeue(out var hello);Check(hello?.Player?.Name=="NetworkPlayer"&&hello.Team==1&&hello.TeamSize==3,"LAN handshake preserves profile and requested team size");
            serverPeer.Send(wire);Check(SpinWait.SpinUntil(()=>!clientPeer.Inbox.IsEmpty,3000),"LAN TCP sends a real server frame");
            clientPeer.Inbox.TryDequeue(out var received);Check(received?.Revision==7&&LanPeer.Canvas(received).Lines().SequenceEqual(standings.Lines()),"LAN client receives authoritative frame revision");
            clientPeer.Send(new("input",Key:(int)ConsoleKey.Enter,Revision:7));Check(SpinWait.SpinUntil(()=>!serverPeer.Inbox.IsEmpty,3000),"LAN client sends input back to host");
            serverPeer.Inbox.TryDequeue(out var command);Check(command?.Revision==7&&command.Key==(int)ConsoleKey.Enter,"input includes screen revision for stale-input rejection");
        }
        finally{listener.Stop();}
    }
}
