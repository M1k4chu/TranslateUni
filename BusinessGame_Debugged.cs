using System.Globalization;
using System.Runtime.InteropServices;

class Program
{
    const int CardWidth = 22;
    const int CardHeight = 10;
    const int CardSpace = 24;
    static int rerolls = 3;
    const int CardBaseY = 2;
    const int SelectionLift = 2;
    static double cash = 5000, debt = 1200, price = 25, unitCost = 12, share = 18;
    static int inventory = 450;
    static bool inShop;
    static int shopSelection;
    static string shopMessage = "Select an item with 1-6 or arrows, then ENTER to buy.";

    sealed class Decoration
    {
        public string Name { get; }
        public int Cost { get; }
        public string Boost { get; }
        public string[] Art { get; }
        public Action Apply { get; }
        public bool Owned { get; set; }

        public Decoration(string name, int cost, string boost, string[] art, Action apply)
        {
            Name = name; Cost = cost; Boost = boost; Art = art; Apply = apply;
        }
    }

    static readonly Decoration[] ShopItems =
    {
        new("Desk Plant", 100, "Share +0.5%", new[] { "  \\|/  ", "  /|\\  ", "  [__] " }, () => share += 0.5),
        new("Desk Lamp", 150, "Unit cost -$0.25", new[] { "  ___  ", " /___\\ ", "   |__ " }, () => unitCost = Math.Max(0, unitCost - 0.25)),
        new("Wall Clock", 125, "Inventory +15", new[] { " .---. ", " | / | ", " '---' " }, () => inventory += 15),
        new("Motivation Art", 175, "Price +$0.50", new[] { " +---+ ", " |UP!| ", " +---+ " }, () => price += 0.5),
        new("Lucky Cat", 200, "Extra reroll +1", new[] { " /\\_/\\ ", " (o.o) ", "  /|_  " }, () => rerolls++),
        new("Desk Fountain", 150, "Debt -$75", new[] { "  ~|~  ", " (_|_) ", " [___] " }, () => debt = Math.Max(0, debt - 75))
    };


    static Random random = new Random();

    // Set to false if you never want duplicate cards,
    // even when the library contains fewer than three cards.
    const bool AllowDuplicateFallback = true;

    static List<string[]> CardLibrary = new List<string[]>
{
    new string[]
    {
        "Sell part of",
        "the company",
        "",
        "Cash: +$250",
        "Share: -10%"
    },

    new string[]
    {
        "Advertise product",
        "",
        "Cash: -$100",
        "Inventory: -50",
        "Price: +$5"
    },

    new string[]
    {
        "Black Friday Sale",
        "",
        "Cash: +$500",
        "Inventory: -100",
        "Price: -$10"
    },

    // Add more cards below this line!

    new string[]
    {
        "New Factory",
        "",
        "Cash: -$1000",
        "Inventory: +200",
        "Price: +$2"
    },

    new string[]
    {
        "Market Crash",
        "",
        "Cash: -$500",
        "Share: -5%"
    },

    new string[]
    {
        "Successful Launch",
        "",
        "Cash: +$750",
        "Inventory: -25",
        "Price: +$3"
    }
};


    static void AnimateChosenCardAway(string[] card, int selected)
    {
        int firstCardX = Math.Max(
            0,
            (Console.WindowWidth - CardSpace * 3) / 2
        );
        int centerX = firstCardX + selected * CardSpace;

        int baseY = CardBaseY - SelectionLift;

        // Shrink the chosen card horizontally
        for (int frame = 0; frame < 10; frame++)
        {
            double progress = (frame + 1) / 10.0;

            int width = Math.Max(
                5,
                CardWidth - (int)Math.Round(progress * (CardWidth - 5))
            );

            int x = centerX + (CardWidth - width) / 2;

            // Erase only the card's previous drawing
            for (int row = 0; row < CardHeight; row++)
                WriteAt(centerX, baseY + row, new string(' ', CardSpace));

            DrawCardAt(card, x, baseY, width, CardHeight);

            Thread.Sleep(25);
        }

        // Collapse vertically
        for (int frame = 0; frame < 5; frame++)
        {
            for (int row = 0; row < CardHeight; row++)
                WriteAt(centerX, baseY + row, new string(' ', CardSpace));

            int middleX = centerX + CardSpace / 2;
            int middleY = baseY + CardHeight / 2;

            if (frame < 3)
            {
                WriteAt(middleX, middleY, "*");
                WriteAt(middleX - 1, middleY, "<");
                WriteAt(middleX + 1, middleY, ">");
            }

            Thread.Sleep(35);
        }

        // Final star-like disappearance
        for (int frame = 0; frame < 4; frame++)
        {
            for (int row = 0; row < CardHeight; row++)
                WriteAt(centerX, baseY + row, new string(' ', CardSpace));

            int middleX = centerX + CardSpace / 2;
            int middleY = baseY + CardHeight / 2;

            if (frame == 0)
            {
                WriteAt(middleX - 1, middleY, "*");
                WriteAt(middleX + 1, middleY, "*");
            }
            else if (frame == 1)
            {
                WriteAt(middleX, middleY - 1, ".");
                WriteAt(middleX, middleY + 1, ".");
            }

            Thread.Sleep(35);
        }

        for (int row = 0; row < CardHeight; row++)
            WriteAt(centerX, baseY + row, new string(' ', CardSpace));
    }


    static string[][] DrawThreeCards()
    {
        if (CardLibrary.Count == 0)
            throw new InvalidOperationException(
                "The card library is empty!"
            );

        // Make a shuffled copy of the available cards.
        List<string[]> shuffled = new List<string[]>(CardLibrary);

        // Fisher-Yates shuffle
        for (int i = shuffled.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);

            string[] temp = shuffled[i];
            shuffled[i] = shuffled[j];
            shuffled[j] = temp;
        }

        // Take three different cards whenever possible.
        List<string[]> result = new List<string[]>();

        int uniqueCount = Math.Min(3, shuffled.Count);

        for (int i = 0; i < uniqueCount; i++)
            result.Add(shuffled[i]);

        // OPTIONAL: allow duplicates if fewer than 3 cards exist.
        if (result.Count < 3)
        {
            if (!AllowDuplicateFallback)
            {
                throw new InvalidOperationException(
                    "You need at least three cards in CardLibrary."
                );
            }

            while (result.Count < 3)
            {
                result.Add(
                    CardLibrary[random.Next(CardLibrary.Count)]
                );
            }
        }

        return result.ToArray();
    }

    static void Main()
    {
        string[][] cards = DrawThreeCards();
        int selected = -1;
        bool confirmed = false;

        try
        {
            using var input = new GameInput();
            Console.CursorVisible = false;
            Console.Clear();

            DrawScene(cards, selected, false);

            while (true)
            {
                ConsoleKey key = input.ReadKey();

                if (key == ConsoleKey.S || (inShop && key == ConsoleKey.Escape))
                {
                    inShop = !inShop;
                    Console.Clear();
                    if (inShop) DrawShop();
                    else DrawScene(cards, selected, confirmed);
                    continue;
                }

                if (key == ConsoleKey.Escape)
                    break;

                if (inShop)
                {
                    HandleShopKey(key);
                    DrawShop();
                    continue;
                }

                if (key == ConsoleKey.R && rerolls > 0)
                {
                    if (confirmed)
                    {
                        AnimateChosenCardAway(cards[selected], selected);
                    }
                    TryReroll(ref cards, ref selected, ref confirmed);
                    DrawScene(cards, selected, confirmed);
                    continue;
                }

                if (confirmed) continue;

                int previous = selected;

                // Arrows move between cards without wrapping
                if (key == ConsoleKey.LeftArrow ||
                    key == ConsoleKey.UpArrow)
                {
                    selected = Math.Max(0, selected - 1);
                }
                else if (key == ConsoleKey.RightArrow ||
                         key == ConsoleKey.DownArrow)
                {
                    selected = Math.Min(cards.Length - 1, selected + 1);
                }

                // Keep number-key selection too
                else if (key == ConsoleKey.D1 ||
                         key == ConsoleKey.NumPad1)
                    selected = 0;

                else if (key == ConsoleKey.D2 ||
                         key == ConsoleKey.NumPad2)
                    selected = 1;

                else if (key == ConsoleKey.D3 ||
                         key == ConsoleKey.NumPad3)
                    selected = 2;

                else if (key == ConsoleKey.Spacebar)
                    selected = -1;
                else if (key == ConsoleKey.Enter && selected >= 0)
                {
                    AnimateAway(cards, selected);
                    ApplyCardEffects(cards[selected]);
                    confirmed = true;
                    DrawScene(cards, selected, true);
                    continue;
                }

                if (selected != previous)
                    DrawScene(cards, selected, false);
            }
        }
        finally
        {
            Console.ResetColor();
            Console.CursorVisible = true;
            Console.Clear();
        }
    }

    static bool TryReroll(ref string[][] cards, ref int selected, ref bool confirmed)
    {
        if (rerolls <= 0) return false;
        cards = DrawThreeCards();
        rerolls--;
        selected = -1;
        confirmed = false;
        return true;
    }

    static void ApplyCardEffects(string[] card)
    {
        foreach (string line in card)
        {
            string[] parts = line.Split(':', 2);
            if (parts.Length != 2) continue;
            string amount = parts[1].Replace("$", "").Replace("%", "").Trim();
            if (!double.TryParse(amount, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)) continue;
            switch (parts[0].Trim())
            {
                case "Cash": cash += value; break;
                case "Debt": debt = Math.Max(0, debt + value); break;
                case "Price": price = Math.Max(0, price + value); break;
                case "Unit cost": unitCost = Math.Max(0, unitCost + value); break;
                case "Inventory": inventory = Math.Max(0, inventory + (int)value); break;
                case "Share": share = Math.Clamp(share + value, 0, 100); break;
            }
        }
    }

    static bool BuyDecoration(int index)
    {
        if (index < 0 || index >= ShopItems.Length) return false;
        Decoration item = ShopItems[index];
        if (item.Owned) { shopMessage = "You already own " + item.Name + "."; return false; }
        if (cash < item.Cost) { shopMessage = "Not enough cash for " + item.Name + "."; return false; }
        cash -= item.Cost;
        item.Apply();
        item.Owned = true;
        shopMessage = "Bought " + item.Name + "! " + item.Boost + ".";
        return true;
    }

    static void HandleShopKey(ConsoleKey key)
    {
        int number = key >= ConsoleKey.D1 && key <= ConsoleKey.D6 ? key - ConsoleKey.D1
            : key >= ConsoleKey.NumPad1 && key <= ConsoleKey.NumPad6 ? key - ConsoleKey.NumPad1 : -1;
        if (number >= 0) shopSelection = number;
        else if (key == ConsoleKey.LeftArrow) shopSelection = Math.Max(0, shopSelection - 1);
        else if (key == ConsoleKey.RightArrow) shopSelection = Math.Min(5, shopSelection + 1);
        else if (key == ConsoleKey.UpArrow) shopSelection = Math.Max(0, shopSelection - 3);
        else if (key == ConsoleKey.DownArrow) shopSelection = Math.Min(5, shopSelection + 3);
        else if (key == ConsoleKey.Enter) BuyDecoration(shopSelection);
    }

    static void DrawShop()
    {
        int left = Math.Max(0, (Console.WindowWidth - 72) / 2);
        for (int y = 0; y < Math.Min(25, Console.BufferHeight); y++)
            WriteAt(0, y, new string(' ', Math.Max(0, Console.BufferWidth - 1)));
        Console.ForegroundColor = ConsoleColor.Cyan;
        WriteAt(left, 0, "OFFICE ODDITIES  |  Cash: $" + cash.ToString("N2"));
        Console.ResetColor();
        for (int i = 0; i < ShopItems.Length; i++)
        {
            Decoration item = ShopItems[i];
            int x = left + i % 3 * 24;
            int y = 2 + i / 3 * 7;
            Console.ForegroundColor = item.Owned ? ConsoleColor.DarkGray
                : i == shopSelection ? ConsoleColor.Yellow : ConsoleColor.Gray;
            for (int row = 0; row < item.Art.Length; row++) WriteAt(x + 7, y + row, item.Art[row]);
            WriteAt(x, y + 3, $"{i + 1}. {item.Name}");
            WriteAt(x, y + 4, item.Owned ? "OWNED" : "$" + item.Cost + "  " + item.Boost);
            WriteAt(x, y + 5, item.Boost);
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            WriteAt(x, y + 6, "======================");
        }
        Console.ForegroundColor = ConsoleColor.White;
        WriteAt(left + 5, 17, "  .----.    Welcome! A little style goes a long way.");
        WriteAt(left + 5, 18, "  |o  o|    I'm your office shopkeeper.");
        WriteAt(left + 5, 19, "  | -- |    All boosts last for this game.");
        WriteAt(left + 5, 20, " /|____|\\");
        Console.ForegroundColor = ConsoleColor.DarkYellow;
        WriteAt(left, 21, new string('=', 70));
        Console.ResetColor();
        WriteAt(left, 22, shopMessage);
        WriteAt(left, 23, "1-6 / arrows: Select | ENTER: Buy | S / ESC: Back to table");
        DrawButton(ShopButtonX(), 15, "S");
    }

    static void DrawScene(
        string[][] cards,
        int selected,
        bool confirmed)
    {
        // Clear only the card area, not the entire screen
        ClearCardArea();

        int screenWidth = Console.WindowWidth;
        int totalWidth = CardSpace * 3;
        int startX = Math.Max(0, (screenWidth - totalWidth) / 2);
        int baseY = 2;

        // Draw all three cards. Highlight the currently selected card.
        for (int i = 0; i < cards.Length; i++)
        {
            if (confirmed && i != selected) continue;
            if (i == selected)
                Console.ForegroundColor = confirmed
                    ? ConsoleColor.Green
                    : ConsoleColor.Yellow;
            else
                Console.ForegroundColor = ConsoleColor.Gray;

            DrawCardAt(
                cards[i],
                startX + i * CardSpace,
                baseY - (i == selected ? SelectionLift : 0),
                CardWidth,
                CardHeight
            );
        }
        Console.ResetColor();

        // Keep your statistics values and drawing unchanged
        if (Console.BufferHeight > 19)
            Console.SetCursorPosition(0, 14);

        DrawStatistics(
            cash: cash,
            debt: debt,
            price: price,
            unitCost: unitCost,
            inventory: inventory,
            share: share
        );
        DrawRerollCounter();
        DrawButton(ShopButtonX(), 15, "S");

        // Three empty lines at the bottom
        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine();

        WriteAt(
            0,
            Math.Min(23, Console.BufferHeight - 1),
            confirmed
                ? (rerolls > 0
                    ? $"Confirmed | R: Reroll ({rerolls}) | S: Shop | ESC: Exit"
                    : "No rerolls left | S: Shop | ESC: Exit")
                : $"1-3 / arrows: Select | ENTER: Play | SPACE: Clear | R: {rerolls} | S: Shop | ESC: Exit"
        );
    }

    static void AnimateAway(string[][] cards, int selected)
    {
        int startX = Math.Max(
            0,
            (Console.WindowWidth - CardSpace * 3) / 2
        );

        int baseY = 2;

        // 12 frames x 35 ms = 420 ms
        // 5 vertical frames x 45 ms = 225 ms
        // 5 star frames x 70 ms = 350 ms
        // Total animation time: approximately 995 ms
        for (int frame = 0; frame < 12; frame++)
        {
            double progress = (frame + 1) / 12.0;

            for (int i = 0; i < cards.Length; i++)
            {
                if (i == selected)
                    continue;

                int originalX = startX + i * CardSpace;

                int width = Math.Max(
                    5,
                    CardWidth - (int)Math.Round(
                        progress * (CardWidth - 5)
                    )
                );

                // Fade text into spaces while the walls shrink
                DrawAnimatedCard(
                    cards[i],
                    originalX,
                    baseY,
                    width,
                    CardHeight,
                    progress
                );
            }

            Thread.Sleep(35);
        }

        // Collapse vertically once the cards are five characters wide
        for (int frame = 0; frame < 5; frame++)
        {
            int height = CardHeight - (frame + 1) * 2;

            if (height < 1)
                height = 1;

            for (int i = 0; i < cards.Length; i++)
            {
                if (i == selected)
                    continue;

                int originalX = startX + i * CardSpace;

                DrawAnimatedCard(
                    cards[i],
                    originalX,
                    baseY,
                    5,
                    height,
                    1.0
                );
            }

            Thread.Sleep(45);
        }

        // Star-like disappearance
        for (int frame = 0; frame < 5; frame++)
        {
            for (int i = 0; i < cards.Length; i++)
            {
                if (i == selected)
                    continue;

                int originalX = startX + i * CardSpace;

                ClearOneCard(originalX, baseY);

                int centerX = originalX + CardSpace / 2;
                int centerY = baseY + CardHeight / 2;

                if (frame == 0)
                {
                    WriteAt(centerX, centerY, "*");
                }
                else if (frame == 1)
                {
                    WriteAt(centerX, centerY, "+");
                    WriteAt(centerX - 1, centerY, "*");
                    WriteAt(centerX + 1, centerY, "*");
                }
                else if (frame == 2)
                {
                    WriteAt(centerX - 1, centerY - 1, "\\");
                    WriteAt(centerX + 1, centerY - 1, "/");
                    WriteAt(centerX, centerY, "*");
                    WriteAt(centerX - 1, centerY + 1, "/");
                    WriteAt(centerX + 1, centerY + 1, "\\");
                }
                else if (frame == 3)
                {
                    WriteAt(centerX - 2, centerY, ".");
                    WriteAt(centerX + 2, centerY, ".");
                    WriteAt(centerX, centerY - 1, ".");
                    WriteAt(centerX, centerY + 1, ".");
                }
                // Frame 4 stays empty
            }

            Thread.Sleep(70);
        }

        // Clear all traces of the unselected cards
        for (int i = 0; i < cards.Length; i++)
        {
            if (i != selected)
                ClearOneCard(startX + i * CardSpace, baseY);
        }
    }

    static void DrawRerollCounter()
    {
        int boardWidth = 54;
        int boardLeft = Math.Max(
            0,
            (Console.WindowWidth - boardWidth) / 2
        );

        // Keep the counter to the left of the statistics board.
        int x = Math.Max(0, boardLeft - 7);
        int y = 15;

        DrawButton(x, y, "R");
        WriteAt(x, y + 3, $" x{rerolls} ");
    }

    static int ShopButtonX() => Math.Max(0, Math.Min(Console.WindowWidth - 6,
        Math.Max(0, (Console.WindowWidth - 54) / 2) + 56));

    static void DrawButton(int x, int y, string label)
    {
        WriteAt(x, y, ",---,");
        WriteAt(x, y + 1, "| " + label + " |");
        WriteAt(x, y + 2, "'---'");
    }

    static void DrawAnimatedCard(
        string[] card,
        int originalX,
        int originalY,
        int width,
        int height,
        double progress)
    {
        ClearOneCard(originalX, originalY);

        // Center the shrinking card in its original space
        int drawnWidth = width + 2;
        int x = originalX + (CardSpace - drawnWidth) / 2;
        int y = originalY + (CardHeight - height) / 2;

        int contentWidth = Math.Max(0, width - 4);

        string[] maskedCard = new string[card.Length];

        // Gradually replace the text with spaces
        for (int i = 0; i < card.Length; i++)
        {
            string text = card[i];

            int visibleLength = (int)Math.Round(
                text.Length * (1.0 - progress)
            );

            visibleLength = Math.Min(
                text.Length,
                Math.Max(0, visibleLength)
            );

            if (visibleLength > contentWidth)
                visibleLength = contentWidth;

            maskedCard[i] = text.Substring(0, visibleLength);
        }

        DrawCardAt(maskedCard, x, y, width, height);
    }

    static void DrawCardAt(
        string[] card,
        int x,
        int y,
        int width,
        int height)
    {
        if (height <= 0 || width < 1)
            return;

        int contentWidth = Math.Max(0, width - 4);
        int renderedWidth = width + 2;

        // At height 1, show just the top edge
        if (height == 1)
        {
            WriteAt(
                x,
                y,
                " ˛" + new string('~', Math.Max(0, width - 2)) + "˛ "
            );
            return;
        }

        // Top border
        WriteAt(
            x,
            y,
            " ˛" + new string('~', Math.Max(0, width - 2)) + "˛ "
        );

        // Contents and side walls
        for (int row = 1; row < height - 1; row++)
        {
            int cardRow = row - 1;
            string text = cardRow < card.Length
                ? card[cardRow]
                : "";

            if (text.Length > contentWidth)
                text = text.Substring(0, contentWidth);

            string line = row % 2 == 1
                ? " ( " + text.PadRight(contentWidth) + " ) "
                : " ) " + text.PadRight(contentWidth) + " ( ";

            // Keep each line at the current width
            if (line.Length > renderedWidth)
                line = line.Substring(0, renderedWidth);

            WriteAt(x, y + row, line.PadRight(renderedWidth));
        }

        // Bottom border
        WriteAt(
            x,
            y + height - 1,
            " ´" + new string('~', Math.Max(0, width - 2)) + "´ "
        );
    }

    static void ClearCardArea()
    {
        int startX = Math.Max(
            0,
            (Console.WindowWidth - CardSpace * 3) / 2
        );

        for (int i = 0; i < 3; i++)
            ClearOneCard(startX + i * CardSpace, 0);
    }

    static void ClearOneCard(int x, int y)
    {
        for (int row = 0; row < CardHeight + 2; row++)
            WriteAt(x, y + row, new string(' ', CardSpace));
    }

    static void WriteAt(int x, int y, string text)
    {
        if (y < 0 || y >= Console.BufferHeight)
            return;

        x = Math.Max(0, x);

        if (x >= Console.BufferWidth)
            return;

        int available = Console.BufferWidth - x;

        if (text.Length > available)
            text = text.Substring(0, available);

        Console.SetCursorPosition(x, y);
        Console.Write(text);
    }

    // Console mouse input works on Windows; other terminals retain keyboard input.
    sealed class GameInput : IDisposable
    {
        readonly IntPtr handle;
        readonly uint originalMode;
        readonly bool mouseEnabled;
        bool leftDown;

        public GameInput()
        {
            if (!OperatingSystem.IsWindows() || Console.IsInputRedirected) return;
            handle = GetStdHandle(-10);
            if (!GetConsoleMode(handle, out originalMode)) return;
            // Enable mouse + extended flags, disable Quick Edit (which steals clicks).
            mouseEnabled = SetConsoleMode(handle, (originalMode | 0x0010u | 0x0080u) & ~0x0040u);
        }

        public ConsoleKey ReadKey()
        {
            if (!mouseEnabled) return Console.ReadKey(true).Key;
            while (true)
            {
                if (!ReadConsoleInputW(handle, out InputRecord record, 1, out uint count))
                    return Console.ReadKey(true).Key;
                if (count == 0) continue;
                if (record.Type == 1 && record.KeyDown != 0)
                    return (ConsoleKey)record.VirtualKey;
                if (record.Type != 2) continue;
                bool pressed = (record.Buttons & 1) != 0;
                bool clicked = pressed && !leftDown && record.MouseFlags == 0;
                leftDown = pressed;
                if (!clicked) continue;
                int x = record.MouseX - Console.WindowLeft;
                int y = record.MouseY - Console.WindowTop;
                if (Hit(x, y, ShopButtonX(), 15, 5, 3)) return ConsoleKey.S;
                if (inShop)
                {
                    int left = Math.Max(0, (Console.WindowWidth - 72) / 2);
                    for (int i = 0; i < ShopItems.Length; i++)
                        if (Hit(x, y, left + i % 3 * 24, 2 + i / 3 * 7, 22, 6))
                            return (ConsoleKey)((int)ConsoleKey.D1 + i);
                }
                else
                {
                    int boardLeft = Math.Max(0, (Console.WindowWidth - 54) / 2);
                    if (Hit(x, y, Math.Max(0, boardLeft - 7), 15, 5, 3)) return ConsoleKey.R;
                    int startX = Math.Max(0, (Console.WindowWidth - CardSpace * 3) / 2);
                    for (int i = 0; i < 3; i++)
                        if (Hit(x, y, startX + i * CardSpace, 0, CardSpace, CardHeight + SelectionLift))
                            return (ConsoleKey)((int)ConsoleKey.D1 + i);
                }
            }
        }

        static bool Hit(int x, int y, int left, int top, int width, int height)
            => x >= left && x < left + width && y >= top && y < top + height;

        public void Dispose()
        {
            if (mouseEnabled) SetConsoleMode(handle, originalMode);
        }

        [StructLayout(LayoutKind.Explicit, Size = 20)]
        struct InputRecord
        {
            [FieldOffset(0)] public ushort Type;
            [FieldOffset(4)] public int KeyDown;
            [FieldOffset(10)] public ushort VirtualKey;
            [FieldOffset(4)] public short MouseX;
            [FieldOffset(6)] public short MouseY;
            [FieldOffset(8)] public uint Buttons;
            [FieldOffset(16)] public uint MouseFlags;
        }

        [DllImport("kernel32.dll")] static extern IntPtr GetStdHandle(int id);
        [DllImport("kernel32.dll")] static extern bool GetConsoleMode(IntPtr handle, out uint mode);
        [DllImport("kernel32.dll")] static extern bool SetConsoleMode(IntPtr handle, uint mode);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        static extern bool ReadConsoleInputW(IntPtr handle, out InputRecord record, uint length, out uint count);
    }

    // Your statistics drawing, kept as provided
    static void DrawStatistics(
        double cash,
        double debt,
        double price,
        double unitCost,
        int inventory,
        double share)
    {
        const int baseColumnWidth = 24;
        const int rowCount = 4;

        int widestWidth = 2 + baseColumnWidth * 2
                            + (rowCount - 1) * 2;

        Console.WriteLine(
            new string(
                ' ',
                Math.Max(0, (Console.WindowWidth - widestWidth + 8) / 2)
            ) + new string('_', widestWidth - 8)
        );

        string[] leftStats =
        {
            "",
            $"  CASH: ${cash:N2}",
            $"   PRICE: ${price:N2}",
            $"    INVENTORY: {inventory:N0}"
        };

        string[] rightStats =
        {
            "",
            $"DEBT: ${debt:N2}",
            $"UNIT COST: ${unitCost:N2}",
            $"SHARE: {share:N1}%"
        };

        for (int i = 0; i < rowCount; i++)
        {
            int columnWidth = baseColumnWidth + i;
            int lineWidth = 2 + columnWidth * 2;

            string left = leftStats[i];
            string right = rightStats[i];

            if (left.Length > columnWidth)
                left = left.Substring(0, columnWidth);

            if (right.Length > columnWidth)
                right = right.Substring(0, columnWidth);

            string content =
                left.PadRight(columnWidth) +
                right.PadRight(columnWidth);

            Console.Write(
                new string(
                    ' ',
                    Math.Max(0, (Console.WindowWidth - lineWidth) / 2)
                ) + "/" + content + "\\"
            );

            Console.WriteLine();
        }
    }
}
