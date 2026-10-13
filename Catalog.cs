using System.Reflection;
using System.Text.Json;

namespace BusinessGame;

// These records read data exported from EconForge. No EconForge engine is used.
public sealed record Course(string Name, List<Lesson> Lessons);
public sealed record Lesson(string Name, LessonData Simulation);
public sealed class LessonData
{
    public string Name { get; set; } = "";
    public string Template { get; set; } = "";
    public string Briefing { get; set; } = "";
    public string Theory { get; set; } = "";
    public string Strategy { get; set; } = "";
    public List<DecisionData> Decisions { get; set; } = [];
    public List<EventData> Events { get; set; } = [];
    public Dictionary<string, double> Parameters { get; set; } = [];
}
public sealed record DecisionData(string Id, string Name, string Unit);
public sealed record EventData(string Name, string Description, Dictionary<string, double> Multipliers,
    Dictionary<string, double>? Additions);

public static class Catalog
{
    public static List<Course> Load()
    {
        using Stream data = Assembly.GetExecutingAssembly().GetManifestResourceStream("BusinessGame.CourseData")
            ?? throw new InvalidOperationException("Course data is missing. Build using BusinessGameV2.csproj.");
        List<Course> courses = JsonSerializer.Deserialize<List<Course>>(data)
            ?? throw new InvalidOperationException("Course data is empty.");
        if (courses.Count != 6 || courses.Any(c => c.Lessons.Count != 10))
            throw new InvalidOperationException("Expected six courses with ten topics each.");
        return courses;
    }
}
