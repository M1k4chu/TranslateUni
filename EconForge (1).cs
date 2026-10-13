using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EconForge
{
    // Explicit entry point: this file contains no top-level statements.
    public static class Program
    {
        public static int Main(string[] args)
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
            try
            {
                string dataDirectory = Path.Combine(Environment.CurrentDirectory, "data");
                for (int i = 0; i < args.Length; i++)
                {
                    if (args[i] != "--data-dir") continue;
                    if (++i == args.Length) throw new ArgumentException("--data-dir requires a directory path.");
                    dataDirectory = args[i];
                }
                ConsoleApp app = new(new LocalStore(dataDirectory));
                if (args.Contains("--smoke-test")) app.SmokeTest();
                else if (args.Contains("--verify")) app.VerifyAll();
                else if (args.Contains("--list-courses")) app.ListCourses();
                else app.Run();
                return 0;
            }
            catch (EndOfStreamException)
            {
                Terminal.Write("Input closed. Completed rounds have already been saved.");
                return 0;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or JsonException)
            {
                Terminal.Panel("Unable to complete the action", [ex.Message, "Check the data directory and try again."]);
                return 1;
            }
        }
    }

    // Domain types, catalog, engines, persistence and UI share one source file.
    public enum Template { Pricing, Marketing, Inventory, BeerGame, Market, Investment, CashFlow, Portfolio, Probability, Project }
    public enum Difficulty { Easy, Normal, Hard }
    public enum DecisionType { Integer, Decimal, Percentage, YesNo, MultipleChoice, BudgetAllocation, RankOrdering }
    public sealed record DecisionDefinition(string Id, string Name, DecisionType Type, decimal Min, decimal Max, decimal Default, string Unit = "", string[]? Options = null);
    public sealed record Decision(decimal Number, Dictionary<string, decimal>? Allocation = null, List<string>? Ranking = null);
    public sealed record ScoringRule(string Metric, double Weight, double Min, double Max, bool HigherIsBetter = true);
    public sealed record MarketEvent(string Name, string Description, int Round, Dictionary<string, double> Multipliers, Dictionary<string, double>? Additions = null, double Probability = 1);
    public enum TeamRole { Strategy, Finance, Marketing, Operations }
    public sealed record Team(string Id, string Name, bool IsAi, List<string> Players)
    {
        public TeamRole Role { get; set; } = TeamRole.Strategy;
    }
    public static class Career
    {
        // Derive XP from saved sessions: replaying or reopening cannot award it twice.
        public static int Xp(IEnumerable<Session> sessions, string name) => sessions
            .GroupBy(s => s.Id).Select(g => g.First())
            .Where(s => s.Teams.Any(t => !t.IsAi && string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
            .Sum(s => s.History.Count * 20 + (s.Completed ? 100 : 0));
        public static int Level(int xp) => 1 + Math.Max(0, xp) / 100;
        public static string Title(int xp) => Level(xp) switch
        {
            1 => "Intern", 2 => "Analyst", 3 => "Senior Analyst", 4 => "Manager", _ => "Director"
        };
        public static string Status(int xp) => $"Level {Level(xp)} - {Title(xp)} | {xp} XP | [{new string('#', xp % 100 / 10)}{new string('-', 10 - xp % 100 / 10)}] {100 - xp % 100} XP to next level";
        public static string Guidance(TeamRole role) => role switch
        {
            TeamRole.Finance => "FINANCE: Protect cash, compare costs and returns, and watch debt.",
            TeamRole.Marketing => "MARKETING: Balance customer demand, market share and margin.",
            TeamRole.Operations => "OPERATIONS: Check capacity, inventory, delays and delivery risk.",
            _ => "STRATEGY: Balance the scoring targets and explain your trade-offs."
        };
    }
    public sealed record Lesson(string Name, SimulationDefinition Simulation);
    public sealed record Course(string Name, List<Lesson> Lessons);
    public sealed class SimulationDefinition
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public Template Template { get; set; }
        public string Briefing { get; set; } = "";
        public string Objective { get; set; } = "";
        public string Theory { get; set; } = "";
        public string Strategy { get; set; } = "";
        public List<DecisionDefinition> Decisions { get; set; } = [];
        public List<ScoringRule> Scoring { get; set; } = [];
        public List<MarketEvent> Events { get; set; } = [];
        public Dictionary<string, double> Parameters { get; set; } = [];
    }
    public sealed class MarketState
    {
        public double Demand { get; set; } = 10000;
        public double UnitCost { get; set; } = 1.2;
        public double CompetitorPrice { get; set; } = 2.5;
        public double CapacityFactor { get; set; } = 1;
        public double Interest { get; set; } = 0.04;
        public double Preference { get; set; } = 1;
        public List<string> News { get; set; } = [];
        public MarketState Clone() => new()
        {
            Demand = Demand,
            UnitCost = UnitCost,
            CompetitorPrice = CompetitorPrice,
            CapacityFactor = CapacityFactor,
            Interest = Interest,
            Preference = Preference,
            News = [.. News]
        };
    }
    public sealed class TeamState
    {
        public decimal Cash { get; set; } = 40000;
        public decimal Price { get; set; } = 2.5m;
        public decimal Profit { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal Revenue { get; set; }
        public decimal Cost { get; set; }
        public decimal Debt { get; set; }
        public double Inventory { get; set; } = 2000;
        public double Backlog { get; set; }
        public double Capacity { get; set; } = 4000;
        public double MarketShare { get; set; } = 18;
        public double Satisfaction { get; set; } = 76;
        public double Progress { get; set; }
        public List<double> Pipeline { get; set; } = [];
        public Dictionary<string, double> Metrics { get; set; } = [];
        public TeamState Clone() => new()
        {
            Cash = Cash,
            Price = Price,
            Profit = Profit,
            TotalProfit = TotalProfit,
            Revenue = Revenue,
            Cost = Cost,
            Debt = Debt,
            Inventory = Inventory,
            Backlog = Backlog,
            Capacity = Capacity,
            MarketShare = MarketShare,
            Satisfaction = Satisfaction,
            Progress = Progress,
            Pipeline = [.. Pipeline],
            Metrics = new(Metrics)
        };
    }
    public sealed class RoundSnapshot
    {
        public int Number { get; set; }
        public MarketState Market { get; set; } = new();
        public Dictionary<string, TeamState> Before { get; set; } = [];
        public Dictionary<string, TeamState> After { get; set; } = [];
        public Dictionary<string, Dictionary<string, Decision>> Decisions { get; set; } = [];
        public Dictionary<string, string> Explanations { get; set; } = [];
        public Dictionary<string, double> Scores { get; set; } = [];
    }
    public sealed class Session
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
        public string Course { get; set; } = "";
        public string Lesson { get; set; } = "";
        public SimulationDefinition Definition { get; set; } = new();
        public int Seed { get; set; } = 42;
        public Difficulty Difficulty { get; set; } = Difficulty.Normal;
        public int Rounds { get; set; } = 5;
        public int DurationMinutes { get; set; } = 20;
        public bool Lecturer { get; set; }
        public List<Team> Teams { get; set; } = [];
        public MarketState Market { get; set; } = new();
        public Dictionary<string, TeamState> States { get; set; } = [];
        public List<RoundSnapshot> History { get; set; } = [];
        public bool Completed { get; set; }
    }
    public sealed class Settings
    {
        public int Seed { get; set; } = 42;
        public int Rounds { get; set; } = 5;
        public int DurationMinutes { get; set; } = 20;
        public Difficulty Difficulty { get; set; } = Difficulty.Normal;
        public bool DetailedMetrics { get; set; }
    }
    public sealed record SimulationResult(Dictionary<string, TeamState> States, Dictionary<string, string> Explanations);
    public sealed record BoardEntry(int Rank, string TeamId, string Name, double Score, decimal Profit);

    // Six packs reuse a small set of economic models instead of sixty engines.
    public static class Curriculum
    {
        public static List<Course> Generate()
        {
            string[][] topics =
            [
                ["Pricing", "Market Segmentation", "Advertising Budget", "Customer Acquisition", "Product Positioning", "Brand Investment", "Promotion", "Customer Retention", "Marketing Mix", "Competitor Response"],
                ["Inventory Management", "Beer Game and Bullwhip Effect", "Demand Forecasting", "Supplier Selection", "Lead Times", "Safety Stock", "Capacity Constraints", "Procurement", "Distribution", "Supply Disruption"],
                ["Expected Value", "Binomial Demand", "Poisson Arrivals", "Conditional Probability", "Bayes and Base Rates", "Risk and Variance", "Normal Demand Forecast", "Monte Carlo Sampling", "Decision Trees", "Insurance and Rare Events"],
                ["Demand and Supply", "Price Elasticity", "Consumer Surplus", "Producer Surplus", "Price Ceiling", "Price Floor", "Monopoly Pricing", "Oligopoly", "Externalities", "Market Equilibrium"],
                ["Net Present Value", "Internal Rate of Return", "Working Capital", "Capital Structure", "Liquidity Management", "Cost of Capital", "Debt Financing", "Investment Selection", "Risk-Return Tradeoff", "Dividend versus Reinvestment"],
                ["Project Budget", "Scope and Quality", "Resource Scheduling", "Cost Overrun", "Critical Priorities", "Delivery Milestones", "Project Staffing", "Risk Buffer", "Stakeholder Satisfaction", "Project Recovery"]
            ];
            string[] names = ["Marketing", "Logistics", "Probability", "Microeconomics", "Finance", "Project Management"];
            List<Course> courses = [];
            for (int c = 0; c < names.Length; c++)
            {
                List<Lesson> lessons = [];
                for (int i = 0; i < topics[c].Length; i++) lessons.Add(new(topics[c][i], Create(names[c], topics[c][i], i)));
                courses.Add(new(names[c], lessons));
            }
            return courses;
        }
        public static SimulationDefinition FreshFizz() => Generate()[0].Lessons[0].Simulation;
        private static DecisionDefinition D(string id, string label, DecisionType type,
            decimal min, decimal max, decimal value, string unit = "", params string[] options)
        {
            return new(id, label, type, min, max, value, unit, options.Length == 0 ? null : options);
        }
        private static SimulationDefinition Create(string course, string topic, int index)
        {
            // A lesson selects a template; parameters vary without duplicating its formulas.
            Template template = course switch
            {
                "Marketing" => index is 0 or 6 or 9 ? Template.Pricing : Template.Marketing,
                "Logistics" => index == 1 ? Template.BeerGame : Template.Inventory,
                "Probability" => Template.Probability,
                "Microeconomics" => index is 1 or 6 or 7 ? Template.Pricing : Template.Market,
                "Finance" => index is 2 or 4 ? Template.CashFlow : index == 8 ? Template.Portfolio : Template.Investment,
                _ => Template.Project
            };
            SimulationDefinition d = new()
            {
                Id = (course + "-" + topic).ToLowerInvariant().Replace(' ', '-'),
                Name = course == "Marketing" && index == 0 ? "FreshFizz Pricing Challenge" : topic + " Decision Lab",
                Template = template,
                Objective = "Compare the consequences of your decisions with the weighted scoring targets.",
                Parameters = new()
                {
                    ["Elasticity"] = 1.2 + index * 0.1,
                    ["FixedCost"] = 500 + index * 20,
                    ["InventoryDemand"] = template == Template.BeerGame ? 20 : 800 + index * 50,
                    ["OrderDelay"] = 1,
                    ["ShippingDelay"] = index == 4 ? 2 : 1,
                    ["Yield"] = 0.27 + index * 0.013,
                    ["OutsideFirms"] = 2,
                    ["Mode"] = index
                }
            };
            if (template is Template.Pricing or Template.Marketing)
            {
                d.Briefing = $"{course}: {topic}. Set a selling price in a finite beverage market. Initial price: 2.50 EUR; unit cost: 1.20 EUR; historical share: 18%; market demand: 10,000 units.";
                d.Theory = "A price cut raises relative demand, but profit rises only when extra sales cover the lower contribution margin. Advertising has diminishing returns.";
                d.Strategy = "Compare units sold, margin and inventory after small price changes. Limit marketing to spending that creates additional contribution.";
                d.Decisions =
                [
                    D("Price", "Selling price", DecisionType.Decimal, 0.5m, 8, 2.5m, "EUR")
                ];
                if (template == Template.Marketing)
                {
                    d.Decisions.Add(D("Allocation", "Marketing budget", DecisionType.BudgetAllocation, 0, 5000, 1000, "EUR total", "Acquisition", "Retention", "Brand"));
                    d.Decisions.Add(D("Segment", "Target segment", DecisionType.MultipleChoice, 1, 2, 1, "", "Value seekers", "Premium buyers"));
                }
                d.Scoring =
                [
                    new("Profit", 0.4, -10000, 16000),
                    new("Share", 0.25, 0, 40),
                    new("Satisfaction", 0.2, 0, 100),
                    new("PriceStability", 0.15, 0, 100)
                ];
                if (topic == "Monopoly Pricing") d.Parameters["OutsideFirms"] = 0;
            }
            if (template is Template.Inventory or Template.BeerGame)
            {
                bool beer = template == Template.BeerGame;
                int units = beer ? 20 : (int)d.Parameters["InventoryDemand"];
                d.Briefing = $"{topic}: order stock and forecast customer demand. Orders take {d.Parameters["OrderDelay"]} period(s) to process plus {d.Parameters["ShippingDelay"]} period(s) to ship. Initial expected demand: {units} units.";
                d.Theory = "Inventory and backlog carry forward. Ignoring orders already in transit creates over-ordering and the bullwhip effect.";
                d.Strategy = "Add a measured correction for stock and backlog to expected demand; subtract goods already in the pipeline.";
                d.Decisions =
                [
                    D("Order", "Order quantity", DecisionType.Integer, 0, beer ? 150 : 5000, units, "units"),
                    D("Forecast", "Demand forecast", DecisionType.Integer, 0, beer ? 150 : 5000, units, "units")
                ];
                if (topic == "Supplier Selection") d.Decisions.Add(D("Supplier", "Supplier", DecisionType.MultipleChoice, 1, 2, 1, "", "Standard: cheaper, normal capacity", "Express: 20% premium, priority capacity"));
                d.Scoring =
                [
                    new("InventoryEfficiency", 0.4, 0, 100),
                    new("ForecastAccuracy", 0.3, 0, 100),
                    new("Profit", 0.2, beer ? -1000 : -5000, beer ? 300 : 10000),
                    new("Satisfaction", 0.1, 0, 100)
                ];
            }
            if (template == Template.Market)
            {
                d.Briefing = $"{topic}: choose price and production under a demand curve, production cost and a market regulation. Your results include unmet demand, stock and consumer/producer surplus.";
                d.Theory = "Demand falls as price rises. A binding ceiling can create a shortage, a floor can leave unsold goods, and carbon charges make external costs visible.";
                d.Strategy = "Compare effective price with cost and willingness to pay. Produce only the volume you expect to sell.";
                int policy = topic == "Price Ceiling" ? 2 : topic == "Price Floor" ? 3 : topic == "Externalities" ? 4 : 1;
                d.Decisions =
                [
                    D("Price", "Selling price", DecisionType.Decimal, 0.5m, 8, 2.5m, "EUR"),
                    D("Production", "Production", DecisionType.Integer, 0, 6000, 2400, "units"),
                    D("Policy", "Market rule", DecisionType.MultipleChoice, 1, 4, policy, "", "Free market", "Price ceiling at 2.00 EUR", "Price floor at 3.00 EUR", "Carbon charge: 0.30 EUR per unit")
                ];
                d.Scoring =
                [
                    new("Profit", 0.4, -10000, 15000),
                    new("Service", 0.3, 0, 100),
                    new("Welfare", 0.3, 0, 10000)
                ];
            }
            if (template == Template.Investment)
            {
                d.Briefing = $"{topic}: invest now for four future period cash flows. Yield per period: {d.Parameters["Yield"] * 100:F1}% of capital; discount rate: funding rate plus 4 percentage points. New capital earns from the next round.";
                d.Theory = "NPV discounts future cash flows; IRR is the rate where NPV is zero. Borrowing adds interest and can exhaust liquidity before returns arrive.";
                d.Strategy = "Approve positive-NPV capital only when the cash buffer can cover the payment delay. Compare IRR with the cost of capital.";
                d.Decisions =
                [
                    D("Approve", "Approve investment", DecisionType.YesNo, 0, 1, 1),
                    D("Investment", "Capital investment", DecisionType.Decimal, 0, 20000, 8000, "EUR"),
                    D("Borrowing", "New borrowing", DecisionType.Decimal, 0, 20000, 0, "EUR")
                ];
                d.Scoring =
                [
                    new("Profit", 0.3, -10000, 30000),
                    new("InvestmentQuality", 0.3, 0, 100),
                    new("Liquidity", 0.2, 0, 60000),
                    new("DebtProtection", 0.2, 0, 100)
                ];
            }
            if (template == Template.CashFlow)
            {
                d.Briefing = $"{topic}: choose customer payment terms, borrowing and debt repayment. Credit terms attract sales but defer receipts until the following round.";
                d.Theory = "A sale is not the same as collected cash. Receivables absorb working capital; interest is charged on outstanding debt.";
                d.Strategy = "Keep enough cash to cover operating costs while customers pay. Borrow for timing gaps, rather than to fund repeated losses.";
                d.Decisions =
                [
                    D("CreditDays", "Customer credit period", DecisionType.Integer, 0, 90, 30, "days"),
                    D("Borrowing", "New borrowing", DecisionType.Decimal, 0, 20000, 0, "EUR"),
                    D("Repayment", "Debt repayment", DecisionType.Decimal, 0, 20000, 0, "EUR")
                ];
                d.Scoring =
                [
                    new("Liquidity", 0.4, 0, 70000),
                    new("Profit", 0.3, -10000, 20000),
                    new("DebtProtection", 0.2, 0, 100),
                    new("CollectionEfficiency", 0.1, 0, 100)
                ];
            }
            if (template == Template.Portfolio)
            {
                d.Briefing = "Allocate 100 percent of current wealth to stocks, bonds and cash. Demand news affects stocks; a rate hike affects bond returns.";
                d.Theory = "Diversification changes the range of outcomes. A risky allocation can earn a higher return while reducing downside protection.";
                d.Strategy = "Keep a cash buffer and adjust exposure when interest rates rise. Judge allocation by return and risk together.";
                d.Decisions =
                [
                    D("Allocation", "Portfolio allocation", DecisionType.BudgetAllocation, 100, 100, 100, "% total", "Stocks", "Bonds", "Cash")
                ];
                d.Scoring =
                [
                    new("Profit", 0.45, -10000, 20000),
                    new("Risk", 0.35, 0, 100),
                    new("Liquidity", 0.2, 20000, 60000)
                ];
            }
            if (template == Template.Project)
            {
                d.Parameters["Contract"] = 50000 + index * 1500;
                d.Parameters["Scope"] = 1 + index * 0.02;
                d.Briefing = $"{topic}: deliver a project worth {d.Parameters["Contract"]:N0} EUR. Each round pays staff and budget costs; the contract pays only once, at 100% progress.";
                d.Theory = "Staff and spending advance delivery; schedule, quality and cost priorities compete. Payment arrives after completion, so delays consume cash.";
                d.Strategy = "Fund enough progress to finish within the session. Protect the cash buffer and choose the priority that addresses the current constraint.";
                d.Decisions =
                [
                    D("Budget", "Period budget", DecisionType.Decimal, 0, 12000, 4000, "EUR"),
                    D("Employees", "Project employees", DecisionType.Integer, 1, 30, 12),
                    D("Priority", "Project priority order", DecisionType.RankOrdering, 0, 0, 0, "", "Quality", "Schedule", "Cost")
                ];
                d.Scoring =
                [
                    new("Progress", 0.4, 0, 100),
                    new("Profit", 0.3, -30000, 25000),
                    new("Satisfaction", 0.2, 0, 100),
                    new("Liquidity", 0.1, 0, 60000)
                ];
            }
            if (template == Template.Probability) ConfigureProbability(d, topic, index);
            if (template != Template.Probability)
            {
                d.Events =
                [
                    new("Demand shift", "Market demand increases by 20 percent.", 2, new() { ["Demand"] = 1.2 }),
                    new("Cost shock", "Production cost increases by 18 percent.", 4, new() { ["UnitCost"] = 1.18 }),
                    new("Recovery", "Demand returns toward its initial level.", 5, new() { ["Demand"] = 1 / 1.2 })
                ];
                if (template == Template.Inventory && index is 6 or 9) d.Events.Add(new("Supplier disruption", "New shipment capacity falls by 30 percent.", 3, new() { ["Capacity"] = 0.7 }));
                if (template is Template.Investment or Template.Portfolio) d.Events.Add(new("Rate hike", "Funding rates rise by 2 percentage points.", 3, [], new() { ["Interest"] = 0.02 }));
                if (template == Template.Project) d.Events.Add(new("Scope change", "Delivery productivity falls by 10 percent.", 3, new() { ["Capacity"] = 0.9 }));
            }
            if (course == "Marketing" && index == 0)
            {
                d.Parameters["OutsideFirms"] = 0;
                d.Parameters["Elasticity"] = 1.6;
                d.Events =
                [
                    new("Competitor price cut", "The reference competitor lowers its price by 10 percent.", 2, new() { ["CompetitorPrice"] = 0.9 }),
                    new("Viral trend", "Market demand rises by 35 percent.", 3, new() { ["Demand"] = 1.35 }),
                    new("Raw material shock", "Unit costs rise by 18 percent.", 4, new() { ["UnitCost"] = 1.18 }),
                    new("Equilibrium returns", "Demand and competitor prices return toward their initial values.", 5, new() { ["Demand"] = 1 / 1.35, ["CompetitorPrice"] = 1 / 0.9 })
                ];
            }
            return d;
        }
        private static void ConfigureProbability(SimulationDefinition d, string topic, int mode)
        {
            // Mode numbers follow the ten Probability topics in Generate().
            d.Parameters["Probability"] = 0.35;
            d.Parameters["Trials"] = 40;
            d.Parameters["Lambda"] = 20;
            d.Parameters["Prior"] = 0.02;
            d.Parameters["Sensitivity"] = 0.95;
            d.Parameters["FalsePositive"] = 0.10;
            d.Parameters["Mean"] = 30;
            d.Parameters["Deviation"] = 6;
            d.Parameters["Joint"] = 0.18;
            d.Parameters["Condition"] = 0.30;
            d.Parameters["Mode"] = mode;
            d.Briefing = $"{topic}: make a decision using the stated probability model, then compare expected and realized outcomes. Seeded draws reproduce the same experiment.";
            d.Theory = mode switch
            {
                1 => "Binomial demand counts successes in independent trials. E[X]=n*p; serving every possible outcome can create expensive overstock.",
                2 => "Poisson arrivals have mean and variance lambda. Capacity reduces the probability of a queue but carries a cost.",
                3 => "P(A|B)=P(A and B)/P(B). Conditional probability differs from unconditional probability.",
                4 => "Bayes updates the base rate with sensitivity and false positives. A positive signal need not imply a high posterior probability.",
                5 => "Expected return and variance measure different properties. More exposure increases both expected upside and loss variability.",
                6 => "Normal demand varies around a mean. Safety stock trades additional carrying cost against the probability of a shortage.",
                7 => "Monte Carlo estimates vary across samples. Standard error shrinks roughly with the square root of sample size; sampling also costs time.",
                8 => "A decision tree weights branch payoffs by their probabilities before choosing an action.",
                9 => "Insurance replaces a rare large loss with a known premium. Expected cost and protection against tail losses can conflict.",
                _ => "Expected value is the probability-weighted payoff, not a promise about one realized outcome. Compare risky choices with a guaranteed alternative."
            };
            d.Strategy = "Calculate the expected outcome before deciding. Use the probability model to justify protection against unfavorable outcomes.";
            if (mode is 1 or 2 or 6)
            {
                d.Decisions =
                [
                    D("Stock", "Stock or service capacity", DecisionType.Integer, 0, 100, mode == 1 ? 18 : mode == 2 ? 22 : 35, "units"),
                    D("Estimate", "Expected demand estimate", DecisionType.Decimal, 0, 100, mode == 1 ? 14 : mode == 2 ? 20 : 30, "units")
                ];
                d.Scoring =
                [
                    new("ExpectedProfit", 0.5, -1000, 1500),
                    new("Service", 0.3, 0, 100),
                    new("ForecastAccuracy", 0.2, 0, 100)
                ];
            }
            else if (mode is 3 or 4)
            {
                d.Decisions =
                [
                    D("Estimate", "Conditional probability estimate", DecisionType.Percentage, 0, 100, 50, "%"),
                    D("Reject", "Reject the flagged shipment", DecisionType.YesNo, 0, 1, 0)
                ];
                d.Scoring =
                [
                    new("ForecastAccuracy", 0.6, 0, 100),
                    new("ExpectedProfit", 0.4, -1000, 0)
                ];
            }
            else if (mode == 7)
            {
                d.Decisions =
                [
                    D("Samples", "Monte Carlo sample size", DecisionType.Integer, 10, 500, 100, "draws"),
                    D("Estimate", "Success probability estimate", DecisionType.Percentage, 0, 100, 35, "%")
                ];
                d.Scoring =
                [
                    new("Precision", 0.4, 0, 100),
                    new("ForecastAccuracy", 0.4, 0, 100),
                    new("CostEfficiency", 0.2, 0, 100)
                ];
            }
            else if (mode == 9)
            {
                d.Decisions =
                [
                    D("Insure", "Buy insurance", DecisionType.YesNo, 0, 1, 1),
                    D("Estimate", "Loss probability estimate", DecisionType.Percentage, 0, 100, 2, "%")
                ];
                d.Scoring =
                [
                    new("ExpectedProfit", 0.3, -2000, 0),
                    new("Risk", 0.4, 0, 100),
                    new("ForecastAccuracy", 0.3, 0, 100)
                ];
            }
            else
            {
                d.Decisions =
                [
                    D("Exposure", "Risky project exposure", DecisionType.Percentage, 0, 100, 50, "% of a 1,000 EUR budget")
                ];
                d.Scoring =
                [
                    new("ExpectedProfit", 0.7, -2000, 5000),
                    new("Risk", 0.3, 0, 100)
                ];
            }
            d.Events =
            [
                new("Stable experiment", "Model parameters stay fixed; a new seeded sample is drawn.", 0, [])
            ];
        }
    }

    // Shared formulas. Money is decimal; probabilities and physical units are double.
    // Simulation methods always clone their input states; replay never changes history.
    public sealed class SimulationEngine
    {
        public SimulationResult Run(SimulationDefinition definition, Difficulty difficulty, int seed, int round,
            MarketState market, Dictionary<string, TeamState> before,
            Dictionary<string, Dictionary<string, Decision>> choices)
        {
            Dictionary<string, TeamState> after = before.ToDictionary(p => p.Key, p => p.Value.Clone());
            Dictionary<string, string> explanations = [];
            if (definition.Template is Template.Pricing or Template.Marketing)
                RunPricing(definition, difficulty, seed, round, market, before, after, choices, explanations);
            else
            {
                foreach ((string id, TeamState state) in after)
                {
                    Dictionary<string, Decision> decision = choices[id];
                    explanations[id] = definition.Template switch
                    {
                        Template.Inventory or Template.BeerGame => Inventory(definition, difficulty, seed, round, id, market, state, decision),
                        Template.Market => Market(definition, difficulty, seed, round, id, market, state, decision),
                        Template.Investment => Investment(definition, market, state, decision),
                        Template.CashFlow => CashFlow(market, state, decision),
                        Template.Portfolio => Portfolio(seed, round, id, market, state, decision),
                        Template.Probability => Probability(definition, seed, round, id, market, state, decision),
                        Template.Project => Project(definition, market, state, decision),
                        _ => throw new ArgumentException("Unsupported simulation template.")
                    };
                }
            }
            foreach (TeamState state in after.Values)
                if (state.Metrics.Values.Any(value => !double.IsFinite(value)))
                    throw new InvalidOperationException("A model produced a non-finite metric.");
            return new(after, explanations);
        }
        private static double Uncertainty(Difficulty difficulty) => difficulty switch
        {
            Difficulty.Easy => 0.01,
            Difficulty.Hard => 0.07,
            _ => 0.03
        };
        private static void RunPricing(SimulationDefinition d, Difficulty difficulty, int seed, int round,
            MarketState market, Dictionary<string, TeamState> before, Dictionary<string, TeamState> after,
            Dictionary<string, Dictionary<string, Decision>> choices, Dictionary<string, string> explanations)
        {
            double elasticity = MathModel.P(d, "Elasticity", 1.6);
            double outsideFirms = MathModel.P(d, "OutsideFirms", 2) + (difficulty == Difficulty.Hard ? 1 : 0);
            Dictionary<string, double> weights = [];
            foreach ((string id, TeamState state) in after)
            {
                Dictionary<string, Decision> decision = choices[id];
                state.Price = MathModel.Money(MathModel.D(decision, "Price", 2.5));
                double spend = decision.GetValueOrDefault("Allocation")?.Allocation is { } allocation ? (double)allocation.Values.Sum() : 0;
                double retention = decision.GetValueOrDefault("Allocation")?.Allocation is { } mix ? (double)mix.GetValueOrDefault("Retention") / Math.Max(1, spend) : 0;
                bool premium = MathModel.D(decision, "Segment", 1) == 2;
                double segment = premium ? 1 + ((double)state.Price - 2.5) * 0.05 : 1.03;
                weights[id] = MathModel.Elastic((double)state.Price / 2.5, elasticity)
                    * MathModel.Marketing(spend) * (0.5 + state.Satisfaction / 200)
                    * Math.Max(0.5, segment) * market.Preference
                    * MathModel.Shock(seed, round, id, Uncertainty(difficulty));
                state.Metrics["MarketingSpend"] = spend;
                state.Metrics["RetentionMix"] = retention;
            }
            // Outside firms absorb demand too, keeping the market allocation finite.
            double weightSum = weights.Values.Sum() + outsideFirms * MathModel.Elastic(market.CompetitorPrice / 2.5, elasticity);
            double averagePrice = (after.Values.Sum(s => (double)s.Price) + outsideFirms * market.CompetitorPrice) / (after.Count + outsideFirms);
            double totalDemand = market.Demand * MathModel.Elastic(averagePrice / 2.5, elasticity * 0.3);
            foreach ((string id, TeamState state) in after)
            {
                TeamState old = before[id];
                double demand = totalDemand * weights[id] / weightSum;
                double production = Math.Min(2400, state.Capacity * market.CapacityFactor);
                double sold = Math.Min(Math.Floor(demand), Math.Floor(state.Inventory + production));
                state.Inventory += production - sold;
                state.MarketShare = sold / Math.Max(1, totalDemand) * 100;
                state.Satisfaction = Math.Clamp(old.Satisfaction + (2.5 - (double)state.Price) * 6
                    - Math.Max(0, demand - sold) / Math.Max(1, demand) * 15 + state.Metrics["RetentionMix"] * 2, 0, 100);
                // All production is paid for now; unsold inventory survives into the next round.
                decimal costs = MathModel.Money(production * market.UnitCost + MathModel.P(d, "FixedCost", 500)
                    + state.Inventory * 0.02 + state.Metrics["MarketingSpend"]);
                MathModel.Account(state, state.Price * MathModel.Money(sold), costs, market);
                state.Metrics["Demand"] = demand;
                state.Metrics["UnitsSold"] = sold;
                state.Metrics["Production"] = production;
                state.Metrics["Share"] = state.MarketShare;
                state.Metrics["Margin"] = (double)state.Price - market.UnitCost;
                state.Metrics["PriceStability"] = Math.Clamp(100 - Math.Abs((double)(state.Price - old.Price)) / Math.Max(0.1, (double)old.Price) * 200, 0, 100);
                explanations[id] = state.Price < old.Price
                    ? "The lower price raised relative demand but reduced unit margin. Compare extra sales with the lower margin; unsold production still costs cash."
                    : "Demand followed relative prices, marketing and customer satisfaction. A higher margin can be offset by fewer sales and unsold stock.";
            }
        }
        private static string Inventory(SimulationDefinition d, Difficulty difficulty, int seed, int round,
            string id, MarketState market, TeamState state, Dictionary<string, Decision> choices)
        {
            bool beer = d.Template == Template.BeerGame;
            double normalDemand = MathModel.P(d, "InventoryDemand", 20);
            int delay = (int)(MathModel.P(d, "OrderDelay", 1) + MathModel.P(d, "ShippingDelay", 1));
            double demand = Math.Round(normalDemand * market.Demand / 10000 * MathModel.Shock(seed, round, id, Uncertainty(difficulty)));
            while (state.Pipeline.Count < delay) state.Pipeline.Add(normalDemand);
            // Receive the front shipment before adding the current order to the back.
            double arrived = state.Pipeline[0];
            state.Pipeline.RemoveAt(0);
            double order = MathModel.D(choices, "Order", normalDemand);
            bool express = MathModel.D(choices, "Supplier", 1) == 2;
            state.Pipeline.Add(Math.Min(order, state.Capacity * (express ? 1 : market.CapacityFactor)));
            double available = state.Inventory + arrived;
            double need = demand + state.Backlog;
            double sold = Math.Min(available, need);
            state.Inventory = available - sold;
            state.Backlog = need - sold;
            double forecast = MathModel.D(choices, "Forecast", normalDemand);
            state.Satisfaction = Math.Clamp(100 - state.Backlog / Math.Max(1, demand) * 40, 0, 100);
            double carrying = beer ? 0.5 : 0.05;
            decimal costs = MathModel.Money(arrived * market.UnitCost * (express ? 1.2 : 1)
                + state.Inventory * carrying + state.Backlog * (beer ? 2 : 1));
            MathModel.Account(state, state.Price * MathModel.Money(sold), costs, market);
            state.Metrics["Demand"] = demand;
            state.Metrics["Orders"] = order;
            state.Metrics["Arrivals"] = arrived;
            state.Metrics["UnitsSold"] = sold;
            state.Metrics["InventoryEfficiency"] = Math.Clamp(100 - Math.Abs(state.Inventory - normalDemand) / Math.Max(1, normalDemand) * 25 - state.Backlog * 100 / Math.Max(1, normalDemand), 0, 100);
            state.Metrics["ForecastAccuracy"] = Math.Clamp(100 - Math.Abs(forecast - demand) / Math.Max(1, demand) * 100, 0, 100);
            return $"Received {arrived:F0}; shipped {sold:F0}; backlog {state.Backlog:F0}. Your order enters a {delay}-period pipeline. Check transit stock before ordering again.";
        }
        private static string Market(SimulationDefinition d, Difficulty difficulty, int seed, int round,
            string id, MarketState market, TeamState state, Dictionary<string, Decision> choices)
        {
            // Apply regulation to the effective price before calculating demand and margin.
            double price = MathModel.D(choices, "Price", 2.5);
            int policy = (int)MathModel.D(choices, "Policy", 1);
            if (policy == 2) price = Math.Min(price, 2);
            if (policy == 3) price = Math.Max(price, 3);
            double demand = market.Demand * 0.24 * MathModel.Elastic(price / 2.5, MathModel.P(d, "Elasticity", 1.4))
                * MathModel.Shock(seed, round, id, Uncertainty(difficulty));
            double production = Math.Min(MathModel.D(choices, "Production", 2400), state.Capacity * market.CapacityFactor);
            double sold = Math.Min(demand, state.Inventory + production);
            state.Inventory += production - sold;
            state.Price = MathModel.Money(price);
            double carbonCost = policy == 4 ? 0.3 : 0;
            double operatingCost = production * (market.UnitCost + carbonCost) + 500 + state.Inventory * 0.03;
            MathModel.Account(state, MathModel.Money(price * sold), MathModel.Money(operatingCost), market);
            double consumerSurplus = Math.Max(0, 4 - price) * sold / 2;
            double producerSurplus = Math.Max(0, price - market.UnitCost - carbonCost) * sold;
            double externalDamage = production * (policy == 4 ? 0.1 : 0.3);
            state.Metrics["Demand"] = demand;
            state.Metrics["UnitsSold"] = sold;
            state.Metrics["UnmetDemand"] = Math.Max(0, demand - sold);
            state.Metrics["ConsumerSurplus"] = consumerSurplus;
            state.Metrics["ProducerSurplus"] = producerSurplus;
            state.Metrics["ExternalDamage"] = externalDamage;
            state.Metrics["Welfare"] = Math.Max(0, consumerSurplus + producerSurplus - externalDamage);
            state.Metrics["Service"] = sold / Math.Max(1, demand) * 100;
            return $"Effective price {price:F2} EUR; demand {demand:F0}; unmet demand {state.Metrics["UnmetDemand"]:F0}. Regulation changes margin and scarcity; surplus estimates use a simplified willingness-to-pay curve.";
        }
        private static string Investment(SimulationDefinition d, MarketState market, TeamState state,
            Dictionary<string, Decision> choices)
        {
            // Current income uses previously installed capital, creating a one-round delay.
            double capital = MathModel.D(choices, "Investment") * MathModel.D(choices, "Approve", 1);
            double borrowing = MathModel.D(choices, "Borrowing");
            double yield = MathModel.P(d, "Yield", 0.3);
            double installed = state.Metrics.GetValueOrDefault("InstalledCapital");
            double npv = MathModel.Npv(capital, capital * yield, market.Interest + 0.04, 4);
            MathModel.Account(state, MathModel.Money(3000 + installed * yield), 1000, market,
                MathModel.Money(capital), MathModel.Money(borrowing));
            state.Metrics["InstalledCapital"] = installed + capital;
            state.Metrics["NPV"] = npv;
            state.Metrics["IRR"] = MathModel.Irr(capital, capital * yield, 4) * 100;
            state.Metrics["InvestmentQuality"] = capital == 0 ? 50 : Math.Clamp(50 + npv / capital * 100, 0, 100);
            return $"NPV {npv:N2} EUR; IRR {state.Metrics["IRR"]:F1}%. New capital earns next round. Today's cash also pays the investment and interest on existing debt.";
        }
        private static string CashFlow(MarketState market, TeamState state, Dictionary<string, Decision> choices)
        {
            // Last period's receivables are collected while new credit sales defer cash.
            double days = MathModel.D(choices, "CreditDays", 30);
            double sales = 12000 * (1 + days / 200) * market.Demand / 10000;
            double receivables = sales * days / 90;
            double collected = sales - receivables + state.Metrics.GetValueOrDefault("Receivables");
            decimal repayment = Math.Min(Math.Min(state.Debt, state.Cash), MathModel.Money(MathModel.D(choices, "Repayment")));
            state.Debt -= repayment;
            state.Cash -= repayment;
            MathModel.Account(state, MathModel.Money(collected), 9000, market, borrowing: MathModel.Money(MathModel.D(choices, "Borrowing")));
            state.Metrics["Receivables"] = receivables;
            state.Metrics["Sales"] = sales;
            state.Metrics["CollectionEfficiency"] = 100 - days;
            return $"Sales {sales:N2} EUR; cash collected {collected:N2} EUR; new receivables {receivables:N2} EUR. Current credit terms defer cash; previous receivables are collected now.";
        }
        private static string Portfolio(int seed, int round, string id, MarketState market, TeamState state,
            Dictionary<string, Decision> choices)
        {
            // Allocation is validated to total 100 percent before reaching this method.
            Dictionary<string, decimal> allocation = choices["Allocation"].Allocation!;
            double stocks = (double)allocation["Stocks"];
            double bonds = (double)allocation["Bonds"];
            double cash = (double)allocation["Cash"];
            double stockReturn = 0.04 + (market.Demand / 10000 - 1) * 0.2 + MathModel.Shock(seed, round, id, 0.12) - 1;
            double bondReturn = 0.02 - (market.Interest - 0.04) * 1.5;
            double portfolioReturn = (stocks * stockReturn + bonds * bondReturn + cash * 0.005) / 100;
            double gain = (double)state.Cash * portfolioReturn;
            MathModel.Account(state, MathModel.Money(Math.Max(0, gain)), MathModel.Money(Math.Max(0, -gain)), market);
            state.Metrics["Risk"] = 100 - stocks * 0.7;
            state.Metrics["PortfolioReturn"] = portfolioReturn * 100;
            return $"Portfolio return {portfolioReturn * 100:F2}%. Stock demand and seeded shocks changed returns; rate hikes reduced bond returns. Cash dampened exposure.";
        }
        private static string Project(SimulationDefinition d, MarketState market, TeamState state,
            Dictionary<string, Decision> choices)
        {
            double budget = MathModel.D(choices, "Budget", 4000);
            double employees = MathModel.D(choices, "Employees", 12);
            string priority = choices["Priority"].Ranking![0];
            double speed = priority == "Schedule" ? 1.25 : priority == "Cost" ? 0.9 : 1;
            double effectiveBudget = priority == "Cost" ? budget * 0.8 : budget;
            double oldProgress = state.Progress;
            if (state.Progress < 100)
                state.Progress = Math.Min(100, state.Progress + (employees * 1.3 + Math.Sqrt(effectiveBudget) / 8) * speed * market.CapacityFactor / MathModel.P(d, "Scope", 1));
            state.Satisfaction = Math.Clamp(65 + (priority == "Quality" ? 20 : 0) + budget / 1000, 0, 100);
            bool delivered = oldProgress < 100 && state.Progress >= 100;
            double payment = delivered ? MathModel.P(d, "Contract", 50000) * (0.8 + state.Satisfaction / 500) : 0;
            // Stop incurring delivery costs once the project was complete before this round.
            double costs = oldProgress >= 100 ? 0 : effectiveBudget + employees * 300;
            MathModel.Account(state, MathModel.Money(payment), MathModel.Money(costs), market);
            state.Metrics["Progress"] = state.Progress;
            state.Metrics["DeliveryPayment"] = payment;
            return $"Progress {state.Progress:F1}%; priority {priority}; payment {payment:N2} EUR. Schedule speeds work, quality improves contract value, and cost priority reduces spending and progress. Payment occurs once at delivery.";
        }
        private static string Probability(SimulationDefinition d, int seed, int round, string id,
            MarketState market, TeamState state, Dictionary<string, Decision> choices)
        {
            // Expected payoff and realized payoff remain separate throughout these labs.
            int mode = (int)MathModel.P(d, "Mode", 0);
            double p = MathModel.P(d, "Probability", 0.35);
            double draw = MathModel.Draw(seed, round, id + "experiment");
            double revenue = 0, costs = 0, expected = 0;
            string explanation;
            if (mode is 1 or 2 or 6)
            {
                int stock = (int)MathModel.D(choices, "Stock", 25);
                int trials = (int)MathModel.P(d, "Trials", 40);
                double lambda = MathModel.P(d, "Lambda", 20);
                double mean = MathModel.P(d, "Mean", 30);
                double deviation = MathModel.P(d, "Deviation", 6);
                int demand;
                Func<int, double> mass;
                if (mode == 1)
                {
                    demand = MathModel.BinomialSample(trials, p, draw);
                    mean = trials * p;
                    mass = k => MathModel.BinomialPmf(k, trials, p);
                }
                else if (mode == 2)
                {
                    demand = MathModel.PoissonSample(lambda, draw);
                    mean = lambda;
                    mass = k => MathModel.PoissonPmf(k, lambda);
                }
                else
                {
                    // Box-Muller generates a normal draw. Exact discrete weights use normal CDF bins.
                    double second = MathModel.Draw(seed, round, id + "normal");
                    demand = (int)Math.Clamp(Math.Round(mean + deviation * Math.Sqrt(-2 * Math.Log(draw)) * Math.Cos(2 * Math.PI * second)), 0, 100);
                    mass = k => NormalCdf((k + 0.5 - mean) / deviation) - NormalCdf((k - 0.5 - mean) / deviation);
                }
                revenue = Math.Min(stock, demand) * 20;
                costs = stock * 8 + Math.Max(0, demand - stock) * 3;
                double service = 0;
                for (int k = 0; k <= 100; k++)
                {
                    // Weight each possible demand outcome, not just today's sampled demand.
                    double probability = mass(k);
                    expected += probability * (Math.Min(stock, k) * 20 - stock * 8 - Math.Max(0, k - stock) * 3);
                    if (k <= stock) service += probability;
                }
                state.Metrics["Demand"] = demand;
                state.Metrics["Service"] = Math.Clamp(service * 100, 0, 100);
                state.Metrics["ForecastAccuracy"] = Math.Clamp(100 - Math.Abs(MathModel.D(choices, "Estimate") - mean) / Math.Max(1, mean) * 100, 0, 100);
                explanation = $"Expected demand {mean:F2}; realized demand {demand}; expected period profit {expected:F2} EUR; probability of sufficient capacity {service * 100:F1}%. Surplus stock costs 8 EUR/unit; an unmet unit costs 3 EUR.";
            }
            else if (mode is 3 or 4)
            {
                // Conditional probability uses a joint event; Bayes includes false positives.
                double conditional = mode == 3 ? MathModel.P(d, "Joint", 0.18) / MathModel.P(d, "Condition", 0.3)
                    : MathModel.Bayes(MathModel.P(d, "Prior", 0.02), MathModel.P(d, "Sensitivity", 0.95), MathModel.P(d, "FalsePositive", 0.1));
                bool reject = MathModel.D(choices, "Reject") == 1;
                costs = reject ? 80 : draw < conditional ? 1000 : 0;
                expected = -(reject ? 80 : conditional * 1000);
                state.Metrics["Posterior"] = conditional * 100;
                state.Metrics["ForecastAccuracy"] = Math.Clamp(100 - Math.Abs(MathModel.D(choices, "Estimate") - conditional * 100), 0, 100);
                explanation = $"Conditional probability {conditional * 100:F2}%. Rejecting costs 80 EUR; accepting risks a 1,000 EUR loss. Expected cost {-expected:F2} EUR. Use the base rate or joint probability, not the signal alone.";
            }
            else if (mode == 7)
            {
                // Give each sample its own key so changing sample count preserves earlier draws.
                int samples = (int)MathModel.D(choices, "Samples", 100);
                int successes = 0;
                for (int i = 0; i < samples; i++) if (MathModel.Draw(seed, round, id + "sample-" + i) < p) successes++;
                double observed = (double)successes / samples;
                double error = Math.Sqrt(p * (1 - p) / samples);
                costs = samples * 0.05;
                state.Metrics["SampleEstimate"] = observed * 100;
                state.Metrics["StandardError"] = error * 100;
                state.Metrics["Precision"] = Math.Clamp(100 - error * 500, 0, 100);
                state.Metrics["CostEfficiency"] = 100 - samples / 5.0;
                state.Metrics["ForecastAccuracy"] = Math.Clamp(100 - Math.Abs(MathModel.D(choices, "Estimate") - p * 100), 0, 100);
                expected = -costs;
                explanation = $"{successes}/{samples} successes: estimate {observed * 100:F1}%, model probability {p * 100:F1}%, standard error {error * 100:F2} percentage points. More draws reduce uncertainty but cost 0.05 EUR each.";
            }
            else if (mode == 9)
            {
                // Insurance improves tail protection even when its expected cost is higher.
                double lossProbability = 0.02;
                bool insured = MathModel.D(choices, "Insure") == 1;
                costs = insured ? 260 : draw < lossProbability ? 10000 : 0;
                expected = -(insured ? 260 : lossProbability * 10000);
                state.Metrics["Risk"] = insured ? 100 : 20;
                state.Metrics["ForecastAccuracy"] = Math.Clamp(100 - Math.Abs(MathModel.D(choices, "Estimate") - lossProbability * 100), 0, 100);
                explanation = $"A 2% risk of losing 10,000 EUR has expected cost 200 EUR. Insurance costs 260 EUR and removes that tail loss. Scoring also values protection against a large loss.";
            }
            else
            {
                double exposure = MathModel.D(choices, "Exposure", 50) / 100;
                double riskyCapital = exposure * 1000;
                double safeCapital = (1 - exposure) * 1000;
                double riskyPayoff = draw < p ? riskyCapital * 3 : 0;
                revenue = riskyPayoff + safeCapital * 1.1;
                costs = 1000;
                expected = riskyCapital * (p * 3 - 1) + safeCapital * 0.1;
                state.Metrics["Variance"] = riskyCapital * riskyCapital * 9 * p * (1 - p);
                state.Metrics["Risk"] = 100 - exposure * 80;
                explanation = $"Success chance {p * 100:F0}%; successful risky capital returns 3x, failure returns zero. Safe capital returns 1.1x. Expected period profit {expected:F2} EUR; realized profit {revenue - costs:F2} EUR.";
            }
            MathModel.Account(state, MathModel.Money(revenue), MathModel.Money(costs), market);
            state.Metrics["ExpectedProfit"] = state.Metrics.GetValueOrDefault("ExpectedProfit") + expected;
            return explanation;
        }
        private static double NormalCdf(double z)
        {
            double absolute = Math.Abs(z);
            double t = 1 / (1 + 0.2316419 * absolute);
            double density = Math.Exp(-absolute * absolute / 2) / Math.Sqrt(2 * Math.PI);
            double tail = density * t * (0.319381530 + t * (-0.356563782 + t * (1.781477937 + t * (-1.821255978 + t * 1.330274429))));
            return z >= 0 ? 1 - tail : tail;
        }
    }

    public static class Validation
    {
        public static void Definition(SimulationDefinition d)
        {
            if (string.IsNullOrWhiteSpace(d.Id) || !Enum.IsDefined(d.Template) || d.Decisions.Count == 0)
                throw new ArgumentException("A simulation needs an ID, a template and decisions.");
            if (d.Decisions.Select(x => x.Id).Distinct().Count() != d.Decisions.Count)
                throw new ArgumentException("Decision IDs must be unique.");
            if (d.Scoring.Count == 0 || d.Scoring.Any(r => !double.IsFinite(r.Weight) || r.Weight <= 0
                || !double.IsFinite(r.Min) || !double.IsFinite(r.Max) || r.Max <= r.Min))
                throw new ArgumentException("Scoring requires positive weights and increasing finite ranges.");
            if (d.Parameters.Values.Any(value => !double.IsFinite(value)))
                throw new ArgumentException("Parameters must be finite.");
            foreach (DecisionDefinition decision in d.Decisions)
            {
                if (decision.Min > decision.Max || decision.Default < decision.Min || decision.Default > decision.Max)
                    throw new ArgumentException("Decision bounds or defaults are invalid.");
                if (decision.Type is DecisionType.BudgetAllocation or DecisionType.RankOrdering or DecisionType.MultipleChoice
                    && (decision.Options is null || decision.Options.Length < 2 || decision.Options.Distinct().Count() != decision.Options.Length))
                    throw new ArgumentException("This decision requires distinct named options.");
            }
            foreach (MarketEvent ev in d.Events)
            {
                if (ev.Round is < 0 or > 10 || !double.IsFinite(ev.Probability) || ev.Probability is < 0 or > 1)
                    throw new ArgumentException("Event rounds and probabilities are out of range.");
                if (ev.Multipliers.Concat(ev.Additions ?? []).Any(p => !SessionService.EventFields.Contains(p.Key) || !double.IsFinite(p.Value)))
                    throw new ArgumentException("An event contains an unknown field or a non-finite value.");
            }
        }
        public static bool Valid(DecisionDefinition d, Decision value)
        {
            if (value.Number < d.Min || value.Number > d.Max) return false;
            if (d.Type == DecisionType.BudgetAllocation)
                return value.Allocation is not null && d.Options is not null
                    && value.Allocation.Keys.Order().SequenceEqual(d.Options.Order())
                    && value.Allocation.Values.All(v => v >= 0) && value.Allocation.Values.Sum() == value.Number;
            if (d.Type == DecisionType.RankOrdering)
                return value.Ranking is not null && d.Options is not null
                    && value.Ranking.Count == d.Options.Length && value.Ranking.Order().SequenceEqual(d.Options.Order());
            if (d.Type is DecisionType.Integer or DecisionType.MultipleChoice or DecisionType.YesNo
                && decimal.Truncate(value.Number) != value.Number) return false;
            return d.Type != DecisionType.YesNo || value.Number is 0 or 1;
        }
        public static Decision Default(DecisionDefinition d)
        {
            // Assign rounding residue to the last allocation to preserve the exact total.
            if (d.Type == DecisionType.RankOrdering) return new(0, Ranking: [.. d.Options!]);
            if (d.Type != DecisionType.BudgetAllocation) return new(d.Default);
            string[] options = d.Options!;
            decimal part = decimal.Round(d.Default / options.Length, 2, MidpointRounding.ToZero);
            Dictionary<string, decimal> allocation = options.ToDictionary(option => option, _ => part);
            allocation[options[^1]] += d.Default - allocation.Values.Sum();
            return new(d.Default, allocation);
        }
    }

    // Session orchestration owns event timing and immutable round snapshots.
    public sealed class SessionService
    {
        public static readonly string[] EventFields = ["Demand", "UnitCost", "CompetitorPrice", "Capacity", "Interest", "Preference"];
        private readonly SimulationEngine engine = new();
        public Session Create(SimulationDefinition definition, string course, string lesson, List<Team> teams,
            int rounds = 5, int seed = 42, Difficulty difficulty = Difficulty.Normal, bool lecturer = false, int duration = 20)
        {
            Validation.Definition(definition);
            if (rounds is < 3 or > 10 || teams.Count is < 1 or > 30 || duration is not (15 or 20))
                throw new ArgumentException("Use 3-10 rounds, 1-30 teams and a 15/20 minute preset.");
            if (teams.Select(t => t.Id).Distinct().Count() != teams.Count)
                throw new ArgumentException("Team IDs must be unique.");
            Session session = new()
            {
                Definition = definition,
                Course = course,
                Lesson = lesson,
                Teams = teams,
                Rounds = rounds,
                Seed = seed,
                Difficulty = difficulty,
                Lecturer = lecturer,
                DurationMinutes = duration
            };
            foreach (Team team in teams)
            {
                bool beer = definition.Template == Template.BeerGame;
                double demand = MathModel.P(definition, "InventoryDemand", 1000);
                session.States[team.Id] = new()
                {
                    Inventory = beer ? 40 : definition.Template == Template.Inventory ? demand * 2 : 2000,
                    Capacity = beer ? 150 : 4000
                };
            }
            return session;
        }
        public MarketState Prepare(Session session)
        {
            MarketState market = session.Market.Clone();
            market.News = [];
            int round = session.History.Count + 1;
            foreach (MarketEvent ev in session.Definition.Events.Where(e => e.Round == 0 || e.Round == round))
            {
                if (MathModel.Draw(session.Seed, round, "event-" + ev.Name) >= ev.Probability) continue;
                foreach ((string key, double value) in ev.Multipliers) Apply(market, key, value, true);
                foreach ((string key, double value) in ev.Additions ?? []) Apply(market, key, value, false);
                market.News.Add(ev.Name + ": " + ev.Description);
            }
            if (market.News.Count == 0) market.News.Add("Normal market conditions. No new event this round.");
            return market;
        }
        private static void Apply(MarketState market, string key, double value, bool multiply)
        {
            double current = key switch
            {
                "Demand" => market.Demand,
                "UnitCost" => market.UnitCost,
                "CompetitorPrice" => market.CompetitorPrice,
                "Capacity" => market.CapacityFactor,
                "Interest" => market.Interest,
                "Preference" => market.Preference,
                _ => throw new ArgumentException("Unknown event field: " + key)
            };
            double next = multiply ? current * value : current + value;
            switch (key)
            {
                case "Demand": market.Demand = Math.Clamp(next, 100, 1000000); break;
                case "UnitCost": market.UnitCost = Math.Clamp(next, 0.1, 100); break;
                case "CompetitorPrice": market.CompetitorPrice = Math.Clamp(next, 0.1, 100); break;
                case "Capacity": market.CapacityFactor = Math.Clamp(next, 0.1, 3); break;
                case "Interest": market.Interest = Math.Clamp(next, 0, 0.3); break;
                case "Preference": market.Preference = Math.Clamp(next, 0.2, 3); break;
            }
        }
        public RoundSnapshot Advance(Session session, Dictionary<string, Dictionary<string, Decision>> choices)
        {
            if (session.Completed) throw new InvalidOperationException("This session has completed.");
            foreach (Team team in session.Teams)
            {
                if (!choices.TryGetValue(team.Id, out Dictionary<string, Decision>? submitted))
                    throw new ArgumentException("Every team needs decisions.");
                foreach (DecisionDefinition d in session.Definition.Decisions)
                    if (!submitted.TryGetValue(d.Id, out Decision? choice) || !Validation.Valid(d, choice))
                        throw new ArgumentException("Invalid decision: " + d.Name);
            }
            RoundSnapshot snapshot = new()
            {
                Number = session.History.Count + 1,
                Market = Prepare(session),
                Before = session.States.ToDictionary(p => p.Key, p => p.Value.Clone()),
                Decisions = CloneChoices(choices)
            };
            SimulationResult result = engine.Run(session.Definition, session.Difficulty, session.Seed,
                snapshot.Number, snapshot.Market, snapshot.Before, snapshot.Decisions);
            snapshot.After = result.States;
            snapshot.Explanations = result.Explanations;
            foreach (Team team in session.Teams)
                snapshot.Scores[team.Id] = MathModel.Score(session.Definition,
                    session.History.Select(r => r.After[team.Id]).Append(snapshot.After[team.Id]).ToList());
            session.History.Add(snapshot);
            // Current states and history use separate objects to prevent accidental history edits.
            session.States = result.States.ToDictionary(p => p.Key, p => p.Value.Clone());
            session.Market = snapshot.Market.Clone();
            session.Completed = session.History.Count == session.Rounds;
            return snapshot;
        }
        public SimulationResult Counterfactual(Session session, int round, string team, string decision, Decision alternative)
        {
            // Rival choices, random draws and market news remain fixed for this comparison.
            RoundSnapshot actual = session.History.Single(r => r.Number == round);
            DecisionDefinition d = session.Definition.Decisions.Single(x => x.Id == decision);
            if (!Validation.Valid(d, alternative)) throw new ArgumentException("Alternative choice is invalid.");
            Dictionary<string, Dictionary<string, Decision>> choices = CloneChoices(actual.Decisions);
            choices[team][decision] = CloneChoice(alternative);
            return engine.Run(session.Definition, session.Difficulty, session.Seed, round, actual.Market.Clone(),
                actual.Before.ToDictionary(p => p.Key, p => p.Value.Clone()), choices);
        }
        public static Decision CloneChoice(Decision choice) => choice with
        {
            Allocation = choice.Allocation is null ? null : new(choice.Allocation),
            Ranking = choice.Ranking is null ? null : [.. choice.Ranking]
        };
        private static Dictionary<string, Dictionary<string, Decision>> CloneChoices(Dictionary<string, Dictionary<string, Decision>> choices)
            => choices.ToDictionary(t => t.Key, t => t.Value.ToDictionary(d => d.Key, d => CloneChoice(d.Value)));
        public static List<BoardEntry> Board(Session session) => session.Teams.Select(t => new BoardEntry(0, t.Id, t.Name,
            MathModel.Score(session.Definition, session.History.Select(r => r.After[t.Id]).ToList()), session.States[t.Id].TotalProfit))
            .OrderByDescending(entry => entry.Score).ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .Select((entry, index) => entry with { Rank = index + 1 }).ToList();
    }

    // No API calls: AI uses economic rules and a seeded search on observed decisions.
    public sealed class AiStrategy
    {
        public Dictionary<string, Decision> Choose(Session session, string teamId, MarketState market)
        {
            TeamState state = session.States[teamId];
            SimulationDefinition d = session.Definition;
            Dictionary<string, Decision> choices = d.Decisions.ToDictionary(x => x.Id, Validation.Default);
            double uncertainty = session.Difficulty == Difficulty.Easy ? 0.25 : 0.04;
            double factor = MathModel.Shock(session.Seed, session.History.Count + 1, teamId + "strategy", uncertainty);
            void Set(string key, double proposed)
            {
                DecisionDefinition? field = d.Decisions.Find(x => x.Id == key);
                if (field is null) return;
                decimal value = Math.Clamp(MathModel.Money(proposed), field.Min, field.Max);
                if (field.Type == DecisionType.Integer) value = decimal.Round(value, 0);
                choices[key] = new(value);
            }
            Set("Price", Math.Max(market.UnitCost * 1.8, market.CompetitorPrice) * factor);
            double demand = state.Metrics.GetValueOrDefault("Demand", MathModel.P(d, "InventoryDemand", 20));
            Set("Order", (demand + state.Backlog + Math.Max(0, demand * 2 - state.Inventory - state.Pipeline.Sum()) * 0.5) * factor);
            Set("Forecast", demand);
            Set("Production", Math.Max(0, market.Demand * 0.24 - state.Inventory * 0.5));
            Set("Investment", state.Cash > 25000 ? 8000 : 0);
            Set("Borrowing", state.Cash < 10000 ? 12000 : 0);
            Set("CreditDays", state.Cash < 20000 ? 10 : 30);
            Set("Employees", session.Difficulty == Difficulty.Hard ? 15 : 12);
            int mode = (int)MathModel.P(d, "Mode", 0);
            if (d.Template == Template.Probability)
            {
                double p = MathModel.P(d, "Probability", 0.35);
                double mean = mode == 1 ? MathModel.P(d, "Trials", 40) * p : mode == 2 ? MathModel.P(d, "Lambda", 20) : MathModel.P(d, "Mean", 30);
                Set("Stock", mean + (session.Difficulty == Difficulty.Hard ? 8 : 5));
                Set("Estimate", mode is 1 or 2 or 6 ? mean : mode == 4
                    ? MathModel.Bayes(MathModel.P(d, "Prior", 0.02), 0.95, 0.1) * 100 : mode == 3 ? 60 : mode == 9 ? 2 : p * 100);
                Set("Reject", mode is 3 or 4 ? 1 : 0);
                Set("Exposure", session.Difficulty == Difficulty.Easy ? 70 : 20);
            }
            if (session.Difficulty == Difficulty.Easy && choices.ContainsKey("Price"))
                Set("Price", (double)state.Price * 0.7 + (double)choices["Price"].Number * 0.3);
            if (session.Difficulty == Difficulty.Hard && d.Template is Template.Pricing or Template.Marketing)
            {
                SimulationEngine engine = new();
                Dictionary<string, Dictionary<string, Decision>> previous = session.Teams.ToDictionary(t => t.Id,
                    t => d.Decisions.ToDictionary(field => field.Id, field => session.History.LastOrDefault()?.Decisions[t.Id].GetValueOrDefault(field.Id) ?? Validation.Default(field)));
                double best = double.NegativeInfinity;
                decimal bestPrice = choices["Price"].Number;
                for (decimal candidate = 1.8m; candidate <= 4; candidate += 0.2m)
                {
                    previous[teamId] = new(choices) { ["Price"] = new(candidate) };
                    SimulationResult result = engine.Run(d, session.Difficulty, session.Seed, session.History.Count + 1, market, session.States, previous);
                    double score = MathModel.Score(d, session.History.Select(r => r.After[teamId]).Append(result.States[teamId]).ToList());
                    if (score > best) { best = score; bestPrice = candidate; }
                }
                choices["Price"] = new(bestPrice);
            }
            return choices;
        }
    }

    // Atomic local JSON writes; malformed and old-format files do not crash the menu.
    public sealed class LocalStore
    {
        public string Root { get; }
        public List<string> Notices { get; } = [];
        private readonly JsonSerializerOptions options = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() } };
        public LocalStore(string root)
        {
            Root = Path.GetFullPath(root);
            foreach (string folder in new[] { "", "sessions", "results", "players", "exports" })
                Directory.CreateDirectory(Path.Combine(Root, folder));
        }
        private T? Read<T>(string path) where T : class
        {
            if (!File.Exists(path)) return null;
            try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path), options); }
            catch (Exception ex) when (ex is JsonException or IOException)
            { Notices.Add("Could not read " + Path.GetFileName(path) + "; using defaults or skipping this file."); return null; }
        }
        private void Write<T>(string path, T value)
        {
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, options), new UTF8Encoding(false));
            File.Move(temporary, path, true);
        }
        public List<Course> LoadCourses()
        {
            // Keep a backup when migrating the previous twenty-course catalog.
            string path = Path.Combine(Root, "courses.json");
            List<Course>? courses = Read<List<Course>>(path);
            if (courses is not null)
            {
                try
                {
                    if (courses.Count != 6 || courses.Any(c => c.Lessons.Count != 10)
                        || !courses.Select(c => c.Name).Order().SequenceEqual(Curriculum.Generate().Select(c => c.Name).Order()))
                        throw new ArgumentException("The catalog belongs to a different version.");
                    foreach (Lesson lesson in courses.SelectMany(c => c.Lessons)) Validation.Definition(lesson.Simulation);
                    return courses;
                }
                catch (Exception ex) when (ex is ArgumentException or NullReferenceException)
                { Notices.Add("Replaced an older or invalid catalog with the six-course version. A backup was kept."); }
            }
            if (File.Exists(path)) File.Copy(path, path + ".backup-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"), false);
            courses = Curriculum.Generate();
            Write(path, courses);
            return courses;
        }
        public Settings LoadSettings()
        {
            Settings settings = Read<Settings>(Path.Combine(Root, "settings.json")) ?? new();
            if (settings.Rounds is < 3 or > 10 || !Enum.IsDefined(settings.Difficulty) || settings.DurationMinutes is not (15 or 20)) settings = new();
            SaveSettings(settings);
            return settings;
        }
        public void SaveSettings(Settings settings) => Write(Path.Combine(Root, "settings.json"), settings);
        public void Save(Session session)
        {
            if (session.Id.Any(c => !char.IsAsciiLetterOrDigit(c))) throw new ArgumentException("Invalid session identifier.");
            Write(Path.Combine(Root, "sessions", session.Id + ".json"), session);
            if (session.Completed) Write(Path.Combine(Root, "results", session.Id + ".json"), session);
            Write(Path.Combine(Root, "players", session.Id + ".json"), session.Teams);
        }
        public List<Session> Sessions()
        {
            List<Session> sessions = [];
            foreach (string file in Directory.GetFiles(Path.Combine(Root, "sessions"), "*.json"))
            {
                Session? session = Read<Session>(file);
                if (session is null) continue;
                try
                {
                    Validation.Definition(session.Definition);
                    if (session.Teams.Count == 0 || session.Teams.Any(t => !session.States.ContainsKey(t.Id))
                        || session.History.Any(r => session.Teams.Any(t => !r.Scores.ContainsKey(t.Id) || !r.Before.ContainsKey(t.Id) || !r.After.ContainsKey(t.Id) || !r.Decisions.ContainsKey(t.Id))))
                        throw new ArgumentException("Incomplete saved state.");
                    sessions.Add(session);
                }
                catch (Exception ex) when (ex is ArgumentException or NullReferenceException)
                { Notices.Add("Skipped an incompatible saved session: " + Path.GetFileName(file)); }
            }
            return sessions.OrderByDescending(s => s.StartedAt).ToList();
        }
        public string Export(Session session)
        {
            // Quote every CSV field; preserve commas and quotes in user-entered team names.
            string directory = Path.Combine(Root, "exports", session.Id);
            Directory.CreateDirectory(directory);
            string I(object value) => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";
            string Escape(string value)
            {
                if (value.StartsWith('=') || value.StartsWith('+') || value.StartsWith('@')) value = "'" + value;
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            void Csv(string filename, IEnumerable<string[]> rows)
            {
                File.WriteAllLines(Path.Combine(directory, filename), rows.Select(row => string.Join(',', row.Select(Escape))), new UTF8Encoding(false));
            }
            Csv("session-summary.csv", [ ["Session","Course","Lesson","Seed","Rounds","Completed"],
                [session.Id,session.Course,session.Lesson,I(session.Seed),I(session.History.Count),I(session.Completed)] ]);
            Csv("team-results.csv", new[] { new[] { "Rank", "Team", "Score", "Profit", "Cash", "Debt" } }
                .Concat(SessionService.Board(session).Select(e => new[] { I(e.Rank), e.Name, I(e.Score), I(e.Profit), I(session.States[e.TeamId].Cash), I(session.States[e.TeamId].Debt) })));
            List<string[]> history = [["Round", "Team", "Decision", "Previous", "New", "Profit", "Cash", "Inventory", "Backlog", "Score", "ScoreChange", "Explanation"]];
            foreach (RoundSnapshot round in session.History)
                foreach (Team team in session.Teams)
                    foreach ((string key, Decision choice) in round.Decisions[team.Id])
                    {
                        Decision previous = round.Number == 1 ? Validation.Default(session.Definition.Decisions.Single(d => d.Id == key)) : session.History[round.Number - 2].Decisions[team.Id][key];
                        TeamState result = round.After[team.Id];
                        double oldScore = round.Number == 1 ? 0 : session.History[round.Number - 2].Scores[team.Id];
                        history.Add([I(round.Number), team.Name, key, Format(previous), Format(choice), I(result.Profit), I(result.Cash), I(result.Inventory), I(result.Backlog), I(round.Scores[team.Id]), I(round.Scores[team.Id] - oldScore), round.Explanations[team.Id]]);
                    }
            Csv("round-history.csv", history);
            return directory;
        }
        public static string Format(Decision choice) => choice.Allocation is not null
            ? string.Join(";", choice.Allocation.Select(p => p.Key + "=" + p.Value.ToString(CultureInfo.InvariantCulture)))
            : choice.Ranking is not null ? string.Join(">", choice.Ranking) : choice.Number.ToString("0.##", CultureInfo.InvariantCulture);
    }

    // All prompts retry invalid input; EOF exits instead of spinning forever.
    public static class Terminal
    {
        public static bool Interactive { get; set; }
        private static int Width => Interactive ? Math.Clamp(Console.WindowWidth - 1, 20, 76) : 76;
        public static void Clear()
        {
            if (Interactive) Console.Clear();
        }
        public static void Pause()
        {
            if (!Interactive) return;
            Console.Write("[Any key to continue]");
            Console.ReadKey(true);
            Clear();
        }
        public static void Color(string text, ConsoleColor color)
        {
            if (!Interactive) { Write(text); return; }
            ConsoleColor previous = Console.ForegroundColor;
            try { Console.ForegroundColor = color; Write(text); }
            finally { Console.ForegroundColor = previous; }
        }
        public static string Ascii(string text) => new(text.Select(c => c is >= ' ' and <= '~' ? c : '?').ToArray());
        public static void Write(string text = "")
        {
            string remaining = Ascii(text);
            do
            {
                if (Interactive && Console.CursorTop >= Math.Max(1, Console.WindowHeight - 3)) Pause();
                int length = Interactive ? Math.Min(remaining.Length, Width) : remaining.Length;
                Console.WriteLine(remaining[..length]);
                remaining = remaining[length..];
            } while (remaining.Length > 0);
        }
        public static void Panel(string title, IEnumerable<string> lines)
        {
            string border = "+" + new string('-', Width - 2) + "+";
            Write(); Write(border);
            if (Interactive) Console.ForegroundColor = ConsoleColor.Cyan;
            PrintWrapped(title.ToUpperInvariant());
            if (Interactive) Console.ResetColor();
            Write(border);
            foreach (string line in lines) PrintWrapped(line);
            Write(border);
        }
        private static void PrintWrapped(string text)
        {
            text = Ascii(text);
            int usable = Width - 4;
            while (text.Length > usable)
            {
                int space = text.LastIndexOf(' ', usable);
                int cut = space > 0 ? space : usable;
                Write("| " + text[..cut].PadRight(usable) + " |");
                text = text[(space > 0 ? cut + 1 : cut)..];
            }
            Write("| " + text.PadRight(usable) + " |");
        }
        public static string Read(string prompt, string fallback = "")
        {
            if (Interactive)
            {
                if (prompt.StartsWith("Press Enter")) { Pause(); return fallback; }
                Write(prompt + (fallback.Length > 0 ? " [" + fallback + "]" : ""));
                Write("Enter = default | T = type a custom value");
                while (true)
                {
                    ConsoleKey key = Console.ReadKey(true).Key;
                    if (key == ConsoleKey.Enter) return fallback;
                    if (key == ConsoleKey.T) break;
                }
            }
            Console.Write(Ascii(prompt + (fallback.Length > 0 ? " [" + fallback + "]" : "") + ": "));
            string? line = Console.ReadLine();
            if (line is null) throw new EndOfStreamException();
            return string.IsNullOrWhiteSpace(line) ? fallback : line.Trim();
        }
        public static int Integer(string label, int min, int max, int fallback)
        {
            if (Interactive) return (int)Adjust(label, min, max, fallback, 1, min >= 0 && max <= 9, true);
            while (true)
            {
                if (int.TryParse(Read(label, fallback.ToString(CultureInfo.InvariantCulture)), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
                    && value >= min && value <= max) return value;
                Write($"Enter a whole number from {min} to {max}.");
            }
        }
        public static decimal Number(string label, decimal min, decimal max, decimal fallback)
        {
            if (Interactive) return Adjust(label, min, max, fallback, max - min <= 20 ? 0.01m : max - min <= 100 ? 1 : 100);
            while (true)
            {
                string text = Read(label, fallback.ToString(CultureInfo.InvariantCulture)).TrimEnd('%');
                if (decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out decimal value)
                    && value >= min && value <= max) return value;
                Write($"Enter a number from {min} to {max}; use a decimal point.");
            }
        }
        public static bool YesNo(string label, bool fallback = true)
        {
            if (Interactive)
            {
                Write(label + " [Y = yes / N = no / Enter = " + (fallback ? "yes" : "no") + "]");
                while (true)
                {
                    ConsoleKey key = Console.ReadKey(true).Key;
                    if (key is ConsoleKey.Y or ConsoleKey.D1) return true;
                    if (key is ConsoleKey.N or ConsoleKey.D0) return false;
                    if (key == ConsoleKey.Enter) return fallback;
                }
            }
            while (true)
            {
                string answer = Read(label + " (yes/no)", fallback ? "yes" : "no").ToLowerInvariant();
                if (answer is "yes" or "y" or "1") return true;
                if (answer is "no" or "n" or "0") return false;
                Write("Enter yes or no.");
            }
        }
        public static string Name(string label, string fallback)
        {
            while (true)
            {
                string text = Read(label, fallback);
                if (text.Length is >= 1 and <= 32 && text.All(c => c is >= ' ' and <= '~')) return text;
                Write("Use 1-32 standard ASCII characters.");
            }
        }
        private static decimal Adjust(string label, decimal min, decimal max, decimal fallback, decimal step, bool shortcuts = false, bool whole = false)
        {
            Write(label);
            Write(shortcuts ? "Press an option number, or arrows + Enter." : "Arrows: adjust | PgUp/PgDn: x10 | Home/End: bounds");
            if (!shortcuts) Write("Enter: confirm | T: type an exact value");
            Write();
            int row = Console.CursorTop - 1;
            decimal value = Math.Clamp(fallback, min, max);
            while (true)
            {
                Console.SetCursorPosition(0, row);
                string display = $"> {value:0.##} [{min:0.##}..{max:0.##}]";
                Console.Write(display[..Math.Min(display.Length, Width)].PadRight(Width));
                ConsoleKeyInfo key = Console.ReadKey(true);
                if (key.Key == ConsoleKey.Enter) { Console.SetCursorPosition(0, row + 1); return value; }
                if (shortcuts && key.KeyChar >= '0' && key.KeyChar <= '9' && key.KeyChar - '0' >= min && key.KeyChar - '0' <= max)
                { Console.SetCursorPosition(0, row + 1); return key.KeyChar - '0'; }
                if (!shortcuts && key.Key == ConsoleKey.T)
                {
                    Console.SetCursorPosition(0, row + 1);
                    Console.Write("Value: ");
                    string? text = Console.ReadLine();
                    if (text is null) throw new EndOfStreamException();
                    if (decimal.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out decimal typed)
                        && typed >= min && typed <= max && (!whole || typed == decimal.Truncate(typed))) return typed;
                    Color("Invalid value. Use the displayed bounds.", ConsoleColor.Yellow);
                    Write(); row = Console.CursorTop - 1;
                }
                value = key.Key switch
                {
                    ConsoleKey.LeftArrow or ConsoleKey.DownArrow => Math.Max(min, value - step),
                    ConsoleKey.RightArrow or ConsoleKey.UpArrow => Math.Min(max, value + step),
                    ConsoleKey.PageDown => Math.Max(min, value - step * 10),
                    ConsoleKey.PageUp => Math.Min(max, value + step * 10),
                    ConsoleKey.Home => min, ConsoleKey.End => max, _ => value
                };
            }
        }
        public static Decision AskDecision(DecisionDefinition d, Decision? previous = null)
        {
            // Previous choices provide useful defaults; the application validates them again.
            Decision fallback = previous ?? Validation.Default(d);
            Write($"{d.Name} ({d.Unit}); range {d.Min:0.##} to {d.Max:0.##}");
            if (d.Type == DecisionType.YesNo) return new(YesNo(d.Name, fallback.Number != 0) ? 1 : 0);
            if (d.Type == DecisionType.Integer) return new(Integer(d.Name, (int)d.Min, (int)d.Max, (int)fallback.Number));
            if (d.Type == DecisionType.MultipleChoice)
            {
                for (int i = 0; i < d.Options!.Length; i++) Write($"{i + 1}. {d.Options[i]}");
                return new(Integer(d.Name, (int)d.Min, (int)d.Max, (int)fallback.Number));
            }
            if (d.Type == DecisionType.RankOrdering)
            {
                if (Interactive)
                {
                    List<string> remaining = [.. d.Options!];
                    List<string> order = [];
                    while (remaining.Count > 0)
                    {
                        Panel("Choose priority " + (order.Count + 1), remaining.Select((option, i) => $"{i + 1}. {option}"));
                        int selected = Integer("Priority", 1, remaining.Count, 1) - 1;
                        order.Add(remaining[selected]); remaining.RemoveAt(selected);
                    }
                    return new(0, Ranking: order);
                }
                for (int i = 0; i < d.Options!.Length; i++) Write($"{i + 1}. {d.Options[i]}");
                while (true)
                {
                    string[] fields = Read("Priority order", string.Join(',', Enumerable.Range(1, d.Options.Length))).Split(',', StringSplitOptions.TrimEntries);
                    if (fields.Length == d.Options.Length
                        && fields.All(x => int.TryParse(x, out int n) && n >= 1 && n <= d.Options.Length))
                    {
                        List<string> ranking = fields.Select(x => d.Options[int.Parse(x, CultureInfo.InvariantCulture) - 1]).ToList();
                        Decision result = new(0, Ranking: ranking);
                        if (Validation.Valid(d, result)) return result;
                    }
                    Write("Enter each option number exactly once, separated by commas.");
                }
            }
            if (d.Type == DecisionType.BudgetAllocation)
            {
                decimal total = d.Min == d.Max ? d.Default : Number("Total budget", d.Min, d.Max, fallback.Number);
                decimal remaining = total;
                Dictionary<string, decimal> allocation = [];
                for (int i = 0; i < d.Options!.Length - 1; i++)
                {
                    string option = d.Options[i];
                    decimal amount = Number(option, 0, remaining, Math.Min(remaining, fallback.Allocation![option]));
                    allocation[option] = amount;
                    remaining -= amount;
                }
                allocation[d.Options[^1]] = remaining;
                Write($"{d.Options[^1]} receives the remaining {remaining:0.##}.");
                return new(total, allocation);
            }
            return new(Number(d.Name, d.Min, d.Max, fallback.Number));
        }
        public static void Bars(string title, IEnumerable<(string Label, double Value)> input)
        {
            List<(string Label, double Value)> bars = input.ToList();
            double largest = Math.Max(1, bars.Select(b => Math.Abs(b.Value)).DefaultIfEmpty(1).Max());
            Write(); Write(title.ToUpperInvariant());
            foreach ((string label, double value) in bars)
            {
                string name = label.Length > 24 ? label[..24] : label;
                int length = (int)Math.Round(Math.Abs(value) / largest * 28);
                string bar = new(value < 0 ? '-' : '#', length);
                Color($"{name,-24} {(value < 0 ? "!" : " ")}{bar,-28} {value,10:N2}", value < 0 ? ConsoleColor.Red : ConsoleColor.Green);
            }
        }
    }

    // The console controller keeps rendering separate from the simulation formulas.
    public sealed class ConsoleApp
    {
        private readonly LocalStore store;
        private readonly List<Course> courses;
        private Settings settings;
        private readonly SessionService service = new();
        private readonly AiStrategy ai = new();
        public ConsoleApp(LocalStore store)
        {
            this.store = store;
            courses = store.LoadCourses();
            settings = store.LoadSettings();
        }
        public void Run()
        {
            Terminal.Interactive = !Console.IsInputRedirected && !Console.IsOutputRedirected;
            Terminal.Clear();
            Notices();
            while (true)
            {
                Terminal.Panel("ECONFORGE", [
                    "1. Lecturer Mode","2. Student / Team Mode","3. Quick Demo - FreshFizz Pricing",
                    "4. Previous Results and Replay","5. Settings","6. License and Subscription",
                    "7. Exit","8. Course Library and Search","9. Product Analytics",
                    $"{courses.Count} courses | {courses.Sum(c => c.Lessons.Count)} simulations | C# single-source edition"
                ]);
                int selected = Terminal.Integer("Select", 1, 9, 3);
                if (selected == 7) { Terminal.Write("Thank you for using EconForge."); return; }
                Terminal.Clear();
                try
                {
                    switch (selected)
                    {
                        case 1: Classroom(true); break;
                        case 2: Classroom(false); break;
                        case 3: QuickDemo(); break;
                        case 4: Previous(); break;
                        case 5: ConfigureSettings(); break;
                        case 6: Plans(); break;
                        case 8: Library(); break;
                        case 9: ProductAnalytics(); break;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
                { Terminal.Panel("Action could not complete", [ex.Message, "Saved rounds remain in the data directory."]); }
                Notices();
                Terminal.Pause();
            }
        }
        public void ListCourses()
        {
            Terminal.Panel("Course catalog", courses.Select(c => $"{c.Name}: {c.Lessons.Count} simulations"));
            foreach (Course course in courses)
                Terminal.Panel(course.Name, course.Lessons.Select((lesson, i) => $"{i + 1}. {lesson.Name}"));
        }
        private void Notices()
        {
            foreach (string notice in store.Notices) Terminal.Write("Storage notice: " + notice);
            store.Notices.Clear();
        }
        private (Course Course, Lesson Lesson)? Select()
        {
            Terminal.Panel("Select course", courses.Select((c, i) => $"{i + 1}. {c.Name} ({c.Lessons.Count} lessons)")
                .Append($"{courses.Count + 1}. Search simulations | 0. Return"));
            int selected = Terminal.Integer("Course", 0, courses.Count + 1, 0);
            if (selected == 0) return null;
            if (selected == courses.Count + 1)
            {
                string term = Terminal.Read("Search keyword", "inventory");
                List<(Course Course, Lesson Lesson)> found = courses.SelectMany(c => c.Lessons.Select(l => (Course: c, Lesson: l)))
                    .Where(x => (x.Course.Name + " " + x.Lesson.Name + " " + x.Lesson.Simulation.Theory).Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
                Terminal.Panel("Search results", found.Select((x, i) => $"{i + 1}. {x.Course.Name} / {x.Lesson.Name}"));
                if (found.Count == 0) { Terminal.Write("No matching simulations."); return null; }
                int item = Terminal.Integer("Simulation (0 to return)", 0, found.Count, 0);
                return item == 0 ? null : found[item - 1];
            }
            Course course = courses[selected - 1];
            Terminal.Panel(course.Name, course.Lessons.Select((l, i) => $"{i + 1}. {l.Name}").Append("0. Return"));
            int lesson = Terminal.Integer("Lesson", 0, course.Lessons.Count, 0);
            return lesson == 0 ? null : (course, course.Lessons[lesson - 1]);
        }
        private void Library()
        {
            (Course Course, Lesson Lesson)? selected = Select();
            if (selected is null) return;
            Briefing(selected.Value.Lesson.Simulation);
        }
        private static void Briefing(SimulationDefinition definition)
        {
            Terminal.Panel(definition.Name, [ definition.Briefing,
                "OBJECTIVE: "+definition.Objective,"THEORY: "+definition.Theory,
                "SCORING: "+string.Join(" | ",definition.Scoring.Select(r => $"{r.Metric} {r.Weight*100:F0}%")) ]);
        }
        private Difficulty ChooseDifficulty()
        {
            Terminal.Write("1. Easy | 2. Normal | 3. Hard");
            return (Difficulty)(Terminal.Integer("Difficulty", 1, 3, (int)settings.Difficulty + 1) - 1);
        }
        private void Classroom(bool lecturer)
        {
            (Course Course, Lesson Lesson)? selected = Select();
            if (selected is null) return;
            int humans = lecturer ? Terminal.Integer("Human teams", 0, 20, 1) : 1;
            int bots = Terminal.Integer("AI competitors", humans == 0 ? 1 : 0, 30 - humans, 3);
            Difficulty difficulty = ChooseDifficulty();
            int rounds = Terminal.Integer("Rounds", 3, 10, settings.Rounds);
            int duration = Terminal.Integer("Duration: 1 = 15 minutes, 2 = 20 minutes", 1, 2, settings.DurationMinutes == 15 ? 1 : 2) == 1 ? 15 : 20;
            int seed = Terminal.Integer("Random seed", int.MinValue, int.MaxValue, settings.Seed);
            List<Team> teams = [];
            for (int i = 0; i < humans; i++)
            {
                string name = Terminal.Name("Team name", $"Team {i + 1}");
                int members = Terminal.Integer("Members (1 for individual)", 1, 20, 1);
                List<string> players = [];
                for (int m = 0; m < members; m++) players.Add(Terminal.Name($"Member {m + 1}", $"Student {m + 1}"));
                teams.Add(new($"team-{i + 1}", name, false, players) { Role = ChooseRole() });
            }
            AddBots(teams, bots);
            Play(service.Create(selected.Value.Lesson.Simulation, selected.Value.Course.Name, selected.Value.Lesson.Name,
                teams, rounds, seed, difficulty, lecturer, duration));
        }
        private static void AddBots(List<Team> teams, int count)
        {
            string[] names = ["AI Alpha", "AI Delta", "AI Sigma"];
            for (int i = 0; i < count; i++) teams.Add(new($"ai-{i + 1}", i < 3 ? names[i] : $"AI Team {i + 1}", true, []));
        }
        private static TeamRole ChooseRole()
        {
            Terminal.Panel("Choose your team role", [
                "1. Strategy - balance objectives and trade-offs",
                "2. Finance - cash, costs and risk",
                "3. Marketing - demand, customers and pricing",
                "4. Operations - stock, capacity and delivery",
                "Roles provide a decision focus; all decisions remain available."
            ]);
            TeamRole role = (TeamRole)(Terminal.Integer("Role", 1, 4, 1) - 1);
            Terminal.Clear();
            return role;
        }
        private void QuickDemo()
        {
            List<Team> teams = [new("freshfizz", "FreshFizz", false, ["Presenter"])];
            teams[0].Role = ChooseRole();
            AddBots(teams, 3);
            Terminal.Panel("FreshFizz - five-round Quick Demo", [
                "Choose one selling price each round. Press Enter to keep the displayed value.",
                "R1 normal market; R2 competitor cut; R3 viral demand; R4 cost shock; R5 equilibrium.",
                "Initial price 2.50 EUR; cost 1.20 EUR; share 18%; demand 10,000; three rivals."
            ]);
            Terminal.Pause();
            Play(service.Create(Curriculum.FreshFizz(), "Marketing", "Pricing", teams, 5, settings.Seed, Difficulty.Normal, false, 15));
        }
        private void Play(Session session)
        {
            Briefing(session.Definition);
            Terminal.Pause();
            store.Save(session);
            Dictionary<string, int> careerXp = session.Teams.Where(t => !t.IsAi)
                .ToDictionary(t => t.Id, t => Career.Xp(store.Sessions(), t.Name));
            while (!session.Completed)
            {
                MarketState market = service.Prepare(session);
                int round = session.History.Count + 1;
                Terminal.Panel($"Round {round}/{session.Rounds}", [
                    $"Course: {session.Course} | Lesson: {session.Lesson}",
                    $"Seed: {session.Seed} | Difficulty: {session.Difficulty} | Pacing: {(double)session.DurationMinutes/session.Rounds:F1} minutes per round",
                    ..market.News
                ]);
                Dictionary<string, Dictionary<string, Decision>> allChoices = [];
                foreach (Team team in session.Teams)
                {
                    if (team.IsAi) { allChoices[team.Id] = ai.Choose(session, team.Id, market); continue; }
                    TeamState information = session.Difficulty == Difficulty.Hard && session.History.Count > 0
                        ? session.History[^1].Before[team.Id] : session.States[team.Id];
                    DecisionInformation(session.Definition, team.Name, information, market);
                    Terminal.Color(Career.Status(careerXp[team.Id]), ConsoleColor.Magenta);
                    Terminal.Color(Career.Guidance(team.Role), ConsoleColor.Yellow);
                    if (session.Difficulty == Difficulty.Hard) Terminal.Write("Hard: numeric operating reports are delayed one period; news is current.");
                    if (session.Difficulty == Difficulty.Easy) Terminal.Write("GUIDANCE: " + session.Definition.Strategy);
                    Dictionary<string, Decision> submitted = [];
                    foreach (DecisionDefinition d in session.Definition.Decisions)
                    {
                        Decision? previous = session.History.LastOrDefault()?.Decisions[team.Id].GetValueOrDefault(d.Id);
                        submitted[d.Id] = Terminal.AskDecision(d, previous);
                    }
                    allChoices[team.Id] = submitted;
                }
                RoundSnapshot snapshot = service.Advance(session, allChoices);
                store.Save(session);
                foreach (Team team in session.Teams.Where(t => !t.IsAi || session.Lecturer))
                {
                    Terminal.Pause();
                    ShowRound(session, snapshot, team);
                    if (!team.IsAi)
                    {
                        int before = careerXp[team.Id];
                        int earned = 20 + (session.Completed ? 100 : 0);
                        careerXp[team.Id] += earned;
                        Terminal.Color($"+{earned} XP | " + Career.Status(careerXp[team.Id]), ConsoleColor.Magenta);
                        if (Career.Level(careerXp[team.Id]) > Career.Level(before))
                            Terminal.Color($"LEVEL UP! {team.Name} reached level {Career.Level(careerXp[team.Id])}.", ConsoleColor.Green);
                    }
                }
                if (session.Lecturer) { Board(session); Dashboard(session); }
                Terminal.Pause();
            }
            Board(session);
            Debrief(session);
            Terminal.Write("Results saved automatically.");
            Terminal.Pause();
            Inspect(session);
        }
        private static void DecisionInformation(SimulationDefinition d, string name, TeamState state, MarketState market)
        {
            List<string> lines = [$"Cash {state.Cash:N2} EUR | Debt {state.Debt:N2} EUR"];
            if (d.Template is Template.Pricing or Template.Marketing or Template.Market)
                lines.Add($"Price {state.Price:F2} EUR | Current unit cost {market.UnitCost:F2} EUR | Inventory {state.Inventory:N0} | Share {state.MarketShare:F1}%");
            if (d.Template is Template.Inventory or Template.BeerGame)
                lines.Add($"Stock {state.Inventory:N0} | Backlog {state.Backlog:N0} | Pipeline {state.Pipeline.Sum():N0} | Last demand {state.Metrics.GetValueOrDefault("Demand", MathModel.P(d, "InventoryDemand", 20)):N0}");
            if (d.Template == Template.Investment) lines.Add($"Funding rate {market.Interest * 100:F1}% | Installed capital {state.Metrics.GetValueOrDefault("InstalledCapital"):N2} EUR");
            if (d.Template == Template.CashFlow) lines.Add($"Receivables {state.Metrics.GetValueOrDefault("Receivables"):N2} EUR | Funding rate {market.Interest * 100:F1}%");
            if (d.Template == Template.Portfolio) lines.Add($"Funding rate {market.Interest * 100:F1}% | Previous portfolio return {state.Metrics.GetValueOrDefault("PortfolioReturn"):F2}%");
            if (d.Template == Template.Project) lines.Add($"Progress {state.Progress:F1}% | Customer satisfaction {state.Satisfaction:F1}/100");
            if (d.Template == Template.Probability)
            {
                int mode = (int)d.Parameters["Mode"];
                if (mode is 0 or 1 or 5 or 7 or 8) lines.Add($"Success probability {d.Parameters["Probability"] * 100:F1}% | Binomial trials {d.Parameters["Trials"]:F0}");
                if (mode == 2) lines.Add($"Poisson arrival rate lambda = {d.Parameters["Lambda"]:F1}");
                if (mode == 3) lines.Add($"P(A and B) = {d.Parameters["Joint"]:F2}; P(B) = {d.Parameters["Condition"]:F2}");
                if (mode == 4) lines.Add($"Prior {d.Parameters["Prior"] * 100:F1}%; sensitivity {d.Parameters["Sensitivity"] * 100:F1}%; false-positive rate {d.Parameters["FalsePositive"] * 100:F1}%");
                if (mode == 6) lines.Add($"Normal demand: mean {d.Parameters["Mean"]:F1}, standard deviation {d.Parameters["Deviation"]:F1}");
                if (mode == 9) lines.Add("Loss chance 2%; loss 10,000 EUR; insurance premium 260 EUR.");
            }
            Terminal.Panel(name + " - decision information", lines);
        }
        private void ShowRound(Session session, RoundSnapshot snapshot, Team team)
        {
            TeamState state = snapshot.After[team.Id];
            List<string> lines = [ "DECISIONS: "+string.Join(" | ",snapshot.Decisions[team.Id].Select(d => d.Key+"="+LocalStore.Format(d.Value))),
                $"Revenue {state.Revenue:N2} EUR | Cost {state.Cost:N2} EUR | Profit {state.Profit:N2} EUR",
                $"Total profit {state.TotalProfit:N2} EUR | Cash {state.Cash:N2} EUR | Score {snapshot.Scores[team.Id]:F1}/1000" ];
            foreach (ScoringRule rule in session.Definition.Scoring.Where(r => r.Metric is not ("Profit" or "Liquidity")))
                lines.Add($"{rule.Metric}: {state.Metrics.GetValueOrDefault(rule.Metric):N2}");
            if (session.Definition.Template is Template.Inventory or Template.BeerGame)
                lines.Add($"Stock {state.Inventory:N0}; backlog {state.Backlog:N0}; arrivals {state.Metrics["Arrivals"]:N0}");
            lines.Add("WHY: " + snapshot.Explanations[team.Id]);
            Terminal.Panel(team.Name + " - result", lines);
            if (settings.DetailedMetrics) Terminal.Panel("All active metrics", state.Metrics.OrderBy(p => p.Key).Select(p => $"{p.Key}: {p.Value:N2}"));
            Terminal.Bars(team.Name + " profit history", session.History.Select(r => ($"R{r.Number}", (double)r.After[team.Id].Profit)));
            if (session.Definition.Template is Template.Inventory or Template.BeerGame)
            {
                Terminal.Write("Demand: " + string.Join(" ", session.History.Select(r => r.After[team.Id].Metrics["Demand"].ToString("F0"))));
                Terminal.Write("Orders: " + string.Join(" ", session.History.Select(r => r.After[team.Id].Metrics["Orders"].ToString("F0"))));
                Terminal.Bars("Ending inventory", session.History.Select(r => ($"R{r.Number}", r.After[team.Id].Inventory)));
            }
        }
        private static void Board(Session session)
        {
            List<BoardEntry> entries = SessionService.Board(session);
            Terminal.Panel("Leaderboard", entries.Select(e => $"{e.Rank}. {e.Name,-24} {e.Score:F1} points | Profit {e.Profit:N2} EUR"));
            Terminal.Bars("Score", entries.Select(e => (e.Name, e.Score)));
        }
        private static void Dashboard(Session session)
        {
            RoundSnapshot round = session.History[^1];
            List<string> lines = [$"Average period profit {round.After.Values.Average(s => s.Profit):N2} EUR"];
            if (session.Definition.Template is Template.Pricing or Template.Marketing)
            {
                int cuts = session.Teams.Count(t => round.After[t.Id].Price < round.Before[t.Id].Price * 0.9m);
                lines.Add($"Average price {round.After.Values.Average(s => s.Price):F2} EUR; {100.0 * cuts / session.Teams.Count:F0}% of teams cut prices by more than 10%.");
                lines.Add("Check whether additional volume covered the lost unit margin.");
            }
            if (session.Definition.Template is Template.Inventory or Template.BeerGame)
                lines.Add($"Average inventory {round.After.Values.Average(s => s.Inventory):F1}; backlog {round.After.Values.Average(s => s.Backlog):F1}; forecast accuracy {round.After.Values.Average(s => s.Metrics["ForecastAccuracy"]):F1}%.");
            Terminal.Panel("Class analytics", lines);
        }
        private static void Debrief(Session session)
        {
            List<string> lines = ["LEARNING OBJECTIVE: Apply "+session.Lesson.ToLowerInvariant()+" using decisions and observed outcomes.",
                "THEORY CONNECTION: "+session.Definition.Theory,
                $"CLASS RESULT: Average period profit {session.History.SelectMany(r => r.After.Values).Average(s => s.Profit):N2} EUR." ];
            foreach (Team team in session.Teams)
            {
                List<RoundSnapshot> sorted = session.History.OrderBy(r => r.After[team.Id].Profit).ToList();
                lines.Add($"{team.Name}: best period by profit R{sorted[^1].Number} ({sorted[^1].After[team.Id].Profit:N2} EUR); worst R{sorted[0].Number} ({sorted[0].After[team.Id].Profit:N2} EUR).");
                if (session.Definition.Template is Template.Pricing or Template.Marketing)
                    lines.Add($"Average share {session.History.Average(r => r.After[team.Id].MarketShare):F1}%; pricing stability {session.History.Average(r => r.After[team.Id].Metrics["PriceStability"]):F1}/100.");
            }
            if (session.Definition.Template == Template.BeerGame)
            {
                double Variance(IEnumerable<double> source)
                {
                    List<double> values = source.ToList();
                    double mean = values.Average();
                    return values.Average(value => Math.Pow(value - mean, 2));
                }
                List<TeamState> states = session.History.SelectMany(r => r.After.Values).ToList();
                double demandVariance = Variance(states.Select(s => s.Metrics["Demand"]));
                double orderVariance = Variance(states.Select(s => s.Metrics["Orders"]));
                lines.Add($"BULLWHIP: Order/demand variance ratio {(demandVariance > 0 ? orderVariance / demandVariance : 0):F2}. Count delayed orders before reacting to a shortage.");
            }
            if (session.Definition.Template == Template.Probability) lines.Add("COMMON MISTAKE: A favorable realized draw does not prove the decision had a high expected payoff. Compare expected outcomes and risk.");
            if (session.States.Values.Any(s => s.Debt > 0)) lines.Add("LIQUIDITY: Some teams used debt; next-round interest increases the cost of cash shortages.");
            lines.Add("RECOMMENDED STRATEGY: " + session.Definition.Strategy);
            Terminal.Panel("Educational debrief", lines);
        }
        private void Inspect(Session session)
        {
            while (true)
            {
                Terminal.Panel("Explore results", ["1. Counterfactual", "2. Replay a round", "3. Export CSV", "4. Show debrief", "0. Return to menu"]);
                int selected = Terminal.Integer("Select", 0, 4, 0);
                if (selected == 0) return;
                if (session.History.Count == 0 && selected != 3) { Terminal.Write("No completed rounds yet."); continue; }
                switch (selected)
                {
                    case 1: Counterfactual(session); break;
                    case 2: Replay(session); break;
                    case 3: Terminal.Write("CSV reports: " + store.Export(session)); break;
                    case 4: Debrief(session); break;
                }
                Terminal.Pause();
            }
        }
        private void Counterfactual(Session session)
        {
            Terminal.Panel("Counterfactual", ["Clone one saved round; keep the same market, seed and other teams' decisions.", "This compares immediate effects; future rounds are not rerun."]);
            for (int i = 0; i < session.Teams.Count; i++) Terminal.Write($"{i + 1}. {session.Teams[i].Name}");
            Team team = session.Teams[Terminal.Integer("Team", 1, session.Teams.Count, 1) - 1];
            int round = Terminal.Integer("Round", 1, session.History.Count, Math.Min(2, session.History.Count));
            for (int i = 0; i < session.Definition.Decisions.Count; i++) Terminal.Write($"{i + 1}. {session.Definition.Decisions[i].Name}");
            DecisionDefinition d = session.Definition.Decisions[Terminal.Integer("Decision", 1, session.Definition.Decisions.Count, 1) - 1];
            RoundSnapshot actual = session.History[round - 1];
            Decision alternative = Terminal.AskDecision(d, d.Id == "Price" ? new(2.38m) : actual.Decisions[team.Id][d.Id]);
            TeamState result = service.Counterfactual(session, round, team.Id, d.Id, alternative).States[team.Id];
            double score = MathModel.Score(session.Definition, session.History.Take(round - 1).Select(r => r.After[team.Id]).Append(result).ToList());
            Terminal.Panel("Actual versus alternative", [
                $"ACTUAL: {LocalStore.Format(actual.Decisions[team.Id][d.Id])}; profit {actual.After[team.Id].Profit:N2} EUR; score {actual.Scores[team.Id]:F1}",
                $"ALTERNATIVE: {LocalStore.Format(alternative)}; profit {result.Profit:N2} EUR; score {score:F1}",
                $"Difference {result.Profit-actual.After[team.Id].Profit:+0.00;-0.00;0.00} EUR; score change {score-actual.Scores[team.Id]:+0.0;-0.0;0.0}",
                "The original session is unchanged."
            ]);
        }
        private static void Replay(Session session)
        {
            int number = Terminal.Integer("Round", 1, session.History.Count, 1);
            RoundSnapshot round = session.History[number - 1];
            Terminal.Panel($"Replay round {number}", round.Market.News);
            foreach (Team team in session.Teams)
            {
                List<string> lines = [];
                foreach ((string key, Decision choice) in round.Decisions[team.Id])
                {
                    Decision previous = number > 1 ? session.History[number - 2].Decisions[team.Id][key] : Validation.Default(session.Definition.Decisions.Single(d => d.Id == key));
                    lines.Add(key + ": " + LocalStore.Format(previous) + " -> " + LocalStore.Format(choice));
                }
                TeamState before = round.Before[team.Id], after = round.After[team.Id];
                lines.Add($"Profit {after.Profit:N2} EUR; cash change {after.Cash - before.Cash:+0.00;-0.00;0.00} EUR.");
                lines.Add($"Carryover: stock {after.Inventory:N0}; backlog {after.Backlog:N0}; debt {after.Debt:N2} EUR.");
                lines.Add(round.Explanations[team.Id]);
                Terminal.Panel(team.Name, lines);
            }
        }
        private void Previous()
        {
            List<Session> sessions = store.Sessions();
            Terminal.Panel("Previous sessions", sessions.Select((s, i) => $"{i + 1}. {s.Course} / {s.Lesson} | {s.History.Count}/{s.Rounds} rounds | {(s.Completed ? "Completed" : "Incomplete")}"));
            if (sessions.Count == 0) return;
            int selected = Terminal.Integer("Session (0 to return)", 0, sessions.Count, 0);
            if (selected == 0) return;
            Session session = sessions[selected - 1];
            if (!session.Completed && Terminal.YesNo("Resume this session")) Play(session); else Inspect(session);
        }
        private void ConfigureSettings()
        {
            settings.Difficulty = ChooseDifficulty();
            settings.Rounds = Terminal.Integer("Default rounds", 3, 10, settings.Rounds);
            settings.DurationMinutes = Terminal.Integer("Duration: 1 = 15 minutes, 2 = 20 minutes", 1, 2, settings.DurationMinutes == 15 ? 1 : 2) == 1 ? 15 : 20;
            settings.Seed = Terminal.Integer("Default seed", int.MinValue, int.MaxValue, settings.Seed);
            settings.DetailedMetrics = Terminal.YesNo("Show all active metrics", settings.DetailedMetrics);
            store.SaveSettings(settings);
            Terminal.Write("Settings saved.");
        }
        private static void Plans()
        {
            Terminal.Panel("License and subscription", [
                "FREE INSTRUCTOR | 5 demo simulations | Maximum 10 players",
                "COURSE | One course pack | Classroom analytics",
                "FACULTY | All 6 course packs | Unlimited sessions | CSV export",
                "ENTERPRISE | University license | Planned LMS integration and SSO",
                "Prototype access is unrestricted. No payment processing."
            ]);
            Terminal.Read("Press Enter to continue");
        }
        private void ProductAnalytics()
        {
            List<Session> results = store.Sessions().Where(s => s.Completed).ToList();
            string MostUsed(Func<Session, string> key) => results.Count == 0 ? "None yet"
                : results.GroupBy(key).OrderByDescending(g => g.Count()).ThenBy(g => g.Key).First().Key;
            Terminal.Panel("Local product analytics", [
                $"Simulations played: {results.Count}",
                $"Active courses: {results.Select(s => s.Course).Distinct().Count()}",
                $"Student participations: {results.Sum(s => s.Teams.Where(t => !t.IsAi).Sum(t => t.Players.Count))}",
                $"Average score: {(results.Count == 0 ? 0 : results.SelectMany(SessionService.Board).Average(e => e.Score)):F1}/1000",
                "Most used course: "+MostUsed(s => s.Course),"Most used lesson: "+MostUsed(s => s.Lesson)
            ]);
            Terminal.Read("Press Enter to continue");
        }
        public void SmokeTest()
        {
            List<Team> teams = [new("freshfizz", "FreshFizz", false, ["Presenter"])];
            AddBots(teams, 3);
            Session session = service.Create(Curriculum.FreshFizz(), "Marketing", "Pricing", teams);
            decimal[] prices = [2.5m, 2.2m, 2.4m, 2.65m, 2.5m];
            while (!session.Completed)
            {
                MarketState market = service.Prepare(session);
                Dictionary<string, Dictionary<string, Decision>> choices = teams.ToDictionary(t => t.Id, t => ai.Choose(session, t.Id, market));
                choices["freshfizz"]["Price"] = new(prices[session.History.Count]);
                RoundSnapshot round = service.Advance(session, choices);
                store.Save(session);
                Terminal.Panel($"Demo round {round.Number}", [.. round.Market.News, $"FreshFizz profit {round.After["freshfizz"].Profit:N2} EUR"]);
            }
            Board(session); Debrief(session);
            TeamState alternative = service.Counterfactual(session, 2, "freshfizz", "Price", new(2.38m)).States["freshfizz"];
            Terminal.Write($"Alternative price 2.38 EUR: profit {alternative.Profit:N2} EUR.");
            Terminal.Write("CSV reports: " + store.Export(session));
            if (courses.Count != 6 || courses.Sum(c => c.Lessons.Count) != 60 || session.History.Count != 5)
                throw new InvalidOperationException("Smoke test count mismatch.");
            Terminal.Write("SMOKE TEST PASSED: 6 courses, 60 simulations, 5 demo rounds.");
        }
        public void VerifyAll()
        {
            // This exercises real decisions and rounds, rather than checking only catalog size.
            int count = 0;
            foreach (Difficulty difficulty in Enum.GetValues<Difficulty>())
                foreach (Course course in courses)
                    foreach (Lesson lesson in course.Lessons)
                    {
                        List<Team> teams = [new("team-1", "Alpha", true, []), new("team-2", "Beta", true, [])];
                        Session session = service.Create(lesson.Simulation, course.Name, lesson.Name, teams, difficulty: difficulty);
                        while (!session.Completed)
                        {
                            MarketState market = service.Prepare(session);
                            service.Advance(session, teams.ToDictionary(t => t.Id, t => ai.Choose(session, t.Id, market)));
                        }
                        if (SessionService.Board(session).Any(e => e.Score < 0 || e.Score > 1000))
                            throw new InvalidOperationException("Score out of bounds: " + lesson.Name);
                        count++;
                    }
            Terminal.Write($"VERIFIED: {count} complete sessions; all 60 simulations at all 3 difficulties.");
        }
    }

    // Exact discrete distributions and bounded finance formulas are reusable across labs.
    public static class MathModel
    {
        public static decimal Money(double number) => decimal.Round((decimal)Math.Clamp(double.IsFinite(number) ? number : 0, -1e12, 1e12), 2);
        public static double P(SimulationDefinition d, string key, double fallback) => d.Parameters.GetValueOrDefault(key, fallback);
        public static double D(Dictionary<string, Decision> choices, string key, double fallback = 0) => choices.TryGetValue(key, out Decision? value) ? (double)value.Number : fallback;
        public static double Elastic(double relativePrice, double elasticity) => Math.Pow(Math.Clamp(relativePrice, 0.1, 10), -Math.Clamp(elasticity, 0.1, 5));
        public static double Marketing(double spending) => 1 + Math.Log(1 + Math.Max(0, spending) / 1000) * 0.2;
        public static double Draw(int seed, int round, string key)
        {
            // Stable hashing avoids runtime-dependent string hashes and global random state.
            uint value = unchecked((uint)seed) ^ unchecked((uint)round * 2654435761u);
            foreach (char character in key) value = unchecked((value ^ character) * 16777619u);
            value ^= value << 13; value ^= value >> 17; value ^= value << 5;
            return (value + 0.5) / (uint.MaxValue + 1.0);
        }
        public static double Shock(int seed, int round, string key, double amplitude) => 1 + (Draw(seed, round, key) * 2 - 1) * amplitude;
        public static double BinomialPmf(int k, int n, double probability)
        {
            if (k < 0 || k > n) return 0;
            if (probability <= 0) return k == 0 ? 1 : 0;
            if (probability >= 1) return k == n ? 1 : 0;
            // Compute n choose k without factorial overflow.
            double coefficient = 1;
            for (int i = 1; i <= Math.Min(k, n - k); i++) coefficient *= (double)(n - i + 1) / i;
            return coefficient * Math.Pow(probability, k) * Math.Pow(1 - probability, n - k);
        }
        public static double BinomialCdf(int k, int n, double probability) => Enumerable.Range(0, Math.Clamp(k + 1, 0, n + 1)).Sum(i => BinomialPmf(i, n, probability));
        public static int BinomialSample(int n, double probability, double draw)
        {
            double cumulative = 0;
            for (int k = 0; k <= n; k++) { cumulative += BinomialPmf(k, n, probability); if (draw <= cumulative) return k; }
            return n;
        }
        public static double PoissonPmf(int k, double lambda)
        {
            double result = Math.Exp(-lambda);
            for (int i = 1; i <= k; i++) result *= lambda / i;
            return result;
        }
        public static double PoissonCdf(int k, double lambda) => Enumerable.Range(0, Math.Clamp(k + 1, 0, 101)).Sum(i => PoissonPmf(i, lambda));
        public static int PoissonSample(double lambda, double draw)
        {
            double cumulative = 0;
            for (int k = 0; k <= 100; k++) { cumulative += PoissonPmf(k, lambda); if (draw <= cumulative) return k; }
            return 100;
        }
        public static double Bayes(double prior, double sensitivity, double falsePositiveRate) => prior * sensitivity / Math.Max(1e-12, prior * sensitivity + (1 - prior) * falsePositiveRate);
        public static double Npv(double capital, double cashFlow, double rate, int periods) => Enumerable.Range(1, periods).Sum(i => cashFlow / Math.Pow(1 + rate, i)) - capital;
        public static double Irr(double capital, double cashFlow, int periods)
        {
            // For conventional positive future cash flows, NPV decreases as the rate rises.
            if (capital <= 0) return 0;
            double low = -0.95, high = 5;
            for (int i = 0; i < 80; i++) { double mid = (low + high) / 2; if (Npv(capital, cashFlow, mid, periods) > 0) low = mid; else high = mid; }
            return (low + high) / 2;
        }
        public static double Score(SimulationDefinition d, IReadOnlyList<TeamState> history)
        {
            if (history.Count == 0) return 0;
            // Scores use fixed target ranges, so a team's score is not a class percentile.
            double totalWeight = d.Scoring.Sum(r => r.Weight);
            double weighted = d.Scoring.Sum(rule =>
            {
                double metric = rule.Metric is "Profit" or "Liquidity" or "Progress" or "ExpectedProfit" ? history[^1].Metrics.GetValueOrDefault(rule.Metric) : history.Average(s => s.Metrics.GetValueOrDefault(rule.Metric));
                double normalized = Math.Clamp((metric - rule.Min) / (rule.Max - rule.Min), 0, 1);
                return rule.Weight * (rule.HigherIsBetter ? normalized : 1 - normalized);
            });
            return Math.Round(weighted / totalWeight * 1000, 1);
        }
        public static void Account(TeamState state, decimal revenue, decimal costs, MarketState market, decimal capital = 0, decimal borrowing = 0)
        {
            // Capital spending affects cash, not current operating profit. New debt adds cash.
            decimal interest = decimal.Round(state.Debt * (decimal)market.Interest, 2);
            state.Revenue = revenue; state.Cost = costs + interest;
            state.Profit = revenue - state.Cost; state.TotalProfit += state.Profit;
            state.Cash += state.Profit - capital + borrowing; state.Debt += borrowing;
            if (state.Cash < 0) { state.Debt -= state.Cash; state.Cash = 0; }
            state.Metrics["Profit"] = (double)state.TotalProfit;
            state.Metrics["Liquidity"] = (double)state.Cash;
            state.Metrics["DebtProtection"] = Math.Clamp(100 - (double)state.Debt / 1000, 0, 100);
            state.Metrics["Satisfaction"] = state.Satisfaction;
        }
    }
}
