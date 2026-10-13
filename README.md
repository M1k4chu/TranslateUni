# BusinessGame v2 — EconForge edition

A new C#/.NET 8 console card game. The course catalog is data extracted from EconForge; the quota economy, game flow, UI and persistence are implemented for this version.

## Play

On Windows, double-click **StartGame.cmd**. The `play` folder contains a built copy of the game. It requires the .NET 8 runtime, which is installed on this computer.

For development, open **BusinessGameV2.sln** in Visual Studio and run the project. You can also use:

```powershell
dotnet run --project BusinessGameV2.csproj
```

Use a terminal at least **80 columns × 30 rows**; **100 × 34** is recommended. The game tries to enlarge Windows console windows where supported. Colors, ASCII cards and difficulty chips give the console UI a Balatro-inspired presentation.

## The run

Choose one of the six EconForge courses, one of its ten topic levels, and an **Easy**, **Normal** or **Hard** chip. Quick Demo starts Marketing / Pricing on Easy, with the full 12-month win condition.

1. **Decisions:** Play up to three cards per month (two on Hard). Price, advertising, stock, production efficiency, debt and course-specific decisions affect your company. Rerolls work with an empty selection. Selected cards rise by two rows.
2. **Events:** Three distinct random cards appear. Accept one, or leave all three. Imported EconForge event descriptions and modifiers are included alongside new business opportunities. Demand, cost, capacity and interest shocks last for the current month; accepted stock, cash, share and quality changes persist.
3. **Quota check:** Monthly sales settle automatically. Meet the profit quota and every secondary target shown on the table. A missed target ends the run. A clear gives a $200 cash bonus.
4. **Booster:** After months 1–11, choose one free booster from three random cards, or skip it. Booster effects last for the run unless the card says “now.”
5. **Shop:** Buy any affordable decorations from two shelves with three items each. Each decoration can be bought once per run. S leaves the shop; S reopens it from the between-months screen. N begins the next month.
6. **Victory:** Clear month 12 to win immediately. The final month ends the run rather than offering upgrades for a thirteenth month.

## Levels and economy

The catalog has **Marketing, Logistics, Probability, Microeconomics, Finance and Project Management**, each with ten original topic names. The Course Library includes the original theory, strategy and decision labels.

These are card-game adaptations of the topics. The economy uses the earlier BusinessGame scale: $5,000 cash, $25 price, $12 unit cost, 450 stock, $1,200 debt and 18% share. Topic parameters influence demand elasticity; selected market-regulation topics add ceilings, floors or externality costs. Each course has a distinct secondary quota and focus card. Later topics add service, cash-buffer and quality requirements, higher profit quotas, and additional decision types.

Quotas rise **14% of the first-month target per month**. Higher difficulty increases targets and limits decisions/rerolls. Stock, costs, debt, decorations and boosters carry forward. Advertising reach decays between months. Excessive prices erode quality.

Profit counts sold-stock costs, overhead, interest, advertising and event gains/losses. Purchasing stock spends cash immediately; the unsold stock remains available. Capital upgrades spend cash without becoming a second operating expense. Investments begin earning next month. The forecast shows the outcome under the current decisions, before accepting an event.

## Controls

| Screen | Controls |
|---|---|
| Menus | Arrows, Enter; click a button; number shortcuts |
| Topics | 1–9 and 0 for topic 10; Enter opens selection |
| Difficulty | Arrows / 1–3 / click a chip; Enter starts |
| Cards | Arrows / 1–3 / click to select; Enter plays; Space clears |
| Decisions | R rerolls; E ends decisions early |
| Events | Enter accepts the selected event; E leaves all three |
| Booster | Enter claims the selected booster; E skips |
| Shop | Arrows / 1–6 / click to select; Enter buys; S/ESC leaves; N advances |
| Active run | ESC opens pause; Enter/ESC resumes; M abandons to menu |
| Finished run | R retries the topic; Enter/ESC returns to menu |

Mouse input is supported in compatible Windows consoles, including terminals that forward console mouse events. All actions also have keyboard controls. Live mouse compatibility depends on the terminal.

## Saved data and verification

Settings and finished-run results are saved to `data` beside the executable. Previous Results and Product Analytics use this local history. Active runs are not saved; the pause screen explains this before abandonment.

Run automated verification with:

```powershell
dotnet run --project BusinessGameV2.csproj -- --self-test
```

The checks exercise the real economy and UI controller, including quota failure, a 12-month victory, phase guards, rerolls, mouse hitboxes, card lift, boosters, purchases, and saved settings/results. They run all 60 topics at all three difficulties and verify a winning path for every combination. These are balance checks, not a substitute for player feedback.

Optional text previews of the principal screens:

```powershell
dotnet run --project BusinessGameV2.csproj -- --preview
```

`course-data.json` is embedded into the executable by the project file. Keep it with the sources when rebuilding. The original EconForge and earlier BusinessGame projects are preserved.
