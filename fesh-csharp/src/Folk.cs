namespace Fesh;

// Someone pottering about near their own spot: they stand a while, then stroll a few steps to somewhere close and
// clear, with the same four-frame walk as you, and stop to face you when you come near (so talking to them is easy).
sealed class Stroller
{
    public float X, Y, HomeX, HomeY, RangeX, RangeUp, RangeDown, Notice = 28, Wait = 2, WalkT;
    public float? ToX, ToY;
    public string Face = "down";
    public bool Moving;
    // The walk frame for LookData.DrawPerson: a slower, shorter stride than yours.
    public int Step => Moving ? 1 + (int)(WalkT * 5) % 4 : 0;
}

// Tomas pottering round his camp, Pip shuffling behind the counter, and the Amihan villagers strolling in front of
// their houses. Looks only: the strolls use fxRng, and the autotest pins everyone at home (standStill) unless a check
// wants them moving.
partial class Game
{
    bool standStill;
    readonly Stroller tomasWalk = new() { HomeX = TomasHomeX, HomeY = TomasHomeY, RangeX = 18, RangeUp = 2, RangeDown = 14 };
    readonly Stroller pipWalk = new() { HomeX = PipX, HomeY = PipY, RangeX = 6, Notice = 42 };
    readonly Dictionary<string, Stroller> islanderWalk = new();

    // Tomas strolls only around his own camp: in bed, or down on the dock for the ending, he stays put.
    bool TomasAtCamp => Dist(tomasX, tomasY, TomasHomeX, TomasHomeY) < 40;

    Stroller IslanderWalk(string id)
    {
        if (islanderWalk.TryGetValue(id, out var s)) return s;
        var n = Islanders.First(i => i.id == id);
        // Up and down the front of their house; the house itself is solid (IslandSolids).
        s = new Stroller { HomeX = n.x, HomeY = n.y + 2, X = n.x, Y = n.y + 2, RangeX = 13, RangeDown = 7, Notice = 26 };
        islanderWalk[id] = s;
        return s;
    }

    void UpdateStrollers(float dt)
    {
        if (scene != "world") return;
        if (TomasAtCamp && !TomasInBed)
        {
            tomasWalk.X = tomasX; tomasWalk.Y = tomasY;
            Stroll(tomasWalk, dt, (x, y) => FolkCanStand(x, y, tomas: true));
            tomasX = tomasWalk.X; tomasY = tomasWalk.Y;
        }
        else if (TomasInBed) { tomasX = TomasHomeX; tomasY = TomasHomeY; tomasWalk.ToX = tomasWalk.ToY = null; tomasWalk.Moving = false; }
        // Behind the counter there's nothing to bump into.
        if (PipOpen) Stroll(pipWalk, dt, (_, _) => true);
        else { pipWalk.X = PipX; pipWalk.Y = PipY; pipWalk.Moving = false; }
        foreach (var n in Islanders)
        {
            var s = IslanderWalk(n.id);
            if (Dist(s.X, s.Y, player.X, player.Y) < 400) Stroll(s, dt, (x, y) => FolkCanStand(x, y));
        }
        UpdateHabagatFolk(dt);
    }

    // Clear ground for someone's feet: dry land, nothing solid (bar Tomas himself), and not on top of you.
    bool FolkCanStand(float x, float y, bool tomas = false)
    {
        foreach (var (ax, ay) in new[] { (x - 3, y - 3), (x + 2.9f, y - 3), (x - 3, y), (x + 2.9f, y) })
            if (!Walkable(TileAt((int)MathF.Floor(ax / T), (int)MathF.Floor(ay / T)))) return false;
        var feet = new Box(x - 3, y - 3, 7, 3);
        if (feet.Overlaps(new Box(player.X - 5, player.Y - 5, 10, 7))) return false;
        foreach (var r in Solids())
        {
            if (tomas && r.X == tomasX - 3 && r.Y == tomasY - 3 && r.W == 7 && r.H == 3) continue;
            if (r.Overlaps(feet)) return false;
        }
        return true;
    }

    void Stroll(Stroller s, float dt, Func<float, float, bool> clear)
    {
        if (s.X == 0 && s.Y == 0) { s.X = s.HomeX; s.Y = s.HomeY; }
        s.Moving = false;
        if (standStill) { s.X = s.HomeX; s.Y = s.HomeY; s.ToX = s.ToY = null; s.Face = "down"; return; }
        // Someone coming over: stop and turn to them.
        if (Dist(player.X, player.Y, s.X, s.Y) < s.Notice)
        {
            s.ToX = s.ToY = null;
            s.Wait = Math.Max(s.Wait, 1.5f);
            float dx = player.X - s.X, dy = player.Y - s.Y;
            s.Face = MathF.Abs(dx) > MathF.Abs(dy) * 1.5f ? (dx > 0 ? "right" : "left") : "down";
            return;
        }
        if (s.ToX is float tx && s.ToY is float ty)
        {
            float dx = tx - s.X, dy = ty - s.Y, d = MathF.Sqrt(dx * dx + dy * dy);
            if (d < 0.5f)
            {
                s.X = tx; s.Y = ty; s.ToX = s.ToY = null;
                s.Wait = FxRand(3, 9);
                s.Face = fxRng.Next(3) == 0 ? (fxRng.Next(2) == 0 ? "left" : "right") : "down";
                return;
            }
            float step = Math.Min(d, 17 * dt), nx = s.X + dx / d * step, ny = s.Y + dy / d * step;
            // Something got in the way (you, or a piece just built): give up and stand a moment.
            if (!clear(nx, ny)) { s.ToX = s.ToY = null; s.Wait = FxRand(1, 3); return; }
            s.X = nx; s.Y = ny; s.Moving = true; s.WalkT += dt;
            s.Face = MathF.Abs(dx) > MathF.Abs(dy) ? (dx > 0 ? "right" : "left") : (dy > 0 ? "down" : "up");
            return;
        }
        s.Wait -= dt;
        if (s.Wait > 0) return;
        s.Wait = FxRand(2, 5);
        for (int tries = 0; tries < 6; tries++)
        {
            float x = s.HomeX + FxRand(-s.RangeX, s.RangeX), y = s.HomeY + FxRand(-s.RangeUp, s.RangeDown);
            // Home is always a fine place to wander back to.
            if (tries == 5) (x, y) = (s.HomeX, s.HomeY);
            float d = Dist(x, y, s.X, s.Y);
            if (d < 3) continue;
            bool ok = true;
            for (float k = 2; k <= d && ok; k += 2) ok = clear(s.X + (x - s.X) * k / d, s.Y + (y - s.Y) * k / d);
            if (!ok) continue;
            s.ToX = x; s.ToY = y;
            break;
        }
    }
}
