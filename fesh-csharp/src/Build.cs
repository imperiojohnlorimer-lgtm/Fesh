using Raylib_cs;

namespace Fesh;

record struct Box(float X, float Y, float W, float H)
{
    public bool Overlaps(Box b) => X < b.X + b.W && X + W > b.X && Y < b.Y + b.H && Y + H > b.Y;
}

partial class Game
{
    /* ---------- Collision ---------- */
    // Tomas's hut, his campfire, the wreck, Tomas himself, Pip's stall, the carving at the Starwell and the fallen rocks. Only outdoors.
    List<Box> StaticSolids() => scene != "world" ? new() : new()
    {
        new(150, 56, 20, 13),
        new(FireX - 4, FireY - 3, 9, 5),
        new(12, 144, 28, 14),
        TomasSolid ? new(tomasX - 3, tomasY - 3, 7, 3) : new(-100, -100, 0, 0),
        new(PipX - 10, PipY + 1, 20, 9),
        new(CarvingX - 4, CarvingY - 3, 8, 4),
        // The rocks fallen over the rocky shore's tide pools, until you break them up (Chapters.cs).
        RocksCleared ? new(-100, -100, 0, 0) : new(RockfallX - 6, RockfallY - 2, 13, 6)
    };

    // Tomas blocks the way while he's standing at his camp, but not if he came back from bed while you stood on his spot.
    bool TomasSolid => !TomasInBed && !new Box(tomasX - 3, tomasY - 3, 7, 3).Overlaps(new Box(player.X - 3, player.Y - 3, 6, 3));

    List<Box> Solids()
    {
        var list = StaticSolids();
        if (scene == "world") { list.AddRange(IslandSolids()); list.AddRange(HabagatSolids()); }
        foreach (var b in SceneBuilds()) if (BuildBox(b) is Box r) list.Add(r);
        list.AddRange(SchoolRoomSolids());
        if (scene == "cave")
            foreach (var n in nodes) if (!NodeMined(n)) list.Add(new Box(n.X * T + 1, n.Y * T + 2, 8, 7));
        return list;
    }

    static bool Walkable(char t) => t is 's' or 'g' or 'p' or 'd' or 'b' or 'n' or 'e' or 'i' or 'D' or 'j' or '.' or 'Y' or 'F' or 'U' or 'G';

    // Shallow water you can wade into with waders: the sea's shallows, the lagoons, the oasis and the swamp pools.
    static bool Wadeable(char t) => t is 'w' or 'l' or 'o' or 'm';
    // Not while the sea's going out and coming back (RestlessSea.cs): you never step off the shore then.
    bool Wading => scene == "world" && Has("waders") > 0 && !SeaEmergency;
    // On a reef flat the tide has dried out (Tides.cs) you're on wet sand, not in the water.
    bool InWater
    {
        get
        {
            int tx = (int)MathF.Floor(player.X / T), ty = (int)MathF.Floor((player.Y - 1.5f) / T);
            return scene == "world" && Wadeable(TileAt(tx, ty)) && !FlatOpen(tx, ty);
        }
    }

    // Wade lets you into shallow water (waders); swim into any water at all (riding Tidemane, see SwimOk and StormHoldsBack).
    // Flats: you on foot, who can walk out onto Habagat's reef flats while a spring low has dried them (Tides.cs).
    bool CanStand(float x, float y, bool wade = false, bool swim = false, bool flats = false)
    {
        if (swim && StormHoldsBack(x, y)) return false;
        foreach (var (ax, ay) in new[] { (x - 3, y - 3), (x + 2.9f, y - 3), (x - 3, y), (x + 2.9f, y) })
        {
            int tx = (int)MathF.Floor(ax / T), ty = (int)MathF.Floor(ay / T);
            char t = TileAt(tx, ty);
            if (!(Walkable(t) || wade && Wadeable(t) || swim && SwimOk(tx, ty, t) || flats && FlatOk(tx, ty)) || BridgeClosed(tx, ty)) return false;
        }
        foreach (var r in Solids())
            if (x + 3 > r.X && x - 3 < r.X + r.W && y > r.Y && y - 3 < r.Y + r.H) return false;
        return true;
    }

    /* ---------- Building ---------- */
    // Outdoors you build outdoor pieces; inside your own shack you place furniture and crafting stations.
    // The bahay kubo joins the outdoor pieces once you've seen the islands' own houses, in Amihan.
    string[] BuildTools() => InOwnHouse ? Data.IndoorTools : state.Hinted("amihan") ? Data.OutdoorTools : OutdoorToolsBeforeAmihan;
    static readonly string[] OutdoorToolsBeforeAmihan = Data.OutdoorTools.Where(t => t != "kubo").ToArray();

    void ReindexBuilds()
    {
        buildIndex = new();
        foreach (var b in SceneBuilds())
            for (int i = 0; i < Data.BuildById[b.id].W; i++) buildIndex[(b.x + i, b.y)] = b;
    }

    Build BuildAt(int tx, int ty) => buildIndex.GetValueOrDefault((tx, ty));

    static Box? BuildBox(Build b)
    {
        var box = Data.BuildById[b.id].Box;
        return box == null ? null : new Box(b.x * T + box[0], b.y * T + box[1], box[2], box[3]);
    }

    bool CanAfford(BuildDef d) => d.Cost.All(kv => Has(kv.Key) >= kv.Value);

    (int tx, int ty) FrontTile()
    {
        int tx = (int)MathF.Floor(player.X / T), ty = (int)MathF.Floor((player.Y - 1.5f) / T);
        string f = player.Face;
        return (tx + (f == "left" ? -1 : f == "right" ? 1 : 0), ty + (f == "up" ? -1 : f == "down" ? 1 : 0));
    }

    bool InReach(int tx, int ty, int w) => Dist(player.X, player.Y - 2, (tx + w / 2f) * T, ty * T + 5) <= Reach;

    const string BuboWater = "A bubo goes in fresh water: a pond, lake or mangrove pool";

    string PlaceProblem(BuildDef d, int tx, int ty)
    {
        if (!InReach(tx, ty, d.W)) return "Too far away";
        for (int i = 0; i < d.W; i++)
        {
            char t = TileAt(tx + i, ty);
            if (InHouse ? t != '.' : d.Water ? !Wadeable(t) : !Buildable.Contains(t)) return d.Id == "bubo" ? BuboWater : d.Water ? "Crab pots go in shallow water" : "Can't build here";
            if (d.Id == "bubo" && BuboSpot(tx + i, ty) == null) return BuboWater;
            if (InHouse && tx + i == RoomDoorX && ty >= SRows - 3) return "Keep the doorway clear";
            if (BuildAt(tx + i, ty) != null) return "Something is already here";
        }
        var area = new Box(tx * T, ty * T, d.W * T, T);
        if (scene == "world" && IslandSolids().Concat(HabagatSolids()).Any(b => b.Overlaps(area))) return "Keep the island landmarks clear";
        if (scene == "world" && Islanders.Any(n => area.Overlaps(new Box(n.x - 26, n.y - 38, 52, 62)))) return "Keep the village paths clear";
        if (scene == "world" && HabagatFolk.Any(n => area.Overlaps(new Box(n.x - 26, n.y - 38, 52, 62)))) return "Keep the village paths clear";
        if (scene == "world" && area.Overlaps(new Box(JoyX - 12, JoyY - 14, 24, 20))) return "Keep the warden's platform clear";
        if (scene == "world" && d.Water && InSanctuary(tx * T + 5, ty * T + 5)) return "No traps inside the marine sanctuary";
        // Baga's permanent danger zone, the lahar channel, and Baga while it's evacuated (Magayon.cs).
        // Every tile of the piece, not just the first (Codex: a shack could stand half inside the zone).
        for (int i = 0; i < d.W && scene == "world"; i++)
        {
            float cx = (tx + i) * T + 5, cy = ty * T + 5;
            if (Restriction(cx, cy) != null) return InPdz(cx, cy) ? "Not inside the permanent danger zone" : "That's closed: nobody builds there";
            if (ChannelDist(cx, cy) < 12 || Dist(cx, cy, LaharMouthX, LaharMouthY) < 20) return "Keep the lahar channel clear";
            if (Dist(cx, cy, EvacTableX, EvacTableY) < 16 || Dist(cx, cy, AbacaX, AbacaY) < 18 || Dist(cx, cy, PiliX, PiliY) < 10) return "Keep Manay Mila's garden and the jetty path clear";
        }
        if (StaticSolids().Any(r => area.Overlaps(new Box(r.X - 3, r.Y - 3, r.W + 6, r.H + 6)))) return "No room here";
        if (scene == "world" && Dist(tx * T + 5, ty * T + 5, MouthDoorX, MouthDoorY) < 24) return "Keep the cave entrance clear";
        if (scene == "world" && area.Overlaps(new Box(TomasHomeX - 6, TomasHomeY - 6, 13, 9))) return "Keep Tomas's spot clear";
        // He potters about his camp (Folk.cs), so not on top of him wherever he's got to either.
        if (scene == "world" && !TomasInBed && area.Overlaps(new Box(tomasX - 4, tomasY - 4, 9, 5))) return "Tomas is standing there";
        if (BuildBox(new Build { id = d.Id, x = tx, y = ty }) is Box box && box.Overlaps(new Box(player.X - 3, player.Y - 3, 6, 3)))
            return "Step back to make room";
        if (SceneBuilds().Count >= MaxBuilds) return "That's plenty of building for one place";
        if (!CanAfford(d))
            return "Need " + string.Join(" and ", d.Cost.Where(kv => Has(kv.Key) < kv.Value)
                .Select(kv => $"{kv.Value - Has(kv.Key)} more {Items.ById[kv.Key].Name.ToLowerInvariant()}"));
        return "";
    }

    void UpdateGhost()
    {
        var (tx, ty) = hover ?? FrontTile();
        if (buildTool == "remove")
        {
            var t = BuildAt(tx, ty);
            string reason = t == null ? "Nothing to take down here" : !InReach(t.x, t.y, Data.BuildById[t.id].W) ? "Too far away" : "";
            ghost = new Ghost { Tx = tx, Ty = ty, Target = t, Reason = reason };
            return;
        }
        var d = Data.BuildById[buildTool];
        if (d.W > 1 && hover == null && player.Face == "left") tx -= d.W - 1;
        ghost = new Ghost { Tx = tx, Ty = ty, Reason = PlaceProblem(d, tx, ty) };
    }

    void BuildAction()
    {
        UpdateGhost();
        var g = ghost;
        if (g.Reason != "") { Sfx.Play("nope"); Toast(g.Reason, 1.6f); return; }
        var list = SceneBuilds();
        if (buildTool == "remove")
        {
            var b = g.Target;
            var d = Data.BuildById[b.id];
            list.Remove(b);
            string extra = PackUp(b);
            Sfx.Play("remove");
            Toast($"Took down the {d.Name.ToLowerInvariant()}. Got back {d.CostText}.{extra}", extra == "" ? 2 : 3.5f);
        }
        else
        {
            var d = Data.BuildById[buildTool];
            foreach (var (id, n) in d.Cost) Take(id, n);
            list.Add(new Build { id = d.Id, x = g.Tx, y = g.Ty });
            if (d.Water) state.pots[$"{g.Tx},{g.Ty}"] = state.day;
            if (scene == "world")
                state.loose.RemoveAll(l =>
                {
                    bool under = l.ty == g.Ty && l.tx >= g.Tx && l.tx < g.Tx + d.W;
                    if (under) Give(l.kind);
                    return under;
                });
            Sfx.Play("build");
            if (d.Tip != null && !state.Hinted("built:" + d.Id)) { state.hinted["built:" + d.Id] = true; Toast(d.Tip, 3.5f); }
        }
        ReindexBuilds();
        mapTexDirty = true;
        Save();
    }

    // Refunds a piece that has already been taken out of its scene's list. Returns a note about anything else that came back.
    string PackUp(Build b)
    {
        var d = Data.BuildById[b.id];
        if (d.Water) state.pots.Remove($"{b.x},{b.y}");
        foreach (var (id, n) in d.Cost) Give(id, n);
        string extra = "";
        if (b.id == "aquarium" && EmptyTank(TankKey(b)) is int fishBack && fishBack > 0) extra = $" The {fishBack} fish went back in your bag.";
        if (b.id == "dryrack" && state.racks.Remove(RackKey(b), out var load) && load.fish.Count > 0)
        {
            foreach (var f in load.fish) Give(f);
            extra = $" The {load.fish.Count} {RackWhat(load)} on it went back in your bag.";
        }
        // Taking down a shack also packs up everything inside it, aquarium fish included.
        if (d.Door && state.rooms.Remove(ShackKey(b), out var room) && room.Count > 0)
        {
            foreach (var f in room)
            {
                foreach (var (id, n) in Data.BuildById[f.id].Cost) Give(id, n);
                if (f.id == "aquarium") EmptyTank($"{ShackKey(b)}|{f.x},{f.y}");
            }
            extra = $" Packed up {room.Count} thing{(room.Count == 1 ? "" : "s")} from inside, too.";
        }
        return extra;
    }

    // Starfall Atoll used to be a small island further west. Anything built on its old shore is now out at sea:
    // land pieces standing in water, and crab pots in deep water. They go back into the bag.
    int RescueSunkBuilds()
    {
        var sunk = state.builds.Where(b =>
        {
            var d = Data.BuildById[b.id];
            return Enumerable.Range(0, d.W).Any(i => worldMap[b.y, Math.Clamp(b.x + i, 0, COLS - 1)] is var t && (d.Water ? t == '~' : Water.Contains(t)));
        }).ToList();
        foreach (var b in sunk) { state.builds.Remove(b); PackUp(b); }
        if (sunk.Count > 0) { ReindexBuilds(); mapTexDirty = true; Save(); }
        return sunk.Count;
    }

    void EnterBuild()
    {
        if (Aboard) { Toast("Land your boat before building."); return; }
        if (mode == "panel") ClosePanels();
        if (boss != null || guardian != null) { Sfx.Play("nope"); Toast("Not in the middle of a fight!"); return; }
        if (eclipse != null) { Sfx.Play("nope"); Toast("Not now: Bakunawa has the moon!"); return; }
        if (tremor != null || SeaEmergency) { Sfx.Play("nope"); Toast("Not now: up to School Rise!"); return; }
        if (VolcanoControlled) { Sfx.Play("nope"); Toast(BagaEvacuating ? "Not now: to the jetty!" : "Not now: help get the shelter ready."); return; }
        if (mode != "play")
        {
            if (FishingModes.Contains(mode)) Toast("Finish fishing first.");
            return;
        }
        if (scene == "cave" || scene == "house:tomas" || scene == "house:school")
        {
            Sfx.Play("nope");
            Toast(scene == "cave" ? "You can't build down here." : scene == "house:school" ? "This is the school, and today it's the shelter." : "This is Tomas's place. Build a shack of your own to furnish.", 3);
            return;
        }
        if (!BuildTools().Contains(buildTool)) buildTool = BuildTools()[0];
        mode = "build";
        hover = null;
        Sfx.Play("ui");
        if (!state.Hinted("buildHelp"))
        {
            state.hinted["buildHelp"] = true;
            Toast("Press <act> to build in front of you, or click a spot nearby. Number keys pick what to build.", 5);
        }
    }

    void ExitBuild()
    {
        if (mode != "build") return;
        mode = "play";
        hover = null;
        ghost = null;
        Save();
    }

    void ToggleBuild()
    {
        if (Aboard) { Toast("Land your boat before building."); return; }
        if (mode == "build") ExitBuild();
        else EnterBuild();
    }

    void SelectTool(string id)
    {
        if (buildTool == id) return;
        buildTool = id;
        Sfx.Play("blip");
    }

    /* ---------- Driftwood and stones ---------- */
    HashSet<(int, int)> Reachable()
    {
        int sx = (int)MathF.Floor(player.X / T), sy = (int)MathF.Floor((player.Y - 1.5f) / T);
        var seen = new HashSet<(int, int)> { (sx, sy) };
        var queue = new Stack<(int, int)>();
        queue.Push((sx, sy));
        while (queue.Count > 0)
        {
            var (x, y) = queue.Pop();
            foreach (var (ox, oy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var n = (x + ox, y + oy);
                if (!Walkable(TileAt(n.Item1, n.Item2)) || !seen.Add(n)) continue;
                queue.Push(n);
            }
        }
        return seen;
    }

    // Driftwood washes up on beaches, stones turn up on any open ground and worm mounds in grass, always near the player.
    bool SpawnLoose(string kind)
    {
        if (scene != "world" || kind == "worm" && quietWildlife) return false;
        // Gleaning finds only turn up at low tide, on Habagat's beaches by the water.
        if (kind == "glean" && (!LowTide || !InHabagat || !GleanLeft)) return false;
        var cands = new List<(int x, int y)>();
        int px = (int)(player.X / T), py = (int)(player.Y / T);
        // Gleaning finds also turn up out on the reef flats while a spring low has dried them.
        var where = kind == "glean" && FlatsDry ? Reachable().Concat(Flats().Where(f => Math.Abs(f.Item1 - px) <= 15 && Math.Abs(f.Item2 - py) <= 9)) : Reachable();
        foreach (var (x, y) in where)
        {
            if (Math.Abs(x - px) > 15 || Math.Abs(y - py) > 9) continue;
            char t = map[y, x];
            if (kind == "glean" ? !GleanGround(x, y) : kind == "wood" ? !WoodGround.Contains(t) : kind == "worm" ? t is not ('g' or 'j') : !Buildable.Contains(t)) continue;
            if (BuildAt(x, y) != null || state.loose.Any(l => l.tx == x && l.ty == y)) continue;
            if (Dist(x * T + 5, y * T + 6, player.X, player.Y) < 24 || !CanStand(x * T + 5, y * T + 7, flats: kind == "glean")) continue;
            cands.Add((x, y));
        }
        if (cands.Count == 0) return false;
        var (cx, cy) = cands[rng.Next(cands.Count)];
        state.loose.Add(new Loose { kind = kind, tx = cx, ty = cy, x = cx * T + 3 + rng.Next(4), y = cy * T + 4 + rng.Next(3) });
        return true;
    }

    int LooseCount(string kind) => state.loose.Count(l => l.kind == kind);

    void FillLoose()
    {
        if (scene != "world") return;
        state.loose.RemoveAll(l => Dist(l.x, l.y, player.X, player.Y) > 400);
        foreach (var kind in LooseMax.Keys)
            while (LooseCount(kind) < LooseMax[kind] && SpawnLoose(kind)) { }
    }

    void TickLoose(float dt)
    {
        looseTimer += dt;
        // The tide coming in (or a tide gleaned clean) takes what's left on the flats straight away.
        if (!LowTide || !GleanLeft) state.loose.RemoveAll(l => l.kind == "glean");
        // ...and the water coming back over the outer flats takes what was out there.
        else if (scene == "world" && !FlatsDry) state.loose.RemoveAll(l => l.kind == "glean" && IsFlat(l.tx, l.ty));
        if (looseTimer > 9)
        {
            looseTimer = 0;
            // Anything left far behind washes away, so new finds turn up where the player is.
            state.loose.RemoveAll(l => Dist(l.x, l.y, player.X, player.Y) > 400);
            var need = LooseMax.Keys.Where(k => LooseCount(k) < LooseMax[k]).ToArray();
            if (need.Length > 0) SpawnLoose(need[rng.Next(need.Length)]);
            // At low tide on Habagat, a second try for the flats so they fill up while you walk them.
            if (LooseCount("glean") < LooseMax["glean"]) SpawnLoose("glean");
        }
        for (int i = state.loose.Count - 1; i >= 0; i--)
        {
            var l = state.loose[i];
            if (l.kind == "worm" || Dist(player.X, player.Y - 2, l.x, l.y) > 7) continue;
            if (l.kind == "glean" && (!LowTide || !GleanLeft)) continue;   // the top of the next frame clears it
            state.loose.RemoveAt(i);
            if (l.kind == "glean") { PickGlean(l); continue; }
            Give(l.kind);
            Learned("gather");
            Sfx.Play("pickup");
            if (!state.Hinted("buildTip"))
            {
                state.hinted["buildTip"] = true;
                Toast($"{(l.kind == "wood" ? "Driftwood" : "A good flat stone")}! Press <build> to make things with it.", 4);
            }
            else Toast($"+1 {l.kind} ({Has(l.kind)} in your bag)", 1.4f);
        }
    }
}
