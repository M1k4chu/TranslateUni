# TranslateUni v6.0

Extract this folder and run StartGame.cmd. Windows 10 and 11 are supported; the .NET 8 runtime is required. A compiled build is included in play/. Open TranslateUni.sln to edit the source.

## Play modes

Student / Team PLAY now opens Singleplayer, CO-OP, Rivalry (2 player), or Rivalry CO-OP. Enter a name and choose one of six exact 4 x 4 ASCII avatars. Singleplayer continues to the usual course, topic and difficulty selection.

Multiplayer works between separate computers. Each player runs this version of the game. Choose the same multiplayer mode before connecting. TAB switches between Host and Join; F6 switches between LAN and Online.

- CO-OP: 2-8 players share one company and vote on decisions, optional events, boosters, shop purchases, rerolls, loans, repayments and advancing to the next month.
- Rivalry: two players each run an independent company, starting with the same course, topic, difficulty and random seed.
- Rivalry CO-OP: two competing companies, each controlled by a voting team of 1-4 players. The host sets Team 1's count with F4. The first player joining Team 2 sets that team's count with F4 before joining. Host F5 provides an initial Team 2 count; the joining captain can override it. Teams may have different counts.

The host selects the course (F1), topic (F2) and difficulty (F3). In the lobby, ENTER starts the match after all chosen slots are filled. Player names must be unique. Rivalry guests join Team 2; Rivalry CO-OP guests choose their team with F5. CO-OP guests join the shared team.

## LAN connection

On one computer choose Host and press ENTER. Its lobby shows local IP addresses and TCP port 45873. On each other computer choose Join, type the host's LAN IP or DNS name, and press ENTER. Allow TranslateUni through Windows Firewall for the network being used if Windows asks. No router forwarding is needed between computers on the same local network.

## Online connection: the host PC is the server

1. Host: press F6 for Online. Type your public IP address, DNS name, or shared VPN address. An optional :port changes the server's listening port; otherwise it uses TCP 45873. Select course/topic/difficulty and team counts, then press ENTER.
2. For direct internet hosting, forward that TCP port on your router to the host PC's local IP and allow inbound connections to the game in Windows Firewall. Keep the host PC and game running. IPv4 and IPv6/DNS endpoints are supported. IPv6 typically needs an inbound firewall allowance rather than IPv4 NAT forwarding.
3. The lobby displays a translateuni:// invite code. F9 saves the complete code to play/data/online-invite.txt in the compiled build. Share that code with your players.
4. Guests: select the matching mode, switch to Online with F6 and Join with TAB, and paste the entire invite code. In Windows Terminal use Ctrl+Shift+V; in a classic console use Ctrl+V. Choose the joining team and any captain's team count, then press ENTER.
5. The host presses ENTER when the lobby is full.

Connections use managed TLS 1.2 with ECDHE and AES-GCM. The invitation pins the host's SHA-256 certificate fingerprint and contains a random room secret. A new host session creates a new invitation. Credentials stay in memory; nothing is installed in Windows certificate or key stores. Bouncy Castle 2.7.0 and its MIT license are included.

There is no central server or relay. The game cannot bypass your router, ISP carrier-grade NAT, a blocked inbound port, or an unreachable public address. If direct port forwarding is unavailable, put the players on a shared VPN and use the host's VPN address. A private home-network IP alone is not reachable from another household. The game does not change router or firewall settings automatically.

## Timers, votes and standings

The decision phase lasts at most 3 minutes. A company that finishes early waits for the other company before playing its optional events. When both companies finish, they advance immediately. The shared 2-minute shop clock starts when the first company reaches the shop. Both companies can finish early by choosing SPACE. When the shop clock expires, outstanding event/results/news/booster screens advance with optional choices skipped, and the next month starts for surviving companies.

In a voting team, the first vote starts a 20-second countdown. Players may change their vote during that window. Once all connected teammates vote, the result resolves early. The choice with the most votes wins; abstentions do not count. A tie opens a 10-second revote restricted to the tied choices. A second tie skips the choice. E votes to skip decisions/events/boosters or reject a shop purchase. SPACE votes to finish shopping. Required result/news screens use ENTER to continue.

Each player's assigned color, avatar, current vote, last settled month's profit and total operating profit appear on the left. CO-OP teammates display their shared company's profit. F7/F8 page through longer player lists. Quotas, booster cards and the statistics board stay on the right/bottom. Timers appear below the left player list so shop prices and card animations stay clear.

Rivalry standings rank quotas cleared first, then total operating profit; equal results are a tie. After the year finishes, teams that cleared all 12 quotas may vote with E to continue into endless mode. All connected eligible teams must agree before continuing together. Endless preserves rapidly rising quotas and unlocks decoration tiers 4-5. SPACE can vote against an endless choice. Q leaves a multiplayer session; when the host leaves, the server closes. If a team loses every connected player, it forfeits. Mid-match reconnection is not supported; disconnected lobby slots can be replaced before starting.

## Existing gameplay and controls

All six courses, 60 topics, three difficulties, 12 monthly quotas, guided tutorial/learning guide, course comparison library, events, company headlines, boosters, 20 upgrade families, loans, profit calculations, statistic feedback and card animations are retained. Loan stacking remains disabled; the three-month cooldown starts after the previous debt is fully repaid. Singleplayer history/settings continue to save locally; multiplayer results are shown in the live standings.

WASD/arrows or 1-3 select cards; ENTER plays/takes/claims. R rerolls; E ends decisions or leaves optional cards. Shop: WASD/1-6 select, ENTER buys, R refreshes, SPACE finishes. L opens borrowing/repayment; P repays. I shows card information; click statistic bars for explanations. PgUp/PgDn page calculations or boosters. ESC pauses or returns to the previous panel. Multiplayer clocks continue while a player views information or pauses.

## Windows 11 fullscreen fix

On Windows 11, startup prefers a dedicated Windows Terminal window using its fullscreen command. It now verifies the actual visible game window against the monitor bounds instead of treating a hidden ConPTY console or launch flag as proof. Startup waits for the window to settle and has a monitor-sized fallback for that game window. Classic Alt+Enter fullscreen is also verified from actual bounds when display-mode flags are incomplete. Redirected IDE terminals can relaunch the game into an interactive host.

If Windows Terminal is unavailable or its launch fails, the game falls back to a dedicated classic console. StartClassicFullscreen.cmd forces that path. StartGame.cmd --windowed opts out of fullscreen. DiagnoseDisplay.cmd saves operating-system, monitor, DPI and console details in play/data/display-diagnostics.txt. On a small/high-DPI monitor, use Ctrl+- in Windows Terminal to fit at least 108 x 42 cells (singleplayer) or 140 x 42 (multiplayer); 172 x 46 is recommended for multiplayer. No persistent Terminal profile settings are edited.

## Validation

Release build: zero warnings/errors. Self-test: 7,652 checks, including all 180 course/topic/difficulty campaigns, real LAN TCP packets, real encrypted online host/client streams, wrong server-fingerprint rejection, invitation secrets and endpoint parsing, vote/revote timers, team-size choices, shop/decision barriers, loans, mouse voting, stale-input guards, disconnects and shared endless progression. Fullscreen and queued-input/palette tests passed on this Windows 10 machine. Rendered multiplayer screens were visually inspected at 140 x 42 and 172 x 46.

Internet routing between two physical computers and the Windows 11 GUI path need confirmation on those machines; local socket/TLS tests do not verify a router's port forwarding or a particular Windows 11 installation.

