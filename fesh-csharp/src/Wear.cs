using Raylib_cs;

namespace Fesh;

// Trodden ground (1.41). Walk the same grass or forest floor again and again and it wears: a speckle of bare earth, a worn
// patch, packed earth. The count is kept for every tile you step onto on foot (`state.wear`, saved with the game and counted
// with Light: Off too, since it is only how it looks), it fades a little each day, and what it looks like is painted into the
// rich layer (Light: Soft or Full), one tile at a time when a tile crosses a stage.
partial class Game
{
    const int WearCap = 300;
    static readonly int[] WearSteps = { 12, 40, 100 };      // tile entries for stages 1, 2 and 3
    (int x, int y) lastWearTile = (-1, -1);

    internal static int StageOf(int entries) => entries >= WearSteps[2] ? 3 : entries >= WearSteps[1] ? 2 : entries >= WearSteps[0] ? 1 : 0;
    internal int WearStage(int x, int y) => state.wear.TryGetValue($"{x},{y}", out int n) ? StageOf(n) : 0;

    // Called as you walk on foot. A tile counts once each time you step onto it from another (distance, not footfalls).
    void NoteWear()
    {
        // Not on foot on dry ground (or in a match): nothing counts, and stepping back onto the last tile afterwards counts again.
        if (scene != "world" || Riding || Aboard || InWater || MatchActive) { lastWearTile = (-1, -1); return; }
        int tx = (int)MathF.Floor(player.X / T), ty = (int)MathF.Floor((player.Y - 1.5f) / T);
        if (tx < 0 || ty < 0 || tx >= COLS || ty >= ROWS || (tx, ty) == lastWearTile) return;
        lastWearTile = (tx, ty);
        // Only open grass and forest floor wears, and not under a shack, a lantern or a path piece laid on it.
        if (map[ty, tx] is not ('g' or 'j') || BuildAt(tx, ty) != null) return;
        string key = $"{tx},{ty}";
        int before = state.wear.GetValueOrDefault(key), after = Math.Min(WearCap, before + 1);
        state.wear[key] = after;
        if (StageOf(after) != StageOf(before) && worldRich != null) RichTile(tx, ty);
    }

    // Each day without being walked on, a tile loses a little more than it did (more for a well-worn one), so a stage doesn't
    // vanish overnight. Run from Update whenever the day has moved on, and when a game starts; days jumped over count one by one.
    void CatchUpWear()
    {
        if (state.wearDay <= 0) { state.wearDay = state.day; return; }
        if (state.wearDay >= state.day) { state.wearDay = Math.Min(state.wearDay, state.day); return; }
        var changed = new List<(int x, int y)>();
        int days = Math.Min(state.day - state.wearDay, 400);
        for (int d = 0; d < days && state.wear.Count > 0; d++)
            foreach (var key in state.wear.Keys.ToList())
            {
                int n = state.wear[key], faded = n - (1 + n / 40);
                if (StageOf(faded) != StageOf(n) && key.Split(',') is { Length: 2 } p && int.TryParse(p[0], out int x) && int.TryParse(p[1], out int y)) changed.Add((x, y));
                if (faded <= 0) state.wear.Remove(key); else state.wear[key] = faded;
            }
        state.wearDay = state.day;
        if (worldRich != null) foreach (var (x, y) in changed.Distinct()) if (x >= 0 && y >= 0 && x < COLS && y < ROWS) RichTile(x, y);
    }

    // The wear's look, painted by RichGroundDetail before the other decor: a patch of bare earth from the tile's middle outwards,
    // organic at the edge, only on plain, calm ground of the tile's own kind.
    static readonly Color[] EarthTones = Tones("#b79a68", "#a98a5a", "#9c7f52");
    void PaintWear(int x, int y, char t, int stage)
    {
        int X = x * T, Y = y * T;
        float strength = stage == 1 ? 0.5f : stage == 2 ? 0.7f : 0.88f;
        for (int py = 0; py < T; py++)
            for (int px = 0; px < T; px++)
            {
                int wx = X + px, wy = Y + py;
                float dx = px - 4.5f, dy = py - 4.5f, d = MathF.Sqrt(dx * dx + dy * dy);
                float n = VNoise(wx, wy, 5, 1200) * 0.55f + (float)Pix.Hash(wx, wy, 1201) * 0.2f + (BayerAt(wx, wy) - 0.5f) * 0.15f;
                bool paint = stage == 1 ? Pix.Hash(wx, wy, 1202) < 0.13 && d < 5.2f : stage == 2 ? d < 3.2f + 3.2f * n : d < 4.0f + 3.4f * n;
                if (!paint) continue;
                var tone = EarthTones[stage - 1];
                DecoWear(x, y, t, px, py, tone, strength * (0.8f + 0.4f * n));
            }
    }

    // One pixel of wear: the ground colour mixed towards bare earth (so the ground's own shading shows through).
    void DecoWear(int x, int y, char t, int px, int py, Color earth, float amount)
    {
        int wx = x * T + px, wy = y * T + py, i = wy * worldBase.W + wx;
        if (shape[wy * PW + wx] != t) return;
        var b = worldBase.Buf[i];
        if (!worldRich.Buf[i].Equals(b) || !b.Equals(GroundPixel(wx, wy, t))) return;
        foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            var n = worldBase.Buf[(wy + dy) * worldBase.W + wx + dx];
            if (Math.Abs(n.R - b.R) + Math.Abs(n.G - b.G) + Math.Abs(n.B - b.B) > 80) return;
        }
        worldRich.Buf[i] = Pix.Mix(b, earth, Math.Clamp(amount, 0, 1));
    }
}
