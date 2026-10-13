namespace BusinessGame;

public enum Difficulty { Easy, Normal, Hard }
public enum Phase { Decisions, Events, Result, SpecialEvent, Booster, Shop, Won, Lost }
public enum Rarity { Common, Uncommon, Rare, Epic, Legendary }
public enum BoosterFamily { Green, Purple }
public enum CardKind
{
    Advertise, RaisePrice, LowerPrice, Restock, Efficiency, Factory, Repay, Borrow,
    SellShares, CustomerCare, Sale, CourseFocus, Research, Insurance, Investment, Express,
    BundleDeal, LoyaltyProgram, StaffTraining, SupplierDeal, Overtime, QualityAudit,
    Crowdfunding, DebtRefinance, Warehouse, ProductRefresh, Automation, Influencer,
    PremiumLaunch, EmergencyShipment, ExportContract, BoardRestructure
}
public sealed record Card(CardKind Kind, string Name, Rarity Rarity = Rarity.Common);
public sealed record EventCard(string Name, string Description, EventData? Imported = null, int Custom = -1, Rarity Rarity = Rarity.Common);
public sealed record Booster(string Name, string Description, int Id, Rarity Rarity = Rarity.Common,
    BoosterFamily Family = BoosterFamily.Green, bool Persistent = true);
public sealed record ActiveBooster(Booster Card, int Stacks = 1);
public sealed record CompanyEvent(string Name, string Flavor, string Description, int Id, Rarity Rarity = Rarity.Common);
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
    public List<ActiveBooster> ActiveBoosts { get; } = [];
    public CompanyEvent? CompanyNews { get; private set; }
    public List<CompanyEvent> NewsHistory { get; } = [];
    public bool Tutorial { get; }
    public List<MonthReport> Reports { get; } = [];
    public MonthReport? LastReport => Reports.LastOrDefault();
    public bool BoosterClaimed { get; private set; }
    readonly Random random;
    double spending, demandShock = 1, costShock = 1, capacityShock = 1, referencePrice = 25;
    double extraInterest, demandBonus, marginBonus, fixedDiscount;
    int extraRerolls, extraActions, stockBonus;
    bool insured;
    double nextDemand = 1, nextCost = 1, nextCapacity = 1, nextOverheadDiscount, monthOverheadDiscount;

    public static readonly Decoration[] Decorations =
    [
        new("Desk Plant", 300, "+5% demand every month", [" \\|/ ", " /|\\ ", " [__]"], 0),
        new("Desk Lamp", 450, "Unit cost -$0.50", ["  ___ ", " /___\\", "   |__"], 1),
        new("Wall Clock", 550, "+1 decision each month", [".---.", "| / |", "'---'"], 2),
        new("Motivation Art", 350, "+$1 per unit sold", ["+---+", "|UP!|", "+---+"], 3),
        new("Lucky Cat", 400, "+1 reroll each month", ["/\\_/\\", "(o.o)", " /|_ "], 4),
        new("Desk Fountain", 400, "Overhead -$60/month", [" ~|~ ", "(_|_)", "[___]"], 5)
    ];
    public static readonly Booster[] BoosterPool =
    [
        new("Brand Spark", "+4% demand every month", 0),
        new("Lean Operations", "Unit cost -$0.35", 1),
        new("Safety Stock", "+100 free stock now", 2, Persistent: false),
        new("Investor Grant", "+$450 cash now", 3, Persistent: false),
        new("Loyal Customers", "+1% share; +3% demand", 4),
        new("Buying Power", "+$0.50 per sale forever", 5),
        new("Fresh Perspective", "+1 reroll each month", 6, Rarity.Uncommon),
        new("Reliable Supply", "+40 free stock each month", 7, Rarity.Uncommon),
        new("Quality Culture", "+8 quality; risk -5", 8, Rarity.Uncommon),
        new("Coupon Engine", "+$0.75 per sale; -$25 overhead/month", 9, Rarity.Rare, BoosterFamily.Purple),
        new("Steady Dividend", "+$75 income each month", 10, Rarity.Uncommon),
        new("Warehouse Fairy", "+65 free stock each month", 11, Rarity.Rare, BoosterFamily.Purple),
        new("Creative Team", "+6% demand every month", 12, Rarity.Uncommon),
        new("Credit Whisperer", "Debt -$350 now; overhead -$20/month", 13, Rarity.Uncommon),
        new("Golden Handshake", "+$1.50 per sale", 14, Rarity.Epic, BoosterFamily.Purple),
        new("Overtime Owl", "+1 decision each month", 15, Rarity.Rare, BoosterFamily.Purple),
        new("Supply Wizard", "Unit cost -$0.80; capacity +60", 16, Rarity.Epic, BoosterFamily.Purple),
        new("PR Guardian", "+6 quality; next scandals softened", 17, Rarity.Rare, BoosterFamily.Purple),
        new("Unicorn Investor", "+$150 income and +35 free stock/month", 18, Rarity.Legendary, BoosterFamily.Purple),
        new("Second Chance", "+1 reroll/month; risk -8", 19, Rarity.Uncommon)
    ];

    public static readonly CompanyEvent[] CompanyEventPool =
    [
        new("New Competitor", "Someone stole your homework", "Next month: demand -8%. Restock cautiously or improve advertising.", 0),
        new("Internet Scandal", "The comments are on fire", "Next month: demand -10%. Quality -6 and risk +5 now. PR Guardian halves these losses.", 1),
        new("Good PR", "The internet likes you today", "Next month: demand +10%. Share +0.5% now.", 2),
        new("New Company Majority", "New boss, extra bossiness", "Cash +$350 now; share -0.5% and risk +3. No new debt.", 3, Rarity.Uncommon),
        new("Supplier Strike", "Boxes took the day off", "Next month: capacity -10%. Existing inventory is safe.", 4),
        new("Venture Investor", "Money arrived wearing a suit", "Cash +$500 now; debt +$200. More cash, more responsibility.", 5, Rarity.Uncommon),
        new("Viral Meme", "Your logo became a meme", "Next month: demand +15%. Quality +2 now.", 6, Rarity.Rare),
        new("Tax Relief", "The taxman found compassion", "Next month's overhead is $80 lower.", 7, Rarity.Uncommon),
        new("Local Business Award", "Mom finally understands your job", "Quality +4 and share +0.3% now.", 8),
        new("Market Boom", "Everybody suddenly wants your stuff", "Next month: demand +12%, unit costs +5%.", 9, Rarity.Uncommon),
        new("Board Audit", "Spreadsheets scared the debt away", "Debt -$100 and risk -5 now.", 10),
        new("Logistics Breakthrough", "Boxes learned to teleport", "Free stock +50 and permanent capacity +25.", 11, Rarity.Rare)
    ];

    public Run(Course course, int topicIndex, Difficulty difficulty, int seed, bool tutorial = false)
    {
        if (topicIndex < 0 || topicIndex >= course.Lessons.Count) throw new ArgumentOutOfRangeException(nameof(topicIndex));
        Course = course; TopicIndex = topicIndex; Lesson = course.Lessons[topicIndex]; Difficulty = difficulty;
        random = new Random(seed);
        Tutorial = tutorial;
        StartMonth();
    }

    public double ProfitTarget => ProfitTargetFor(Month);
    public double ProfitTargetFor(int month) => Math.Round((1050 + (int)Difficulty * 375 + TopicIndex * 70) * (1 + (month - 1) * 0.14));
    public List<Quota> Quotas(int? forMonth = null)
    {
        int quotaMonth = forMonth ?? Month;
        var result = new List<Quota> { new("Profit", ProfitTargetFor(quotaMonth)) };
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
        if (TopicIndex >= 6) result.Add(new("Cash", 700 + quotaMonth * 75 + (int)Difficulty * 150));
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
        double overhead = Math.Max(60, 300 + TopicIndex * 15 - fixedDiscount - monthOverheadDiscount);
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
        CardKind.Insurance => 100, CardKind.Investment => 500, CardKind.Express => 300,
        CardKind.BundleDeal => 90, CardKind.LoyaltyProgram => 160, CardKind.StaffTraining => 250,
        CardKind.SupplierDeal => 220, CardKind.Overtime => 180, CardKind.QualityAudit => 140,
        CardKind.DebtRefinance => 200, CardKind.Warehouse => 450, CardKind.ProductRefresh => 300,
        CardKind.Automation => 900, CardKind.Influencer => 380, CardKind.PremiumLaunch => 650,
        CardKind.EmergencyShipment => 450, CardKind.ExportContract => 200, CardKind.BoardRestructure => 700, _ => 0
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
        CardKind.BundleDeal => ["Spend $90", "Month demand +12%", "Price -$0.50"],
        CardKind.LoyaltyProgram => ["Spend $160", "Share +0.8%", "Demand +2% forever"],
        CardKind.StaffTraining => ["Spend $250", "Quality +6", "Capacity +30"],
        CardKind.SupplierDeal => ["Spend $220", "Unit cost -$0.40", "Permanent saving"],
        CardKind.Overtime => ["Spend $180", "Month capacity +25%", "Quality -2"],
        CardKind.QualityAudit => ["Spend $140", "Risk -8", "Quality +5"],
        CardKind.Crowdfunding => ["Cash +$600", "Debt +$300", "Share -0.5%"],
        CardKind.DebtRefinance => ["Spend $200", "Debt -$250", "Risk -4"],
        CardKind.Warehouse => ["Spend $450", "Capacity +75", "Free stock +80"],
        CardKind.ProductRefresh => ["Spend $300", "Quality +4", "Demand +3% forever"],
        CardKind.Automation => ["Spend $900", "Unit cost -$0.90", "Capacity +60"],
        CardKind.Influencer => ["Spend $380", "Ad reach +800", "Risk +4; share +0.4%"],
        CardKind.PremiumLaunch => ["Spend $650", "Price +$2", "Quality +6", "Share +0.6%"],
        CardKind.EmergencyShipment => ["Spend $450", "Free stock +100", "Immediate delivery"],
        CardKind.ExportContract => ["Spend $200", "Cash/profit +$150", "Month demand +25%"],
        CardKind.BoardRestructure => ["Spend $700", "Debt -$400", "Quality +8; share +1%", "Risk -8"],
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
            case CardKind.BundleDeal: Price = Math.Max(5, Price - 0.5); demandShock *= 1.12; spending += cost; break;
            case CardKind.LoyaltyProgram: Share += 0.8; demandBonus += 0.02; spending += cost; break;
            case CardKind.StaffTraining: Quality = Math.Min(100, Quality + 6); Capacity += 30; break;
            case CardKind.SupplierDeal: UnitCost = Math.Max(3, UnitCost - 0.4); break;
            case CardKind.Overtime: capacityShock *= 1.25; Quality = Math.Max(0, Quality - 2); spending += cost; break;
            case CardKind.QualityAudit: Risk = Math.Max(0, Risk - 8); Quality = Math.Min(100, Quality + 5); spending += cost; break;
            case CardKind.Crowdfunding: Cash += 600; Debt += 300; Share = Math.Max(1, Share - 0.5); break;
            case CardKind.DebtRefinance: Debt = Math.Max(0, Debt - 250); Risk = Math.Max(0, Risk - 4); break;
            case CardKind.Warehouse: Capacity += 75; Inventory += 80; break;
            case CardKind.ProductRefresh: Quality = Math.Min(100, Quality + 4); demandBonus += 0.03; break;
            case CardKind.Automation: UnitCost = Math.Max(3, UnitCost - 0.9); Capacity += 60; break;
            case CardKind.Influencer: Advertising += 800; Risk += 4; Share += 0.4; spending += cost; break;
            case CardKind.PremiumLaunch: Price = Math.Min(60, Price + 2); Quality = Math.Min(100, Quality + 6); Share += 0.6; break;
            case CardKind.EmergencyShipment: Inventory += 100; break;
            case CardKind.ExportContract: demandShock *= 1.25; Cash += 150; spending += cost - 150; break;
            case CardKind.BoardRestructure: Debt = Math.Max(0, Debt - 400); Quality = Math.Min(100, Quality + 8); Share++; Risk = Math.Max(0, Risk - 8); break;
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
            case 7: Cash += 180; spending -= 180; Quality = Math.Min(100, Quality + 2); break;
            case 8: costShock *= 0.85; demandShock *= 1.05; break;
            case 9: Advertising += 400; Risk = Math.Min(100, Risk + 6); break;
            case 10: Cash += 600; spending -= 600; if (!insured) capacityShock *= 0.9; break;
            case 11: Cash += 900; spending -= 900; break;
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
        CompanyNews = Tutorial ? CompanyEventPool[2] : Weighted(CompanyEventPool, e => e.Rarity, 1)[0];
        Phase = Phase.SpecialEvent;
    }
    public bool AcknowledgeCompanyEvent()
    {
        if (Phase != Phase.SpecialEvent || CompanyNews == null) return false;
        CompanyEvent news = CompanyNews;
        NewsHistory.Add(news);
        if (Cleared == 12) { Phase = Phase.Won; return true; }
        switch (news.Id)
        {
            case 0: nextDemand *= 0.92; break;
            case 1:
                double loss = prGuardian ? 0.5 : 1;
                nextDemand *= 1 - 0.10 * loss; Quality = Math.Max(0, Quality - 6 * loss); Risk = Math.Min(100, Risk + 5 * loss); break;
            case 2: nextDemand *= 1.1; Share = Math.Min(60, Share + 0.5); break;
            case 3: Cash += 350; Share = Math.Max(1, Share - 0.5); Risk = Math.Min(100, Risk + 3); break;
            case 4: nextCapacity *= 0.9; break;
            case 5: Cash += 500; Debt += 200; break;
            case 6: nextDemand *= 1.15; Quality = Math.Min(100, Quality + 2); break;
            case 7: nextOverheadDiscount += 80; break;
            case 8: Quality = Math.Min(100, Quality + 4); Share = Math.Min(60, Share + 0.3); break;
            case 9: nextDemand *= 1.12; nextCost *= 1.05; break;
            case 10: Debt = Math.Max(0, Debt - 100); Risk = Math.Max(0, Risk - 5); break;
            case 11: Inventory += 50; Capacity += 25; break;
        }
        Boosters = Tutorial ? [BoosterPool[0], BoosterPool[9], BoosterPool[7]] : Weighted(BoosterPool, b => b.Rarity, 3);
        BoosterClaimed = false; Phase = Phase.Booster;
        Message = news.Name + ": changes begin now or next month, as shown.";
        return true;
    }
    bool prGuardian;
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
            case 9: marginBonus += 0.75; fixedDiscount += 25; break;
            case 10: pendingIncome += 75; break;
            case 11: stockBonus += 65; break;
            case 12: demandBonus += 0.06; break;
            case 13: Debt = Math.Max(0, Debt - 350); fixedDiscount += 20; break;
            case 14: marginBonus += 1.5; break;
            case 15: extraActions++; break;
            case 16: UnitCost = Math.Max(3, UnitCost - 0.8); Capacity += 60; break;
            case 17: Quality = Math.Min(100, Quality + 6); prGuardian = true; break;
            case 18: pendingIncome += 150; stockBonus += 35; break;
            case 19: extraRerolls++; Risk = Math.Max(0, Risk - 8); break;
        }
        BoosterClaimed = true; TrackBooster(card); Message = "Booster: " + card.Name;
        if (Tutorial)
        {
            Booster gift = card.Family == BoosterFamily.Green ? BoosterPool[9] : BoosterPool[0];
            if (gift.Id == 9) { marginBonus += 0.75; fixedDiscount += 25; }
            else demandBonus += 0.04;
            TrackBooster(gift);
            Message += ". Tutorial gift: " + gift.Name + ".";
        }
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
        Rerolls = 1 + extraRerolls;
        spending = 0; demandShock = nextDemand; costShock = nextCost; capacityShock = nextCapacity;
        monthOverheadDiscount = nextOverheadDiscount;
        nextDemand = nextCost = nextCapacity = 1; nextOverheadDiscount = 0;
        referencePrice = 25; extraInterest = 0;
        insured = false; Phase = Phase.Decisions;
        Inventory += stockBonus;
        InvestmentIncome += pendingIncome; pendingIncome = 0;
        Advertising *= Month == 1 ? 1 : 0.65;
        Message = $"Month {Month}: plan your decisions to meet every quota.";
        DealHand();
    }
    void DealHand()
    {
        List<Card> pool = GameContent.Decisions.ToList();
        tutorialDeals++;
        CardKind[] lessonHand = tutorialDeals == 1 ? [CardKind.Advertise, CardKind.Restock, CardKind.Sale]
            : tutorialDeals == 2 ? [CardKind.Efficiency, CardKind.Restock, CardKind.CustomerCare]
            : [CardKind.Sale, CardKind.Research, CardKind.Restock];
        Hand = Tutorial ? lessonHand.Select(kind => pool.Single(c => c.Kind == kind)).ToList() : Weighted(pool, c => c.Rarity, 3);
        // A shortage always offers a way to replenish stock; rerolls never trap the player.
        if (Inventory < Preview().Demand && Hand.All(c => c.Kind != CardKind.Restock))
            Hand[0] = pool.Single(c => c.Kind == CardKind.Restock);
    }
    int tutorialDeals;
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
        pool.AddRange(new EventCard[]
        {
            new("Office Bake Sale", "Cash/profit +$180; quality +2.", Custom: 7),
            new("Bulk Supplier Deal", "Unit cost -15% this month; demand +5%.", Custom: 8, Rarity: Rarity.Uncommon),
            new("Influencer Oops", "Ad reach +400; risk +6.", Custom: 9, Rarity: Rarity.Uncommon),
            new("Export Windfall", "Cash/profit +$600; capacity -10% this month.", Custom: 10, Rarity: Rarity.Rare),
            new("Corporate Jackpot", "Cash/profit +$900 now.", Custom: 11, Rarity: Rarity.Legendary)
        });
        Events = Tutorial ? [pool.First(e => e.Custom == 0), pool.First(e => e.Custom == 5), pool.First(e => e.Custom == 6)]
            : Weighted(pool, e => e.Rarity, 3);
    }
    void TrackBooster(Booster card)
    {
        if (!card.Persistent) return; // Cash/stock gifts are spent immediately, not active effects.
        int index = ActiveBoosts.FindIndex(b => b.Card.Id == card.Id);
        if (index < 0) ActiveBoosts.Add(new(card));
        else ActiveBoosts[index] = ActiveBoosts[index] with { Stacks = ActiveBoosts[index].Stacks + 1 };
    }
    public static double RarityWeight(Rarity rarity) => rarity switch
    { Rarity.Common => 6, Rarity.Uncommon => 3, Rarity.Rare => 1, Rarity.Epic => 0.35, _ => 0.1 };
    List<T> Weighted<T>(IEnumerable<T> source, Func<T, Rarity> rarity, int count)
        => source.Select(item => (Item: item, Score: -Math.Log(Math.Max(1e-12, random.NextDouble())) / RarityWeight(rarity(item))))
            .OrderBy(item => item.Score).Take(count).Select(item => item.Item).ToList();
    IEnumerable<T> Shuffled<T>(IEnumerable<T> source)
    {
        var list = source.ToList();
        for (int i = list.Count - 1; i > 0; i--) { int j = random.Next(i + 1); (list[i], list[j]) = (list[j], list[i]); }
        return list;
    }
}
