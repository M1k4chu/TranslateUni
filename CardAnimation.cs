namespace TranslateUni;

public enum AnimationStage { Horizontal, Vertical, Star }
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
        var result = new List<AnimationFrame>();
        string[] lines = source.Lines();
        Canvas clean()
        {
            var frame = new Canvas(source.Width, source.Height); frame.Paste(source, 0, 0);
            foreach (HitBox card in cards) for (int row = 0; row < card.Height; row++)
                frame.Text(card.X, card.Y + row, new string(' ', card.Width));
            return frame;
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
            if (chosen != null) DrawSuction(drawing, source, chosen, (frame + 1) / 17.0);
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
            if (chosen != null) DrawSuction(drawing, source, chosen, (frame + 13) / 17.0);
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
            if (chosen != null) DrawBurst(drawing, chosen.X + chosen.Width / 2, Math.Max(1, chosen.Y - 3), frame);
            result.Add(new(drawing, 56, AnimationStage.Star, 0, 0) { Areas = areas });
        }
        return result;
    }
    static void DrawSuction(Canvas drawing, Canvas source, HitBox card, double progress)
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
                int row = card.Y + Math.Clamp((int)Math.Round(fraction * (card.Height - 1)), 1, card.Height - 2);
                string text = lines[row].Substring(card.X + 1, card.Width - 2).Trim();
                int visible = Math.Min(width - 2, (int)Math.Round(text.Length * (1 - progress)));
                drawing.Text(center - visible / 2, y, text[..Math.Max(0, visible)], source.ColorAt(card.X + 2, row));
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
