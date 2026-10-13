namespace TranslateUni;

public sealed record ProfitLine(string Name, string Calculation, double Amount, string Metric = "cash");
public sealed record ProfitBreakdown(IReadOnlyList<ProfitLine> Lines, double Total, int Demand, int Sold, int AvailableStock, int EffectiveCapacity);

public sealed partial class Run
{
    readonly List<ProfitLine> monthTransactions = [];
    void TrackTransaction(string name, double change)
    { if(Math.Abs(change)>.0001) monthTransactions.Add(new(name,"",-change)); }
    ProfitBreakdown BuildBreakdown(int wanted,int sold,double effectivePrice,double carbon,double overhead,double interest,double carryCost,double profit)
    {
        var lines=new List<ProfitLine>();
        void add(string name,string formula,double amount,string metric="cash",bool always=false)
        { if(always || Math.Abs(amount)>.00001) lines.Add(new(name,formula,Math.Round(amount,2),metric)); }
        add("Sales revenue",$"{sold} x ${effectivePrice:0.00}",sold*effectivePrice,"revenue",true);
        add("Per-sale boosts",$"{sold} x ${marginBonus:0.00}",sold*marginBonus,"profit");
        add("Production cost",$"{sold} x ${MonthStartStats.Cost:0.00}",-sold*MonthStartStats.Cost,"cost",true);
        add("Unit cost improvements",$"{sold} x ${MonthStartStats.Cost-UnitCost:0.00}",sold*(MonthStartStats.Cost-UnitCost),"cost");
        add("Supplier / event effect",$"{sold} x ${UnitCost:0.00} x {1-costShock:+0.###;-0.###;0}",sold*UnitCost*(1-costShock),"cost");
        add("Carbon cost",$"{sold} x ${carbon:0.00}",-sold*carbon,"cost");
        double gross=300+TopicIndex*15;
        add("Office overhead",$"Base ${gross:0} - savings ${gross-overhead:0}",-overhead,"cost",true);
        foreach(var group in monthTransactions.GroupBy(entry=>entry.Name)) add(group.Key,"Decisions / event",group.Sum(entry=>entry.Amount));
        double untracked=-spending-monthTransactions.Sum(entry=>entry.Amount);
        add("Other operating spending","",untracked,"cost");
        add("Debt event charges","",-interest,"debt");
        add("Stock holding cost",$"{Math.Max(0,Inventory-sold)} x ${(Course.Name=="Logistics"?.35:.08):0.00}",-carryCost,"cost",true);
        add("Passive income","Investments / boosters / decor",InvestmentIncome,"income");
        double rounding=Math.Round(profit,2)-lines.Sum(line=>line.Amount);
        add("Rounding","",rounding);
        return new(lines.ToArray(),Math.Round(profit,2),wanted,sold,Inventory,Math.Max(0,(int)(Capacity*capacityShock)));
    }
}

public sealed partial class GameApp
{
    int resultPage;
    const int ResultPageSize=12;
    int? resultRevealCount;
    bool pendingResultAnimation;
    StatSnapshot? pendingStatsFrom, pendingStatsTo, animatedStats;
    bool statsPulse;
    IReadOnlyList<ProfitLine> ResultLines => Game?.LastReport?.Breakdown?.Lines ?? [];
    void DrawProfitStatement(Canvas c)
    {
        Run g=Game!; MonthReport r=g.LastReport!; bool passed=r.Failures.Count==0;
        Header(c,passed?"MONTH CLEARED":"QUOTA MISSED",$"Month {r.Month}{(g.Endless?" / ENDLESS":" / 12")} - what helped, what held us back");
        c.Center(4,$"Sold {r.Sold} / wanted {r.Breakdown?.Demand ?? r.Sold} | Cash ${r.Cash:N0} | Service {r.Service:0.#}%");
        int pages=Math.Max(1,(ResultLines.Count+ResultPageSize-1)/ResultPageSize);
        resultPage=Math.Clamp(resultPage,0,pages-1);
        c.Box(3,5,c.Width-6,18,ConsoleColor.DarkCyan);
        c.Text(5,6,"MONTHLY PROFIT CALCULATION",ConsoleColor.Cyan);
        c.Text(c.Width-17,6,$"PAGE {resultPage+1}/{pages}",ConsoleColor.DarkGray);
        var visible=ResultLines.Skip(resultPage*ResultPageSize).Take(ResultPageSize).ToList();
        int reveal=resultRevealCount??int.MaxValue;
        for(int i=0;i<Math.Min(reveal,visible.Count);i++)
        {
            ProfitLine line=visible[i];
            string label=line.Name+(line.Calculation.Length>0?": "+line.Calculation:"");
            int maxLabel=c.Width-27;
            if(label.Length>maxLabel) label=label[..Math.Max(0,maxLabel-1)]+"…";
            c.Text(5,7+i,$"{resultPage*ResultPageSize+i+1,2}. {label}",GameTheme.Statistic(line.Metric));
            string amount=$"{(line.Amount>=0?"+":"-")}${Math.Abs(line.Amount):N2}";
            c.Text(c.Width-5-amount.Length,7+i,amount,line.Amount>=0?ConsoleColor.Green:ConsoleColor.Red);
        }
        c.Text(5,19,new string('-',c.Width-10),ConsoleColor.DarkCyan);
        if(reveal>visible.Count)
        {
            string total=$"NET PROFIT: ${r.Profit:N2}";
            c.Text(5,20,total,r.Profit>=0?ConsoleColor.Green:ConsoleColor.Red);
            c.Text(5,21,$"Profit quota: ${g.ProfitTarget:N2} | {(r.Profit+r.BridgeFunding>=g.ProfitTarget?"MET":"SHORT")} by ${Math.Abs(r.Profit+r.BridgeFunding-g.ProfitTarget):N2}",ConsoleColor.Cyan);
        }
        if(r.BridgeFunding>0) c.Text(4,23,$"Loan bridge ${r.BridgeFunding:N2} covered the gap; actual profit unchanged.",ConsoleColor.Cyan);
        else c.Text(4,23,passed?"+$200 clear reward (outside operating profit).":"Red costs subtract; green contributions add.",ConsoleColor.Gray);
        int y=24;
        foreach(Quota quota in g.Quotas())
        {
            double value=quota.Name switch {"Profit"=>r.Profit,"Service"=>r.Service,"Cash"=>r.Cash,"Share"=>r.Share,"Debt"=>r.Debt,"Quality"=>r.Quality,"Risk"=>r.Risk,_=>0};
            string financed=quota.Name=="Profit" && r.BridgeFunding>0?" + bridge":"";
            c.Text(4,y++,$"[{(!r.Failures.Contains(quota.Name)?"PASS":"MISS")}] {quota.Name}: {Metric(quota.Name,value)}{financed} / {Metric(quota.Name,quota.Target)}",!r.Failures.Contains(quota.Name)?ConsoleColor.Green:ConsoleColor.Red);
        }
        c.Button(3,28,"ENTER CONTINUE","continue",selected:true);
        if(!g.Tutorial) c.Button(28,28,"L LOANS / REPAY","loans");
        if(pages>1) { c.Button(3,30,"PgUp PREV","resultPrev"); c.Button(25,30,"PgDn NEXT","resultNext"); }
        Footer(c,"ENTER: Continue | PgUp/PgDn: Calculation pages | L: Loans | ESC: Pause");
    }
    public List<AnimationFrame> TakeFeedbackFrames(int width,int height)
    {
        var frames=new List<AnimationFrame>();
        if(Game==null) { pendingStatsFrom=pendingStatsTo=null; pendingResultAnimation=false; return frames; }
        try
        {
            if(pendingResultAnimation && Screen==Screen.Result)
            {
                resultPage=Math.Clamp(resultPage,0,Math.Max(1,(ResultLines.Count+ResultPageSize-1)/ResultPageSize)-1);
                int count=ResultLines.Skip(resultPage*ResultPageSize).Take(ResultPageSize).Count();
                for(int i=0;i<=count+1;i++)
                {
                    resultRevealCount=i;
                    animatedStats=pendingStatsFrom!=null && pendingStatsTo!=null ? StatMotion.Interpolate(pendingStatsFrom,pendingStatsTo,Math.Min(1,i/4.0)) : null;
                    statsPulse=i<3;
                    frames.Add(new(Render(width,height),i==0?80:i==count+1?180:115,AnimationStage.ResultLine,0,0){Areas=[new(0,0,width,height,"feedback")]});
                }
            }
            else if(pendingStatsFrom!=null && pendingStatsTo!=null)
            {
                for(int step=1;step<=10;step++)
                {
                    animatedStats=StatMotion.Interpolate(pendingStatsFrom,pendingStatsTo,step/10.0);
                    statsPulse=step<=4;
                    Canvas drawing=Render(width,height);
                    int top=(height>46?(height-46)/2:0)+StatChangeRow;
                    int left=width>140?(width-140)/2:0;
                    frames.Add(new(drawing,30,AnimationStage.Statistics,0,0){Areas=step==1?[new(0,0,width,height,"feedback")]:[new(left,top,Math.Min(width,140)-30,7,"statistics")]});
                }
            }
        }
        finally { pendingResultAnimation=false; pendingStatsFrom=pendingStatsTo=animatedStats=null; resultRevealCount=null; statsPulse=false; }
        return frames;
    }
}

public static class StatMotion
{
    public static double[] Values(StatSnapshot s)=>[s.Cash,s.Debt,s.Price,s.Stock,s.Cost,s.Share,s.Capacity,s.Quality,s.Risk,s.Service];
    public static StatSnapshot Interpolate(StatSnapshot from,StatSnapshot to,double progress)
    {
        if(progress<=0) return from; if(progress>=1) return to;
        double t=Math.Clamp(progress,0,1); t=1-Math.Pow(1-t,3);
        double n(double a,double b)=>a+(b-a)*t;
        return new(n(from.Cash,to.Cash),n(from.Debt,to.Debt),n(from.Price,to.Price),Math.Round(n(from.Stock,to.Stock)),n(from.Cost,to.Cost),n(from.Share,to.Share),Math.Round(n(from.Capacity,to.Capacity)),n(from.Quality,to.Quality),n(from.Risk,to.Risk),n(from.Service,to.Service));
    }
}
