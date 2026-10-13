namespace TranslateUni;

public sealed partial class GameApp
{
    int boosterScroll;
    Screen beforeInspect;
    string inspectTitle = "", inspectFlavor = "", inspectDefinition = "";
    Rarity inspectRarity;
    int tutorialStep = -1;
    public int TutorialStep => tutorialStep;

    Canvas DrawSidebar(int height)
    {
        var c = new Canvas(30, height);
        Run g = Game!;
        for (int y = 0; y < height; y++) c.Text(0, y, "|", ConsoleColor.DarkCyan);
        bool between = g.Phase is Phase.SpecialEvent or Phase.Booster or Phase.Shop;
        int month = between && (g.Month < 12 || g.Endless) ? g.Month + 1 : g.Month;
        MonthReport? previous = between || g.Phase is Phase.Won or Phase.Lost
            ? g.LastReport : g.Reports.LastOrDefault(r => r.Month < g.Month);
        c.Text(2, 1, g.Endless ? $"{(between ? "NEXT " : "")}ENDLESS MONTH {month}" : $"{(between ? "NEXT " : "")}MONTH {month}/12 QUOTAS", ConsoleColor.Cyan);
        c.Text(2, 2, $"Profit >= ${g.ProfitTargetFor(month):N0}", ConsoleColor.Yellow);
        c.Text(2, 3, previous == null ? "Last profit: -- (new run)" : $"Last profit: ${previous.Profit:N0}", ConsoleColor.White);
        c.Text(2, 4, previous == null ? "Last sales: --" : $"Last sales: ${previous.Revenue:N0}");
        int yQuota = 5;
        foreach (Quota quota in g.Quotas(month).Skip(1))
            c.Text(2, yQuota++, $"{quota.Name} {(quota.AtLeast ? ">=" : "<=")} {Metric(quota.Name, quota.Target)}", ConsoleColor.Cyan);
        c.Text(2, 9, $"Decor: {g.Owned.Count}/20 | Tiers 1-{g.MaxShopTier}");
        c.Text(2, 10, "ACTIVE BOOSTER CARDS", ConsoleColor.White);
        c.Text(2, 11, "GREEN regular / PURPLE rare", ConsoleColor.DarkGray);
        List<ActiveBooster> active = SortedBoosters();
        int visible = Math.Max(1, (height - 16) / 4);
        boosterScroll = Math.Clamp(boosterScroll, 0, Math.Max(0, active.Count - visible));
        if (active.Count == 0)
        {
            c.Text(2, 14, "No active boosters yet.");
            c.Wrapped(2, 16, "Clear a month, then choose a free booster.", 26, 3);
        }
        for (int row = 0; row < Math.Min(visible, active.Count - boosterScroll); row++)
        {
            ActiveBooster entry = active[boosterScroll + row]; Booster b = entry.Card;
            int y = 13 + row * 4;
            ConsoleColor color = b.Family == BoosterFamily.Green ? ConsoleColor.Green : ConsoleColor.Magenta;
            string name = (b.Family == BoosterFamily.Green ? "G " : "P ") + b.Name + (entry.Stacks > 1 ? $" x{entry.Stacks}" : "");
            c.Text(2, y, name.Length > 26 ? name[..26] : name, color);
            c.Wrapped(2, y + 1, (entry.Stacks > 1 ? "Per copy: " : "") + b.Description, 26, 2);
            c.Buttons.Add(new(2, y, 26, 3, "activeInspect", boosterScroll + row));
        }
        c.Text(2, height - 3, $"Showing {Math.Min(boosterScroll + 1, active.Count)}-{Math.Min(boosterScroll + visible, active.Count)} / {active.Count}", ConsoleColor.DarkGray);
        c.Button(2, height - 2, "UP", "scrollUp"); c.Button(11, height - 2, "DOWN", "scrollDown");
        c.Text(2, height - 1, "PgUp/PgDn | Click: details", ConsoleColor.DarkGray);
        return c;
    }
    List<ActiveBooster> SortedBoosters() => Game!.ActiveBoosts.OrderBy(b => b.Card.Family).ThenBy(b => b.Card.Id).ToList();
    void ScrollBoosters(int delta) => boosterScroll = Math.Max(0, boosterScroll + delta);

    void DrawCompanyEvent(Canvas c)
    {
        Run g = Game!; CompanyEvent news = g.CompanyNews!;
        Header(c, "BREAKING COMPANY NEWS", $"After clearing month {g.Month}");
        c.Center(6, news.Name.ToUpperInvariant(), GameContent.Color(news.Rarity));
        c.Center(8, news.Flavor, ConsoleColor.Yellow);
        c.Wrapped(6, 11, news.Description, c.Width - 12, 6, ConsoleColor.White);
        c.Wrapped(6, 19, g.Cleared == 12 && !g.Endless ? "The year is won. This headline applies if you choose Endless, and cannot cancel your victory."
            : "This is a company event, not an optional card. Read the consequences, then prepare for next month.", c.Width - 12, 3);
        c.Button((c.Width - 21) / 2, 25, "ENTER ACKNOWLEDGE", "companyContinue", selected: true);
        Footer(c, "ENTER: Continue | I: Details | ESC: Pause");
    }
    void InspectSelected()
    {
        if (Game == null) return;
        if (Screen is Screen.Table or Screen.Events or Screen.Booster && selected < 0)
        { notice = "Select a card first, then press I for its full explanation."; return; }
        switch (Screen)
        {
            case Screen.Table:
                Card card = Game.Hand[selected];
                ShowInfo(card.Name, GameContent.Flavor(card.Kind), string.Join(". ", Game.Describe(card.Kind)), card.Rarity); break;
            case Screen.Events:
                EventCard e = Game.Events[selected]; ShowInfo(e.Name, GameContent.Flavor(e), e.Description, e.Rarity); break;
            case Screen.Booster:
                Booster b = Game.Boosters[selected]; ShowInfo(b.Name, GameContent.Flavor(b), b.Description + (b.Persistent ? ". This effect stays for this run." : ". This is an instant gift; it is not an active booster."), b.Rarity); break;
            case Screen.Shop:
                if (shopIndex >= Game.ShopOffers.Count) { notice = "This shelf slot is empty."; break; }
                Decoration d = Game.ShopOffers[shopIndex];
                ShowInfo(d.Name, GameContent.DecorationFlavor(d.Id), $"Tier {d.Tier}/{Game.MaxShopTier}. Price: ${d.Cost}. Total effect at this tier: {d.Boost}. Current tier: {Game.ShopTier(d.Id)}. Upgrading adds only the difference, and preserves earlier progress. Tiers 4-5 unlock after choosing Endless.", d.Rarity); break;
            case Screen.SpecialEvent:
                CompanyEvent n = Game.CompanyNews!; ShowInfo(n.Name, n.Flavor, n.Description, n.Rarity); break;
        }
    }
    void InspectActive(int index)
    {
        List<ActiveBooster> cards = SortedBoosters();
        if (index < 0 || index >= cards.Count) return;
        ActiveBooster b = cards[index];
        ShowInfo(b.Card.Name, GameContent.Flavor(b.Card), b.Card.Description + $". Active copies: {b.Stacks}. Effects apply for each copy.", b.Card.Rarity);
    }
    void ShowInfo(string title, string flavor, string definition, Rarity rarity)
    {
        if (Screen != Screen.Inspect) beforeInspect = Screen;
        inspectTitle = title; inspectFlavor = flavor; inspectDefinition = definition; inspectRarity = rarity;
        Screen = Screen.Inspect;
    }
    void DrawInspect(Canvas c)
    {
        Header(c, "CARD EXPLANATION", inspectTitle);
        c.Center(6, inspectRarity.ToString().ToUpperInvariant(), GameContent.Color(inspectRarity));
        c.Wrapped(5, 9, inspectFlavor, c.Width - 10, 2, ConsoleColor.Yellow);
        c.Text(5, 13, "WHAT IT DOES", ConsoleColor.Cyan);
        c.Wrapped(5, 15, inspectDefinition, c.Width - 10, 12, ConsoleColor.White);
        c.Button((c.Width - 17) / 2, 30, "ENTER BACK", "inspectBack", selected: true);
        Footer(c, "ENTER / ESC: Back | Your cards and choices are preserved");
    }

    void StartTutorial()
    {
        courseIndex = topicIndex = 0; difficultyIndex = (int)Difficulty.Easy;
        Game = new Run(courses[0], 0, Difficulty.Easy, 31415, tutorial: true);
        selected = -1; shopIndex = 1; boosterScroll = 0; recorded = false; tutorialStep = 0;
        Screen = Screen.TutorialIntro;
    }
    string TutorialInstruction => tutorialStep switch
    {
        1 => "Press 1, then ENTER to advertise.",
        2 => "Press R to use your one reroll.",
        3 => "Press E to end decisions.",
        4 => "Press E to leave the optional events.",
        5 => "Press ENTER to continue from the quota report.",
        6 => "Press ENTER to acknowledge company news.",
        7 => "Press 1, then ENTER to claim a green booster.",
        8 => "Press 2, then ENTER to buy the Desk Lamp.",
        9 => "Press SPACE to leave and start month two.",
        _ => "Press ENTER to continue the tutorial."
    };
    bool TutorialAllows(string action, int index)
    {
        if (Game?.Tutorial != true || Screen is Screen.Pause or Screen.Inspect) return true;
        if (action is "activeInspect" or "scrollUp" or "scrollDown" or "inspect" or "inspectBack") return true;
        return tutorialStep switch
        {
            0 => action == "tutorialBegin", 1 => action == "select" || action == "play" && selected == 0,
            2 => action == "reroll", 3 => action == "skip", 4 => action == "skip",
            5 => action == "continue", 6 => action == "companyContinue",
            7 => action == "select" || action == "play" && selected == 0,
            8 => action == "shopSelect" || action == "buy" && index == 1,
            9 => action == "next", 11 => action == "tutorialFinish", _ => true
        };
    }
    void ObserveTutorial(string action, Phase? before)
    {
        if (Game?.Tutorial != true) return;
        if (tutorialStep == 1 && action == "play" && Game.Actions == 2) tutorialStep = 2;
        else if (tutorialStep == 2 && action == "reroll" && Game.Rerolls == 0) tutorialStep = 3;
        else if (tutorialStep == 3 && Game.Phase == Phase.Events) tutorialStep = 4;
        else if (tutorialStep == 4 && Game.Phase == Phase.Result) tutorialStep = 5;
        else if (tutorialStep == 5 && Game.Phase == Phase.SpecialEvent) tutorialStep = 6;
        else if (tutorialStep == 6 && Game.Phase == Phase.Booster) tutorialStep = 7;
        else if (tutorialStep == 7 && Game.Phase == Phase.Shop) tutorialStep = 8;
        else if (tutorialStep == 8 && Game.Owned.Contains(1)) tutorialStep = 9;
        else if (tutorialStep == 9 && Game.Month == 2)
        { tutorialStep = 11; Screen = Screen.TutorialComplete; }
    }
    void DrawTutorialIntro(Canvas c)
    {
        Header(c, "TRANSLATEUNI - GUIDED PRACTICE", "Your learning guide in action; about two minutes");
        c.Wrapped(5, 6, "Pick a course, choose a topic, then select a difficulty chip. Full runs last twelve months. This practice uses Marketing on Easy.", c.Width - 10, 4);
        c.Wrapped(5, 12, "Your goal: meet every quota on the right. Profit is sales money minus costs. The desk shows cash, debt, price, stock, cost and share.", c.Width - 10, 4);
        c.Wrapped(5, 18, "Select a card with WASD, 1-3 or arrows. It lifts up two rows. The joke explains the idea; the numbers underneath explain the actual effect.", c.Width - 10, 4);
        c.Wrapped(5, 24, "We will play a decision, reroll, skip an event, clear a quota, read company news, claim boosters, buy an upgrade and begin the next month.", c.Width - 10, 4);
        c.Button((c.Width - 23) / 2, 32, "ENTER START PRACTICE", "tutorialBegin", selected: true);
        Footer(c, "ENTER: Start | ESC: Main menu | About two minutes, at your pace");
    }
    void DrawTutorialHint(Canvas c)
    {
        if (Game?.Tutorial != true || tutorialStep is < 1 or > 9 || Screen is Screen.Inspect or Screen.Pause) return;
        string meaning = tutorialStep switch
        {
            1 => "Ads attract buyers. Check projected profit before spending.",
            2 => "Rerolls replace choices, not your stats. You begin with one.",
            3 => "No need to spend every decision. E moves to events.",
            4 => "Events are optional. E leaves all three; ENTER takes one.",
            5 => "Green PASS means every quota was met. Profit is not cash.",
            6 => "Company news is mandatory and changes the next month.",
            7 => "Choose one free booster. This demo gifts the other color too.",
            8 => "Decorations start small. ENTER can buy the next upgrade tier.",
            _ => "SPACE leaves and starts next month. Stats and boosts carry over."
        };
        int y = Screen is Screen.Table or Screen.Events ? 7 : Screen == Screen.Booster ? 6 : Screen == Screen.Shop ? 34 : 32;
        c.Wrapped(3, y, $"PRACTICE {tutorialStep}/9: {TutorialInstruction}", c.Width - 6, 1, ConsoleColor.Green);
        c.Wrapped(3, y + 1, meaning, c.Width - 6, 2, ConsoleColor.Yellow);
    }
    void DrawTutorialComplete(Canvas c)
    {
        Header(c, "TUTORIAL COMPLETE", "You ran a month and prepared the next one.");
        c.Wrapped(5, 7, "Look right: month two has a bigger quota, last month's actual earnings, and both your green and purple boosters. Your lamp also stays active.", c.Width - 10, 4, ConsoleColor.Green);
        DrawStats(c);
        c.Wrapped(5, 14, "Clear twelve quotas to win, then choose Endless if you want more. Shop items have three normal tiers and two extra endless tiers. Targets grow 35% each endless month.", c.Width - 10, 4);
        c.Wrapped(5, 20, "Common cards show up often. Uncommon, Rare, Epic and Legendary cards appear less often. Rarer is exciting, but read the cost before playing.", c.Width - 10, 4);
        c.Wrapped(5, 24, "I opens the full card explanation. Click a side-panel booster to inspect it. Page Up/Down scrolls long booster lists. ESC pauses; Space clears a selection.", c.Width - 10, 4);
        c.Button((c.Width - 22) / 2, 35, "ENTER LEARNING GUIDE", "tutorialFinish", selected: true);
        Footer(c, "ENTER: Learning Guide | ESC: Main menu | Ready for a twelve-month run");
    }
}

