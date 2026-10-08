using Raylib_cs;

namespace Fesh;

partial class Game
{
    bool Aboard => state.aboard && scene == "world" && Has("boat") > 0;
    (float x, float y) BoatPosition() => state.boatX > 0 && state.boatY > 0 ? (state.boatX, state.boatY)
        : state.boatAt == "atoll" ? (AtollJettyX - 14, AtollJettyY) : (SaltJettyX + 14, SaltJettyY);

    bool BoatCanStand(float x, float y)
    {
        // The hull needs water under every corner. Dock planks and land cannot be sailed through.
        foreach (var (ox, oy) in new[] { (-5f, -3f), (5f, -3f), (-5f, 3f), (5f, 3f) })
        {
            int tx = (int)MathF.Floor((x + ox) / T), ty = (int)MathF.Floor((y + oy) / T);
            if (tx <= 0 || ty <= 0 || tx >= COLS - 1 || ty >= ROWS - 1 || TileAt(tx, ty) is not ('~' or 'w' or 'l' or 'm' or 'o')) return false;
        }
        return !Solids().Any(r => r.Overlaps(new Box(x - 5, y - 3, 10, 6)));
    }

    (float x, float y)? LandingSpot()
    {
        // Search immediately beside the hull; never hop across another tile, wall or island.
        for (int r = 8; r <= 16; r += 2)
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                float x = player.X + r * dx, y = player.Y + r * dy;
                if (!CanStand(x, y)) continue;
                bool clear = true;
                for (int step = 2; step < r; step += 2)
                {
                    char t = TileUnder(player.X + step * dx, player.Y + step * dy);
                    if (!(Swimmable(t) || Walkable(t))) { clear = false; break; }
                }
                if (clear && !Solids().Any(b => b.Overlaps(new Box(Math.Min(x, player.X) - 2, Math.Min(y, player.Y) - 2,
                    Math.Abs(x - player.X) + 4, Math.Abs(y - player.Y) + 4)))) return (x, y);
            }
        return null;
    }

    // A moored boat close enough to step into. E boards it only when nothing else is here (FindTarget puts it last,
    // so it can't hide a fishing spot, a villager or the storm shelter); <ride> boards it any time.
    bool BoatInReach()
    {
        if (scene != "world" || Has("boat") == 0 || Aboard || Riding) return false;
        // Original jetties keep their quick route and alternate helm action.
        if (Dist(player.X, player.Y, SaltJettyX, SaltJettyY) < 10 || Dist(player.X, player.Y, AtollJettyX, AtollJettyY) < 10) return false;
        var (x, y) = BoatPosition();
        return Dist(player.X, player.Y, x, y) < 23 && BoatCanStand(x, y);
    }

    void LaunchBoat()
    {
        if (Has("boat") == 0) { Toast("Build a sailboat at a workbench: 20 wood, 4 iron bars and sailcloth from Pip.", 5); return; }
        if (Stormy) { Toast("Wait for the storm to pass before launching."); return; }
        bool atoll = Dist(player.X, player.Y, AtollJettyX, AtollJettyY) < 12;
        if (!atoll && Dist(player.X, player.Y, SaltJettyX, SaltJettyY) >= 12) return;
        // Jetties can recover your owned boat, just as the original Sail route did.
        float x = atoll ? AtollJettyX - 18 : SaltJettyX + 18, y = atoll ? AtollJettyY : SaltJettyY;
        if (!BoatCanStand(x, y)) { Toast("There isn't room to launch here."); return; }
        LeaveMount();
        state.boatX = x; state.boatY = y;
        BoardBoat(checkDistance: false);
    }

    void BoardBoat(bool checkDistance = true)
    {
        if (Has("boat") == 0 || scene != "world" || Aboard) return;
        if (Stormy) { Toast("Wait ashore until the storm passes."); return; }
        var (x, y) = BoatPosition();
        if (checkDistance && Dist(player.X, player.Y, x, y) >= 23 || !BoatCanStand(x, y)) return;
        LeaveMount();
        state.aboard = true; state.boatX = player.X = x; state.boatY = player.Y = y;
        target = null; player.Moving = false; Save(); Sfx.Play("splash");
        Toast("At the helm! Steer with <move>. Over deep water, <act> fishes the open sea; beside dry shore, <ride> lands. Amihan lies east of Starfall; <map> opens your chart.", 8);
    }

    void LandBoat()
    {
        if (!Aboard) return;
        if (LandingSpot() is not (float x, float y)) { Toast("Come closer to a beach or jetty to land."); return; }
        trolling = false;
        state.boatX = player.X; state.boatY = player.Y; state.aboard = false;
        player.X = x; player.Y = y; target = null;
        Save(); Sfx.Play("pickup");
        Toast("Boat moored. Press <ride> beside it to board again.");
    }

    void DrawHelmsman() => DrawBoat(player.X, player.Y, time, occupied: true);

    /* ---------- How the banca moves ---------- */
    int boatDir = 1;        // which way the bow points: it only turns when you steer left or right
    float sailFurl = 1;     // 0 with the sail up, 1 furled (moored, or stopped to fish)
    float sprayT;

    bool BoatUnderWay => Aboard && player.Moving && mode == "play";
    // A big sail is a third faster; trolling goes slowly; a storm slows everything.
    float BoatSpeed => (trolling ? 58 : Wears("big_sail") ? 124 : 96) * (Stormy ? 2f / 3 : 1);
    // The hull rides the swell: faster under way, harder in a storm.
    int BoatBob(float t) => (int)MathF.Round(MathF.Sin(t * (BoatUnderWay ? 3.2f : 2)) * (Stormy ? 1.6f : BoatUnderWay ? 1.1f : .8f));
    // Where the fisher sits: at the stern, behind the mast.
    int BoatSeatX => (int)MathF.Round(player.X) - 5 * boatDir;
    // The hand on the rod while fishing from the boat (pump lifts it a pixel while you reel).
    (int x, int y) BoatRodHand(int pump) => (BoatSeatX + (player.Face == "left" ? -3 : 2), (int)MathF.Round(player.Y) + BoatBob(time) - 4 + pump);

    void UpdateBoat(float dt)
    {
        if (!Aboard) { sailFurl = 1; return; }
        if (mode == "play" && player.Face is "left" or "right") boatDir = player.Face == "right" ? 1 : -1;
        // Stopped to fish, the sail comes down; back at the helm it goes up again.
        float want = FishingModes.Contains(mode) || mode is "catch" or "odd" or "legend" ? 1 : 0;
        sailFurl = want > sailFurl ? Math.Min(want, sailFurl + dt * 2.5f) : Math.Max(want, sailFurl - dt * 2.5f);
        if (!BoatUnderWay && !towing) return;
        // Spray off the bow, more of it in a storm.
        sprayT += dt;
        float every = Stormy ? 0.035f : 0.075f;
        while (sprayT > every)
        {
            sprayT -= every;
            float bx = player.X + 12 * boatDir, by = player.Y + 3 + BoatBob(time);
            particles.Add(new Particle { X = bx + FxRand(-1, 1), Y = by, Vx = boatDir * FxRand(6, 26) + FxRand(-8, 8), Vy = FxRand(-34, -12), Life = FxRand(0.2f, 0.38f), Color = fxRng.Next(3) == 0 ? "#bfe3ec" : "#eef9fb" });
            if (fxRng.Next(4) == 0)
                particles.Add(new Particle { X = player.X + (fxRng.Next(2) == 0 ? -11 : 11) * boatDir, Y = by + (fxRng.Next(2) == 0 ? -6 : 6), Vx = -boatDir * FxRand(4, 12), Vy = FxRand(-18, -8), Life = FxRand(0.15f, 0.28f), Color = "#eef9fb" });
        }
    }

    // Shared banca artwork: both bamboo floats run alongside the hull, with two crossbeams. The seated fisher, sail and
    // hull share one bob; the near gunwale hides the fisher's legs. Under way the sail fills, the pennant streams and
    // spray flies off the bow (UpdateBoat); stopped, the sail luffs and the water laps at the floats; moored or
    // fishing, the sail is furled on its boom.
    void DrawBoat(float x, float y, float t, bool occupied = false)
    {
        int dir = boatDir;
        bool underWay = occupied && (BoatUnderWay || towing), fishing = occupied && (fish != null || mode == "charging");
        // The big sail stands taller and wider on a taller mast.
        bool big = Wears("big_sail");
        int rows = big ? 13 : 10, mast = big ? 18 : 15;
        float furl = occupied ? sailFurl : 1;
        int X = (int)MathF.Round(x), waterY = (int)MathF.Round(y);
        int Y = waterY + BoatBob(t);
        // The mast sways a pixel either way as the hull rolls.
        float roll = t * (underWay ? 3.2f : 2) + 1.3f;
        int lean = (int)MathF.Round(MathF.Sin(roll) * (Stormy ? 1.4f : underWay ? .9f : .6f));
        void R(int dx, int dy, int w, int h, string col) =>
            pix.Rect(X + (dir > 0 ? dx : -dx - w + 1), Y + dy, w, h, col);
        void Water(float wx, float wy, Color c) { if (Swimmable(TileUnder(wx, wy + 1.5f))) pix.Rect(wx, wy, 1, 1, c); }

        // The reflection, wake and ripples stay on the water rather than bobbing with the boat.
        pix.Rect(X - 10, waterY + 4, 21, 2, Pal.Rgba(10, 35, 51, .24f));
        if (underWay)
        {
            // A V of foam spreading behind the stern, and a bow wave curling off either side of the bow.
            float dx = player.Face == "left" ? -1 : player.Face == "right" ? 1 : 0;
            float dy = player.Face == "up" ? -1 : player.Face == "down" ? 1 : 0;
            for (int n = 0; n < 6; n++)
            {
                float distance = 11 + (t * 16 + n * 4) % 24;
                foreach (int side in new[] { -1, 1 })
                {
                    float wx = x - dx * distance - dy * side * distance * .32f;
                    float wy = y + 3 - dy * distance + dx * side * distance * .32f;
                    var c = Pal.Rgba(215, 244, 247, (35 - distance) * .028f);
                    Water(wx, wy, c);
                    Water(wx + dx, wy + dy, c);
                }
            }
            for (int k = 0; k < 4; k++)
            {
                var c = Pal.Rgba(236, 250, 252, .75f - k * .16f);
                int bx = X + (12 - k * 2) * dir;
                Water(bx, waterY + 7 + k / 2 + (int)(t * 9 + k) % 2, c);
                Water(bx, waterY - 7 - k / 2, c);
            }
        }
        else
        {
            // Water lapping at the floats.
            for (int k = 0; k < 2; k++)
            {
                double ph = (t * .55 + k * .5) % 1;
                var c = Pal.Rgba(235, 248, 252, (float)(.45 * (1 - ph)));
                pix.Ring(X + (k == 0 ? -12 : 12) * dir, waterY + 7.5, 1.5 + ph * 3.5, c);
                pix.Ring(X + (k == 0 ? 12 : -12) * dir, waterY - 4.5, 1 + ph * 3, c);
            }
        }

        void Float(int fy)
        {
            R(-10, fy, 21, 2, "#9f8950");
            R(-11, fy, 23, 1, "#d9c78d");
            R(-9, fy - 1, 19, 1, "#f2dfa5");
            R(-7, fy - 1, 1, 2, "#ad8c4e");
            R(7, fy - 1, 1, 2, "#ad8c4e");
        }
        Float(-5);
        foreach (int beam in new[] { -7, 7 })
        {
            R(beam, -5, 2, 12, "#a8844e");
            R(beam, -5, 1, 12, "#dec48b");
        }

        // Open deck, raised bow and a thwart behind the mast.
        R(-10, -3, 20, 5, "#bd8750");
        R(-9, -2, 18, 4, "#68452f");
        R(-8, -1, 16, 2, "#936440");
        R(-5, -2, 2, 4, "#d6a369");
        R(9, -4, 3, 4, "#d6a369");
        R(12, -5, 1, 3, "#f1cc86");
        R(-12, -3, 2, 3, "#d6a369");

        // The mast leans with the roll; the sail hangs from it.
        for (int k = 0; k <= mast; k++)
        {
            int mx = 2 + (lean * k + (lean > 0 ? mast / 2 : lean < 0 ? -mast / 2 : 0)) / mast;
            R(mx, -k, 1, 1, k < 4 ? "#6c4c32" : "#c69b61");
        }
        int top = (int)MathF.Round(furl * rows);
        for (int row = top; row < rows; row++)
        {
            // Under way the middle of the sail bellies out; stopped, its edge flaps.
            int width = 1 + row * (big ? 9 : 7) / (rows - 1);
            if (underWay && row >= 2 && row <= rows - 3) width++;
            else if (!underWay && row >= 3 && row <= rows - 2 && MathF.Sin(t * 7 + row * .9f) > .55f) width++;
            int sx = 3 + lean * (rows - row) / rows;
            R(sx, -(mast - 1) + row, width, 1, row == rows - 2 ? "#58a5a1" : row == rows - 1 ? "#d7cda8" : big && row == rows / 2 ? "#e3c46f" : "#f4edce");
            if (underWay && width > 3) R(sx + width / 2, -(mast - 1) + row, 1, 1, "#e6dcbc");
        }
        if (furl > .05f)
        {
            // The sail furled along the boom, lashed in two places.
            int len = 1 + (int)MathF.Round((big ? 9 : 7) * Math.Min(1, furl * 1.25f));
            R(3, -6, len, 2, "#e9dfbf");
            R(3, -6, len, 1, "#f6efd8");
            if (len > 3) R(4, -6, 1, 2, "#a8844e");
            if (len > 6) R(7, -6, 1, 2, "#a8844e");
        }
        // The pennant streams back off the masthead under way, and flutters loosely when stopped.
        for (int i = 0; i < 4; i++)
        {
            int fx = 2 + lean - (i + 1);
            int fy = -mast + (int)MathF.Round(MathF.Sin(t * (underWay ? 15 : 5) - i * 1.2f) * i * (underWay ? .35f : .25f)) + (underWay ? 0 : i / 2);
            R(fx, fy, 1, i < 2 ? 2 : 1, i == 0 ? "#d5603f" : "#e37a53");
        }

        if (occupied)
        {
            int seatX = X - 5 * dir;
            bool holding = heldT > 0 && heldItem != null;
            bool blinkHit = iframes > 0 && (int)(time * 16) % 2 == 0;
            int pump = mode == "reeling" && ReelHeld() ? (int)(time * 10) % 2 : 0;
            // Sitting still at the helm, the fisher looks about now and then.
            string glance = !fishing && !underWay && idleT > 3 && (idleT - 3) % 7 < 1 ? (player.Face == "left" ? "down" : player.Face == "down" ? "right" : "down") : null;
            if (!blinkHit)
            {
                LookData.DrawPerson(pix, state.look, seatX, Y + 2, player.Face, 0, shadow: false, blink: time % 3.7f < 0.12f,
                    arms: holding ? 2 : fishing ? 3 : 0, swing: pump, head: glance);
                R(-5, -1, 5, 2, LookData.Pants[state.look.pants % LookData.Pants.Length]);
            }
            if (!fishing && !holding)
            {
                // A hand on the short tiller, which swings as you steer up or down.
                int tilt = underWay && player.Face == "up" ? -1 : underWay && player.Face == "down" ? 1 : 0;
                R(-12, -2 + tilt, 3, 1, "#d7ad70");
                R(-9, -2, 3, 1, "#d7ad70");
                if (!blinkHit) R(-8, -3, 2, 2, LookData.Skins[state.look.skin % LookData.Skins.Length]);
            }
            if (fishing)
            {
                var (hx, hy) = BoatRodHand(pump);
                var tip = RodTip();
                pix.Line(hx, hy, tip.X, tip.Y, "#6b4a2b");
            }
            if (holding) DrawHeld(seatX, Y + 2 - 17);
            if (trolling) DrawTroll(X, Y, dir, t);
        }

        // Draw the near side last so nobody stands on top of the gunwale.
        R(-11, 0, 22, 2, "#4e9691");
        R(-10, 2, 20, 2, "#916039");
        R(-8, 4, 16, 1, "#563e2d");
        R(-11, 0, 22, 1, "#bce0bd");
        R(-8, 2, 15, 1, "#c48b51");
        Float(7);
    }
}
