class Program
{
    const int CardWidth = 22;
    const int CardHeight = 10;
    const int CardSpace = 24;
    static int rerolls = 3;


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

        int baseY = 2;

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
        int selected = 0;
        bool confirmed = false;

        try
        {
            Console.CursorVisible = false;
            Console.Clear();

            DrawScene(cards, selected, false);

            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(true);

                if (key.Key == ConsoleKey.Escape)
                    break;

                if (confirmed)
                {
                    if (key.Key == ConsoleKey.R && rerolls > 0)
                    {
                        rerolls--;

                        // Animate the currently chosen card away
                        AnimateChosenCardAway(cards[selected], selected);

                        // Deal a fresh set of three cards
                        cards = DrawThreeCards();

                        selected = 0;
                        confirmed = false;

                        DrawScene(cards, selected, false);
                    }

                    continue;
                }

                int previous = selected;

                // Arrows move between cards without wrapping
                if (key.Key == ConsoleKey.LeftArrow ||
                    key.Key == ConsoleKey.UpArrow)
                {
                    selected = Math.Max(0, selected - 1);
                }
                else if (key.Key == ConsoleKey.RightArrow ||
                         key.Key == ConsoleKey.DownArrow)
                {
                    selected = Math.Min(cards.Length - 1, selected + 1);
                }

                // Keep number-key selection too
                else if (key.Key == ConsoleKey.D1 ||
                         key.Key == ConsoleKey.NumPad1)
                    selected = 0;

                else if (key.Key == ConsoleKey.D2 ||
                         key.Key == ConsoleKey.NumPad2)
                    selected = 1;

                else if (key.Key == ConsoleKey.D3 ||
                         key.Key == ConsoleKey.NumPad3)
                    selected = 2;

                else if (key.Key == ConsoleKey.Enter)
                {
                    AnimateAway(cards, selected);
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
            if (i == selected)
                Console.ForegroundColor = confirmed
                    ? ConsoleColor.Green
                    : ConsoleColor.Yellow;
            else
                Console.ForegroundColor = ConsoleColor.Gray;

            DrawCardAt(
                cards[i],
                startX + i * CardSpace,
                baseY,
                CardWidth,
                CardHeight
            );
        }
        Console.ResetColor();

        // Keep your statistics values and drawing unchanged
        if (Console.BufferHeight > 19)
            Console.SetCursorPosition(0, 14);

        DrawStatistics(
            cash: 5000,
            debt: 1200,
            price: 25,
            unitCost: 12,
            inventory: 450,
            share: 18
        );
        DrawRerollCounter();

        // Three empty lines at the bottom
        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine();

        WriteAt(
            0,
            Math.Min(23, Console.BufferHeight - 1),
            confirmed
                ? (rerolls > 0
                    ? $"Selection confirmed | R: Reroll ({rerolls} left) | ESC: Exit"
                    : "No rerolls left | ESC: Exit")
                : "Arrows or 1-3: Select | ENTER: Confirm | ESC: Exit"
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

        WriteAt(x, y, ",---,");
        WriteAt(x, y + 1, "| " + rerolls + " |");
        WriteAt(x, y + 2, "´---´");
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
