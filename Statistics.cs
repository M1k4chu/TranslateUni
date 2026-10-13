namespace TranslateUni;

public sealed partial class GameApp
{
    readonly Dictionary<int, (string Title, string Description)> statDetails = [];
    public const int StatChangeRow = 32, StatBarTop = 34, StatBarBottom = 36;
    void DrawStats(Canvas c)
    {
        Run g = Game!;
        StatSnapshot now = g.CaptureStats(), old = g.MonthStartStats;
        string[] names = ["CASH", "DEBT", "PRICE", "STOCK", "COST", "SHARE", "CAP", "QUALITY", "RISK", "SERVICE"];
        double[] values = [now.Cash, now.Debt, now.Price, now.Stock, now.Cost, now.Share, now.Capacity, now.Quality, now.Risk, now.Service];
        double[] previous = [old.Cash, old.Debt, old.Price, old.Stock, old.Cost, old.Share, old.Capacity, old.Quality, old.Risk, old.Service];
        double[] maxima = [10000, 5000, 60, Math.Max(500,g.Capacity), 25, 60, 1000, 100, 100, 100];
        ConsoleColor[] palette = [ConsoleColor.Green, ConsoleColor.Red, ConsoleColor.Cyan, ConsoleColor.Blue, ConsoleColor.Magenta, ConsoleColor.Yellow, ConsoleColor.DarkCyan, ConsoleColor.DarkGreen, ConsoleColor.DarkRed, ConsoleColor.DarkBlue];
        // Claim controls sit at row 29. The whole statistics board is two rows lower than v5.4.
        for (int row = StatChangeRow; row <= 38; row++) c.Text(0, row, new string(' ', c.Width));
        int usable = c.Width - 4;
        for (int i = 0; i < names.Length; i++)
        {
            int left = 2 + i * usable / names.Length, right = 2 + (i + 1) * usable / names.Length;
            int center = (left + right) / 2, cellWidth = right - left;
            bool currency = i is 0 or 1 or 2 or 4, percentage = i is 5 or 7 or 8 or 9;
            double delta = values[i] - previous[i];
            ConsoleColor change = palette[i];
            string amount(double value, bool signed)
            {
                string sign = signed ? value > .005 ? "+" : value < -.005 ? "-" : "" : value < 0 ? "-" : "";
                double absolute = Math.Abs(value);
                string number = absolute >= 1000000 ? $"{absolute/1000000:0.#}m" : absolute >= 1000 ? $"{absolute/1000:0.#}k" : $"{absolute:0.#}";
                return sign + (currency ? "$" : "") + number + (percentage ? "%" : "");
            }
            void text(int row, string value, ConsoleColor color)
            { if (value.Length > cellWidth) value = value[..cellWidth]; c.Text(center - value.Length / 2, row, value, color); }
            text(StatChangeRow, amount(delta, true), change);
            // Row 33 remains empty: changes always have one blank row before the bars.
            double fill = Math.Clamp(values[i] / maxima[i], 0, 1) * 24;
            string[] blocks = [" ", "▁", "▂", "▃", "▄", "▅", "▆", "▇", "█"];
            for (int row = StatBarTop; row <= StatBarBottom; row++)
            {
                int units = Math.Clamp((int)Math.Round(fill - (StatBarBottom - row) * 8), 0, 8);
                c.Text(center - 2, row, "│", ConsoleColor.DarkGray);
                c.Text(center - 1, row, blocks[units] + blocks[units] + blocks[units], palette[i]);
                c.Text(center + 2, row, "│", ConsoleColor.DarkGray);
            }
            text(37, names[i], palette[i]);
            text(38, amount(values[i], false), palette[i]);
            string[] meanings = ["Cash pays for decisions, upgrades and loan repayments.", "Debt is your remaining loan principal plus the fixed interest fee. Clear it to start the three-month loan cooldown.", "Unit price is what each customer pays. Higher prices may lower demand.", "Stock is the number of products ready to sell.", "Unit cost is the production cost per sold product.", "Share measures your slice of the market.", "Capacity limits how many products you can sell per month.", "Quality measures how happy customers are with your product.", "Risk is your exposure to trouble; lower is safer.", "Service is the percentage of customer demand you can fulfill."];
            statDetails[i] = (names[i], $"{meanings[i]} Current: {amount(values[i], false)}. Month start: {amount(previous[i], false)}. Change: {amount(delta, true)}. Bar is full at {amount(maxima[i], false)}. The value above the bar compares with this month's start; values beyond the bar limit are still shown below.");
            c.Buttons.Add(new(left, StatChangeRow, cellWidth, 7, "statInspect", i));
        }
    }
}
