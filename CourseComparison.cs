namespace TranslateUni;

public sealed record CourseDifference(string Focus, string Changes, string Learning);
public static class CourseDifferences
{
    public static CourseDifference For(string course) => course switch
    {
        "Marketing" => new("Course Focus: share +1% and advertising reach +300.","Grow demand with price and advertising; the focus card builds your brand.","Study pricing, segmentation, advertising, retention and competitor response."),
        "Logistics" => new("Course Focus: stock +70 and capacity +40.","Unsold stock costs $0.35/unit/month here, versus $0.08 in other courses.","Study inventory, forecasting, suppliers, capacity and supply disruption."),
        "Probability" => new("Course Focus: risk -15 and permanent demand +5%.","The focus card lowers risk; lesson events explore uncertain outcomes.","Study expected value, forecasts, conditional probability and insurance."),
        "Microeconomics" => new("Course Focus: unit cost -$0.40 and quality +3.","Some topics cap/floor prices, change monopoly demand, or add $3 carbon cost per sale.","Study supply, elasticity, competition, price controls and externalities."),
        "Finance" => new("Course Focus: debt -$250; income +$30/month from next month.","The focus card improves your balance and builds recurring investment income.","Study cash flow, capital structure, liquidity, debt and investment choices."),
        _ => new("Course Focus: quality +10 and capacity +50.","The focus card improves delivery capability and product quality.","Study budgets, resources, scope, risk buffers and project recovery.")
    };
}
public sealed partial class GameApp
{
    void DrawCourseComparison(Canvas c)
    {
        Header(c,"COURSE LIBRARY - HOW COURSES DIFFER","Shared company economy; different focus effects, lessons and events");
        for(int i=0;i<courses.Count;i++)
        {
            int y=5+i*5; Course course=courses[i]; CourseDifference difference=CourseDifferences.For(course.Name);
            c.Text(4,y,course.Name.ToUpperInvariant(),ConsoleColor.Cyan);
            c.Wrapped(6,y+1,difference.Focus,c.Width-12,1,ConsoleColor.White);
            c.Wrapped(6,y+2,difference.Changes,c.Width-12,2);
            c.Wrapped(6,y+4,difference.Learning,c.Width-12,1,ConsoleColor.DarkGray);
        }
        c.Text(4,36,"All courses share decisions, loans, boosters and shops. Topics vary price sensitivity and events.");
        c.Button(4,38,"ENTER BACK TO LIBRARY","comparisonBack");
        Footer(c,"ENTER / C / ESC: Course library");
    }
}
