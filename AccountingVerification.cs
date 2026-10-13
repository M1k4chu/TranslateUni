namespace TranslateUni;
public static partial class Verification
{
    static void CheckAccountingAndSelection(List<Course> courses)
    {
        foreach(var course in courses)
        {
            var run=new Run(course,0,Difficulty.Easy,42);double start=run.Cash;
            run.FinishDecisions();Forecast forecast=run.Preview();run.SkipEvents();MonthReport report=run.LastReport!;
            double reward=report.Failures.Count==0?200:0;
            Check(Math.Abs(run.Cash-start-report.Profit-reward)<.001,"monthly cash credits net profit rather than gross sales for "+course.Name);
            Check(report.Cash==run.Cash&&report.SettlementCash==forecast.Profit,"report and payout include consistent cash totals");
            double settled=run.Cash;Check(!run.SkipEvents()&&run.Cash==settled,"settlement cannot pay the same month twice");
        }
        var paid=new Run(courses[0],0,Difficulty.Easy,42);double initial=paid.Cash;
        paid.Hand[0]=new(CardKind.Advertise,"Advertising");paid.Play(0);paid.FinishDecisions();paid.SkipEvents();
        Check(Math.Abs(paid.Cash-initial-paid.LastReport!.Profit-(paid.LastReport.Failures.Count==0?200:0))<.001,"advertising is subtracted once across immediate payment and monthly profit");
        var income=new Run(courses[0],0,Difficulty.Easy,42);initial=income.Cash;income.FinishDecisions();income.Events[0]=new("Client bonus","",Custom:7);income.TakeEvent(0);
        Check(Math.Abs(income.Cash-initial-income.LastReport!.Profit-(income.LastReport.Failures.Count==0?200:0))<.001,"immediate event income is included once in cash and profit");
        var loss=new Run(courses[0],0,Difficulty.Easy,42);typeof(Run).GetProperty(nameof(Run.Price))!.SetValue(loss,5d);initial=loss.Cash;loss.FinishDecisions();loss.SkipEvents();
        Check(loss.LastReport!.Profit<0&&Math.Abs(loss.Cash-initial-loss.LastReport.Profit)<.001,"negative profit reduces cash instead of paying sales revenue");
        Check(StatNumbers.Format(.6,true,false,true)=="+$0.60"&&StatNumbers.Format(-.004,true,false,true)=="$0.00","small monetary changes retain cents without negative zero");
        Check(StatNumbers.Format(.5,false,true,true)=="+0.5pp"&&StatNumbers.Format(18.5,false,true,false)=="18.5%","rate changes use percentage points while values use percent");
        Check(StatNumbers.Format(1234567890,true,false,true,8).Length<=8,"large changes retain a complete compact number within their cell");
        var overtime=new Run(courses[0],0,Difficulty.Easy,42);overtime.Hand[0]=new(CardKind.Overtime,"Overtime");overtime.Play(0);
        Check(overtime.CaptureStats().Capacity==625,"capacity statistics include temporary overtime boosts");
        overtime.FinishDecisions();overtime.Events[0]=new("Supplier deal","",Custom:5);overtime.TakeEvent(0);
        Check(Math.Abs(overtime.CaptureStats().Cost-10.8)<.001,"unit-cost statistics include temporary supplier savings");
        var shop=new Run(courses[0],0,Difficulty.Easy,42);typeof(Run).GetProperty(nameof(Run.Phase))!.SetValue(shop,Phase.Shop);typeof(Run).GetProperty(nameof(Run.Cash))!.SetValue(shop,150d);
        shop.ShopOffers.Clear();shop.ShopOffers.Add(ShopUpgrades.Offer(0,1) with {Cost=100});shop.ShopOffers.Add(ShopUpgrades.Offer(1,3));shop.ShopTiers[1]=3;shop.ShopOffers.Add(ShopUpgrades.Offer(3,1) with {Cost=50});shop.ShopOffers.Add(ShopUpgrades.Offer(4,1) with {Cost=500});
        Check(shop.BuyableShopItems().SequenceEqual(new[]{0,2}),"only affordable, unlocked next-tier upgrades are buyable");
        Check(ShopSelection.Move(0,ConsoleKey.RightArrow,shop.BuyableShopItems())==2,"navigation skips the maxed item between buyable items");
        var app=new GameApp(courses,new Storage(Path.Combine(Path.GetTempPath(),"TranslateUni-accounting-"+Guid.NewGuid().ToString("N"))));app.AttachLanRun(shop);
        Canvas frame=app.Render(108,42);Check(frame.Buttons.Where(b=>b.Action=="shopSelect").Select(b=>b.Index).SequenceEqual(new[]{0,2}),"mouse hitboxes exclude maxed and unaffordable shop items");
        app.Handle(new(ConsoleKey.D2),frame);Check(app.Render(108,42).Buttons.Single(b=>b.Action=="buy").Index==0,"number keys cannot select a maxed upgrade");
        app.Handle(new(ConsoleKey.D3),app.Render(108,42));app.Handle(new(ConsoleKey.Enter),app.Render(108,42));
        Check(shop.Cash==100&&app.Render(108,42).Buttons.Single(b=>b.Action=="buy").Index==0,"purchase refreshes selection to the remaining affordable upgrade");
        typeof(Run).GetProperty(nameof(Run.Cash))!.SetValue(shop,0d);Check(!app.Render(108,42).Buttons.Any(b=>b.Action is "shopSelect" or "buy"),"empty budget leaves no selectable item or buy button");
    }
}
