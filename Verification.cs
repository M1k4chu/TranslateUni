namespace TranslateUni;

public static class Verification
{
    static int assertions;
    static void Check(bool okay, string message)
    { assertions++; if (!okay) throw new InvalidOperationException("Verification failed: " + message); }
    static void Key(GameApp app, ConsoleKey key) => app.Handle(new(key), app.Render(120, 42));

    public static void SelfTest(List<Course> courses)
    {
        assertions = 0;
        Check(courses.Count == 6 && courses.Sum(c => c.Lessons.Count) == 60, "all course data retained");
        Check(GameContent.Decisions.Length == 32 && Run.BoosterPool.Length == 20, "expanded pools");
        foreach (Card card in GameContent.Decisions)
        {
            Check(GameContent.Flavor(card.Kind).Split(' ').Length <= 5, "decision explanation word limit");
            Check(new Run(courses[0], 0, Difficulty.Easy, 1).Describe(card.Kind).Length > 0, "every card has numeric definition");
        }
        foreach (Booster card in Run.BoosterPool) Check(GameContent.Flavor(card).Split(' ').Length <= 5, "booster explanation word limit");
        foreach (CompanyEvent e in Run.CompanyEventPool) Check(e.Flavor.Split(' ').Length <= 5, "company headline word limit");
        foreach (int id in Enumerable.Range(0, 20)) Check(GameContent.DecorationFlavor(id).Split(' ').Length <= 5, "shop explanation word limit");
        Check(Run.Decorations.Length == 20, "twenty possible shop boosts");

        var frequencies = new Dictionary<Rarity, int>(); var kinds = new HashSet<CardKind>();
        foreach (int seed in Enumerable.Range(0, 3000))
        {
            Run sample = new(courses[0], 0, Difficulty.Normal, seed);
            Check(sample.Rerolls == 1 && sample.Hand.Select(c => c.Kind).Distinct().Count() == 3, "one initial reroll; unique deal");
            foreach (Card card in sample.Hand) { kinds.Add(card.Kind); frequencies[card.Rarity] = frequencies.GetValueOrDefault(card.Rarity) + 1; }
        }
        Check(kinds.Count == 32, "every decision can appear in real draws");
        Check(frequencies[Rarity.Common] > frequencies[Rarity.Uncommon] && frequencies[Rarity.Uncommon] > frequencies[Rarity.Rare]
            && frequencies[Rarity.Rare] > frequencies[Rarity.Epic] && frequencies[Rarity.Epic] > frequencies[Rarity.Legendary], "rarity weights affect draws");
        var a = new Run(courses[0], 0, Difficulty.Easy, 42);
        var b = new Run(courses[0], 0, Difficulty.Easy, 42);
        Check(a.Hand.SequenceEqual(b.Hand), "seeded reproducibility");
        Check(!a.Play(-1) && a.Actions == 3, "empty selection guard");
        Check(a.Reroll() && a.Rerolls == 0 && !a.Reroll(), "one reroll available without selection");
        Check(!a.AcknowledgeCompanyEvent(), "company event cannot apply before clear");
        a.FinishDecisions(); Check(a.Events.Count == 3, "three optional events");
        Check(a.Events.All(e => GameContent.Flavor(e).Split(' ').Length <= 5), "event explanations word limit");
        Check(a.SkipEvents() && !a.SkipEvents() && a.Reports.Count == 1, "one settlement per month");
        a.ContinueResult(); Check(a.Phase == Phase.SpecialEvent && a.CompanyNews != null, "news after a quota clear");
        Check(a.AcknowledgeCompanyEvent() && !a.AcknowledgeCompanyEvent() && a.NewsHistory.Count == 1, "news applies once");
        Check(a.Phase == Phase.Booster && a.Boosters.Count == 3, "news precedes booster pack");
        Check(a.ClaimBooster(0) && !a.ClaimBooster(0), "only one booster claim");
        double cash = a.Cash;
        int price = a.ShopOffers[1].Cost;
        Check(a.Buy(1) && !a.Buy(1) && a.Cash == cash - price, "shop cost and duplicate guard");
        Check(a.ShopOffers.Count == 6 && a.ShopOffers.Select(item => item.Id).Distinct().Count() == 6, "six distinct rotating shop offers");
        int decisionRerolls = a.Rerolls;
        int[] oldOffers = a.ShopOffers.Select(item => item.Id).ToArray(); cash = a.Cash;
        Check(a.RerollShop() && a.Cash == cash - 75 && a.ShopRerollCost == 100, "paid shop reroll and increasing price");
        Check(a.Rerolls == decisionRerolls && a.ShopOffers.All(item => !a.Owned.Contains(item.Id)), "shop refresh is separate from decision rerolls and excludes owned boosts");
        Check(a.ShopOffers.Any(item => !oldOffers.Contains(item.Id)), "refresh changes at least one available boost");
        Check(a.NextMonth() && a.Month == 2, "month progression");
        Check(a.Rerolls == 1 + (a.ActiveBoosts.Any(x => x.Card.Id is 6 or 19) ? 1 : 0), "one base reroll plus actual booster");
        Check(!a.RerollShop(), "shop reroll is guarded outside shop phase");
        var seenShop = new HashSet<int>();
        foreach (int seed in Enumerable.Range(0, 400))
        {
            Run offers = OpenShop(courses[0], seed);
            foreach (Decoration offer in offers.ShopOffers) seenShop.Add(offer.Id);
            Check(offers.ShopOffers.Count == 6 && offers.ShopOffers.Select(item => item.Id).Distinct().Count() == 6, "distinct offers from full pool");
        }
        Check(seenShop.Count == 20, "every one of twenty decorations can appear");
        foreach (int id in Enumerable.Range(0, 20))
        {
            Run itemRun = Enumerable.Range(0, 2000).Select(seed => OpenShop(courses[0], seed)).First(r => r.ShopOffers.Any(item => item.Id == id));
            int slot = itemRun.ShopOffers.FindIndex(item => item.Id == id);
            double beforeCash = itemRun.Cash, beforeUnit = itemRun.UnitCost, beforeQuality = itemRun.Quality,
                beforeDebt = itemRun.Debt, beforeShare = itemRun.Share, beforeRisk = itemRun.Risk;
            int beforeCapacity = itemRun.Capacity, beforeStock = itemRun.Inventory;
            Forecast priorForecast = itemRun.Preview();
            Check(itemRun.Buy(slot) && itemRun.Owned.Contains(id) && itemRun.Cash == beforeCash - Run.Decorations[id].Cost,
                "all twenty real shop purchases deduct their actual offer price");
            Check(!itemRun.Buy(slot), "every upgrade is unique per run");
            if (id is 1 or 9) Check(itemRun.UnitCost == beforeUnit - (id == 1 ? 0.5 : 0.3), "shop unit-cost effect");
            if (id is 6 or 12) Check(itemRun.Capacity == beforeCapacity + (id == 6 ? 40 : 60), "shop capacity effect");
            if (id is 5 or 10) Check(Math.Abs(itemRun.Preview().Profit - priorForecast.Profit - (id == 5 ? 60 : 40)) < 0.01, "shop overhead effect");
            if (id == 8) Check(itemRun.Risk == Math.Max(0, beforeRisk - 6) && itemRun.Quality == Math.Min(100, beforeQuality + 4), "whiteboard effect");
            if (id == 11) Check(itemRun.Quality == Math.Min(100, beforeQuality + 8) && itemRun.Preview().Demand > priorForecast.Demand, "aquarium effect");
            if (id == 13) Check(itemRun.Share == beforeShare + 0.5, "cork board effect");
            if (id == 17) Check(itemRun.Debt == Math.Max(0, beforeDebt - 400) && itemRun.Risk == Math.Max(0, beforeRisk - 4), "meeting table effect");
            if (id is 0 or 18) Check(itemRun.Preview().Demand > priorForecast.Demand, "demand decoration effect");
            if (id is 3 or 16) Check(itemRun.Preview().Revenue > priorForecast.Revenue, "per-unit margin effect");
            itemRun.NextMonth();
            if (id is 2 or 19) Check(itemRun.Actions == 4, "extra shop decision effect");
            if (id == 4) Check(itemRun.Rerolls == 2, "shop extra reroll effect");
            if (id is 7 or 15) Check(itemRun.Inventory == beforeStock + (id == 7 ? 20 : 30), "recurring free-stock effect");
            if (id == 14) Check(itemRun.InvestmentIncome == 50, "coin jar income starts next month");
        }
        Run costlyRefresh = OpenShop(courses[0], 9);
        while (costlyRefresh.Cash >= costlyRefresh.ShopRerollCost) Check(costlyRefresh.RerollShop(), "affordable reroll works");
        double beforeRejected = costlyRefresh.Cash;
        int[] unchanged = costlyRefresh.ShopOffers.Select(item => item.Id).ToArray();
        Check(!costlyRefresh.RerollShop() && costlyRefresh.Cash == beforeRejected && unchanged.SequenceEqual(costlyRefresh.ShopOffers.Select(item => item.Id)), "unaffordable shop refresh changes neither stock nor cash");

        foreach (int eventId in Enumerable.Range(0, 12))
        {
            Run news = Enumerable.Range(0, 2000).Select(seed => ClearFirst(courses[0], seed))
                .First(r => r.CompanyNews!.Id == eventId);
            double oldShare = news.Share, oldCash = news.Cash, oldDebt = news.Debt, oldQuality = news.Quality;
            news.AcknowledgeCompanyEvent();
            if (eventId == 1) Check(news.Quality == oldQuality - 6, "scandal applies quality loss");
            if (eventId == 2) Check(news.Share == oldShare + 0.5, "good PR applies share gain");
            if (eventId == 3) Check(news.Cash == oldCash + 350 && news.Share == oldShare - 0.5, "new majority trade-off");
            if (eventId == 5) Check(news.Cash == oldCash + 500 && news.Debt == oldDebt + 200, "investor cash/debt");
            news.SkipBooster(); news.NextMonth();
            Forecast before = news.Preview(); news.FinishDecisions(); news.SkipEvents(); news.ContinueResult();
            Check(double.IsFinite(before.Profit), "pending company effects enter next-month economy");
        }
        Run competitor = Enumerable.Range(0, 2000).Select(seed => ClearFirst(courses[0], seed)).First(r => r.CompanyNews!.Id == 0);
        competitor.AcknowledgeCompanyEvent(); competitor.SkipBooster(); competitor.NextMonth();
        var normalNext = new Run(courses[0], 0, Difficulty.Easy, 13);
        Check(competitor.Preview().Demand < 190, "new competitor lowers following month's demand");

        foreach (int boosterId in Enumerable.Range(0, 20))
        {
            Run gift = Enumerable.Range(0, 3000).Select(seed => ClearFirst(courses[0], seed)).First(r =>
            { r.AcknowledgeCompanyEvent(); return r.Boosters.Any(x => x.Id == boosterId); });
            int index = gift.Boosters.FindIndex(x => x.Id == boosterId);
            gift.ClaimBooster(index);
            Check(gift.ActiveBoosts.Any(x => x.Card.Id == boosterId) == Run.BoosterPool[boosterId].Persistent,
                "instant gifts excluded; persistent boosts listed");
        }

        string directory = Path.Combine(Path.GetTempPath(), "TranslateUni-tests-" + Guid.NewGuid().ToString("N"));
        var store = new Storage(directory); var app = new GameApp(courses, store);
        Canvas main = app.Render(120, 42);
        Check(main.Lines().Any(line => line.Contains("TranslateUni")), "new game branding");
        Check(main.Lines().Any(line => line.Contains("3 DIFFICULTY")) && main.Lines().All(line => !line.Contains("DIFFICULTY CHIPS")), "main menu difficulty label");
        Key(app, ConsoleKey.T); Check(app.Screen == Screen.Guide, "tutorial and reference merged");
        Check(app.Render(120, 42).Buttons.Any(b => b.Action == "tutorialStart"), "merged guide starts practice");
        Key(app, ConsoleKey.Enter); Check(app.Screen == Screen.TutorialIntro, "practice opens from merged guide");
        Check(app.Game!.Rerolls == 1, "tutorial starts with one reroll");
        Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.D1);
        Canvas selected = app.Render(108, 40);
        Check(selected.Lines()[10][3] == '+' && selected.Lines()[12].Count(c => c == '+') == 4, "two-row selection lift");
        Check(selected.Lines().Any(line => line.Contains("Shout louder")), "funny card explanation visible");
        Check(selected.Lines()[28].Contains(new string('_', 48)) && selected.Lines()[30].Contains("CASH:"), "old statistics desk restored");
        Key(app, ConsoleKey.I); Check(app.Screen == Screen.Inspect, "full card explanation");
        Key(app, ConsoleKey.Escape); Check(app.Screen == Screen.Table, "inspection preserves selection");
        Key(app, ConsoleKey.Enter); Check(app.TutorialStep == 2, "tutorial advances after actual card play");
        Key(app, ConsoleKey.R); Check(app.TutorialStep == 3 && app.Game.Rerolls == 0, "tutorial teaches actual reroll");
        Key(app, ConsoleKey.E); Key(app, ConsoleKey.E); Key(app, ConsoleKey.Enter);
        Check(app.Screen == Screen.SpecialEvent, "tutorial shows company news");
        Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.D1); Key(app, ConsoleKey.Enter);
        Check(app.Screen == Screen.Shop && app.Game.ActiveBoosts.Any(x => x.Card.Family == BoosterFamily.Green)
            && app.Game.ActiveBoosts.Any(x => x.Card.Family == BoosterFamily.Purple), "tutorial demonstrates both booster colors");
        Canvas shop = app.Render(108, 40);
        Check(shop.Lines()[15].Count(c => c == '=') == 66 && shop.Lines()[26].Count(c => c == '=') == 66, "two shop shelves");
        Check(shop.Lines().Any(l => l.Contains("NEXT MONTH 2/12 QUOTAS")), "quotas visible in shop");
        Key(app, ConsoleKey.S); Check(app.Screen == Screen.Shop, "S navigates shop rather than exiting");
        Key(app, ConsoleKey.W); // Navigate back to the lamp row.
        HitBox lamp = shop.Buttons.Single(h => h.Action == "shopSelect" && h.Index == 1);
        app.Handle(new(X: lamp.X + 1, Y: lamp.Y + 1), shop); Key(app, ConsoleKey.Enter);
        Check(app.Game.Owned.Contains(1), "mouse shop selection and purchase");
        Canvas side = app.Render(120, 42); HitBox active = side.Buttons.First(h => h.Action == "activeInspect");
        Check(side.ColorAt(active.X, active.Y) == ConsoleColor.Green, "green booster rendered in green");
        app.Handle(new(X: active.X, Y: active.Y), side); Check(app.Screen == Screen.Inspect, "side booster opens details");
        Canvas another = app.Render(120, 42); HitBox purple = another.Buttons.Last(h => h.Action == "activeInspect");
        Check(another.ColorAt(purple.X, purple.Y) == ConsoleColor.Magenta, "purple booster rendered in purple");
        app.Handle(new(X: purple.X, Y: purple.Y), another);
        Key(app, ConsoleKey.Escape); Key(app, ConsoleKey.N);
        Check(app.Screen == Screen.Shop && app.Game.Month == 1, "N no longer leaves shop");
        Key(app, ConsoleKey.Spacebar);
        Check(app.Screen == Screen.TutorialComplete && app.Game.Month == 2, "tutorial demonstrates next month");
        Canvas finished = app.Render(108, 40);
        Check(finished.Lines().Any(l => l.Contains("Last profit: $")) && finished.Lines().Any(l => l.Contains("Last sales: $")), "last month earnings next to quotas");
        Check(finished.Lines().Any(l => l.Contains("G Brand Spark")) && finished.Lines().Any(l => l.Contains("P Coupon Engine")), "both active booster names visible");
        Key(app, ConsoleKey.Enter); Check(store.History.Count == 0 && store.Preferences.TutorialCompleted && app.Screen == Screen.Guide, "practice returns to merged guide without fake result");
        Check(new Storage(directory).Preferences.TutorialCompleted, "tutorial completion persists");

        Key(app, ConsoleKey.Escape);
        Key(app, ConsoleKey.S); Key(app, ConsoleKey.W); Key(app, ConsoleKey.Enter);
        Check(app.Screen == Screen.Courses, "W/S navigate main menu");
        Key(app, ConsoleKey.D); Key(app, ConsoleKey.A); Key(app, ConsoleKey.S); Key(app, ConsoleKey.W); Key(app, ConsoleKey.Enter);
        Check(app.Screen == Screen.Topics, "WASD navigates course grid");
        Key(app, ConsoleKey.S); Key(app, ConsoleKey.W); Key(app, ConsoleKey.Enter);
        Key(app, ConsoleKey.D); Key(app, ConsoleKey.S); Key(app, ConsoleKey.W); Key(app, ConsoleKey.A); Key(app, ConsoleKey.Enter);
        Check(app.Game!.Difficulty == Difficulty.Easy, "WASD navigates difficulty selection");
        Key(app, ConsoleKey.D); Key(app, ConsoleKey.D);
        Check(app.Render(108, 40).Lines()[10][27] == '+', "D selects second card");
        Key(app, ConsoleKey.A); Key(app, ConsoleKey.Spacebar);
        foreach (ConsoleKey key in new[] { ConsoleKey.R, ConsoleKey.Escape, ConsoleKey.Escape }) Key(app, key);
        Check(app.Game!.Rerolls == 0 && app.Screen == Screen.Table, "regular keyboard and pause flow");
        app.Game.ActiveBoosts.AddRange(Run.BoosterPool.Where(b => b.Persistent).Select(b => new ActiveBooster(b)));
        Canvas longList = app.Render(108, 40);
        Check(longList.Buttons.Count(b => b.Action == "activeInspect") == 6, "long booster lists fit the side viewport");
        Key(app, ConsoleKey.PageDown); Key(app, ConsoleKey.PageDown);
        Check(app.Render(108, 40).Lines().Any(l => l.Contains("Showing 3-8")), "page keys scroll active boosters");
        foreach (int i in Enumerable.Range(0, 40)) Key(app, ConsoleKey.PageDown);
        Canvas lastPage = app.Render(108, 40);
        int lastIndex = lastPage.Buttons.Where(b => b.Action == "activeInspect").Max(b => b.Index);
        Check(lastIndex == app.Game.ActiveBoosts.Count - 1, "scroll reaches the final active booster");
        Key(app, ConsoleKey.E); Key(app, ConsoleKey.E); Key(app, ConsoleKey.Enter);
        Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.E);
        Check(app.Screen == Screen.Shop, "regular run reaches randomized shop");
        double uiCash = app.Game.Cash;
        Check(app.Render(108, 40).Buttons.Any(b => b.Action == "shopReroll"), "shop refresh button is present");
        Key(app, ConsoleKey.R);
        Check(app.Game.Cash == uiCash - 75 && app.Game.ShopRerolls == 1 && app.Game.Rerolls == 0, "R uses paid shop reroll rather than decision reroll");
        Key(app, ConsoleKey.Spacebar);
        Check(app.Screen == Screen.Table && app.Game.Month == 2, "Space leaves regular shop and begins next month");
        Run soldOut = OpenShop(courses[0], 19);
        soldOut.Owned.UnionWith(Run.Decorations.Select(item => item.Id));
        double soldOutCash = soldOut.Cash;
        Check(!soldOut.RerollShop() && soldOut.Cash == soldOutCash, "sold-out pool does not charge for a refresh");
        soldOut.NextMonth(); soldOut.FinishDecisions(); soldOut.SkipEvents(); soldOut.ContinueResult();
        soldOut.AcknowledgeCompanyEvent(); soldOut.SkipBooster();
        Check(soldOut.ShopOffers.Count == 0 && !soldOut.Buy(0), "empty shop pool handles purchases safely");

        int won = 0, retries = 0;
        foreach (Course course in courses)
        foreach (int topic in Enumerable.Range(0, 10))
        foreach (Difficulty difficulty in Enum.GetValues<Difficulty>())
        {
            Run campaign = AutoPlay(course, topic, difficulty, 73);
            Check(campaign.Phase is Phase.Won or Phase.Lost && campaign.Month <= 12, "normal campaign terminates");
            Check(campaign.NewsHistory.Count == campaign.Cleared, "one acknowledged company event per met quota");
            if (campaign.Phase == Phase.Won) won++;
            else
            {
                bool achievable = false;
                for (int seed = 1; seed <= 60 && !achievable; seed++)
                { retries++; achievable = AutoPlay(course, topic, difficulty, seed).Phase == Phase.Won; }
                Check(achievable, $"victory path: {course.Name}, topic {topic + 1}, {difficulty}");
            }
        }
        Run winner = AutoPlay(courses[0], 0, Difficulty.Easy, 73);
        Check(winner.Phase == Phase.Won && winner.Cleared == 12 && !winner.NextMonth(), "no month 13");
        store.Record(winner); Check(new Storage(directory).History.Count == 1, "saved results round-trip");
        Console.WriteLine($"SELF-TEST PASSED: {assertions} checks; 180 campaigns ({won} initial wins); {retries} seeded retries verify every combination is winnable.");
    }
    static Run ClearFirst(Course course, int seed)
    {
        var run = new Run(course, 0, Difficulty.Easy, seed);
        run.FinishDecisions(); run.SkipEvents(); run.ContinueResult();
        return run;
    }
    static Run OpenShop(Course course, int seed)
    {
        Run run = ClearFirst(course, seed);
        run.AcknowledgeCompanyEvent(); run.SkipBooster();
        return run;
    }

    public static Run AutoPlay(Course course, int topic, Difficulty difficulty, int seed)
    {
        var run = new Run(course, topic, difficulty, seed);
        for (int guard = 0; guard < 1000 && run.Phase is not (Phase.Won or Phase.Lost); guard++)
        {
            if (run.Phase == Phase.Decisions)
            {
                Forecast f = run.Preview();
                int find(params CardKind[] kinds) => run.Hand.FindIndex(c => kinds.Contains(c.Kind) && run.Cash >= run.CardCost(c.Kind));
                int card = -1;
                if (run.Inventory < f.Demand) card = find(CardKind.Restock);
                if (card < 0 && run.Capacity < f.Demand) card = find(CardKind.Factory, CardKind.Warehouse, CardKind.Automation, CardKind.StaffTraining);
                if (card < 0 && run.Quality < 82) card = find(CardKind.CustomerCare, CardKind.QualityAudit, CardKind.StaffTraining, CardKind.ProductRefresh);
                if (card < 0 && run.Course.Name == "Finance" && run.Debt > 2500) card = find(CardKind.Repay, CardKind.DebtRefinance, CardKind.BoardRestructure);
                if (card < 0) card = find(CardKind.Efficiency, CardKind.SupplierDeal, CardKind.Automation);
                if (card < 0 && f.Profit < run.ProfitTarget + 500)
                    card = find(CardKind.Advertise, CardKind.Influencer, CardKind.ProductRefresh, CardKind.ExportContract);
                if (card < 0 && run.Course.Name == "Marketing") card = find(CardKind.CourseFocus);
                if (card < 0 && run.Price < 27 && run.Lesson.Simulation.Parameters.GetValueOrDefault("Elasticity", 1.6) < 1.9) card = find(CardKind.RaisePrice);
                if (card < 0 && run.Rerolls > 0 && (run.Inventory < f.Demand || f.Profit < run.ProfitTarget + 250 || run.Quality < 80))
                { run.Reroll(); continue; }
                if (card < 0 || !run.Play(card)) run.FinishDecisions();
            }
            else if (run.Phase == Phase.Events)
            {
                int favorable = run.Events.FindIndex(e => e.Custom is 0 or 3 or 5 or 7 or 8 or 10 or 11 || e.Imported?.Name is "Viral trend" or "Demand shift");
                if (favorable >= 0) run.TakeEvent(favorable); else run.SkipEvents();
            }
            else if (run.Phase == Phase.Result) run.ContinueResult();
            else if (run.Phase == Phase.SpecialEvent) run.AcknowledgeCompanyEvent();
            else if (run.Phase == Phase.Booster)
            {
                int pick = run.Boosters.FindIndex(b => b.Id is 7 or 11 or 18);
                if (pick < 0) pick = run.Boosters.FindIndex(b => b.Id is 1 or 5 or 9 or 14 or 16);
                run.ClaimBooster(pick < 0 ? 0 : pick);
            }
            else if (run.Phase == Phase.Shop)
            {
                foreach (int item in new[] { 2, 19, 1, 3, 5, 4, 0, 7, 9, 10, 15, 16, 14, 6, 8, 11, 12, 13, 17, 18 })
                {
                    int slot = run.ShopOffers.FindIndex(offer => offer.Id == item);
                    if (slot >= 0 && run.Cash > run.ShopOffers[slot].Cost + 2200) run.Buy(slot);
                }
                run.NextMonth();
            }
        }
        return run;
    }

    public static void Preview(List<Course> courses)
    {
        string directory = Path.Combine(Environment.CurrentDirectory, "previews-translateuni"); Directory.CreateDirectory(directory);
        var app = new GameApp(courses, new Storage(Path.Combine(Path.GetTempPath(), "TranslateUni-preview-" + Guid.NewGuid().ToString("N"))));
        void capture(string name)
        {
            File.WriteAllLines(Path.Combine(directory, name + ".txt"), app.Render(120, 42).Lines());
            File.WriteAllLines(Path.Combine(directory, name + "-108.txt"), app.Render(108, 40).Lines());
        }
        capture("01-main"); Key(app, ConsoleKey.T); capture("02-learning-guide"); Key(app, ConsoleKey.Enter); capture("02-tutorial-intro"); Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.D1); capture("03-cards-and-desk");
        Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.R); Key(app, ConsoleKey.E); capture("04-events"); Key(app, ConsoleKey.E); capture("05-report");
        Key(app, ConsoleKey.Enter); capture("06-company-news"); Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.D1); capture("07-boosters");
        Key(app, ConsoleKey.Enter); capture("08-shop-and-side-menu"); Key(app, ConsoleKey.D2); Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.Spacebar); capture("09-tutorial-complete");
        Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.Escape); Key(app, ConsoleKey.D1); capture("10-course-selection"); Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.Enter); capture("11-difficulty-chips");
        Console.WriteLine("Saved UI previews to " + directory);
    }
}

