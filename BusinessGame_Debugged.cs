using System.Globalization;

namespace BusinessGame;

public static class Program
{
    public static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            List<Course> courses = Catalog.Load();
            if (args.Contains("--self-test")) { Verification.SelfTest(courses); return 0; }
            if (args.Contains("--preview")) { Verification.Preview(courses); return 0; }
            if (Console.IsInputRedirected || Console.IsOutputRedirected)
            { Console.WriteLine("Run this game in an interactive terminal, or use --self-test."); return 1; }
            TryResize();
            using var input = new TerminalInput();
            var app = new GameApp(courses, new Storage());
            Console.Clear();
            while (!app.Quit)
            {
                int width = Math.Min(120, Console.WindowWidth), height = Math.Min(38, Console.WindowHeight);
                Canvas frame = app.Render(width, height);
                try { frame.Flush(); }
                catch (ArgumentOutOfRangeException) { continue; } // The window was resized during drawing.
                Input next = input.Read();
                if (width < 80 || height < 30)
                {
                    if (next.Key is ConsoleKey.Escape or ConsoleKey.Q) app.Exit();
                    continue;
                }
                app.Handle(next, frame);
            }
            Console.Clear();
            return 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Text.Json.JsonException)
        { Console.WriteLine("The game could not start: " + ex.Message); return 1; }
    }
    static void TryResize()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            int width = Math.Max(Console.WindowWidth, Math.Min(100, Console.LargestWindowWidth));
            int height = Math.Max(Console.WindowHeight, Math.Min(34, Console.LargestWindowHeight));
            if (Console.BufferWidth < width || Console.BufferHeight < height)
                Console.SetBufferSize(Math.Max(Console.BufferWidth, width), Math.Max(Console.BufferHeight, height));
            Console.SetWindowSize(width, height);
        }
        catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException or PlatformNotSupportedException) { }
    }
}

public enum Screen { Main, Courses, Topics, Difficulty, Table, Events, Result, Booster, Shop, Reward, End, Pause, History, Settings, Guide, Analytics, Briefing }

public sealed class GameApp
{
    public Screen Screen { get; private set; } = Screen.Main;
    public Run? Game { get; private set; }
    public bool Quit { get; private set; }
    readonly List<Course> courses;
    readonly Storage storage;
    int menuIndex, courseIndex, topicIndex, difficultyIndex, selected = -1, shopIndex;
    bool browsing, recorded;
    string notice = "";
    Screen beforePause;
    static readonly string[] Menu = ["Student / Team - PLAY", "Quick Demo - FreshFizz", "Course Library", "Previous Results", "Settings", "Lecturer / Learning Guide", "Product Analytics", "Exit"];

    public GameApp(List<Course> courses, Storage storage)
    { this.courses = courses; this.storage = storage; difficultyIndex = (int)storage.Preferences.Difficulty; }
    public void Exit() => Quit = true;

    public Canvas Render(int width, int height)
    {
        var c = new Canvas(width, height);
        if (width < 80 || height < 30)
        {
            c.Center(Math.Max(0, height / 2 - 1), "Please resize this terminal to at least 80 x 30.", ConsoleColor.Yellow);
            c.Center(Math.Max(1, height / 2 + 1), "ESC: Exit | Recommended: 100 x 34");
            return c;
        }
        switch (Screen)
        {
            case Screen.Main: DrawMain(c); break;
            case Screen.Courses: DrawCourses(c); break;
            case Screen.Topics: DrawTopics(c); break;
            case Screen.Difficulty: DrawDifficulty(c); break;
            case Screen.Table: DrawTable(c, false); break;
            case Screen.Events: DrawTable(c, true); break;
            case Screen.Result: DrawResult(c); break;
            case Screen.Booster: DrawBooster(c); break;
            case Screen.Shop: DrawShop(c); break;
            case Screen.Reward: DrawReward(c); break;
            case Screen.End: DrawEnd(c); break;
            case Screen.Pause: DrawPause(c); break;
            case Screen.History: DrawHistory(c); break;
            case Screen.Settings: DrawSettings(c); break;
            case Screen.Guide: DrawGuide(c); break;
            case Screen.Analytics: DrawAnalytics(c); break;
            case Screen.Briefing: DrawBriefing(c); break;
        }
        if (notice.Length > 0) c.Text(3, height - 3, notice.Length > width - 6 ? notice[..(width - 6)] : notice, ConsoleColor.Yellow);
        else if (storage.Notice.Length > 0) c.Text(3, height - 3, storage.Notice, ConsoleColor.Yellow);
        return c;
    }
    public void Handle(Input input, Canvas frame)
    {
        notice = "";
        if (input.X >= 0)
        {
            HitBox? hit = frame.Buttons.LastOrDefault(b => input.X >= b.X && input.X < b.X + b.Width && input.Y >= b.Y && input.Y < b.Y + b.Height);
            if (hit != null) Activate(hit.Action, hit.Index);
            return;
        }
        ConsoleKey key = input.Key;
        if (key == ConsoleKey.NoName) return;
        if (key == ConsoleKey.Escape) { Back(); return; }
        int digit = Digit(key);
        if (Screen == Screen.Main)
        {
            menuIndex = Navigate(key, menuIndex, Menu.Length);
            if (digit >= 0 && digit < Menu.Length) { menuIndex = digit; Activate("menu", menuIndex); }
            else if (key == ConsoleKey.Enter) Activate("menu", menuIndex);
            return;
        }
        if (Screen == Screen.Courses)
        {
            courseIndex = GridNavigate(key, courseIndex, courses.Count, 3);
            if (digit >= 0 && digit < courses.Count) courseIndex = digit;
            if (key == ConsoleKey.Enter) Activate("course", courseIndex);
            return;
        }
        if (Screen == Screen.Topics)
        {
            topicIndex = Navigate(key, topicIndex, 10);
            if (digit >= 0) topicIndex = digit;
            if (key == ConsoleKey.Enter) Activate("topic", topicIndex);
            return;
        }
        if (Screen == Screen.Difficulty)
        {
            difficultyIndex = Navigate(key, difficultyIndex, 3);
            if (digit >= 0 && digit < 3) difficultyIndex = digit;
            if (key == ConsoleKey.Enter) StartRun();
            return;
        }
        if (Screen is Screen.Table or Screen.Events or Screen.Booster)
        {
            selected = Navigate(key, selected, 3);
            if (digit >= 0 && digit < 3) selected = digit;
            if (key == ConsoleKey.Spacebar) selected = -1;
            if (key == ConsoleKey.Enter) Activate("play");
            else if (key == ConsoleKey.R) Activate("reroll");
            else if (key == ConsoleKey.E) Activate("skip");
            else if (key == ConsoleKey.S) notice = "The shop opens after clearing the month and choosing a booster.";
            return;
        }
        if (Screen == Screen.Shop)
        {
            shopIndex = GridNavigate(key, shopIndex, 6, 3);
            if (digit >= 0 && digit < 6) shopIndex = digit;
            if (key == ConsoleKey.Enter) Activate("buy", shopIndex);
            else if (key == ConsoleKey.S) Activate("shopBack");
            else if (key == ConsoleKey.N) Activate("next");
            return;
        }
        if (Screen == Screen.Reward)
        {
            if (key == ConsoleKey.S) Activate("shop");
            else if (key is ConsoleKey.N or ConsoleKey.Enter) Activate("next");
            return;
        }
        if (Screen == Screen.Result && key == ConsoleKey.Enter) Activate("continue");
        else if (Screen == Screen.End)
        { if (key == ConsoleKey.R) StartRun(); else if (key == ConsoleKey.Enter) ReturnMain(); }
        else if (Screen == Screen.Pause)
        { if (key is ConsoleKey.Enter or ConsoleKey.R) Screen = beforePause; else if (key == ConsoleKey.M) ReturnMain(); }
        else if (Screen == Screen.Settings)
        {
            if (digit is >= 0 and < 3) { storage.Preferences.Difficulty = (Difficulty)digit; difficultyIndex = digit; }
            storage.SaveSettings();
        }
        else if (Screen == Screen.Briefing && key == ConsoleKey.Enter)
        { browsing = false; Screen = Screen.Difficulty; }
    }
    void Activate(string action, int index = -1)
    {
        switch (action)
        {
            case "menu":
                menuIndex = index;
                switch (index)
                {
                    case 0: browsing = false; Screen = Screen.Courses; break;
                    case 1: courseIndex = topicIndex = 0; difficultyIndex = (int)Difficulty.Easy; StartRun(); break;
                    case 2: browsing = true; Screen = Screen.Courses; break;
                    case 3: Screen = Screen.History; break;
                    case 4: Screen = Screen.Settings; break;
                    case 5: Screen = Screen.Guide; break;
                    case 6: Screen = Screen.Analytics; break;
                    case 7: Quit = true; break;
                }
                break;
            case "course": courseIndex = index; topicIndex = 0; Screen = Screen.Topics; break;
            case "topic": topicIndex = index; Screen = browsing ? Screen.Briefing : Screen.Difficulty; break;
            case "difficulty": difficultyIndex = index; break;
            case "start": StartRun(); break;
            case "select": selected = index; break;
            case "play":
                if (Game == null || selected < 0) { notice = "Select a card first."; break; }
                bool played = Game.Phase switch
                {
                    Phase.Decisions => Game.Play(selected), Phase.Events => Game.TakeEvent(selected),
                    Phase.Booster => Game.ClaimBooster(selected), _ => false
                };
                if (played) { selected = -1; SyncScreen(); }
                break;
            case "reroll":
                if (Game?.Reroll() == true) selected = -1;
                else notice = "No rerolls available in this phase.";
                break;
            case "skip":
                if (Game == null) break;
                if (Game.Phase == Phase.Decisions) Game.FinishDecisions();
                else if (Game.Phase == Phase.Events) Game.SkipEvents();
                else if (Game.Phase == Phase.Booster) Game.SkipBooster();
                selected = -1; SyncScreen(); break;
            case "continue": Game?.ContinueResult(); SyncScreen(); break;
            case "buy": Game?.Buy(index); break;
            case "shopSelect": shopIndex = index; break;
            case "lockedShop": notice = "The shop opens after clearing the month and choosing a booster."; break;
            case "shopBack": Screen = Screen.Reward; break;
            case "shop": if (Game?.Phase == Phase.Shop) Screen = Screen.Shop; break;
            case "next": if (Game?.NextMonth() == true) { selected = -1; SyncScreen(); } break;
            case "resume": Screen = beforePause; break;
            case "main": ReturnMain(); break;
            case "retry": StartRun(); break;
            case "back": Back(); break;
            case "briefingPlay": browsing = false; Screen = Screen.Difficulty; break;
            case "settingDifficulty": storage.Preferences.Difficulty = (Difficulty)index; difficultyIndex = index; storage.SaveSettings(); break;
        }
    }
    void StartRun()
    {
        Game = new Run(courses[courseIndex], topicIndex, (Difficulty)difficultyIndex, Random.Shared.Next());
        selected = -1; shopIndex = 0; recorded = false; SyncScreen();
    }
    void SyncScreen()
    {
        if (Game == null) return;
        Screen = Game.Phase switch { Phase.Decisions => Screen.Table, Phase.Events => Screen.Events,
            Phase.Result => Screen.Result, Phase.Booster => Screen.Booster, Phase.Shop => Screen.Shop,
            _ => Screen.End };
        if (Game.Phase is Phase.Won or Phase.Lost && !recorded)
        { storage.Record(Game); recorded = true; }
    }
    void Back()
    {
        switch (Screen)
        {
            case Screen.Main: Quit = true; break;
            case Screen.Courses: ReturnMain(); break;
            case Screen.Topics: Screen = Screen.Courses; break;
            case Screen.Difficulty: Screen = Screen.Topics; break;
            case Screen.Briefing: Screen = Screen.Topics; break;
            case Screen.Shop: Screen = Screen.Reward; break;
            case Screen.Pause: Screen = beforePause; break;
            case Screen.End: ReturnMain(); break;
            case Screen.Table: case Screen.Events: case Screen.Result: case Screen.Booster: case Screen.Reward:
                beforePause = Screen; Screen = Screen.Pause; break;
            default: ReturnMain(); break;
        }
    }
    void ReturnMain() { Game = null; Screen = Screen.Main; notice = ""; }
    static int Digit(ConsoleKey key) => key is >= ConsoleKey.D1 and <= ConsoleKey.D9 ? key - ConsoleKey.D1
        : key is >= ConsoleKey.NumPad1 and <= ConsoleKey.NumPad9 ? key - ConsoleKey.NumPad1
        : key is ConsoleKey.D0 or ConsoleKey.NumPad0 ? 9 : -1;
    static int Navigate(ConsoleKey key, int value, int count) => key switch
    {
        ConsoleKey.LeftArrow or ConsoleKey.UpArrow => Math.Max(0, value - 1),
        ConsoleKey.RightArrow or ConsoleKey.DownArrow => Math.Min(count - 1, value + 1), _ => value
    };
    static int GridNavigate(ConsoleKey key, int value, int count, int columns) => key switch
    {
        ConsoleKey.UpArrow => Math.Max(0, value - columns), ConsoleKey.DownArrow => Math.Min(count - 1, value + columns),
        _ => Navigate(key, value, count)
    };
    static void Header(Canvas c, string title, string subtitle = "")
    {
        c.Center(1, title, ConsoleColor.Cyan); c.Rule(2);
        if (subtitle.Length > 0) c.Center(3, subtitle, ConsoleColor.White);
    }
    static void Footer(Canvas c, string text) { c.Rule(c.Height - 2); c.Text(3, c.Height - 1, text); }
    void DrawMain(Canvas c)
    {
        c.Center(1, ".------------------------------------------------------.", ConsoleColor.DarkCyan);
        c.Center(2, "|              B U S I N E S S G A M E                 |", ConsoleColor.Cyan);
        c.Center(3, "'------------------------------------------------------'", ConsoleColor.DarkCyan);
        c.Center(5, "THE ECONFORGE EDITION", ConsoleColor.Yellow);
        c.Center(6, "One company. Twelve months. Every decision counts.");
        for (int i = 0; i < Menu.Length; i++) c.Button(6, 9 + i * 2, (i + 1) + ". " + Menu[i], "menu", i, i == menuIndex);
        int x = c.Width - 29;
        c.Box(x, 10, 22, 11, ConsoleColor.DarkCyan);
        c.Text(x + 4, 12, "  /\\_/\\", ConsoleColor.Yellow);
        c.Text(x + 4, 13, "  (o.o)", ConsoleColor.Yellow);
        c.Text(x + 3, 16, "12 MONTH QUOTAS", ConsoleColor.White);
        c.Text(x + 3, 18, "60 TOPIC DECKS", ConsoleColor.Cyan);
        c.Text(x + 3, 19, "3 DIFFICULTY CHIPS", ConsoleColor.Red);
        Footer(c, "Arrows: Navigate | ENTER: Choose | 1-8 / mouse: Open | ESC: Exit");
    }
    void DrawCourses(Canvas c)
    {
        Header(c, browsing ? "COURSE LIBRARY" : "SELECT COURSE", "Choose the subject of your company run");
        int cardWidth = (c.Width - 8) / 3;
        for (int i = 0; i < courses.Count; i++)
        {
            int x = 3 + i % 3 * (cardWidth + 1), y = 6 + i / 3 * 9;
            ConsoleColor color = i == courseIndex ? ConsoleColor.Yellow : ConsoleColor.DarkCyan;
            c.Box(x, y, cardWidth, 8, color);
            c.Wrapped(x + 2, y + 1, $"{i + 1}. {courses[i].Name}", cardWidth - 4, 2, color);
            c.Text(x + 2, y + 4, "10 topic levels");
            c.Text(x + 2, y + 5, "12 months per run");
            c.Buttons.Add(new(x, y, cardWidth, 8, "course", i));
        }
        c.Center(25, "Later topics add harder quotas and more responsibilities.");
        Footer(c, "Arrows / 1-6: Highlight | ENTER / mouse: Open course | ESC: Back");
    }
    void DrawTopics(Canvas c)
    {
        Course course = courses[courseIndex];
        Header(c, "SELECT TOPIC / LEVEL", course.Name);
        for (int i = 0; i < 10; i++)
        {
            string tier = i >= 9 ? "EXPERT" : i >= 6 ? "ADVANCED" : i >= 3 ? "INTERMEDIATE" : "FOUNDATION";
            c.Button(4, 5 + i * 2, $"{(i + 1),2}. {course.Lessons[i].Name}", "topic", i, i == topicIndex);
            c.Text(c.Width - 17, 5 + i * 2, tier, i >= 6 ? ConsoleColor.Red : ConsoleColor.DarkCyan);
        }
        Footer(c, "Arrows / 1-9, 0=10: Highlight | ENTER / mouse: Select | ESC: Courses");
    }
    void DrawDifficulty(Canvas c)
    {
        Header(c, "CHOOSE YOUR DIFFICULTY CHIP", courses[courseIndex].Name);
        c.Center(4, $"Level {topicIndex + 1}: {courses[courseIndex].Lessons[topicIndex].Name}", ConsoleColor.Yellow);
        int gap = (c.Width - 72) / 2;
        for (int i = 0; i < 3; i++)
        {
            int x = gap + i * 24, y = i == difficultyIndex ? 8 : 10;
            ConsoleColor color = i == difficultyIndex ? ConsoleColor.Yellow : i == 0 ? ConsoleColor.White : i == 1 ? ConsoleColor.Red : ConsoleColor.Blue;
            c.Text(x + 3, y, "   .--------.", color);
            c.Text(x + 3, y + 1, "  / //    \\ \\", color);
            c.Text(x + 3, y + 2, $" |  {((Difficulty)i).ToString().ToUpper(),-7}  |", color);
            c.Text(x + 3, y + 3, "  \\ \\    // /", color);
            c.Text(x + 3, y + 4, "   '--------'", color);
            c.Text(x + 2, y + 6, i == 2 ? "2 decisions/month" : "3 decisions/month");
            c.Text(x + 2, y + 7, $"{(i == 0 ? 4 : i == 1 ? 3 : 2)} rerolls/month");
            c.Text(x + 2, y + 8, i == 0 ? "Lower profit quotas" : i == 1 ? "Standard quotas" : "Tighter quotas");
            c.Buttons.Add(new(x, y, 22, 10, "difficulty", i));
        }
        c.Button((c.Width - 19) / 2, 23, "START 12-MONTH RUN", "start", selected: true);
        Footer(c, "Arrows / 1-3 / mouse: Choose chip | ENTER: Start | ESC: Topics");
    }
    void DrawTable(Canvas c, bool events)
    {
        Run g = Game!; Forecast forecast = g.Preview();
        Header(c, events ? "EVENT PHASE - TAKE IT OR LEAVE IT" : "DECISION TABLE", $"{g.Course.Name} / {g.Lesson.Name}");
        c.Text(3, 4, $"MONTH {g.Month}/12   Quotas met: {g.Cleared}/12   {g.Difficulty}", ConsoleColor.Yellow);
        c.Text(3, 5, $"Decisions left: {g.Actions}   Rerolls: {g.Rerolls}   Projected profit: ${forecast.Profit:N0}");
        string quotas = string.Join(" | ", g.Quotas().Select(q => $"{q.Name} {(q.AtLeast ? ">=" : "<=")} {Metric(q.Name, q.Target)}"));
        c.Wrapped(3, 6, "MONTH QUOTAS: " + quotas, c.Width - 6, 2, ConsoleColor.Cyan);
        for (int i = 0; i < 3; i++)
        {
            string name = events ? g.Events[i].Name : g.Hand[i].Name;
            string[] lines = events ? Wrap(g.Events[i].Description, 19).Take(6).ToArray() : g.Describe(g.Hand[i].Kind);
            DrawCard(c, i, name, lines, i == selected, events ? ConsoleColor.Magenta : ConsoleColor.Cyan);
        }
        DrawStats(c);
        c.Button(3, 25, $"R {g.Rerolls}", "reroll");
        c.Button(c.Width - 10, 25, "S", "lockedShop");
        c.Button(15, 25, events ? "E LEAVE ALL" : "E END DECISIONS", "skip");
        c.Button(15, 26, events ? "ENTER TAKE" : "ENTER PLAY", "play");
        c.Text(3, c.Height - 3, g.Message, ConsoleColor.Yellow);
        Footer(c, "Arrows/1-3: Select | SPACE: Clear | ENTER: Play | R: Reroll | ESC: Pause");
    }
    void DrawCard(Canvas c, int index, string title, string[] lines, bool raised, ConsoleColor color)
    {
        int x = Math.Max(3, (c.Width - 72) / 2) + index * 24;
        int y = raised ? 8 : 10; // Balatro-style selection lift: exactly two rows.
        c.Box(x, y, 22, 11, raised ? ConsoleColor.Yellow : color);
        c.Text(x + 2, y + 1, (index + 1) + ".", ConsoleColor.White);
        c.Wrapped(x + 2, y + 2, title, 18, 2, raised ? ConsoleColor.Yellow : ConsoleColor.White);
        for (int row = 0; row < Math.Min(6, lines.Length); row++) c.Text(x + 2, y + 4 + row, lines[row].Length > 18 ? lines[row][..18] : lines[row]);
        c.Buttons.Add(new(x, y, 22, 11, "select", index));
    }
    void DrawStats(Canvas c)
    {
        Run g = Game!;
        c.Rule(21);
        c.Text(3, 22, $"CASH ${g.Cash:N0}  |  DEBT ${g.Debt:N0}  |  STOCK {g.Inventory}  |  CAPACITY {g.Capacity}");
        c.Text(3, 23, $"PRICE ${g.Price:F2} | UNIT COST ${g.UnitCost:F2} | SHARE {g.Share:F1}% | QUALITY {g.Quality:F0}");
        c.Text(3, 24, $"SERVICE {g.Preview().Service:F0}% | RISK {g.Risk:F0} | Upgrades {g.Owned.Count} | Boosters {g.ActiveBoosts.Count}");
    }
    void DrawResult(Canvas c)
    {
        Run g = Game!; MonthReport r = g.LastReport!; bool passed = r.Failures.Count == 0;
        Header(c, passed ? "MONTH CLEARED" : "QUOTA MISSED", $"Month {r.Month} / 12");
        c.Center(5, passed ? $"{g.Cleared} OF 12 QUOTAS MET" : "Every quota must be met to continue.", passed ? ConsoleColor.Green : ConsoleColor.Red);
        c.Text(6, 7, $"Units sold: {r.Sold}     Revenue: ${r.Revenue:N0}     Profit: ${r.Profit:N0}");
        c.Text(6, 8, $"Cash before reward: ${r.Cash:N0}    Service: {r.Service:F1}%");
        int y = 11;
        foreach (Quota quota in g.Quotas())
        {
            double value = quota.Name switch { "Profit" => r.Profit, "Service" => r.Service, "Cash" => r.Cash,
                "Share" => r.Share, "Debt" => r.Debt, "Quality" => r.Quality, "Risk" => r.Risk, _ => 0 };
            bool okay = !r.Failures.Contains(quota.Name);
            c.Text(6, y++, $"[{(okay ? "PASS" : "MISS")}] {quota.Name}: {Metric(quota.Name, value)} / {(quota.AtLeast ? ">=" : "<=")} {Metric(quota.Name, quota.Target)}", okay ? ConsoleColor.Green : ConsoleColor.Red);
        }
        c.Center(20, passed ? g.Cleared == 12 ? "The twelfth month is cleared. Your company made it!"
            : "+$200 clear bonus. Next: a free booster and the decoration shop."
            : "Missed: " + string.Join(", ", r.Failures));
        c.Button((c.Width - 16) / 2, 23, "ENTER CONTINUE", "continue", selected: true);
        Footer(c, "ENTER / mouse: Continue | ESC: Pause");
    }
    void DrawBooster(Canvas c)
    {
        Run g = Game!;
        Header(c, "MONTHLY BOOSTER PACK", $"Month {g.Month} reward - choose one free booster");
        c.Center(5, "One choice. Lasting help for the months ahead.", ConsoleColor.Yellow);
        for (int i = 0; i < 3; i++) DrawCard(c, i, g.Boosters[i].Name, Wrap(g.Boosters[i].Description, 18).ToArray(), selected == i, ConsoleColor.Green);
        c.Button(5, 24, "ENTER CLAIM + SHOP", "play");
        c.Button(c.Width - 25, 24, "E SKIP + SHOP", "skip");
        Footer(c, "Arrows / 1-3 / mouse: Select | ENTER: Claim | E: Skip booster | ESC: Pause");
    }
    void DrawShop(Canvas c)
    {
        Run g = Game!;
        Header(c, "OFFICE ODDITIES - DECORATION SHOP", $"Cash ${g.Cash:N0} | Upgrades last for this run");
        int left = (c.Width - 72) / 2;
        for (int i = 0; i < 6; i++)
        {
            Decoration item = Run.Decorations[i]; int x = left + i % 3 * 24, y = 5 + i / 3 * 7;
            ConsoleColor color = g.Owned.Contains(i) ? ConsoleColor.DarkGray : i == shopIndex ? ConsoleColor.Yellow : ConsoleColor.White;
            for (int row = 0; row < 3; row++) c.Text(x + 8, y + row, item.Art[row], color);
            c.Text(x, y + 3, $"{i + 1}. {item.Name}", color);
            c.Text(x, y + 4, g.Owned.Contains(i) ? "OWNED" : "$" + item.Cost, color);
            c.Text(x, y + 5, item.Boost, color);
            c.Text(x, y + 6, "======================", ConsoleColor.DarkYellow);
            c.Buttons.Add(new(x, y, 22, 6, "shopSelect", i));
        }
        c.Text(left + 2, 20, "  .----.    SHOPKEEPER: A little style goes a long way.", ConsoleColor.White);
        c.Text(left + 2, 21, "  |o  o|    Choose a decoration, then ENTER to buy.");
        c.Text(left + 2, 22, " /| -- |\\   New month, same helpful office!");
        c.Rule(23, ConsoleColor.DarkYellow);
        c.Button(left, 24, "ENTER BUY", "buy", shopIndex);
        c.Button(left + 19, 24, "S BACK", "shopBack");
        c.Button(left + 34, 24, "N NEXT MONTH", "next");
        c.Text(3, c.Height - 3, g.Message, ConsoleColor.Yellow);
        Footer(c, "Arrows/1-6: Select | ENTER: Buy | S/ESC: Leave shop | N: Next month");
    }
    void DrawReward(Canvas c)
    {
        Run g = Game!;
        Header(c, "BETWEEN MONTHS", $"{g.Cleared}/12 quotas cleared | Cash ${g.Cash:N0}");
        c.Center(8, g.Message, ConsoleColor.Yellow);
        c.Center(11, "Your booster choice has been settled for this month.");
        c.Center(13, "Visit the shop again, or begin the next month.");
        c.Button((c.Width - 22) / 2, 18, "S DECORATION SHOP", "shop");
        c.Button((c.Width - 22) / 2, 21, "N NEXT MONTH", "next", selected: true);
        Footer(c, "S: Shop | N / ENTER: Next month | ESC: Pause");
    }
    void DrawEnd(Canvas c)
    {
        Run g = Game!; bool won = g.Phase == Phase.Won;
        Header(c, won ? "YOU WIN - A YEAR WELL RUN" : "RUN OVER", g.Course.Name + " / " + g.Lesson.Name);
        c.Center(7, won ? "* * *  ALL 12 MONTHLY QUOTAS CLEARED  * * *" : $"{g.Cleared}/12 months cleared", won ? ConsoleColor.Green : ConsoleColor.Red);
        c.Center(10, $"{g.Difficulty} chip | Total operating profit ${g.Reports.Sum(r => r.Profit):N0}");
        c.Center(12, $"Cash ${g.Cash:N0} | Decorations {g.Owned.Count} | Boosters {g.ActiveBoosts.Count}");
        if (!won) c.Center(15, "Missed: " + string.Join(", ", g.LastReport!.Failures));
        c.Button((c.Width - 23) / 2, 20, "R RETRY SAME TOPIC", "retry");
        c.Button((c.Width - 23) / 2, 23, "ENTER MAIN MENU", "main", selected: true);
        Footer(c, "R: Retry | ENTER / ESC: Main menu");
    }
    void DrawPause(Canvas c)
    {
        Header(c, "RUN PAUSED", "Your company is waiting.");
        c.Center(10, "Returning to the menu abandons this unfinished run.");
        c.Center(12, "Completed run results are saved; active runs are not saved.");
        c.Button((c.Width - 21) / 2, 18, "ENTER RESUME", "resume", selected: true);
        c.Button((c.Width - 21) / 2, 21, "M ABANDON TO MENU", "main");
        Footer(c, "ENTER / R / ESC: Resume | M: Abandon to main menu");
    }
    void DrawHistory(Canvas c)
    {
        Header(c, "PREVIOUS RESULTS", "Completed attempts saved locally");
        var recent = storage.History.TakeLast(9).Reverse().ToList();
        if (recent.Count == 0) c.Center(10, "No finished runs yet. Start your first company.");
        for (int i = 0; i < recent.Count; i++)
        {
            RunRecord r = recent[i];
            c.Text(4, 6 + i * 2, $"{(r.Won ? "WIN " : "LOSS")} {r.Cleared,2}/12 | {r.Difficulty,-6} | {r.Course} / {r.Topic}", r.Won ? ConsoleColor.Green : ConsoleColor.Gray);
            c.Text(4, 7 + i * 2, $"{r.Finished:yyyy-MM-dd HH:mm} | Profit ${r.Profit:N0}", ConsoleColor.DarkGray);
        }
        Footer(c, "ESC: Main menu");
    }
    void DrawSettings(Canvas c)
    {
        Header(c, "SETTINGS", "Choose your default difficulty");
        for (int i = 0; i < 3; i++) c.Button(8, 8 + i * 3, $"{i + 1}. {(Difficulty)i}", "settingDifficulty", i, (int)storage.Preferences.Difficulty == i);
        c.Text(8, 19, "Keyboard and Windows console mouse input are both supported.");
        c.Text(8, 21, "Settings are saved beside the game in data/settings.json.");
        Footer(c, "1-3 / mouse: Default difficulty | ESC: Main menu");
    }
    void DrawGuide(Canvas c)
    {
        Header(c, "LECTURER / LEARNING GUIDE", "A card game about business trade-offs");
        string[] lines =
        [
            "1. Select one of six courses, then one of its ten topic levels.",
            "2. Choose an Easy, Normal, or Hard chip. Every run lasts 12 months.",
            "3. Each month, use decisions to change price, ads, stock, costs and risk.",
            "4. Select a card with arrows, 1-3, or a mouse click. ENTER plays it.",
            "5. R rerolls even with no selected card. E ends decisions early.",
            "6. In the event phase, take ONE card, or E to leave all three.",
            "7. Monthly sales settle automatically. Meet EVERY displayed quota.",
            "8. After a clear, choose one FREE booster from three random cards.",
            "9. Shop for persistent decorations. S leaves; N starts the next month.",
            "10. Meet the twelfth monthly quota to win. One missed month ends the run.",
            "Profit includes sold-stock costs, ads, overhead, interest and events.",
            "Stock purchases use cash now; unsold stock remains for later months."
        ];
        for (int i = 0; i < lines.Length; i++) c.Text(4, 5 + i * 2, lines[i]);
        Footer(c, "Later topics add service, cash-buffer and quality quotas. ESC: Main menu");
    }
    void DrawAnalytics(Canvas c)
    {
        Header(c, "PRODUCT ANALYTICS", "Your local company runs");
        c.Text(8, 8, $"Finished runs: {storage.History.Count}");
        c.Text(8, 11, $"Wins: {storage.History.Count(r => r.Won)}");
        c.Text(8, 14, $"Months cleared: {storage.History.Sum(r => r.Cleared)}");
        c.Text(8, 17, $"Courses played: {storage.History.Select(r => r.Course).Distinct().Count()} / 6");
        c.Text(8, 20, $"Topics played: {storage.History.Select(r => (r.Course, r.Topic)).Distinct().Count()} / 60");
        Footer(c, "ESC: Main menu");
    }
    void DrawBriefing(Canvas c)
    {
        Lesson lesson = courses[courseIndex].Lessons[topicIndex];
        Header(c, "COURSE LIBRARY - TOPIC BRIEFING", courses[courseIndex].Name + " / " + lesson.Name);
        c.Text(4, 5, "THEORY FROM ECONFORGE", ConsoleColor.Yellow);
        c.Wrapped(4, 7, lesson.Simulation.Theory, c.Width - 8, 5);
        c.Text(4, 13, "STRATEGY", ConsoleColor.Yellow);
        c.Wrapped(4, 15, lesson.Simulation.Strategy, c.Width - 8, 4);
        c.Wrapped(4, 20, "Original decision themes: " + string.Join(", ", lesson.Simulation.Decisions.Select(d => d.Name)), c.Width - 8, 3);
        c.Button((c.Width - 20) / 2, 24, "ENTER PLAY TOPIC", "briefingPlay");
        Footer(c, "ENTER: Choose difficulty | ESC: Topics");
    }
    static string Metric(string name, double value) => name is "Profit" or "Cash" or "Debt" ? $"${value:N0}"
        : name is "Share" or "Service" ? $"{value:F1}%" : $"{value:F0}";
    static IEnumerable<string> Wrap(string text, int width)
    {
        string line = "";
        foreach (string word in text.Split(' '))
        {
            if (line.Length + word.Length + 1 > width && line.Length > 0) { yield return line; line = ""; }
            line += (line.Length == 0 ? "" : " ") + word;
        }
        if (line.Length > 0) yield return line;
    }
}
