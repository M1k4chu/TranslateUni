namespace TranslateUni;

public enum AnimationStage { MoveToCenter, Hold, Horizontal, Vertical, Suction, Star }
public sealed record AnimationFrame(Canvas Canvas, int Delay, AnimationStage Stage, int CardWidth, int CardHeight)
{
    public IReadOnlyList<HitBox> Areas { get; init; } = [];
}

public static class CardAnimation
{
    // Adapted from AnimateAway in the user's first BusinessGame_Debugged.cs:
    // 25% faster playback: original delays divided by 1.25 (796ms total).
    public static List<AnimationFrame> Build(Canvas source, int selectedIndex = -1)
    {
        HitBox[] cards = source.Buttons.Where(b => b.Action == "select").ToArray();
        if (cards.Length == 0) return [];
        HitBox? chosen = cards.FirstOrDefault(b => b.Index == selectedIndex);
        HitBox[] collapsing = cards.Where(b => b.Index != selectedIndex).ToArray();
        HitBox[] areas = cards.Select(b => b == chosen ? b with { Y = Math.Max(0, b.Y - 4), Height = b.Height + Math.Min(4, b.Y) } : b).ToArray();
        HitBox? centered = chosen == null ? null : chosen with { X = (cards.Min(b => b.X) + cards.Max(b => b.X + b.Width) - chosen.Width) / 2 };
        if (chosen != null && centered != null)
        {
            int left = Math.Min(chosen.X, centered.X), right = Math.Max(chosen.X + chosen.Width, centered.X + centered.Width);
            areas = areas.Append(new HitBox(left, Math.Max(0, chosen.Y - 4), right - left, chosen.Height + Math.Min(4, chosen.Y), "animation")).ToArray();
        }
        var result = new List<AnimationFrame>();
        string[] lines = source.Lines();
        Canvas clean()
        {
            var frame = new Canvas(source.Width, source.Height); frame.Paste(source, 0, 0);
            foreach (HitBox card in cards) for (int row = 0; row < card.Height; row++)
                frame.Text(card.X, card.Y + row, new string(' ', card.Width));
            return frame;
        }
        if (chosen != null && centered != null)
        {
            for (int step = 1; step <= 8; step++)
            {
                double progress = step / 8.0;
                double eased = progress * progress * (3 - 2 * progress);
                var drawing = new Canvas(source.Width, source.Height); drawing.Paste(source, 0, 0);
                for (int row = 0; row < chosen.Height; row++) drawing.Text(chosen.X, chosen.Y + row, new string(' ', chosen.Width));
                int x = (int)Math.Round(chosen.X + (centered.X - chosen.X) * eased);
                DrawCopy(drawing, source, chosen, x, chosen.Y);
                result.Add(new(drawing, chosen.X == centered.X || step == 8 ? 0 : 25, AnimationStage.MoveToCenter, chosen.Width, chosen.Height) { Areas = areas });
            }
        }
        for (int frame = 0; frame < 12; frame++)
        {
            double progress = (frame + 1) / 12.0;
            int width = Math.Max(5, 22 - (int)Math.Round(progress * 17));
            Canvas drawing = clean();
            foreach (HitBox card in collapsing)
            {
                int x = card.X + (card.Width - width) / 2;
                drawing.Box(x, card.Y, width, card.Height, ConsoleColor.Yellow);
                for (int row = 1; row < card.Height - 1; row++)
                {
                    string text = lines[card.Y + row].Substring(card.X + 1, card.Width - 2).Trim();
                    int visible = Math.Min(width - 2, (int)Math.Round(text.Length * (1 - progress)));
                    drawing.Text(x + 1, card.Y + row, text[..Math.Max(0, visible)]);
                }
            }
            if (chosen != null && centered != null) DrawCopy(drawing, source, chosen, centered.X, centered.Y);
            result.Add(new(drawing, 28, AnimationStage.Horizontal, width, 15) { Areas = areas });
        }
        for (int frame = 0; frame < 5; frame++)
        {
            int height = Math.Max(1, 15 - (int)Math.Round((frame + 1) / 5.0 * 14));
            Canvas drawing = clean();
            foreach (HitBox card in collapsing)
            {
                int x = card.X + (card.Width - 5) / 2, y = card.Y + (card.Height - height) / 2;
                if (height == 1) drawing.Text(x, y, "+---+", ConsoleColor.Yellow);
                else drawing.Box(x, y, 5, height, ConsoleColor.Yellow);
            }
            if (chosen != null && centered != null) DrawCopy(drawing, source, chosen, centered.X, centered.Y);
            result.Add(new(drawing, 36, AnimationStage.Vertical, 5, height) { Areas = areas });
        }
        for (int frame = 0; frame < 5; frame++)
        {
            Canvas drawing = clean();
            foreach (HitBox card in collapsing)
            {
                int x = card.X + card.Width / 2, y = card.Y + card.Height / 2;
                if (frame == 0) drawing.Text(x, y, "*", ConsoleColor.Yellow);
                if (frame == 1) drawing.Text(x - 1, y, "*+*", ConsoleColor.Yellow);
                if (frame == 2)
                {
                    drawing.Text(x - 1, y - 1, "\\ /", ConsoleColor.Yellow);
                    drawing.Text(x, y, "*", ConsoleColor.Yellow);
                    drawing.Text(x - 1, y + 1, "/ \\", ConsoleColor.Yellow);
                }
                if (frame == 3)
                {
                    drawing.Text(x - 2, y, ".   ."); drawing.Text(x, y - 1, "."); drawing.Text(x, y + 1, ".");
                }
            }
            if (chosen != null && centered != null) DrawCopy(drawing, source, chosen, centered.X, centered.Y);
            result.Add(new(drawing, 56, AnimationStage.Star, 0, 0) { Areas = areas });
        }
        if (chosen != null && centered != null)
        {
            Canvas held = clean(); DrawCopy(held, source, chosen, centered.X, centered.Y);
            // The 796ms discard animation already counts toward the 1.5-second pause.
            result.Add(new(held, 1500 - 796, AnimationStage.Hold, chosen.Width, chosen.Height) { Areas = areas });
            for (int step = 1; step <= 17; step++)
            {
                Canvas drawing = clean();
                DrawSuction(drawing, source, centered, step / 17.0, chosen);
                result.Add(new(drawing, step <= 12 ? 28 : 36, AnimationStage.Suction, 0, 0) { Areas = areas });
            }
            for (int step = 0; step < 5; step++)
            {
                Canvas drawing = clean();
                DrawBurst(drawing, centered.X + centered.Width / 2, Math.Max(1, centered.Y - 3), step);
                result.Add(new(drawing, 56, AnimationStage.Star, 0, 0) { Areas = areas });
            }
        }
        return result;
    }
    static void DrawCopy(Canvas drawing, Canvas source, HitBox card, int left, int top)
    {
        string[] lines = source.Lines();
        for (int y = 0; y < card.Height; y++)
        for (int x = 0; x < card.Width; x++)
            drawing.Text(left + x, top + y, lines[card.Y + y][card.X + x].ToString(), source.ColorAt(card.X + x, card.Y + y));
    }
    static void DrawSuction(Canvas drawing, Canvas source, HitBox card, double progress, HitBox original)
    {
        int center = card.X + card.Width / 2, target = Math.Max(1, card.Y - 3);
        double pull = 1 - Math.Pow(1 - progress, 1.5);
        int top = (int)Math.Round(card.Y + (target - card.Y) * pull);
        int bottom = (int)Math.Round(card.Y + card.Height - 1 + (target - (card.Y + card.Height - 1)) * progress);
        if (progress >= 1 || bottom <= top) { drawing.Text(center, target, ".", ConsoleColor.Yellow); return; }
        string[] lines = source.Lines();
        for (int y = top; y <= bottom; y++)
        {
            double fraction = (y - top) / (double)Math.Max(1, bottom - top);
            // The top pinches first; the rounded lower edge follows it into the dot.
            double taper = 1 - pull * (1 - Math.Sin(fraction * Math.PI / 2)) * 0.85;
            int half = Math.Max(0, (int)Math.Round(card.Width / 2.0 * (1 - progress) * taper));
            if (half == 0) { drawing.Text(center, y, "|", ConsoleColor.Yellow); continue; }
            int left = center - half, width = half * 2 + 1;
            if (y == top || y == bottom) drawing.Text(left, y, "+" + new string('-', width - 2) + "+", ConsoleColor.Yellow);
            else
            {
                drawing.Text(left, y, "/" + new string(' ', width - 2) + "\\", ConsoleColor.Yellow);
                int row = original.Y + Math.Clamp((int)Math.Round(fraction * (card.Height - 1)), 1, card.Height - 2);
                string text = lines[row].Substring(original.X + 1, original.Width - 2).Trim();
                int visible = Math.Min(width - 2, (int)Math.Round(text.Length * (1 - progress)));
                drawing.Text(center - visible / 2, y, text[..Math.Max(0, visible)], source.ColorAt(original.X + 2, row));
            }
        }
        drawing.Text(center, target, ".", ConsoleColor.Yellow);
    }
    static void DrawBurst(Canvas drawing, int x, int y, int frame)
    {
        if (frame == 0) drawing.Text(x, y, "*", ConsoleColor.Yellow);
        if (frame == 1) drawing.Text(x - 1, y, "*+*", ConsoleColor.Yellow);
        if (frame == 2)
        { drawing.Text(x - 1, y - 1, @"\ /", ConsoleColor.Yellow); drawing.Text(x, y, "*", ConsoleColor.Yellow); drawing.Text(x - 1, y + 1, @"/ ", ConsoleColor.Yellow); }
        if (frame == 3)
        { drawing.Text(x - 2, y, ".   ."); drawing.Text(x, y - 1, "."); drawing.Text(x, y + 1, "."); }
    }
}

public sealed partial class GameApp
{
    Canvas? inputFrame;
    Canvas? animationSource;
    int animationSelected = -1;
    public List<AnimationFrame> TakeAnimationFrames()
    {
        Canvas? source = animationSource; animationSource = null;
        return source == null ? [] : CardAnimation.Build(source, animationSelected);
    }
    public void PlayPendingAnimation()
    {
        foreach (AnimationFrame frame in TakeAnimationFrames())
        {
            if (Console.WindowWidth < frame.Canvas.Width || Console.WindowHeight < frame.Canvas.Height) break;
            try { frame.Canvas.FlushRegions(frame.Areas); }
            catch (ArgumentOutOfRangeException) { break; }
            Thread.Sleep(frame.Delay);
        }
    }
}
