namespace BusinessGame;

public enum Difficulty { Easy, Normal, Hard }
public enum Phase { Decisions, Events, Result, Booster, Shop, Won, Lost }
public enum CardKind
{
    Advertise, RaisePrice, LowerPrice, Restock, Efficiency, Factory, Repay, Borrow,
    SellShares, CustomerCare, Sale, CourseFocus, Research, Insurance, Investment, Express
}
public sealed record Card(CardKind Kind, string Name);
public sealed record EventCard(string Name, string Description, EventData? Imported = null, int Custom = -1);
public sealed record Booster(string Name, string Description, int Id);
public sealed record Decoration(string Name, int Cost, string Boost, string[] Art, int Id);
public sealed record Quota(string Name, double Target, bool AtLeast = true);
public sealed record MonthReport(int Month, double Revenue, double Profit, int Sold, double Service,
    double Cash, double Share, double Debt, double Quality, double Risk, List<string> Failures);
public sealed record Forecast(int Demand, int Sold, double Revenue, double Profit, double Service);

public sealed class Run
{
    public Course Course { get; }
    public Lesson Lesson { get; }
    public int TopicIndex { get; }
    public Difficulty Difficulty { get; }
    public Phase Phase { get; private set; } = Phase.Decisions;
    public int Month { get; private set; } = 1;
    public int Cleared { get; private set; }
    public int Actions { get; private set; }
    public int Rerolls { get; private set; }
    public double Cash { get; private set; } = 5000;
    public double Debt { get; private set; } = 1200;
    public double Price { get; private set; } = 25;
    public double UnitCost { get; private set; } = 12;
    public int Inventory { get; private set; } = 450;
    public int Capacity { get; private set; } = 500;
    public double Share { get; private set; } = 18;
    public double Quality { get; private set; } = 80;
    public double Risk { get; private set; } = 25;
    public double Advertising { get; private set; }
    public double InvestmentIncome { get; private set; }
    public string Message { get; private set; } = "Choose a decision, or end decisions with E.";
    public List<Card> Hand { get; private set; } = [];
    public List<EventCard> Events { get; private set; } = [];
    public List<Booster> Boosters { get; private set; } = [];
    public HashSet<int> Owned { get; } = [];
    public List<string> ActiveBoosts { get; } = [];
    public List<MonthReport> Reports { get; } = [];
    public MonthReport? LastReport => Reports.LastOrDefault();
    public bool BoosterClaimed { get; private set; }
    readonly Random random;
    double spending, demandShock = 1, costShock = 1, capacityShock = 1, referencePrice = 25;
    double extraInterest, demandBonus, marginBonus, fixedDiscount;
    int extraRerolls, extraActions, stockBonus;
    bool insured;

    public static readonly Decoration[] Decorations =
    [
        new("Desk Plant", 300, "+5% demand every month", [" \\|/ ", " /|\\ ", " [__]"], 0),
        new("Desk Lamp", 450, "Unit cost -$0.50", ["  ___ ", " /___\\", "   |__"], 1),
        new("Wall Clock", 550, "+1 decision each month", [".---.", "| / |", "'---'"], 2),
        new("Motivation Art", 350, "+$1 per unit sold", ["+---+", "|UP!|", "+---+"], 3),
        new("Lucky Cat", 400, "+1 reroll each month", ["/\\_/\\", "(o.o)", " /|_ "], 4),
        new("Desk Fountain", 400, "Overhead -$60/month", [" ~|~ ", "(_|_)", "[___]"], 5)
    ];
    static readonly Booster[] BoosterPool =
    [
        new("Brand Spark", "+4% demand every month", 0),
        new("Lean Operations", "Unit cost -$0.35", 1),
        new("Safety Stock", "+100 free stock now", 2),
        new("Investor Grant", "+$450 cash now", 3),
        new("Loyal Customers", "+1% share; +3% demand", 4),
        new("Buying Power", "+$0.50 per sale forever", 5),
        new("Fresh Perspective", "+1 reroll each month", 6),
        new("Reliable Supply", "+40 free stock each month", 7),
        new("Quality Culture", "+8 quality; risk -5", 8)
    ];

    public Run(Course course, int topicIndex, Difficulty difficulty, int seed)
    {
        if (topicIndex < 0 || topicIndex >= course.Lessons.Count) throw new ArgumentOutOfRangeException(nameof(topicIndex));
        Course = course; TopicIndex = topicIndex; Lesson = course.Lessons[topicIndex]; Difficulty = difficulty;
        random = new Random(seed);
        StartMonth();
    }

    public double ProfitTarget => Math.Round((1050 + (int)Difficulty * 375 + TopicIndex * 70) * (1 + (Month - 1) * 0.14));
    public List<Quota> Quotas()
    {
        var result = new List<Quota> { new("Profit", ProfitTarget) };
        switch (Course.Name)
        {
            case "Marketing": result.Add(new("Share", 16 + (int)Difficulty)); break;
            case "Logistics": result.Add(new("Service", 60 + (int)Difficulty * 5)); break;
            case "Probability": result.Add(new("Risk", 65 - (int)Difficulty * 5, false)); break;
            case "Microeconomics": result.Add(new("Quality", 60 + (int)Difficulty * 5)); break;
            case "Finance": result.Add(new("Debt", 4000 - (int)Difficulty * 500, false)); break;
            case "Project Management": result.Add(new("Quality", 65 + (int)Difficulty * 5)); break;
        }
        if (TopicIndex >= 3 && result.All(q => q.Name != "Service")) result.Add(new("Service", 65 + (int)Difficulty * 5));
        if (TopicIndex >= 6) result.Add(new("Cash", 700 + Month * 75 + (int)Difficulty * 150));
        if (TopicIndex >= 9 && result.All(q => q.Name != "Quality")) result.Add(new("Quality", 70 + (int)Difficulty * 3));
        return result;
    }

    public Forecast Preview()
    {
        double elasticity = Lesson.Simulation.Parameters.GetValueOrDefault("Elasticity", 1.6);
        double reference = Lesson.Name == "Monopoly Pricing" ? referencePrice * 1.1 : referencePrice;
        double effectivePrice = Lesson.Name == "Price Ceiling" ? Math.Min(Price, 20)
            : Lesson.Name == "Price Floor" ? Math.Max(Price, 30) : Price;
        double baseDemand = (170 + Month * 10) * (1 + TopicIndex * 0.025);
        // A new economy scaled to the original BusinessGame's $25 price / $12 cost.
        double demand = baseDemand * Math.Pow(effectivePrice / reference, -elasticity)
            * (1 + Math.Log(1 + Advertising / 350) * 0.3 + demandBonus)
            * (0.8 + Share / 90) * demandShock;
        int wanted = Math.Clamp((int)Math.Round(demand), 1, 10000);
        int sold = Math.Min(wanted, Math.Min(Inventory, Math.Max(0, (int)(Capacity * capacityShock))));
        double revenue = sold * (effectivePrice + marginBonus);
        double carbon = Lesson.Name == "Externalities" ? 3 : 0;
        double overhead = Math.Max(60, 300 + TopicIndex * 15 - fixedDiscount);
        double interest = Debt * (0.02 + (int)Difficulty * 0.005 + extraInterest);
        double carryCost = Math.Max(0, Inventory - sold) * (Course.Name == "Logistics" ? 0.35 : 0.08);
        double profit = revenue - sold * (UnitCost * costShock + carbon) - overhead - spending - interest - carryCost + InvestmentIncome;
        return new(wanted, sold, Math.Round(revenue, 2), Math.Round(profit, 2), 100.0 * sold / wanted);
    }

    public double CurrentMetric(string name, Forecast? forecast = null)
    {
        Forecast f = forecast ?? Preview();
        return name switch { "Profit" => f.Profit, "Service" => f.Service, "Cash" => Cash,
            "Share" => Share, "Debt" => Debt, "Quality" => Quality, "Risk" => Risk, _ => 0 };
    }
    public int RestockUnits => Math.Max(180, (int)Math.Ceiling(Preview().Demand * 1.2) - Inventory);
    public double CardCost(CardKind kind) => kind switch
    {
        CardKind.Advertise => 180, CardKind.Restock => RestockUnits * UnitCost * costShock,
        CardKind.Efficiency => 250, CardKind.Factory => 800, CardKind.Repay => Math.Min(350, Debt),
        CardKind.CustomerCare => 120, CardKind.CourseFocus => 150, CardKind.Research => 180,
        CardKind.Insurance => 100, CardKind.Investment => 500, CardKind.Express => 300, _ => 0
    };
    public string[] Describe(CardKind kind) => kind switch
    {
        CardKind.Advertise => ["Spend $180", "Ad reach +350", "Share +0.5%"],
        CardKind.RaisePrice => ["Unit price +$2", "Demand may fall", ">$32: quality loss", "No cash cost"],
        CardKind.LowerPrice => ["Unit price -$2", "Demand may rise", "No cash cost"],
        CardKind.Restock => [$"Stock +{RestockUnits}", $"Pay ${CardCost(kind):N0}", "Stock carries over"],
        CardKind.Efficiency => ["Spend $250", "Unit cost -$0.60", "Permanent boost"],
        CardKind.Factory => ["Spend $800", "Capacity +150", "Free stock +100"],
        CardKind.Repay => [$"Pay ${CardCost(kind):N0}", "Debt falls equally", "Lower interest"],
        CardKind.Borrow => ["Cash +$700", "Debt +$700", "Risk +5"],
        CardKind.SellShares => ["Cash +$500", "Share -1.5%", "No new debt"],
        CardKind.CustomerCare => ["Spend $120", "Quality +8", "Share +0.5%"],
        CardKind.Sale => ["Price -$1", "Month demand +15%", "Quality -2"],
        CardKind.CourseFocus => ["Spend $150", .. FocusDescription()],
        CardKind.Research => ["Spend $180", "Risk -12", "Quality +3"],
        CardKind.Insurance => ["Spend $100", "Block bad events", "This month only"],
        CardKind.Investment => ["Spend $500", "Income +$90/month", "Starts next month"],
        CardKind.Express => ["Spend $300", "Free stock +60", "Month capacity +20%"],
        _ => []
    };
    string[] FocusDescription() => Course.Name switch
    {
        "Marketing" => ["Share +1%", "Ad reach +300"],
        "Logistics" => ["Stock +70", "Capacity +40"],
        "Probability" => ["Risk -15", "Demand +5%"],
        "Microeconomics" => ["Unit cost -$0.40", "Quality +3"],
        "Finance" => ["Debt -$250", "Income +$30/month"],
        _ => ["Quality +10", "Capacity +50"]
    };

    public bool Play(int index)
    {
        if (Phase != Phase.Decisions || index < 0 || index >= Hand.Count) return false;
        Card card = Hand[index]; double cost = CardCost(card.Kind);
        if (Cash < cost) { Message = $"Not enough cash: {card.Name} costs ${cost:N0}."; return false; }
        if (card.Kind == CardKind.Repay && Debt <= 0) { Message = "There is no debt to repay."; return false; }
        Cash -= cost;
        switch (card.Kind)
        {
            case CardKind.Advertise: Advertising += 350; Share += 0.5; spending += cost; break;
            case CardKind.RaisePrice: Price = Math.Min(60, Price + 2); break;
            case CardKind.LowerPrice: Price = Math.Max(5, Price - 2); break;
            case CardKind.Restock: Inventory += RestockUnits; break;
            case CardKind.Efficiency: UnitCost = Math.Max(3, UnitCost - 0.6); break;
            case CardKind.Factory: Capacity += 150; Inventory += 100; break;
            case CardKind.Repay: Debt = Math.Max(0, Debt - cost); break;
            case CardKind.Borrow: Cash += 700; Debt += 700; Risk += 5; break;
            case CardKind.SellShares: Cash += 500; Share = Math.Max(1, Share - 1.5); break;
            case CardKind.CustomerCare: Quality = Math.Min(100, Quality + 8); Share += 0.5; spending += cost; break;
            case CardKind.Sale: Price = Math.Max(5, Price - 1); demandShock *= 1.15; Quality -= 2; break;
            case CardKind.CourseFocus: ApplyFocus(); spending += cost; break;
            case CardKind.Research: Risk = Math.Max(0, Risk - 12); Quality = Math.Min(100, Quality + 3); spending += cost; break;
            case CardKind.Insurance: insured = true; spending += cost; break;
            case CardKind.Investment: pendingIncome += 90; break;
            case CardKind.Express: Inventory += 60; capacityShock *= 1.2; spending += cost; break;
        }
        Share = Math.Clamp(Share, 1, 60); Risk = Math.Clamp(Risk, 0, 100);
        Actions--;
        Message = "Played " + card.Name + ".";
        if (Actions == 0) FinishDecisions(); else DealHand();
        return true;
    }
    double pendingIncome;
    void ApplyFocus()
    {
        switch (Course.Name)
        {
            case "Marketing": Share += 1; Advertising += 300; break;
            case "Logistics": Inventory += 70; Capacity += 40; break;
            case "Probability": Risk = Math.Max(0, Risk - 15); demandBonus += 0.05; break;
            case "Microeconomics": UnitCost = Math.Max(3, UnitCost - 0.4); Quality = Math.Min(100, Quality + 3); break;
            case "Finance": Debt = Math.Max(0, Debt - 250); pendingIncome += 30; break;
            default: Quality = Math.Min(100, Quality + 10); Capacity += 50; break;
        }
    }

    public void FinishDecisions()
    {
        if (Phase != Phase.Decisions) return;
        Phase = Phase.Events; DealEvents();
        Message = "Take one event with ENTER, or leave all three with E.";
    }
    public bool Reroll()
    {
        if (Phase is not (Phase.Decisions or Phase.Events) || Rerolls == 0) return false;
        Rerolls--;
        if (Phase == Phase.Decisions) DealHand(); else DealEvents();
        Message = "Fresh cards dealt.";
        return true;
    }

    public bool TakeEvent(int index)
    {
        if (Phase != Phase.Events || index < 0 || index >= Events.Count) return false;
        EventCard card = Events[index];
        if (card.Imported is EventData data)
        {
            foreach ((string key, double value) in data.Multipliers)
            {
                bool harmful = key == "UnitCost" ? value > 1 : value < 1;
                if (insured && harmful) continue;
                switch (key)
                {
                    case "Demand": demandShock *= value; break;
                    case "UnitCost": costShock *= value; break;
                    case "Capacity": capacityShock *= value; break;
                    case "CompetitorPrice": referencePrice *= value; break;
                }
            }
            if (!insured && data.Additions != null) extraInterest += data.Additions.GetValueOrDefault("Interest");
        }
        else switch (card.Custom)
        {
            case 0: demandShock *= 1.2; break;
            case 1: Cash += 400; spending -= 400; if (!insured) costShock *= 1.08; break;
            case 2: Inventory += 80; if (!insured) Quality = Math.Max(0, Quality - 4); break;
            case 3: Share += 0.8; Quality = Math.Min(100, Quality + 3); break;
            case 4: if (!insured) demandShock *= 0.85; spending -= 350; Cash += 350; break;
            case 5: costShock *= 0.9; break;
            case 6:
                double payout = random.NextDouble() < 0.65 ? 550 : insured ? 0 : -250;
                Cash += payout; spending -= payout; break;
        }
        Message = "Accepted " + card.Name + ".";
        SettleMonth();
        return true;
    }
    public bool SkipEvents()
    {
        if (Phase != Phase.Events) return false;
        Message = "Left all three events."; SettleMonth(); return true;
    }

    void SettleMonth()
    {
        Forecast f = Preview();
        // Ad/event expenses have already affected cash. COGS was paid when stock was purchased.
        double operatingCash = f.Profit + spending + f.Sold * UnitCost;
        Cash += operatingCash;
        Inventory -= f.Sold;
        // Excessive prices erode customer quality, adding a trade-off on advanced topics.
        Quality = Math.Clamp(Quality - Math.Max(0, Price - 32) * 0.3, 0, 100);
        var failures = Quotas().Where(q => q.AtLeast ? CurrentMetric(q.Name, f) + 0.001 < q.Target
            : CurrentMetric(q.Name, f) - 0.001 > q.Target).Select(q => q.Name).ToList();
        if (Cash < 0) failures.Add("Solvency");
        Reports.Add(new(Month, f.Revenue, f.Profit, f.Sold, f.Service, Cash, Share, Debt, Quality, Risk, failures));
        if (failures.Count == 0) { Cleared++; Cash += 200; }
        Phase = Phase.Result;
    }
    public void ContinueResult()
    {
        if (Phase != Phase.Result) return;
        if (LastReport!.Failures.Count > 0) { Phase = Phase.Lost; return; }
        if (Cleared == 12) { Phase = Phase.Won; return; }
        Boosters = Shuffled(BoosterPool).Take(3).ToList();
        BoosterClaimed = false; Phase = Phase.Booster;
    }
    public bool ClaimBooster(int index)
    {
        if (Phase != Phase.Booster || BoosterClaimed || index < 0 || index >= Boosters.Count) return false;
        Booster card = Boosters[index];
        switch (card.Id)
        {
            case 0: demandBonus += 0.04; break;
            case 1: UnitCost = Math.Max(3, UnitCost - 0.35); break;
            case 2: Inventory += 100; break;
            case 3: Cash += 450; break;
            case 4: Share = Math.Min(60, Share + 1); demandBonus += 0.03; break;
            case 5: marginBonus += 0.5; break;
            case 6: extraRerolls++; break;
            case 7: stockBonus += 40; break;
            case 8: Quality = Math.Min(100, Quality + 8); Risk = Math.Max(0, Risk - 5); break;
        }
        BoosterClaimed = true; ActiveBoosts.Add(card.Name); Message = "Booster: " + card.Name;
        Phase = Phase.Shop; return true;
    }
    public bool SkipBooster()
    {
        if (Phase != Phase.Booster) return false;
        BoosterClaimed = true; Phase = Phase.Shop; Message = "Booster skipped."; return true;
    }
    public bool Buy(int index)
    {
        if (Phase != Phase.Shop || index < 0 || index >= Decorations.Length) return false;
        Decoration item = Decorations[index];
        if (Owned.Contains(item.Id)) { Message = "Already owned: " + item.Name; return false; }
        if (Cash < item.Cost) { Message = "Not enough cash for " + item.Name; return false; }
        Cash -= item.Cost; Owned.Add(item.Id);
        switch (item.Id)
        {
            case 0: demandBonus += 0.05; break;
            case 1: UnitCost = Math.Max(3, UnitCost - 0.5); break;
            case 2: extraActions++; break;
            case 3: marginBonus += 1; break;
            case 4: extraRerolls++; break;
            case 5: fixedDiscount += 60; break;
        }
        Message = "Bought " + item.Name + ". " + item.Boost;
        return true;
    }
    public bool NextMonth()
    {
        if (Phase != Phase.Shop || Cleared != Month || Month >= 12) return false;
        Month++; StartMonth(); return true;
    }
    void StartMonth()
    {
        Actions = (Difficulty == Difficulty.Hard ? 2 : 3) + extraActions;
        Rerolls = (Difficulty == Difficulty.Easy ? 4 : Difficulty == Difficulty.Normal ? 3 : 2) + extraRerolls;
        spending = 0; demandShock = costShock = capacityShock = 1; referencePrice = 25; extraInterest = 0;
        insured = false; Phase = Phase.Decisions;
        Inventory += stockBonus;
        InvestmentIncome += pendingIncome; pendingIncome = 0;
        Advertising *= Month == 1 ? 1 : 0.65;
        Message = $"Month {Month}: plan your decisions to meet every quota.";
        DealHand();
    }
    void DealHand()
    {
        var pool = new List<Card>
        {
            new(CardKind.Advertise, "Advertise Product"), new(CardKind.RaisePrice, "Premium Pricing"),
            new(CardKind.LowerPrice, "Price Cut"), new(CardKind.Restock, "Restock Inventory"),
            new(CardKind.Efficiency, "Lean Production"), new(CardKind.Factory, "New Factory"),
            new(CardKind.Repay, "Repay Debt"), new(CardKind.SellShares, "Sell Company Shares"),
            new(CardKind.CustomerCare, "Customer Care"), new(CardKind.Sale, "Black Friday Sale"),
            new(CardKind.CourseFocus, Course.Name + " Focus")
        };
        if (TopicIndex >= 3 || Course.Name == "Finance") pool.Add(new(CardKind.Borrow, "Bridge Loan"));
        if (TopicIndex >= 6 || Course.Name == "Probability")
        {
            pool.Add(new(CardKind.Research, "Market Research")); pool.Add(new(CardKind.Insurance, "Risk Insurance"));
        }
        if (Course.Name == "Finance") pool.Add(new(CardKind.Investment, "Capital Investment"));
        if (Course.Name == "Logistics") pool.Add(new(CardKind.Express, "Express Supplier"));
        Hand = Shuffled(pool).Take(3).ToList();
        // A shortage always offers a way to replenish stock; rerolls never trap the player.
        if (Inventory < Preview().Demand && Hand.All(c => c.Kind != CardKind.Restock))
            Hand[0] = pool.Single(c => c.Kind == CardKind.Restock);
    }
    void DealEvents()
    {
        var pool = Lesson.Simulation.Events.Select(e => new EventCard(e.Name, e.Description, e)).ToList();
        pool.AddRange(new EventCard[]
        {
            new("Viral Spotlight", "Demand +20% this month.", Custom: 0),
            new("Rush Contract", "Cash/profit +$400; unit cost +8% this month.", Custom: 1),
            new("Surplus Delivery", "Free stock +80; quality -4.", Custom: 2),
            new("Happy Customers", "Share +0.8%; quality +3.", Custom: 3),
            new("Quiet Month Grant", "Cash/profit +$350; demand -15% this month.", Custom: 4),
            new("Supplier Rebate", "Unit cost -10% this month.", Custom: 5),
            new("Risky Opportunity", "65%: +$550; 35%: -$250 cash/profit.", Custom: 6)
        });
        Events = Shuffled(pool).Take(3).ToList();
    }
    IEnumerable<T> Shuffled<T>(IEnumerable<T> source)
    {
        var list = source.ToList();
        for (int i = list.Count - 1; i > 0; i--) { int j = random.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
        return list;
    }
}
