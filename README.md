# TranslateUni v5

## Start

Double-click **StartGame.cmd** after extracting the package. The game starts fullscreen by default. On Windows, it uses a dedicated Windows Terminal fullscreen window when Windows Terminal is available; traditional console windows use a borderless fullscreen fallback. The fallback restores the window frame and position on exit.

Fullscreen uses Microsoft's documented [Windows Terminal fullscreen launch option](https://learn.microsoft.com/windows/terminal/command-line-arguments). It does not change global terminal settings or send shortcuts to other applications. Some embedded terminal hosts provide an invisible console proxy; those hosts cannot be resized through the traditional fallback. For normal desktop play, launch from StartGame.cmd or the executable.

For a windowed development session:

```powershell
.\play\TranslateUni.exe --windowed
```

The ready-built executable uses the .NET 8 runtime. Open **TranslateUni.sln** in Visual Studio to edit the source. The minimum terminal size is **108 × 40**; **120 × 42** is recommended. In larger fullscreen viewports, the game fills the screen and centers its readable interface.

## Original card animation restored

Card play, rerolls and leaving a card phase now show the sequence from the original uploaded BusinessGame_Debugged.cs:

1. **Horizontal shrink:** 12 frames, with the text fading as the card narrows.
2. **Vertical collapse:** 5 frames, down to a single row.
3. **Star burst:** 5 frames of stars and sparks, ending with a clean disappearance.

The original timing totals about one second. Raised-card positions are respected, and the desk, quotas and booster sidebar remain visible during the animation. Unaffordable or invalid actions do not play a disappearance animation.

## Weaker bases, powerful upgrades

All **20 shop item families** have **three normal tiers**. Base items are cheaper and weaker than in the previous version. Buy an item, and its next tier appears in that shelf slot. Press Enter again to purchase the upgrade, or return in another monthly shop.

The shop offers the next tier of each eligible family. Maxed families are excluded from future draws. Each tier's displayed bonus is the **total effect at that tier**: an upgrade adds only the difference from the previous tier. Bonuses do not accidentally stack the full totals of every purchased tier.

Some modest base items provide periodic effects. A base Wall Clock gives one extra decision every third month; its improved version does so every second month, and its gold version gives one every month. The Lucky Cat follows the same pattern for rerolls. The displayed `/3mo` and `/2mo` bonuses refer to every third/even month of the run.

Example upgrade path:

| Tier | Item | Price for this tier | Total effect |
|---|---|---:|---|
| 1 | Boss Chair | $500 | Capacity +15 |
| 2 | Comfy Boss Chair | $900 | Capacity +35; +1 decision every second month |
| 3 | Gold Boss Chair | $1,600 | Capacity +80; +1 decision each month |
| 4 | Diamond Boss Chair | $3,000 | Capacity +175; +2 decisions each month |
| 5 | Cosmic Boss Chair | $6,000 | Capacity +400; +4 decisions each month |

Tiers must be purchased in order. The prices above are the price of each purchase, not the combined cost of reaching that tier. Other families also grow sharply: for example, a Desk Lamp goes from a $0.10 unit-cost saving at tier 1 to $3.00 at tier 5, and a Desk Plant goes from +1% demand to +35% demand.

**I** opens the full upgrade definition, including current tier, next tier, price, total effect and unlock requirements. More expensive tiers are colored by rarity. Immediate stat gains apply when purchased; recurring stock, income, decisions and rerolls take effect from the following month.

## Endless mode

Clear all **12 normal monthly quotas** to win. On the victory screen, press **E** or click **Continue Endless** to keep the same company, cash, stock, boosters and upgrades.

- Choosing Endless unlocks **tier 4 (Diamond)** and **tier 5 (Cosmic)** for every shop family.
- A booster reward and shop visit prepare you for month 13.
- Every endless profit quota is **35% higher than the previous month's**: the targets compound rather than grow by a fixed amount.
- Existing secondary quotas still apply, and company-news events continue after each clear.
- There is no fixed final month. A missed quota ends the endless attempt.
- Your original twelve-month victory remains a win. Results add the number of extra quotas cleared and update the same run record rather than creating duplicate wins.

Returning to the menu from a paused endless run saves its current result, but active company state is not saved for later resumption. A new run must earn its own twelve-month clear before choosing Endless.

## Controls and learning

The combined **Learning Guide + Tutorial** is available with **T/G** from the main menu. It includes the controls and a guided practice month. The guide and practice mention upgrades and the endless unlock.

| Action | Keys |
|---|---|
| Navigate / choose | WASD, arrows, numbers, mouse |
| Play, buy or upgrade | Enter |
| Inspect full definition | I |
| Clear selected card | Space during card phases |
| End decisions / skip optional cards | E |
| Reroll cards | R, using the monthly card rerolls |
| Reroll shop | R, starting at $75; +$25 for each refresh that month |
| Leave shop / next month | Space |
| Return from shop to rewards / reopen shop | B |
| Pause | ESC |
| Scroll active boosters | Page Up / Page Down |
| **Continue Endless after winning** | **E on the victory screen** |

The six courses, 60 topics, three difficulties, funny short explanations, sloping statistics desk and active-booster sidebar are included. Settings and finished results are stored in `data` beside the executable.

## Verification

```powershell
dotnet run --project TranslateUni.csproj -- --self-test
dotnet run --project TranslateUni.csproj -- --preview
```

Checks cover the animation's original three stages and timing, fullscreen-size rendering, all 20 families and five tier definitions, upgrade prices and effect replacement, normal/endless tier limits, victory unlocks, month-13 progression, compounded quotas, continued company events, and saved endless results. All **180 course/topic/difficulty combinations** have a verified normal victory path. The endless sample clears additional quotas and ends naturally when its growing targets exceed the company's results.

Keep `course-data.json` with the project when rebuilding; it is embedded into the executable.

## Fullscreen compatibility update (v5.1)
Startup checks the operating system, process architecture, all monitor pixel dimensions and positions, work areas, DPI scaling, terminal host and console grid. Classic consoles use their own fullscreen command when the old display-mode API is unavailable. Embedded terminals open a dedicated console when Windows Terminal is unavailable. The game does not resize the console after entering fullscreen. Font size is reduced on small classic-console displays when needed to fit the game.

Start with StartGame.cmd. To check compatibility without starting the game, run DiagnoseDisplay.cmd. The report is saved beside the executable in data/display-diagnostics.txt (normally play/data/display-diagnostics.txt). The --fullscreen-test argument briefly tests fullscreen and exits; --windowed disables automatic fullscreen. Monitor sizes are measured in pixels, not physical inches. On non-Windows systems, use the terminal's own fullscreen control.

Validated on Windows 10 build 19045 with three 1920x1080 monitors at 100% scaling: a dedicated console entered fullscreen, reported mode flag 1, and had bounds (0,0)-(1920,1080). The game regression suite passed all 4377 checks. Windows Terminal and other operating systems were not available for live fullscreen testing.

## Animation and statistics update (v5.2)
Animation playback is 25% faster: 22 frames now total 796ms instead of 995ms. Playing a decision, event or booster makes the chosen card taper and curve upward into a dot three rows above its raised top edge, then burst into a star. Only the unchosen cards use horizontal shrink and vertical collapse. Rerolling or skipping collapses all discarded cards.

The original statistics desk is anchored lower in the gameplay area and stays visible on every active-run screen, including results, company news, boosters, shop, pause and inspection. Controls are positioned above the desk. The fullscreen compatibility changes from v5.1 are included.

Validation: Release build with no warnings; all 4471 checks passed, including selected-card flight, animation cleanup, enlarged redraw region, persistent statistics and the full campaign suite. Text-frame previews inspected at 108x40 and 120x42.
