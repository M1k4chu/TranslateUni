namespace TranslateUni;
public static partial class Verification
{
    static void CheckNoVote(List<Course> courses)
    {
        var now=DateTimeOffset.Parse("2026-10-13T12:00:00Z");
        LanMatch Team(int count=3){var m=new LanMatch(courses,PlayMode.Coop,[count],0,0,Difficulty.Easy,42);for(int i=0;i<count;i++)m.Add(new("NoVoter"+i),0);m.Start(now);return m;}
        var m=Team();var run=m.Teams[0].Run;var hand=run.Hand.ToArray();var before=run.CaptureStats();int actions=run.Actions,rerolls=run.Rerolls;
        m.Input(0,new(ConsoleKey.L),now);m.Input(1,new(ConsoleKey.N),now);
        Check(m.Render(1,172,46,now).Buttons.Count(b=>b.Action=="select")==3,"proposing a loan keeps all decision cards visible");
        m.Input(2,new(ConsoleKey.N),now);
        Check(run.Hand.SequenceEqual(hand)&&run.Actions==actions&&run.Rerolls==rerolls&&run.CaptureStats()==before&&run.Phase==Phase.Decisions&&!run.HasActiveLoan,"majority No rejects a loan proposal without discarding cards or advancing the phase");
        Check(m.Teams[0].Ballot.Deadline==null&&m.Members.All(p=>p.View.Screen==Screen.Table),"No clears only the ballot");
        m.Input(0,new(ConsoleKey.D1),now);m.Input(0,new(ConsoleKey.Enter),now);
        Canvas mouse=m.Render(1,237,67,now);var no=mouse.Buttons.Single(b=>b.Action=="noVote");m.Input(1,new(X:no.X,Y:no.Y),now);
        Check(m.Teams[0].Ballot.Votes[1].Action=="noVote","left-panel No button accepts mouse votes in fullscreen coordinates");
        m.Input(2,new(ConsoleKey.N),now);Check(run.Hand.SequenceEqual(hand)&&run.Actions==actions,"No rejects a card vote without using or replacing a card");
        foreach(int id in new[]{0,1,2})m.Input(id,new(ConsoleKey.L),now);
        m.Input(0,new(ConsoleKey.D1),now);m.Input(0,new(ConsoleKey.Enter),now);m.Input(1,new(ConsoleKey.D3),now);m.Input(1,new(ConsoleKey.Enter),now);m.Tick(now.AddSeconds(20));
        Canvas offers=m.Render(2,172,46,now.AddSeconds(20));
        foreach(var offer in run.LoanOffers)Check(offers.Lines().Any(line=>line.Contains(offer.Name)),"loan revote preserves visible offer "+offer.Name);
        Check(offers.Buttons.Count(b=>b.Action=="select")==2,"excluded loan offer stays visible but cannot receive a revote");
        foreach(int id in new[]{0,1,2})m.Input(id,new(ConsoleKey.N),now.AddSeconds(21));
        Check(!run.HasActiveLoan&&run.Hand.SequenceEqual(hand)&&run.Actions==actions&&run.CaptureStats()==before&&m.Members.All(p=>p.View.Screen==Screen.Loans),"No during loan revote cancels the vote and leaves the loan screen and cards intact");
        foreach(int id in new[]{0,1,2}){m.Input(id,new(ConsoleKey.D1),now.AddSeconds(22));m.Input(id,new(ConsoleKey.Enter),now.AddSeconds(22));}
        Check(run.HasActiveLoan&&m.Members.All(p=>p.Animation.All(f=>f.Stage==AnimationStage.Statistics)),"accepted multiplayer loan has statistics feedback without card collapse or suction");
        var tie=Team(2);var unchanged=tie.Teams[0].Run.Hand.ToArray();int left=tie.Teams[0].Run.Actions;
        foreach(int id in new[]{0,1}){tie.Input(id,new((ConsoleKey)((int)ConsoleKey.D1+id)),now);tie.Input(id,new(ConsoleKey.Enter),now);}
        Check(tie.Teams[0].Ballot.Revote,"card disagreement starts revote");
        foreach(int id in new[]{0,1})tie.Input(id,new(ConsoleKey.Enter),now.AddSeconds(1));
        Check(tie.Teams[0].Run.Hand.SequenceEqual(unchanged)&&tie.Teams[0].Run.Actions==left&&tie.Teams[0].Run.Phase==Phase.Decisions,"second tie skips only the vote, keeping cards and decisions unchanged");
    }
}
