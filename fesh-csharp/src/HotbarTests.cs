#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // The hotbar, fishing anywhere, the shop's descriptions and the title (1.20). FESH_HOTBAR_TEST=1 runs only this.
    IEnumerable<int> HotbarScript()
    {
        Note("The hotbar, fishing anywhere, the shop and the title");
        Inp.ScriptMouse = Offscreen;

        /* ---------- Starting out ---------- */
        state = new State { created = true, look = new Look { name = "Holder" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        state.food = 60; state.hp = 100;
        yield return 3;
        Check($"a new game starts with the rod on the hotbar and your hands free ({string.Join(",", state.hotbar.Select(k => k ?? "-"))}, {state.held})",
            state.hotbar.Length == HotbarSlots && state.hotbar[0] == "rod" && state.held == -1);
        Check($"the hotbar is drawn along the bottom ({lastHotbarTop:0})", HotbarShown && lastHotbarTop > Gfx.LH - 80);
        pendingShot = "hotbar-01-start"; yield return 2;

        // An older save (no hotbar) gets one from the tools it owns, in order, hands free.
        var old = new State { created = true, look = new Look { name = "Old" }, hotbar = null };
        old.flags.metTomas = true;
        old.inv["axe"] = 1; old.inv["iron_pickaxe"] = 1; old.inv["copper_sword"] = 1;
        state = old; StartGame(false); quietWildlife = true; yield return 2;
        Check($"an older save gets a hotbar of the tools it owns ({string.Join(",", state.hotbar.Select(k => k ?? "-"))})",
            state.hotbar.Take(4).SequenceEqual(new[] { "rod", "axe", "pick", "sword" }) && state.hotbar[4] == null && state.held == -1);
        Check("the pickaxe slot holds your best pickaxe", SlotItem("pick") == "iron_pickaxe");
        // Getting a better one doesn't take another slot; getting a new kind of tool fills an empty one.
        Give("crystal_pickaxe");
        Give("spear");
        Check($"a better pickaxe replaces the old in its slot, a new kind of tool takes an empty one ({string.Join(",", state.hotbar.Select(k => k ?? "-"))})",
            SlotItem("pick") == "crystal_pickaxe" && state.hotbar.Count(k => k == "pick") == 1 && state.hotbar[4] == "spear");

        /* ---------- Picking what you hold ---------- */
        var open = OpenGround();
        player.X = open.x; player.Y = open.y; player.Face = "right"; yield return 3;
        Inp.Tap(KeyboardKey.Two); yield return 2;
        Check($"2 takes the axe in hand ({HeldItem})", state.held == 1 && HeldItem == "axe" && HeldTool == "axe");
        var hp = HandsPose("right", (int)player.X, (int)player.Y, false, 0);
        Check($"you carry it in your hand ({hp?.Tool})", hp?.Tool == "axe");
        pendingShot = "hotbar-02-axe-in-hand"; yield return 2;
        foreach (var face in new[] { "left", "up", "down" })
            if (HandsPose(face, (int)player.X, (int)player.Y, false, 0)?.Tool != "axe") Check($"carried facing {face}", false);
        Inp.Tap(KeyboardKey.Two); yield return 2;
        Check("2 again puts it away", state.held == -1 && HeldItem == null && HandsPose("right", 100, 100, false, 0) == null);
        ClickButton("hotbar:3"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check($"clicking a slot takes it in hand ({state.held}, {HeldItem})", state.held == 2 && HeldItem == "crystal_pickaxe");
        Inp.ScriptRightX = 1; yield return 2; Inp.ScriptRightX = 0; yield return 2;
        Check($"a flick of the right stick steps to the next slot ({state.held})", state.held == 3);
        Inp.ScriptRightX = 1; yield return 4;
        Check("held over, it only steps once", state.held == 4);
        Inp.ScriptRightX = null; yield return 1;

        /* ---------- Swinging at nothing ---------- */
        player.X = open.x; player.Y = open.y; player.Face = "right"; yield return 3;
        Inp.Tap(KeyboardKey.Two); yield return 2;
        var felled = state.felled.Count;
        Inp.Tap(KeyboardKey.E); yield return 2;
        Check($"with nothing in front, E swings the axe at the air ({swingTool}, {swingT:0.00})", swingT > 0 && swingTool == "axe" && state.felled.Count == felled && mode == "play");
        pendingShot = "hotbar-03-air-swing"; yield return 2;
        yield return 20;
        Inp.Tap(KeyboardKey.V); yield return 2;
        Check("so does the use key", swingT > 0 && swingTool == "axe");
        yield return 20;
        // Facing a tree, the use key chops it.
        if (TreeToChop() is (int tx, int ty, float px, float py, string tf))
        {
            player.X = px; player.Y = py; player.Face = tf; yield return 3;
            chopHits = 0; chopTile = (-1, -1);
            Inp.Tap(KeyboardKey.V); yield return 2;
            Check($"facing a tree, the use key chops it ({chopHits} hit)", chopHits == 1 && chopTile == (tx, ty));
            yield return 20;
        }
        else Note("no tree to chop found");
        // The pickaxe swings too, and a sword (with nothing to hit).
        player.X = open.x; player.Y = open.y; yield return 3;
        Inp.Tap(KeyboardKey.Three); yield return 2; Inp.Tap(KeyboardKey.E); yield return 2;
        Check($"the pickaxe swings ({swingTool})", swingTool == "pick" && swingT > 0);
        yield return 20;
        Inp.Tap(KeyboardKey.Four); yield return 2; Inp.Tap(KeyboardKey.E); yield return 2;
        Check($"and the sword ({swingTool})", swingTool == "sword" && swingT > 0);
        yield return 20;
        // A click on the world uses it too.
        Inp.Tap(KeyboardKey.Two); yield return 2;
        Inp.ScriptMouse = new System.Numerics.Vector2(Gfx.LW / 2 + 60, Gfx.LH / 2); yield return 2;
        Inp.ScriptClickNext = true; yield return 2;
        Check($"a click on the world swings what you hold ({swingTool})", swingTool == "axe" && swingT > 0);
        Inp.ScriptMouse = Offscreen; yield return 20;

        /* ---------- Fishing anywhere ---------- */
        Inp.Tap(KeyboardKey.One); yield return 2;
        Check($"1 takes the rod ({HeldItem})", HeldItem == BestRod() && HeldTool == "rod");
        // Salt shallows with no fishing spot about: the cast still lands, and fish from the nearest spot of that water bite.
        if (FreeShore((x, y) => TileAt(x, y) == 'w' && BuboSpot(x, y) == null) is (float sx, float sy, string sface))
        {
            player.X = sx; player.Y = sy; player.Face = sface; yield return 3;
            Check($"facing water with no spot about, the prompt offers a cast ({prompt.Text})", target == null && prompt.Text.Contains("Cast"));
            Inp.Hold(KeyboardKey.E, true); yield return 20; Inp.Hold(KeyboardKey.E, false);
            for (int i = 0; i < 90 && mode is "charging" or "casting"; i++) yield return 1;
            Check($"holding and letting go of E casts into the water ({mode}, {fish?.Spot})", mode == "waiting" && fish is { Wild: true, Dry: false } && Data.SpotById.ContainsKey(fish.Spot)
                && fish.Spot is not ("starwell" or "icehole") && !FreshSpots.Contains(fish.Spot));
            pendingShot = "hotbar-04-free-cast"; yield return 2;
            // Only everyday fish (and now and then a chest) bite on a cast like this.
            var spot = fish.Spot;
            var rolls = Enumerable.Range(0, 400).Select(_ => RollCatch(spot)).ToList();
            Check($"only the spot's everyday fish bite there (of {rolls.Select(r => r?.Id).Distinct().Count()} kinds)",
                rolls.All(r => r != null && !r.Exotic && !r.Odd && !r.Boss && (r.Chest || Data.Common[spot].Any(f => f.Id == r.Id && !f.Legend))));
            fish.Timer = 0; yield return 2;
            Check($"and they do bite ({mode})", mode == "bite");
            Inp.Tap(KeyboardKey.E); yield return 2;
            if (mode == "reeling") { reel.Progress = 1.2f; reel.Running = 0; reel.Leap = 0; yield return 3; }
            for (int i = 0; i < 60 && mode is "reeling" or "chest"; i++) yield return 1;
            Check($"and it comes in like any other catch ({mode})", mode is "catch" or "play" or "chest" or "dialogue");
            while (mode is "catch" or "odd" or "dialogue") { Inp.Tap(KeyboardKey.E); yield return 3; }
            mode = "play"; fish = null; yield return 2;
        }
        else Check("found salt shallows with no spot about", false);
        // Fresh water: the fish of that pond or lake.
        if (FreeShore((x, y) => BuboSpot(x, y) != null) is (float fx, float fy, string fface))
        {
            player.X = fx; player.Y = fy; player.Face = fface; yield return 3;
            CastFree(0.5f);
            Check($"into fresh water, its own fish ({fish?.Spot})", fish != null && FreshSpots.Contains(fish.Spot));
            fish = null; mode = "play"; yield return 2;
        }
        else Note("no fresh shore without a spot found");
        // Dry land: the bobber lands on the ground and you reel in, with no bait used.
        player.X = open.x; player.Y = open.y; player.Face = "right"; yield return 3;
        Give("worm", 3); state.tackle["bait"] = "worm";
        int worms = Has("worm");
        if (!FreeLanding(1f).water)
        {
            CastFree(1f);
            for (int i = 0; i < 90 && mode == "casting"; i++) yield return 1;
            Check($"a cast onto dry land comes back with no bait used ({toastMsg})", mode == "play" && fish == null && toastMsg.Contains("dry ground") && Has("worm") == worms);
        }
        else Note("open ground faces water: dry cast skipped");
        state.tackle["bait"] = "auto";
        // The Starwell's water only ever gives the atoll's everyday fish, never what rises there for a coconut.
        state.hinted["starwell"] = true; SetNight(true); Give("coconut", 2);
        player.X = StarwellX - 30; player.Y = StarwellY; player.Face = "right"; yield return 3;
        CastFree(0.3f);
        if (fish is { Wild: true } wf)
        {
            fish.Bait = "coconut";
            Check($"a free cast into the Starwell isn't the Starwell ({wf.Spot}) and never brings it up",
                wf.Spot != "starwell" && Enumerable.Range(0, 300).All(_ => RollCatch(wf.Spot) is not { Boss: true }));
        }
        else Check($"a free cast reaches the Starwell ({mode})", false);
        fish = null; mode = "play"; SetNight(false); yield return 2;
        // The sanctuary is still no fishing.
        player.X = (SanctCX - SanctRX) * T - 30; player.Y = SanctCY * T; player.Face = "right";
        state.aboard = false; state.tamed = true; state.riding = true; yield return 3;
        CastFree(1f);
        Check($"no free cast into the marine sanctuary ({toastMsg})", fish == null && toastMsg.Contains("sanctuary"));
        state.riding = false; state.tamed = false; mode = "play"; yield return 2;
        // A named spot is still a named spot: holding the rod (or anything), E there fishes it the old way.
        player.X = open.x; player.Y = open.y;   // StandNear searches from where you are
        var (lpx, lpy) = StandNear("lagoon");
        player.X = lpx; player.Y = lpy; yield return 3;
        Inp.Tap(KeyboardKey.Two); yield return 2;
        Check($"with the axe in hand, a fishing spot still takes E ({target?.Type} {target?.Id})", target?.Type == "spot");
        Inp.Hold(KeyboardKey.E, true); yield return 10; Inp.Hold(KeyboardKey.E, false);
        for (int i = 0; i < 60 && mode is "charging" or "casting"; i++) yield return 1;
        Check($"and E there casts at the spot as before ({fish?.Spot}, wild {fish?.Wild})", fish != null && fish.Spot == "lagoon" && !fish.Wild);
        fish = null; mode = "play"; yield return 2;
        // A gamepad's B uses what you hold, in plain play (a quick cast with the rod).
        if (FreeShore((x, y) => TileAt(x, y) == 'w' && BuboSpot(x, y) == null) is (float bx, float by, string bface))
        {
            player.X = bx; player.Y = by; player.Face = bface; yield return 3;
            Inp.Tap(KeyboardKey.One); yield return 2;
            Inp.TapPad(GamepadButton.RightFaceRight); yield return 3;
            Check($"B on a gamepad casts the rod you hold ({mode})", mode is "casting" or "waiting" && fish is { Wild: true });
            fish = null; mode = "play"; yield return 2;
        }

        /* ---------- Food ---------- */
        player.X = open.x; player.Y = open.y; yield return 3;
        Give("grilled_fish", 2); state.food = 50;
        TogglePanel("bag"); bagSel = "grilled_fish"; yield return 3;
        Check("the bag card offers hotbar slots for food", Gfx.Seen.ContainsKey("hotbar-set:6"));
        ClickButton("hotbar-set:6"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check($"a real click puts it on slot 6 ({state.hotbar[5]})", state.hotbar[5] == "grilled_fish");
        pendingShot = "hotbar-05-bag"; yield return 2;
        // Number keys don't pick from the hotbar with a panel open.
        int heldBefore = state.held;
        Inp.Tap(KeyboardKey.Six); yield return 2;
        Check("with a panel open, the number keys leave the hotbar alone", state.held == heldBefore);
        ClosePanels(); yield return 2;
        Inp.Tap(KeyboardKey.Six); yield return 2;
        float food0 = state.food;
        Inp.Tap(KeyboardKey.E); yield return 2;
        Check($"with food in hand, E eats one ({food0:0} -> {state.food:0}, {Has("grilled_fish")} left)", state.food > food0 && Has("grilled_fish") == 1);
        Inp.Tap(KeyboardKey.E); yield return 2;
        Check($"the last one eaten, the slot is empty but remembered ({SlotItem(state.hotbar[5]) ?? "none"})", Has("grilled_fish") == 0 && SlotItem(state.hotbar[5]) == null && state.hotbar[5] == "grilled_fish");
        Inp.Tap(KeyboardKey.E); yield return 2;
        Check("and E with nothing left in hand does nothing", mode == "play");

        /* ---------- Build mode keeps its keys ---------- */
        Inp.Tap(KeyboardKey.Two); yield return 2;
        int keep = state.held;
        Inp.Tap(KeyboardKey.B); yield return 3;
        Inp.Tap(KeyboardKey.One); yield return 3;
        Check($"in build mode the number keys pick pieces, not the hotbar ({buildTool}, held {state.held})", mode == "build" && state.held == keep && buildTool == BuildTools()[0]);
        Inp.Tap(KeyboardKey.B); yield return 3;

        /* ---------- Saved ---------- */
        state.held = 2; Save();
        var saved = SaveFile.Read(SaveFile.Slot);
        state = saved; StartGame(false); quietWildlife = true; yield return 2;
        Check($"the hotbar and what you hold are saved ({string.Join(",", state.hotbar.Select(k => k ?? "-"))}, {state.held})", state.held == 2 && state.hotbar[5] == "grilled_fish" && state.hotbar[1] == "axe");

        /* ---------- The prompt sits above the hotbar ---------- */
        Inp.Tap(KeyboardKey.One); yield return 2;
        if (FreeShore((x, y) => TileAt(x, y) == 'w' && BuboSpot(x, y) == null) is (float qx, float qy, string qface))
        {
            player.X = qx; player.Y = qy; player.Face = qface; yield return 4;
            Check($"the prompt stands clear above the hotbar ({prompt.Text})", prompt.Text.Length > 0 && lastHotbarTop > Gfx.LH - 80);
            pendingShot = "hotbar-06-prompt"; yield return 2;
        }

        /* ---------- Pip's stall ---------- */
        state.coins = 500;
        OpenShop(); shopTab = "buy"; yield return 3;
        Check($"every row of Pip's goods fits the stall ({lastShopBottom:0} of {(Gfx.LH + 680) / 2 - 24:0})", lastShopBottom <= (Gfx.LH + 680) / 2 - 20);
        var longest = Items.Shop.Select(s => s.id).OrderByDescending(id => Gfx.Measure(Bind.Fix(Items.ById[id].Desc), FontKind.Note, 15)).First();
        int li = Array.FindIndex(Items.Shop, s => s.id == longest);
        // Point at the longest description's row: the full text shows by the mouse.
        float colW = (1080 - 48 - 16) / 2f, rx = (Gfx.LW - 1080) / 2 + 24 + (li % 2) * (colW + 16), ry = (Gfx.LH - 680) / 2 + 24 + 62 + 50 + (li / 2) * 86;
        Inp.ScriptMouse = new System.Numerics.Vector2(rx + 120, ry + 50); yield return 3;
        Check($"pointing at an item shows what it is in full ({shopTip})", shopTip == longest);
        pendingShot = "hotbar-07-shop-tip"; yield return 2;
        Inp.ScriptMouse = Offscreen; yield return 2;
        pendingShot = "hotbar-08-shop"; yield return 2;
        // Pip still sells by the real button.
        int suka = Has("suka");
        ClickButton("Buy 1"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 1;
        Check($"the Buy button still works ({suka} -> {Has("suka")})", Has("suka") == suka + 1);
        shopTab = "sell"; Give("stone", 3); yield return 3;
        pendingShot = "hotbar-09-shop-sell"; yield return 2;
        ClosePanels(); yield return 2;

        /* ---------- The title ---------- */
        mode = "title"; titleView = "main"; slotInfo = null; yield return 4;
        bool fits = TitleControls.All(l => Gfx.Measure(Bind.Fix(l), FontKind.Ui500, 18) <= Gfx.LW - 40);
        Check($"the title's controls fit the screen, and name the hotbar and the use key", fits && TitleControls[0].Contains("hotbar") && TitleControls[0].Contains("<use>"));
        Check($"and it shows the version ({TitleVersion})", TitleVersion.StartsWith("Version ") && !TitleVersion.EndsWith("?"));
        pendingShot = "hotbar-10-title"; yield return 2;
        mode = "play";
        yield return 2;
    }

    // Somewhere on open grass on Saltmere with nothing in front of you (no target), facing right onto land.
    (float x, float y) OpenGround()
    {
        for (int ty = 4; ty < 16; ty++)
            for (int tx = 4; tx < 28; tx++)
            {
                if (TileAt(tx, ty) != 'g' || TileAt(tx + 1, ty) != 'g' || TileAt(tx + 2, ty) != 'g') continue;
                player.X = tx * T + 5; player.Y = ty * T + 8; player.Face = "right";
                if (!CanStand(player.X, player.Y) || FindTarget() != null || FreeLanding(1f).water) continue;
                return (player.X, player.Y);
            }
        return (160, 115);
    }

    // A shore where nothing else is in front of you and a cast would land in water of the kind asked for.
    (float x, float y, string face)? FreeShore(Func<int, int, bool> wanted)
    {
        foreach (var (x0, y0, x1, y1) in new[] { (0, 0, 32, 18), (90, 15, 135, 42), (32, 0, 140, 56) })
            for (int ty = y0; ty < y1; ty++)
                for (int tx = x0; tx < x1; tx++)
                {
                    if (!Walkable(TileAt(tx, ty))) continue;
                    foreach (var face in new[] { "right", "left", "down", "up" })
                    {
                        player.X = tx * T + 5; player.Y = ty * T + 8; player.Face = face;
                        if (!CanStand(player.X, player.Y) || FindTarget() != null) continue;
                        var (lx, ly, water) = FreeLanding(0.5f);
                        if (!water || InSanctuary(lx, ly) || !wanted((int)MathF.Floor(lx / T), (int)MathF.Floor(ly / T))) continue;
                        return (player.X, player.Y, face);
                    }
                }
        return null;
    }

    // A tree you can stand beside and face.
    (int tx, int ty, float px, float py, string face)? TreeToChop()
    {
        for (int ty = 2; ty < 17; ty++)
            for (int tx = 2; tx < 30; tx++)
            {
                if (TileAt(tx, ty) is not ('t' or 'f' or 'h')) continue;
                float px = (tx - 1) * T + 5, py = ty * T + 8;
                if (!CanStand(px, py)) continue;
                player.X = px; player.Y = py; player.Face = "right";
                if (FindTarget() is { Type: "tree" } t && t.Tx == tx && t.Ty == ty) return (tx, ty, px, py, "right");
            }
        return null;
    }
}
#endif
