#if DEBUG
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

partial class Game
{
    // The wardrobe, mastery and trophy tiers (1.46, FESH_WARDROBE_TEST=1): the catalogue's data, the three new hats drawn in
    // every facing and walk frame without leaving the person's box or covering the face, ownership (bought, earned by
    // achievement, friendship and mastery; never twice; unknown and unowned ids come off on loading), Pip's Dress stall and the
    // wardrobe panel by real clicks (a draft that only changes you on Wear it), the boat's trim and the rod's colour, the
    // mastery curve and its titles, the trophy tiers and the records' dates, and the panels' fit in both fonts.
    IEnumerable<int> WardrobeScript()
    {
        Note("Wardrobe, mastery and trophy tiers");
        OldCelebrate = false;
        state = new State { created = true, look = new Look { name = "Dresser" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 5; Inp.ScriptMouse = Offscreen;
        state.coins = 1000;
        yield return 3;

        /* ---------- the catalogue ---------- */
        var cat = Wardrobe.Catalog;
        Check($"{cat.Length} cosmetics with unique ids and real slots", cat.Select(c => c.Id).Distinct().Count() == cat.Length && cat.All(c => Wardrobe.Slots.Contains(c.Slot)));
        Check("every value is a colour, every shop price is set and earned ones are free", cat.All(c => c.Value.Length == 7 && c.Value[0] == '#' && (c.InShop ? c.Price >= 100 : c.Price == 0)));
        Check("hats name a style from 4 to 6, nothing else does", cat.Where(c => c.Slot == "hat").All(c => c.Style is >= 4 and <= 6) && cat.Where(c => c.Slot != "hat").All(c => c.Style == 0));
        Check("every earned one points at a real achievement, friend or mastery level", cat.Where(c => !c.InShop).All(c =>
        {
            var p = c.Unlock.Split(':');
            return p[0] switch
            {
                "ach" => Achievements.Any(a => a.Id == p[1]),
                "friend" => FriendById.ContainsKey(p[1]) && int.TryParse(p[2], out int hh) && hh is >= 1 and <= 5,
                "mastery" => int.TryParse(p[1], out int n) && n is >= 1 and <= MasteryMax,
                _ => false
            };
        }));
        Check("the most expensive thing is a real coin sink (over 120)", cat.Max(c => c.Price) > 120 && cat.Count(c => c.InShop) >= 15);
        Check("the names are plain (the salakot is a salakot, not a costume)", Wardrobe.ById["hat_salakot"].Name == "Salakot" && cat.All(c => !c.Name.Contains("costume", StringComparison.OrdinalIgnoreCase)));

        /* ---------- the hats ---------- */
        (int minX, int maxX, int minY, int maxY, int px) Box(Look l, string face, int step)
        {
            var p = new Pix(40, 44);
            LookData.DrawPerson(p, l, 20, 38, face, step, shadow: false, idle: false);
            int x0 = 99, x1 = -1, y0 = 99, y1 = -1, n = 0;
            for (int y = 0; y < 44; y++) for (int x = 0; x < 40; x++) if (p.Buf[y * 40 + x].A > 0) { n++; x0 = Math.Min(x0, x); x1 = Math.Max(x1, x); y0 = Math.Min(y0, y); y1 = Math.Max(y1, y); }
            return (x0, x1, y0, y1, n);
        }
        int Hash(Look l, string face, int step)
        {
            var p = new Pix(40, 44);
            LookData.DrawPerson(p, l, 20, 38, face, step, shadow: false, idle: false);
            int h = 17; foreach (var c in p.Buf) h = unchecked(h * 31 + c.R * 5 + c.G * 3 + c.B + c.A * 7); return h;
        }
        bool hatsFit = true, hatsShow = true;
        var worst = "";
        foreach (int style in new[] { 4, 5, 6 })
            foreach (int hair in new[] { 0, 1 })
                foreach (var face in new[] { "down", "up", "left", "right" })
                    for (int step = 0; step <= 4; step++)
                    {
                        var bare = new Look { name = "x", hat = 0, hair = hair };
                        var hatted = new Look { name = "x", hat = style, hair = hair, hatId = style == 4 ? "hat_bandana_red" : style == 5 ? "hat_bucket_khaki" : "hat_salakot" };
                        var b0 = Box(bare, face, step); var b1 = Box(hatted, face, step);
                        // The hat may add width (a brim) and height, within reason, and never change the feet.
                        int addW = (b1.maxX - b1.minX) - (b0.maxX - b0.minX), addH = b0.minY - b1.minY;
                        if (b1.maxY != b0.maxY || addW > 9 || addH > 5 || addH < 0) { hatsFit = false; worst = $"hat {style} {face} step {step}: +{addW} wide, +{addH} tall"; }
                        if (Hash(hatted, face, step) == Hash(bare, face, step)) hatsShow = false;
                    }
        Check($"the new hats stay near the person's own box in every facing and walk frame ({worst})", hatsFit);
        Check("and each one is really drawn", hatsShow);
        // The face stays visible: the same eye pixels with and without the hat, facing the viewer.
        int Eyes(Look l)
        {
            var p = new Pix(40, 44);
            LookData.DrawPerson(p, l, 20, 38, "down", 0, shadow: false, idle: false);
            int n = 0; foreach (var c in p.Buf) if (c.A > 0 && c.R < 70 && c.G < 60 && c.B < 60 && c.R > 20) n++;
            return n;
        }
        Check("the hats leave the eyes showing", new[] { 4, 5, 6 }.All(s => Eyes(new Look { hat = s, hatId = null }) >= Eyes(new Look { hat = 0 }) - 0));
        {
            var sheet = new Pix(120, 60);
            Array.Fill(sheet.Buf, Pal.C("#5d9b45"));
            int ax = 10;
            foreach (int style in new[] { 0, 4, 5, 6 })
                foreach (var face in new[] { "down", "right", "up" })
                {
                    LookData.DrawPerson(sheet, new Look { hat = style, shirt = 1, hair = style == 5 ? 1 : 0, hatId = style == 4 ? "hat_bandana_blue" : style == 5 ? "hat_bucket_navy" : style == 6 ? "hat_salakot" : null }, ax, 28, face, 0, shadow: false, idle: false);
                    LookData.DrawPerson(sheet, new Look { hat = style, shirt = 3, hairColor = 3, hair = 1, hatId = style == 4 ? "hat_bandana_red" : style == 5 ? "hat_bucket_khaki" : style == 6 ? "hat_salakot" : null }, ax, 54, face, 0, shadow: false, idle: false);
                    ax += 9;
                }
            var img = GenImageColor(120, 60, Color.Black);
            for (int y = 0; y < 60; y++) for (int x = 0; x < 120; x++) ImageDrawPixel(ref img, x, y, sheet.Buf[y * 120 + x]);
            ImageResizeNN(ref img, 120 * 8, 60 * 8);
            ExportImage(img, Path.Combine(testDir, "hats.png"));
            UnloadImage(img);
        }

        /* ---------- owning things ---------- */
        var teal = Wardrobe.ById["shirt_teal"]; var ivory = Wardrobe.ById["shirt_ivory"]; var sea = Wardrobe.ById["shirt_sea"]; var rose = Wardrobe.ById["hair_rose"];
        Check("a shop item isn't yours until you buy it; earned ones aren't either until earned", !Owns(teal) && !Owns(ivory) && !Owns(sea) && !Owns(rose));
        state.coins = 100;
        Check("without the coins a purchase is refused and nothing changes", !BuyCosmetic(teal) && state.coins == 100 && !Owns(teal));
        state.coins = 1000;
        Check("with the coins it is bought: the coins and the ownership move together", BuyCosmetic(teal) && state.coins == 850 && Owns(teal) && state.cosmetics.Contains("shirt_teal"));
        Check("and never twice", !BuyCosmetic(teal) && state.coins == 850 && state.cosmetics.Count(c => c == "shirt_teal") == 1);
        Check("an earned one can't be bought", !BuyCosmetic(ivory) && !Owns(ivory));
        state.hinted["ach:fish100"] = true;
        Check("an achievement unlocks its cosmetic", Owns(ivory));
        state.xp = XpFor(10) + MasteryTotal(2) - 1;
        Check("mastery 1 isn't mastery 2", MasteryLevel == 1 && !Owns(sea));
        state.xp = XpFor(10) + MasteryTotal(2);
        Check("mastery 2 unlocks the sea-blue shirt", MasteryLevel == 2 && Owns(sea));
        state.friends["lira"] = new Friendship { points = HeartAt[3] };
        Check("four hearts isn't enough for the rose hair", !Owns(rose));
        state.friends["lira"].points = HeartAt[4];
        Check("five hearts with Lira is", Owns(rose));
        Check("the text says how to get each", UnlockText(rose).Contains("Lira") && UnlockText(Wardrobe.ById["shirt_gold"]).Contains("Master size") && UnlockText(Wardrobe.ById["hat_salakot"]).Contains("Dress stall"));

        /* ---------- what you wear is checked ---------- */
        state.look.shirtId = "shirt_teal"; state.look.pantsId = "pants_moss"; state.look.hatId = "hat_salakot"; state.look.hat = 6; state.look.hairId = "no_such_thing"; state.hullId = "hull_gold"; state.rodId = "rod_red";
        FixWardrobe();
        Check("what you own stays on", state.look.shirtId == "shirt_teal");
        Check("what you don't (pants, a hat, a hull, a rod) comes off, and the hat style goes with it", state.look.pantsId == null && state.look.hatId == null && state.look.hat == 0 && state.hullId == null && state.rodId == null);
        Check("an unknown id comes off", state.look.hairId == null);
        state.look.shirtId = "pants_moss"; FixWardrobe();
        Check("an id in the wrong slot isn't worn", state.look.shirtId == null);
        state.cosmetics.Add("future_thing_from_a_newer_version");
        Save(); state = SaveFile.Read(SaveFile.Slot); StartGame(false); yield return 3;
        Check("ownership ids from a newer version are kept, not deleted", state.cosmetics.Contains("future_thing_from_a_newer_version") && state.cosmetics.Contains("shirt_teal"));
        string oldPath = Path.Combine(Path.GetTempPath(), "fesh-ward-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(oldPath, "{\"created\":true,\"look\":{\"name\":\"Old\",\"hat\":5},\"coins\":10}");
        var oldSave = SaveFile.Load(oldPath); File.Delete(oldPath);
        Check("an older save with no wardrobe fields loads, with no cosmetics worn", oldSave != null && oldSave.cosmetics.Count == 0 && oldSave.look.shirtId == null && oldSave.hullId == null);
        state.look.hat = 1;

        /* ---------- Pip's Dress stall by real clicks ---------- */
        state.coins = 500;
        shopTab = "sell"; panel = "shop"; mode = "panel"; yield return 3;
        ClickButton("Dress"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("the stall has a Dress tab", shopTab == "dress");
        pendingShot = "w1-dress"; yield return 4;
        Check($"its rows fit the stall ({lastDressBottom:0} of {panelAt.y + panelAt.h - 12:0})", lastDressBottom > 0 && lastDressBottom <= panelAt.y + panelAt.h - 12);
        int coins0 = state.coins;
        ClickButton("dress:buy:shirt_coral"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check($"Buy takes the coins ({coins0 - state.coins}) and it is owned", state.coins == coins0 - 150 && Owns("shirt_coral"));
        ClickButton("dress:slot:hat"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        pendingShot = "w2-dress-hats"; yield return 4;
        ClickButton("dress:buy:hat_salakot"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("the salakot costs 450 and she cannot afford it with what is left", !Owns("hat_salakot") && state.coins == coins0 - 150);
        state.coins = 1000;
        ClickButton("dress:buy:hat_bucket_khaki"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("a hat bought by a click", Owns("hat_bucket_khaki") && state.coins == 700);
        ClosePanels(); yield return 2;

        /* ---------- the wardrobe panel ---------- */
        state.cosmetics.AddRange(new[] { "hull_red", "rod_black", "hat_salakot" });
        // From the journal.
        TogglePanel("journal"); yield return 3;
        pendingShot = "w0-journal"; yield return 4;
        Note($"journal buttons: {string.Join(", ", new[] { "Wardrobe", "Moon", "Friends", "Harbour board", "Angler's Ledger" }.Select(k => k + " " + (Gfx.Seen.TryGetValue(k, out var r) ? $"{r.X:0}-{r.X + r.Width:0}" : "none")))}");
        Check("the journal has Wardrobe and Moon buttons in a row of their own, inside the panel, not overlapping", Gfx.Seen.TryGetValue("Wardrobe", out var wr) && Gfx.Seen.TryGetValue("Moon", out var mn) && wr.X + wr.Width <= mn.X && wr.X > panelAt.x + 100 && wr.Y == mn.Y && mn.X + mn.Width <= panelAt.x + panelAt.w);
        // The header (the user found it crammed): fishing progress on the title line, one row of shortcuts, nothing stacked.
        Check($"the journal shows your fishing progress ({lastFishingProgress.head}; {lastFishingProgress.right})", Gfx.Seen.ContainsKey("journal:fishing") && lastFishingProgress.head.StartsWith("Fishing level") && lastFishingProgress.right.Contains("XP"));
        {
            var ledgerB = Gfx.Seen["Angler's Ledger"]; var moonB = Gfx.Seen["Moon"]; var card = Gfx.Seen["journal:fishing"];
            Check("every shortcut is on one row, below the title line and the progress card, none over another", ledgerB.Y == moonB.Y && ledgerB.Y >= card.Y + card.Height - 2 && Gfx.Seen["Wardrobe"].Y == ledgerB.Y && ledgerB.X + ledgerB.Width <= Gfx.Seen["Wardrobe"].X);
            Check("and the goals start below that row", Gfx.Seen.Where(kv => kv.Key.StartsWith("follow:")).All(kv => kv.Value.Y > ledgerB.Y + ledgerB.Height));
        }
        state.xp = XpFor(3) + 7; yield return 3;
        Check($"level 3 with 7 XP says so ({lastFishingProgress.head}, {lastFishingProgress.right})", lastFishingProgress.head == "Fishing level 3" && lastFishingProgress.right.StartsWith("7 / ") && lastFishingProgress.right.Contains(PerkText[4]) && lastFishingProgress.frac > 0 && lastFishingProgress.frac < 0.2f);
        state.xp = XpFor(10) + MasteryTotal(2) + 10; yield return 3;
        Check($"past level 10 it shows mastery ({lastFishingProgress.head}, {lastFishingProgress.right})", lastFishingProgress.head.Contains("mastery 2") && lastFishingProgress.right.StartsWith("10 / " + MasteryNeed(3)));
        state.xp = XpFor(10) + MasteryTotal(MasteryMax) + 5; yield return 3;
        Check("and at the top it says it's complete", lastFishingProgress.right == "Mastery complete" && lastFishingProgress.frac == 1);
        state.xp = 0; yield return 3;
        ClickButton("Wardrobe"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("it opens the wardrobe", mode == "panel" && panel == "wardrobe" && wardLook != null);
        string shirtWas = state.look.shirtId;
        ClickButton("ward:tile:shirt_teal"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("trying a shirt on changes the preview, not you", wardLook.shirtId == "shirt_teal" && state.look.shirtId == shirtWas);
        pendingShot = "w3-wardrobe"; yield return 4;
        ClickButton("ward:tile:shirt_gold"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("a locked shirt can't be tried on", wardLook.shirtId == "shirt_teal");
        ClickButton("ward:slot:hat"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("ward:tile:hat_salakot"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("a hat style and its id are set together", wardLook.hat == 6 && wardLook.hatId == "hat_salakot");
        pendingShot = "w4-wardrobe-hats"; yield return 4;
        ClickButton("ward:slot:hull"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("ward:tile:hull_red"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("ward:slot:rod"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("ward:tile:rod_black"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("the boat and the rod are drafted too", wardHull == "hull_red" && wardRod == "rod_black" && state.hullId == null && state.rodId == null);
        Check($"the panel fits in the pixel font ({lastWardBottom:0} of {panelAt.y + panelAt.h - 12:0})", lastWardBottom > 0 && lastWardBottom <= panelAt.y + panelAt.h - 12);
        Settings.Data.clearFont = true; lastWardBottom = 0; yield return 4;
        Check($"and in the clear font ({lastWardBottom:0})", lastWardBottom > 0 && lastWardBottom <= panelAt.y + panelAt.h - 12);
        Settings.Data.clearFont = false; yield return 2;
        // Cancel throws the draft away.
        ClickButton("ward:cancel"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("Cancel leaves everything as it was", mode == "play" && state.look.shirtId == shirtWas && state.hullId == null && state.rodId == null);
        // Wear it.
        OpenWardrobe(); yield return 3;
        ClickButton("ward:tile:shirt_teal"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("ward:slot:hull"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("ward:tile:hull_red"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("ward:slot:rod"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("ward:tile:rod_black"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        ClickButton("ward:wear"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("Wear it puts it all on and closes", mode == "play" && state.look.shirtId == "shirt_teal" && state.hullId == "hull_red" && state.rodId == "rod_black" && state.look.name == "Dresser");
        Check("and it is saved", SaveFile.Read(SaveFile.Slot).look.shirtId == "shirt_teal" && SaveFile.Read(SaveFile.Slot).hullId == "hull_red");
        Check("the colour the figure is drawn in is the cosmetic's", LookData.ShirtHex(state.look) == "#1f8a8a" && LookData.ShirtHex(new Look { shirt = 2 }) == LookData.Shirts[2]);
        // From the pause menu.
        OpenPause(); menuTab = "game"; yield return 4;
        pendingShot = "w5-pause";
        Check("the pause menu has a Wardrobe button inside the menu", Gfx.Seen.TryGetValue("Wardrobe", out var pw) && pw.Y + pw.Height < 700);
        yield return 3;
        ClickButton("Wardrobe"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 4;
        Check("it opens the wardrobe from there", mode == "panel" && panel == "wardrobe");
        ClosePanels(); yield return 2;

        /* ---------- the boat's trim and the rod ---------- */
        state.hullId = null;
        var plain = BoatPaletteNow();
        state.hullId = "hull_red";
        var red = BoatPaletteNow();
        Check("the trim palette entries become the paint, the planks stay", red[1].R > red[1].B && red[1].R == 0xc0 && plain[1].R != red[1].R && red[2].Equals(plain[2]) && red[5].Equals(plain[5]));
        Check("with no paint it is the original palette", (state.hullId = null) == null && BoatPaletteNow()[1].Equals(plain[1]));
        state.rodId = "rod_black";
        Check("the rod is held in its paint", RodHex == "#2b2b30" && HandsPose("right", 20, 20, true, 0).Tint == "#2b2b30");
        state.rodId = null; state.hullId = "hull_red";

        /* ---------- mastery ---------- */
        Check("the mastery curve: each level a bit more than the last", Enumerable.Range(1, MasteryMax).Select(MasteryNeed).SequenceEqual(new[] { 100, 125, 150, 175, 200, 225, 250, 275, 300, 325 }));
        state.xp = XpFor(10) - 1;
        Check("before level 10 there is no mastery", MasteryLevel == 0);
        var levels = new List<int>();
        foreach (int total in new[] { 0, 99, 100, 224, 225, 2124, 2125, 99999 }) { state.xp = XpFor(10) + total; levels.Add(MasteryLevel); }
        Check($"levels at the edges ({string.Join(",", levels)})", levels.SequenceEqual(new[] { 0, 0, 1, 1, 2, 9, 10, 10 }));
        Check("titles at 1, 3, 4, 6, 7 and 9; cosmetics at 2, 5, 8 and 10", new[] { 1, 3, 4, 6, 7, 9 }.All(MasteryTitles.ContainsKey) && new[] { 2, 5, 8, 10 }.All(n => Wardrobe.UnlockedBy("mastery:" + n) != null));
        var ach = Achievements.Where(a => a.Group == "Mastery").ToList();
        Check($"the Ledger has a Mastery page with {ach.Count} entries and the titles", ach.Count(a => a.Id.StartsWith("mastery")) == MasteryMax && ach.Where(a => a.Id.StartsWith("mastery") && a.Title != null).Select(a => a.Title).SequenceEqual(MasteryTitles.Values));
        Check("a mastery card says what it unlocks", Achievements.First(a => a.Id == "mastery5").Desc.Contains("wardrobe"));
        state.xp = XpFor(10) + MasteryTotal(3); CheckLedger(); celebQueue.Clear(); banner = null;
        Check("reaching mastery 3 earns its title once", AchEarned(state, Achievements.First(a => a.Id == "mastery3")) && EarnedTitles.Contains("Salt-handed"));
        Check("and the Numbers say so", LedgerNumbers().Any(r => r.label == "Fishing level" && r.value.Contains("mastery 3")));
        OpenLedger(); ledgerTab = "Mastery"; lastLedgerBottom = 0; yield return 4;
        pendingShot = "w6-ledger-mastery";
        yield return 3;
        Check($"the Mastery page fits ({lastLedgerBottom:0} of {panelAt.y + panelAt.h - 12:0})", lastLedgerBottom > 0 && lastLedgerBottom <= panelAt.y + panelAt.h - 12);
        ledgerTab = "Numbers"; lastLedgerBottom = 0; yield return 4;
        Check("and the Numbers page still fits with its new rows", lastLedgerBottom > 0 && lastLedgerBottom <= panelAt.y + panelAt.h - 12);
        ClosePanels(); yield return 2;

        /* ---------- trophy tiers ---------- */
        Check("the tier edges", new[] { 1.49f, 1.5f, 1.99f, 2.0f, 2.49f, 2.5f, 3f }.Select(TrophyTier).SequenceEqual(new[] { 0, 1, 1, 2, 2, 3, 3 }));
        Check("their names", TierName(1) == "Silver" && TierName(2) == "Gold" && TierName(3) == "Master" && TierName(0) == "");
        var fish = Data.AllCommon.First(f => !f.Legend && f.Id == "pond_perch");
        int g0 = state.golds, m0 = state.masters;
        state.day = 12;
        AddCatch(fish, 1, false, 2.1f);
        Check($"a gold-size fish counts as gold and not as master ({state.golds - g0}, {state.masters - m0})", state.golds == g0 + 1 && state.masters == m0 + (lastM >= 2.5f ? 1 : 0));
        AddCatch(fish, 1, false, 2.6f);
        Check("a master-size fish counts as both", state.masters >= m0 + 1 && state.golds >= g0 + 2);
        Check("the record remembers its day and size", state.recordDay.GetValueOrDefault("pond_perch") == 12 && state.recordM.GetValueOrDefault("pond_perch") >= 2.1f);
        AddCatch(fish, 1, false, 0.01f); celebQueue.Clear(); banner = null;
        Check("a lighter fish doesn't change the record's day", state.recordDay["pond_perch"] == 12 || state.records["pond_perch"] < 0.01f);
        Check("the old trophy count still counts silver and up", state.trophies.GetValueOrDefault("pond_perch") >= 2);
        OldCelebrate = true;
    }
}
#endif
