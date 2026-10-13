namespace TranslateUni;

public static partial class Verification
{
    static int assertions;
    static void Check(bool okay, string message)
    { assertions++; if (!okay) throw new InvalidOperationException("Verification failed: " + message); }
    static void Key(GameApp app, ConsoleKey key) {
        app.Handle(new(key), app.Render(120, 42));
        if (app.Game != null)
        {
            string[] lines = app.Render(120, 42).Lines();
            foreach (HitBox control in app.Render(120,42).Buttons.Where(b => b.Action is "tutorialBegin" or "tutorialFinish" or "inspectBack" or "buy" or "next" or "play" or "continue"))
                Check(lines[control.Y][control.X] == '[', "statistics do not hide controls on " + app.Screen);
            Check(lines[39].Contains("CASH") && lines[39].Contains("DEBT") && lines[39].Contains("STOCK"), "persistent statistics bars on " + app.Screen);
        }
    }

    public static void SelfTest(List<Course> courses)
    {
        assertions = 0;
        CheckLoansAndStatistics(courses);
        CheckInputAndTheme(courses);
        CheckStatementsAndMotion(courses);
        CheckLatestFixes(courses);
        CheckMultiplayer(courses);
        CheckVotingRefinements(courses);
        CheckNoVote(courses); CheckNeutralVotes(courses);
        CheckOnlineAndFullscreen();
        Check(courses.Count == 6 && courses.Sum(c => c.Lessons.Count) == 60, "all course data retained");
        Check(GameContent.Decisions.Length == 31 && Run.BoosterPool.Length == 20, "expanded pools");
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
        Check(kinds.Count == GameContent.Decisions.Count(card=>card.Kind!=CardKind.Repay), "every eligible decision can appear in real draws");
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
        int purchasedId = a.ShopOffers[1].Id;
        Check(a.Buy(1) && a.Cash == cash - price && a.ShopTier(purchasedId) == 1 && a.ShopOffers[1].Tier == 2,
            "shop purchase reveals the next upgrade tier");
        Check(a.ShopOffers.Count == 6 && a.ShopOffers.Select(item => item.Id).Distinct().Count() == 6, "six distinct rotating shop offers");
        int decisionRerolls = a.Rerolls;
        int[] oldOffers = a.ShopOffers.Select(item => item.Id).ToArray(); cash = a.Cash;
        Check(a.RerollShop() && a.Cash == cash - 75 && a.ShopRerollCost == 100, "paid shop reroll and increasing price");
        Check(a.Rerolls == decisionRerolls && a.ShopOffers.All(item => item.Tier == a.ShopTier(item.Id) + 1), "shop refresh offers only the next eligible tier");
        Check(a.ShopOffers.Any(item => !oldOffers.Contains(item.Id)), "refresh changes at least one available boost");
        Check(a.NextMonth() && a.Month == 2, "month progression");
        Check(a.Rerolls == 1 + a.ActiveBoosts.Where(x => x.Card.Id is 6 or 19).Sum(x => x.Stacks)
            + ShopUpgrades.BonusRerolls(4, a.ShopTier(4), a.Month), "one base reroll plus booster and periodic shop effects");
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
            Check(itemRun.ShopOffers[slot].Tier == 2, "tier-one purchase becomes an upgrade offer");
            ShopEffect effect = ShopUpgrades.Effect(id, 1);
            if (id is 1 or 9) Check(Math.Abs(itemRun.UnitCost - beforeUnit + effect.UnitCostSaving) < .001, "weaker unit-cost effect");
            if (id is 6 or 12 or 19) Check(itemRun.Capacity == beforeCapacity + effect.Capacity, "weaker capacity effect");
            if (id is 5 or 10) Check(Math.Abs(itemRun.Preview().Profit - priorForecast.Profit - effect.OverheadSaving) < 0.01, "weaker overhead effect");
            if (id is 8 or 11) Check(itemRun.Quality == Math.Min(100, beforeQuality + effect.Quality), "weaker quality effect");
            if (id == 13) Check(itemRun.Share == beforeShare + effect.Share, "weaker share effect");
            if (id == 17) Check(itemRun.Debt == Math.Max(0, beforeDebt - effect.DebtReduction) && itemRun.Risk == Math.Max(0, beforeRisk - effect.RiskReduction), "weaker debt and risk effect");
            if (id is 0 or 18) Check(itemRun.Preview().Demand > priorForecast.Demand, "demand decoration effect");
            if (id is 3 or 16) Check(itemRun.Preview().Revenue > priorForecast.Revenue, "per-unit margin effect");
            itemRun.NextMonth();
            if (id is 2 or 19) Check(itemRun.Actions == 3 + ShopUpgrades.BonusDecisions(id, 1, 2), "weak periodic shop decision effect");
            if (id == 4) Check(itemRun.Rerolls == 1 + ShopUpgrades.BonusRerolls(4, 1, 2), "weak periodic shop reroll effect");
            if (id is 7 or 15) Check(itemRun.Inventory == beforeStock + effect.StockPerMonth, "weaker recurring free-stock effect");
            if (id == 14) Check(itemRun.InvestmentIncome == effect.IncomePerMonth, "weaker income starts next month");
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
            if (eventId == 5) Check(news.Cash == oldCash + 500 && news.Debt == oldDebt && news.Share == oldShare - 0.3, "investor equity does not bypass loan limits");
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
        Canvas selected = app.Render(108, 42);
        Check(selected.Lines()[10][3] == '+' && selected.Lines()[12].Count(c => c == '+') == 4, "two-row selection lift");
        Check(selected.Lines().Any(line => line.Contains("Shout louder")), "funny card explanation visible");
        Check(selected.Lines()[35].Substring(0, 78).Trim().Length == 0 && selected.Lines()[39].Contains("CASH"), "statistics have a blank row above the bars");
        foreach (Canvas source in new[] { selected, app.Render(200, 60) })
        {
            List<AnimationFrame> frames = CardAnimation.Build(source);
            Check(frames.Count == 22 && frames.Sum(f => f.Delay) == 498, "discard animation is faster");
            Check(frames.Take(12).All(f => f.Stage == AnimationStage.Horizontal) && frames[11].CardWidth == 5,
                "horizontal shrink ends at five columns");
            Check(frames.Skip(12).Take(5).All(f => f.Stage == AnimationStage.Vertical) && frames[16].CardHeight == 1,
                "vertical shrink ends at one row");
            Check(frames.Skip(17).All(f => f.Stage == AnimationStage.Star), "star burst follows vertical collapse");
            HitBox card = source.Buttons.First(b => b.Action == "select");
            Check(frames[17].Canvas.Lines()[card.Y + card.Height / 2][card.X + card.Width / 2] == '*', "star appears at card center");
            Check(frames[^1].Canvas.Lines().Skip(card.Y).Take(card.Height).All(line => line.Substring(card.X, card.Width).Trim().Length == 0),
                "animation clears all traces of the card");
            Check(frames.All(f => f.Canvas.Lines()[1] == source.Lines()[1]), "animation preserves the rest of the screen");
            foreach (HitBox chosen in source.Buttons.Where(b => b.Action == "select"))
            {
                List<AnimationFrame> suction = CardAnimation.Build(source, chosen.Index);
                HitBox[] cards = source.Buttons.Where(b => b.Action == "select").ToArray();
                int centeredX = (cards.Min(b => b.X) + cards.Max(b => b.X + b.Width) - chosen.Width) / 2;
                int dotY = chosen.Y - 2, center = centeredX + chosen.Width / 2;
                Check(suction.Count == 52 && suction.Take(22).All(f => f.Stage != AnimationStage.MoveToCenter), "selected card waits for all discards");
                Check(suction.Skip(22).Sum(f => f.Delay) == 1250 && suction.Skip(22).Take(8).All(f => f.Stage == AnimationStage.MoveToCenter), "selected animation plays twenty percent slower");
                for (int row = 0; row < chosen.Height; row++)
                    Check(suction[29].Canvas.Lines()[chosen.Y + row].Substring(centeredX, chosen.Width) == source.Lines()[chosen.Y + row].Substring(chosen.X, chosen.Width), "centered card retains original text while waiting");
                Check(suction[30].Stage == AnimationStage.Suction && suction[46].Canvas.Lines()[dotY][center] == '.', "curving begins after moving and ends at the lowered dot");
                Check(suction[47].Canvas.Lines()[dotY][center] == '*', "selected star burst occurs above the middle");
                Check(suction[47].Canvas.Lines()[chosen.Y + chosen.Height / 2][center] == ' ', "selected card does not collapse at its old center");
                Check(suction[^1].Canvas.Lines().Skip(chosen.Y).Take(chosen.Height).All(line => line.Substring(chosen.X, chosen.Width).Trim().Length == 0), "suction leaves no card ghost at original position");
                Check(suction[^1].Canvas.Lines().Skip(chosen.Y).Take(chosen.Height).All(line => line.Substring(centeredX, chosen.Width).Trim().Length == 0), "suction leaves no card ghost in middle");
                HitBox other = cards.First(b => b.Index != chosen.Index);
                Check(suction[17].Canvas.Lines()[other.Y + other.Height / 2][other.X + other.Width / 2] == '*' || other.X == centeredX, "unchosen cards collapse before selected moves");
                Check(suction.All(f => f.Areas.Any(a => a.Y <= dotY - 1 && a.X <= centeredX && a.X + a.Width >= centeredX + chosen.Width)), "redraw includes slide and centered flight path");
                Check(suction.All(f => f.Canvas.Lines()[source.Height > 46 ? 46 : 39] == source.Lines()[source.Height > 46 ? 46 : 39]), "statistics stay visible throughout animation");
                for (int row = 0; row < chosen.Height; row++)
                    Check(suction[21].Canvas.Lines()[chosen.Y + row].Substring(chosen.X, chosen.Width) == source.Lines()[chosen.Y + row].Substring(chosen.X, chosen.Width), "selected card waits intact at original position");
            }
        }
        Key(app, ConsoleKey.I); Check(app.Screen == Screen.Inspect, "full card explanation");
        Key(app, ConsoleKey.Escape); Check(app.Screen == Screen.Table, "inspection preserves selection");
        Key(app, ConsoleKey.Enter); Check(app.TutorialStep == 2, "tutorial advances after actual card play");
        Check(app.TakeAnimationFrames().Count == 52 && app.TakeAnimationFrames().Count == 0, "successful card action queues one animation");
        Key(app, ConsoleKey.R); Check(app.TutorialStep == 3 && app.Game.Rerolls == 0, "tutorial teaches actual reroll");
        Key(app, ConsoleKey.E); Key(app, ConsoleKey.E); Key(app, ConsoleKey.Enter);
        Check(app.Screen == Screen.SpecialEvent, "tutorial shows company news");
        Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.D1); Key(app, ConsoleKey.Enter);
        Check(app.Screen == Screen.Shop && app.Game.ActiveBoosts.Any(x => x.Card.Family == BoosterFamily.Green)
            && app.Game.ActiveBoosts.Any(x => x.Card.Family == BoosterFamily.Purple), "tutorial demonstrates both booster colors");
        Canvas shop = app.Render(108, 42);
        Check(shop.Lines()[13].Count(c => c == '+') == 6 && shop.Lines()[25].Count(c => c == '+') == 6, "two shop shelves");
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
        Canvas finished = app.Render(108, 42);
        Check(finished.Lines().Any(l => l.Contains("Last profit: $")) && finished.Lines().Any(l => l.Contains("Last sales: $")), "last month earnings next to quotas");
        Check(finished.Lines().Any(l => l.Contains("G Brand Spark")) && finished.Lines().Any(l => l.Contains("P Coupon Engine")), "both active booster names visible");
        Key(app, ConsoleKey.Enter); Check(store.History.Count == 0 && store.Preferences.TutorialCompleted && app.Screen == Screen.Guide, "practice returns to merged guide without fake result");
        Check(new Storage(directory).Preferences.TutorialCompleted, "tutorial completion persists");

        Key(app, ConsoleKey.Escape);
        Key(app, ConsoleKey.S); Key(app, ConsoleKey.W); Key(app, ConsoleKey.Enter);
        Check(app.Screen == Screen.Modes, "W/S navigate main menu to play modes");
        Key(app,ConsoleKey.Enter);Key(app,ConsoleKey.Enter);
        Check(app.Screen == Screen.Courses, "singleplayer profile continues to courses");
        Key(app, ConsoleKey.D); Key(app, ConsoleKey.A); Key(app, ConsoleKey.S); Key(app, ConsoleKey.W); Key(app, ConsoleKey.Enter);
        Check(app.Screen == Screen.Topics, "WASD navigates course grid");
        Key(app, ConsoleKey.S); Key(app, ConsoleKey.W); Key(app, ConsoleKey.Enter);
        Key(app, ConsoleKey.D); Key(app, ConsoleKey.S); Key(app, ConsoleKey.W); Key(app, ConsoleKey.A); Key(app, ConsoleKey.Enter);
        Check(app.Game!.Difficulty == Difficulty.Easy, "WASD navigates difficulty selection");
        Key(app, ConsoleKey.D); Key(app, ConsoleKey.D);
        Check(app.Render(108, 42).Lines()[10][27] == '+', "D selects second card");
        Key(app, ConsoleKey.A); Key(app, ConsoleKey.Spacebar);
        foreach (ConsoleKey key in new[] { ConsoleKey.R, ConsoleKey.Escape, ConsoleKey.Escape }) Key(app, key);
        Check(app.Game!.Rerolls == 0 && app.Screen == Screen.Table, "regular keyboard and pause flow");
        app.Game.ActiveBoosts.AddRange(Run.BoosterPool.Where(b => b.Persistent).Select(b => new ActiveBooster(b)));
        Canvas longList = app.Render(108, 42);
        Check(longList.Buttons.Count(b => b.Action == "activeInspect") == 6, "long booster lists fit the side viewport");
        Key(app, ConsoleKey.PageDown); Key(app, ConsoleKey.PageDown);
        Check(app.Render(108, 42).Lines().Any(l => l.Contains("Showing 3-8")), "page keys scroll active boosters");
        foreach (int i in Enumerable.Range(0, 40)) Key(app, ConsoleKey.PageDown);
        Canvas lastPage = app.Render(108, 42);
        int lastIndex = lastPage.Buttons.Where(b => b.Action == "activeInspect").Max(b => b.Index);
        Check(lastIndex == app.Game.ActiveBoosts.Count - 1, "scroll reaches the final active booster");
        Key(app, ConsoleKey.E); Key(app, ConsoleKey.E); Key(app, ConsoleKey.Enter);
        Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.E);
        Check(app.Screen == Screen.Shop, "regular run reaches randomized shop");
        double uiCash = app.Game.Cash;
        Check(app.Render(108, 42).Buttons.Any(b => b.Action == "shopReroll"), "shop refresh button is present");
        Key(app, ConsoleKey.R);
        Check(app.Game.Cash == uiCash - 75 && app.Game.ShopRerolls == 1 && app.Game.Rerolls == 0, "R uses paid shop reroll rather than decision reroll");
        Key(app, ConsoleKey.Spacebar);
        Check(app.Screen == Screen.Table && app.Game.Month == 2, "Space leaves regular shop and begins next month");
        Run soldOut = OpenShop(courses[0], 19);
        soldOut.Owned.UnionWith(Run.Decorations.Select(item => item.Id));
        foreach (int id in Enumerable.Range(0, 20)) soldOut.ShopTiers[id] = 3;
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
                Check(achievable, $"victory path: {course.Name}, topic {topic + 1}, {difficulty}; sample month {campaign.Month}, misses {string.Join(',', campaign.LastReport!.Failures)}, profit {campaign.LastReport.Profit:0}, target {campaign.ProfitTarget:0}");
            }
        }
        Run winner = AutoPlay(courses[0], 0, Difficulty.Easy, 73);
        Check(winner.Phase == Phase.Won && winner.Cleared == 12 && !winner.NextMonth(), "no month 13");
        store.Record(winner); Check(new Storage(directory).History.Count == 1, "saved results round-trip");
        Check(!new Run(courses[0], 0, Difficulty.Easy, 1).EnterEndless(), "endless is locked before twelve clears");
        foreach (int id in Enumerable.Range(0, 20))
        {
            Run upgrade = Enumerable.Range(0, 2000).Select(seed => OpenShop(courses[0], seed)).First(r => r.ShopOffers.Any(item => item.Id == id));
            int slot = upgrade.ShopOffers.FindIndex(item => item.Id == id);
            double startingCost = upgrade.UnitCost; int startingCapacity = upgrade.Capacity;
            for (int tier = 1; tier <= 3; tier++)
            {
                double funds = upgrade.Cash; Decoration offer = upgrade.ShopOffers[slot];
                Check(offer.Tier == tier && upgrade.Buy(slot) && upgrade.Cash == funds - offer.Cost && upgrade.ShopTier(id) == tier,
                    "sequential normal upgrades deduct one tier price");
            }
            Check(!upgrade.Buy(slot) && upgrade.MaxShopTier == 3, "normal tier-three cap");
            ShopEffect gold = ShopUpgrades.Effect(id, 3);
            Check(Math.Abs(upgrade.UnitCost - Math.Max(3, startingCost - gold.UnitCostSaving)) < .001
                && upgrade.Capacity == startingCapacity + gold.Capacity, "gold effect replaces earlier tiers rather than stacking totals");
            for (int tier = 2; tier <= 5; tier++)
                Check(ShopUpgrades.Offer(id, tier).Cost > ShopUpgrades.Offer(id, tier - 1).Cost, "higher tiers cost more");
            Run cosmic = AutoPlay(courses[0], 0, Difficulty.Easy, 73);
            Check(cosmic.EnterEndless() && cosmic.MaxShopTier == 5, "all families unlock extra tiers only in endless");
            cosmic.SkipBooster();
            // Isolated purchase fixture: enough funds to verify every family's full upgrade path.
            typeof(Run).GetProperty(nameof(Run.Cash))!.SetValue(cosmic, 250000.0);
            int previousTier = cosmic.ShopTier(id); double previousCost = cosmic.UnitCost; int previousCapacity = cosmic.Capacity;
            while (cosmic.ShopTier(id) < 5)
            {
                Decoration offer = ShopUpgrades.Offer(id, cosmic.ShopTier(id) + 1);
                if (cosmic.ShopOffers.Count == 0) cosmic.ShopOffers.Add(offer); else cosmic.ShopOffers[0] = offer;
                Check(cosmic.Buy(0), "unlocked tiers four and five are purchasable");
            }
            ShopEffect old = ShopUpgrades.Effect(id, previousTier), top = ShopUpgrades.Effect(id, 5);
            Check(cosmic.ShopTier(id) == 5 && !cosmic.Buy(0), "tier-five cap");
            Check(Math.Abs(cosmic.UnitCost - Math.Max(3, previousCost - top.UnitCostSaving + old.UnitCostSaving)) < .001
                && cosmic.Capacity == previousCapacity + top.Capacity - old.Capacity, "cosmic upgrade adds only the effect difference");
        }
        Check(ShopUpgrades.Offer(19, 1).Name == "Boss Chair" && ShopUpgrades.Offer(19, 2).Name == "Comfy Boss Chair"
            && ShopUpgrades.Offer(19, 3).Name == "Gold Boss Chair", "chair upgrade names");
        Check(ShopUpgrades.BonusDecisions(2, 1, 2) == 0 && ShopUpgrades.BonusDecisions(2, 1, 3) == 1
            && ShopUpgrades.BonusDecisions(2, 5, 13) == 4, "weak periodic bonuses grow into powerful tier-five bonuses");
        var preservedTiers = winner.ShopTiers.ToDictionary(item => item.Key, item => item.Value);
        Check(winner.EnterEndless() && !winner.EnterEndless() && winner.Cleared == 12, "choose endless once after victory");
        Check(preservedTiers.All(item => winner.ShopTier(item.Key) == item.Value), "endless preserves owned upgrades");
        winner.SkipBooster(); Check(winner.NextMonth() && winner.Month == 13, "endless advances past month twelve");
        Check(Math.Abs(winner.ProfitTargetFor(13) / winner.ProfitTargetFor(12) - 1.35) < .001
            && Math.Abs(winner.ProfitTargetFor(14) / winner.ProfitTargetFor(13) - 1.35) < .001, "endless quotas compound at thirty-five percent");
        Run endlessResult = AutoPlay(courses[0], 0, Difficulty.Easy, 73, winner);
        Check(endlessResult.Phase == Phase.Lost && endlessResult.Cleared > 12 && endlessResult.Month > 13,
            "real endless campaign clears extra quotas and eventually fails rising targets");
        store.Record(endlessResult);
        Storage savedEndless = new(directory);
        Check(savedEndless.History.Count == 1 && savedEndless.History[0].Won && savedEndless.History[0].Endless
            && savedEndless.History[0].Cleared > 12, "endless updates the original win without duplicating it");
        Run uiWinner = AutoPlay(courses[0], 0, Difficulty.Easy, 73);
        var uiStore = new Storage(Path.Combine(directory, "endless-ui")); uiStore.Record(uiWinner);
        var ui = new GameApp(courses, uiStore);
        typeof(GameApp).GetProperty(nameof(GameApp.Game))!.SetValue(ui, uiWinner);
        typeof(GameApp).GetProperty(nameof(GameApp.Screen))!.SetValue(ui, Screen.End);
        Check(ui.Render(120, 42).Buttons.Any(button => button.Action == "endless"), "victory screen offers endless choice");
        Key(ui, ConsoleKey.E); Check(ui.Screen == Screen.Booster && ui.Game!.Endless, "E enters endless rewards");
        Key(ui, ConsoleKey.E); Check(ui.Screen == Screen.Shop && ui.Render(108, 42).Lines().Any(line => line.Contains("Tiers 1-5")), "unlocked tier cap appears in shop sidebar");
        Key(ui, ConsoleKey.Spacebar); Check(ui.Game!.Month == 13 && ui.Render(108, 42).Lines().Any(line => line.Contains("ENDLESS MONTH 13")), "endless progress is rendered correctly");
        Key(ui, ConsoleKey.Escape); Key(ui, ConsoleKey.M);
        Check(new Storage(Path.Combine(directory, "endless-ui")).History.Count == 1, "abandoning endless updates the same run record");
        Console.WriteLine($"Endless sample: {endlessResult.Cleared - 12} extra quotas cleared; ended in month {endlessResult.Month}.");
        Console.WriteLine($"SELF-TEST PASSED: {assertions} checks; 180 campaigns ({won} initial wins); {retries} seeded retries verify every combination is winnable.");
    }

    static void CheckLoansAndStatistics(List<Course> courses)
    {
        void set(Run run, string property, object value) => typeof(Run).GetProperty(property)!.SetValue(run, value);
        for (int seed = 0; seed < 80; seed++)
        {
            var run = new Run(courses[0], 0, Difficulty.Easy, seed);
            Check(run.Debt == 0 && run.CanBorrow, "new company starts with loan access and no active loan");
            double small = Math.Max(500, Math.Max(0, run.ProfitTarget - run.Preview().Profit) + Math.Max(0, 300 - run.Cash) + 300);
            run.PrepareLoans(); LoanOffer[] original = run.LoanOffers.ToArray(); run.PrepareLoans();
            Check(original.SequenceEqual(run.LoanOffers) && original.Length == 3, "three stable offers, no browsing rerolls");
            for (int i = 0; i < 3; i++)
            {
                LoanOffer offer = original[i];
                double amountVariation = Math.Abs(offer.Amount / (small * (i + 1)) - 1);
                double rateVariation = Math.Abs(offer.InterestRate / (.5 * (i + 1)) - 1);
                Check(amountVariation >= .0499 && amountVariation <= .1001 && rateVariation >= .0499 && rateVariation <= .1001, "loan amount and rate vary by five to ten percent");
                Check(Math.Abs(offer.Total - offer.Amount - offer.Fee) < .011, "interest fee is included exactly once in repayment");
            }
            double cash = run.Cash, profit = run.Preview().Profit;
            Check(run.TakeLoan(1) && run.Cash == cash + original[1].Amount && run.Debt == original[1].Total, "loan credits principal and creates total repayment balance");
            Check(run.Preview().Profit == profit && run.HasActiveLoan && !run.CanBorrow, "borrowing is cash, not sales profit, and blocks a second loan");
            double balance = run.Debt; cash = run.Cash;
            Check(!run.TakeLoan(0) && run.Debt == balance && run.Cash == cash, "active loan blocks additional borrowing atomically");
            set(run, nameof(Run.Cash), 125.0);
            Check(run.RepayLoan() && run.Debt == balance - 125 && run.NextLoanMonth == 1, "partial repayment does not start cooldown");
            set(run, nameof(Run.Cash), run.Debt + 500);
            Check(run.RepayLoan() && run.Debt == 0 && Math.Abs(run.Cash - 500) < .001 && run.NextLoanMonth == 4, "full repayment starts three-month cooldown");
            set(run, nameof(Run.Month), 2); Check(!run.CanBorrow, "one month after repayment is blocked");
            set(run, nameof(Run.Month), 3); Check(!run.CanBorrow, "two months after repayment are blocked");
            set(run, nameof(Run.Month), 4); Check(run.CanBorrow && run.TakeLoan(0), "borrowing unlocks three months after repayment");
            set(run, nameof(Run.Month), 8); Check(!run.CanBorrow, "elapsed months never bypass an active balance");
            set(run, nameof(Run.Cash), run.Debt + 500); run.RepayLoan();
            Check(run.NextLoanMonth == 11, "cooldown starts at repayment month, not borrowing month");
        }
        var rescue = new Run(courses.Single(c => c.Name == "Marketing"), 0, Difficulty.Easy, 14);
        set(rescue, nameof(Run.Price), 5.0); rescue.FinishDecisions(); rescue.SkipEvents();
        Check(rescue.LastReport!.Failures.Contains("Profit") && rescue.Cleared == 0, "rescue fixture actually misses profit quota");
        double actualProfit = rescue.LastReport.Profit, gap = rescue.BridgeCost, actualRevenue = rescue.LastReport.Revenue;
        Check(rescue.TakeLoan(2) && rescue.Cleared == 1 && rescue.LastReport.Failures.Count == 0, "loan funds a missed money quota and continues the run");
        Check(rescue.LastReport.Profit == actualProfit && rescue.LastReport.Revenue == actualRevenue && rescue.LastReport.BridgeFunding == gap, "bridge support is recorded separately from true earnings");
        rescue.ContinueResult(); Check(rescue.Phase == Phase.SpecialEvent && rescue.HasActiveLoan, "rescued month enters normal rewards with loan outstanding");
        var service = new Run(courses.Single(c => c.Name == "Microeconomics"), 0, Difficulty.Easy, 15);
        set(service, nameof(Run.Quality), 0.0); service.FinishDecisions(); service.SkipEvents();
        double beforeCash = service.Cash;
        Check(!service.TakeLoan(2) && service.Cash == beforeCash && service.Debt == 0, "loans cannot erase non-money failures");
        var finance = new Run(courses.Single(c => c.Name == "Finance"), 0, Difficulty.Easy, 16);
        finance.FinishDecisions(); finance.SkipEvents(); finance.PrepareLoans();
        finance.LoanOffers[0] = new("Too much debt", 5000, 1);
        Check(!finance.TakeLoan(0) && finance.Debt == 0, "result-screen borrowing respects debt quota even after an initial pass");
        var cards = new Run(courses[0], 0, Difficulty.Easy, 17); cards.TakeLoan(0);
        set(cards, nameof(Run.Debt), 200.0); cards.Hand[0] = new(CardKind.Repay, "Repay");
        Check(cards.Play(0) && cards.Debt == 0 && cards.NextLoanMonth == cards.Month + 3, "repayment card also starts cooldown");
        cards.Hand[0] = new(CardKind.Borrow, "Removed borrowing action"); beforeCash = cards.Cash;
        Check(!cards.Play(0) && cards.Cash == beforeCash && cards.Debt == 0, "legacy borrowing card cannot bypass loan selection or cooldown");
        var app = new GameApp(courses, new Storage(Path.Combine(Path.GetTempPath(), "TranslateUni-credit-tests-" + Guid.NewGuid().ToString("N"))));
        var normal = new Run(courses[0], 0, Difficulty.Easy, 18);
        typeof(GameApp).GetProperty(nameof(GameApp.Game))!.SetValue(app, normal);
        typeof(GameApp).GetProperty(nameof(GameApp.Screen))!.SetValue(app, Screen.Table);
        Key(app, ConsoleKey.L); Check(app.Screen == Screen.Loans && app.Render(108,42).Buttons.Count(b => b.Action == "select") == 3, "L opens three loan cards");
        Key(app, ConsoleKey.D3); Key(app, ConsoleKey.Enter);
        Check(app.Screen == Screen.Loans && normal.HasActiveLoan && app.TakeAnimationFrames().Count == 52, "loan choice stays in repayment view and animates selected offer");
        Canvas repayment=app.Render(108,42);
        Check(repayment.Buttons.All(b => b.Action != "select" && b.Action != "loanTake") && repayment.Buttons.Any(b => b.Action=="loanRepay"), "active loan screen only offers repayment");
        Key(app, ConsoleKey.P);
        Check(app.Render(108,42).Lines().Any(line=>line.Contains("Next loan in 3 months")) && app.Render(108,42).Buttons.All(b=>b.Action!="loanRepay" && b.Action!="loanTake"), "repaid loan screen shows cooldown without borrowing controls");
        Check(normal.Debt == 0 && !normal.CanBorrow, "loan desk repayment applies cooldown");
        Key(app, ConsoleKey.Escape); Check(app.Screen == Screen.Table, "loan screen returns safely");
        foreach ((int width, int height) in new[] { (108,42), (120,42), (140,46) })
        {
            Canvas frame = app.Render(width,height); string[] lines = frame.Lines();
            int tableWidth = width - 30;
            Check(lines[35][..tableWidth].Trim().Length == 0 && GameApp.StatChangeRow == GameApp.StatBarTop - 2, "change labels have exactly one empty row before bars");
            Check(GameApp.StatChangeRow == 34 && GameApp.StatBarTop == 36 && GameApp.StatBarBottom == 38 && lines[39].Contains("CASH") && lines[39].Contains("SERVICE"), "whole statistics board is lowered by two rows");
            Check(Enumerable.Range(36,3).All(y => lines[y][..tableWidth].Count(c => c == '│') == 20), "every statistic has its own vertical progress bar");
        }
        typeof(Run).GetProperty(nameof(Run.Phase))!.SetValue(normal, Phase.Shop);
        normal.ShopOffers.Clear(); normal.ShopOffers.Add(ShopUpgrades.Offer(0,3)); normal.ShopTiers[0] = 3;
        typeof(GameApp).GetProperty(nameof(GameApp.Screen))!.SetValue(app, Screen.Shop);
        Canvas shop = app.Render(108,42); string[] shopLines = shop.Lines();
        Check(shopLines[14].Contains("SELECTED") && shop.ColorAt(3,13) == ConsoleColor.Magenta && shopLines[13][3] == '+' && shopLines[15][3] == '+', "selected shop item has a purple selection box");
        Check(shopLines[8].Contains("MAX TIER") && shop.ColorAt(3,8) == ConsoleColor.Magenta, "selected completed upgrade keeps the selection accent");
    }


    static void CheckInputAndTheme(List<Course> courses)
    {
        var guard=new InputReleaseGuard(); guard.Block([ConsoleKey.Enter,ConsoleKey.D1]);
        Check(!guard.Accept(ConsoleKey.Enter,true) && !guard.Accept(ConsoleKey.Enter,true),"held Enter and repeated keydowns cannot cross a screen boundary");
        Check(!guard.Accept(ConsoleKey.Enter,false) && guard.Accept(ConsoleKey.Enter,true),"fresh Enter works after release");
        Check(guard.Accept(ConsoleKey.D2,true) && !guard.Accept(ConsoleKey.D1,true),"fresh key works while an old held key is blocked");
        var canvas=new Canvas(90,4); canvas.Text(0,0,"Risk -1; Debt -$75; Cash +$400; Unit cost -$0.10; Share +0.5%"); canvas.Text(0,1,"Quality 80% | Capacity 500 | Service 100% | Inventory +25");
        GameTheme.ColorStatistics(canvas);
        foreach((string name,ConsoleColor color) in new[] {("Risk",ConsoleColor.DarkRed),("Debt",ConsoleColor.Red),("Cash",ConsoleColor.Green),("Unit cost",ConsoleColor.Magenta),("Share",ConsoleColor.Yellow)})
        { int x=canvas.Lines()[0].IndexOf(name,StringComparison.Ordinal); Check(canvas.ColorAt(x,0)==color,"mixed statistic text has consistent colors: "+name); }
        Check(canvas.ColorAt(canvas.Lines()[0].IndexOf("$75"),0)==ConsoleColor.Red && canvas.ColorAt(canvas.Lines()[0].IndexOf("$400"),0)==ConsoleColor.Green,"statistic amounts match their labels");
        Check(GameTheme.Statistic("pay")==GameTheme.Statistic("debt") && GameTheme.Statistic("interest")==GameTheme.Statistic("debt"), "repayment and interest text use debt colors");
        Check(GameTheme.Palette.Skip(9).All(c=>Math.Max(c.R,Math.Max(c.G,c.B))-Math.Min(c.R,Math.Min(c.G,c.B))<90),"accent palette is brighter but avoids full saturation");
        var app=new GameApp(courses,new Storage(Path.Combine(Path.GetTempPath(),"TranslateUni-input-tests-"+Guid.NewGuid().ToString("N"))));
        int version=app.InputVersion; Key(app,ConsoleKey.Enter); Check(app.InputVersion>version,"menu transition requests queue clearing");
        var run=new Run(courses[0],0,Difficulty.Easy,99);
        typeof(GameApp).GetProperty(nameof(GameApp.Game))!.SetValue(app,run); typeof(GameApp).GetProperty(nameof(GameApp.Screen))!.SetValue(app,Screen.Table);
        Key(app,ConsoleKey.D1); version=app.InputVersion; Key(app,ConsoleKey.R); Check(app.InputVersion>version,"reroll requests clearing even on same screen");
        Key(app,ConsoleKey.L); Key(app,ConsoleKey.D1); version=app.InputVersion; Key(app,ConsoleKey.Enter); Check(app.InputVersion>version,"taking a loan requests clearing before repayment view");
        version=app.InputVersion; Key(app,ConsoleKey.P); Check(app.InputVersion>version,"repayment requests clearing before cooldown view");
    }


    static void CheckStatementsAndMotion(List<Course> courses)
    {
        var run=new Run(courses[0],0,Difficulty.Easy,112,tutorial:true);
        Check(run.Preview().Breakdown!=null && Math.Abs(run.Preview().Breakdown!.Lines.Sum(line=>line.Amount)-run.Preview().Profit)<.001,"initial breakdown sums to forecast profit");
        run.Play(0); run.FinishDecisions();
        run.Events[0]=new("Office Bake Sale","",Custom:7); run.TakeEvent(0);
        ProfitBreakdown saved=run.LastReport!.Breakdown!;
        Check(saved.Lines.Any(line=>line.Name=="Advertise Product" && line.Amount==-180),"statement includes actual advertising expense");
        Check(saved.Lines.Any(line=>line.Name=="Office Bake Sale" && line.Amount==180),"statement includes actual event payout");
        Check(Math.Abs(saved.Lines.Sum(line=>line.Amount)-run.LastReport.Profit)<.001 && saved.Total==run.LastReport.Profit,"settled statement exactly reconciles with true profit");
        Check(saved.AvailableStock==450 && run.Inventory==450-run.LastReport.Sold,"statement captures pre-sale inventory rather than recalculating after settlement");
        foreach(var course in courses)
        for(int seed=0;seed<16;seed++)
        {
            var sample=new Run(course,Math.Min(9,course.Lessons.Count-1),Difficulty.Hard,seed);
            sample.Hand[0]=new(CardKind.Advertise,"Ads"); sample.Play(0);
            sample.Hand[0]=new(CardKind.Express,"Express"); sample.Play(0);
            sample.TakeEvent(seed%3);
            var report=sample.LastReport!;
            Check(report.Breakdown!=null && Math.Abs(report.Breakdown.Lines.Sum(line=>line.Amount)-report.Profit)<.001,"different course/event statements reconcile with actual settlement");
        }
        var app=new GameApp(courses,new Storage(Path.Combine(Path.GetTempPath(),"TranslateUni-statement-tests-"+Guid.NewGuid().ToString("N"))));
        var company=new Run(courses[0],0,Difficulty.Easy,51);
        typeof(GameApp).GetProperty(nameof(GameApp.Game))!.SetValue(app,company);
        typeof(GameApp).GetProperty(nameof(GameApp.Screen))!.SetValue(app,Screen.Table);
        company.Hand[0]=new(CardKind.RaisePrice,"Raise price");
        StatSnapshot before=company.CaptureStats(); Key(app,ConsoleKey.D1); Key(app,ConsoleKey.Enter);
        var feedback=app.TakeFeedbackFrames(108,42);
        Check(feedback.Count==10 && feedback.Sum(f=>f.Delay)==300 && feedback.All(f=>f.Stage==AnimationStage.Statistics),"stat change gets a short 300ms animation");
        StatSnapshot after=company.CaptureStats();
        Check(StatMotion.Interpolate(before,after,0)==before && StatMotion.Interpolate(before,after,1)==after,"stat interpolation has exact endpoints");
        Check(StatMotion.Interpolate(before,after,.5).Price>before.Price && StatMotion.Interpolate(after,before,.5).Price<after.Price,"increases and decreases animate in the proper direction");
        Check(app.TakeFeedbackFrames(108,42).Count==0 && company.CaptureStats()==after,"visual feedback is consumed once and does not alter gameplay statistics");
        Key(app,ConsoleKey.E); Key(app,ConsoleKey.E);
        var printing=app.TakeFeedbackFrames(108,42);
        Check(printing.Count>2 && printing.All(f=>f.Stage==AnimationStage.ResultLine),"quota results queue line-by-line printing");
        Check(printing[0].Canvas.Lines().All(line=>!line.Contains("NET PROFIT:")) && printing[^1].Canvas.Lines().Any(line=>line.Contains("NET PROFIT:")),"profit total prints after the contributing lines");
        Check(printing[1].Canvas.Lines().Any(line=>line.Contains("Sales revenue")) && printing[0].Canvas.Lines().All(line=>!line.Contains("Sales revenue")),"first calculation prints one line at a time");
        Check(printing[^1].Canvas.Lines()[39].Contains("CASH") && printing[^1].Canvas.Lines()[32].Contains("ENTER:"),"result printing preserves lower controls and statistics");
        company.Reports[^1]=company.LastReport! with { Breakdown=new ProfitBreakdown(Enumerable.Range(0,25).Select(i=>new ProfitLine("Entry "+i,"",i)).ToArray(),300,180,180,450,500) };
        Canvas first=app.Render(108,42); Check(first.Lines().Any(line=>line.Contains("PAGE 1/3")),"long statements are paginated without hiding entries");
        Key(app,ConsoleKey.PageDown); var second=app.TakeFeedbackFrames(108,42);
        Check(second[^1].Canvas.Lines().Any(line=>line.Contains("PAGE 2/3")) && second[^1].Canvas.Lines().Any(line=>line.Contains("Entry 12")),"next page prints its own calculation lines");
        Key(app,ConsoleKey.PageDown); Key(app,ConsoleKey.PageDown);
        Check(app.Render(108,42).Lines().Any(line=>line.Contains("PAGE 3/3")),"statement page navigation stays in bounds");
    }


    static void CheckLatestFixes(List<Course> courses)
    {
        Check(GameContent.Decisions.All(card=>card.Kind!=CardKind.Borrow),"non-working loan desk card is removed from pool");
        bool sawRepay=false;
        for(int seed=0;seed<160;seed++)
        {
            var run=new Run(courses[0],0,Difficulty.Easy,seed);
            Check(run.Hand.All(card=>card.Kind!=CardKind.Repay && card.Kind!=CardKind.Borrow),"no debt means no repayment card");
            run.Reroll(); Check(run.Hand.All(card=>card.Kind!=CardKind.Repay),"reroll also filters inactive repayment cards");
            run.TakeLoan(0); run.Reroll();
            typeof(Run).GetProperty(nameof(Run.Rerolls))!.SetValue(run,1); run.Reroll();
            if(run.Hand.Any(card=>card.Kind==CardKind.Repay)) sawRepay=true;
            run.Hand[0]=new(CardKind.Repay,"Repay Debt");
            typeof(Run).GetProperty(nameof(Run.Cash))!.SetValue(run,run.Debt+500);
            run.RepayLoan(); Check(run.Hand.All(card=>card.Kind!=CardKind.Repay),"full repayment replaces stale repayment cards immediately");
            foreach(var paid in new[]{CardKind.Factory,CardKind.Warehouse,CardKind.Express,CardKind.EmergencyShipment})
                Check(run.Describe(paid).All(line=>!line.Contains("free",StringComparison.OrdinalIgnoreCase)),"paid stock cards do not advertise free stock");
        }
        Check(sawRepay,"repayment cards remain available when debt is active");
        var savings=new Run(courses[0],0,Difficulty.Easy,33);
        typeof(Run).GetProperty(nameof(Run.Phase))!.SetValue(savings,Phase.Shop);
        typeof(Run).GetProperty(nameof(Run.Cash))!.SetValue(savings,50000.0);
        foreach(int id in new[]{5,10})
        for(int tier=1;tier<=3;tier++)
        {
            savings.ShopOffers.Clear(); savings.ShopOffers.Add(ShopUpgrades.Offer(id,tier));
            double before=savings.OfficeOverhead,profit=savings.Preview().Profit;
            Check(savings.Buy(0),"overhead decoration upgrade purchases succeed");
            Check(savings.OfficeOverhead<=before && Math.Abs(savings.Preview().Profit-profit-(before-savings.OfficeOverhead))<.011,"each overhead upgrade immediately increases profit by the actual savings");
            Check(savings.Message.Contains("Office overhead now"),"overhead purchase reports the new monthly charge");
        }
        typeof(Run).GetProperty(nameof(Run.Endless))!.SetValue(savings,true);
        savings.ShopOffers[0]=ShopUpgrades.Offer(5,4); savings.Buy(0);
        savings.ShopOffers[0]=ShopUpgrades.Offer(5,5); savings.Buy(0);
        Check(savings.OfficeOverhead==0 && savings.OfficeOverheadSavings==savings.BaseOfficeOverhead,"overhead savings can eliminate the bill instead of stopping at sixty dollars");
        ProfitLine overhead=savings.Preview().Breakdown!.Lines.Single(line=>line.Name=="Office overhead");
        Check(overhead.Amount==0 && overhead.Calculation.Contains("savings $300") && Math.Abs(savings.Preview().Breakdown!.Lines.Sum(line=>line.Amount)-savings.Preview().Profit)<.001,"statement shows the net overhead charge without double-counting savings");
        typeof(Run).GetProperty(nameof(Run.Cleared))!.SetValue(savings,savings.Month);
        Check(savings.NextMonth() && savings.OfficeOverhead==0,"permanent overhead reduction persists across actual month transition");
        var coupon=new Run(courses[0],0,Difficulty.Easy,91);
        double baseProfit=coupon.Preview().Profit;
        typeof(Run).GetProperty(nameof(Run.Phase))!.SetValue(coupon,Phase.Booster);
        coupon.Boosters.Add(Run.BoosterPool.Single(card=>card.Id==9));
        Check(coupon.ClaimBooster(0) && coupon.OfficeOverhead==275 && Math.Abs(coupon.Preview().Profit-baseProfit-coupon.Preview().Sold*.75-25)<.011,"booster overhead savings and per-sale margin apply together");
        var tax=ClearFirst(courses[0],92);
        typeof(Run).GetProperty(nameof(Run.CompanyNews))!.SetValue(tax,Run.CompanyEventPool.Single(card=>card.Id==7));
        tax.AcknowledgeCompanyEvent(); tax.SkipBooster();
        Check(tax.NextMonth() && tax.OfficeOverhead==220,"tax relief reduces the next month's real overhead charge");
        tax.FinishDecisions(); tax.SkipEvents(); tax.ContinueResult();
        typeof(Run).GetProperty(nameof(Run.CompanyNews))!.SetValue(tax,Run.CompanyEventPool[0]);
        tax.AcknowledgeCompanyEvent(); tax.SkipBooster();
        Check(tax.NextMonth() && tax.OfficeOverhead==300,"temporary overhead relief expires after exactly one month");
        var tutorial=new GameApp(courses,new Storage(Path.Combine(Path.GetTempPath(),"TranslateUni-tutorial-text-"+Guid.NewGuid().ToString("N"))));
        Key(tutorial,ConsoleKey.T); Key(tutorial,ConsoleKey.Enter); Key(tutorial,ConsoleKey.Enter); Key(tutorial,ConsoleKey.D1); Key(tutorial,ConsoleKey.Enter); Key(tutorial,ConsoleKey.R); Key(tutorial,ConsoleKey.E);
        Key(tutorial,ConsoleKey.Enter); // Wrong action during the guided leave-events step.
        string line=tutorial.Render(108,42).Lines()[33][..78].Trim();
        Check(line=="Press E to leave the optional events.","tutorial notice clears old message characters before writing the instruction");
        var library=new GameApp(courses,new Storage(Path.Combine(Path.GetTempPath(),"TranslateUni-course-comparison-"+Guid.NewGuid().ToString("N"))));
        Key(library,ConsoleKey.D3); Key(library,ConsoleKey.C);
        Check(library.Screen==Screen.CourseComparison && library.Render(108,42).Lines().Any(line=>line.Contains("$0.35/unit")),"course library comparison explains actual logistics carrying cost");
        Check(courses.All(course=>CourseDifferences.For(course.Name).Focus.Contains("Course Focus")) && library.Render(108,42).Lines().Any(line=>line.Contains("carbon cost")),"comparison explains focus effects and special topic mechanics");
        Key(library,ConsoleKey.Escape); Check(library.Screen==Screen.Courses,"comparison returns to course library");
        Key(library,ConsoleKey.Enter); Key(library,ConsoleKey.Enter); Key(library,ConsoleKey.Enter);
        Check(library.Render(108,42).Lines().All(line=>!line.Contains("CHIP",StringComparison.OrdinalIgnoreCase)),"difficulty screens omit the old terminology");
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

    public static Run AutoPlay(Course course, int topic, Difficulty difficulty, int seed, Run? existing = null)
    {
        var run = existing ?? new Run(course, topic, difficulty, seed);
        for (int guard = 0; guard < 1000 && run.Phase is not (Phase.Won or Phase.Lost); guard++)
        {
            if (run.Phase == Phase.Decisions)
            {
                Forecast f = run.Preview();
                int find(params CardKind[] kinds) => run.Hand.FindIndex(c => kinds.Contains(c.Kind) && run.Cash >= run.CardCost(c.Kind));
                int card = -1;
                if (run.Inventory < f.Demand) card = find(CardKind.Restock);
                if (card < 0 && run.Inventory < f.Demand && run.Cash < run.CardCost(CardKind.Restock))
                    card = find(CardKind.EmergencyShipment, CardKind.Warehouse, CardKind.Crowdfunding, CardKind.Borrow);
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
                    double reserve = Math.Max(2200, run.CardCost(CardKind.Restock) * 1.35 + 500);
                    while (slot >= 0 && run.ShopTier(item) < run.MaxShopTier && run.Cash > run.ShopOffers[slot].Cost + reserve)
                        if (!run.Buy(slot)) break;
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
            File.WriteAllLines(Path.Combine(directory, name + "-108.txt"), app.Render(108, 42).Lines());
        }
        capture("01-main"); Key(app, ConsoleKey.T); capture("02-learning-guide"); Key(app, ConsoleKey.Enter); capture("02-tutorial-intro"); Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.D1); capture("03-cards-and-desk");
        var flight = CardAnimation.Build(app.Render(120, 42), 0);
        foreach (int i in new[] { 0, 17, 21, 29, 38, 46, 49, 51 })
            File.WriteAllLines(Path.Combine(directory, $"03-flight-{i:00}.txt"), flight[i].Canvas.Lines());
        Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.R); Key(app, ConsoleKey.E); capture("04-events"); Key(app, ConsoleKey.E); capture("05-report");
        Key(app, ConsoleKey.Enter); capture("06-company-news"); Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.D1); capture("07-boosters");
        Key(app, ConsoleKey.Enter); capture("08-shop-and-side-menu"); Key(app, ConsoleKey.D2); Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.Spacebar); capture("09-tutorial-complete");
        Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.Escape); Key(app, ConsoleKey.D1);capture("10-modes");Key(app,ConsoleKey.Enter);capture("10-profile");Key(app,ConsoleKey.Enter); capture("10-course-selection"); Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.Enter); capture("11-difficulty");
        Run year = AutoPlay(courses[0], 0, Difficulty.Easy, 73);
        typeof(GameApp).GetProperty(nameof(GameApp.Game))!.SetValue(app, year);
        typeof(GameApp).GetProperty(nameof(GameApp.Screen))!.SetValue(app, Screen.End);
        capture("12-victory-endless-choice");
        Key(app, ConsoleKey.E); Key(app, ConsoleKey.E); capture("13-endless-upgrades");
        Key(app, ConsoleKey.Spacebar); capture("14-endless-month13");
        Console.WriteLine("Saved UI previews to " + directory);
    }
}


