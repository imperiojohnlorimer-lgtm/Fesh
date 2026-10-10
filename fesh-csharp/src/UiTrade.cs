using Raylib_cs;

namespace Fesh;

partial class Game
{
    int shopPage;
    string shopTip;          // the item the mouse is over in the shop: its full description is drawn last, by the mouse
    float lastShopBottom;    // the lowest row the buy list drew (the autotest checks it stays inside the stall)

    void DrawCoins(float x, float y, float size = 26)
    {
        DrawIcon("coin", x, y, size);
        Gfx.Text($"{state.coins} coins", x + size + 8, y + size / 2 - 11, FontKind.Ui700, 22, Pal.C("#9a6a1a"));
    }

    /* ---------- Pip's stall ---------- */
    void DrawShop()
    {
        Backdrop();
        const float cw = 1080, ch = 680, pad = 24;
        float x = (Gfx.LW - cw) / 2, y = (Gfx.LH - ch) / 2;
        shopTip = null;
        Gfx.Box(x, y, cw, ch, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Text("Pip's stall", x + pad, y + pad, FontKind.Ui700, 36, Pal.PaperInk);
        float tx = x + pad + Gfx.Measure("Pip's stall", FontKind.Ui700, 36) + 28;
        foreach (var (id, label) in new[] { ("sell", "Sell"), ("buy", "Buy"), ("derby", "Derby") })
        {
            float w = SmallW(label);
            if (Button(label, tx, y + pad - 2, w, 44, FontKind.Ui700, 20, shopTab == id ? Pal.Lantern : Pal.Sand, Pal.Ink, 4)) { shopTab = id; shopPage = 0; }
            tx += w + 10;
        }
        DrawCoins(tx + 20, y + pad + 6);
        if (SmallButton("Close", x + cw - pad - SmallW("Close"), y + pad - 2)) ClosePanels();
        float ly = y + pad + 62;
        if (shopTab == "sell") DrawSellList(x + pad, ly, cw - pad * 2, y + ch - pad - ly);
        else if (shopTab == "buy") DrawBuyList(x + pad, ly, cw - pad * 2);
        else DrawDerbyTab(x + pad, ly, cw - pad * 2);
        if (shopTip != null) DrawItemTip(shopTip, Gfx.Mouse.X, Gfx.Mouse.Y);
        if (Gfx.PressedOutside(x, y, cw, ch)) ClosePanels();
    }

    const float DerbyFishW = 520 - 170 - 14;   // a rival's fish and its weight, inside their 520 px row

    // Pip's daily derby: today's rivals, the prizes, and the button to start.
    void DrawDerbyTab(float x, float y, float w)
    {
        Gfx.Text("Catch the heaviest fish you can in three minutes. Any island, any spot, rod or spear.", x, y + 8, FontKind.Note, 19, Muted);
        Gfx.Text("The clock only runs while you're out fishing, not while you're in a menu.", x, y + 36, FontKind.Note, 19, Muted);
        y += 82;
        Gfx.Text("Today's anglers to beat", x, y, FontKind.Ui700, 22, Pal.PaperInk);
        y += 36;
        foreach (var (name, kg, fishName) in DerbyRivals(state.day))
        {
            Gfx.Box(x, y, 520, 46, CardBg, Pal.C("#c9b48f"), 2, 5);
            Gfx.Text(name, x + 14, y + 12, FontKind.Ui700, 19, Pal.PaperInk);
            Gfx.Text(Gfx.Ellipsize($"{fishName}, {Kg(kg)}", FontKind.Ui500, 18, DerbyFishW), x + 170, y + 13, FontKind.Ui500, 18, Muted);
            y += 54;
        }
        float px = x + 560, py = y - 54 * 3 - 36;
        Gfx.Text("Prizes", px, py, FontKind.Ui700, 22, Pal.PaperInk);
        string first = Has("golden_hook") == 0 ? "a golden hook" : Has("gold_reel") == 0 ? "a gold reel" : "5 glow bait";
        foreach (var (place, prize) in new[] { ("1st", $"120 coins and {first}"), ("2nd", "50 coins and 3 glow bait"), ("3rd", "20 coins and 5 bait") })
        {
            py += 36;
            Gfx.Text(place, px, py, FontKind.Ui700, 19, Pal.C("#9a6a1a"));
            Gfx.Text(prize, px + 56, py, FontKind.Ui500, 18, Pal.PaperInk);
        }
        Gfx.Text($"Derbies won: {state.derbyWins}. The other anglers get better each time you win.", px, py + 42, FontKind.Ui500, 16, Muted);
        y += 20;
        if (DerbyOn) Gfx.Text($"The derby is on! {(int)derbyT / 60}:{(int)derbyT % 60:00} left. Go fish!", x, y + 14, FontKind.Ui700, 22, Pal.C("#3f7d35"));
        else if (state.derbyDay == state.day) Gfx.Text("You've fished today's derby. Come back tomorrow!", x, y + 14, FontKind.Ui700, 22, Muted);
        else if (BigButton("Start the derby", x, y, true)) StartDerby();
    }

    void DrawSellList(float x, float y, float w, float h)
    {
        Gfx.Text("Pip buys fish and materials. Rare fish fetch a lot more.", x, y + 8, FontKind.Note, 19, Muted);
        float bw = Gfx.Measure("Sell all ordinary fish", FontKind.Ui700, 18) + 30;
        if (Button("Sell all ordinary fish", x + w - bw, y, bw, 40, FontKind.Ui700, 18, Pal.Buoy, White, 3, 2, 5)) SellAllFish();
        var items = BagItems().Where(id => Items.SellPrice(id) > 0).ToList();
        y += 54;
        if (items.Count == 0) { Gfx.Text("Nothing Pip wants right now. Go catch something!", x, y + 10, FontKind.Note, 20, Muted); return; }
        const float rowH = 52, gap = 8;
        int rows = (int)((h - 54 - 50) / (rowH + gap)), perPage = rows * 2, pages = (items.Count + perPage - 1) / perPage;
        shopPage = Math.Clamp(shopPage, 0, Math.Max(0, pages - 1));
        float colW = (w - 16) / 2;
        var page = items.Skip(shopPage * perPage).Take(perPage).ToList();
        for (int i = 0; i < page.Count; i++)
        {
            string id = page[i];
            float rx = x + (i / rows) * (colW + 16), ry = y + (i % rows) * (rowH + gap);
            Gfx.Box(rx, ry, colW, rowH, CardBg, Pal.C("#c9b48f"), 2, 5);
            DrawIcon(id, rx + 8, ry + 8, 36);
            var d = Items.ById[id];
            // Pointing at the name or picture shows what it is (the buttons keep their own hover).
            if (Gfx.Hover(rx, ry, colW - 200, rowH)) shopTip = id;
            bool rare = d.Kind == "fish" && Data.FishById[id].Rare;
            Gfx.Text(Gfx.Ellipsize($"{d.Name} ×{Has(id)}", FontKind.Ui700, 18, ClosedSeason(id) ? colW - 70 : colW - 300), rx + 54, ry + 8, FontKind.Ui700, 18, rare ? Rust : Pal.PaperInk);
            // In its closed season Pip won't buy it (Release.cs).
            if (ClosedSeason(id))
            {
                Gfx.Text("Closed season: Pip won't buy it until the habagat", rx + 54, ry + 29, FontKind.Ui500, 15, Rust);
                continue;
            }
            int big = d.Kind == "fish" ? BigCount(id) : 0;
            Gfx.Text($"{PriceOf(id)} each" + (big > 0 ? $" ({big} big: {(int)MathF.Round(PriceOf(id) * 1.6f)})" : ""), rx + 54, ry + 29, FontKind.Ui500, 15, big > 0 ? Pal.C("#9a6a1a") : Muted);
            if (Button("Sell 1", rx + colW - 196, ry + 8, 86, 36, FontKind.Ui700, 17, Pal.Sand, Pal.Ink, 3, 2, 5)) Sell(id, 1);
            if (Button("Sell all", rx + colW - 102, ry + 8, 94, 36, FontKind.Ui700, 17, Pal.Sand, Pal.Ink, 3, 2, 5)) Sell(id, Has(id));
        }
        if (pages > 1)
        {
            float py = y + rows * (rowH + gap) + 4;
            Gfx.Text($"Page {shopPage + 1} of {pages}", x, py + 10, FontKind.Ui600, 17, Muted);
            if (Button("Previous", x + 140, py, 120, 38, FontKind.Ui700, 17, Pal.Sand, Pal.Ink, 3, 2, 5, shopPage > 0)) shopPage--;
            if (Button("Next", x + 270, py, 100, 38, FontKind.Ui700, 17, Pal.Sand, Pal.Ink, 3, 2, 5, shopPage < pages - 1)) shopPage++;
        }
    }

    void DrawBuyList(float x, float y, float w)
    {
        Gfx.Text("Pip's goods. Sailcloth and vinegar are the only things you can't make yourself.", x, y + 8, FontKind.Note, 19, Muted);
        y += 50;
        // Six rows of two fit the stall's 680 px, each with room for two lines of what the thing is. Anything longer
        // is in full on the tooltip (shopTip).
        const float rowH = 80, gap = 6;
        float colW = (w - 16) / 2;
        for (int i = 0; i < Items.Shop.Length; i++)
        {
            var (id, price) = Items.Shop[i];
            var d = Items.ById[id];
            float rx = x + (i % 2) * (colW + 16), ry = y + (i / 2) * (rowH + gap);
            Gfx.Box(rx, ry, colW, rowH, CardBg, Pal.C("#c9b48f"), 2, 5);
            DrawIcon(id, rx + 10, ry + 9, 40);
            // The name and how many you have, kept clear of the buttons.
            float btnL = id is "bait" or "chum" ? colW - 226 : colW - 114, nameW = btnL - 62 - 10;
            string name = Gfx.Ellipsize(d.Name, FontKind.Ui700, 19, nameW);
            Gfx.Text(name, rx + 62, ry + 5, FontKind.Ui700, 19, Pal.PaperInk);
            string have = $"(have {Has(id)})";
            if (Gfx.Measure(name, FontKind.Ui700, 19) + 8 + Gfx.Measure(have, FontKind.Ui500, 14) <= nameW)
                Gfx.Text(have, rx + 70 + Gfx.Measure(name, FontKind.Ui700, 19), ry + 8, FontKind.Ui500, 14, Muted);
            var desc = Gfx.Wrap(Bind.Fix(d.Desc), FontKind.Note, 15, colW - 76);
            if (desc.Count > 2) desc = new() { desc[0], Gfx.Ellipsize(string.Join(" ", desc.Skip(1)), FontKind.Note, 15, colW - 76) };
            Lines(desc, rx + 62, ry + 43, 16, FontKind.Note, 15, Pal.PaperInk);
            if (Gfx.Hover(rx, ry, btnL, rowH)) shopTip = id;
            lastShopBottom = ry + rowH;
            DrawIcon("coin", rx + 62, ry + 25, 18);
            Gfx.Text(price.ToString(), rx + 84, ry + 24, FontKind.Ui700, 18, Pal.C("#9a6a1a"));
            bool can1 = state.coins >= price;
            if (id is "bait" or "chum")
            {
                bool can10 = state.coins >= price * 10;
                if (Button("Buy 10", rx + colW - 226, ry + 6, 104, 34, FontKind.Ui700, 17, can10 ? Pal.Buoy : Pal.C("#d9ccb0"), can10 ? White : Muted, 3, 2, 5, can10)) Buy(id, price, 10);
            }
            if (Button("Buy 1", rx + colW - 114, ry + 6, 104, 34, FontKind.Ui700, 17, can1 ? Pal.Buoy : Pal.C("#d9ccb0"), can1 ? White : Muted, 3, 2, 5, can1)) Buy(id, price, 1);
        }
    }

    // An item's name and everything it says about itself, in a box by the mouse (kept on the screen), drawn after the
    // rows so nothing covers it.
    void DrawItemTip(string id, float mx, float my)
    {
        var d = Items.ById[id];
        const float w = 380, fs = 17, lh = 22;
        var lines = Gfx.Wrap(Bind.Fix(d.Desc), FontKind.Note, fs, w - 28);
        if (Items.SellPrice(id) > 0 && d.Kind != "fish") lines.Add($"Pip pays {Items.SellPrice(id)} each.");
        float h = 46 + lines.Count * lh + 10;
        float x = Math.Clamp(mx + 18, 8, Gfx.LW - w - 8), y = Math.Clamp(my + 18, 8, Gfx.LH - h - 8);
        if (my + 18 + h > Gfx.LH - 8) y = Math.Max(8, my - h - 12);
        Gfx.Box(x, y, w, h, NavyStrong, Pal.Ink, 2, 6);
        Gfx.Text(Gfx.Ellipsize(d.Name, FontKind.Ui700, 19, w - 28), x + 14, y + 10, FontKind.Ui700, 19, Pal.Lantern);
        Lines(lines, x + 14, y + 40, lh, FontKind.Note, fs, Pal.Paper);
    }

    /* ---------- Aquarium ---------- */
    int tankPage;           // which page of your fish the aquarium shows (21 to a page)
    float lastTankBottom;   // the lowest thing the aquarium panel drew (the autotest checks it stays inside the panel)
    const int TankPerPage = 21;

    void DrawTank()
    {
        Backdrop();
        const float cw = 1000, ch = 690, pad = 24;
        float x = (Gfx.LW - cw) / 2, y = (Gfx.LH - ch) / 2;
        Gfx.Box(x, y, cw, ch, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Text("Aquarium", x + pad, y + pad, FontKind.Ui700, 36, Pal.PaperInk);
        if (SmallButton("Close", x + cw - pad - SmallW("Close"), y + pad - 2)) { ClosePanels(); return; }
        Gfx.Text("Up to four fish. Rare ones are worth showing off.", x + pad, y + pad + 46, FontKind.Note, 19, Muted);
        var tank = Tank(tankKey);
        float bottom = 0;

        // In the tank
        float sx = x + pad, sy = y + pad + 84;
        const float tankH = 300;
        Gfx.Rect(sx, sy, 380, tankH, Pal.C("#3a8db0"), 6);
        Gfx.Rect(sx, sy + tankH - 30, 380, 30, Pal.C("#e8cf96"), 6);
        for (int i = 0; i < 4; i++)
        {
            float cx = sx + 14 + (i % 2) * 180, cy = sy + 12 + (i / 2) * 142;
            Gfx.Box(cx, cy, 170, 130, Pal.C("#5fa9c9"), Pal.C("#2f7fa3"), 2, 6);
            if (i >= tank.Count) { Gfx.TextCenter("Empty", cx + 85, cy + 54, FontKind.Ui600, 18, Pal.C("#d8f0f8")); continue; }
            string id = tank[i];
            float bob = MathF.Sin(time * 2 + i) * 3;
            DrawIcon(id, cx + 55, cy + 6 + bob, 60);
            bool rare = Data.FishById[id].Rare;
            Gfx.TextCenter(Gfx.Ellipsize(Items.ById[id].Name, FontKind.Ui700, 16, 160), cx + 85, cy + 70, FontKind.Ui700, 16, rare ? Pal.Lantern : White);
            if (Button("Take out", cx + 30, cy + 92, 110, 32, FontKind.Ui700, 15, Pal.Sand, Pal.Ink, 3, 2, 5)) TankTake(i);
        }
        bottom = sy + tankH;

        // From your bag: seven to a row, three rows to a page, and the name of the one you point at on the line below.
        float bx = sx + 400, by = sy;
        const float card = 72, gap = 8;
        Gfx.Text("Your fish", bx, by, FontKind.Ui700, 22, Pal.PaperInk);
        var fish = BagItems().Where(id => Items.ById[id].Kind == "fish").ToList();
        int pages = Math.Max(1, (fish.Count + TankPerPage - 1) / TankPerPage);
        tankPage = Math.Clamp(tankPage, 0, pages - 1);
        if (pages > 1)
        {
            float px = bx + 7 * (card + gap) - gap;
            if (Button(">", px - 40, by - 6, 40, 32, FontKind.Ui700, 18, Pal.Sand, Pal.Ink, 2, 2, 5, tankPage < pages - 1)) tankPage++;
            if (Button("<", px - 88, by - 6, 40, 32, FontKind.Ui700, 18, Pal.Sand, Pal.Ink, 2, 2, 5, tankPage > 0)) tankPage--;
            string pg = $"{tankPage + 1} / {pages}";
            Gfx.Text(pg, px - 100 - Gfx.Measure(pg, FontKind.Ui600, 16), by + 2, FontKind.Ui600, 16, Muted);
        }
        if (fish.Count == 0) Gfx.Text("No fish in your bag.", bx, by + 40, FontKind.Note, 19, Muted);
        bool full = tank.Count >= 4;
        string pointed = null;
        var shown = fish.Skip(tankPage * TankPerPage).Take(TankPerPage).ToList();
        for (int i = 0; i < shown.Count; i++)
        {
            string id = shown[i];
            float fx = bx + (i % 7) * (card + gap), fy = by + 34 + (i / 7) * (card + gap);
            bool hov = Gfx.Hover(fx, fy, card, card);
            if (hov) pointed = id;
#if DEBUG
            Gfx.Seen["tankfish:" + id] = new Rectangle(fx, fy, card, card);
#endif
            bool rare = Data.FishById[id].Rare;
            Gfx.Box(fx, fy, card, card, hov && !full ? Pal.Lantern : CardBg, rare ? Pal.C("#d9a640") : Pal.C("#c9b48f"), 2, 5);
            DrawIcon(id, fx + 12, fy + 3, 48);
            Gfx.Text($"×{Has(id)}", fx + 7, fy + 52, FontKind.Ui700, 14, Pal.Ink);
            if (!full && Gfx.Click(fx, fy, card, card)) TankPut(id);
            bottom = Math.Max(bottom, fy + card);
        }
        string hint = pointed != null ? $"{Items.ById[pointed].Name} ×{Has(pointed)}" + (full ? ". The tank is full: take one out first." : ". Click to put it in.")
            : full ? "The tank is full. Take a fish out to swap." : "Click a fish to put it in.";
        Gfx.Text(Gfx.Ellipsize(hint, FontKind.Ui600, 17, 7 * (card + gap) - gap), bx, sy + tankH - 22, FontKind.Ui600, 17, pointed != null ? Pal.PaperInk : Muted);

        // Collections: a set of four on show (in any of your aquariums) gives a bonus. Four to a row.
        float cy2 = sy + tankH + 16;
        Gfx.Text("Collections: show all four fish of a set in your aquariums for a bonus", sx, cy2, FontKind.Ui700, 18, Pal.PaperInk);
        cy2 += 30;
        var onShow = Displayed();
        const float setH = 102;
        float setW = (cw - pad * 2 - 36) / 4;
        string tip = null;
        for (int i = 0; i < Data.AquaSets.Length; i++)
        {
            var set = Data.AquaSets[i];
            float qx = sx + (i % 4) * (setW + 12), qy = cy2 + (i / 4) * (setH + 8);
            bool done = set.Fish.All(onShow.Contains);
            Gfx.Box(qx, qy, setW, setH, done ? Pal.C("#e3f3d6") : CardBg, done ? Pal.C("#5fb04f") : Pal.C("#c9b48f"), 2, 5);
            Gfx.Text(Gfx.Ellipsize(set.Name, FontKind.Ui700, 16, setW - 20 - (done ? 26 : 0)), qx + 10, qy + 6, FontKind.Ui700, 16, done ? Pal.C("#2f6428") : Pal.PaperInk);
            if (done) DrawIcon("star", qx + setW - 30, qy + 4, 22);
            for (int k = 0; k < 4; k++)
            {
                string f = set.Fish[k];
                float ix = qx + 10 + k * 40;
                if (onShow.Contains(f)) DrawIcon(f, ix, qy + 28, 32);
                else if (state.commons.GetValueOrDefault(f) > 0) { DrawIcon(f, ix, qy + 28, 32); Gfx.Rect(ix, qy + 28, 32, 32, Pal.C("rgba(241,230,200,0.7)"), 4); }
                else Gfx.TextCenter("?", ix + 16, qy + 32, FontKind.Ui700, 20, Pal.C("#9a8a70"));
                if (Gfx.Hover(ix, qy + 28, 32, 32))
                    tip = state.commons.GetValueOrDefault(f) > 0 ? Items.ById[f].Name + (onShow.Contains(f) ? " (on show)" : "") : "Not caught yet";
            }
            var perk = Gfx.Wrap(set.Perk + (done ? " (on now)" : ""), FontKind.Ui500, 14, setW - 20);
            if (perk.Count > 2) perk = new List<string> { perk[0], Gfx.Ellipsize(perk[1] + " " + perk[2], FontKind.Ui500, 14, setW - 20) };
            Lines(perk, qx + 10, qy + 64, 16, FontKind.Ui500, 14, Muted);
            bottom = Math.Max(bottom, Math.Max(qy + setH, qy + 64 + perk.Count * 16));
        }
        lastTankBottom = bottom;
        // The name of the set fish you point at, beside the mouse, over everything else.
        if (tip != null)
        {
            var m = Gfx.Mouse;
            float tw = Gfx.Measure(tip, FontKind.Ui600, 15) + 20, tx = Math.Clamp(m.X + 14, 4, Gfx.LW - tw - 4), ty = m.Y + 22 > Gfx.LH - 34 ? m.Y - 34 : m.Y + 22;
            Gfx.Rect(tx, ty, tw, 28, NavyStrong, 4);
            Gfx.Text(tip, tx + 10, ty + 5, FontKind.Ui600, 15, Pal.Paper);
        }
        if (Gfx.PressedOutside(x, y, cw, ch)) ClosePanels();
    }
}
