namespace Fesh;

partial class Game
{
    // Pip the travelling trader, who keeps a stall on Saltmere.
    static readonly Look PipLook = new() { name = "Pip", skin = 3, hair = 1, hairColor = 4, hat = 3, shirt = 4, pants = 1 };

    /* ---------- Pip's stall ---------- */
    void TalkPip()
    {
        Say P(string t) => new("Pip", t);
        if (!PipOpen) { Sfx.Play("nope"); Toast("Pip's stall has just closed for the night."); return; }
        if (!state.Hinted("metPip"))
        {
            state.hinted["metPip"] = true;
            Talk(new()
            {
                P("Oh! A customer! I'm Pip. I trade up and down these islands."),
                P("I'll buy any fish you catch, plus ore, wool, truffles, that sort of thing."),
                P("And I sell bait, chum, tackle, crab pots, metal bars and crystal. Even sailcloth, if you ever want to build a boat."),
                P("Oh, and once a day I run a fishing derby. Biggest fish in three minutes wins a prize!"),
                P("They say there's an atoll out east that no bridge reaches. Just saying.")
            }, () => OpenShop());
            return;
        }
        // Once you've been to the atoll, Pip passes on a sailors' story (until you've found out for yourself).
        if (state.Hinted("visitedAtoll") && !StarwellFound && !state.Hinted("pipHoofprints"))
        {
            state.hinted["pipHoofprints"] = true;
            Talk(new()
            {
                P("Back from the atoll? Then tell me: did you see the hoofprints?"),
                P("Sailors swear there are hoofprints in the sand out there, heading off into the palms. Hoofprints! On an atoll!"),
                P("Nobody's ever seen a horse, mind. Anyway. What can I get you?")
            }, () => OpenShop());
            return;
        }
        // Once you can sail the open sea from the atoll, Pip passes on where the southern islands are.
        if (state.Hinted("visitedAtoll") && Has("boat") > 0 && !state.Hinted("habagat") && !state.Hinted("pipHabagat"))
        {
            state.hinted["pipHabagat"] = true;
            Talk(new()
            {
                P("A boat of your own! Then listen: the fishers from the south say there are islands down past the Dunes and Mirewood."),
                P("Salt beds, a hundred little islets, and an old lighthouse that's gone dark. They call the wind there the habagat."),
                P("You can't get there from my jetty, it's all bridges this side. Take the helm at the atoll's jetty and head south.")
            }, () => OpenShop());
            return;
        }
        // Once a day Pip passes on what the boats say about tomorrow's weather.
        if (!KnowTomorrow)
        {
            TellTomorrow();
            Talk(new() { P($"Word from the boats about tomorrow: {DescribeDay(state.tomorrow)}. Now, what can I get you?") }, () => OpenShop());
            return;
        }
        OpenShop();
    }

    string shopTab = "sell";

    void OpenShop()
    {
        Sfx.Play("ui");
        panel = "shop";
        mode = "panel";
        SetPrompt("");
    }

    void Sell(string id, int n)
    {
        n = Math.Min(n, Has(id));
        int value = SaleValue(id, n);
        if (n <= 0 || value <= 0) return;
        SoldFish(id, n);
        Take(id, n);
        state.coins += value;
        Sfx.Play("coin");
        Toast($"Sold {Items.Amount(id, n)} for {value} coins.", 1.8f);
        Save();
    }

    void SellAllFish()
    {
        int total = 0, count = 0;
        foreach (var (id, n) in state.inv.Where(kv => Items.ById[kv.Key].Kind == "fish" && !Data.FishById[kv.Key].Rare).ToList())
        {
            total += SaleValue(id, n);
            count += n;
            SoldFish(id, n);
            Take(id, n);
        }
        if (count == 0) { Sfx.Play("nope"); Toast("No ordinary fish to sell. Rare fish are sold one at a time."); return; }
        state.coins += total;
        Sfx.Play("coin");
        Toast($"Sold {count} fish for {total} coins.", 2.2f);
        Save();
    }

    void Buy(string id, int price, int n)
    {
        if (state.coins < price * n) { Sfx.Play("nope"); Toast($"You need {price * n - state.coins} more coins."); return; }
        state.coins -= price * n;
        Give(id, n);
        Sfx.Play("coin");
        string tip = id switch
        {
            "bait" when !state.Hinted("bait") => " Bait is used up automatically, one per cast.",
            "sapling" when !state.Hinted("sapling") => " Plant it outdoors with <build>.",
            "sailcloth" when !state.Hinted("sailcloth") => " A boat is made at a workbench: 20 wood, 4 iron bars and this.",
            "crab_pot" when !state.Hinted("crab_pot") => " Press <build> outdoors and set it in shallow water.",
            "chum" when !state.Hinted("chum") => " At a fishing spot, press <alt> to throw it.",
            "cork_bobber" or "spinner_lure" when !state.Hinted("tackleTip") => " Change your tackle with <tackle>.",
            _ => ""
        };
        if (tip != "") state.hinted[id is "cork_bobber" or "spinner_lure" ? "tackleTip" : id] = true;
        Toast($"Bought {Items.Amount(id, n)}.{tip}", tip == "" ? 1.8f : 4.5f);
        Save();
    }

    /* ---------- Tomas's requests ---------- */
    Req NextRequest()
    {
        if (state.reqDone < Items.RequestChain.Length)
        {
            var r = Items.RequestChain[state.reqDone];
            return new Req { item = r.Item, count = r.Count, coins = r.Coins, bonus = r.Bonus, bonusCount = r.BonusCount };
        }
        // After the set list: any ordinary fish from somewhere you can already reach.
        var pool = Data.Spots.Where(s => (s.Biome != "atoll" || state.Hinted("visitedAtoll")) && (s.Biome != "amihan" || state.Hinted("amihan"))
                && (s.Biome != "habagat" || state.Hinted("habagat")) && SpotKnown(s) && (s.Scene != "sea" || Has("boat") > 0 || state.tamed))
            .SelectMany(s => Data.Common[s.Id]).Where(f => !f.Rare && (f.Need == null || state.Hinted(f.Need))).ToList();
        var fish = pool[rng.Next(pool.Count)];
        int count = 2 + rng.Next(3);
        return new Req
        {
            item = fish.Id, count = count, coins = Items.SellPrice(fish.Id) * count * 2 + 10,
            bonus = rng.NextDouble() < 0.4 ? "bait" : null, bonusCount = 3
        };
    }

    string RewardText(Req r) => $"{r.coins} coins" + (r.bonus != null ? $" and {Items.Amount(r.bonus, r.bonusCount)}" : "");

    // A request line added to whatever Tomas is saying. Returns null if he has nothing to ask.
    Say RequestLine()
    {
        if (!state.flags.metTomas) return null;
        if (state.req == null)
        {
            state.req = NextRequest();
            Save();
            return new Say("Tomas", $"Oh, and if you're heading out: could you bring me {Items.Amount(state.req.item, state.req.count)}? I'll give you {RewardText(state.req)}.");
        }
        return new Say("Tomas", $"Still hoping for {Items.Amount(state.req.item, state.req.count)}, if you come across them. You have {Has(state.req.item)}.");
    }

    bool CanHandIn => state.req != null && Has(state.req.item) >= state.req.count;

    void HandIn()
    {
        var r = state.req;
        Take(r.item, r.count);
        state.coins += r.coins;
        if (r.bonus != null) Give(r.bonus, r.bonusCount);
        state.req = null;
        state.reqDone++;
        Save();
        Sfx.Play("coin");
        Talk(new()
        {
            new("Tomas", $"{Items.Amount(r.item, r.count)}! That's exactly what I needed. Thank you, {state.look.name}."),
            new("", $"Tomas gives you {RewardText(r)}.")
        });
    }

    /* ---------- Aquariums ---------- */
    string tankKey;
    string TankKey(Build b) => $"{scene}|{b.x},{b.y}";
    List<string> Tank(string key) => state.tanks.TryGetValue(key, out var l) ? l : state.tanks[key] = new List<string>();

    void OpenTank(Build b)
    {
        tankKey = TankKey(b);
        Sfx.Play("ui");
        panel = "tank";
        mode = "panel";
        SetPrompt("");
    }

    void TankPut(string fishId)
    {
        var tank = Tank(tankKey);
        var before = Data.AquaSets.Where(s => SetActive(s.Id)).Select(s => s.Id).ToHashSet();
        if (tank.Count >= 4 || !Take(fishId)) { Sfx.Play("nope"); return; }
        tank.Add(fishId);
        Sfx.Play("splash");
        if (Data.AquaSets.FirstOrDefault(s => !before.Contains(s.Id) && SetActive(s.Id)) is AquaSet done)
        {
            Sfx.Play("rare");
            Toast($"Collection complete: {done.Name}! {done.Perk} while they're on show.", 4.5f);
        }
        Save();
    }

    void TankTake(int index)
    {
        var tank = Tank(tankKey);
        if (index >= tank.Count) return;
        Give(tank[index]);
        tank.RemoveAt(index);
        Sfx.Play("pickup");
        Save();
    }

    // Puts every fish from an aquarium back in the bag (used when it or its shack is taken down).
    int EmptyTank(string key)
    {
        if (!state.tanks.Remove(key, out var fish)) return 0;
        foreach (var f in fish) Give(f);
        return fish.Count;
    }

    /* ---------- Boat ---------- */
    void Sail(string to)
    {
        if (Has("boat") == 0)
        {
            Sfx.Play("nope");
            Toast("You'd need a boat. Make one at a workbench: 20 wood, 4 iron bars and sailcloth from Pip.", 4.5f);
            return;
        }
        if (state.weather == "storm") { Sfx.Play("nope"); Toast("Far too rough to sail in a storm. Wait for it to pass."); return; }
        bool atoll = to == "atoll", asinan = to == "asinan";
        LeaveMount();
        race = null; raceArmed = false;
        Sfx.Play("splash");
        FadeThrough(() =>
        {
            state.boatAt = to;
            state.aboard = false; state.boatX = state.boatY = 0;
            player.X = atoll ? AtollJettyX + 4 : asinan ? AsinanJettyX : SaltJettyX - 4;
            player.Y = atoll ? AtollJettyY + 1 : asinan ? AsinanJettyY + 6 : SaltJettyY + 1;
            player.Face = atoll ? "right" : asinan ? "down" : "left";
            lastBiome = (byte)(atoll ? 4 : asinan ? 6 : 0);
            Save();
        }, () =>
        {
            var b = Data.Biomes[lastBiome];
            Toast(atoll || asinan ? $"{b.Enter} ({b.Climate})" : b.Enter, 3.5f);
            if (atoll && !state.Hinted("visitedAtoll"))
            {
                state.hinted["visitedAtoll"] = true;
                Save();
            }
        });
    }

    // Once you've found the Habagat islands, a jetty asks where you'd like to sail (the panel "voyage").
    bool VoyageChoice => state.Hinted("habagat") && Has("boat") > 0;

    void OpenVoyage()
    {
        Sfx.Play("ui");
        panel = "voyage";
        mode = "panel";
        SetPrompt("");
    }

    void DrawVoyage()
    {
        Backdrop();
        // Where you are now isn't offered.
        string here = Dist(player.X, player.Y, AtollJettyX, AtollJettyY) < 14 ? "atoll" : Dist(player.X, player.Y, AsinanJettyX, AsinanJettyY) < 14 ? "asinan" : "saltmere";
        var places = new List<(string id, string name, string sub)>
        {
            ("saltmere", "Saltmere", "Pip's jetty, by Tomas's camp"), ("atoll", "Starfall Atoll", "The atoll's jetty, out east"),
            ("asinan", "Asinan", "The landing by the salt beds, in the Habagat islands")
        };
        places.RemoveAll(p => p.id == here || p.id == "asinan" && !state.Hinted("habagat"));
        const float w = 620, pad = 24, rowH = 62, gap = 10;
        float h = pad + 44 + 8 + 26 + 18 + places.Count * (rowH + gap) - gap + pad;
        float x = (Gfx.LW - w) / 2, y = (Gfx.LH - h) / 2;
        Gfx.Box(x, y, w, h, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Text("Set sail", x + pad, y + pad, FontKind.Ui700, 32, Pal.PaperInk);
        if (SmallButton("Close", x + w - pad - SmallW("Close"), y + pad - 4)) { ClosePanels(); return; }
        float cy = y + pad + 44 + 8;
        Gfx.Text("Where to? (Or press F at the jetty to take the helm yourself.)", x + pad, cy, FontKind.Ui500, 18, Muted);
        cy += 26 + 18;
        foreach (var (id, name, sub) in places)
        {
            float rx = x + pad, rw = w - pad * 2;
            Gfx.Box(rx, cy, rw, rowH, Gfx.Hover(rx, cy, rw, rowH) ? Lighten(Pal.Sand, 0.14f) : Pal.Sand, Pal.Ink, 3, 6, 4);
            Gfx.Text(name, rx + 18, cy + (rowH - 24) / 2 - 1, FontKind.Ui700, 24, Pal.Ink);
            Gfx.Text(sub, rx + rw - 18 - Gfx.Measure(sub, FontKind.Ui500, 16), cy + (rowH - 16) / 2, FontKind.Ui500, 16, Muted);
#if DEBUG
            Gfx.Seen["voyage:" + id] = new Raylib_cs.Rectangle(rx, cy, rw, rowH);
#endif
            if (Gfx.Click(rx, cy, rw, rowH)) { ClosePanels(); Sail(id); return; }
            cy += rowH + gap;
        }
        if (Gfx.PressedOutside(x, y, w, h)) ClosePanels();
    }

    void PickPlanter(Build b)
    {
        string key = $"b:{b.x},{b.y}";
        if (state.picked.GetValueOrDefault(key) == state.day) { Toast("No berries left. They grow back by tomorrow."); return; }
        int n = 2 + rng.Next(2);
        state.picked[key] = state.day;
        Give("berries", n);
        Sfx.Play("pickup");
        Burst(b.x * T + 5, b.y * T + 4, "#e04b3a", 6);
        Toast($"Picked {n} berries.");
        Save();
    }
}
