namespace BusinessGame;

// Run with --self-test. This uses the real economy and UI controllers without console input.
public static class Verification
{
    static int assertions;
    static void Check(bool okay, string message)
    { assertions++; if (!okay) throw new InvalidOperationException("Verification failed: " + message); }

    public static void SelfTest(List<Course> courses)
    {
        assertions = 0;
        Check(courses.Count == 6 && courses.Sum(c => c.Lessons.Count) == 60, "curriculum counts");
        var a = new Run(courses[0], 0, Difficulty.Easy, 42);
        var b = new Run(courses[0], 0, Difficulty.Easy, 42);
        Check(a.Hand.SequenceEqual(b.Hand), "seeded deals");
        Check(a.Hand.Select(c => c.Kind).Distinct().Count() == 3, "unique cards per hand");
        double cash = a.Cash; int actions = a.Actions, rerolls = a.Rerolls;
        Check(!a.Play(-1) && a.Cash == cash && a.Actions == actions, "empty selection does not play");
        Check(a.Reroll() && a.Rerolls == rerolls - 1 && a.Actions == actions, "reroll before selecting");
        while (a.Rerolls > 0) a.Reroll();
        Card[] same = a.Hand.ToArray();
        Check(!a.Reroll() && same.SequenceEqual(a.Hand), "exhausted reroll leaves hand intact");
        a.FinishDecisions();
        Check(a.Phase == Phase.Events && a.Events.Count == 3, "three random event cards");
        Check(!a.Play(0), "decision cards cannot be played in event phase");
        Check(a.SkipEvents() && a.Phase == Phase.Result, "skip all events settles exactly one month");
        Check(!a.SkipEvents() && a.Reports.Count == 1, "month cannot settle twice");
        Check(a.LastReport!.Failures.Count == 0, "first Easy quota achievable without interventions");
        a.ContinueResult();
        Check(a.Phase == Phase.Booster && a.Boosters.Count == 3, "three booster choices after a clear");
        Check(!a.NextMonth() && !a.Buy(0), "cannot bypass rewards or shop phase");
        Check(a.ClaimBooster(0) && !a.ClaimBooster(0), "only one booster per month");
        cash = a.Cash;
        Check(a.Buy(0) && a.Cash == cash - Run.Decorations[0].Cost && !a.Buy(0), "purchase and duplicate guard");
        Check(a.NextMonth() && a.Month == 2 && a.Actions == 3 && a.Rerolls == 4, "monthly progression and resource reset");

        var failing = Enumerable.Range(0, 100).Select(seed => new Run(courses[0], 9, Difficulty.Hard, seed))
            .First(r => r.Hand.Any(c => c.Kind == CardKind.SellShares));
        failing.Play(failing.Hand.FindIndex(c => c.Kind == CardKind.SellShares));
        failing.FinishDecisions(); failing.SkipEvents(); failing.ContinueResult();
        Check(failing.Phase == Phase.Lost, "a missed quota ends the run");
        Check(!failing.Reroll() && !failing.NextMonth() && !failing.Buy(0), "finished runs cannot mutate");

        var beginner = new Run(courses[1], 0, Difficulty.Normal, 2);
        var expert = new Run(courses[1], 9, Difficulty.Normal, 2);
        Check(expert.ProfitTarget > beginner.ProfitTarget && expert.Quotas().Count > beginner.Quotas().Count,
            "later topics increase complexity");
        Check(new Run(courses[0], 0, Difficulty.Hard, 2).ProfitTarget > beginner.ProfitTarget,
            "difficulty raises quotas");

        string scratch = Path.Combine(Path.GetTempPath(), "BusinessGame-check-" + Guid.NewGuid().ToString("N"));
        var store = new Storage(scratch);
        var app = new GameApp(courses, store);
        Key(app, ConsoleKey.D1); Check(app.Screen == Screen.Courses, "main menu opens courses");
        Key(app, ConsoleKey.D1); Key(app, ConsoleKey.Enter); Check(app.Screen == Screen.Topics, "course opens topics");
        Key(app, ConsoleKey.Enter); Check(app.Screen == Screen.Difficulty, "topic opens difficulty chips");
        Key(app, ConsoleKey.D1); Key(app, ConsoleKey.Enter); Check(app.Screen == Screen.Table, "chip starts game");
        Canvas initial = app.Render(80, 30);
        Check(initial.Lines()[8].Count(c => c == '+') == 0 && initial.Lines()[10].Count(c => c == '+') == 6,
            "fresh cards start without selection");
        Key(app, ConsoleKey.D2);
        Canvas raised = app.Render(80, 30);
        Check(raised.Lines()[8].Count(c => c == '+') == 2 && raised.Lines()[10].Count(c => c == '+') == 4,
            "selected card rises exactly two rows");
        Key(app, ConsoleKey.Spacebar);
        Check(app.Render(80, 30).Lines()[8].Count(c => c == '+') == 0, "deselection lowers the card");
        Key(app, ConsoleKey.R); Check(app.Game!.Rerolls == 3, "controller accepts reroll with no selection");
        Key(app, ConsoleKey.Escape); Check(app.Screen == Screen.Pause, "ESC pauses active run");
        Key(app, ConsoleKey.Escape); Check(app.Screen == Screen.Table, "ESC resumes paused run");
        Key(app, ConsoleKey.E); Key(app, ConsoleKey.E); Key(app, ConsoleKey.Enter);
        Check(app.Screen == Screen.Booster, "event skip leads through quota report to booster");
        Key(app, ConsoleKey.D1); Key(app, ConsoleKey.Enter); Check(app.Screen == Screen.Shop, "booster opens shop");
        Canvas shop = app.Render(80, 30);
        Check(shop.Lines()[11].Count(c => c == '=') == 66 && shop.Lines()[18].Count(c => c == '=') == 66,
            "shop has two shelves of three items");
        HitBox item = shop.Buttons.Single(h => h.Action == "shopSelect" && h.Index == 1);
        app.Handle(new(X: item.X + 1, Y: item.Y + 1), shop);
        Key(app, ConsoleKey.Enter); Check(app.Game.Owned.Contains(1), "mouse selects a decoration for buying");
        Key(app, ConsoleKey.S); Check(app.Screen == Screen.Reward, "S leaves shop");
        Key(app, ConsoleKey.S); Check(app.Screen == Screen.Shop, "S reopens shop between months");
        Key(app, ConsoleKey.N); Check(app.Game.Month == 2 && app.Screen == Screen.Table, "N begins next month");

        int wins = 0, attempts = 0, retries = 0;
        var failures = new List<string>();
        foreach (Course course in courses)
        foreach (int topic in Enumerable.Range(0, 10))
        foreach (Difficulty difficulty in Enum.GetValues<Difficulty>())
        {
            attempts++;
            Run campaign = AutoPlay(course, topic, difficulty, 73);
            Check(campaign.Phase is Phase.Won or Phase.Lost && campaign.Month <= 12, "campaign termination");
            Check(campaign.Reports.Count == campaign.Month, "one report per month");
            if (campaign.Phase == Phase.Won)
            { wins++; Check(campaign.Cleared == 12 && campaign.Reports.All(r => r.Failures.Count == 0), "12 quota victory"); }
            else
            {
                failures.Add($"{course.Name}/{topic + 1}/{difficulty}: month {campaign.Month} ({string.Join(',', campaign.LastReport!.Failures)})");
                bool reachable = false;
                for (int seed = 1; seed <= 20 && !reachable; seed++)
                { retries++; reachable = AutoPlay(course, topic, difficulty, seed).Phase == Phase.Won; }
                Check(reachable, "every course/topic/difficulty has a verified path to twelve clears");
            }
        }
        Console.WriteLine($"Verified {assertions} assertions; completed {attempts} automated campaigns; {wins} wins.");
        foreach (string failure in failures.Take(12)) Console.WriteLine("Balance sample: " + failure);
        Check(wins >= 150, "a sensible automated policy can win most course/topic/difficulty combinations");
        Run winner = AutoPlay(courses[0], 0, Difficulty.Easy, 73);
        Check(winner.Phase == Phase.Won && !winner.NextMonth(), "no month 13 after victory");
        store.Record(winner);
        var loaded = new Storage(scratch);
        Check(loaded.History.Count == 1 && loaded.History[0].Won, "run results round-trip through local storage");
        store.Preferences.Difficulty = Difficulty.Hard; store.SaveSettings();
        Check(new Storage(scratch).Preferences.Difficulty == Difficulty.Hard, "settings persist");
        Console.WriteLine($"All 180 combinations have a verified 12-month victory path ({retries} additional seeded attempts).");
        Console.WriteLine("SELF-TEST PASSED.");
    }
    static void Key(GameApp app, ConsoleKey key) => app.Handle(new(key), app.Render(100, 34));

    public static Run AutoPlay(Course course, int topic, Difficulty difficulty, int seed)
    {
        var run = new Run(course, topic, difficulty, seed);
        for (int guard = 0; guard < 1000 && run.Phase is not (Phase.Won or Phase.Lost); guard++)
        {
            if (run.Phase == Phase.Decisions)
            {
                Forecast f = run.Preview();
                int find(CardKind kind) => run.Hand.FindIndex(c => c.Kind == kind && run.Cash >= run.CardCost(kind));
                int card = -1;
                if (run.Inventory < f.Demand) card = find(CardKind.Restock);
                if (card < 0 && run.Capacity < f.Demand) card = find(CardKind.Factory);
                if (card < 0 && run.Price < 23) card = find(CardKind.RaisePrice);
                if (card < 0) card = find(CardKind.Efficiency);
                if (card < 0) card = find(CardKind.CourseFocus);
                if (card < 0 && f.Profit < run.ProfitTarget + 300) card = find(CardKind.Advertise);
                if (card < 0 && run.Rerolls > 0 && (run.Inventory < f.Demand || f.Profit < run.ProfitTarget + 200))
                { run.Reroll(); continue; }
                if (card < 0 || !run.Play(card)) run.FinishDecisions();
            }
            else if (run.Phase == Phase.Events)
            {
                int favorable = run.Events.FindIndex(e => e.Custom is 0 or 3 or 5 || e.Imported?.Name is "Viral trend" or "Demand shift");
                if (favorable >= 0) run.TakeEvent(favorable); else run.SkipEvents();
            }
            else if (run.Phase == Phase.Result) run.ContinueResult();
            else if (run.Phase == Phase.Booster)
            {
                int pick = run.Boosters.FindIndex(b => b.Id == 7);
                if (pick < 0) pick = run.Boosters.FindIndex(b => b.Id == 1);
                run.ClaimBooster(pick < 0 ? 0 : pick);
            }
            else if (run.Phase == Phase.Shop)
            {
                foreach (int item in new[] { 2, 1, 3, 5, 4, 0 }) if (run.Cash > Run.Decorations[item].Cost + 2200) run.Buy(item);
                run.NextMonth();
            }
        }
        return run;
    }

    public static void Preview(List<Course> courses)
    {
        string directory = Path.Combine(Environment.CurrentDirectory, "previews");
        Directory.CreateDirectory(directory);
        var app = new GameApp(courses, new Storage(Path.Combine(Path.GetTempPath(), "BusinessGame-preview-data")));
        void capture(string name)
        {
            File.WriteAllLines(Path.Combine(directory, name + ".txt"), app.Render(100, 34).Lines());
            File.WriteAllLines(Path.Combine(directory, name + "-80.txt"), app.Render(80, 30).Lines());
        }
        capture("01-main-menu"); Key(app, ConsoleKey.D1); capture("02-courses");
        Key(app, ConsoleKey.Enter); capture("03-topics"); Key(app, ConsoleKey.Enter); capture("04-difficulty");
        Key(app, ConsoleKey.D1); Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.D2); capture("05-table");
        Key(app, ConsoleKey.E); capture("06-events"); Key(app, ConsoleKey.E); capture("07-quota-result");
        Key(app, ConsoleKey.Enter); capture("08-booster"); Key(app, ConsoleKey.D1); Key(app, ConsoleKey.Enter); capture("09-shop");
        Console.WriteLine("Screen previews saved to " + directory);
    }
}
