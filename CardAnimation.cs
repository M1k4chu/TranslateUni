namespace TranslateUni;

public enum AnimationStage { Horizontal, Vertical, Star }
public sealed record AnimationFrame(Canvas Canvas, int Delay, AnimationStage Stage, int CardWidth, int CardHeight)
{
    public IReadOnlyList<HitBox> Areas { get; init; } = [];
}

public static class CardAnimation
{
    // Adapted from AnimateAway in the user's first BusinessGame_Debugged.cs:
    // 12 x 35ms horizontal fade, 5 x 45ms vertical collapse, 5 x 70ms star burst.
    public static List<AnimationFrame> Build(Canvas source)
    {
        HitBox[] cards = source.Buttons.Where(b => b.Action == "select").ToArray();
        if (cards.Length == 0) return [];
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
            foreach (HitBox card in cards)
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
            result.Add(new(drawing, 35, AnimationStage.Horizontal, width, 15) { Areas = cards });
        }
        for (int frame = 0; frame < 5; frame++)
        {
            int height = Math.Max(1, 15 - (int)Math.Round((frame + 1) / 5.0 * 14));
            Canvas drawing = clean();
            foreach (HitBox card in cards)
            {
                int x = card.X + (card.Width - 5) / 2, y = card.Y + (card.Height - height) / 2;
                if (height == 1) drawing.Text(x, y, "+---+", ConsoleColor.Yellow);
                else drawing.Box(x, y, 5, height, ConsoleColor.Yellow);
            }
            result.Add(new(drawing, 45, AnimationStage.Vertical, 5, height) { Areas = cards });
        }
        for (int frame = 0; frame < 5; frame++)
        {
            Canvas drawing = clean();
            foreach (HitBox card in cards)
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
            result.Add(new(drawing, 70, AnimationStage.Star, 0, 0) { Areas = cards });
        }
        return result;
    }
}

public sealed partial class GameApp
{
    Canvas? inputFrame;
    Canvas? animationSource;
    public List<AnimationFrame> TakeAnimationFrames()
    {
        Canvas? source = animationSource; animationSource = null;
        return source == null ? [] : CardAnimation.Build(source);
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
