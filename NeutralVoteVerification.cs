namespace TranslateUni;
public static partial class Verification
{
    static void CheckNeutralVotes(List<Course> courses)
    {
        var now=DateTimeOffset.Parse("2026-10-13T12:00:00Z");
        LanMatch Team(){var m=new LanMatch(courses,PlayMode.Coop,[3],0,0,Difficulty.Easy,42);for(int i=0;i<3;i++)m.Add(new("Player"+i),0);m.Start(now);return m;}
        var m=Team();var hand=m.Teams[0].Run.Hand.ToArray();int actions=m.Teams[0].Run.Actions;
        m.Input(0,new(ConsoleKey.D1),now);m.Input(0,new(ConsoleKey.Enter),now);m.Input(1,new(ConsoleKey.N),now);m.Input(2,new(ConsoleKey.V),now);
        Check(m.Teams[0].Ballot.Revote,"neutral vote allows yes/no tie to start revote immediately");
        foreach(int width in new[]{140,172,237})
        {
            Canvas frame=m.Render(0,width,46,now);
            Check(Enumerable.Range(1,3).All(i=>frame.Lines().Any(line=>line.Contains($"{i}. "))),"No vote in a tied ballot preserves every card");
            var tied=frame.Buttons.Where(b=>b.Action=="select").ToArray();
            Check(tied.Length==1&&tied[0].Index==0,"visible excluded cards cannot receive revotes");
            Check(frame.Lines().Any(l=>l.Contains("END DECISIONS"))&&frame.Lines().Any(l=>l.Contains("LOANS")),"No vote revote keeps other action controls visible");
        }
        m.Input(0,new(ConsoleKey.N),now.AddSeconds(1));m.Input(1,new(ConsoleKey.N),now.AddSeconds(1));m.Input(2,new(ConsoleKey.V),now.AddSeconds(1));
        Check(m.Teams[0].Run.Hand.SequenceEqual(hand)&&m.Teams[0].Run.Actions==actions&&!m.Teams[0].Ballot.Revote,"No result keeps hand and actions intact");
        var neutral=Team();var before=neutral.Teams[0].Run.CaptureStats();
        foreach(int id in new[]{0,1})neutral.Input(id,new(ConsoleKey.V),now);
        Canvas mouse=neutral.Render(2,237,67,now);var skip=mouse.Buttons.Single(b=>b.Action=="abstain");neutral.Input(2,new(X:skip.X,Y:skip.Y),now);
        Check(neutral.Teams[0].Ballot.Deadline==null&&neutral.Teams[0].Run.CaptureStats()==before&&neutral.Teams[0].Run.Actions==actions,"all-neutral mouse and keyboard votes cancel only the ballot");
        var ballot=new TeamBallot();ballot.Cast(0,new("reroll"),now);ballot.Cast(1,new("abstain"),now);ballot.Cast(2,new("abstain"),now);
        Check(ballot.Resolve([0,1,2],now).Winner==new VoteChoice("reroll"),"neutral votes do not oppose or outvote the only actual choice");
        var minority=new LanMatch(courses,PlayMode.Coop,[5],0,0,Difficulty.Easy,42);for(int i=0;i<5;i++)minority.Add(new("Five"+i),0);minority.Start(now);
        for(int i=0;i<4;i++){minority.Input(i,new(i<2?ConsoleKey.D1:ConsoleKey.D3),now);minority.Input(i,new(ConsoleKey.Enter),now);}minority.Input(4,new(ConsoleKey.N),now);
        Canvas minorityFrame=minority.Render(0,172,46,now);
        Check(minority.Teams[0].Ballot.Revote&&!minority.Teams[0].Ballot.TiedChoices!.Contains(new("noVote"))&&minorityFrame.Lines().Any(l=>l.Contains("2. Course Playbook")),"a minority No vote also keeps every card visible in the following revote");
        var loans=Team();foreach(int id in new[]{0,1,2})loans.Input(id,new(ConsoleKey.L),now);
        loans.Input(0,new(ConsoleKey.D2),now);loans.Input(0,new(ConsoleKey.Enter),now);loans.Input(1,new(ConsoleKey.N),now);loans.Input(2,new(ConsoleKey.V),now);
        Check(loans.Teams[0].Ballot.Revote&&loans.Teams[0].Run.LoanOffers.All(o=>loans.Render(0,172,46,now).Lines().Any(l=>l.Contains(o.Name))),"loan/no tie preserves every loan offer by the same general rule");
        var shop=Team();var run=shop.Teams[0].Run;run.FinishDecisions();run.SkipEvents();run.ContinueResult();run.AcknowledgeCompanyEvent();run.SkipBooster();shop.Tick(now);
        var original=shop.Render(0,172,46,now);var item=original.Buttons.Single(b=>b.Action=="shopSelect"&&b.Index==0);
        shop.Input(0,new(ConsoleKey.Enter),now);shop.Input(1,new(ConsoleKey.N),now);shop.Input(2,new(ConsoleKey.V),now);
        Canvas revote=shop.Render(0,172,46,now);
        Check(shop.Teams[0].Ballot.Revote&&revote.Buttons.Single(b=>b.Action=="shopSelect").X==item.X,"shop/no tie retains the original shelf layout");
        var counts=new PlayerCountSelection();var c=new Canvas(172,46);counts.Draw(c,PlayMode.RivalryCoop,false,0);
        var plus=c.Buttons.Single(b=>b.Action=="countUp"&&b.Index==1);counts.Handle(new(X:plus.X,Y:plus.Y),c,PlayMode.RivalryCoop,false);
        Check(counts.Second==3&&counts.First==2,"clicking team two plus changes only that team's count");
        for(int i=0;i<12;i++)counts.Handle(new(ConsoleKey.RightArrow),c,PlayMode.RivalryCoop,false);
        Check(counts.Second==4,"player counts clamp at the mode's maximum");
        var lobby=new LanMatch(courses,PlayMode.Coop,[3],0,0,Difficulty.Easy,42);lobby.Add(new("Host"),0);lobby.Add(new("Guest"),0);
        Check(lobby.Render(1,172,46,now).Lines().Any(l=>l.Contains("WAITING FOR 1 MORE PLAYER")),"joining lobby explains exactly how many players are missing");
        lobby.Add(new("Last"),0);Check(lobby.Render(1,140,42,now).Lines().Any(l=>l.Contains("Waiting for the host to press ENTER")),"full lobby tells guests what happens next");
    }
}

