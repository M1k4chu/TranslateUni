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
        foreach (Canvas source in new[] { selected, app.Render(200, 60) })
        {
            List<AnimationFrame> frames = CardAnimation.Build(source);
            Check(frames.Count == 22 && frames.Sum(f => f.Delay) == 995, "original animation frame counts and timing");
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
        }
        Key(app, ConsoleKey.I); Check(app.Screen == Screen.Inspect, "full card explanation");
        Key(app, ConsoleKey.Escape); Check(app.Screen == Screen.Table, "inspection preserves selection");
        Key(app, ConsoleKey.Enter); Check(app.TutorialStep == 2, "tutorial advances after actual card play");
        Check(app.TakeAnimationFrames().Count == 22 && app.TakeAnimationFrames().Count == 0, "successful card action queues one animation");
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
        Key(ui, ConsoleKey.E); Check(ui.Screen == Screen.Shop && ui.Render(108, 40).Lines().Any(line => line.Contains("Tiers 1-5")), "unlocked tier cap appears in shop sidebar");
        Key(ui, ConsoleKey.Spacebar); Check(ui.Game!.Month == 13 && ui.Render(108, 40).Lines().Any(line => line.Contains("ENDLESS MONTH 13")), "endless progress is rendered correctly");
        Key(ui, ConsoleKey.Escape); Key(ui, ConsoleKey.M);
        Check(new Storage(Path.Combine(directory, "endless-ui")).History.Count == 1, "abandoning endless updates the same run record");
        Console.WriteLine($"Endless sample: {endlessResult.Cleared - 12} extra quotas cleared; ended in month {endlessResult.Month}.");
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
            File.WriteAllLines(Path.Combine(directory, name + "-108.txt"), app.Render(108, 40).Lines());
        }
        capture("01-main"); Key(app, ConsoleKey.T); capture("02-learning-guide"); Key(app, ConsoleKey.Enter); capture("02-tutorial-intro"); Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.D1); capture("03-cards-and-desk");
        Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.R); Key(app, ConsoleKey.E); capture("04-events"); Key(app, ConsoleKey.E); capture("05-report");
        Key(app, ConsoleKey.Enter); capture("06-company-news"); Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.D1); capture("07-boosters");
        Key(app, ConsoleKey.Enter); capture("08-shop-and-side-menu"); Key(app, ConsoleKey.D2); Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.Spacebar); capture("09-tutorial-complete");
        Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.Escape); Key(app, ConsoleKey.D1); capture("10-course-selection"); Key(app, ConsoleKey.Enter); Key(app, ConsoleKey.Enter); capture("11-difficulty-chips");
        Run year = AutoPlay(courses[0], 0, Difficulty.Easy, 73);
        typeof(GameApp).GetProperty(nameof(GameApp.Game))!.SetValue(app, year);
        typeof(GameApp).GetProperty(nameof(GameApp.Screen))!.SetValue(app, Screen.End);
        capture("12-victory-endless-choice");
        Key(app, ConsoleKey.E); Key(app, ConsoleKey.E); capture("13-endless-upgrades");
        Key(app, ConsoleKey.Spacebar); capture("14-endless-month13");
        Console.WriteLine("Saved UI previews to " + directory);
    }
}

