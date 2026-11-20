namespace Fesh;

// Generating and baking the Philippines window (PhMap.cs). Every tile is worked out from the country's own tiles (PhTile),
// and the water depth and the land's distance from the water come from fields worked out once for the whole country, so a
// window cut anywhere shows the same land and sea, with no seam where two windows meet.
//
// Moving the window (PhShift) doesn't bake it all again (that took about two seconds): the tiles and baked pixels that are
// still inside it are moved over, the new strip along the far edge is filled in, and the strip is baked a little at a time
// over the next frames (PhBakeStep), well before you can see it (you're always PhEdge tiles or more from the edge).
partial class Game
{
    // The country's tile kinds: 0 deep sea, 1 shallow sea (within 2 tiles of land), 2 lake, 3 land.
    static byte[] phKind;
    static float[] phDepth, phLandDist;
    static void PhFields()
    {
        if (phKind != null) return;
        int W0 = PhMapData.Cols, H0 = PhMapData.Rows, n = W0 * H0;
        const int Far = 9999;
        // Chessboard distance from land (sea tiles), for the shallows.
        var dl = new int[n];
        for (int i = 0; i < n; i++) dl[i] = PhGrid()[i] == 1 ? 0 : Far;
        void Pass(int[] d, bool forward)
        {
            int ys = forward ? 0 : H0 - 1, ye = forward ? H0 : -1, st = forward ? 1 : -1;
            for (int y = ys; y != ye; y += st)
                for (int x = forward ? 0 : W0 - 1; forward ? x < W0 : x >= 0; x += st)
                {
                    int i = y * W0 + x, best = d[i];
                    void Try(int xx, int yy) { if (xx >= 0 && yy >= 0 && xx < W0 && yy < H0) best = Math.Min(best, d[yy * W0 + xx] + 1); }
                    Try(x - st, y); Try(x, y - st); Try(x - st, y - st); Try(x + st, y - st);
                    d[i] = best;
                }
        }
        Pass(dl, true); Pass(dl, false);
        phKind = new byte[n];
        for (int i = 0; i < n; i++) phKind[i] = PhGrid()[i] switch { 1 => (byte)3, 2 => (byte)2, _ => dl[i] <= 2 ? (byte)1 : (byte)0 };
        // The same chamfer distances ComputeDepth works out (1 straight, 1.41 diagonal): the deep sea's distance from the
        // shallows, and the land's from the water.
        float[] Chamfer(Func<int, bool> source)
        {
            var d = new float[n];
            for (int i = 0; i < n; i++) d[i] = source(i) ? 0 : 999;
            float At(int x, int y) => x < 0 || y < 0 || x >= W0 || y >= H0 ? 999 : d[y * W0 + x];
            for (int y = 0; y < H0; y++)
                for (int x = 0; x < W0; x++)
                    d[y * W0 + x] = MathF.Min(d[y * W0 + x], MathF.Min(MathF.Min(At(x - 1, y) + 1, At(x, y - 1) + 1), MathF.Min(At(x - 1, y - 1) + 1.41f, At(x + 1, y - 1) + 1.41f)));
            for (int y = H0 - 1; y >= 0; y--)
                for (int x = W0 - 1; x >= 0; x--)
                    d[y * W0 + x] = MathF.Min(d[y * W0 + x], MathF.Min(MathF.Min(At(x + 1, y) + 1, At(x, y + 1) + 1), MathF.Min(At(x + 1, y + 1) + 1.41f, At(x - 1, y + 1) + 1.41f)));
            return d;
        }
        var deepDist = Chamfer(i => phKind[i] != 0);
        phDepth = new float[n];
        for (int i = 0; i < n; i++)
        {
            int x = i % W0, y = i / W0;
            phDepth[i] = phKind[i] switch
            {
                0 => 1 + deepDist[i],
                1 => 1,
                2 => PhKindAt(x - 1, y) != 3 && PhKindAt(x + 1, y) != 3 && PhKindAt(x, y - 1) != 3 && PhKindAt(x, y + 1) != 3 ? 2 : 1,
                _ => 0
            };
        }
        var wd = Chamfer(i => phKind[i] != 3);
        phLandDist = new float[n];
        for (int i = 0; i < n; i++) phLandDist[i] = wd[i] >= 999 ? 0 : wd[i];
    }
    static byte PhKindAt(int gx, int gy) => gx < 0 || gy < 0 || gx >= PhMapData.Cols || gy >= PhMapData.Rows ? (byte)0 : phKind[gy * PhMapData.Cols + gx];

    // The ground at a tile of the country (before any plant).
    static char PhGround(int gx, int gy)
    {
        PhFields();
        switch (PhKindAt(gx, gy))
        {
            case 0: return '~';
            case 1: return 'w';
            case 2: return 'l';
        }
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
                if (PhKindAt(gx + dx, gy + dy) is 0 or 1) return 's';
        // (A volcano's bare cone is in its drawing, PhTerrain.cs; as ground, its rock showed out past the drawing's foot.)
        // Forest and open grass in big soft patches (slow noise in the country's own tiles), the forest thicker up the
        // mountains.
        double n = PhNoise(gx * 0.045, gy * 0.045) * 0.7 + PhNoise(gx * 0.16 + 31, gy * 0.16 - 7) * 0.3;
        float up = phElev[gy * PhMapData.Cols + gx];
        return n > 0.56 - Math.Min(0.3, up / 3000.0) ? 'j' : 'g';
    }

    // The tile: palms along the beaches, trees in the forest and a few on the grass, some bushes.
    static char PhTile(int gx, int gy)
    {
        char t = PhGround(gx, gy);
        double r = Pix.Hash(gx, gy, 311);
        return t switch
        {
            's' when r < 0.16 && PhGround(gx - 1, gy) != '~' && PhGround(gx + 1, gy) != '~' => 'h',
            // Forest is mostly trees (palms in the lowlands), thicker up the mountains; the grass has scattered trees, a palm
            // or two near the coast, and bushes. (Too thin before: Marinduque looked nearly bare.)
            'j' when r < 0.3 + Math.Min(0.12, phElev[gy * PhMapData.Cols + gx] / 8000.0) => r < 0.1 && phElev[gy * PhMapData.Cols + gx] < 500 ? 'h' : 't',
            'g' when r < 0.07 => phLandDist[gy * PhMapData.Cols + gx] < 4 && r < 0.035 ? 'h' : 't',
            'g' when r < 0.1 => 'y',
            _ => t
        };
    }
    static bool PhPlant(char t) => t is 't' or 'h' or 'y';

    // The country's tiles, all of them, once a game (they're the world: trees, depth and the land's distance from the water).
    bool phTilesReady;
    void PhInitTiles()
    {
        PhFields();
        trees.Clear(); bridgeSet.Clear(); hoofprints.Clear();
        int C = PhMapData.Cols;
        for (int y = 0; y < ROWS; y++)
            for (int x = 0; x < COLS; x++)
            {
                char t = PhTownTile(x, y) ?? PhTile(x, y);
                if (PhPlant(t) && PhUnderMountain(x, y)) t = PhGround(x, y);   // no trees standing on a mountain's drawn face
                worldMap[y, x] = t;
                biome[y, x] = 5;
                depth[y, x] = phDepth[y * C + x];
                landDist[y, x] = phLandDist[y * C + x];
                if (PhPlant(t)) trees.Add((x, y, t));
            }
        for (int y = 0; y < ROWS; y++)
            for (int x = 0; x < COLS; x++) groundCache[y, x] = Ground(worldMap[y, x], x, y);
        relief = new float[bufW * bufH];
        phTilesReady = true;
        ResetWorldCaches();
    }

    // The window whose top-left tile is (x0, y0), baked whole: the shape of the coast, the ground, then the shore.
    void PhBakeWindow(int x0, int y0)
    {
        phQueue.Clear();
        var keepMap = map; var keepBase = basePix;
        map = worldMap; basePix = worldBase;
        bufX0 = x0 * T; bufY0 = y0 * T;
        worldBase.CamX = bufX0; worldBase.CamY = bufY0;
        worldRich = null;
        state.phX0 = x0; state.phY0 = y0;
        for (int y = y0; y < y0 + PhWin && y < ROWS; y++)
            for (int x = x0; x < x0 + PhWin && x < COLS; x++) ShapeTile(x, y);
        for (int y = y0; y < y0 + PhWin && y < ROWS; y++)
            for (int x = x0; x < x0 + PhWin && x < COLS; x++) PaintTile(x, y);
        bridgeClip = null;
        for (int y = y0; y < y0 + PhWin && y < ROWS; y++)
            for (int x = x0; x < x0 + PhWin && x < COLS; x++) PaintBridge(x, y);   // the towns' piers
        FindShore();
        shoreCells = null;   // (the tint field and the open sea are per tile, worked out for the whole country once)
        map = keepMap; basePix = keepBase;
        if (scene == "world") { map = worldMap; basePix = worldBase; }
    }

    /* ---------- moving the window ---------- */
    // Tiles still to bake after a move: shape first (a ring of 2 round the new strip), then paint (3), then the rich sea (4).
    readonly Queue<(int x, int y, int stage)> phQueue = new();
    (int x0, int y0, int x1, int y1) phPending = (0, 0, -1, -1);   // the box of tiles being baked, in world tiles

    // Moves the window to (x0, y0): what's already baked is shifted over, the new edge is queued to bake (PhBakeStep).
    void PhMoveWindow(int x0, int y0)
    {
        int ox = bufX0 / T, oy = bufY0 / T, dx = x0 - ox, dy = y0 - oy;
        if (dx == 0 && dy == 0) return;
        if (Math.Abs(dx) >= PhWin / 2 || Math.Abs(dy) >= PhWin / 2) { PhBakeWindow(x0, y0); return; }   // too far: start afresh
        while (phQueue.Count > 0) PhBakeStep(int.MaxValue);   // finish the last move first
        int pw = bufW, ph = bufH;
        static void MovePx<TT>(TT[] a, int dxp, int dyp, int pw, int ph, TT fill)
        {
            var b = (TT[])a.Clone();
            for (int y = 0; y < ph; y++)
            {
                int sy = y + dyp;
                if (sy < 0 || sy >= ph) { Array.Fill(a, fill, y * pw, pw); continue; }
                int x0 = Math.Max(0, -dxp), x1 = Math.Min(pw, pw - dxp);
                if (x0 > 0) Array.Fill(a, fill, y * pw, x0);
                if (x1 < pw) Array.Fill(a, fill, y * pw + Math.Max(0, x1), pw - Math.Max(0, x1));
                if (x1 > x0) Array.Copy(b, sy * pw + x0 + dxp, a, y * pw + x0, x1 - x0);
            }
        }
        MovePx(shape, dx * T, dy * T, pw, ph, '~');
        MovePx(worldBase.Buf, dx * T, dy * T, pw, ph, Pal.Sea);
        if (worldRich != null) MovePx(worldRich.Buf, dx * T, dy * T, pw, ph, Pal.Sea);
        bufX0 = x0 * T; bufY0 = y0 * T;
        worldBase.CamX = bufX0; worldBase.CamY = bufY0;
        if (worldRich != null) { worldRich.CamX = bufX0; worldRich.CamY = bufY0; }
        state.phX0 = x0; state.phY0 = y0;
        // The new strip (world tiles), and every tile within 4 of it to bake again (shape, paint and the rich sea reach a
        // little round a tile).
        int wx1 = x0 + PhWin - 1, wy1 = y0 + PhWin - 1;
        int nx0 = dx > 0 ? ox + PhWin : x0, nx1 = dx > 0 ? wx1 : ox - 1, ny0 = dy > 0 ? oy + PhWin : y0, ny1 = dy > 0 ? wy1 : oy - 1;
        bool Near(int x, int y, int r) => dx != 0 && x >= nx0 - r && x <= nx1 + r || dy != 0 && y >= ny0 - r && y <= ny1 + r;
        // (stage 3 paints the piers over the fresh ground, before stage 2 makes the rich sea from it)
        foreach (int stage in new[] { 0, 1, 3, 2 })
            for (int y = y0; y <= wy1; y++)
                for (int x = x0; x <= wx1; x++)
                    if (Near(x, y, stage == 3 ? 3 : stage + 2)) phQueue.Enqueue((x, y, stage));
        phPending = (x0, y0, wx1, wy1);
        if (dx != 0) phPending = dx > 0 ? (Math.Max(x0, nx0 - 4), y0, wx1, wy1) : (x0, y0, Math.Min(wx1, nx1 + 4), wy1);
        if (dy != 0 && dx == 0) phPending = dy > 0 ? (x0, Math.Max(y0, ny0 - 4), wx1, wy1) : (x0, y0, wx1, Math.Min(wy1, ny1 + 4));
        // The shore list loses what's gone out of the window and what's being baked again; the bake adds it back.
        var keep = shore.Where(s => InBuf(s.x, s.y) && !InPendingBox(s.x / T, s.y / T, dx, dy, nx0, nx1, ny0, ny1)).ToList();
        shore.Clear(); shore.AddRange(keep);
        shoreCells = null;   // (the tint field and the open sea are per tile, worked out for the whole country once)
    }

    static bool InPendingBox(int tx, int ty, int dx, int dy, int nx0, int nx1, int ny0, int ny1) =>
        dx != 0 && tx >= nx0 - 4 && tx <= nx1 + 4 || dy != 0 && ty >= ny0 - 4 && ty <= ny1 + 4;

    // Bakes up to `budget` queued tiles (Update calls it every frame); when the queue runs dry, the strip's shore is found.
    void PhBakeStep(int budget)
    {
        if (phQueue.Count == 0) return;
        var keepMap = map; var keepBase = basePix;
        map = worldMap; basePix = worldBase;
        for (int n = 0; n < budget && phQueue.Count > 0; n++)
        {
            var (x, y, stage) = phQueue.Dequeue();
            if (stage == 0) ShapeTile(x, y);
            else if (stage == 1) PaintTile(x, y);
            else if (stage == 3) { bridgeClip = null; PaintBridge(x, y); }
            else RichTile(x, y);
        }
        if (phQueue.Count == 0)
        {
            // The shore of everything baked again (the strip and the 4 tiles round it), back into the list.
            var (x0, y0, x1, y1) = phPending;
            var have = new HashSet<(short, short)>(shore.Select(s => (s.x, s.y)));
            for (int wy = Math.Max(bufY0, y0 * T); wy <= Math.Min(bufY0 + bufH - 1, (y1 + 1) * T - 1); wy++)
                for (int wx = Math.Max(bufX0, x0 * T); wx <= Math.Min(bufX0 + bufW - 1, (x1 + 1) * T - 1); wx++)
                {
                    char w = shape[BI(wx, wy)];
                    if (!Wet(w) || have.Contains(((short)wx, (short)wy))) continue;
                    int nx = 0, ny = 0;
                    if (Shore(ShapePx(wx + 1, wy))) nx--;
                    if (Shore(ShapePx(wx - 1, wy))) nx++;
                    if (Shore(ShapePx(wx, wy + 1))) ny--;
                    if (Shore(ShapePx(wx, wy - 1))) ny++;
                    bool touches = Shore(ShapePx(wx + 1, wy)) || Shore(ShapePx(wx - 1, wy)) || Shore(ShapePx(wx, wy + 1)) || Shore(ShapePx(wx, wy - 1));
                    if (touches) shore.Add(((short)wx, (short)wy, (sbyte)nx, (sbyte)ny, w));
                }
            shoreCells = null;
            phPending = (0, 0, -1, -1);
        }
        map = keepMap; basePix = keepBase;
        if (scene == "world") { map = worldMap; basePix = worldBase; }
    }
}
