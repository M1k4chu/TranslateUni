namespace TranslateUni;

public sealed record LoanOffer(string Name, double Amount, double InterestRate)
{
    public double Fee => Math.Round(Amount * InterestRate, 2);
    public double Total => Math.Round(Amount + Fee, 2);
}
public sealed record StatSnapshot(double Cash, double Debt, double Price, double Stock, double Cost,
    double Share, double Capacity, double Quality, double Risk, double Service);

public sealed partial class Run
{
    public int NextLoanMonth { get; private set; } = 1;
    public bool HasActiveLoan => Debt > 0.001;
    public bool CreditPhase => Phase is not (Phase.Won or Phase.Lost);
    public bool CanBorrow => CreditPhase && !HasActiveLoan && Month >= NextLoanMonth;
    public string LoanStatus => HasActiveLoan ? $"Active balance: ${Debt:N0}" : Month < NextLoanMonth
        ? $"Next loan: month {NextLoanMonth} ({NextLoanMonth - Month}mo)" : "Loan available";
    public List<LoanOffer> LoanOffers { get; } = [];
    public StatSnapshot MonthStartStats { get; private set; } = new(5000,0,25,450,12,18,500,80,25,100);
    public double EffectiveUnitCost=>UnitCost*costShock;
    public int EffectiveCapacity=>Math.Max(0,(int)(Capacity*capacityShock));
    public StatSnapshot CaptureStats() => new(Cash, Debt, Price, Inventory, EffectiveUnitCost, Share, EffectiveCapacity, Quality, Risk, Phase is Phase.Decisions or Phase.Events ? Preview().Service : LastReport?.Service ?? Preview().Service);
    public double BridgeCost => Phase == Phase.Result && LastReport?.Failures.Contains("Profit") == true
        ? Math.Max(0, ProfitTarget - LastReport.Profit) : 0;
    public void PrepareLoans()
    {
        if (LoanOffers.Count > 0) return; // Browsing never rerolls loan prices.
        double profitGap = Phase == Phase.Result ? BridgeCost : Math.Max(0, ProfitTarget - Preview().Profit);
        double cashGap = Math.Max(0, Math.Max(300, Quotas().FirstOrDefault(q => q.Name == "Cash")?.Target ?? 300) - Cash);
        double small = Math.Max(500, profitGap + cashGap + 300);
        double variation() => (random.Next(2) == 0 ? -1 : 1) * (.05 + random.NextDouble() * .05);
        string[] names = ["Keep Us Afloat", "Room to Breathe", "Big Umbrella"];
        for (int i = 0; i < 3; i++)
            LoanOffers.Add(new(names[i], Math.Round(small * (i + 1) * (1 + variation()), 2), Math.Round(.5 * (i + 1) * (1 + variation()), 4)));
    }
    public bool TakeLoan(int index)
    {
        PrepareLoans();
        if (!CanBorrow || index < 0 || index >= LoanOffers.Count)
        { Message = LoanStatus + ". Repay fully before borrowing again."; return false; }
        LoanOffer offer = LoanOffers[index];
        double bridge = BridgeCost;
        if (Phase == Phase.Result)
        {
            if (LastReport!.Failures.Any(f => f is not ("Profit" or "Cash" or "Solvency")))
            { Message = "Loans can cover money shortfalls, not quality or service failures."; return false; }
            double cashAfter = Cash + offer.Amount - bridge;
            if (cashAfter < 0 || Quotas().Any(q => q.Name == "Cash" && cashAfter < q.Target || q.Name == "Debt" && Debt + offer.Total > q.Target))
            { Message = "This loan would still miss a cash or debt quota. Try another offer."; return false; }
        }
        Cash += offer.Amount; Debt += offer.Total;
        if (Phase == Phase.Result)
        {
            bool rescue = LastReport!.Failures.Count > 0;
            Cash -= bridge;
            if (rescue) { Cleared++; Cash += 200; }
            Reports[^1] = LastReport with { Cash = Cash, Debt = Debt, Failures = rescue ? [] : LastReport.Failures, BridgeFunding = bridge };
        }
        Message = $"Borrowed ${offer.Amount:N0}; fee ${offer.Fee:N0}; repay ${offer.Total:N0}.";
        return true;
    }
    public bool RepayLoan()
    {
        if (!CreditPhase || Debt <= 0 || Cash <= 0) { Message = "No repayable debt or cash available."; return false; }
        double payment = Math.Min(Cash, Debt);
        Cash -= payment; Debt -= payment;
        Message = Debt <= 0 ? $"Loan repaid. Next loan in month {NextLoanMonth}." : $"Paid ${payment:N0}; ${Debt:N0} still owed.";
        if (Phase == Phase.Result && LastReport != null)
        {
            MonthReport r = LastReport;
            var failures = r.Failures.Where(f => f is not ("Cash" or "Debt" or "Solvency")).ToList();
            foreach (Quota q in Quotas().Where(q => q.Name is "Cash" or "Debt"))
            { double value = q.Name == "Cash" ? Cash : Debt; if (q.AtLeast ? value < q.Target : value > q.Target) failures.Add(q.Name); }
            if (Cash < 0) failures.Add("Solvency");
            if (r.Failures.Count == 0 && failures.Count > 0) { Cleared--; Cash -= 200; }
            else if (r.Failures.Count > 0 && failures.Count == 0) { Cleared++; Cash += 200; }
            Reports[^1] = r with { Cash = Cash, Debt = Debt, Failures = failures };
        }
        return true;
    }
}

public sealed partial class GameApp
{
    Screen beforeLoans;
    int loanIndex;
    void OpenLoans()
    {
        if (Game == null || !Game.CreditPhase) { notice = "Loans are available during an active run."; return; }
        if (Game.Tutorial) { notice = "Finish practice first; loans are available in normal runs."; return; }
        Game.PrepareLoans(); beforeLoans = Screen; loanIndex = 0; Screen = Screen.Loans;
    }
    void DrawLoans(Canvas c)
    {
        Run g = Game!;
        if(g.HasActiveLoan)
        {
            Header(c,"LOAN REPAYMENT");
            c.Center(10,$"Remaining to repay: ${g.Debt:N2}",GameTheme.Statistic("debt"));
            c.Button((c.Width-22)/2,15,"ENTER PAY BACK","loanRepay",selected:g.Cash>0);
            c.Button(c.Width-17,29,"ESC BACK","loanBack");
            Footer(c,"ENTER / P: Pay back using cash | ESC: Back");
            return;
        }
        if(!g.CanBorrow)
        {
            Header(c,"LOAN COOLDOWN");
            int months=Math.Max(0,g.NextLoanMonth-g.Month);
            c.Center(11,$"Next loan in {months} month{(months==1?"":"s")}",ConsoleColor.Cyan);
            c.Center(13,$"Available in month {g.NextLoanMonth}",ConsoleColor.Gray);
            c.Button(c.Width-17,29,"ESC BACK","loanBack");
            Footer(c,"ESC: Back | Cooldown begins when the last loan is fully repaid");
            return;
        }
        Header(c, "CHOOSE A LOAN", g.LoanStatus);
        c.Center(4, "One loan at a time. Repay, then wait three months.", ConsoleColor.Cyan);
        c.Center(5, "Interest is a one-time fee, included in the total debt.");
        c.Center(6, g.Phase == Phase.Result ? $"Bridge funding needed: ${g.BridgeCost:N0}; other quotas still apply." : "Borrowing gives cash, not sales profit.");
        for (int i = 0; i < 3; i++)
        {
            LoanOffer offer = g.LoanOffers[i];
            DrawCard(c, i, offer.Name, "Future you gets the bill", [$"Cash +${offer.Amount:N0}", $"Interest {offer.InterestRate:P1}", $"Fee ${offer.Fee:N0}", $"Repay ${offer.Total:N0}", "Fixed fee; no compounding"], (Rarity)i, loanIndex == i, ConsoleColor.Cyan);
        }
        c.Button(3, 29, "ENTER BORROW", "loanTake", loanIndex, g.CanBorrow);

        c.Button(c.Width - 17, 29, "ESC BACK", "loanBack");
        Footer(c, "WASD/1-3: Select | ENTER: Borrow | P: Repay using cash | ESC: Back");
    }
}
