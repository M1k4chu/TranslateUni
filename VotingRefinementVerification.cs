namespace TranslateUni;
public static partial class Verification
{
    static void CheckVotingRefinements(List<Course> courses)
    {
        var now=DateTimeOffset.Parse("2026-10-13T12:00:00Z");
        LanMatch Team(int count=3){var m=new LanMatch(courses,PlayMode.Coop,[count],0,0,Difficulty.Easy,123);for(int i=0;i<count;i++)m.Add(new("Voter"+i,i%6),0);m.Start(now);return m;}
        var cards=Team();cards.Input(0,new(ConsoleKey.D1),now);cards.Input(0,new(ConsoleKey.Enter),now);
        foreach(int width in new[]{140,172,237})foreach(int height in new[]{42,46,67})
        {
            Canvas frame=cards.Render(1,width,height,now);var box=frame.Buttons.Single(b=>b.Action=="select"&&b.Index==0);
            Check(frame.Lines()[box.Y+box.Height-1].Substring(box.X,box.Width).Contains("●1"),"card vote badge follows actual card coordinates");
            Check(frame.ColorAt(box.X+2,box.Y+box.Height-1)==LanMatch.PlayerColors[0],"card vote badge uses the player's color");
        }
        cards.Input(1,new(ConsoleKey.D3),now);cards.Input(1,new(ConsoleKey.Enter),now);cards.Tick(now.AddSeconds(20));
        Check(cards.Teams[0].Ballot.Revote,"two cards tie after third player abstains");
        foreach(int width in new[]{140,172,237})
        {
            Canvas frame=cards.Render(0,width,46,now.AddSeconds(20));var visible=frame.Buttons.Where(b=>b.Action=="select").OrderBy(b=>b.X).ToArray();
            Check(visible.Length==2&&visible.Select(b=>b.Index).SequenceEqual(new[]{0,2}),"revote hides the unvoted card and keeps original choice numbers");
            double middle=(width-Math.Min(172,width))/2+32+(Math.Min(140,width-32)-30)/2.0;
            Check(Math.Abs(middle-(visible[0].X+visible[0].Width)-(visible[1].X-middle))<.001&&visible[1].X-visible[0].X-visible[0].Width==2,"tied cards are symmetric around table center with the normal two-column gap");
            Check(!frame.Buttons.Any(b=>b.Action is "loans" or "skip" or "reroll"),"non-tied voting controls disappear during revote");
        }
        cards.Input(2,new(ConsoleKey.D2),now.AddSeconds(21));cards.Input(2,new(ConsoleKey.Enter),now.AddSeconds(21));
        Check(!cards.Teams[0].Ballot.Votes.ContainsKey(2),"keyboard cannot vote for the hidden third card");
        cards.Input(0,new(ConsoleKey.RightArrow),now.AddSeconds(21));Check(cards.Members[0].Card==2,"arrows navigate only surviving tied cards");
        Canvas shifted=cards.Render(2,172,46,now.AddSeconds(21));var target=shifted.Buttons.Single(b=>b.Action=="select"&&b.Index==2);
        cards.Input(2,new(X:target.X,Y:target.Y),now.AddSeconds(21));cards.Input(2,new(ConsoleKey.Enter),now.AddSeconds(21));
        Check(cards.Teams[0].Ballot.Votes[2]==new VoteChoice("play",2),"mouse hitbox follows recentered card");

        var loan=Team();loan.Input(0,new(ConsoleKey.L),now);
        Check(loan.Members.All(m=>m.View.Screen==Screen.Table)&&!loan.Teams[0].Run.HasActiveLoan,"one vote cannot open or take a loan");
        Canvas loanVote=loan.Render(1,172,46,now);var loanButton=loanVote.Buttons.Single(b=>b.Action=="loans");
        Check(loanVote.Lines()[loanButton.Y-1].Contains("●1"),"open-loans vote badge appears at the loans button");
        loan.Input(1,new(ConsoleKey.L),now);loan.Input(2,new(ConsoleKey.L),now);
        Check(loan.Members.All(m=>m.View.Screen==Screen.Loans)&&!loan.Teams[0].Run.HasActiveLoan,"agreeing to open loans shows offers to the whole team without borrowing");
        loan.Input(0,new(ConsoleKey.D1),now);loan.Input(0,new(ConsoleKey.Enter),now);loan.Input(1,new(ConsoleKey.D3),now);loan.Input(1,new(ConsoleKey.Enter),now);loan.Tick(now.AddSeconds(20));
        var loans=loan.Render(2,172,46,now.AddSeconds(20)).Buttons.Where(b=>b.Action=="select").ToArray();
        Check(loans.Length==2&&loans.All(b=>b.Index!=1),"loan revote removes the unused loan offer");
        foreach(int id in new[]{0,1,2}){loan.Input(id,new(ConsoleKey.D3),now.AddSeconds(21));loan.Input(id,new(ConsoleKey.Enter),now.AddSeconds(21));}
        Check(loan.Teams[0].Run.HasActiveLoan&&loan.Members.All(m=>m.View.Screen==Screen.Loans),"loan majority borrows once and keeps repayment visible for everyone");
        loan.Tick(now.AddSeconds(24));loan.Input(0,new(ConsoleKey.P),now.AddSeconds(24));
        Canvas repay=loan.Render(1,172,46,now.AddSeconds(24));var repayButton=repay.Buttons.Single(b=>b.Action=="loanRepay");
        Check(repay.Lines()[repayButton.Y+1].Contains("●1"),"repayment button carries its voter's badge");
        loan.Input(1,new(ConsoleKey.Escape),now.AddSeconds(24));loan.Input(2,new(ConsoleKey.Escape),now.AddSeconds(24));
        Check(loan.Members.All(m=>m.View.Screen==Screen.Table)&&loan.Teams[0].Run.HasActiveLoan,"leaving loans is a separate team vote and does not repay debt");

        var shop=Team();var run=shop.Teams[0].Run;
        run.FinishDecisions();run.SkipEvents();run.ContinueResult();run.AcknowledgeCompanyEvent();run.SkipBooster();shop.Tick(now);
        shop.Input(0,new(ConsoleKey.D1),now);shop.Input(0,new(ConsoleKey.Enter),now);
        Canvas purchase=shop.Render(1,172,46,now);var item=purchase.Buttons.Single(b=>b.Action=="shopSelect"&&b.Index==0);
        Check(purchase.Lines()[item.Y+item.Height-1].Contains("●1"),"shop purchase vote is shown on the actual item");
        shop.Input(1,new(ConsoleKey.D6),now);shop.Input(1,new(ConsoleKey.Enter),now);shop.Tick(now.AddSeconds(20));
        var shelf=shop.Render(2,172,46,now.AddSeconds(20)).Buttons.Where(b=>b.Action=="shopSelect").OrderBy(b=>b.X).ToArray();
        Check(shelf.Length==2&&shelf.Select(b=>b.Index).SequenceEqual(new[]{0,5})&&shelf[0].Y==shelf[1].Y,"shop revote centers the two tied items on one shelf");
        var eight=Team(8);foreach(int id in Enumerable.Range(0,7)){eight.Input(id,new(ConsoleKey.D1),now);eight.Input(id,new(ConsoleKey.Enter),now);}
        Canvas crowded=eight.Render(7,172,46,now);var voted=crowded.Buttons.Single(b=>b.Action=="select"&&b.Index==0);
        for(int i=0;i<7;i++)Check(crowded.Lines()[voted.Y+voted.Height-1-i/4].Substring(voted.X,voted.Width).Contains("●"+(i+1)),"large team vote badges remain inside the card");
        Check(crowded.Lines()[39].Contains("CASH")&&crowded.Lines()[39].Contains("DEBT"),"vote badges preserve the statistics board");
        eight.Input(7,new(ConsoleKey.D1),now);eight.Input(7,new(ConsoleKey.Enter),now);
        Check(eight.Members[0].Animation[0].Canvas.Lines().Any(l=>l.Contains("●8")),"final player's badge remains visible in the selected card animation");
        void ButtonBadge(LanMatch m,string action,int rowOffset=1)
        {
            Canvas frame=m.Render(1,172,46,now);var button=frame.Buttons.Single(b=>b.Action==action);
            Check(frame.Lines()[button.Y+rowOffset][button.X..].Contains("●1"),"vote marker appears on "+action+" control");
        }
        var controls=Team();controls.Input(0,new(ConsoleKey.E),now);ButtonBadge(controls,"skip");controls.Input(0,new(ConsoleKey.R),now);ButtonBadge(controls,"reroll",-1);
        var flow=Team();var business=flow.Teams[0].Run;business.FinishDecisions();flow.Tick(now);
        foreach(int id in new[]{0,1,2}){flow.Input(id,new(ConsoleKey.D1),now);flow.Input(id,new(ConsoleKey.Enter),now);}
        flow.Tick(now.AddSeconds(5));flow.Input(0,new(ConsoleKey.Enter),now.AddSeconds(5));ButtonBadge(flow,"continue");
        foreach(int id in new[]{1,2})flow.Input(id,new(ConsoleKey.Enter),now.AddSeconds(5));
        flow.Input(0,new(ConsoleKey.Enter),now.AddSeconds(5));ButtonBadge(flow,"companyContinue");
        foreach(int id in new[]{1,2})flow.Input(id,new(ConsoleKey.Enter),now.AddSeconds(5));flow.Tick(now.AddSeconds(7));
        flow.Input(0,new(ConsoleKey.D2),now.AddSeconds(7));flow.Input(0,new(ConsoleKey.Enter),now.AddSeconds(7));
        Canvas booster=flow.Render(1,172,46,now.AddSeconds(7));var boost=booster.Buttons.Single(b=>b.Action=="select"&&b.Index==1);
        Check(booster.Lines()[boost.Y+boost.Height-1].Contains("●1"),"booster choice shows its player's badge");
        foreach(int id in new[]{1,2}){flow.Input(id,new(ConsoleKey.D2),now.AddSeconds(7));flow.Input(id,new(ConsoleKey.Enter),now.AddSeconds(7));}flow.Tick(now.AddSeconds(10));
        flow.Input(0,new(ConsoleKey.R),now.AddSeconds(10));ButtonBadge(flow,"shopReroll",-1);
        flow.Input(0,new(ConsoleKey.E),now.AddSeconds(10));ButtonBadge(flow,"skip",-1);
        flow.Input(0,new(ConsoleKey.Spacebar),now.AddSeconds(10));ButtonBadge(flow,"next");

        foreach(var pair in new[]{(1,9),(1,3),(3,16),(5,10),(0,18),(7,15)})foreach(int tier in Enumerable.Range(1,5))
        {
            double metric(ShopEffect e)=>pair.Item1 switch {1 or 3=>e.UnitCostSaving+e.Margin,5=>e.OverheadSaving,0=>e.Demand,_=>e.StockPerMonth};
            double expected=ShopUpgrades.Price(pair.Item1,tier)*(metric(ShopUpgrades.Effect(pair.Item2,tier))-metric(ShopUpgrades.Effect(pair.Item2,tier-1)))/(metric(ShopUpgrades.Effect(pair.Item1,tier))-metric(ShopUpgrades.Effect(pair.Item1,tier-1)));
            Check(Math.Abs(ShopUpgrades.Price(pair.Item2,tier)-expected)<=.501,"matching shop effects have matching prices per added tier benefit");
        }
        var firstEvents=new HashSet<string>();var firstNews=new HashSet<int>();
        foreach(int seed in Enumerable.Range(0,180))
        {
            var sample=new Run(courses[0],0,Difficulty.Easy,seed);sample.FinishDecisions();firstEvents.Add(string.Join('|',sample.Events.Select(e=>e.Name)));sample.SkipEvents();sample.ContinueResult();firstNews.Add(sample.CompanyNews!.Id);
        }
        Check(firstEvents.Count>150&&firstNews.Count==12,"different seeded Marketing runs draw varied event hands and every possible first headline");
        var a=new Run(courses[0],0,Difficulty.Easy,99);var b=new Run(courses[0],0,Difficulty.Easy,99);b.Reroll();a.FinishDecisions();b.FinishDecisions();
        Check(a.Events.SequenceEqual(b.Events),"decision rerolls do not bias the event random stream");
        a.SkipEvents();b.SkipEvents();a.ContinueResult();b.ContinueResult();Check(a.CompanyNews==b.CompanyNews,"decision randomness does not bias seeded company headlines");
        var bag=new Run(courses[0],0,Difficulty.Easy,1234);
        typeof(Run).GetProperty(nameof(Run.InvestmentIncome))!.SetValue(bag,100000000d);
        var news=new List<int>();
        for(int month=0;month<24;month++)
        {
            bag.FinishDecisions();bag.SkipEvents();bag.ContinueResult();Check(bag.CompanyNews!=null&&bag.Phase==Phase.SpecialEvent,"funded company reaches its monthly headline");news.Add(bag.CompanyNews!.Id);bag.AcknowledgeCompanyEvent();
            if(bag.Phase==Phase.Won)bag.EnterEndless();bag.SkipBooster();bag.NextMonth();
        }
        Check(news.Take(12).Distinct().Count()==12&&news.Skip(12).Distinct().Count()==12&&news[11]!=news[12],"each company headline appears once per shuffled cycle with no repeat at cycle boundary");
        Check(Run.CompanyEventPool.All(e=>!e.Flavor.Contains("homework",StringComparison.OrdinalIgnoreCase)),"company headlines use business language");
    }
}
