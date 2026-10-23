using Raylib_cs;

namespace Fesh;

// Three more day events (1.45.1), rolled by Events.cs like the first five:
//  whale   a whale is singing off a beach; stand at the marked place from 06:00 to 10:00 and listen: the sea feels awake, so
//          rare luck +1 until noon. Watching and listening only. (Whales really do sing, and sound carries a long way under
//          water; the game doesn't say which whale.)
//  market  market day in Amihan Village's square, 08:00-16:00: a few stalls of local goods you won't find at Pip's (it reuses
//          the trader's panel)
//  kite    a child's kite blew away and landed on a beach of Amihan Village; pick it up (the event carries over the dawn
//          until it's given back) and give it to Ma'am Isay for her thanks and a little from the school fund
partial class Game
{
    /* ---------- Whale song ---------- */
    bool WhaleOpen => EventOn("whale") && !Night && state.clock >= 6 * 60 && state.clock < 10 * 60 && !Stormy;
    bool WhaleBoost => EventOn("whale") && TodayEvent().stage == 1 && !Night && state.clock < 12 * 60;

    void ListenToWhale(DayEvent ev)
    {
        FaceToward(ev.x, ev.y);
        ev.stage = 1;
        Sfx.Play("whale");
        Save();
        Talk(new()
        {
            new("", "You stand still and listen. Under the wash of the waves there's a long, low call, then another, rising and falling like a slow song."),
            new("", "Whales really do sing, and sound carries a very long way under the water. This one is far out. You'd never see it from here."),
            new("", "The sea feels awake this morning. Rare fish may rise until noon.")
        });
    }

    /* ---------- Market day ---------- */
    (float x, float y)? MarketSpot()
    {
        var solids = Solids();
        foreach (float dx in new[] { 0f, -34f, 34f, -60f })
            if (CanStandBare(RondallaX + dx, RondallaY + 10, solids) && CanStandBare(RondallaX + dx + 14, RondallaY + 10, solids) && CanStandBare(RondallaX + dx - 14, RondallaY + 10, solids))
                return (RondallaX + dx, RondallaY + 10);
        return null;
    }

    bool MarketHere => EventOn("market") && !Night && state.clock >= 8 * 60 && state.clock < 16 * 60 && !Stormy;
    bool TradeOpen => TraderHere || MarketHere;

    static List<Offer> MarketOffers(MatchRng rng)
    {
        var pool = new List<Offer>
        {
            new() { item = "salt", n = 3, price = 15 }, new() { item = "suka", n = 2, price = 10 }, new() { item = "calamansi", n = 4, price = 16 },
            new() { item = "tinapa", n = 2, price = 46 }, new() { item = "dried_guso", n = 2, price = 40 }, new() { item = "tuyo", n = 3, price = 36 }
        };
        var pick = new List<Offer>();
        while (pick.Count < 3 && pool.Count > 0)
        {
            int k = (int)(rng.NextDouble() * pool.Count) % pool.Count;
            pick.Add(pool[k]); pool.RemoveAt(k);
        }
        return pick;
    }

    /* ---------- The kite ---------- */
    const string VillageRegion = "amihan:Amihan Village";
    bool KiteLying => EventOn("kite") && TodayEvent().stage == 0;
    internal bool KiteHeld => !EventsOff && !MatchActive && state?.dayEvent is { kind: "kite", stage: 1 };

    void PickKite(DayEvent ev)
    {
        FaceToward(ev.x, ev.y);
        ev.stage = 1;
        Sfx.Play("pickup");
        Save();
        Talk(new()
        {
            new("", "A paper kite on a split-bamboo frame, its tail of rag strips tangled in the sand. There's a name inked on the back: \"Jun\"."),
            new("", "Ma'am Isay will know whose it is. Take it back to her.")
        });
    }

    // Ma'am Isay: the kite goes home, for her thanks and a little from the school fund.
    bool IsayKite()
    {
        if (!KiteHeld) return false;
        var ev = state.dayEvent;
        ev.stage = 2;
        state.coins += 35;
        state.hinted["kiteReturned"] = true;
        if (!FriendsOff)
        {
            if (!state.friends.ContainsKey("isay")) state.friends["isay"] = new Friendship();
            AddFriendPoints("isay", 3);
        }
        Sfx.Play("coin");
        Save();
        Say I(string t) => new("Ma'am Isay", t);
        Talk(new()
        {
            I("Jun's kite! He cried all morning. And you found it, tangled in the sand?"),
            I("Here, a little from the school fund for your trouble. I'll tell him a fisher brought it home. He'll want to fly it to you, I think.")
        });
        return true;
    }

    /* ---------- Hooks used by Events.cs ---------- */
    Target MoreEventTarget(DayEvent ev)
    {
        float x = player.X, y = player.Y;
        if (WhaleOpen && ev.stage == 0 && Dist(x, y, ev.x, ev.y) < 14) return new Target { Type = "event", Id = "whale", Label = "Listen for the whale song" };
        if (MarketHere && Dist(x, y, ev.x, ev.y + 4) < 40) return new Target { Type = "event", Id = "market", Label = "Browse the market stalls" };
        if (KiteLying && Dist(x, y, ev.x, ev.y) < 12) return new Target { Type = "event", Id = "kite", Label = "Pick up the kite" };
        return null;
    }

    bool UseMoreEvent(string id, DayEvent ev)
    {
        switch (id)
        {
            case "whale": ListenToWhale(ev); return true;
            case "market": FaceToward(ev.x, ev.y); Sfx.Play("ui"); panel = "trader"; mode = "panel"; SetPrompt(""); return true;
            case "kite": PickKite(ev); return true;
        }
        return false;
    }

    string MoreEventNews(DayEvent ev) => ev.kind switch
    {
        "whale" => $"A whale is singing off {IslandName(RegionOf(ev.x, ev.y))}. Listen from the marked beach between {HourText(6 * 60)} and {HourText(10 * 60)}.",
        "market" => $"Market day in Amihan Village: stalls in the square from {HourText(8 * 60)} to {HourText(16 * 60)}.",
        "kite" => "A child's kite blew away and landed on a beach in Amihan Village. Bring it back to Ma'am Isay.",
        _ => ""
    };

    Goal MoreEventGoal(DayEvent ev) => ev.kind switch
    {
        "whale" when ev.stage == 0 && WhaleOpen => At(NewGoal("event:whale", "daily", "A whale is singing", "Stand quietly at the marked beach and listen (<act>)."), ev.x, ev.y, "The listening place"),
        "market" when MarketHere && ev.offers.Any(o => !o.sold) => At(NewGoal("event:market", "daily", "Market day", $"Stalls of local goods in Amihan Village's square until {HourText(16 * 60)}."), ev.x, ev.y + 4, "The market"),
        "kite" when ev.stage == 0 => At(NewGoal("event:kite", "daily", "A lost kite", "It landed on a beach in Amihan Village. Pick it up (<act>)."), ev.x, ev.y, "The kite"),
        "kite" when ev.stage == 1 => NewGoal("event:kite", "daily", "Take the kite to Ma'am Isay", "She knows whose it is."),
        _ => null
    };

    void MoreEventObjects(DayEvent ev, List<(float y, Action draw)> list, Func<float, float, bool> near)
    {
        if (ev.kind == "whale" && WhaleOpen && near(ev.x, ev.y)) list.Add((ev.y - 40, () => DrawWhaleOffshore(ev)));
        if (MarketHere && near(ev.x, ev.y)) list.Add((ev.y, () => DrawMarket(ev.x, ev.y)));
        if (ev.kind == "kite" && ev.stage == 0 && near(ev.x, ev.y)) list.Add((ev.y, () => DrawKiteOnSand(ev.x, ev.y)));
    }

    void DrawKiteOnSand(float x, float y)
    {
        int X = (int)x, Y = (int)y;
        pix.Rect(X - 4, Y, 9, 1, "rgba(0,0,0,0.18)");
        // A diamond of coloured paper on a bamboo cross, with a rag tail trailing across the sand.
        for (int i = 0; i < 4; i++) pix.Rect(X - i, Y - 5 + i, 2 * i + 1, 1, i % 2 == 0 ? "#e0584a" : "#f3c25b");
        for (int i = 0; i < 3; i++) pix.Rect(X - 2 + i, Y - 1 + i, 2 * (3 - i) - 1, 1, i == 1 ? "#e0584a" : "#4a90a8");
        pix.Rect(X, Y - 5, 1, 7, "#8a6038");
        pix.Rect(X - 3, Y - 2, 7, 1, "#8a6038");
        float sway = Calm ? 0 : MathF.Sin(time * 2.2f) * 0.6f;
        for (int i = 0; i < 6; i++) pix.Rect(X + 3 + i + sway, Y + 1 + (i % 2), 1, 1, i % 2 == 0 ? "#4a90a8" : "#f3c25b");
    }

    void DrawMarket(float x, float y)
    {
        // Two little stalls under striped awnings with crates of goods, and a seller between them.
        for (int s = 0; s < 2; s++)
        {
            int X = (int)x + (s == 0 ? -17 : 17), Y = (int)y;
            string a = s == 0 ? "#c0392b" : "#2a7d74";
            pix.Rect(X - 8, Y - 1, 17, 2, "rgba(0,0,0,0.18)");
            pix.Rect(X - 7, Y - 8, 15, 8, "#8a6038"); pix.Rect(X - 7, Y - 8, 15, 1, "#b58250");
            pix.Rect(X - 8, Y - 15, 17, 4, a);
            for (int i = 0; i < 17; i += 4) pix.Rect(X - 8 + i, Y - 15, 2, 4, "#f3eedd");
            pix.Rect(X - 8, Y - 11, 17, 1, "rgba(0,0,0,0.25)");
            pix.Rect(X - 6, Y - 10, 3, 2, s == 0 ? "#e0b030" : "#d8d4c4"); pix.Rect(X - 1, Y - 10, 3, 2, "#4aa05a"); pix.Rect(X + 4, Y - 10, 3, 2, s == 0 ? "#c66a2a" : "#7ab0c0");
        }
        bool close = Dist(player.X, player.Y, x, y) < 50;
        LookData.DrawPerson(pix, new Look { skin = 2, shirt = 3, hat = 1, hair = 3 }, (int)x, (int)y + 1, close ? "down" : "left", 0, arms: close ? 1 : 0, swing: (int)(time * 3) % 2);
    }

    // Far out beyond the shore: a dark back rolling, and now and then a spout (still under Reduce motion).
    void DrawWhaleOffshore(DayEvent ev)
    {
        float best = 99, dx = 0, dy = 1;
        for (int k = 0; k < 8; k++)
        {
            float ux = MathF.Cos(k * MathF.PI / 4), uy = MathF.Sin(k * MathF.PI / 4);
            for (int d = 6; d < 60 && d < best; d += 2)
                if (WaterTile(TileAt((int)((ev.x + ux * d) / T), (int)((ev.y + uy * d) / T)))) { best = d; dx = ux; dy = uy; break; }
        }
        float wx = ev.x + dx * (best + 34), wy = ev.y + dy * (best + 34);
        if (!IsWater(wx, wy)) return;
        float ph = Calm ? 0.4f : (time * 0.16f) % 1;   // a slow roll: up, spout, down
        float up = MathF.Sin(MathF.Min(1, ph / 0.6f) * MathF.PI);
        if (up <= 0.02f) return;
        int X = (int)wx, Y = (int)wy;
        pix.Rect(X - 9, Y, 19, 1, Pal.Rgba(255, 255, 255, 0.35f * up));
        int h = (int)MathF.Ceiling(3 * up);
        for (int r = 0; r < h; r++) pix.Rect(X - 7 + r, Y - 1 - r, 15 - 2 * r, 1, r == 0 ? "#2a3f52" : "#3b566b");
        pix.Rect(X + 5, Y - 3, 2, 1, "#2a3f52");
        if (ph is > 0.2f and < 0.55f)
            for (int i = 0; i < 5; i++) pix.Rect(X - 2 + (i % 2) * 2 + (i > 2 ? 1 : 0), Y - 5 - i, 1, 1, Pal.Rgba(240, 248, 252, 0.85f - i * 0.12f));
    }
}
