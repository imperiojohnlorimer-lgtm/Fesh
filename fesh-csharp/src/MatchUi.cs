using Raylib_cs;

namespace Fesh;

// The Matches panel (1.30): pick a match, and read your local leaderboards. It opens from the Matches button in the HUD
// (next to the Fesh-dex) and from the Esc menu.
partial class Game
{
    string matchesTab = "play";                 // play or board
    string matchRival = "marlo";                // the Derby Duel's rival
    bool matchRaceBest;                         // the daily challenge against your best run today
    int lastMatchesBottom;

    void OpenMatches(string tab = "play")
    {
        if (mode == "panel") ClosePanels();
        if (mode != "play") return;
        Sfx.Play("ui");
        matchesTab = tab;
        panel = "matches";
        mode = "panel";
        SetPrompt("");
    }

    static int NewMatchSeed() => MatchRules.Mix(Environment.TickCount, (int)(DateTime.Now.Ticks & 0x7fffffff), 31);

    MatchSpec DerbySpec() => new()
    {
        Mode = "derby", Seed = NewMatchSeed(), Rival = matchRival,
        Label = $"Derby Duel · against {MatchRules.RivalById(matchRival).Name}"
    };

    MatchSpec DailySpec()
    {
        var today = DateTime.Now;
        int seed = MatchRules.DailySeed(today);
        bool race = matchRaceBest && Leaderboards.BestOn("daily", seed)?.log != null;
        return new MatchSpec { Mode = "daily", Seed = seed, Rival = race ? "best" : null, Label = $"Daily Challenge · {MatchRules.DailyLabel(today)}" };
    }

    static string WhenText(string iso) => DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var d) ? d.ToString("MMM d") : "";

    void DrawMatches()
    {
        var (px, py, pw, ph) = PanelFrame("Matches", "Short head-to-head runs. Both sides get the same seeded water or the same board, so skill decides.");
        if (PanelClose()) { ClosePanels(); return; }
        var board = Leaderboards.Board();

        // Tabs, and who you are on this computer's boards.
        float ty = py + 96, tx = px + PanelPad;
        foreach (var (id, label) in new[] { ("play", "Play"), ("dock", "Tournament Dock"), ("board", "Leaderboards") })
        {
            float bw = SmallW(label);
            if (Button(label, tx, ty, bw, 44, FontKind.Ui700, 20, matchesTab == id ? Pal.Lantern : Pal.Sand, Pal.Ink, 4)) matchesTab = id;
            tx += bw + 10;
        }
        // Your name for matches: pick each word from its list (there is no typing a name), or let chance choose.
        float nx = px + pw - PanelPad - SmallW("Random");
        if (SmallButton("Random", nx, ty)) { var rn = new Random(); Leaderboards.SetName(rn.Next(1000), rn.Next(1000)); Sfx.Play("ui"); }
        float pickW = 150;
        nx -= 12 + 40 + pickW + 40;
        if (NamePicker("noun", nx, ty, pickW, Leaderboards.NounAt(board.noun), d => Leaderboards.SetName(board.adj, board.noun + d))) { }
        nx -= 14 + 40 + pickW + 40;
        NamePicker("adj", nx, ty, pickW, Leaderboards.AdjectiveAt(board.adj), d => Leaderboards.SetName(board.adj + d, board.noun));
        Gfx.Text("Your name", nx - 12 - Gfx.Measure("Your name", FontKind.Ui600, 18), ty + 11, FontKind.Ui600, 18, Muted);

        float top = ty + 62, gap = 20;
        float cw = (pw - PanelPad * 2 - gap * 2) / 3, ch = ph - (top - py) - PanelPad;
        lastMatchesBottom = (int)(top + ch);
        float c0 = px + PanelPad, c1 = c0 + cw + gap, c2 = c1 + cw + gap;
        if (matchesTab == "dock") DrawDockCard(c0, top, pw - PanelPad * 2, ch);
        else if (matchesTab == "play")
        {
            DrawDerbyCard(c0, top, cw, ch);
            DrawDailyCard(c1, top, cw, ch);
            DrawSungkaCard(c2, top, cw, ch);
        }
        else
        {
            DrawBoardPages(c0, top, pw - PanelPad * 2, ch);
        }
    }

    // Your best times from the adventure itself, in one strip under the boards.
    void DrawTimeAttacks(float x, float y, float w)
    {
        var rs = MatchActive ? match.Real : state;
        Gfx.Box(x, y, w, 62, CardBg, Pal.C("rgba(16,36,58,0.35)"), 2, 6);
        Gfx.Text("From your adventure", x + 20, y + 8, FontKind.Ui700, 17, Muted);
        var items = new[]
        {
            ("Regatta, fastest", rs.regattaBest > 0 ? RaceTime(rs.regattaBest) : "not raced yet"),
            ("Crabs sorted right in a row", rs.sortBest > 0 ? rs.sortBest.ToString() : "none yet"),
            ("Platejaw, fight won in", rs.platejawBest > 0 ? RaceTime(rs.platejawBest) : "not beaten yet")
        };
        float cw = (w - 40) / 3;
        for (int i = 0; i < items.Length; i++)
        {
            Gfx.Text(items[i].Item1, x + 20 + i * cw + (i == 0 ? 0 : 0), y + 30, FontKind.Ui500, 15, Muted);
            string v = items[i].Item2;
            Gfx.Text(v, x + 20 + i * cw + cw - 20 - Gfx.Measure(v, FontKind.Ui700, 17), y + 29, FontKind.Ui700, 17, Pal.PaperInk);
        }
    }

    // A word with an arrow each side. Returns true if an arrow was pressed.
    bool NamePicker(string key, float x, float y, float w, string word, Action<int> change)
    {
        bool hit = false;
        for (int side = 0; side < 2; side++)
        {
            float bx = side == 0 ? x : x + 40 + w;
            if (Button("", bx, y, 40, 44, FontKind.Ui700, 20, Pal.Sand, Pal.Ink, 4))
            {
                change(side == 0 ? -1 : 1); Sfx.Play("ui"); hit = true;
            }
            float cx = bx + 20, cy = y + 22, d = side == 0 ? -1 : 1;
            Gfx.Triangle(cx + 5 * d, cy, cx - 4 * d, cy - 8, cx - 4 * d, cy + 8, Pal.Ink);
#if DEBUG
            Gfx.Seen[$"name:{key}:{(side == 0 ? "prev" : "next")}"] = new Rectangle(bx, y, 40, 44);
#endif
        }
        Gfx.Box(x + 40, y, w, 44, CardBg, Pal.Ink, 3, 6);
        Gfx.TextCenter(word, x + 40 + w / 2, y + 10, FontKind.Ui700, 20, Pal.PaperInk);
        return hit;
    }

    void MatchCard(float x, float y, float w, float h, string title, string sub)
    {
        Gfx.Box(x, y, w, h, CardBg, Pal.C("rgba(16,36,58,0.35)"), 2, 6);
        Gfx.Text(title, x + 20, y + 16, FontKind.Ui700, 28, Pal.PaperInk);
        Gfx.Text(Gfx.Ellipsize(sub, FontKind.Ui600, 16, w - 40), x + 20, y + 52, FontKind.Ui600, 16, Muted);
    }

    void DrawDerbyCard(float x, float y, float w, float h)
    {
        MatchCard(x, y, w, h, "Derby Duel", "3 minutes · the lagoon · Old rod");
        float iy = y + 84;
        var text = Gfx.Wrap("You and a rival fish the same water: the nth cast of each of you meets the same fish at the same size. Heaviest total wins; a tie goes to more perfect hooks.", FontKind.Note, 17, w - 40);
        Lines(text, x + 20, iy, 23, FontKind.Note, 17, Pal.PaperInk);
        iy += text.Count * 23 + 10;
        Gfx.Text("Choose your rival", x + 20, iy, FontKind.Ui700, 17, Muted);
        iy += 28;
        foreach (var r in MatchRules.Rivals)
        {
            bool on = matchRival == r.Id;
            float rh = 46;
            var fill = on ? Pal.C("#fff0bd") : Pal.C("#f6ecd2");
            if (Gfx.Hover(x + 20, iy, w - 40, rh)) fill = Lighten(fill, 0.25f);
            Gfx.Box(x + 20, iy, w - 40, rh, fill, on ? Pal.Ink : Pal.C("rgba(16,36,58,0.35)"), on ? 3 : 2, 5);
            Gfx.Text(r.Name, x + 34, iy + 5, FontKind.Ui700, 18, Pal.PaperInk);
            Gfx.Text(Gfx.Ellipsize(r.Blurb, FontKind.Ui500, 14, w - 40 - 28), x + 34, iy + 26, FontKind.Ui500, 14, Muted);
#if DEBUG
            Gfx.Seen[$"rival:{r.Id}"] = new Rectangle(x + 20, iy, w - 40, rh);
#endif
            if (Gfx.Click(x + 20, iy, w - 40, rh)) { matchRival = r.Id; Sfx.Play("ui"); }
            iy += rh + 6;
        }
        var (played, won, lost, drawn) = Leaderboards.Record("derby");
        string why = MatchBlockReason();
        if (BigButtonLive("Start the duel", x + 20, y + h - 92, why == null) && why == null) { BeginMatch(DerbySpec()); return; }
        // Under the button: why not (if not), else how your duels have gone.
        string line = why ?? (played == 0 ? "No duels yet." : $"Won {won}, lost {lost}" + (drawn > 0 ? $", drawn {drawn}" : "") + ".");
        Gfx.Text(Gfx.Ellipsize(line, FontKind.Note, 15, w - 40), x + 20, y + h - 30, FontKind.Note, 15, why != null ? Rust : Muted);
    }

    void DrawDailyCard(float x, float y, float w, float h)
    {
        var today = DateTime.Now;
        int seed = MatchRules.DailySeed(today);
        MatchCard(x, y, w, h, "Daily Challenge", $"{MatchRules.DailyLabel(today)} · 3 minutes");
        float iy = y + 84;
        var text = Gfx.Wrap("One seeded run a day: the same fish in the same order for everyone on this date. Play as often as you like; your best counts. Race your best run to beat it.", FontKind.Note, 17, w - 40);
        Lines(text, x + 20, iy, 23, FontKind.Note, 17, Pal.PaperInk);
        iy += text.Count * 23 + 14;
        var best = Leaderboards.BestOn("daily", seed);
        Gfx.Text("Today's best", x + 20, iy, FontKind.Ui700, 17, Muted);
        iy += 28;
        if (best == null) Gfx.Text("Not played yet today.", x + 20, iy, FontKind.Ui600, 20, Pal.PaperInk);
        else
        {
            Gfx.Text($"{best.score / 100.0:0.00} kg", x + 20, iy - 4, FontKind.Ui700, 36, Pal.PaperInk);
            Gfx.Text($"{best.catches} fish · {best.perfects} perfect hooks", x + 20, iy + 38, FontKind.Ui500, 16, Muted);
        }
        iy += 74;
        bool canRace = best?.log != null;
        if (canRace)
        {
            string label = matchRaceBest ? "Racing your best run" : "Race your best run";
            if (Button(label, x + 20, iy, SmallW(label), 44, FontKind.Ui700, 18, matchRaceBest ? Pal.Lantern : Pal.Sand, Pal.Ink, 4)) matchRaceBest = !matchRaceBest;
        }
        else matchRaceBest = false;
        string why = MatchBlockReason();
        string go = best == null ? "Play today's run" : "Play it again";
        if (BigButtonLive(go, x + 20, y + h - 92, why == null) && why == null) { BeginMatch(DailySpec()); return; }
        if (why != null) Gfx.Text(Gfx.Ellipsize(why, FontKind.Note, 15, w - 40), x + 20, y + h - 30, FontKind.Note, 15, Rust);
    }

    // The Tournament Dock tab: where matches begin (Dock.cs, DockGame.cs). Visit it alone, host it for others on your
    // network, or find one somebody else is hosting.
    void DrawDockCard(float x, float y, float w, float h)
    {
        MatchCard(x, y, w, h, "Tournament Dock", "Walk the beach, meet the anglers, start a match from there");
        string why = MatchBlockReason();
        if (dockView == "finding") { DrawDockFinding(x, y, w, h, why); return; }
        var text = Gfx.Wrap("A little sand islet of its own, out in the open sea south of Saltmere, with a notice board and three anglers waiting for a Derby Duel: Old Bess, Marlo and Captain Ivy. Visit it on your own, or host it and let other people on your network walk in: you can see each other, say a quick phrase (number keys) and challenge each other to sungka. Nothing on the dock touches your adventure, and when a match is over you are back on the dock.", FontKind.Note, 19, w - 40);
        Lines(text, x + 20, y + 92, 27, FontKind.Note, 19, Pal.PaperInk);
        float by = y + 92 + text.Count * 27 + 30;
        if (BigButtonLive("Visit alone", x + 20, by, why == null) && why == null) { BeginDock(); return; }
        float bx = x + 20 + BigW("Visit alone") + 14;
        if (BigButtonLive("Host a dock", bx, by, why == null) && why == null) { StartDockHost(); return; }
        bx += BigW("Host a dock") + 14;
        if (BigButtonLive("Find a dock", bx, by, why == null) && why == null) { StartDockFind(); return; }
        string line = why ?? dockMsg;
        if (line != "") Gfx.Text(Gfx.Ellipsize(line, FontKind.Note, 16, w - 40), x + 20, by + 70, FontKind.Note, 16, Rust);
        var tip = Gfx.Wrap("The first time, Windows may ask whether Fesh can use the network: choose Private networks.", FontKind.Note, 17, w - 40);
        Lines(tip, x + 20, y + h - 20 - tip.Count * 24, 24, FontKind.Note, 17, Muted);
    }

    void DrawDockFinding(float x, float y, float w, float h, string why)
    {
        Gfx.Text("Docks on your network", x + 20, y + 90, FontKind.Ui700, 22, Pal.PaperInk);
        float ry = y + 128;
        var docks = dockBrowser?.Hosts.Where(f => f.Game == "dock").ToList() ?? new();
        if (docks.Count == 0) Gfx.Text(dockConnect != null ? "" : "Looking... (the other player chooses Host a dock)", x + 20, ry + 6, FontKind.Note, 19, Muted);
        for (int i = 0; i < Math.Min(3, docks.Count); i++)
        {
            var f = docks[i];
            Gfx.Box(x + 20, ry, w - 40, 56, CardBg, Pal.C("rgba(16,36,58,0.35)"), 2, 5);
            Gfx.Text(Gfx.Ellipsize(f.Name, FontKind.Ui700, 20, 420), x + 36, ry + 8, FontKind.Ui700, 20, Pal.PaperInk);
            Gfx.Text($"{f.Ip}:{f.Port}", x + 36, ry + 33, FontKind.Ui500, 15, Muted);
            bool live = f.Open && dockConnect == null && why == null;
            if (Button("Join", x + w - 36 - SmallW("Join"), ry + 6, SmallW("Join"), 44, FontKind.Ui700, 20, live ? Pal.Buoy : Pal.C("#d8cbb0"), live ? White : Muted, 4, 3, 6, live)) { JoinDock(f.Ip, f.Port); return; }
            ry += 64;
        }
        float ay = y + 128 + 3 * 64 + 12;
        Gfx.Text("Or type their address (they can read it on their screen):", x + 20, ay, FontKind.Ui600, 18, Muted);
        float fy = ay + 32, fw = 340;
        Gfx.Box(x + 20, fy, fw, 48, CardBg, Pal.Ink, 2, 5);
        Gfx.Text(dockAddr + ((int)(time * 2) % 2 == 0 && dockConnect == null ? "|" : ""), x + 34, fy + 9, FontKind.Ui700, 26, Pal.PaperInk);
        if (Button("Connect", x + 20 + fw + 14, fy + 2, SmallW("Connect"), 44, FontKind.Ui700, 20, Pal.Buoy, White, 4, 3, 6, dockAddr.Length > 0 && dockConnect == null && why == null))
        {
            if (Lan.TryParse(dockAddr, out var ip, out int port)) JoinDock(ip, port);
            else { dockMsg = "That doesn't look like an address. Try something like 192.168.1.20"; Sfx.Play("nope"); }
            return;
        }
        string status = dockConnect != null ? "Connecting..." : why ?? dockMsg;
        if (status != "") Gfx.Text(Gfx.Ellipsize(status, FontKind.Ui600, 18, w - 40), x + 20, fy + 62, FontKind.Ui600, 18, dockConnect != null ? Muted : Rust);
        if (BigButton("Back", x + w - 20 - BigW("Back"), y + h - 76, false)) { dockBrowser?.Dispose(); dockBrowser = null; dockConnect?.Cancel(); dockConnect = null; dockView = "menu"; dockMsg = ""; return; }
    }

    void DrawSungkaCard(float x, float y, float w, float h)
    {
        MatchCard(x, y, w, h, "Sungka (LAN)", "2 players · 5 minutes at most");
        float iy = y + 84;
        var text = Gfx.Wrap("Play sungka against someone on the same Wi-Fi or network. One of you hosts and the other joins. Twenty seconds a move; if you leave, they win. Nothing here touches your adventure.", FontKind.Note, 17, w - 40);
        Lines(text, x + 20, iy, 23, FontKind.Note, 17, Pal.PaperInk);
        var (played, won, lost, drawn) = Leaderboards.Record("sungka");
        Gfx.Text(played == 0 ? "No games yet." : $"Won {won}, lost {lost}" + (drawn > 0 ? $", drawn {drawn}" : "") + ".", x + 20, iy + text.Count * 23 + 12, FontKind.Ui600, 17, Muted);
        if (BigButton("Find a game", x + 20, y + h - 92, false)) { OpenSungkaLan("find"); return; }
        if (BigButton("Host a game", x + 20, y + h - 92 - 66, true)) { OpenSungkaLan("host"); return; }
    }

    // BigButton that can be dimmed and refuse clicks.
    static bool BigButtonLive(string label, float x, float y, bool live) =>
        Button(label, x, y, BigW(label), 56, FontKind.Ui700, 24, live ? Pal.Buoy : Pal.C("#d8cbb0"), live ? White : Muted, 5, 3, 6, live);

    // One score as the board shows it.
    static string BoardScore(string mode, BoardEntry e) => mode switch
    {
        "sungka" => $"{e.score} shells",
        "regatta" => e.score > 0 ? HeatTime(e.score / 1000f) : "no time",
        "quiz" => $"{QuizRules.Right(e.score)} of {QuizRules.Count} right",
        "spear" => $"{e.score} points",
        _ => $"{e.score / 100.0:0.00} kg"
    };

    void DrawBoardColumn(float x, float y, float w, float h, string mode, string title)
    {
        MatchCard(x, y, w, h, title, "Kept on this computer only");
        var (played, won, lost, drawn) = Leaderboards.Record(mode);
        float iy = y + 84;
        Gfx.Text("Best scores", x + 20, iy, FontKind.Ui700, 17, Muted);
        if (played > 0)
        {
            string rec = $"{won}-{lost}" + (drawn > 0 ? $"-{drawn}" : "");
            Gfx.Text(rec, x + w - 20 - Gfx.Measure(rec, FontKind.Ui700, 17), iy, FontKind.Ui700, 17, Pal.PaperInk);
        }
        iy += 28;
        int bestN = h >= 480 ? 6 : 5, recentN = h >= 480 ? 5 : 4;
        var best = Leaderboards.Best(mode, bestN);
        if (best.Count == 0) Gfx.Text("Nothing here yet.", x + 20, iy, FontKind.Note, 17, Muted);
        for (int i = 0; i < best.Count; i++)
        {
            var e = best[i];
            float ry = iy + i * 28;
            if (i % 2 == 0) Gfx.Rect(x + 10, ry - 3, w - 20, 27, Pal.C("rgba(16,36,58,0.06)"), 4);
            Gfx.Text($"{i + 1}", x + 20, ry, FontKind.Ui700, 17, Pal.PaperInk);
            Gfx.Text(BoardScore(mode, e), x + 46, ry, FontKind.Ui700, 16, Pal.PaperInk);
            string mid = mode is "sungka" or "regatta" or "quiz" ? (e.rival != null ? $"vs {e.rival}" : "") : e.perfects > 0 ? $"{e.perfects} perfect" : "";
            string right = WhenText(e.date);
            Gfx.Text(Gfx.Ellipsize(mid, FontKind.Ui500, 14, w - 150 - 20 - Gfx.Measure(right, FontKind.Ui500, 14) - 8), x + 150, ry + 1, FontKind.Ui500, 14, Muted);
            Gfx.Text(right, x + w - 20 - Gfx.Measure(right, FontKind.Ui500, 14), ry + 1, FontKind.Ui500, 14, Muted);
        }
        iy += bestN * 28 + 14;
        Gfx.Text("Recent matches", x + 20, iy, FontKind.Ui700, 17, Muted);
        iy += 28;
        var recent = Leaderboards.Recent(mode, recentN);
        if (recent.Count == 0) Gfx.Text("No matches yet.", x + 20, iy, FontKind.Note, 17, Muted);
        for (int i = 0; i < recent.Count; i++)
        {
            var e = recent[i];
            float ry = iy + i * 28;
            string res = e.result switch { "win" => "Won", "loss" => "Lost", "draw" => "Drew", "forfeit" => "Left", _ => "Played" };
            string line = e.rival != null ? $"{res} vs {e.rival}" : res;
            string right = mode is "sungka" or "regatta" ? $"{(mode == "regatta" ? e.casts : e.score)}-{e.catches}" : BoardScore(mode, e);
            Gfx.Text(right, x + w - 20 - Gfx.Measure(right, FontKind.Ui500, 15), ry + 1, FontKind.Ui500, 15, Muted);
            Gfx.Text(Gfx.Ellipsize(line, FontKind.Ui600, 16, w - 40 - Gfx.Measure(right, FontKind.Ui500, 15) - 12), x + 20, ry, FontKind.Ui600, 16, e.result == "win" ? Pal.C("#2f7d3a") : e.result == "loss" ? Rust : Pal.PaperInk);
        }
    }
}

partial class Game
{
    int boardPage;                              // 0: Derby, Daily, Sungka   1: Regatta, Sea School Duel, Spearfishing

    // The leaderboards, three at a time, with the adventure's own times under them and a button for the other page.
    void DrawBoardPages(float x, float top, float w, float ch)
    {
        const float gap = 20;
        float cw = (w - gap * 2) / 3;
        float c1 = x + cw + gap, c2 = c1 + cw + gap;
        if (boardPage == 0)
        {
            DrawBoardColumn(x, top, cw, ch - 72, "derby", "Derby Duel");
            DrawBoardColumn(c1, top, cw, ch - 72, "daily", "Daily Challenge");
            DrawBoardColumn(c2, top, cw, ch - 72, "sungka", "Sungka (LAN)");
        }
        else
        {
            DrawBoardColumn(x, top, cw, ch - 72, "regatta", "Regatta");
            DrawBoardColumn(c1, top, cw, ch - 72, "quiz", "Sea School Duel");
            DrawBoardColumn(c2, top, cw, ch - 72, "spear", "Spearfishing");
        }
        string label = boardPage == 0 ? "More boards" : "First boards";
        float bw = BigW(label);
        DrawTimeAttacks(x, top + ch - 62, w - bw - 14);
        if (BigButton(label, x + w - bw, top + ch - 59, false)) { boardPage = 1 - boardPage; Sfx.Play("ui"); }
    }
}
