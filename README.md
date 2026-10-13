# BusinessGame v3

## Start

On Windows, double-click **StartGame.cmd**, or open **BusinessGameV3.sln** in Visual Studio. The `play` folder contains a ready-built executable; it uses the .NET 8 runtime installed on this computer.

Use a terminal at least **108 columns × 40 rows**. **120 × 42** is recommended. The wider layout keeps three readable cards, the original statistics desk, and the quota/booster panel visible together. The game attempts to resize Windows console windows where supported.

Press **T** on the main menu, or choose **Tutorial — 2 minutes**, for the guided practice run. It takes roughly two minutes at a normal reading pace; there is no countdown or forced delay.

## What's new

- **32 decision cards**, up from 16, plus **20 booster choices** and a larger optional-event pool.
- **Common, Uncommon, Rare, Epic and Legendary** rarities. Weighted draws make rarer cards less frequent. Cards in a single deal are distinct.
- Every card has a funny explanation of **five words or fewer**, with the actual numeric definition underneath. Example: Black Friday Sale says **“Low price, more buy”**, followed by its price, demand and quality changes.
- **I** opens a full explanation for a selected decision, optional event, booster, decoration or company event.
- A persistent **right-side menu** shows the current/next month's quotas, **last month's profit and sales revenue**, and active **green and purple booster cards**. Click a booster to inspect it; **Page Up / Page Down** scrolls long lists. Repeated booster copies are grouped with a count. Instant cash/stock gifts are consumed and do not appear as ongoing effects.
- Every difficulty starts with **one reroll each month**. Acquired reroll boosters and the Lucky Cat add to that base.
- The old **sloping statistics desk** displays cash, debt, price, unit cost, inventory and share again, with R and S buttons beside it.
- After every cleared quota, a **company-news event** appears before the booster reward. New competitors, internet scandals, good PR, new majorities, strikes, investors and other developments change the business. All consequences specify their timing. These are mandatory company developments; the earlier three-card event phase remains optional.
- An interactive tutorial plays through one practice month and begins the next. It teaches card selection, effects, the reroll, optional events, quota results, mandatory company news, boosters, shop purchases and progression. The tutorial awards one extra booster to show both colors. Practice results do not enter run history.

## The monthly cycle

**Decisions → optional events → quota check → company news → booster → shop → next month.**

Choose a course, a topic, and a difficulty chip. Full runs require **12 consecutive monthly quota clears**. All displayed targets must be met. A missed quota ends the run. After the twelfth clear, the final company headline leads to victory; it cannot undo the already earned win.

Green boosters provide regular permanent improvements. Purple boosters are Rare/Epic/Legendary improvements. Colors indicate their family; each card also prints its exact rarity. Read the numbers and costs before deciding: rarity does not guarantee the best choice for your current situation.

Company-news changes marked **next month** last for that following month unless explicitly called permanent. Changes marked **now** apply once when you acknowledge the news. News never applies twice if you revisit screens. The PR Guardian booster softens internet scandals.

The side panel shows next month's quota while viewing company news, choosing rewards, or shopping. In the decision and optional-event phases, it shows the current month's quota beside the previous month's actual earnings. New runs show `--` because there is no previous month yet. **Profit** is revenue minus operating expenses; **sales** is revenue before expenses. They are shown separately.

## Controls

| Screen | Controls |
|---|---|
| Menus | Arrows + Enter; numbers or mouse; T starts the tutorial; G opens the full guide |
| Cards | Arrows / 1–3 / mouse select; Enter plays; Space deselects; I explains |
| Decisions | R rerolls, even without a selection; E ends decisions early |
| Optional events | Enter accepts one selected card; E leaves all three |
| Quota result / company news | Enter continues or acknowledges |
| Booster pack | Enter claims one; E skips in a regular run |
| Shop | Arrows / 1–6 / mouse select; Enter buys; I explains; S/ESC leaves; N advances |
| Active boosters | Page Up / Page Down or side arrows scroll; click opens details |
| Active run | ESC pauses; Enter/ESC resumes; M abandons to main menu |
| Finished run | R retries; Enter/ESC returns to menu |

Compatible Windows consoles support mouse input; the keyboard controls cover every action.

## Build, save and verify

The six EconForge courses and all 60 original topic names, theory and strategy data are retained. Settings and finished results are saved to `data` beside the executable. Active runs are not saved. The earlier game versions are separate projects.

```powershell
dotnet run --project BusinessGameV3.csproj
dotnet run --project BusinessGameV3.csproj -- --self-test
dotnet run --project BusinessGameV3.csproj -- --preview
```

Automated checks cover all card explanations and rarity distribution; one-reroll behavior; company-news timing and guards; booster persistence; the old desk; side-panel colors, scrolling and inspection; shop controls; the whole tutorial; saved settings/results; and all **180 course/topic/difficulty combinations**. Every combination has a verified 12-month victory path. UI text previews are generated at both supported minimum and recommended terminal sizes.

The included source is a complete project. Keep `course-data.json` with the project when rebuilding; the project embeds it into the executable.
