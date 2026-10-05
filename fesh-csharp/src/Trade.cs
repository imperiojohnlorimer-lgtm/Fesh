namespace Fesh;

partial class Game
{
    // Pip the travelling trader, who keeps a stall on Saltmere.
    static readonly Look PipLook = new() { name = "Pip", skin = 3, hair = 1, hairColor = 4, hat = 3, shirt = 4, pants = 1 };

    /* ---------- Pip's stall ---------- */
    void TalkPip()
    {
        Say P(string t) => new("Pip", t);
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
            "sapling" when !state.Hinted("sapling") => " Plant it outdoors with B.",
            "sailcloth" when !state.Hinted("sailcloth") => " A boat is made at a workbench: 20 wood, 4 iron bars and this.",
            "crab_pot" when !state.Hinted("crab_pot") => " Press B outdoors and set it in shallow water.",
            "chum" when !state.Hinted("chum") => " At a fishing spot, press F to throw it.",
            "cork_bobber" or "spinner_lure" when !state.Hinted("tackleTip") => " Change your tackle with T.",
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
        var pool = Data.Spots.Where(s => s.Biome != "atoll" || state.Hinted("visitedAtoll"))
            .SelectMany(s => Data.Common[s.Id]).Where(f => !f.Rare).ToList();
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
        if (state.weather == "storm") { Sfx.Play("nope"); Toast("Far too rough to sail in a storm. Try tomorrow."); return; }
        bool atoll = to == "atoll";
        Sfx.Play("splash");
        FadeThrough(() =>
        {
            state.boatAt = to;
            player.X = atoll ? AtollJettyX + 4 : SaltJettyX - 4;
            player.Y = atoll ? AtollJettyY + 1 : SaltJettyY + 1;
            player.Face = atoll ? "right" : "left";
            lastBiome = (byte)(atoll ? 4 : 0);
            Save();
        }, () =>
        {
            var b = Data.Biomes[lastBiome];
            Toast(atoll ? $"{b.Enter} ({b.Climate})" : b.Enter, 3.5f);
            if (atoll && !state.Hinted("visitedAtoll"))
            {
                state.hinted["visitedAtoll"] = true;
                Save();
            }
        });
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
