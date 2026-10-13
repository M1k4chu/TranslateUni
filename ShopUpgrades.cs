namespace TranslateUni;

public sealed record ShopEffect(double Demand = 0, double UnitCostSaving = 0, double Margin = 0,
    double OverheadSaving = 0, int Capacity = 0, double Quality = 0, int StockPerMonth = 0,
    double RiskReduction = 0, double Share = 0, double IncomePerMonth = 0, double DebtReduction = 0);

public static class ShopUpgrades
{
    static readonly string[] Names = ["Desk Plant", "Desk Lamp", "Wall Clock", "Motivation Art", "Lucky Cat",
        "Desk Fountain", "Coffee Machine", "Desk Calendar", "Whiteboard", "Bookcase", "Desk Fan", "Office Aquarium",
        "Standing Desk", "Cork Board", "Coin Jar", "Office Printer", "Desk Pen", "Meeting Table", "Neon Sign", "Boss Chair"];
    static readonly int[] BasePrices = [300,450,550,350,400,400,450,350,425,500,375,650,550,450,600,675,750,800,850,1000];
    static readonly string[][] Art =
    [
        [" \\|/ ", " /|\\ ", " [__]"], ["  ___ ", " /___\\", "   |__"], [".---.", "| / |", "'---'"],
        ["+---+", "|UP!|", "+---+"], ["/\\_/\\", "(o.o)", " /|_ "], [" ~|~ ", "(_|_)", "[___]"],
        ["[~~~]", "|[_]|", "'---'"], ["+---+", "| 12|", "+---+"], ["+---+", "|A+B|", "+---+"],
        ["|I||I", "|_||_", "|I||I"], ["\\ | /", "--O--", " /|\\ "], ["+---+", "|><>|", "+---+"],
        ["_____", " | | ", "_| |_"], ["+---+", "|* *|", "+---+"], [" ___ ", "($$$)", "'---'"],
        [" ___ ", "[===]", " [_] "], ["   / ", "  /  ", " /_  "], ["_____", "|___|", " | | "],
        ["+---+", "|OPEN", "+---+"], ["[___]", "|___|", " _|_ "]
    ];
    public static Decoration Offer(int id, int tier)
    {
        if (id is < 0 or >= 20 || tier is < 1 or > 5) throw new ArgumentOutOfRangeException();
        string name = tier == 1 ? Names[id] : tier == 2 ? (id == 19 ? "Comfy Boss Chair" : "Improved " + Names[id])
            : (tier == 3 ? "Gold " : tier == 4 ? "Diamond " : "Cosmic ") + Names[id];
        double[] priceScale = [0.5, 0.9, 1.6, 3, 6];
        return new(name, (int)Math.Round(BasePrices[id] * priceScale[tier - 1]), Description(id, tier), Art[id], id, (Rarity)(tier - 1), tier);
    }
    public static ShopEffect Effect(int id, int tier)
    {
        if (tier == 0) return new();
        if (tier is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(tier));
        double v(params double[] values) => values[tier - 1];
        int n(params int[] values) => values[tier - 1];
        return id switch
        {
            0 => new(Demand: v(.01,.03,.07,.16,.35)),
            1 => new(UnitCostSaving: v(.1,.3,.7,1.5,3)),
            2 => new(),
            3 => new(Margin: v(.15,.5,1.25,3,6)),
            4 => new(),
            5 => new(OverheadSaving: v(10,30,75,180,400)),
            6 => new(Capacity: n(8,20,50,110,240), Quality: v(1,2,5,10,20)),
            7 => new(StockPerMonth: n(5,15,40,100,250)),
            8 => new(RiskReduction: v(1,3,7,15,30), Quality: v(1,3,6,12,24)),
            9 => new(UnitCostSaving: v(.05,.15,.4,.9,1.8)),
            10 => new(OverheadSaving: v(5,20,50,120,275)),
            11 => new(Quality: v(2,5,10,20,35), Demand: v(.005,.015,.04,.1,.22)),
            12 => new(Capacity: n(10,30,75,160,350)),
            13 => new(Share: v(.1,.3,.7,1.5,3.5)),
            14 => new(IncomePerMonth: v(10,30,80,220,600)),
            15 => new(StockPerMonth: n(6,20,55,140,350)),
            16 => new(Margin: v(.1,.35,.9,2.2,5)),
            17 => new(DebtReduction: v(75,200,500,1300,3500), RiskReduction: v(1,2,5,10,20)),
            18 => new(Demand: v(.015,.045,.1,.23,.5)),
            19 => new(Capacity: n(15,35,80,175,400)),
            _ => throw new ArgumentOutOfRangeException(nameof(id))
        };
    }
    public static int BonusDecisions(int id, int tier, int month)
        => id == 2 ? PeriodicBonus(tier, month) : id == 19 ? tier == 1 ? 0 : PeriodicBonus(tier, month) : 0;
    public static int BonusRerolls(int id, int tier, int month) => id == 4 ? PeriodicBonus(tier, month) : 0;
    static int PeriodicBonus(int tier, int month) => tier switch
    { 1 => month % 3 == 0 ? 1 : 0, 2 => month % 2 == 0 ? 1 : 0, 3 => 1, 4 => 2, 5 => 4, _ => 0 };
    public static string Description(int id, int tier)
    {
        ShopEffect e = Effect(id, tier);
        var pieces = new List<string>();
        if (e.Demand > 0) pieces.Add($"Demand +{e.Demand * 100:0.#}%");
        if (e.UnitCostSaving > 0) pieces.Add($"Unit cost -${e.UnitCostSaving:0.00}");
        if (e.Margin > 0) pieces.Add($"+${e.Margin:0.00}/sale");
        if (e.OverheadSaving > 0) pieces.Add($"Overhead -${e.OverheadSaving:0}/mo");
        if (e.Capacity > 0) pieces.Add($"Capacity +{e.Capacity}");
        if (e.Quality > 0) pieces.Add($"Quality +{e.Quality:0}");
        if (e.StockPerMonth > 0) pieces.Add($"Stock +{e.StockPerMonth}/mo");
        if (e.RiskReduction > 0) pieces.Add($"Risk -{e.RiskReduction:0}");
        if (e.Share > 0) pieces.Add($"Share +{e.Share:0.#}%");
        if (e.IncomePerMonth > 0) pieces.Add($"Income +${e.IncomePerMonth:0}/mo");
        if (e.DebtReduction > 0) pieces.Add($"Debt -${e.DebtReduction:0}");
        if (id is 2 or 4 || id == 19 && tier >= 2)
        {
            string what = id == 4 ? "reroll" : "decision";
            pieces.Add(tier <= 2 ? $"+1 {what}/{(tier == 1 ? 3 : 2)}mo" : $"+{(tier == 3 ? 1 : tier == 4 ? 2 : 4)} {what}/mo");
        }
        return string.Join("; ", pieces);
    }
}
