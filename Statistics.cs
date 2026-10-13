namespace TranslateUni;

public sealed partial class GameApp
{
    readonly Dictionary<int, (string Title, string Description)> statDetails = [];
    public const int StatChangeRow = 34, StatBarTop = 36, StatBarBottom = 38;
    void DrawStats(Canvas c)
    {
        Run g = Game!;
        StatSnapshot now = animatedStats ?? g.CaptureStats(), old = g.MonthStartStats;
        // Animate the bars and current values; keep the actual monthly change stable.
        double[] finalValues=StatMotion.Values(pendingStatsTo??g.CaptureStats());
        string[] names = ["CASH", "DEBT", "PRICE", "STOCK", "COST", "SHARE", "CAP", "QUALITY", "RISK", "SERVICE"];
        double[] values = [now.Cash, now.Debt, now.Price, now.Stock, now.Cost, now.Share, now.Capacity, now.Quality, now.Risk, now.Service];
        double[] previous = [old.Cash, old.Debt, old.Price, old.Stock, old.Cost, old.Share, old.Capacity, old.Quality, old.Risk, old.Service];
        double[] maxima = [10000, 5000, 60, Math.Max(500,g.Capacity), 25, 60, 1000, 100, 100, 100];
        ConsoleColor[] palette = [ConsoleColor.Green, ConsoleColor.Red, ConsoleColor.Cyan, ConsoleColor.Blue, ConsoleColor.Magenta, ConsoleColor.Yellow, ConsoleColor.DarkCyan, ConsoleColor.DarkGreen, ConsoleColor.DarkRed, ConsoleColor.DarkBlue];
        // Claim controls sit at row 29. The whole statistics board is two rows lower than v5.4.
        for (int row = StatChangeRow; row <= 40; row++) c.Text(0, row, new string(' ', c.Width));
        int usable = c.Width - 4;
        double[] motionFrom=pendingStatsFrom==null?values:StatMotion.Values(pendingStatsFrom), motionTo=pendingStatsTo==null?values:StatMotion.Values(pendingStatsTo);
        for (int i = 0; i < names.Length; i++)
        {
            int left = 2 + i * usable / names.Length, right = 2 + (i + 1) * usable / names.Length;
            int center = (left + right) / 2, cellWidth = right - left;
            bool currency = i is 0 or 1 or 2 or 4, percentage = i is 5 or 7 or 8 or 9;
            double delta = finalValues[i] - previous[i];
            ConsoleColor change = Math.Abs(delta)<.005?ConsoleColor.DarkGray:palette[i];
            string amount(double value, bool signed)
            {
                return StatNumbers.Format(value,currency,percentage,signed,cellWidth);
            }
            void text(int row, string value, ConsoleColor color)
            { if (value.Length > cellWidth) value = value[..cellWidth]; c.Text(center - value.Length / 2, row, value, color); }
            text(StatChangeRow, amount(delta, true), change);
            // Row 35 remains empty: changes always have one blank row before the bars.
            double fill = Math.Clamp(values[i] / maxima[i], 0, 1) * 24;
            string[] blocks = [" ", "▁", "▂", "▃", "▄", "▅", "▆", "▇", "█"];
            for (int row = StatBarTop; row <= StatBarBottom; row++)
            {
                int units = Math.Clamp((int)Math.Round(fill - (StatBarBottom - row) * 8), 0, 8);
                c.Text(center - 2, row, "│", ConsoleColor.DarkGray);
                c.Text(center - 1, row, blocks[units] + blocks[units] + blocks[units], palette[i]);
                c.Text(center + 2, row, "│", ConsoleColor.DarkGray);
            }
            text(39, names[i], palette[i]);
            bool pulse=statsPulse && Math.Abs(motionTo[i]-motionFrom[i])>.001;
            text(40, amount(values[i], false), pulse?ConsoleColor.White:palette[i]);
            string[] meanings = ["Cash pays for decisions, upgrades and loan repayments.", "Debt is your remaining loan principal plus the fixed interest fee. Clear it to start the three-month loan cooldown.", "Unit price is what each customer pays. Higher prices may lower demand.", "Stock is the number of products ready to sell.", "Unit cost is the production cost per sold product, including this month's supplier/event effects.", "Share measures your slice of the market.", "Capacity limits how many products you can sell per month, including temporary boosts.", "Quality measures how happy customers are with your product.", "Risk is your exposure to trouble; lower is safer.", "Service is the percentage of customer demand you can fulfill."];
            string exact(double value,bool signed)=>StatNumbers.Format(value,currency,percentage,signed);
            statDetails[i] = (names[i], $"{meanings[i]} Current: {exact(finalValues[i], false)}. Month start: {exact(previous[i], false)}. Change: {exact(delta, true)}. Bar is full at {exact(maxima[i], false)}. The value above the bar compares with this month's start; values beyond the bar limit are still shown below."+(percentage?" pp means percentage points: 18% to 19% is +1pp.":""));
            c.Buttons.Add(new(left, StatChangeRow, cellWidth, 7, "statInspect", i));
        }
    }
}

