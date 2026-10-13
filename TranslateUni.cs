using System.Globalization;

namespace TranslateUni;

public static class Program
{
    public static int Main(string[] args)
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        try
        {
            List<Course> courses = Catalog.Load();
            if (args.Contains("--self-test")) { Verification.SelfTest(courses); return 0; }
            if (args.Contains("--preview")) { Verification.Preview(courses); return 0; }
            DisplayDiagnostics.Capture();
            if(args.Contains("--terminal-self-test"))
            {
                if(FullscreenLauncher.RelaunchIfAvailable(args)) return 0;
                string report=Path.Combine(AppContext.BaseDirectory,"data","terminal-self-test.txt");
                try
                {
                    using var testInput=new TerminalInput(); using var testTheme=new TerminalTheme();
                    string check=testInput.VerifyQueueBoundary();
                    bool visible=OperatingSystem.IsWindows() && NativeDisplay.IsWindowVisible(NativeDisplay.GetConsoleWindow());
                    if(visible && !testTheme.PaletteApplied) throw new InvalidOperationException("Muted console palette was not applied.");
                    Directory.CreateDirectory(Path.GetDirectoryName(report)!);
                    File.WriteAllText(report,check+"\nMuted classic-console palette verified: "+testTheme.PaletteApplied);
                    FullscreenLauncher.Acknowledge(args,true); return 0;
                }
                catch(Exception ex)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(report)!); File.WriteAllText(report,"FAIL: "+ex.Message);
                    FullscreenLauncher.Acknowledge(args,false); throw;
                }
            }
            if (args.Contains("--diagnose-display"))
            {
                DisplayDiagnostics.Save();
                Console.WriteLine(DisplayDiagnostics.Capture());
                Console.WriteLine("Report: " + DisplayDiagnostics.ReportPath);
                return 0;
            }
            if (args.Contains("--fullscreen-test"))
            {
                if (FullscreenLauncher.RelaunchIfAvailable(args)) return 0;
                using var testFullscreen = new ConsoleFullscreen(args.Contains("--windowed") || args.Contains("--host-fullscreen"));
                DisplayDiagnostics.LogConsoleSize();
                DisplayDiagnostics.Save();
                FullscreenLauncher.Acknowledge(args, testFullscreen.Applied || args.Contains("--host-fullscreen"));
                return testFullscreen.Applied ? 0 : 1;
            }
            if (Console.IsInputRedirected || Console.IsOutputRedirected)
            { Console.WriteLine("Run this game in an interactive terminal, or use --self-test."); return 1; }
            if (FullscreenLauncher.RelaunchIfAvailable(args)) return 0;
            using var fullscreen = new ConsoleFullscreen(args.Contains("--windowed") || args.Contains("--host-fullscreen"));
            // Resizing after entering fullscreen can switch a classic console back to windowed mode.
            if (!fullscreen.Applied && !args.Contains("--host-fullscreen")) TryResize();
            DisplayDiagnostics.LogConsoleSize();
            DisplayDiagnostics.Save();
            using var input = new TerminalInput();
            using var theme = new TerminalTheme();
            Console.Title = "TranslateUni";
            var app = new GameApp(courses, new Storage());
            Console.Clear();
            FullscreenLauncher.Acknowledge(args, true);
            while (!app.Quit)
            {
                int width = Console.WindowWidth, height = Console.WindowHeight;
                Canvas frame = app.Render(width, height);
                try { frame.Flush(); }
                catch (ArgumentOutOfRangeException) { continue; } // The window was resized during drawing.
                Input next = input.Read();
                if (width < 108 || height < 42)
                {
                    if (next.Key is ConsoleKey.Escape or ConsoleKey.Q) app.Exit();
                    continue;
                }
                int inputVersion = app.InputVersion;
                app.Handle(next, frame);
                app.PlayPendingAnimation();
                if(app.InputVersion != inputVersion) input.DiscardPending();
            }
            Console.Clear();
            return 0;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Text.Json.JsonException)
        { FullscreenLauncher.Acknowledge(args, false); DisplayDiagnostics.Add("Startup error: " + ex.Message); DisplayDiagnostics.Save(); Console.WriteLine("The game could not start: " + ex.Message); return 1; }
    }
    static void TryResize()
    {
        if (!OperatingSystem.IsWindows()) return;
        try
        {
            int width = Math.Max(Console.WindowWidth, Math.Min(120, Console.LargestWindowWidth));
            int height = Math.Max(Console.WindowHeight, Math.Min(42, Console.LargestWindowHeight));
            if (Console.BufferWidth < width || Console.BufferHeight < height)
                Console.SetBufferSize(Math.Max(Console.BufferWidth, width), Math.Max(Console.BufferHeight, height));
            Console.SetWindowSize(width, height);
        }
        catch (Exception ex) when (ex is IOException or ArgumentOutOfRangeException or PlatformNotSupportedException) { }
    }
}

public enum Screen { Main, Courses, Topics, Difficulty, Table, Events, Result, SpecialEvent, Booster, Shop, Reward, End, Pause, History, Settings, Guide, Analytics, Briefing, Inspect, TutorialIntro, TutorialComplete, Loans, CourseComparison }

public sealed partial class GameApp
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
    static readonly string[] Menu = ["Student / Team - PLAY", "Quick Demo - FreshFizz", "Course Library", "Previous Results", "Settings", "Learning Guide + Tutorial", "Product Analytics", "Exit"];

    public GameApp(List<Course> courses, Storage storage)
    { this.courses = courses; this.storage = storage; difficultyIndex = (int)storage.Preferences.Difficulty; }
    public void Exit() => Quit = true;

    public Canvas Render(int width, int height)
    {
        if (width > 140 || height > 46)
        {
            Canvas core = Render(Math.Min(140, width), Math.Min(46, height));
            var fullscreen = new Canvas(width, height);
            fullscreen.Paste(core, (width - core.Width) / 2, (height - core.Height) / 2);
            return fullscreen;
        }
        var c = new Canvas(Game == null ? width : width - 30, height);
        if (width < 108 || height < 42)
        {
            c = new Canvas(width, height);
            c.Center(Math.Max(0, height / 2 - 1), "Please resize this terminal to at least 108 x 42.", ConsoleColor.Yellow);
            c.Center(Math.Max(1, height / 2 + 1), "ESC: Exit | Recommended: 120 x 42");
            return c;
        }
        switch (Screen)
        {
            case Screen.Main: DrawMain(c); break;
            case Screen.Courses: DrawCourses(c); break;
            case Screen.CourseComparison: DrawCourseComparison(c); break;
            case Screen.Topics: DrawTopics(c); break;
            case Screen.Difficulty: DrawDifficulty(c); break;
            case Screen.Table: DrawTable(c, false); break;
            case Screen.Events: DrawTable(c, true); break;
            case Screen.Result: DrawResult(c); break;
            case Screen.SpecialEvent: DrawCompanyEvent(c); break;
            case Screen.Booster: DrawBooster(c); break;
            case Screen.Shop: DrawShop(c); break;
            case Screen.Loans: DrawLoans(c); break;
            case Screen.Reward: DrawReward(c); break;
            case Screen.End: DrawEnd(c); break;
            case Screen.Pause: DrawPause(c); break;
            case Screen.History: DrawHistory(c); break;
            case Screen.Settings: DrawSettings(c); break;
            case Screen.Guide: DrawGuide(c); break;
            case Screen.Analytics: DrawAnalytics(c); break;
            case Screen.Briefing: DrawBriefing(c); break;
            case Screen.Inspect: DrawInspect(c); break;
            case Screen.TutorialIntro: DrawTutorialIntro(c); break;
            case Screen.TutorialComplete: DrawTutorialComplete(c); break;
        }
        string overlay=notice.Length>0?notice:storage.Notice;
        if(overlay.Length>0)
        {
            int row=Game==null?height-3:33;
            c.Text(3,row,new string(' ',Math.Max(0,c.Width-6)));
            c.Wrapped(3,row,overlay,c.Width-6,1,ConsoleColor.Yellow);
        }
        if (Game == null) { GameTheme.ColorStatistics(c); return c; }
        DrawStats(c); // Persistent desk, shared by every screen in an active run.

        GameTheme.ColorStatistics(c);
        var combined = new Canvas(width, height);
        combined.Paste(c, 0, 0);
        Canvas sidebar = DrawSidebar(height); GameTheme.ColorStatistics(sidebar);
        combined.Paste(sidebar, width - 30, 0);
        return combined;
    }
    public int InputVersion { get; private set; }
    public void Handle(Input input, Canvas frame)
    {
        Screen previous = Screen; Phase? phase = Game?.Phase;
        Run? previousGame=Game; StatSnapshot? before=Game?.CaptureStats();
        pendingStatsFrom=pendingStatsTo=null; pendingResultAnimation=false;
        HandleCore(input, frame);
        if(Game!=null && ReferenceEquals(Game,previousGame) && before!=null)
        {
            StatSnapshot after=Game.CaptureStats();
            if(before!=after) { pendingStatsFrom=before; pendingStatsTo=after; InputVersion++; }
        }
        if(Game?.Phase==Phase.Result && phase!=Phase.Result) { pendingResultAnimation=true; resultPage=0; }
        if(Screen != previous || Game?.Phase != phase || animationSource != null) InputVersion++;
    }
    void HandleCore(Input input, Canvas frame)
    {
        inputFrame = frame; animationSource = null; animationSelected = -1;
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
        if (Game != null && key is ConsoleKey.PageUp or ConsoleKey.PageDown)
        { if(Screen==Screen.Result) { resultPage+=key==ConsoleKey.PageUp?-1:1; pendingResultAnimation=true; InputVersion++; } else ScrollBoosters(key == ConsoleKey.PageUp ? -1 : 1); return; }
        if (Screen == Screen.Loans)
        {
            loanIndex = Navigate(key, loanIndex, 3);
            int loanDigit = Digit(key); if (loanDigit is >= 0 and < 3) loanIndex = loanDigit;
            if (key == ConsoleKey.Enter) { if(Game?.HasActiveLoan == true) Activate("loanRepay"); else if(Game?.CanBorrow == true) Activate("loanTake", loanIndex); }
            else if (key == ConsoleKey.P) Activate("loanRepay");
            return;
        }
        if (Game != null && key == ConsoleKey.L) { OpenLoans(); return; }
        if (key == ConsoleKey.I) { InspectSelected(); return; }
        if (Screen == Screen.Inspect)
        { if (key == ConsoleKey.Enter) Screen = beforeInspect; return; }
        if (Screen == Screen.TutorialIntro)
        { if (key == ConsoleKey.Enter) Activate("tutorialBegin"); return; }
        if (Screen == Screen.TutorialComplete)
        { if (key == ConsoleKey.Enter) Activate("tutorialFinish"); return; }
        if (Screen == Screen.Main && key is ConsoleKey.T or ConsoleKey.G) { Screen = Screen.Guide; return; }
        if (Screen == Screen.Guide)
        { if (key == ConsoleKey.Enter) StartTutorial(); return; }
        int digit = Digit(key);
        if (Screen == Screen.Main)
        {
            menuIndex = Navigate(key, menuIndex, Menu.Length);
            if (digit >= 0 && digit < Menu.Length) { menuIndex = digit; Activate("menu", menuIndex); }
            else if (key == ConsoleKey.Enter) Activate("menu", menuIndex);
            return;
        }
        if (Screen == Screen.CourseComparison) { if(key==ConsoleKey.Enter || key==ConsoleKey.C) Screen=Screen.Courses; return; }
        if (Screen == Screen.Courses)
        {
            if(key==ConsoleKey.C) { Screen=Screen.CourseComparison; return; }
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
            else if (key == ConsoleKey.B) notice = "The shop opens after clearing the month and choosing a booster.";
            return;
        }
        if (Screen == Screen.Shop)
        {
            shopIndex = GridNavigate(key, shopIndex, 6, 3);
            if (digit >= 0 && digit < 6) shopIndex = digit;
            if (key == ConsoleKey.Enter) Activate("buy", shopIndex);
            else if (key == ConsoleKey.B) Activate("shopBack");
            else if (key == ConsoleKey.Spacebar) Activate("next");
            else if (key == ConsoleKey.R) Activate("shopReroll");
            return;
        }
        if (Screen == Screen.Reward)
        {
            if (key == ConsoleKey.B) Activate("shop");
            else if (key is ConsoleKey.Spacebar or ConsoleKey.Enter) Activate("next");
            return;
        }
        if (Screen == Screen.Result && key == ConsoleKey.Enter) Activate("continue");
        else if (Screen == Screen.SpecialEvent && key == ConsoleKey.Enter) Activate("companyContinue");
        else if (Screen == Screen.End)
        { if (key == ConsoleKey.R) StartRun(); else if (key == ConsoleKey.E) Activate("endless"); else if (key == ConsoleKey.Enter) ReturnMain(); }
        else if (Screen == Screen.Pause)
        { if (key is ConsoleKey.Enter or ConsoleKey.R) Screen = beforePause; else if (key == ConsoleKey.M) ReturnMain(); }
        else if (Screen == Screen.Settings)
        {
            difficultyIndex = Navigate(key, (int)storage.Preferences.Difficulty, 3);
            storage.Preferences.Difficulty = (Difficulty)difficultyIndex;
            if (digit is >= 0 and < 3) { storage.Preferences.Difficulty = (Difficulty)digit; difficultyIndex = digit; }
            storage.SaveSettings();
        }
        else if (Screen == Screen.Briefing && key == ConsoleKey.Enter)
        { browsing = false; Screen = Screen.Difficulty; }
    }
    void Activate(string action, int index = -1)
    {
        if (!TutorialAllows(action, index)) { notice = TutorialInstruction; return; }
        Phase? phaseBefore = Game?.Phase;
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
            case "courseComparison": Screen=Screen.CourseComparison; break;
            case "comparisonBack": Screen=Screen.Courses; break;
            case "resultPrev": resultPage--; pendingResultAnimation=true; InputVersion++; break;
            case "resultNext": resultPage++; pendingResultAnimation=true; InputVersion++; break;
            case "loanTake":
                if (Game?.TakeLoan(index) == true) { animationSource = inputFrame; animationSelected = loanIndex; Screen = Screen.Loans; selected = -1; }
                else notice = Game?.Message ?? "No active run.";
                break;
            case "loanRepay": if(Game?.RepayLoan() == true) InputVersion++; notice = Game?.Message ?? ""; break;
            case "loanBack": Screen = beforeLoans; break;
            case "loans": OpenLoans(); break;
            case "play":
                if (Game?.Phase == Phase.Decisions && selected >= 0 && Game.Hand[selected].Kind == CardKind.Borrow) { OpenLoans(); break; }
                if (Game == null || selected < 0) { notice = "Select a card first."; break; }
                bool played = Game.Phase switch
                {
                    Phase.Decisions => Game.Play(selected), Phase.Events => Game.TakeEvent(selected),
                    Phase.Booster => Game.ClaimBooster(selected), _ => false
                };
                if (played) { animationSource = inputFrame; animationSelected = selected; selected = -1; SyncScreen(); }
                break;
            case "reroll":
                if (Game?.Reroll() == true) { animationSource = inputFrame; selected = -1; }
                else notice = "No rerolls available in this phase.";
                break;
            case "skip":
                if (Game == null) break;
                if (Game.Phase == Phase.Decisions) Game.FinishDecisions();
                else if (Game.Phase == Phase.Events) Game.SkipEvents();
                else if (Game.Phase == Phase.Booster) Game.SkipBooster();
                if (Game.Phase != phaseBefore) animationSource = inputFrame;
                selected = -1; SyncScreen(); break;
            case "continue": Game?.ContinueResult(); SyncScreen(); break;
            case "companyContinue": Game?.AcknowledgeCompanyEvent(); SyncScreen(); break;
            case "buy": if(Game?.Buy(index) == true) InputVersion++; break;
            case "shopSelect": shopIndex = index; break;
            case "shopReroll": if (Game?.RerollShop() == true) { shopIndex = 0; InputVersion++; } break;
            case "lockedShop": notice = "The shop opens after clearing the month and choosing a booster."; break;
            case "shopBack": Screen = Screen.Reward; break;
            case "shop": if (Game?.Phase == Phase.Shop) Screen = Screen.Shop; break;
            case "next": if (Game?.NextMonth() == true) { selected = -1; SyncScreen(); } break;
            case "resume": Screen = beforePause; break;
            case "main": ReturnMain(); break;
            case "retry": StartRun(); break;
            case "endless":
                if (Game?.EnterEndless() == true) { recorded = false; selected = -1; SyncScreen(); }
                else notice = "Clear all twelve quotas before choosing Endless.";
                break;
            case "back": Back(); break;
            case "briefingPlay": browsing = false; Screen = Screen.Difficulty; break;
            case "settingDifficulty": storage.Preferences.Difficulty = (Difficulty)index; difficultyIndex = index; storage.SaveSettings(); break;
            case "activeInspect": InspectActive(index); break;
            case "statInspect": if (statDetails.TryGetValue(index, out var detail)) ShowInfo(detail.Title, "Numbers tell your company story", detail.Description, Rarity.Common); break;
            case "scrollUp": ScrollBoosters(-1); break;
            case "scrollDown": ScrollBoosters(1); break;
            case "inspect": InspectSelected(); break;
            case "inspectBack": Screen = beforeInspect; break;
            case "tutorialBegin": tutorialStep = 1; Screen = Screen.Table; break;
            case "tutorialStart": StartTutorial(); break;
            case "tutorialFinish": storage.Preferences.TutorialCompleted = true; storage.SaveSettings(); ReturnMain(); Screen = Screen.Guide; break;
        }
        ObserveTutorial(action, phaseBefore);
    }
    void StartRun()
    {
        tutorialStep = -1; boosterScroll = 0;
        Game = new Run(courses[courseIndex], topicIndex, (Difficulty)difficultyIndex, Random.Shared.Next());
        selected = -1; shopIndex = 0; recorded = false; SyncScreen();
    }
    void SyncScreen()
    {
        if (Game == null) return;
        Screen = Game.Phase switch { Phase.Decisions => Screen.Table, Phase.Events => Screen.Events,
            Phase.Result => Screen.Result, Phase.SpecialEvent => Screen.SpecialEvent, Phase.Booster => Screen.Booster, Phase.Shop => Screen.Shop,
            _ => Screen.End };
        if (Game.Phase is Phase.Won or Phase.Lost && !recorded && !Game.Tutorial)
        { storage.Record(Game); recorded = true; }
    }
    void Back()
    {
        switch (Screen)
        {
            case Screen.CourseComparison: Screen=Screen.Courses; break;
            case Screen.Inspect: Screen = beforeInspect; break;
            case Screen.Loans: Screen = beforeLoans; break;
            case Screen.TutorialIntro: case Screen.TutorialComplete: ReturnMain(); break;
            case Screen.Main: Quit = true; break;
            case Screen.Courses: ReturnMain(); break;
            case Screen.Topics: Screen = Screen.Courses; break;
            case Screen.Difficulty: Screen = Screen.Topics; break;
            case Screen.Briefing: Screen = Screen.Topics; break;
            case Screen.Shop: Screen = Screen.Reward; break;
            case Screen.Pause: Screen = beforePause; break;
            case Screen.End: ReturnMain(); break;
            case Screen.Table: case Screen.Events: case Screen.Result: case Screen.SpecialEvent: case Screen.Booster: case Screen.Reward:
                beforePause = Screen; Screen = Screen.Pause; break;
            default: ReturnMain(); break;
        }
    }
    void ReturnMain()
    {
        if (Game?.Endless == true && !recorded) storage.Record(Game);
        Game = null; Screen = Screen.Main; notice = ""; tutorialStep = -1;
    }
    static int Digit(ConsoleKey key) => key is >= ConsoleKey.D1 and <= ConsoleKey.D9 ? key - ConsoleKey.D1
        : key is >= ConsoleKey.NumPad1 and <= ConsoleKey.NumPad9 ? key - ConsoleKey.NumPad1
        : key is ConsoleKey.D0 or ConsoleKey.NumPad0 ? 9 : -1;
    static int Navigate(ConsoleKey key, int value, int count) => key switch
    {
        ConsoleKey.LeftArrow or ConsoleKey.UpArrow or ConsoleKey.A or ConsoleKey.W => Math.Max(0, value - 1),
        ConsoleKey.RightArrow or ConsoleKey.DownArrow or ConsoleKey.D or ConsoleKey.S => Math.Min(count - 1, value + 1), _ => value
    };
    static int GridNavigate(ConsoleKey key, int value, int count, int columns) => key switch
    {
        ConsoleKey.UpArrow or ConsoleKey.W => Math.Max(0, value - columns), ConsoleKey.DownArrow or ConsoleKey.S => Math.Min(count - 1, value + columns),
        _ => Navigate(key, value, count)
    };
    static void Header(Canvas c, string title, string subtitle = "")
    {
        c.Center(1, title, ConsoleColor.Cyan); c.Rule(2);
        if (subtitle.Length > 0) c.Center(3, subtitle, ConsoleColor.White);
    }
    void Footer(Canvas c, string text)
    { if(Game != null) { c.Text(3, 32, text); c.Rule(41); } else { c.Rule(c.Height - 2); c.Text(3, c.Height - 1, text); } }
    void DrawMain(Canvas c)
    {
        c.Center(1, ".------------------------------------------------------.", ConsoleColor.DarkCyan);
        c.Center(2, "|                 T r a n s l a t e U n i              |", ConsoleColor.Cyan);
        c.Center(3, "'------------------------------------------------------'", ConsoleColor.DarkCyan);
        c.Center(5, "TranslateUni - the business learning game", ConsoleColor.Yellow);
        c.Center(6, "One company. Twelve months. Every decision counts.");
        for (int i = 0; i < Menu.Length; i++) c.Button(6, 9 + i * 2, (i + 1) + ". " + Menu[i], "menu", i, i == menuIndex);
        int x = c.Width - 29;
        c.Box(x, 10, 22, 11, ConsoleColor.DarkCyan);
        c.Text(x + 4, 12, "  /\\_/\\", ConsoleColor.Yellow);
        c.Text(x + 4, 13, "  (o.o)", ConsoleColor.Yellow);
        c.Text(x + 3, 16, "12 MONTH QUOTAS", ConsoleColor.White);
        c.Text(x + 3, 18, "60 TOPIC DECKS", ConsoleColor.Cyan);
        c.Text(x + 3, 19, "3 DIFFICULTY", ConsoleColor.Red);
        c.Center(27, "T / G: Learning Guide + two-minute guided practice", ConsoleColor.Green);
        Footer(c, "WASD / arrows: Navigate | ENTER: Choose | 1-8 / mouse: Open | ESC: Exit");
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
        c.Button((c.Width-25)/2,28,"C COMPARE COURSES","courseComparison");
        Footer(c, "WASD / arrows / 1-6: Highlight | ENTER / mouse: Open course | ESC: Back");
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
        Footer(c, "WASD / arrows / 1-9, 0=10: Highlight | ENTER: Select | ESC: Courses");
    }
    void DrawDifficulty(Canvas c)
    {
        Header(c, "CHOOSE YOUR DIFFICULTY", courses[courseIndex].Name);
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
            c.Text(x + 2, y + 7, "1 starting reroll/month");
            c.Text(x + 2, y + 8, i == 0 ? "Lower profit quotas" : i == 1 ? "Standard quotas" : "Tighter quotas");
            c.Buttons.Add(new(x, y, 22, 10, "difficulty", i));
        }
        c.Button((c.Width - 19) / 2, 23, "START 12-MONTH RUN", "start", selected: true);
        Footer(c, "WASD / arrows / 1-3: Choose difficulty | ENTER: Start | ESC: Topics");
    }
    void DrawTable(Canvas c, bool events)
    {
        Run g = Game!; Forecast forecast = g.Preview();
        Header(c, events ? "EVENT PHASE - TAKE IT OR LEAVE IT" : "DECISION TABLE", $"{g.Course.Name} / {g.Lesson.Name}");
        c.Text(3, 4, g.Endless ? $"ENDLESS MONTH {g.Month} | Bonus quotas: {g.Cleared - 12} | {g.Difficulty}"
            : $"MONTH {g.Month}/12   Quotas met: {g.Cleared}/12   {g.Difficulty}", ConsoleColor.Yellow);
        c.Text(3, 5, $"Decisions left: {g.Actions}   Rerolls: {g.Rerolls}   Projected profit: ${forecast.Profit:N0}");
        c.Text(3, 6, "Quota + last earnings on the right. I: card details.", ConsoleColor.Cyan);
        for (int i = 0; i < 3; i++)
        {
            string name = events ? g.Events[i].Name : g.Hand[i].Name;
            string[] lines = events ? [g.Events[i].Description] : g.Describe(g.Hand[i].Kind);
            string flavor = events ? GameContent.Flavor(g.Events[i]) : GameContent.Flavor(g.Hand[i].Kind);
            Rarity rarity = events ? g.Events[i].Rarity : g.Hand[i].Rarity;
            DrawCard(c, i, name, flavor, lines, rarity, i == selected, events ? ConsoleColor.Magenta : ConsoleColor.Cyan);
        }
        c.Button(3, 28, $"R {g.Rerolls}", "reroll");
        c.Button(c.Width - 25, 28, "L LOANS", "loans");
        c.Button(c.Width - 10, 28, "B", "lockedShop");
        c.Button(3, 29, events ? "E LEAVE ALL" : "E END DECISIONS", "skip");
        c.Button(28, 29, events ? "ENTER TAKE" : "ENTER PLAY", "play");
        c.Text(3, 33, g.Message, ConsoleColor.Yellow);
        Footer(c, events ? "1-3/WASD: Select | ENTER: Take | E: Leave | L: Loans | I: Info | ESC: Pause"
            : "1-3/WASD: Select | ENTER: Play | E: Skip | L: Loans | I: Info | ESC: Pause");
    }
    void DrawCard(Canvas c, int index, string title, string flavor, string[] lines, Rarity rarity, bool raised, ConsoleColor color)
    {
        int x = Math.Max(3, (c.Width - 72) / 2) + index * 24;
        int y = raised ? 10 : 12; // Balatro-style selection lift: exactly two rows.
        c.Box(x, y, 22, 15, raised ? ConsoleColor.Yellow : color);
        c.Wrapped(x + 2, y + 1, (index + 1) + ". " + title, 18, 2, raised ? ConsoleColor.Yellow : ConsoleColor.White);
        c.Text(x + 2, y + 3, rarity.ToString().ToUpper(), GameContent.Color(rarity));
        c.Wrapped(x + 2, y + 4, flavor, 18, 2, ConsoleColor.Yellow);
        c.Text(x + 2, y + 6, "------------------", ConsoleColor.DarkGray);
        string[] definition = lines.SelectMany(line => Wrap(line, 18)).ToArray();
        for (int row = 0; row < Math.Min(6, definition.Length); row++) c.Text(x + 2, y + 7 + row, definition[row]);
        if (definition.Length > 6) c.Text(x + 2, y + 13, "I: full definition", ConsoleColor.Cyan);
        c.Buttons.Add(new(x, y, 22, 15, "select", index));
    }
    void DrawResult(Canvas c) => DrawProfitStatement(c);
    void DrawBooster(Canvas c)
    {
        Run g = Game!;
        Header(c, "MONTHLY BOOSTER PACK", $"Month {g.Month} reward - choose one free booster");
        c.Center(5, "One choice. Lasting help for the months ahead.", ConsoleColor.Yellow);
        for (int i = 0; i < 3; i++)
        {
            Booster b = g.Boosters[i];
            DrawCard(c, i, b.Name, GameContent.Flavor(b), [b.Description], b.Rarity, selected == i,
                b.Family == BoosterFamily.Green ? ConsoleColor.Green : ConsoleColor.Magenta);
        }
        c.Text(3, 6, "GREEN: regular boosts | PURPLE: stronger rarities", ConsoleColor.Cyan);
        c.Button(5, 29, "ENTER CLAIM + SHOP", "play");
        c.Button(c.Width - 25, 29, "E SKIP + SHOP", "skip");
        Footer(c, "WASD/1-3: Select | ENTER: Claim | E: Skip | I: Details | ESC: Pause");
    }
    void DrawShop(Canvas c)
    {
        Run g = Game!;
        Header(c, "OFFICE ODDITIES - DECORATION SHOP", $"Cash ${g.Cash:N0} | Upgrades last for this run");
        int left = (c.Width - 72) / 2;
        for (int i = 0; i < 6; i++)
        {
            int x=left+i%3*24, y=4+i/3*12;
            bool selectedItem=i==shopIndex;
            if(i>=g.ShopOffers.Count) { c.Text(x,y+3,"All tiers maxed",ConsoleColor.DarkGray); c.Box(x,y+9,22,3,ConsoleColor.DarkCyan); continue; }
            Decoration item=g.ShopOffers[i]; bool maxed=g.ShopTier(item.Id)>=item.Tier;
            ConsoleColor color=selectedItem?ConsoleColor.Magenta:maxed?ConsoleColor.Green:ConsoleColor.Cyan;
            for(int row=0;row<2;row++) c.Text(x+8,y+row,item.Art[row],color);
            c.Wrapped(x,y+2,$"{i+1}. {item.Name}",22,2,color);
            if(maxed) c.Text(x,y+4,$"MAX TIER {item.Tier}",color);
            else { string price=$"${item.Cost}"; c.Text(x,y+4,price,GameTheme.Statistic("cash")); c.Text(x+price.Length+1,y+4,$"TIER {item.Tier}/{g.MaxShopTier}",color); }
            c.Wrapped(x,y+5,GameContent.DecorationFlavor(item.Id),22,2,color);
            c.Wrapped(x,y+7,item.Boost,22,2,color);
            c.Box(x,y+9,22,3,selectedItem?ConsoleColor.Magenta:maxed?ConsoleColor.Green:ConsoleColor.DarkCyan);
            if(selectedItem) c.Text(x+5,y+10," SELECTED ",ConsoleColor.Magenta);
            else if(maxed) c.Text(x+7,y+10,"MAXED",ConsoleColor.Green);
            c.Buttons.Add(new(x,y,22,12,"shopSelect",i));
        }
        c.Text(left+2,28,"[o_o] Shopkeeper: Pick some office magic.",ConsoleColor.Gray);
        c.Button(left, 29, "ENTER BUY", "buy", shopIndex);
        c.Button(left + 19, 29, "B BACK", "shopBack");
        c.Button(left + 34, 29, "SPACE LEAVE SHOP", "next");
        if (!g.Tutorial) c.Button(c.Width - 16, 28, $"R ${g.ShopRerollCost}", "shopReroll");
        c.Text(3, 33, g.Message, ConsoleColor.Yellow);
        Footer(c, "WASD/1-6: Select | ENTER: Buy | R: Reroll | SPACE: Next month");
    }
    void DrawReward(Canvas c)
    {
        Run g = Game!;
        Header(c, "BETWEEN MONTHS", g.Endless ? $"Endless | Month {g.Month} cleared | Cash ${g.Cash:N0}"
            : $"{g.Cleared}/12 quotas cleared | Cash ${g.Cash:N0}");
        c.Wrapped(5, 8, g.Message, c.Width - 10, 3, ConsoleColor.Yellow);
        c.Center(11, "Your booster choice has been settled for this month.");
        c.Center(13, "Visit the shop again, or begin the next month.");
        c.Button((c.Width - 22) / 2, 18, "B DECORATION SHOP", "shop");
        c.Button((c.Width - 22) / 2, 21, "SPACE NEXT MONTH", "next", selected: true);
        Footer(c, "B: Shop | SPACE / ENTER: Next month | ESC: Pause");
    }
    void DrawEnd(Canvas c)
    {
        Run g = Game!; bool won = g.Phase == Phase.Won;
        Header(c, won ? "YOU WIN - A YEAR WELL RUN" : g.Endless ? "ENDLESS RUN ENDED" : "RUN OVER", g.Course.Name + " / " + g.Lesson.Name);
        c.Center(7, won ? "* * *  ALL 12 MONTHLY QUOTAS CLEARED  * * *" : g.Endless ? $"Year cleared + {g.Cleared - 12} endless quotas"
            : $"{g.Cleared}/12 months cleared", won ? ConsoleColor.Green : ConsoleColor.Red);
        c.Center(10, $"{g.Difficulty} | Total operating profit ${g.Reports.Sum(r => r.Profit):N0}");
        c.Center(12, $"Cash ${g.Cash:N0} | Decorations {g.Owned.Count} | Boosters {g.ActiveBoosts.Count}");
        if (!won) c.Center(15, "Missed: " + string.Join(", ", g.LastReport!.Failures));
        if (won && !g.Endless)
        {
            c.Center(15, "ENDLESS: +35% quota per month; diamond and cosmic upgrades", ConsoleColor.Magenta);
            c.Button((c.Width - 23) / 2, 17, "E CONTINUE ENDLESS", "endless", selected: true);
        }
        c.Button((c.Width - 23) / 2, 20, "R RETRY SAME TOPIC", "retry");
        c.Button((c.Width - 23) / 2, 23, "ENTER MAIN MENU", "main", selected: true);
        Footer(c, won ? "E: Endless | R: Retry | ENTER / ESC: Main menu" : "R: Retry | ENTER / ESC: Main menu");
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
            c.Text(4, 6 + i * 2, $"{(r.Won ? "WIN " : "LOSS")} {(r.Endless ? $"12 + {Math.Max(0, r.Cleared - 12)} endless" : $"{r.Cleared}/12")} | {r.Difficulty} | {r.Course} / {r.Topic}", r.Won ? ConsoleColor.Green : ConsoleColor.Gray);
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
        Footer(c, "WASD / arrows / 1-3: Default difficulty | ESC: Main menu");
    }
    void DrawGuide(Canvas c)
    {
        Header(c, "TRANSLATEUNI - LEARNING GUIDE + TUTORIAL", "Read the reference, then try a guided practice month");
        string[] lines =
        [
            "1. Select one of six courses, then one of its ten topic levels.",
            "2. Choose an Easy, Normal, or Hard difficulty. Every run lasts 12 months.",
            "3. Each month, use decisions to change price, ads, stock, costs and risk.",
            "4. WASD / arrows / 1-3 select cards. ENTER plays; I explains.",
            "5. Start with one reroll per month. R replaces cards; E ends decisions.",
            "6. In the event phase, take ONE card, or E to leave all three.",
            "7. Monthly sales settle automatically. Meet EVERY displayed quota.",
            "8. After a clear, company news changes your next month; read its effects.",
            "9. Choose one FREE booster. Green and purple effects appear on the right.",
            "10. Shop items start weak: upgrade them to tier 3. R refreshes shelves.",
            "11. SPACE leaves the shop and starts the next month. B revisits rewards.",
            "12. Clear twelve quotas, then E offers Endless: +35% targets, tiers 4-5.",
            "Common, Uncommon, Rare, Epic, Legendary: less common, more unusual.",
            "I explains a selected card. Click side boosters; PgUp/PgDn scrolls them.",
            "Profit includes sold-stock costs, ads, overhead, interest and events.",
            "L: three loan cards; P repays. Repay fully, then wait three months."
        ];
        for (int i = 0; i < lines.Length; i++) c.Text(4, 5 + i * 2, lines[i]);
        c.Button(4, c.Height - 4, "ENTER: GUIDED PRACTICE (~2 MIN)", "tutorialStart", selected: true);
        Footer(c, "ENTER: Practice with this guide | ESC: Main menu");
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

