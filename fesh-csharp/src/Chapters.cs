using Raylib_cs;

namespace Fesh;

// The Saltmere mystery's chapters between its five creatures (1.22). It used to be five catches back to back; now
// Tomas sets you up between them, and the crafting basics are the story: an axe and a pickaxe to clear the rocks fallen
// over the tide pools (before the Tidecrawler), a home of your own with a workbench and a furnace, and copper from
// Frostfang Caverns for a pickaxe that opens the wreck's rusted hatch (before the Hollow Eel), then iron from deeper
// down to mend the dock (before the Mirror Ray), and Mara's recording played to Tomas (before the Abyssal).
// Every step is worked out from the save, so tools, a shack or bars you already had count, and a chapter the story
// has moved past counts as done (saves from before 1.22). Flags in state.hinted: ch:toolsAsked, ch:rocks,
// ch:hatchSeen, ch:homeAsked, ch:hatch, ch:ironAsked, ch:recording; tut:smelted:<bar> (Pip stocks that bar from then
// on); "chapters" (a game started or loaded since 1.22) and "pipBars" (an older save: Pip kept selling bars).
partial class Game
{
    // The fallen rocks on the rocky shore's east end (their middle), and where you stand to work on the wreck's hatch.
    const float RockfallX = 216, RockfallY = 33;
    const float HatchFrontX = 46, HatchFrontY = 153;
    int rockHits;

    bool RocksCleared => state.Hinted("ch:rocks") || state.Caught("tidecrawler") || state.flags.ended;
    bool HatchOpen => state.Hinted("ch:hatch") || state.Caught("hollow_eel") || state.flags.ended;
    bool RecordingHeard => state.Hinted("ch:recording") || state.Caught("abyssal") || state.flags.ended;

    // Whether a creature's chapter is done, on top of the creature before it (Creature.Req) and its spot being open.
    bool ChapterOpen(Creature cr) => cr.Id switch
    {
        "tidecrawler" => RocksCleared,
        "hollow_eel" => HatchOpen,
        "abyssal" => RecordingHeard,
        _ => true
    };

    // What you notice at a creature's spot while its chapter isn't done (Game.MaybeHint).
    static string GateHint(string id) => id switch
    {
        "tidecrawler" => "Something clicked under the fallen rocks, then went quiet. Those tide pools need clearing first.",
        "hollow_eel" => "Something whistled inside the wreck, behind a rusted hatch. There's no way in yet.",
        "abyssal" => "The dark water is still. Tomas should hear Mara's recording first.",
        _ => null
    };

    /* ---------- A home of your own ---------- */
    IEnumerable<Build> Homes() => state.builds.Where(b => Data.BuildById[b.id].Door);
    bool HasHome => Homes().Any();
    Build HomeWith(string piece) => Homes().FirstOrDefault(h => state.rooms.GetValueOrDefault(ShackKey(h))?.Any(b => b.id == piece) == true);
    bool HomeHas(string piece) => HomeWith(piece) != null;

    // The door of a shack or kubo, from its scene key ("house:x,y").
    static (float x, float y)? HouseDoor(string key)
    {
        if (!key.StartsWith("house:") || key is "house:tomas" or "house:school") return null;
        var p = key[6..].Split(',');
        return p.Length == 2 && int.TryParse(p[0], out int x) && int.TryParse(p[1], out int y) ? (x * T + 10, y * T + 12) : null;
    }

    // A goal inside your home: at that piece, or anywhere in the room (X -1) when it isn't built yet.
    Goal AtHome(Goal g, string piece, string place)
    {
        var home = (piece != null ? HomeWith(piece) : null) ?? Homes().FirstOrDefault();
        if (home == null) return g;
        string key = ShackKey(home);
        var b = piece == null ? null : state.rooms.GetValueOrDefault(key)?.FirstOrDefault(r => r.id == piece);
        if (b == null) { At(g, -1, -1, place, key); return g; }
        return At(g, b.x * T + Data.BuildById[b.id].W * 5f, b.y * T + 12, place, key);
    }

    // Pip keeps copper and iron bars only once you've smelted that kind yourself (or in a save from before 1.22).
    bool ShopSells(string id) => id is not ("copper_bar" or "iron_bar") || state.Hinted("pipBars") || state.Hinted("tut:smelted:" + id);
    IEnumerable<(string id, int price)> ShopStock() => Items.Shop.Where(s => ShopSells(s.id));

    void NoteChapterVersion(bool fresh)
    {
        if (state.Hinted("chapters")) return;
        if (!fresh) state.hinted["pipBars"] = true;
        state.hinted["chapters"] = true;
    }

    /* ---------- The story's steps, for the guide (Guide.StoryGoal) ---------- */
    Goal StoryStep(string step, string title, string text) => NewGoal("story:" + step, "story", title, text);

    // After the Glowgill: Tomas's tools, then the fallen rocks. The rocks only need a pickaxe, so that's what moves the
    // guide on (Codex); the axe is asked for alongside it, and again for the shack's timber.
    Goal RocksGoal()
    {
        if (PickTier == 0 && !state.Hinted("ch:toolsAsked"))
            return AtTomas(StoryStep("tag", "Show Tomas the tag", "The Glowgill had a metal tag on its fin: \"Property of R/V Halcyon.\" Tomas might know that ship."));
        if (PickTier == 0) return ToolsGoal();
        return At(StoryStep("rocks", "Clear the fallen rocks", "Rocks from the storm cover the tide pools at the east end of the rocky shore. Face them and press <act> to break them up with your pickaxe."
            + (Has("axe") == 0 ? " Make a stone axe at a workbench too: you'll want timber before long." : "")),
            RockfallX - 12, RockfallY + 4, "Fallen rocks");
    }

    Goal ToolsGoal()
    {
        var g = StoryStep("tools", "Make an axe and a pickaxe", "");
        bool axe = Has("axe") == 0, pick = PickTier == 0;
        int wood = (axe ? 3 : 0) + (pick ? 3 : 0), stone = (axe ? 2 : 0) + (pick ? 3 : 0);
        string what = axe && pick ? "a stone axe (3 wood, 2 stone) and a stone pickaxe (3 wood, 3 stone)" : axe ? "a stone axe (3 wood, 2 stone)" : "a stone pickaxe (3 wood, 3 stone)";
        // Enough for the first of them: off to the workbench.
        if (Has("wood") >= 3 && Has("stone") >= (axe ? 2 : 3))
        {
            g.Text = $"Make {what} at the workbench in Tomas's hut. You have {Has("wood")} wood and {Has("stone")} stone.";
            g.Ready = Has("wood") >= wood && Has("stone") >= stone;
            return At(g, 4 * T + 10, 3 * T + 8, "Workbench", "house:tomas");
        }
        g.Text = $"You need {wood} wood and {stone} stone for {what}. You have {Has("wood")} and {Has("stone")}: walk over driftwood and stones on the beaches to pick them up.";
        string kind = Has("wood") < 3 ? "wood" : "stone";
        if (scene == "world" && state.loose.Where(l => l.kind == kind).OrderBy(l => Dist(player.X, player.Y, l.x, l.y)).FirstOrDefault() is Loose l)
            At(g, l.x, l.y + 2, kind == "wood" ? "Driftwood" : "A stone");
        return g;
    }

    // After the Tidecrawler: the wreck's hatch, and everything it takes to open it. A copper pickaxe is all the hatch
    // really needs, so with one in hand (an older save, or a shack taken down since) the guide goes straight there.
    Goal HatchGoal()
    {
        if (PickTier >= 2) return OpenHatchGoal();
        if (!state.Hinted("ch:homeAsked"))
        {
            if (!state.Hinted("ch:hatchSeen"))
                return At(StoryStep("wreck", "Walk out to the old wreck", "The tide's out. Walk out to the old wreck off Saltmere's southwest shore and have a look at it."), HatchFrontX, HatchFrontY, "Old wreck");
            return AtTomas(StoryStep("hatch:ask", "Ask Tomas about the hatch", "The wreck's hatch is rusted shut, and something whistles behind it. Tomas might know how to open it."));
        }
        if (!HasHome)
        {
            var g = StoryStep("shack", "Build a shack of your own", $"Press <build>, choose the shack and place it on open ground: 8 wood and 4 stone. You have {Has("wood")} wood and {Has("stone")} stone.");
            if (Has("wood") < 8 || Has("stone") < 4)
                g.Text += Has("axe") > 0 ? " Chop trees with your axe and break boulders with your pickaxe for more." : " Make a stone axe at a workbench (3 wood, 2 stone) to chop trees, and break boulders with your pickaxe.";
            else g.Ready = true;
            return g;
        }
        if (!HomeHas("workbench"))
        {
            var g = StoryStep("workbench", "Put a workbench in your shack", $"Go inside your shack, press <build> and place a workbench: 6 wood and 2 stone. You have {Has("wood")} wood and {Has("stone")} stone.");
            return AtHome(g, null, "Your shack");
        }
        if (!HomeHas("furnace"))
        {
            var g = StoryStep("furnace", "Build a furnace", $"A furnace smelts ore into bars. Inside your shack, press <build> and place one: 10 stone and 2 wood. You have {Has("stone")} stone and {Has("wood")} wood.");
            return AtHome(g, null, "Your shack");
        }
        if (state.caveDeepest < 1 && PickTier < 2 && Has("copper_ore") == 0 && Has("copper_bar") == 0)
            return At(StoryStep("caverns", "Climb down into Frostfang Caverns", "Copper comes from Frostfang Caverns. Cross the bridge east to Frostfang Isle, then climb down at the cave mouth (<act>). Take something to eat, and keep your pickaxe handy for slimes."),
                MouthDoorX, MouthDoorY, "Frostfang Caverns");
        return MetalStep("copper", 2, "a copper pickaxe") ?? CopperPickGoal();
    }

    Goal OpenHatchGoal() => At(StoryStep("hatch", "Open the wreck's hatch", "Your copper pickaxe can pry the rusted hatch open. Walk out to the old wreck, face the hatch and press <act>."),
        HatchFrontX, HatchFrontY, "Old wreck");

    Goal CopperPickGoal()
    {
        var g = StoryStep("copperpick", "Make a copper pickaxe", $"At your workbench: 2 copper bars and 2 wood. You have {Has("copper_bar")} copper bars and {Has("wood")} wood.");
        g.Ready = Has("copper_bar") >= 2 && Has("wood") >= 2;
        return HomeHas("workbench") ? AtHome(g, "workbench", "Your workbench") : At(g, 4 * T + 10, 3 * T + 8, "Workbench", "house:tomas");
    }

    // After the Hollow Eel: the key card, then two iron bars for the dock.
    Goal DockGoal()
    {
        if (!state.Hinted("ch:ironAsked"))
            return AtTomas(StoryStep("keycard", "Show Tomas the key card", "The Hollow Eel was tangled up with a key card for Dr. Mara Ilao. Talk to Tomas about it."));
        if (Has("iron_bar") >= 2)
        {
            var g = StoryStep("dock", "Bring Tomas 2 iron bars", "You have them. Tomas will mend the old dock with them.");
            g.Ready = true;
            return AtTomas(g);
        }
        var step = MetalStep("iron", 2, "the dock");
        if (step != null && ShopSells("iron_bar")) step.Text += " Pip sells iron bars too.";
        return step;
    }

    // The next thing to do towards n bars of copper or iron: somewhere to smelt, a strong enough pickaxe, the ore, then
    // the furnace. Null once you have them.
    Goal MetalStep(string metal, int n, string forWhat)
    {
        string bar = metal + "_bar", ore = metal + "_ore", name = metal == "iron" ? "Iron" : "Copper";
        if (Has(bar) >= n) return null;
        int oreNeed = 2 * (n - Has(bar));
        string id = metal == "iron" ? "iron" : "copper";
        if (Has(ore) >= oreNeed)
        {
            if (!HasHome) return StoryStep($"{id}:shack", "Build a shack for a furnace", "A furnace needs a roof over it. Press <build> and place a shack: 8 wood and 4 stone.");
            if (!HomeHas("furnace")) return AtHome(StoryStep($"{id}:furnace", "Build a furnace", "Inside your shack, press <build> and place a furnace: 10 stone and 2 wood."), null, "Your shack");
            var g = StoryStep($"{id}:smelt", $"Smelt {n - Has(bar)} {metal} bar{(n - Has(bar) == 1 ? "" : "s")}",
                $"At your furnace, 2 {metal} ore and 1 wood make a bar, for {forWhat}. You have {Has(ore)} ore and {Has("wood")} wood.");
            g.Ready = Has("wood") >= n - Has(bar);
            return AtHome(g, "furnace", "Your furnace");
        }
        // Iron needs a copper pickaxe, so copper bars first; copper only needs any pickaxe.
        if (metal == "iron" && PickTier < 2) return MetalStep("copper", 2, "a copper pickaxe") ?? CopperPickGoal();
        if (PickTier < 1) return At(StoryStep($"{id}:pick", "Make a stone pickaxe", "At a workbench: 3 wood and 3 stone. Tomas's hut has one."), 4 * T + 10, 3 * T + 8, "Workbench", "house:tomas");
        var mine = StoryStep($"{id}:mine", $"Mine {metal} ore ({Has(ore)} of {oreNeed})",
            metal == "iron" ? $"Iron is the pale rock in the walls of Frostfang Caverns, from the third floor down. Your copper pickaxe cuts it. Each one gives two ore, for {forWhat}."
                : $"Copper is the orange rock in the walls of Frostfang Caverns. Face one and press <act> with your pickaxe: each gives two ore, for {forWhat}.");
        mine.Scene = "cave"; mine.Ore = metal; mine.Place = $"{name} ore"; mine.HasPoint = true;
        return mine;
    }

    // Underground, the arrow for an ore goal: the nearest rock of it on this floor, else up or down to where it is.
    Waypoint OreRoute(Goal g)
    {
        var kind = Ore(g.Ore);
        string name = g.Ore == "iron" ? "Iron" : "Copper";
        Waypoint Ladder(string how) => new(ropeTile.x * T + 5, ropeTile.y * T + 8, "Ladder", how);
        Waypoint Down(string how) => holeTile.x >= 0 ? new(holeTile.x * T + 5, holeTile.y * T + 5, "Way down", how) : Ladder(how);
        if (caveFloor == AncientFloor || caveFloor > kind.MaxFloor) return Ladder($"There's no {g.Ore} this deep: climb back up.");
        if (caveFloor < kind.MinFloor) return Down($"{name} starts on floor {kind.MinFloor}: keep climbing down.");
        var near = nodes.Where(n => !n.Mined && n.Kind == g.Ore).OrderBy(n => Dist(player.X, player.Y, n.X * T + 5, n.Y * T + 9)).FirstOrDefault();
        if (near != null) return new(near.X * T + 5, near.Y * T + 10, $"{name} ore", null);
        // Mined out on the deepest floor it's found on: back up, as below it there's none (Codex). Floors change every visit.
        if (caveFloor >= kind.MaxFloor) return Ladder($"No {g.Ore} left here, and none deeper. Climb up and come back down: the floors change every time.");
        return Down($"No {g.Ore} left on this floor: climb down to the next.");
    }

    /* ---------- Tomas ---------- */
    // His first words about a chapter come before any request he has.
    bool ChapterTalkDue => state.flags.metTomas && !state.flags.ended && (
        state.Caught("glowgill") && !state.Caught("tidecrawler") && !RocksCleared && !state.Hinted("ch:toolsAsked")
        || state.Caught("tidecrawler") && !HatchOpen && !state.Hinted("ch:homeAsked")
        || state.Caught("mirror_ray") && !RecordingHeard);

    void TomasAfterGlowgill()
    {
        Say Tm(string t) => new("Tomas", t);
        if (RocksCleared)
        {
            Talk(new() { Tm("You cleared those rocks? Good work. Whatever clicks in the pools comes out in daylight, mind.") });
            return;
        }
        if (!state.Hinted("ch:toolsAsked"))
        {
            state.hinted["ch:toolsAsked"] = true;
            Save();
            var lines = new List<Say>
            {
                Tm("A tag? \"Property of R/V Halcyon\"..."),
                Tm("So that ship was carrying more than research gear."),
                Tm("Something's been clicking around the rocky shore up north. Only in daylight."),
                Tm("Trouble is, the storm brought rocks down over the tide pools at its east end. You'll need a pickaxe to shift them.")
            };
            if (Has("axe") > 0 && PickTier > 0) lines.Add(Tm("And you've an axe and a pickaxe already. Good. Those rocks won't shift themselves."));
            else lines.AddRange(new[]
            {
                Tm("My workbench is yours. Make yourself a stone pickaxe, and an axe while you're at it: you'll want timber before long."),
                Tm("Driftwood and good flat stones wash up on every beach. Walk over them and they're yours.")
            });
            Talk(lines);
            return;
        }
        bool tools = Has("axe") > 0 && PickTier > 0;
        Talk(new()
        {
            Tm(tools ? "Now then, you've got the tools. Those fallen rocks are at the east end of the rocky shore, up north."
                : $"A stone axe takes 3 wood and 2 stone, and a pickaxe 3 wood and 3 stone. You've {Has("wood")} wood and {Has("stone")} stone so far.")
        });
    }

    void TomasAfterTidecrawler()
    {
        Say Tm(string t) => new("Tomas", t);
        if (HatchOpen)
        {
            Talk(new()
            {
                Tm("M.I.? Those initials..."),
                Tm("Never mind me. The tide's out, and that hatch is open. Whatever lives in that hull, go gently with it.")
            });
            return;
        }
        if (!state.Hinted("ch:homeAsked"))
        {
            state.hinted["ch:homeAsked"] = true;
            Give("wood", 5);
            Give("stone", 5);
            Save();
            Talk(new()
            {
                Tm("M.I.? Those initials... Never mind me."),
                Tm(state.Hinted("ch:hatchSeen") ? "That hatch on the wreck? Rusted shut since before the Halcyon ever sailed." : "The tide's out, so you can walk to the old wreck off the southwest shore. Its hatch rusted shut years ago, mind."),
                Tm("A stone pick will just bounce off it. Copper, though. A copper pickaxe would pry it open."),
                Tm("There's copper in Frostfang Caverns, across the bridge east. You'll need a furnace to smelt it, and a furnace wants a roof over it."),
                Tm("Time you had a place of your own, I'd say. Build yourself a shack, and put your own workbench and a furnace in it."),
                Tm("Here, what's left from patching my roof: five planks and five stones. Chop and break the rest."),
                Tm("Whatever lives in that hull, go gently with it.")
            }, () => Toast("Got 5 wood and 5 stone. Your journal (<journal>) has every step.", 4));
            return;
        }
        Talk(new() { Tm(TomasNudge()) });
    }

    // Where you've got to on the way to the hatch, in Tomas's words.
    string TomasNudge()
    {
        // A copper pickaxe is all the hatch needs, whatever became of the shack (as the guide says: HatchGoal).
        if (PickTier >= 2) return "That copper pick will shift the hatch. Off you go, and go gently.";
        if (!HasHome) return $"A roof first: a shack takes 8 wood and 4 stone. You've {Has("wood")} wood and {Has("stone")} stone.";
        if (!HomeHas("workbench")) return "Good shack. Now put a workbench in it: 6 wood and 2 stone.";
        if (!HomeHas("furnace")) return "A furnace next, for smelting. Ten stones and a bit of wood, inside your shack.";
        if (Has("copper_bar") >= 2) return "Two copper bars and a bit of wood make a copper pickaxe at your workbench.";
        if (Has("copper_ore") >= 2) return "Smelt that copper at your furnace: two ore and a stick of wood make a bar.";
        if (state.caveDeepest < 1) return "The caverns are on Frostfang Isle, across the bridge east. Take something to eat down there.";
        return "Copper's the orange rock in the cave walls. Four lumps make two bars, and two bars make a pickaxe.";
    }

    void TomasKeyCard()
    {
        Say Tm(string t) => new("Tomas", t);
        if (state.flags.dockFixed)
        {
            Talk(new() { Tm("The dock should hold now. Something wide and silver glides past the end of it, mostly in daylight.") });
            return;
        }
        var lines = new List<Say>();
        if (!state.Hinted("ch:ironAsked"))
        {
            state.hinted["ch:ironAsked"] = true;
            Save();
            lines.AddRange(new[]
            {
                Tm("Let me see that card."),
                Tm("Dr. Mara Ilao..."),
                Tm("Mara is my daughter. She signed on with the Halcyon three years ago. She never came home."),
                Tm("If she left something out there, I want to know. I'll mend the old dock so you can reach the deep water."),
                Tm("But its iron brackets rusted through in the storm. Bring me two iron bars and I'll do the rest."),
                Tm("Iron lies deeper in Frostfang Caverns than copper: the third floor down and below. Your copper pick will cut it, and your furnace will smelt it.")
            });
        }
        if (Has("iron_bar") >= 2)
        {
            lines.Add(Tm("Two good iron bars. That'll hold. Give me till the tide turns."));
            Talk(lines, MendDock);
            return;
        }
        if (lines.Count == 0)
            lines.Add(Tm(Has("iron_ore") >= 2 ? "Smelt that iron at your furnace, then bring me the bars. Two will do."
                : PickTier < 2 ? "Iron's too hard for a stone pick. Make a copper one first: copper ore, smelted, then your workbench."
                : $"Two iron bars for the dock. That's four lumps of iron ore, from the third floor of the caverns and below. You've {Has("iron_bar")} bars so far."));
        Talk(lines);
    }

    // The bars are taken and the dock is mended in one go, before the fade (a game closed during it keeps both).
    void MendDock()
    {
        if (Has("iron_bar") < 2 || state.flags.dockFixed) return;
        Take("iron_bar", 2);
        state.flags.dockFixed = true;
        Save();
        FadeThrough(BuildMap, () => Toast("Tomas mended the old dock, east of camp.", 3.5f));
    }

    void TomasRecording()
    {
        Say Tm(string t) => new("Tomas", t);
        state.hinted["ch:recording"] = true;
        Save();
        Talk(new()
        {
            Tm("Play that recording again..."),
            Tm("She sank the ship herself. To send them home."),
            Tm("That's my Mara. Stubborn as the tide."),
            Tm("There's an old island saying: the deepest things only rise at night. Try the end of the dock after dark.")
        });
    }

    /* ---------- The fallen rocks and the hatch ---------- */
    // Facing them (or close enough that there's no doubt), so the rocky shore's fishing beside them still works.
    bool FacingPoint(float px, float py, float r)
    {
        float dx = px - player.X, dy = py - (player.Y - 2), d = MathF.Sqrt(dx * dx + dy * dy);
        if (d > r) return false;
        if (d < 7) return true;
        var (fx, fy) = player.Face switch { "left" => (-1f, 0f), "right" => (1f, 0f), "up" => (0f, -1f), _ => (0f, 1f) };
        return (dx * fx + dy * fy) / d > 0.55f;
    }

    Target ChapterTarget()
    {
        if (scene != "world") return null;
        if (!RocksCleared && FacingPoint(RockfallX, RockfallY, 16))
            return PickTier > 0 ? new Target { Type = "rockfall", Label = "Break up the fallen rocks" }
                : new Target { Type = "info", Label = "Rocks fallen over the tide pools. A pickaxe would shift them" };
        if (state.flags.tideOut && !HatchOpen && Dist(player.X, player.Y, HatchFrontX, HatchFrontY) < 12)
            return new Target { Type = "hatch", Label = PickTier >= 2 ? "Pry the hatch open with your copper pickaxe" : "Try the rusted hatch" };
        return null;
    }

    void BreakRocks()
    {
        if (RocksCleared) return;
        rockHits++;
        shakeT = 0.25f;
        Swing("pick", 0.25f);
        FaceToward(RockfallX, RockfallY);
        Sfx.Play("mine");
        Burst(RockfallX, RockfallY - 2, "#9aa0a5", 6);
        Learned("boulder");
        if (rockHits < 4) return;
        rockHits = 0;
        state.hinted["ch:rocks"] = true;
        Give("stone", 3);
        Save();
        Burst(RockfallX, RockfallY - 2, "#7d8288", 14);
        Sfx.Play("pickup");
        Talk(new()
        {
            new Say("You", "The rocks tumble aside, and you get 3 stone out of them. Underneath, the tide pools are full of little clicking sounds."),
            new Say("You", "Whatever made them only seems to come out in daylight.")
        });
    }

    void TryHatch()
    {
        if (HatchOpen) return;
        FaceToward(HatchFrontX - 12, HatchFrontY - 3);
        if (PickTier < 2)
        {
            state.hinted["ch:hatchSeen"] = true;
            Save();
            if (PickTier > 0) Swing("pick", 0.25f);
            Sfx.Play("clang");
            shakeT = 0.15f;
            Talk(new() { new Say("You", PickTier > 0 ? "The hatch is rusted solid. Your stone pickaxe just bounces off it. Something whistles faintly on the other side."
                : "The hatch is rusted solid, and it won't budge by hand. Something whistles faintly on the other side.") }, () =>
            {
                if (!state.Hinted("ch:homeAsked")) Toast("Tomas might know how to open it.", 3);
            });
            return;
        }
        Swing("pick", 0.4f);
        Sfx.Play("clang");
        shakeT = 0.3f;
        state.hinted["ch:hatch"] = true;
        Save();
        Burst(HatchFrontX - 14, HatchFrontY - 4, "#a0522d", 10);
        Talk(new()
        {
            new Say("You", "The copper pickaxe bites into the rust. With a screech, the hatch swings open."),
            new Say("You", "Something long and pale whistles in the dark below, then slips out of the hull into the water by the wreck.")
        });
    }

    void DrawRockfall()
    {
        int x = (int)RockfallX, y = (int)RockfallY;
        pix.Rect(x - 8, y + 3, 17, 2, "rgba(0,0,0,0.22)");
        // A heap of rounded grey stones over the pools, lit from the top left like the shore's rocks: the biggest at
        // the back, smaller ones tumbled in front, a dark outline round each.
        void Stone(float cx, float cy, float rx, float ry)
        {
            for (int py = (int)MathF.Floor(cy - ry); py <= (int)MathF.Ceiling(cy + ry); py++)
                for (int px = (int)MathF.Floor(cx - rx); px <= (int)MathF.Ceiling(cx + rx); px++)
                {
                    float dx = (px + 0.5f - cx) / rx, dy = (py + 0.5f - cy) / ry, d = dx * dx + dy * dy;
                    if (d > 1) continue;
                    string c = d > 0.62f ? "#3d4248" : dy < -0.25f && dx < 0.3f ? "#b6bbbf" : dy > 0.3f ? "#6c7176" : "#8d9296";
                    pix.Rect(px, py, 1, 1, c);
                }
        }
        Stone(x - 1.5f, y - 3, 5.5f, 4.5f);
        Stone(x + 4, y - 1, 4, 3.5f);
        Stone(x - 5.5f, y + 0.5f, 3.5f, 3);
        Stone(x + 0.5f, y + 1.5f, 3.5f, 2.6f);
        Stone(x + 6, y + 2.5f, 2.2f, 1.8f);
        // Grit spilled round the foot, and a glint of water trapped between the stones.
        foreach (var (gx, gy) in new[] { (-8, 3), (-3, 4), (3, 4), (8, 3), (-6, 4) }) pix.Rect(x + gx, y + gy, 1, 1, "#9a968c");
        if ((int)(time * 2) % 3 == 0) pix.Rect(x + 2, y - 1, 1, 1, "#cfe8ee");
    }

    // The hatch on the wreck's deck: a rusted cover, or a dark hole with the cover flung back.
    void DrawHatch()
    {
        if (!HatchOpen)
        {
            pix.Rect(27, 147, 7, 3, "#5a2e16");
            pix.Rect(28, 147, 5, 2, "#8a4a24");
            pix.Rect(28, 147, 5, 1, "#a8602e");
            pix.Rect(28, 148, 1, 1, "#c07a3a"); pix.Rect(32, 148, 1, 1, "#c07a3a");
        }
        else
        {
            pix.Rect(27, 148, 6, 2, "#0d0805");
            pix.Rect(28, 148, 4, 1, "#1b120c");
            pix.Rect(33, 146, 3, 3, "#8a4a24");
            pix.Rect(33, 146, 3, 1, "#a8602e");
        }
    }

    /* ---------- The Fesh-dex's chapter strip ---------- */
    (string title, bool done)[] Chapters()
    {
        bool C(string id) => state.flags.ended || state.Caught(id);
        return new[]
        {
            ("A glow in the lagoon", C("glowgill")), ("Tools for the rocks", RocksCleared), ("Clicks in the rocks", C("tidecrawler")),
            ("A place of your own", HatchOpen), ("Inside the wreck", C("hollow_eel")), ("Iron for the dock", state.flags.dockFixed || state.flags.ended),
            ("Silver in the deep", C("mirror_ray")), ("Mara's recording", RecordingHeard), ("The deepest thing", C("abyssal"))
        };
    }

    const float ChapterStripH = 150;

    // Under the creature cards: the nine chapters, the one you're on, and the step to do now.
    void DrawChapterStrip(float x, float y, float w)
    {
        var ch = Chapters();
        int cur = Array.FindIndex(ch, c => !c.done);
        Gfx.Text(cur < 0 ? "The Saltmere mystery: solved" : $"The Saltmere mystery: chapter {cur + 1} of {ch.Length}", x, y, FontKind.Ui700, 22, Pal.PaperInk);
        const float gap = 8, h = 56;
        float cw = (w - gap * (ch.Length - 1)) / ch.Length, cy = y + 32;
        for (int i = 0; i < ch.Length; i++)
        {
            float cx = x + i * (cw + gap);
            bool now = i == cur;
            var fill = ch[i].done ? Pal.C("#d9ebc9") : now ? Pal.C("#fff3cf") : CardBg;
            Gfx.Box(cx, cy, cw, h, fill, now ? Pal.C("#d9a640") : ch[i].done ? Pal.C("#7fb36b") : Pal.C("#c9b48f"), 2, 5);
            Gfx.Text((i + 1).ToString(), cx + 8, cy + 5, FontKind.Ui700, 15, ch[i].done ? Pal.C("#3f7d35") : now ? Pal.C("#9a6a1a") : Muted);
            if (ch[i].done)
            {
                Gfx.Line(cx + cw - 20, cy + 12, cx + cw - 15, cy + 17, 2.4f, Pal.C("#3f7d35"));
                Gfx.Line(cx + cw - 15, cy + 17, cx + cw - 8, cy + 7, 2.4f, Pal.C("#3f7d35"));
            }
            // Chapters still ahead keep their names to themselves.
            string title = ch[i].done || now ? ch[i].title : "???";
            var lines = Gfx.Wrap(title, FontKind.Ui600, 14, cw - 14);
            if (lines.Count > 2) lines = new() { lines[0], Gfx.Ellipsize(string.Join(" ", lines.Skip(1)), FontKind.Ui600, 14, cw - 14) };
            Lines(lines, cx + 7, cy + 23, 16, FontKind.Ui600, 14, ch[i].done || now ? Pal.PaperInk : Muted);
        }
        float ny = cy + h + 12;
        var step = goals.FirstOrDefault(g => g.Group == "story");
        if (step == null) return;
        const float bw = 150;
        string now2 = Gfx.Ellipsize($"Now: {step.Title}. {Bind.Fix(step.Text)}", FontKind.Ui600, 16, w - bw - 16);
        Gfx.Text(now2, x, ny + 6, FontKind.Ui600, 16, Pal.PaperInk);
#if DEBUG
        Gfx.Seen["dex:journal"] = new Rectangle(x + w - bw, ny, bw, 32);
#endif
        if (Button("Open journal", x + w - bw, ny, bw, 32, FontKind.Ui700, 16, Pal.Sand, Pal.Ink, 2, 2, 5))
        {
            ClosePanels();
            TogglePanel("journal");
        }
    }
}
