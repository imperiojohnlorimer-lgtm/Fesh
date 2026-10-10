using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// The hotbar (1.20): six slots along the bottom of the screen for what you carry in your hands. A slot holds a tool
// family ("rod", "axe", "pick", "sword", "spear": always your best of that kind, so an upgrade needs no shuffling) or a
// food item. Keys 1-6 pick a slot (the same key again puts it away), the mouse wheel and a flick of the right stick step
// through them, and a click picks one. What you hold is drawn in your hand (CarryPose).
// Using it: <act> when there's nothing in front of you, or <use> (and a click on the world, or B on a gamepad) any time.
// Tools swing, and still chop, mine or strike what's in front; the rod casts wherever you aim (CastFree), into any water;
// food is eaten. Contextual actions (chopping a tree with E, fishing a named spot) work as before whatever you hold.
partial class Game
{
    public const int HotbarSlots = 6;
    static readonly string[] ToolFamilies = { "rod", "axe", "pick", "sword", "spear" };
    float hotbarTipT;              // how long the held item's name shows over the hotbar after picking it
    float lastHotbarTop;           // where the hotbar's top edge was last drawn (the prompt sits above it)

    // Which family an item belongs to on the hotbar, or the item itself (food), or null if it can't go there.
    static string HotbarKey(string id)
    {
        if (id == null || !Items.ById.TryGetValue(id, out var d)) return null;
        if (d.Kind == "rod") return "rod";
        if (id == "axe") return "axe";
        if (Items.Pickaxes.Contains(id)) return "pick";
        if (d.Kind == "weapon") return "sword";
        if (id == "spear") return "spear";
        return d.Food > 0 ? id : null;
    }

    // What a slot holds right now: your best of that family, or the food item, if you have one; null if not.
    string SlotItem(string key) => key switch
    {
        null or "" => null,
        "rod" => BestRod(),
        "axe" => Has("axe") > 0 ? "axe" : null,
        "pick" => PickTier > 0 ? Items.Pickaxes[PickTier - 1] : null,
        "sword" => Items.Weapons.Where(w => Has(w.id) > 0).Select(w => w.id).FirstOrDefault(),
        "spear" => Has("spear") > 0 ? "spear" : null,
        _ => Has(key) > 0 ? key : null
    };

    string HeldKey => state.hotbar != null && state.held is >= 0 and < HotbarSlots ? state.hotbar[state.held] : null;
    string HeldItem => SlotItem(HeldKey);
    // The tool drawn in your hand (Tools.cs names): null for food or empty hands.
    string HeldTool => HeldItem == null ? null : HeldKey switch { "rod" => "rod", "axe" => "axe", "pick" => "pick", "sword" => "sword", "spear" => "spear", _ => null };

    // Older saves have no hotbar: it starts with the tools you own, hands free. Anything odd in a saved one is tidied.
    void EnsureHotbar()
    {
        if (state.hotbar == null)
        {
            state.hotbar = new string[HotbarSlots];
            int i = 0;
            foreach (var key in ToolFamilies)
                if (SlotItem(key) != null && i < HotbarSlots) state.hotbar[i++] = key;
            state.held = -1;
        }
        if (state.hotbar.Length != HotbarSlots) Array.Resize(ref state.hotbar, HotbarSlots);
        for (int i = 0; i < HotbarSlots; i++)
            if (state.hotbar[i] is string k && !ToolFamilies.Contains(k) && HotbarKey(k) != k) state.hotbar[i] = null;
        if (state.held is < -1 or >= HotbarSlots) state.held = -1;
    }

    // A new kind of tool goes into the first empty slot (once: a slot you emptied stays empty until you get a new kind).
    void HotbarGained(string id)
    {
        if (state.hotbar == null || HotbarKey(id) is not string key || !ToolFamilies.Contains(key) || state.hotbar.Contains(key)) return;
        int free = Array.IndexOf(state.hotbar, null);
        if (free < 0) free = Array.FindIndex(state.hotbar, k => k == "");
        if (free >= 0) state.hotbar[free] = key;
    }

    void AssignHotbar(int slot, string id)
    {
        if (HotbarKey(id) is not string key) return;
        int was = Array.IndexOf(state.hotbar, key);
        if (was >= 0) state.hotbar[was] = state.hotbar[slot];   // swap with wherever it was
        state.hotbar[slot] = key;
        Sfx.Play("ui");
    }

    void SelectSlot(int i)
    {
        if (state.hotbar == null) return;
        state.held = state.held == i ? -1 : i;
        hotbarTipT = 1.6f;
        Sfx.Play("blip");
    }

    void CycleSlot(int step)
    {
        if (state.hotbar == null) return;
        state.held = state.held < 0 ? (step > 0 ? 0 : HotbarSlots - 1) : (state.held + step + HotbarSlots) % HotbarSlots;
        hotbarTipT = 1.6f;
        Sfx.Play("blip");
    }

    // Plain play only: the number keys, the wheel, the right stick, and the use key. True if it took the input.
    bool HotbarKeys()
    {
        if (mode != "play" || state.hotbar == null) return false;
        if (DigitPressed() is int d && d < HotbarSlots) { SelectSlot(d); return true; }
        float wheel = GetMouseWheelMove();
        if (wheel != 0 && !Gfx.OverUiPrev) { CycleSlot(wheel > 0 ? -1 : 1); return true; }
        if (Inp.RightFlick != 0) { CycleSlot(Inp.RightFlick); return true; }
        if (Bind.Pressed("use") || Inp.PadPressed(GamepadButton.RightFaceRight)) { UseHeld(quick: Inp.PadPressed(GamepadButton.RightFaceRight)); return true; }
        return false;
    }

    // What <act> would do with what you hold when there's nothing in front of you (null: nothing).
    string UseLabel()
    {
        if (HeldItem is not string id || Aboard) return null;
        return HeldKey switch
        {
            "rod" => "Cast your line (hold to cast further)",
            "axe" => "Swing your axe",
            "pick" => "Swing your pickaxe",
            "sword" => $"Swing your {Items.ById[id].Name.ToLowerInvariant()}",
            "spear" => "Thrust your spear",
            _ => $"Eat {Items.Amount(id, 1).ToLowerInvariant()}"
        };
    }

    // The prompt's hint for it: just after you pick something, and with the rod whenever a cast would land in water.
    string HeldUseHint() => mode == "play" && (hotbarTipT > 0 || HeldKey == "rod" && !Aboard && FreeLanding(0.6f).water) ? UseLabel() : null;

    // Uses what's in your hands. Something in front that the tool is for (a tree for the axe, a rock or ore for the
    // pickaxe, a monster or Tidemane for a blade, a fishing spot for the rod, reef shallows for the spear) gets it;
    // otherwise a tool swings at the air, the rod casts where you face, and food is eaten. quick: a gamepad's B, which
    // can't be held to power up a cast, so it casts at middling strength.
    void UseHeld(bool quick = false)
    {
        if (mode != "play" || HeldItem is not string id) return;
        // Not while everyone's heading to safety (RestlessSea.cs, Magayon.cs): no casting from the jetty mid-evacuation.
        if (tremor != null || SeaEmergency || VolcanoControlled) { Sfx.Play("nope"); return; }
        string t = target?.Type;
        switch (HeldKey)
        {
            case "rod":
                if (t == "spot" || Aboard || Swimming && target != null) { OnAction(); return; }
                if (quick) { CastFree(0.6f); return; }
                StartFreeCharge();
                return;
            case "axe":
                if (t == "tree" && TileAt(target.Tx, target.Ty) != 'R') { HitTree(target.Tx, target.Ty); return; }
                AirSwing("axe");
                return;
            case "pick":
                if (t == "tree" && TileAt(target.Tx, target.Ty) == 'R' || t is "node" or "drill") { OnAction(); return; }
                AirSwing("pick");
                return;
            case "sword":
                if (t is "monster" or "boss" or "guardian") { OnAction(); return; }
                AirSwing("sword");
                return;
            case "spear":
                if (target?.Alt2Type == "spear") { StartSpear(target.Id); return; }
                AirSwing("throw");
                return;
            default:
                Eat(id);
                return;
        }
    }

    // A swing at nothing in particular: the arms and the tool do the whole stroke, with a swish.
    void AirSwing(string tool)
    {
        if (swingT > 0.05f) return;
        Swing(tool, tool == "throw" ? 0.25f : 0.28f);
        Sfx.Play("swish");
    }

    /* ---------- Fishing anywhere ---------- */
    // Hold to power up a cast in the way you're facing (no named spot: chargeSpot stays null).
    void StartFreeCharge()
    {
        chargeSpot = null;
        charge = 0;
        chargeDir = 1;
        mode = "charging";
        if (!state.Hinted("freeCast"))
        {
            state.hinted["freeCast"] = true;
            Toast("With your rod in hand you can cast into any water. The fishing spots still have the best (and the strangest) fish.", 5);
        }
    }

    // Where a cast you aim yourself lands: straight ahead, further the harder you throw; if that's dry ground, the
    // furthest water short of it along the line; if there's none, it lands on the ground (dry: no fishing).
    (float x, float y, bool water) FreeLanding(float power)
    {
        float dx = player.Face == "left" ? -1 : player.Face == "right" ? 1 : 0, dy = player.Face == "up" ? -1 : player.Face == "down" ? 1 : 0;
        float ox = player.X, oy = player.Y - 4, reach = 18 + power * 52;
        for (float r = reach; r >= 10; r -= 2)
            if (IsWater(ox + dx * r, oy + dy * r)) return (ox + dx * r, oy + dy * r, true);
        return (ox + dx * reach, oy + dy * reach, false);
    }

    // Which fish live in the water at (x, y): the named spot whose water it is. Fresh water by the pools the bubo knows
    // (BuboWaters); the open sea by its region; salt shallows by the nearest open spot on the same island group within
    // reach; a cave pool by the floor's own. Never the Starwell or the ice hole (their fish are theirs alone). Null:
    // nothing to catch here.
    string WildSpot(float x, float y)
    {
        int tx = (int)MathF.Floor(x / T), ty = (int)MathF.Floor(y / T);
        if (scene == "cave")
            return Data.Spots.Where(s => s.Scene == "cave" && SpotHere(s)).OrderBy(s => { var (sx, sy) = SpotPos(s); return Dist(sx, sy, x, y); }).Select(s => s.Id).FirstOrDefault();
        if (scene != "world") return null;
        if (BuboSpot(tx, ty) is string fresh) return fresh == "amihanpond" && PondClosed ? null : fresh;
        char t = TileAt(tx, ty);
        if (t is 'l' or 'o' or 'm' && !BuboWaters().ContainsKey((tx, ty)) && !Data.Spots.Any(s => s.Scene == "world" && !FreshSpots.Contains(s.Id) && Dist(s.X, s.Y, x, y) < 200))
            return null;   // a little pool of fresh water no spot shares: nothing in it
        // Salt water: the nearest open spot on the same island group. Water shut inside an island (the Starwell, a
        // lagoon) is that island's whatever the distance; the deep open sea, away from any spot, is the open sea's.
        byte biome = BiomeAt(tx, ty);
        bool open = OpenSeaTiles()[ty, tx];
        var near = Data.Spots
            .Where(s => s.Scene == "world" && s.Id is not ("starwell" or "icehole") && !FreshSpots.Contains(s.Id) && SpotOpen(s.Id) && SpotKnown(s)
                && BiomeAt((int)(s.X / T), (int)(s.Y / T)) == biome)
            .OrderBy(s => Dist(s.X, s.Y, x, y)).FirstOrDefault();
        float d = near == null ? float.MaxValue : Dist(near.X, near.Y, x, y);
        if (!open) return near?.Id;
        if (t == '~') return d < 120 ? near.Id : SeaSpotAt(x, y);
        return d < 260 ? near.Id : SeaSpotAt(x, y);
    }

    // The water joined to the edge of the map (as the chart works it out): everything else is a lake, lagoon or blue
    // hole inside an island. Worked out once per map.
    bool[,] openSea;
    char[,] openSeaFor;
    bool[,] OpenSeaTiles()
    {
        if (openSea != null && openSeaFor == worldMap) return openSea;
        openSeaFor = worldMap;
        openSea = new bool[ROWS, COLS];
        var queue = new Queue<(int x, int y)>();
        for (int x = 0; x < COLS; x++) { queue.Enqueue((x, 0)); queue.Enqueue((x, ROWS - 1)); }
        for (int y = 0; y < ROWS; y++) { queue.Enqueue((0, y)); queue.Enqueue((COLS - 1, y)); }
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            if (x < 0 || y < 0 || x >= COLS || y >= ROWS || openSea[y, x] || worldMap[y, x] is not ('~' or 'w' or 'T' or 'x' or 'r' or 'I' or 'b' or 'd')) continue;
            openSea[y, x] = true;
            queue.Enqueue((x + 1, y)); queue.Enqueue((x - 1, y)); queue.Enqueue((x, y + 1)); queue.Enqueue((x, y - 1));
        }
        return openSea;
    }

    // The open sea's fish by where the water is (SeaSpotHere goes by where you are).
    string SeaSpotAt(float x, float y) => x >= EastStart * T ? "amihansea" : y >= HabagatTop * T ? "habagatsea" : "opensea";

    void CastFree(float power)
    {
        var (lx, ly, water) = FreeLanding(power);
        if (water && scene == "world" && InSanctuary(lx, ly))
        {
            mode = "play";
            Sfx.Play("nope");
            Toast("No fishing in the marine sanctuary. Turn away from the buoys.");
            return;
        }
        string spot = water ? WildSpot(lx, ly) : null;
        state.casts++;
        var tip = RodTip();
        // No bait is used on a cast that can't catch anything.
        string bait = spot != null ? NextBait() : null;
        if (bait != null && !Items.Baits[bait].Reusable) Take(bait);
        // A little slower than a named spot (where the fish gather), and no chum, shadows or sanctuary spill.
        float wait = Rand(2.2f, 4.6f) * Rod.Bite * WeatherBite * (bait != null ? Items.Baits[bait].Bite : 1) * (Perk(3) ? 0.9f : 1);
        fish = new FishCast
        {
            Spot = spot ?? "", Sx = tip.X, Sy = tip.Y, Tx = lx, Ty = ly, Bx = tip.X, By = tip.Y, Bait = bait, Power = power,
            Depth = CastDepth(power), Timer = wait, Wild = true, Dry = !water, Empty = water && spot == null
        };
        mode = "casting";
        Sfx.Play("cast");
    }

    /* ---------- On screen ---------- */
    bool HotbarShown => state?.hotbar != null && mode != "build" && ActiveModes.Contains(mode) && eclipse == null;

    // Six slots along the bottom: the one in your hands lit up, each with its key; hovering one names it.
    void DrawHotbar()
    {
        const float s = 50, gap = 6, m = 12;
        float w = HotbarSlots * s + (HotbarSlots - 1) * gap, x0 = (Gfx.LW - w) / 2, y = Gfx.LH - m - s;
        lastHotbarTop = y;
        string tip = null;
        for (int i = 0; i < HotbarSlots; i++)
        {
            float x = x0 + i * (s + gap);
            string key = state.hotbar[i], id = SlotItem(key);
            bool sel = state.held == i;
            Gfx.Box(x, y, s, s, sel ? Pal.Lantern : Pal.WithAlpha(Navy, 0.88f), sel ? Pal.Ink : Pal.C("rgba(241,230,200,0.35)"), 2, 6);
            if (id != null) DrawIcon(id, x + 8, y + 8, 34);
            Gfx.Text((i + 1).ToString(), x + 5, y + 2, FontKind.Ui700, 13, sel ? Pal.Ink : Pal.WithAlpha(Pal.Paper, 0.8f));
            if (id != null && !ToolFamilies.Contains(key))
            {
                string n = Has(id).ToString();
                Gfx.Text(n, x + s - 5 - Gfx.Measure(n, FontKind.Ui700, 13), y + s - 17, FontKind.Ui700, 13, sel ? Pal.Ink : Pal.Paper);
            }
#if DEBUG
            Gfx.Seen["hotbar:" + (i + 1)] = new Rectangle(x, y, s, s);
#endif
            if (Gfx.Click(x, y, s, s) && mode == "play") SelectSlot(i);
            if (Gfx.Hover(x, y, s, s)) tip = id != null ? Items.ById[id].Name : key != null ? "None left" : Bind.Fix("Empty: put a tool or food here from your bag (<bag>)");
        }
        // What's in your hands, for a moment after you pick it (or the slot you point at), to the right of the bar.
        if (tip == null && hotbarTipT > 0) tip = HeldItem != null ? Items.ById[HeldItem].Name : "Hands free";
        if (tip == null) return;
        float tw = Gfx.Measure(tip, FontKind.Ui600, 16) + 20, tx = Math.Min(x0 + w + 10, Gfx.LW - 8 - tw);
        Gfx.Rect(tx, y + 12, tw, 26, NavyStrong, 5);
        Gfx.Text(tip, tx + 10, y + 16, FontKind.Ui600, 16, Pal.Paper);
    }

    // On the bag card of a tool or food: which slot it's in, and a button for each slot to put it there.
    void DrawHotbarAssign(float x, float y, string id)
    {
        if (state.hotbar == null || HotbarKey(id) is not string key) return;
        int at = Array.IndexOf(state.hotbar, key);
        Gfx.Text(at >= 0 ? $"On your hotbar (key {at + 1}). Move it:" : "Put it on your hotbar:", x, y, FontKind.Ui600, 16, at >= 0 ? Pal.C("#3f7d35") : Muted);
        for (int i = 0; i < HotbarSlots; i++)
        {
            float bx = x + i * 44, by = y + 24;
            bool here = at == i, hover = Gfx.Hover(bx, by, 38, 34);
            Gfx.Box(bx, by, 38, 34, here ? Pal.Lantern : hover ? Lighten(Pal.Sand, 0.14f) : Pal.Sand, Pal.Ink, 2, 5, 2);
            Gfx.TextCenter((i + 1).ToString(), bx + 19, by + 7, FontKind.Ui700, 18, Pal.Ink);
#if DEBUG
            Gfx.Seen["hotbar-set:" + (i + 1)] = new Rectangle(bx, by, 38, 34);
#endif
            if (Gfx.Click(bx, by, 38, 34)) AssignHotbar(i, id);
        }
    }

    // Holding a tool while you walk about: your hand down at your side, the tool held up and a little forward.
    HandPose CarryPose(string face, int ux, int uy, string tool)
    {
        var near = LookData.NearShoulder(ux, uy, face);
        var (ax, ay) = Project(face, -65);
        var hand = ((int)MathF.Round(near.x + ax * 4), (int)MathF.Round(near.y + ay * 4));
        float elev = tool switch { "rod" => 72, "spear" => 80, "sword" => 50, _ => 58 };
        var (tx, ty) = Project(face, Snap(elev));
        return new HandPose
        {
            Hand = hand, Tool = tool, Dx = tx, Dy = ty, Length = tool switch { "rod" => 11, "spear" => 10, _ => 6 },
            ToolBehind = face == "up", ArmsBehind = face == "up",
            Tint = tool == "pick" ? Items.ById[Items.Pickaxes[Math.Clamp(PickTier, 1, Items.Pickaxes.Length) - 1]].Tint
                : tool == "sword" ? Items.Weapons.Where(w => Has(w.id) > 0).Select(w => Items.ById[w.id].Tint).FirstOrDefault()
                : tool == "rod" && Items.ById.TryGetValue(BestRod(), out var r) ? r.Tint : null
        };
    }
}
