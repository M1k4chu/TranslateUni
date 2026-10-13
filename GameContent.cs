namespace TranslateUni;

public static class GameContent
{
    public static readonly Card[] Decisions =
    [
        new(CardKind.Advertise, "Advertise Product"), new(CardKind.RaisePrice, "Premium Pricing"),
        new(CardKind.LowerPrice, "Price Cut"), new(CardKind.Restock, "Restock Inventory"),
        new(CardKind.Efficiency, "Lean Production", Rarity.Uncommon), new(CardKind.Factory, "New Factory", Rarity.Rare),
        new(CardKind.Repay, "Repay Debt"),
        new(CardKind.SellShares, "Sell Company Shares"), new(CardKind.CustomerCare, "Customer Care"),
        new(CardKind.Sale, "Black Friday Sale"), new(CardKind.CourseFocus, "Course Playbook"),
        new(CardKind.Research, "Market Research", Rarity.Uncommon), new(CardKind.Insurance, "Risk Insurance", Rarity.Uncommon),
        new(CardKind.Investment, "Capital Investment", Rarity.Uncommon), new(CardKind.Express, "Express Supplier", Rarity.Uncommon),
        new(CardKind.BundleDeal, "Bundle Deal"), new(CardKind.LoyaltyProgram, "Loyalty Program"),
        new(CardKind.StaffTraining, "Staff Training", Rarity.Uncommon), new(CardKind.SupplierDeal, "Supplier Handshake", Rarity.Uncommon),
        new(CardKind.Overtime, "Overtime Shift"), new(CardKind.QualityAudit, "Quality Audit"),
        new(CardKind.Crowdfunding, "Crowdfunding", Rarity.Uncommon), new(CardKind.DebtRefinance, "Debt Refinancing", Rarity.Uncommon),
        new(CardKind.Warehouse, "Bigger Warehouse", Rarity.Uncommon), new(CardKind.ProductRefresh, "Product Refresh", Rarity.Uncommon),
        new(CardKind.Automation, "Robot Interns", Rarity.Rare), new(CardKind.Influencer, "Influencer Campaign", Rarity.Uncommon),
        new(CardKind.PremiumLaunch, "Premium Launch", Rarity.Rare), new(CardKind.EmergencyShipment, "Emergency Shipment", Rarity.Uncommon),
        new(CardKind.ExportContract, "Export Contract", Rarity.Epic), new(CardKind.BoardRestructure, "Boardroom Miracle", Rarity.Legendary)
    ];
    public static string Flavor(CardKind kind) => kind switch
    {
        CardKind.Advertise => "Shout louder, sell more",
        CardKind.RaisePrice => "Fancy price, picky buyers",
        CardKind.LowerPrice => "Cheap stuff, hungry wallets",
        CardKind.Restock => "Empty shelves make sad wallets",
        CardKind.Efficiency => "Same stuff, less waste",
        CardKind.Factory => "More machines, fewer excuses",
        CardKind.Repay => "Tell debt to go home",
        CardKind.Borrow => "Future you pays for this",
        CardKind.SellShares => "Sell ownership, feed the wallet",
        CardKind.CustomerCare => "Happy customers complain less",
        CardKind.Sale => "Low price, more buy",
        CardKind.CourseFocus => "Finally use that business degree",
        CardKind.Research => "Guess less, stress less",
        CardKind.Insurance => "Bad luck meets bubble wrap",
        CardKind.Investment => "Plant money, harvest later",
        CardKind.Express => "Boxes arrive wearing running shoes",
        CardKind.BundleDeal => "Two things, one irresistible excuse",
        CardKind.LoyaltyProgram => "Customers collect points, you profit",
        CardKind.StaffTraining => "Teach humans, reduce chaos",
        CardKind.SupplierDeal => "Handshake now, cheaper stuff forever",
        CardKind.Overtime => "More work, grumpier humans",
        CardKind.QualityAudit => "Catch problems before angry reviews",
        CardKind.Crowdfunding => "Internet wallets join your adventure",
        CardKind.DebtRefinance => "Debt gets a smaller hat",
        CardKind.Warehouse => "Give boxes a bigger bedroom",
        CardKind.ProductRefresh => "Same product, fresh haircut",
        CardKind.Automation => "Robots never request coffee breaks",
        CardKind.Influencer => "Famous face, questionable decisions",
        CardKind.PremiumLaunch => "Fancy product, fancier price tag",
        CardKind.EmergencyShipment => "Panic ordered, boxes delivered",
        CardKind.ExportContract => "Sell stuff beyond your postcode",
        CardKind.BoardRestructure => "Adults entered the boardroom",
        _ => "Make your accountant mildly happier"
    };
    public static string Flavor(Booster card) => card.Id switch
    {
        0 => "Your brand learned to sparkle", 1 => "Waste took an unpaid vacation",
        2 => "Surprise boxes, zero invoices", 3 => "Someone funded your coffee addiction",
        4 => "Customers remembered your name", 5 => "Every sale tips you extra",
        6 => "Shuffle again, blame destiny later", 7 => "Fresh boxes arrive like clockwork",
        8 => "Quality up, panic down", 9 => "Coupons somehow print extra profit",
        10 => "Money clocks in every month", 11 => "Boxes appear without adult supervision",
        12 => "Ideas finally paid the rent", 13 => "Debt stops shouting so loudly",
        14 => "Shake hands, collect extra coins", 15 => "An owl handles extra paperwork",
        16 => "Cheap supplies, suspiciously magical delivery", 17 => "Your reputation hired a bodyguard",
        18 => "A unicorn ate your bills", _ => "Destiny allows another awkward attempt"
    };
    public static string Flavor(EventCard card) => card.Custom switch
    {
        0 => "Everybody suddenly knows your name", 1 => "Fast money, slightly expensive chaos",
        2 => "Free boxes, questionable quality", 3 => "Customers typed nice things today",
        4 => "Quiet sales, loud grant money", 5 => "Supplier found their generous side",
        6 => "Risky deal, uncertain company profit", 7 => "Happy clients, healthier sales",
        8 => "Bulk boxes, smaller price tags", 9 => "Fame arrived without adult supervision",
        10 => "Foreign wallets found your stuff", 11 => "Your patent finally pays rent",
        _ => card.Name switch
        {
            "Demand shift" or "Viral trend" => "Suddenly everybody wants your stuff",
            "Cost shock" or "Raw material shock" => "Making stuff became annoyingly expensive",
            "Recovery" or "Equilibrium returns" => "The market remembered normal exists",
            "Competitor price cut" => "Your rival found the scissors",
            "Supplier disruption" => "The delivery truck got distracted",
            "Rate hike" => "Debt developed expensive taste",
            "Scope change" => "The project grew extra arms",
            "Customer Demand Survey" => "Customers checked, forecasts still standing",
            _ => "The economy did a thing"
        }
    };
    public static string DecorationFlavor(int id) => id switch
    {
        0 => "Leafy coworker attracts more customers", 1 => "Bright ideas, smaller production bills",
        2 => "More time for questionable decisions", 3 => "Inspirational words somehow print money",
        4 => "Tiny cat bullies bad luck", 5 => "Relaxed office, smaller overhead bills",
        6 => "Caffeinated humans make fewer mistakes", 7 => "Dates somehow summon more boxes",
        8 => "Ideas stop hiding in brains", 9 => "Books taught supplies to economize",
        10 => "Cool air, cooler running bills", 11 => "Fish judge your business politely",
        12 => "Standing humans work slightly faster", 13 => "Sticky notes attract loyal buyers",
        14 => "Loose change got a job", 15 => "Printer spits out bonus boxes",
        16 => "Fancy signatures squeeze extra profit", 17 => "Meetings finally did something useful",
        18 => "Bright letters lure curious wallets", _ => "Boss chair unlocks extra decisions"
    };
    public static ConsoleColor Color(Rarity rarity) => rarity switch
    { Rarity.Common => ConsoleColor.Gray, Rarity.Uncommon => ConsoleColor.Green, Rarity.Rare => ConsoleColor.Cyan,
        Rarity.Epic => ConsoleColor.Magenta, _ => ConsoleColor.Yellow };
}

