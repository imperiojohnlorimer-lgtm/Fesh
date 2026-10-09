using Raylib_cs;

namespace Fesh;

// Scenes: "world" (outdoors), "cave" (a floor of Frostfang Caverns, see Cave.cs), "house:tomas" and "house:x,y" (a shack at that tile).
partial class Game
{
    string scene = "world";
    readonly char[,] worldMap;
    readonly Pix worldBase;

    // Tomas's hut comes furnished, and you can use his workbench, stove and bed.
    static readonly List<Build> TomasRoom = new()
    {
        new() { id = "bed", x = 1, y = 2 }, new() { id = "workbench", x = 4, y = 2 }, new() { id = "stove", x = 15, y = 2 },
        new() { id = "table", x = 14, y = 6 }, new() { id = "rug", x = 8, y = 6 }, new() { id = "plant", x = 16, y = 9 },
        new() { id = "lamp", x = 1, y = 9 }
    };
    // Room sizes in tiles: Tomas's hut, then your shack.
    const int TomasW = 18, TomasH = 11, ShackW = 22, ShackH = 13;
    const float PhotoX = 115, PhotoY = 28;

    public Game()
    {
        worldMap = map;
        worldBase = basePix;
    }

    bool InHouse => scene.StartsWith("house:");
    bool InOwnHouse => InHouse && scene != "house:tomas";
    static string ShackKey(Build b) => $"house:{b.x},{b.y}";

    // The furniture or buildings that belong to the current scene.
    List<Build> SceneBuilds()
    {
        if (scene == "world") return state.builds;
        if (scene == "house:tomas") return TomasRoom;
        if (InHouse)
        {
            if (!state.rooms.TryGetValue(scene, out var room)) state.rooms[scene] = room = new List<Build>();
            return room;
        }
        return new List<Build>();
    }

    bool SceneExists(string key) =>
        key == "world" || key == "cave" || key == "house:tomas" || state.builds.Any(b => Data.BuildById[b.id].Door && ShackKey(b) == key);

    void LoadScene(string key)
    {
        if (key != "world" && scene == "world") LeaveMount();
        scene = key;
        if (key == "world") { map = worldMap; basePix = worldBase; }
        else if (key == "cave") BuildCave();
        else BuildRoom(key == "house:tomas");
        ReindexBuilds();
        critters.Clear();
        particles.Clear();
        if (key != "cave") monsters.Clear();
        // Platejaw only lives on the Ancient Floor, and goes back under if you leave it (Guardian.cs).
        guardian = null; rocks.Clear();
        hover = null;
        ghost = null;
        fish = null;
        chopTile = (-1, -1);
    }

    void GoTo(string key, float x, float y, string face, Action after = null)
    {
        Sfx.Play("door");
        FadeThrough(() =>
        {
            LoadScene(key);
            player.X = x; player.Y = y; player.Face = face;
            Save();
        }, after);
    }

    void EnterHouse(string key, float doorX, float doorY)
    {
        state.exitX = doorX;
        state.exitY = doorY + 3;
        bool tomas = key == "house:tomas";
        int w = tomas ? TomasW : ShackW, h = tomas ? TomasH : ShackH;
        GoTo(key, (w / 2) * T + 5, (h - 2) * T + 8, "up", () =>
        {
            if (tomas && !state.Hinted("tomasHut"))
            {
                state.hinted["tomasHut"] = true;
                Toast("Tomas's workbench and stove are yours to use. Press <act> at the workbench to craft an axe.", 5);
            }
            else if (!tomas && !state.Hinted("ownHouse"))
            {
                state.hinted["ownHouse"] = true;
                Toast("Your own place. Press <build> to furnish it: workbench, furnace, stove, bed and more.", 5);
            }
        });
    }

    void LeaveToWorld() => GoTo("world", state.exitX, state.exitY, "down");

    /* ---------- House interiors ---------- */
    char[,] roomMap;
    Pix roomBase;
    int RoomDoorX => SCols / 2;

    // Walls around the edge (the top two rows are the back wall), wooden floor inside and a door at the bottom.
    void BuildRoom(bool tomas)
    {
        int w = tomas ? TomasW : ShackW, h = tomas ? TomasH : ShackH;
        roomMap = new char[h, w];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                roomMap[y, x] = y < 2 || x == 0 || x == w - 1 || y == h - 1 ? '#' : '.';
        roomMap[h - 1, w / 2] = 'Y';
        map = roomMap;
        roomBase = new Pix(w * T, h * T);
        basePix = roomBase;
        var g = roomBase;
        // A bahay kubo's walls are woven bamboo (sawali); a shack's are planks.
        bool kubo = state.builds.Any(b => b.id == "kubo" && ShackKey(b) == scene);
        string paper = tomas ? "#5d7d8a" : kubo ? "#d8c08a" : "#c9a77a", stripe = tomas ? "#6a8b98" : kubo ? "#b89a62" : "#d6b78c";
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int X = x * T, Y = y * T;
                char t = roomMap[y, x];
                if (t == '#' && y < 2 && x > 0 && x < w - 1)
                {
                    g.Rect(X, Y, T, T, paper);
                    for (int i = 1; i < T; i += 4) g.Rect(X + i, Y, 1, T, stripe);
                    if (y == 1) { g.Rect(X, Y + 7, T, 3, "#5b3a24"); g.Rect(X, Y + 7, T, 1, "#7a5230"); }
                }
                else if (t == '#')
                {
                    g.Rect(X, Y, T, T, "#3b2a1d");
                    g.Rect(X + (x == 0 ? 8 : 0), Y, 2, T, "#5b3a24");
                    if (y == h - 1) g.Rect(X, Y, T, 2, "#5b3a24");
                }
                else
                {
                    g.Rect(X, Y, T, T, "#a8794a");
                    for (int i = (y % 2) * 1; i < T; i += 3) g.Rect(X, Y + i, T, 1, "#97693e");
                    if (Pix.Hash(x, y, 3) > 0.7) g.Rect(X + (int)(Pix.Hash(x, y, 4) * 8), Y + 4, 1, 1, "#6b4a2b");
                    if (t == 'Y')
                    {
                        g.Rect(X, Y, T, T, "#1b120c");
                        g.Rect(X + 1, Y, 8, 6, "#b5523b");
                        g.Rect(X + 2, Y + 1, 6, 4, "#d9734f");
                    }
                }
            }
        if (tomas)
        {
            // Tomas's photo of Mara on the back wall, and a shelf of bottles.
            int fx = (int)PhotoX - 4;
            g.Rect(fx, 3, 9, 11, "#6b4a2b"); g.Rect(fx + 1, 4, 7, 9, "#e8dcc0");
            g.Rect(fx + 2, 8, 2, 4, "#2f5d8a"); g.Rect(fx + 2, 6, 2, 2, "#e0b07d");
            g.Rect(fx + 5, 8, 2, 4, "#e04b3a"); g.Rect(fx + 5, 6, 2, 2, "#c99a5c");
            g.Rect(20, 6, 14, 2, "#7a5230");
            foreach (var (bx, c) in new[] { (21, "#2a9d8f"), (24, "#b5523b"), (27, "#f3c25b"), (30, "#5e6468") })
                g.Rect(bx, 2, 2, 4, c);
        }
    }

    float RoomDoorWX => RoomDoorX * T + 5;
    float RoomDoorWY => (SRows - 1) * T + 4;

    void DrawWindows()
    {
        string glass = Night ? "#1d2f45" : "#bfe0ea", frame = "#5b3a24";
        foreach (int wx in new[] { 3, SCols - 4 })
        {
            if (scene == "house:tomas" && wx == 3) continue;
            int X = wx * T, Y = 2;
            pix.Rect(X, Y, 10, 12, frame);
            pix.Rect(X + 1, Y + 1, 8, 10, glass);
            pix.Rect(X + 5, Y + 1, 1, 10, frame); pix.Rect(X + 1, Y + 6, 8, 1, frame);
            if (Night) pix.Rect(X + 2, Y + 2, 1, 1, "#f3c25b");
        }
    }

    void RenderRoom(float t)
    {
        DrawWindows();
        var list = new List<(float y, Action draw)>();
        foreach (var b in SceneBuilds())
        {
            if (b.id == "rug") DrawBuild(b, t);
            else list.Add((b.y * T + 9, () => DrawBuild(b, t)));
        }
        list.Add((player.Y, DrawPlayer));
        if (scene == "house:tomas" && TomasInBed) list.Add((TomasBedY - 3.5f, DrawTomasAsleep));
        foreach (var o in list.OrderBy(o => o.y)) o.draw();
        DrawParticles();
        if (Night)
        {
            // A dim room at night, lit by lamps, the furnace and the stove.
            Array.Fill(dark, 0.45f);
            LightHole(player.X, player.Y - 6, 30, 0.6f);
            foreach (var b in SceneBuilds())
            {
                var d = Data.BuildById[b.id];
                if (d.Light != null) LightHole(b.x * T + d.Light[0], b.y * T + d.Light[1], d.Light[2], 0.95f);
            }
            ApplyDark(new Color(10, 8, 20, 255));
            foreach (var b in SceneBuilds())
                if (b.id is "lamp" or "furnace" or "stove")
                {
                    var l = Data.BuildById[b.id].Light;
                    pix.Glow(b.x * T + l[0], b.y * T + l[1], l[2] * 0.7f, Pal.Rgba(243, 170, 70, 0.2f));
                }
        }
        DrawGhost(t);
    }

}
