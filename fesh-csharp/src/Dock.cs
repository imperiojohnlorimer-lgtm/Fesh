using Raylib_cs;

namespace Fesh;

// The Tournament Dock (1.30): the waterfront south of Saltmere's lagoon, where matches begin. Like a match it is a phase
// of the MatchRun sandbox (Match.cs): your adventure is stashed, a tournament State is swapped in, Save does nothing. You
// walk about a small stretch of beach, read the notice board (the leaderboards), talk to the three anglers standing
// there (Old Bess, Marlo, Captain Ivy: E starts a Derby Duel against them, so every mode plays alone) and say a quick
// phrase with the number keys. A match started here ends back on the dock; Leave goes back to the island.
partial class Game
{
    const float DockX0 = DockRules.X0, DockX1 = DockRules.X1, DockY0 = DockRules.Y0, DockY1 = DockRules.Y1;     // the stretch you may walk
    const float DockSpawnX = DockRules.SpawnX, DockSpawnY = DockRules.SpawnY;
    const float DockBoardX = 321, DockBoardY = 194;
    static readonly (string id, string name, float x, float y)[] DockAnglers =
    {
        ("bess", "Old Bess", 266, 198), ("marlo", "Marlo", 296, 193), ("ivy", "Captain Ivy", 310, 205)
    };
    // The quick phrases (number keys 1-8): the only talking there is.
    public static string[] Emotes => DockRules.Emotes;

    bool InDock => MatchActive && match.Phase == "dock";

    // The dock has a place of its own: a small sand islet stamped into open sea south of Saltmere while the sandbox lasts (the
    // world is rebuilt without it when you leave), so it never crowds the island. The spot was chosen because all of
    // DockSiteX0..1, DockSiteY0..1 is open water (DockScript checks that stays true).
    bool stampDock;
    public const int DockSiteX0 = 19, DockSiteX1 = 37, DockSiteY0 = 15, DockSiteY1 = 25;

    void StampDock()
    {
        for (int ty = DockSiteY0; ty <= DockSiteY1; ty++)
            for (int tx = DockSiteX0; tx <= DockSiteX1; tx++)
            {
                double dx = (tx + 0.5 - 28.5) / 7.4, dy = (ty + 0.5 - 19.5) / 3.7, e = dx * dx + dy * dy;
                if (e < 0.5) map[ty, tx] = 'g';
                else if (e < 1) map[ty, tx] = 's';
                else if (e < 1.9 && map[ty, tx] == '~') map[ty, tx] = 'w';
            }
        foreach (var (tx, ty) in new[] { (23, 19), (34, 19), (25, 16), (32, 16) }) { map[ty, tx] = 't'; trees.Add((tx, ty, 't')); }
        for (int ty = 24; ty <= 25; ty++) map[ty, 28] = 'b';
    }

    void PlaceAtDock()
    {
        player.X = DockSpawnX; player.Y = DockSpawnY; player.Face = "up"; player.Moving = false;
        match.LeaveAsk = false; match.BoardOpen = false; match.Emote = "";
    }

    void BeginDock() => BeginMatch(new MatchSpec { Mode = "dock", Label = "Tournament Dock" });

    /* ---------- Between the dock and a match ---------- */
    // A Derby Duel from the dock: the same sandbox, a fresh set of belongings and a new match.
    void StartDockDuel(string rivalId)
    {
        if (!InDock) return;
        var sk = MatchRules.RivalById(rivalId);
        var spec = new MatchSpec { Mode = "derby", Seed = NewMatchSeed(), Rival = sk.Id, Label = $"Derby Duel · against {sk.Name}" };
        int g = ++match.Gen;
        match.Phase = "ready";
        FadeThrough(() => { if (match != null && match.Gen == g) EnterDockChild(spec); },
            () => { if (match == null || match.Gen != g) return; match.Phase = "countdown"; match.Count = MatchRules.CountdownSecs; Sfx.Play("blip"); });
    }

    void ResetMatchCounters(MatchSpec spec)
    {
        var m = match;
        m.Spec = spec;
        m.RivalEvt = 0;
        m.Me = default; m.Log.Clear(); m.Casts = 0; m.Lost = 0; m.T = 0; m.ReelAcc = 0; m.LeaveAsk = false; m.BoardOpen = false; m.Result = null;
        (m.RivalRun, m.RivalName) = MatchRival(spec);
        state = MatchState(m.Real.look);
        EnsureHotbar();
        rng = new Random(MatchRules.Mix(MatchRules.Version, spec.Seed, 9));
    }

    void EnterDockChild(MatchSpec spec)
    {
        match.FromDock = true;
        ResetMatchCounters(spec);
        ResetMatchPlace();
    }

    // Back to the dock after a match (and leaving a match that began there).
    void ReturnToDock(Action after = null)
    {
        int g = ++match.Gen;
        FadeThrough(() => { if (match == null || match.Gen != g) return; match.FromDock = false; ResetMatchCounters(new MatchSpec { Mode = "dock", Label = "Tournament Dock" }); ResetMatchPlace(); PlaceAtDock(); match.Phase = "ready"; },
            () => { if (match == null || match.Gen != g) return; match.Phase = "dock"; after?.Invoke(); });
    }

    // The result card's Leave: out of the dock altogether (the island is restored once, and Pip's prizes paid).
    void LeaveDockFromResult()
    {
        match.FromDock = false;
        LeaveMatch();
    }

    /* ---------- On the dock ---------- */
    void UpdateDock(float dt)
    {
        player.Moving = false;
        bool free = !match.LeaveAsk && !match.BoardOpen;
        if (free)
        {
            float dx = 0, dy = 0;
            if (Bind.Down("left")) dx -= 1;
            if (Bind.Down("right")) dx += 1;
            if (Bind.Down("up")) dy -= 1;
            if (Bind.Down("down")) dy += 1;
            float push = 1;
            if (Inp.Stick != System.Numerics.Vector2.Zero)
            {
                (dx, dy) = (Inp.Stick.X, Inp.Stick.Y);
                push = Math.Clamp(0.35f + 0.65f * (Inp.Stick.Length() - 0.35f) / 0.5f, 0.35f, 1);
            }
            if (dx != 0 || dy != 0)
            {
                float len = MathF.Sqrt(dx * dx + dy * dy), sp = 52 * push * dt;
                float nx = Math.Clamp(player.X + dx / len * sp, DockX0, DockX1), ny = Math.Clamp(player.Y + dy / len * sp, DockY0, DockY1);
                if (CanStand(nx, player.Y, false) && !DockBlocked(nx, player.Y)) player.X = nx;
                if (CanStand(player.X, ny, false) && !DockBlocked(player.X, ny)) player.Y = ny;
                player.Face = MathF.Abs(dx) > MathF.Abs(dy) ? (dx > 0 ? "right" : "left") : (dy > 0 ? "down" : "up");
                player.WalkT += dt * push;
                player.Moving = true;
            }
        }
        int face = player.Face switch { "up" => 0, "right" => 1, "down" => 2, _ => 3 };
        dockClient?.SetPose(player.X, player.Y, face, player.Moving ? 1 + (int)(player.WalkT * 9) % 4 : 0);
        target = free && !DockBusy ? DockTarget() : null;
        SetPrompt(target?.Label ?? "", target != null ? "<act>" : null);
    }

    // The anglers and the board are solid.
    static bool DockBlocked(float x, float y)
    {
        if (Dist(x, y, DockBoardX, DockBoardY) < 6) return true;
        foreach (var a in DockAnglers) if (Dist(x, y, a.x, a.y) < 6) return true;
        return false;
    }

    Target DockTarget()
    {
        // Whoever or whatever is nearest within reach: an angler, another player or the board.
        Target best = null; float bd = 999;
        void Offer(float d, Target t) { if (d < bd) { bd = d; best = t; } }
        foreach (var a in DockAnglers)
        {
            float d = Dist(player.X, player.Y, a.x, a.y);
            if (d < 17) Offer(d, new Target { Type = "dockrival", Id = a.id, Label = $"Challenge {a.name}" });
        }
        if (dockClient != null)
            foreach (var p in dockClient.Others.Where(p => p.State == 0))
            {
                float d = Dist(player.X, player.Y, p.X, p.Y);
                if (d < 17) Offer(d, new Target { Type = "dockplayer", Id = p.Id.ToString(), Label = $"Challenge {p.Name}" });
            }
        float bdist = Dist(player.X, player.Y, DockBoardX, DockBoardY);
        if (bdist < 18) Offer(bdist, new Target { Type = "dockboard", Label = "Read the notice board" });
        return best;
    }

    void DockKeys()
    {
        if (dockInvite != null) { if (BackPressed()) AnswerInvite(false); return; }
        if (dockChallengeTarget >= 0) { if (BackPressed()) dockChallengeTarget = -1; return; }
        if (dockAnglerMenu != null) { if (BackPressed()) dockAnglerMenu = null; return; }
        if (Bind.Pressed("act") || Inp.Pressed(KeyboardKey.Enter)) OnAction();
        else if (BackPressed()) match.LeaveAsk = true;
        else if (DigitPressed() is int d && d < Emotes.Length) SendEmote(d);
    }

    void SendEmote(int d)
    {
        dockClient?.Emote(d);
        match.Emote = Emotes[d];
        match.EmoteT = time + 3;
        Sfx.Play("blip");
    }

    /* ---------- Drawing ---------- */
    void AddDockObjects(List<(float y, Action draw)> list)
    {
        if (!MatchActive || !match.Dock || match.FromDock) return;
        AddDockPlayers(list);
        list.Add((DockBoardY + 4, DrawDockBoard));
        list.Add((DockY0 - 6, DrawDockBunting));
        foreach (var a in DockAnglers)
        {
            var an = a;
            list.Add((an.y, () =>
            {
                int bob = time % 2.6f > 1.7f ? 1 : 0;
                string face = player.X < an.x ? "left" : "right";
                DrawRivalFigure(an.id, (int)an.x, (int)an.y, face, 0, 0, bob, time % 4.3f < 0.12f);
            }));
        }
    }

    void DrawDockBoard()
    {
        int x = (int)DockBoardX, y = (int)DockBoardY;
        pix.Rect(x - 8, y - 2, 16, 3, "rgba(0,0,0,0.18)");
        pix.Rect(x - 7, y - 12, 2, 12, "#6b4a2b"); pix.Rect(x + 5, y - 12, 2, 12, "#6b4a2b");
        pix.Rect(x - 8, y - 20, 16, 10, "#8a5f36"); pix.Rect(x - 7, y - 19, 14, 8, "#b98a55");
        pix.Rect(x - 6, y - 18, 4, 4, "#fdf3c4"); pix.Rect(x - 1, y - 18, 4, 5, "#fffaf0"); pix.Rect(x + 3, y - 17, 3, 4, "#fdf3c4");
        pix.Rect(x - 5, y - 17, 2, 1, "#c0392b"); pix.Rect(x, y - 17, 2, 1, "#2b6fa8");
        pix.Rect(x, y - 26, 1, 6, "#6b4a2b"); pix.Rect(x + 1, y - 26, 5, 3, "#e04b3a"); pix.Rect(x + 1 + (int)(time * 3) % 2, y - 23, 3, 1, "#e04b3a");
    }

    // Bunting along the top of the beach.
    void DrawDockBunting()
    {
        string[] cols = { "#e04b3a", "#f3c25b", "#3f7fa8", "#7fd36b" };
        for (int i = 0; i <= 14; i++)
        {
            float k = i / 14f, bx = DockX0 + (DockX1 - DockX0) * k, by = DockY0 - 12 + MathF.Sin(MathF.PI * k) * 4;
            pix.Rect(bx, by, 1, 1, "#e8dcc0");
            pix.Rect(bx - 1, by + 1, 3, 2, cols[i % 4]);
            pix.Rect(bx, by + 3, 1, 1, cols[i % 4]);
        }
        pix.Rect(DockX0 - 1, DockY0 - 14, 2, 14, "#6b4a2b");
        pix.Rect(DockX1 - 1, DockY0 - 14, 2, 14, "#6b4a2b");
    }

    // Names over the anglers, and what you just said over yourself.
    void DrawDockTags()
    {
        if (!InDock || match.BoardOpen) return;
        DrawDockPlayerTags();
        foreach (var a in DockAnglers)
        {
            var p = ScreenOf(a.x, a.y - 30);
            float w = Gfx.Measure(a.name, FontKind.Ui600, 16) + 16;
            Gfx.Rect(p.X - w / 2, p.Y - 12, w, 24, NavyStrong, 5);
            Gfx.TextCenter(a.name, p.X, p.Y - 9, FontKind.Ui600, 16, Pal.Paper);
        }
        if (time < match.EmoteT && match.Emote != "")
        {
            var p = ScreenOf(player.X, player.Y - 34);
            float w = Gfx.Measure(match.Emote, FontKind.Ui600, 18) + 24, a = Math.Clamp((match.EmoteT - time) / 0.4f, 0, 1);
            Gfx.Box(p.X - w / 2, p.Y - 36, w, 34, Pal.WithAlpha(Pal.Paper, a), Pal.WithAlpha(Pal.Ink, a), 2, 7);
            Gfx.TextCenter(match.Emote, p.X, p.Y - 31, FontKind.Ui600, 18, Pal.WithAlpha(Pal.PaperInk, a));
        }
    }

    void DrawDockHud()
    {
        const float w = 400;
        float x = 14, y = 14;
        var lines = new List<string> { "Walk about and talk to the anglers (<act>) for a Derby Duel, or read the notice board." };
        var wrapped = lines.SelectMany(l => Gfx.Wrap(Bind.Fix(l), FontKind.Ui500, 16, w - 28)).ToList();
        float rosterH = dockClient != null ? 30 + Math.Min(8, dockClient.Roster.Count) * 20 : 0;
        bool hosting = dockListener != null && dockHostAddr.Length > 0;
        float addrH = hosting ? 50 : 0;
        float h = 52 + wrapped.Count * 22 + 34 + rosterH + addrH;
        Gfx.Box(x, y, w, h, NavyStrong, Pal.Ink, 2, 8, 3);
        Gfx.Text("Tournament Dock", x + 14, y + 10, FontKind.Ui700, 24, White);
        for (int i = 0; i < wrapped.Count; i++) Gfx.Text(wrapped[i], x + 14, y + 44 + i * 22, FontKind.Ui500, 16, Pal.C("#bcd3e0"));
        float ry = y + 44 + wrapped.Count * 22 + 8;
        if (hosting)
        {
            Gfx.Text("Others find this dock with Find a dock, or type:", x + 14, ry, FontKind.Ui500, 15, Pal.C("#bcd3e0"));
            Gfx.Text(dockHostAddr, x + 14, ry + 20, FontKind.Ui700, 22, Pal.Lantern);
            ry += addrH;
        }
        if (dockClient != null) DrawDockRoster(x + 14, ry, w - 28);
        Gfx.Text(Bind.Fix("Keys 1-8: say hello. Esc: leave the dock."), x + 14, y + h - 28, FontKind.Ui500, 15, Pal.C("#e9d79a"));
        // The phrases, along the bottom of the screen.
        float bw = 118, bx = (Gfx.LW - (bw * 4 + 8 * 3)) / 2, by = Gfx.LH - 150;
        for (int i = 0; i < Emotes.Length; i++)
        {
            float ex = bx + i % 4 * (bw + 8), ey = by + i / 4 * 34;
            if (match.LeaveAsk || match.BoardOpen) break;
            Gfx.Rect(ex, ey, bw, 30, Pal.C("rgba(16,36,58,0.72)"), 5);
            Gfx.Text($"{i + 1}", ex + 8, ey + 5, FontKind.Ui700, 16, Pal.Lantern);
            Gfx.Text(Gfx.Ellipsize(Emotes[i], FontKind.Ui500, 14, bw - 30), ex + 26, ey + 7, FontKind.Ui500, 14, Pal.Paper);
#if DEBUG
            Gfx.Seen[$"emote:{i}"] = new Rectangle(ex, ey, bw, 30);
#endif
            if (Gfx.Click(ex, ey, bw, 30)) SendEmote(i);
        }
    }

    // The notice board: the leaderboards, for all three modes.
    void DrawDockNoticeBoard()
    {
        var (px, py, pw, ph) = PanelFrame("Notice board", "Your best scores and recent matches, kept on this computer.");
        if (PanelClose()) { match.BoardOpen = false; return; }
        float top = py + 96, gap = 20;
        float ch = ph - (top - py) - PanelPad;
        DrawBoardPages(px + PanelPad, top, pw - PanelPad * 2, ch);
    }
}
