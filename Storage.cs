using System.Text.Json;

namespace TranslateUni;

public sealed record RunRecord(string Course, string Topic, Difficulty Difficulty, int Cleared, bool Won, double Profit,
    DateTimeOffset Finished, string Id = "", bool Endless = false);
public sealed class Preferences
{
    public Difficulty Difficulty { get; set; } = Difficulty.Normal;
    public bool TutorialCompleted { get; set; }
}
public sealed class Storage
{
    readonly string directory;
    public string Notice { get; private set; } = "";
    public Preferences Preferences { get; private set; } = new();
    public List<RunRecord> History { get; private set; } = [];
    public Storage(string? directory = null)
    {
        this.directory = directory ?? Path.Combine(AppContext.BaseDirectory, "data");
        Preferences = Load<Preferences>("settings.json") ?? new();
        if (!Enum.IsDefined(Preferences.Difficulty)) Preferences.Difficulty = Difficulty.Normal;
        History = Load<List<RunRecord>>("runs.json") ?? [];
    }
    T? Load<T>(string name)
    {
        try { string path = Path.Combine(directory, name); return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path)) : default; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { Notice = "Saved data could not be read; using defaults."; return default; }
    }
    void Save<T>(string name, T value)
    {
        try
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, name);
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, path, true);
            Notice = "";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { Notice = "Could not save locally. This session is still playable."; }
    }
    public void SaveSettings() => Save("settings.json", Preferences);
    public void Record(Run run)
    {
        var record = new RunRecord(run.Course.Name, run.Lesson.Name, run.Difficulty, run.Cleared, run.Cleared >= 12,
            run.Reports.Sum(r => r.Profit), DateTimeOffset.Now, run.Id, run.Endless);
        int existing = History.FindIndex(item => item.Id == run.Id);
        if (existing < 0) History.Add(record); else History[existing] = record;
        Save("runs.json", History);
    }
}

