namespace Fesh;

// The rival you fish against in a Derby Duel (1.30): a person on the bank beside you, fishing the same lagoon. What they do
// is played from their run (MatchRules.GhostRun): they cast, wait, fight a fish for the few seconds before it is landed,
// hold it up when it comes in, and "+x kg" rises over them. Looks only: their score is the run, not what you see.
partial class Game
{
    const float RivalX = 127, RivalY = 77;          // on the grass at the lagoon's east shore
    const float RivalBobX = 112, RivalBobY = 72;     // their bobber, in the lagoon
    const float RivalFightSecs = 6, RivalHoldSecs = 1.6f, RivalCycle = 9;

    void AddMatchObjects(List<(float y, Action draw)> list)
    {
        if (MatchActive && match.RivalRun != null && match.Phase is "countdown" or "run" or "done" or "ready" or "netwait" or "waitend")
            list.Add((RivalY, DrawMatchRival));
    }

    // Each frame of a run: a landed fish is announced over the rival as it comes in.
    void UpdateRivalEvents()
    {
        var m = match;
        if (m.RivalRun == null) return;
        while (m.RivalEvt < m.RivalRun.Count && m.RivalRun[m.RivalEvt].t <= m.T)
        {
            var e = m.RivalRun[m.RivalEvt++];
            Floater($"+{e.cg / 100.0:0.00} kg", RivalX, RivalY - 28, e.perfect ? "#f3c25b" : "#ffffff");
        }
    }

    // One of the three anglers (or you, for "Your best") standing at x, y.
    void DrawRivalFigure(string id, int x, int y, string face, int arms, int pump, int bob, bool blink)
    {
        switch (id)
        {
            case "bess": LookData.DrawFigure(pix, "#d9a37a", "#cfcfcf", "#8a5fa8", "#4a4038", true, 1, null, x, y, face, 0, bob: bob, blink: blink, arms: arms, swing: pump, idle: false); break;
            case "marlo": LookData.DrawFigure(pix, "#b97a4e", "#2b1d14", "#3f7fa8", "#3f4a5a", false, 3, "#3a8a5a", x, y, face, 0, bob: bob, blink: blink, arms: arms, swing: pump, idle: false); break;
            case "ivy": LookData.DrawFigure(pix, "#c68a5a", "#5a3a20", "#b5523b", "#2f4a6a", true, 3, "#1f3a6a", x, y, face, 0, bob: bob, blink: blink, arms: arms, swing: pump, idle: false); break;
            case "net": LookData.DrawPerson(pix, netRivalLook ?? new Look(), x, y, face, 0, bob: bob, blink: blink, arms: arms, swing: pump, idle: false); break;
            default: LookData.DrawPerson(pix, match.Real.look, x, y, face, 0, bob: bob, blink: blink, arms: arms, swing: pump, idle: false); break;
        }
    }

    void DrawMatchRival()
    {
        var m = match;
        float t = m.Phase == "run" ? m.T : m.Phase == "done" ? m.Spec.Duration : 0;
        var run = m.RivalRun;
        MatchEvent next = null, prev = null;
        foreach (var e in run) { if (e.t <= t) prev = e; else { next = e; break; } }
        bool holdUp = prev != null && m.Phase != "ready" && t - prev.t < RivalHoldSecs;
        bool reeling = !holdUp && next != null && m.Phase == "run" && next.t - t < RivalFightSecs;
        int x = (int)RivalX, y = (int)RivalY;
        int bob = time % 2.4f > 1.5f ? 1 : 0;
        bool blink = time % 4.1f < 0.12f;
        int arms = holdUp ? 2 : 3;
        int pump = reeling ? (int)(time * 7) % 2 : 0;
        string face = "left";
        DrawRivalFigure(m.Spec.Rival, x, y, face, arms, pump, bob, blink);
        // The rod: from their hands out over the water, bending while they fight a fish.
        float hx = x - 3, hy = y - 9 - bob;
        float tipX = x - 11, tipY = y - 21 - bob + (reeling ? 3 + pump : 0);
        if (holdUp) { tipX = x - 9; tipY = y - 17 - bob; }
        pix.Line(hx, hy, tipX, tipY, "#8a6440");
        if (holdUp)
        {
            // The catch held up over their head.
            bool flip = (int)(time * 7) % 2 == 0;
            if (FishArt.Looks.ContainsKey(prev.fish)) FishArt.Draw(pix, prev.fish, x - 6, y - 25 - bob, 13, 8, flip);
            return;
        }
        // The line and bobber. Between landings they re-cast now and then.
        float cyc = (t + 3) % RivalCycle, castK = !reeling && m.Phase == "run" && cyc < 0.6f ? cyc / 0.6f : 1;
        float bx = tipX + (RivalBobX - tipX) * castK, by = tipY + (RivalBobY - tipY) * castK - MathF.Sin(MathF.PI * castK) * 10;
        if (reeling) by += MathF.Round(MathF.Sin(time * 25) * 0.6f);
        else if (castK >= 1) by += MathF.Round(MathF.Sin(time * 4 + 1) * 0.6f);
        int n = Math.Max(6, (int)MathF.Round(Dist(tipX, tipY, bx, by)));
        float sag = reeling ? 0 : castK < 1 ? 1 : 4;
        var line = Pal.C("rgba(240,240,240,0.75)");
        for (int i = 0; i <= n; i++)
        {
            float k = (float)i / n;
            pix.Rect(tipX + (bx - tipX) * k, tipY + (by - tipY) * k + MathF.Sin(MathF.PI * k) * sag, 1, 1, line);
        }
        if (reeling)
        {
            double ph = (time * 3) % 1;
            pix.Ring(bx, by + 1, 1 + ph * 3, Pal.Rgba(255, 255, 255, (float)(0.9 * (1 - ph))));
            pix.Rect(bx - 2 + MathF.Round(MathF.Sin(time * 20)), by - 1, 1, 1, "#ffffff");
            pix.Rect(bx + 2 + MathF.Round(MathF.Cos(time * 17)), by - 2, 1, 1, "#ffffff");
        }
        else
        {
            pix.Rect(bx - 1, by - 2, 3, 2, "#e04b3a");
            pix.Rect(bx - 1, by, 3, 1, "#ffffff");
            if (castK >= 1) pix.Ring(bx, by + 1, 2.2, Pal.C("rgba(255,255,255,0.35)"));
        }
    }
}
