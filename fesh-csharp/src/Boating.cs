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

    Target BoatTarget()
    {
        if (scene != "world") return null;
        if (Aboard) return new Target { Type = "land", Label = LandingSpot() != null ? "Land on shore" : "Steer with <move> · sail east to Amihan · <map> sea chart" };
        if (Has("boat") == 0 || Riding) return null;
        var (x, y) = BoatPosition();
        // Original jetties keep their quick route and alternate helm action.
        if (Dist(player.X, player.Y, SaltJettyX, SaltJettyY) < 10 || Dist(player.X, player.Y, AtollJettyX, AtollJettyY) < 10) return null;
        if (Dist(player.X, player.Y, x, y) < 23 && BoatCanStand(x, y))
            return new Target { Type = "boat", Label = "Board your boat" };
        return null;
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
        Toast("At the helm! Steer with <move>. Press <act> beside dry shore to land. Amihan lies east of Starfall; <map> opens your chart.", 8);
    }

    void LandBoat()
    {
        if (!Aboard) return;
        if (LandingSpot() is not (float x, float y)) { Toast("Come closer to a beach or jetty to land."); return; }
        state.boatX = player.X; state.boatY = player.Y; state.aboard = false;
        player.X = x; player.Y = y; target = null;
        Save(); Sfx.Play("pickup");
        Toast("Boat moored. Press <act> beside it to board again.");
    }

    void DrawHelmsman() => DrawBoat(player.X, player.Y, time, occupied: true);

    // Shared banca artwork: both bamboo floats run alongside the hull, with two crossbeams.
    // The seated fisher, sail and hull use one bob; the near gunwale hides the fisher's legs.
    void DrawBoat(float x, float y, float t, bool occupied = false)
    {
        int dir = occupied && player.Face == "left" ? -1 : 1;
        int X = (int)MathF.Round(x), waterY = (int)MathF.Round(y);
        int Y = waterY + (int)MathF.Round(MathF.Sin(t * 2) * .8f);
        void R(int dx, int dy, int w, int h, string col) =>
            pix.Rect(X + (dir > 0 ? dx : -dx - w + 1), Y + dy, w, h, col);

        // The reflection and wake stay on the water rather than bobbing with the boat.
        pix.Rect(X - 10, waterY + 4, 21, 2, Pal.Rgba(10, 35, 51, .24f));
        if (occupied && player.Moving)
        {
            float dx = player.Face == "left" ? -1 : player.Face == "right" ? 1 : 0;
            float dy = player.Face == "up" ? -1 : player.Face == "down" ? 1 : 0;
            for (int n = 0; n < 3; n++)
            {
                float distance = 13 + (t * 12 + n * 5) % 15;
                foreach (int side in new[] { -1, 1 })
                {
                    float wx = x - dx * distance - dy * side * distance * .28f;
                    float wy = y - dy * distance + dx * side * distance * .28f;
                    if (Swimmable(TileUnder(wx, wy)))
                        pix.Rect(wx, wy, 2, 1, Pal.Rgba(215, 244, 247, (28 - distance) * .035f));
                }
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

        // A small cream sail sits ahead of the helm, clear of the fisher's face.
        R(2, -15, 1, 16, "#6c4c32");
        R(2, -15, 1, 12, "#c69b61");
        for (int row = 0; row < 10; row++)
        {
            int width = 1 + row * 7 / 9;
            R(3, -14 + row, width, 1, row == 8 ? "#58a5a1" : row == 9 ? "#d7cda8" : "#f4edce");
        }
        R(3, -16, 3, 2, "#e37a53");

        if (occupied)
        {
            int seatX = X - 5 * dir;
            LookData.DrawPerson(pix, state.look, seatX, Y + 2, player.Face, 0, shadow: false);
            R(-5, -1, 5, 2, LookData.Pants[state.look.pants % LookData.Pants.Length]);
            // A hand on the short tiller makes the sitting pose read as steering.
            R(-12, -2, 6, 1, "#d7ad70");
            R(-8, -3, 2, 2, LookData.Skins[state.look.skin % LookData.Skins.Length]);
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
