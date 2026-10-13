# TranslateUni

The next version of the business card game, with a merged learning guide, WASD controls and a rotating decoration shop.

## Play

Extract the complete package and double-click **StartGame.cmd**. The ready-built game is in `play/TranslateUni.exe` and uses the installed .NET 8 runtime.

For source development, open **TranslateUni.sln** in Visual Studio, or run:

```powershell
dotnet run --project TranslateUni.csproj
```

Recommended terminal: **120 columns × 42 rows**. Minimum: **108 × 40**. The game tries to resize compatible Windows console windows.

## Learning Guide + Tutorial

The main menu has one combined **Learning Guide + Tutorial** entry. **T** and **G** both open this same guide. It contains the controls, monthly game cycle, quota rules, economy, boosters and shop instructions. Press **Enter** to start its guided practice, which takes about two minutes at normal reading pace.

Practice demonstrates one complete month and the transition into month two. When it finishes, **Enter returns to the Learning Guide**. Press **ESC** from the guide to return to the main menu. The practice does not add a fake run to your results.

## Controls

**WASD and arrows both navigate.** W/A move up/left; S/D move down/right. Course and shop grids move vertically by a row. Menus, topics, difficulty choices, cards and settings support these keys. Number shortcuts, Enter and mouse selection remain available.

| Action | Keys |
|---|---|
| Navigate / select | WASD, arrows, numbers, or mouse |
| Play card / accept choice / buy | Enter |
| Clear a selected card | Space, during card phases |
| Inspect selected card or decoration | I |
| End decisions / skip optional events | E |
| Decision/event reroll | R; one base reroll per month |
| Shop reroll | R; spends cash and refreshes six offers |
| **Leave shop and start next month** | **Space** |
| Return from shop to rewards | B or ESC |
| Reopen shop from rewards | B |
| Advance from rewards | Space or Enter |
| Pause active run | ESC |
| Scroll active-booster sidebar | Page Up / Page Down |
| Learning Guide + Tutorial | T or G from the main menu |

S is used for downward navigation. The shop shortcut is **B**, and **N no longer advances the month**.

## Rotating shop: 20 possibilities

The two shelves show **six distinct offers** drawn from a pool of **20 decorations**. Offers rotate after each monthly quota and reward sequence. Reopening the shop between months keeps its current offers.

Press **R** or click the shop reroll button to refresh the shelves. The first refresh costs **$75**; each additional refresh that month costs **$25 more**. This fee resets in the next monthly shop and is independent of decision-card rerolls. A refresh introduces at least one new offer. Unaffordable refreshes change neither cash nor stock, and a sold-out pool does not charge a refresh fee.

Decorations can be purchased once per run. Owned decorations are excluded from future draws. If fewer than six remain, some shelves are empty. If all twenty are owned, the shop safely displays empty slots and you can still press Space to continue.

The pool includes the original plant, lamp, clock, art, lucky cat and fountain, plus a coffee machine, calendar, whiteboard, bookcase, fan, aquarium, standing desk, cork board, coin jar, printer, gold pen, meeting table, neon sign and executive chair. Their effects help demand, unit cost, overhead, capacity, stock, income, debt, risk, quality, share, rerolls or decisions. Each offer shows its price, rarity, short explanation and actual effect; **I** opens the full definition.

## The game

Choose one of six courses, one of ten topics, and one of three difficulties. Meet all quotas for **12 months** to win. The run follows:

**Decisions → optional events → quota results → company news → booster → shop → next month.**

The original statistics desk, short card explanations, card rarities, persistent quota/earnings panel, green/purple booster sidebar, and company-news events are included. Settings and finished results are saved to `data` beside the executable. Active runs are not saved.

## Verification

```powershell
dotnet run --project TranslateUni.csproj -- --self-test
dotnet run --project TranslateUni.csproj -- --preview
```

Checks cover merged-guide navigation and practice, WASD controls, Space shop exit, all 20 decoration effects, rotating and paid shop refreshes, purchase guards, sold-out pools, card/booster behavior, rendering and saved results. All **180 course/topic/difficulty combinations** have a verified twelve-month victory path.

Keep `course-data.json` with the source when rebuilding; it is embedded into the executable.
