#if DEBUG
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

partial class Game
{
    // The trophy wall and the house goals (1.46, FESH_TROPHY_TEST=1): records ordered by tier then weight, with a record from
    // before 1.46 shown as "day unknown" and given its tier from its weight; the wall drawn with the three best; the piece
    // in a shack, its target and panel (paged, fitting in both fonts); records surviving the sale of the fish; kinds of
    // furnishing counted once each, the journal goal and the Ledger's three home achievements.
    IEnumerable<int> TrophyScript()
    {
        Note("Trophy wall and house goals");
        OldCelebrate = false;
        state = new State { created = true, look = new Look { name = "Collector" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 5; Inp.ScriptMouse = Offscreen;
        yield return 3;
        var fish = Data.AllCommon.Where(f => !f.Legend && FishArt.Looks.ContainsKey(f.Id)).Take(16).ToList();

        /* ---------- the plaques ---------- */
        Check("with no records, nothing is mounted", Plaques().Count == 0);
        void Rec(int i, float m, int day) { var f = fish[i]; state.records[f.Id] = MathF.Round(f.Kg * m * 100) / 100f; if (m > 0) state.recordM[f.Id] = m; if (day > 0) state.recordDay[f.Id] = day; }
        Rec(0, 1.0f, 3); Rec(1, 2.6f, 9); Rec(2, 2.1f, 6); Rec(3, 1.6f, 4);
        // A record from before 1.46: a weight and nothing else.
        state.records[fish[4].Id] = MathF.Round(fish[4].Kg * 2.2f * 100) / 100f;
        var p = Plaques();
        Check($"best tier first, then the heaviest ({string.Join(", ", p.Select(q => $"{TierName(q.Tier)}{q.Tier}"))})", p.Count == 5 && p[0].Id == fish[1].Id && p[0].Tier == 3 && p[1].Tier == 2 && p[2].Tier == 2 && p[3].Tier == 1 && p[4].Tier == 0);
        Check("an old record takes its tier from its weight against the usual, and has no day", p.First(q => q.Id == fish[4].Id).Tier == 2 && p.First(q => q.Id == fish[4].Id).Day == 0);
        Check("a record's fish are named without their brackets", p.All(q => !q.Name.Contains("(")));
        Check("selling the fish takes nothing off the wall", Plaques().Count == 5 && fish.Take(5).All(f => { state.inv.Remove(f.Id); return state.records.ContainsKey(f.Id); }));

        /* ---------- the piece in a shack ---------- */
        var shack = new Build { id = "shack", x = 13, y = 12 };
        state.builds.Add(shack);
        string key = ShackKey(shack);
        state.rooms[key] = new() { new Build { id = "trophy", x = 3, y = 3 } };
        Check("the piece exists and goes indoors", Data.BuildById.TryGetValue("trophy", out var def) && def.Indoor && Data.IndoorTools.Contains("trophy") && !Data.OutdoorTools.Contains("trophy"));
        Check("aquarium keeps its number key (the tenth tool gets 0)", Data.IndoorTools.ToList().IndexOf("aquarium") == 8 && Data.IndoorTools.ToList().IndexOf("trophy") == 9);
        // The drawing: the wall changes with what is mounted.
        int Wall()
        {
            var one = new Pix(40, 30);
            var keep = pix; pix = one;
            DrawTrophyWall(new Build { id = "trophy", x = 0, y = 0 }, 10, 15);
            pix = keep;
            int h = 17; foreach (var c in one.Buf) h = unchecked(h * 31 + c.R * 5 + c.G * 3 + c.B + c.A); return h;
        }
        int withRecords = Wall(); var saved = new Dictionary<string, float>(state.records); state.records.Clear(); int empty = Wall(); foreach (var kv in saved) state.records[kv.Key] = kv.Value;
        Check("the wall shows what you have caught", withRecords != empty);
        {
            var sheet = new Pix(60, 30); Array.Fill(sheet.Buf, Pal.C("#d8c9a4"));
            var keep = pix; pix = sheet; DrawTrophyWall(new Build { id = "trophy", x = 0, y = 0 }, 20, 16); pix = keep;
            var img = GenImageColor(60, 30, Color.Black);
            for (int y = 0; y < 30; y++) for (int x = 0; x < 60; x++) ImageDrawPixel(ref img, x, y, sheet.Buf[y * 60 + x]);
            ImageResizeNN(ref img, 60 * 10, 30 * 10);
            ExportImage(img, Path.Combine(testDir, "trophy-wall.png"));
            UnloadImage(img);
        }
        LoadScene(key); yield return 4;
        player.X = 40; player.Y = 46; player.Face = "up"; yield return 3;
        Check($"beside it, E reads the wall ({target?.Type}: {target?.Label})", target?.Type == "trophy" && target.Label == "Read the trophy wall");
        pendingShot = "t1-shack"; yield return 3;
        Inp.Tap(KeyboardKey.E); yield return 4;
        Check("it opens the trophy panel", mode == "panel" && panel == "trophies");
        pendingShot = "t2-panel"; lastTrophyBottom = 0; yield return 4;
        Check($"five plaques fit ({lastTrophyBottom:0} of {panelAt.y + panelAt.h - 12:0})", lastTrophyBottom > 0 && lastTrophyBottom <= panelAt.y + panelAt.h - 12 && Gfx.Seen.Keys.Count(k => k.StartsWith("plaque:")) == 5);
        ClosePanels(); yield return 2;

        // Many records: pages, in both fonts.
        for (int i = 5; i < 16; i++) Rec(i, 1.2f + i * 0.05f, 10 + i);
        OpenTrophies(); lastTrophyBottom = 0; yield return 4;
        Check("sixteen records take two pages", Plaques().Count == 16 && Gfx.Seen.ContainsKey("More"));
        pendingShot = "t3-page1"; yield return 3;
        Check($"a full page fits ({lastTrophyBottom:0})", lastTrophyBottom > 0 && lastTrophyBottom <= panelAt.y + panelAt.h - 12);
        Settings.Data.clearFont = true; lastTrophyBottom = 0; yield return 4;
        Check($"and fits in the clear font ({lastTrophyBottom:0})", lastTrophyBottom > 0 && lastTrophyBottom <= panelAt.y + panelAt.h - 12);
        Settings.Data.clearFont = false; yield return 2;
        ClickButton("More"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 3;
        Check("More turns the page", trophyPage == 1);
        ClosePanels(); yield return 2;

        /* ---------- house goals ---------- */
        Check("a trophy wall alone is one kind of furnishing", HomeKinds(key) == 1 && MostKinds == 1);
        state.rooms[key].Add(new Build { id = "rug", x = 6, y = 6 }); state.rooms[key].Add(new Build { id = "rug", x = 8, y = 6 });
        Check("two rugs are one kind", HomeKinds(key) == 2);
        state.rooms[key].Add(new Build { id = "workbench", x = 1, y = 2 });
        Check("a workbench is not furnishing", HomeKinds(key) == 2);
        var goals = new List<Goal>(); HomeGoals(goals);
        Check($"the journal asks for more ({goals.FirstOrDefault()?.Title})", goals.Count == 1 && goals[0].Title.Contains("2 of 7") && goals[0].Text.Contains("table") && !goals[0].Text.Contains("rug"));
        state.rooms[key].Add(new Build { id = "table", x = 2, y = 5 }); CheckLedger(); celebQueue.Clear(); banner = null;
        Check("three kinds earn Cosy", AchEarned(state, Achievements.First(a => a.Id == "home3")) && !AchEarned(state, Achievements.First(a => a.Id == "home5")));
        foreach (var id in new[] { "plant", "lamp" }) state.rooms[key].Add(new Build { id = id, x = 12, y = 5 });
        CheckLedger(); celebQueue.Clear(); banner = null;
        Check("five kinds earn Comfortable", AchEarned(state, Achievements.First(a => a.Id == "home5")) && !AchEarned(state, Achievements.First(a => a.Id == "home7")));
        // A second home doesn't add to the first.
        var kubo = new Build { id = "kubo", x = 20, y = 12 }; state.builds.Add(kubo);
        state.rooms[ShackKey(kubo)] = new() { new Build { id = "bed", x = 3, y = 3 }, new Build { id = "aquarium", x = 8, y = 3 } };
        Check("kinds are counted in one home, not across homes", MostKinds == 5);
        state.rooms[key].Add(new Build { id = "bed", x = 3, y = 6 }); state.rooms[key].Add(new Build { id = "aquarium", x = 8, y = 6 });
        CheckLedger(); celebQueue.Clear(); banner = null;
        goals = new List<Goal>(); HomeGoals(goals);
        Check("all seven earn the title Homebody, and the goal is gone", AchEarned(state, Achievements.First(a => a.Id == "home7")) && EarnedTitles.Contains("Homebody") && goals.Count == 0);
        // Building and the bar.
        LoadScene(key); yield return 3;
        ToggleBuild(); yield return 4;
        Check($"the shack's build bar fits the screen with the new piece ({lastBuildBarW:0} of {Gfx.LW})", mode == "build" && lastBuildBarW > 0 && lastBuildBarW <= Gfx.LW - 30);
        pendingShot = "t4-build"; yield return 3;
        ToggleBuild(); yield return 3;
        OldCelebrate = true;
    }

}
#endif
