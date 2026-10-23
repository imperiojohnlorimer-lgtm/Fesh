using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// The wardrobe panel and Pip's Dress stall tab (1.46, Wardrobe.cs). You try things on a draft of your look and boat and rod
// (nothing changes until Wear it; Cancel throws the draft away), and what you don't own yet is shown locked with how to get
// it. Tiles are big and need no hovering, so it works the same on a phone.
partial class Game
{
    string wardSlot = "shirt", wardFace = "down", dressSlot = "shirt", wardTip;
    Look wardLook;
    string wardHull, wardRod;
    internal float lastWardBottom, lastDressBottom;

    static Look CopyLook(Look s) => new()
    {
        name = s.name, skin = s.skin, hair = s.hair, hairColor = s.hairColor, hat = s.hat, shirt = s.shirt, pants = s.pants,
        shirtId = s.shirtId, pantsId = s.pantsId, hairId = s.hairId, hatId = s.hatId
    };

    void OpenWardrobe()
    {
        wardLook = CopyLook(state.look);
        wardHull = state.hullId; wardRod = state.rodId;
        wardSlot = "shirt"; wardFace = "down";
        Sfx.Play("ui");
        panel = "wardrobe"; mode = "panel";
        SetPrompt("");
    }

    // What a tile is: its key ("i:3" a creator choice, or a cosmetic's id), its name, colour (or hat style), and whether it's yours.
    sealed record WTile(string Key, string Name, string Hex, int Style, bool Owned, string Hint);

    static readonly string[] ShirtNames = { "Red", "Blue", "Green", "Gold", "Purple", "Navy", "White" };
    static readonly string[] PantsNames = { "Navy", "Brown", "Blue", "Grey", "Black" };
    static readonly string[] HairNames = { "Black", "Brown", "Ginger", "Blond", "Auburn", "Grey" };

    List<WTile> WardTiles(string slot)
    {
        var list = new List<WTile>();
        switch (slot)
        {
            case "shirt": for (int i = 0; i < LookData.Shirts.Length; i++) list.Add(new($"i:{i}", ShirtNames[i], LookData.Shirts[i], 0, true, "")); break;
            case "pants": for (int i = 0; i < LookData.Pants.Length; i++) list.Add(new($"i:{i}", PantsNames[i], LookData.Pants[i], 0, true, "")); break;
            case "hair": for (int i = 0; i < LookData.HairColors.Length; i++) list.Add(new($"i:{i}", HairNames[i], LookData.HairColors[i], 0, true, "")); break;
            case "hat": for (int i = 0; i < LookData.CreatorHats.Length; i++) list.Add(new($"i:{i}", LookData.CreatorHats[i], null, i, true, "")); break;
            case "hull": list.Add(new("i:0", "Teal", "#4e9691", 0, true, "")); break;
            case "rod": list.Add(new("i:0", "Original", "#8a6440", 0, true, "")); break;
        }
        foreach (var c in Wardrobe.InSlot(slot)) list.Add(new(c.Id, c.Name, c.Value, c.Style, Owns(c), Owns(c) ? "" : UnlockText(c)));
        return list;
    }

    bool WardSelected(string slot, string key) => slot switch
    {
        "shirt" => key.StartsWith("i:") ? wardLook.shirtId == null && wardLook.shirt == int.Parse(key[2..]) : wardLook.shirtId == key,
        "pants" => key.StartsWith("i:") ? wardLook.pantsId == null && wardLook.pants == int.Parse(key[2..]) : wardLook.pantsId == key,
        "hair" => key.StartsWith("i:") ? wardLook.hairId == null && wardLook.hairColor == int.Parse(key[2..]) : wardLook.hairId == key,
        "hat" => key.StartsWith("i:") ? wardLook.hatId == null && wardLook.hat == int.Parse(key[2..]) : wardLook.hatId == key,
        "hull" => key.StartsWith("i:") ? wardHull == null : wardHull == key,
        "rod" => key.StartsWith("i:") ? wardRod == null : wardRod == key,
        _ => false
    };

    void WardPick(string slot, WTile t)
    {
        bool legacy = t.Key.StartsWith("i:");
        int idx = legacy ? int.Parse(t.Key[2..]) : 0;
        switch (slot)
        {
            case "shirt": if (legacy) { wardLook.shirt = idx; wardLook.shirtId = null; } else wardLook.shirtId = t.Key; break;
            case "pants": if (legacy) { wardLook.pants = idx; wardLook.pantsId = null; } else wardLook.pantsId = t.Key; break;
            case "hair": if (legacy) { wardLook.hairColor = idx; wardLook.hairId = null; } else wardLook.hairId = t.Key; break;
            case "hat": if (legacy) { wardLook.hat = idx; wardLook.hatId = null; } else { wardLook.hat = t.Style; wardLook.hatId = t.Key; } break;
            case "hull": wardHull = legacy ? null : t.Key; break;
            case "rod": wardRod = legacy ? null : t.Key; break;
        }
        Sfx.Play("ui");
    }

    // A tile's picture: a swatch, or (for a hat) a small person wearing it, in the draft's own clothes.
    void DrawWardTile(float x, float y, WTile t, string slot, int slotNo)
    {
        if (slot == "hat")
        {
            var look = CopyLook(wardLook);
            look.hat = t.Style; look.hatId = t.Key.StartsWith("i:") ? null : t.Key;
            PixelScene(x + 24, y + 6, 20, 24, 3, p => LookData.DrawPerson(p, look, 10, 22, "down", 0, shadow: false, idle: false), 40 + slotNo);
            return;
        }
        var c = Pal.C(t.Hex ?? "#808080");
        if (slot == "rod")
        {
            Gfx.Line(x + 28, y + 54, x + 86, y + 18, 5, Pal.C("#3b2a1d"));
            Gfx.Line(x + 30, y + 53, x + 86, y + 18, 4, c);
        }
        else if (slot == "hull")
        {
            Gfx.Rect(x + 24, y + 38, 64, 12, Pal.C("#8a6038"), 4);
            Gfx.Rect(x + 24, y + 32, 64, 7, c, 3);
            Gfx.Rect(x + 30, y + 46, 52, 4, Pal.C("#5a3a20"), 2);
        }
        else
        {
            Gfx.Circle(x + 56, y + 32, 25, Pal.Ink);
            Gfx.Circle(x + 56, y + 32, 23, c);
        }
    }

    void DrawWardrobe()
    {
        wardTip = null;
        var (x, y, w, h) = PanelFrame("Wardrobe", "Try things on, then wear them. Dyes and hats from Pip's Dress stall; the rest are earned.", 1100, 660);
        if (PanelClose()) { wardLook = null; ClosePanels(); return; }
        if (wardLook == null) { ClosePanels(); return; }
        // Left: you, in the draft, and a turn button.
        float px = x + PanelPad, py = y + 104, pw = 300, ph = 380;
        var sky = Gfx.S(px, py, pw, ph);
        DrawRectangleGradientV((int)sky.X, (int)sky.Y, (int)sky.Width, (int)sky.Height, Pal.C("#3a8db0"), Pal.C("#1d4f78"));
        Gfx.Rect(px, py + ph - 60, pw, 60, Pal.C("#e8cf96"));
        PixelScene(px + pw / 2 - 20 * 7, py + ph - 60 - 24 * 14 + 14, 20, 24, 14, p => LookData.DrawPerson(p, wardLook, 10, 22, wardFace, 0, shadow: false, idle: false), 39);
        if (Button("Turn", px + pw / 2 - 50, py + ph + 14, 100, 40, FontKind.Ui700, 18, Pal.Sand, Pal.Ink, 3, 2, 5))
            wardFace = wardFace switch { "down" => "right", "right" => "up", "up" => "left", _ => "down" };
        // Slot tabs.
        float tx = px + pw + 30, ty = py - 2;
        foreach (var slot in Wardrobe.Slots)
        {
            string label = Wardrobe.SlotName(slot);
            float bw = SmallW(label);
            if (Button(label, tx, ty, bw, 44, FontKind.Ui700, 19, wardSlot == slot ? Pal.Lantern : Pal.Sand, Pal.Ink, 4)) wardSlot = slot;
#if DEBUG
            Gfx.Seen["ward:slot:" + slot] = new Rectangle(tx, ty, bw, 44);
#endif
            tx += bw + 8;
        }
        // The tiles of this slot, six to a row.
        var tiles = WardTiles(wardSlot);
        float gx = px + pw + 30, gy = ty + 62, tw = 108, th = 112, gap = 10;
        int cols = (int)((x + w - PanelPad - gx + gap) / (tw + gap));
        for (int i = 0; i < tiles.Count; i++)
        {
            var t = tiles[i];
            float cx = gx + i % cols * (tw + gap), cy = gy + i / cols * (th + gap);
            bool sel = WardSelected(wardSlot, t.Key);
            Gfx.Box(cx, cy, tw, th, sel ? Pal.C("#fff3cf") : CardBg, sel ? Pal.C("#d9a640") : Pal.C("#c9b48f"), sel ? 3 : 2, 6);
            DrawWardTile(cx, cy, t, wardSlot, i);
            Gfx.TextCenter(Gfx.Ellipsize(t.Name, FontKind.Ui600, 15, tw - 10), cx + tw / 2, cy + th - 26, FontKind.Ui600, 15, t.Owned ? Pal.PaperInk : Muted);
            if (!t.Owned)
            {
                Gfx.Rect(cx + 2, cy + 2, tw - 4, th - 4, Pal.C("rgba(236,228,210,0.55)"), 5);
                DrawPadlock(cx + tw - 22, cy + 10, 1.3f, Pal.Ink);
            }
            if (Gfx.Click(cx, cy, tw, th))
            {
                if (t.Owned) WardPick(wardSlot, t);
                else { Sfx.Play("nope"); Toast(t.Hint != "" ? t.Hint : "Not yours yet.", 3.5f); }
            }
            if (Gfx.Hover(cx, cy, tw, th) && !t.Owned) wardTip = t.Name + "\n" + t.Hint;
#if DEBUG
            Gfx.Seen["ward:tile:" + t.Key] = new Rectangle(cx, cy, tw, th);
#endif
            lastWardBottom = MathF.Max(lastWardBottom, cy + th);
        }
        // What the boat and the rod are wearing now.
        float by = y + h - PanelPad - 56;
        Gfx.Text($"Boat trim: {Wardrobe.Find(wardHull)?.Name ?? "Teal"} · Rod: {Wardrobe.Find(wardRod)?.Name ?? "Original"}", px + pw + 30, by - 36, FontKind.Ui500, 17, Muted);
        float bx = x + w - PanelPad - BigW("Wear it");
        if (BigButton("Wear it", bx, by, true)) { WearDraft(); return; }
        bx -= BigW("Cancel") + 13;
        if (BigButton("Cancel", bx, by, false)) { wardLook = null; ClosePanels(); return; }
#if DEBUG
        Gfx.Seen["ward:wear"] = new Rectangle(x + w - PanelPad - BigW("Wear it"), by, BigW("Wear it"), 56);
        Gfx.Seen["ward:cancel"] = new Rectangle(bx, by, BigW("Cancel"), 56);
#endif
        lastWardBottom = MathF.Max(lastWardBottom, by + 56);
        if (wardTip != null) DrawHoverNote(wardTip, Gfx.Mouse.X, Gfx.Mouse.Y);
    }

    void WearDraft()
    {
        state.look = CopyLook(wardLook);
        state.hullId = wardHull; state.rodId = wardRod;
        FixWardrobe();
        wardLook = null;
        Sfx.Play("craft");
        Save();
        ClosePanels();
        Toast($"Looking good, {state.look.name}.");
    }

    // A dark chip by the mouse: a title and a line.
    void DrawHoverNote(string text, float mx, float my)
    {
        var parts = text.Split('\n');
        const float w = 300, fs = 16, lh = 21;
        var lines = Gfx.Wrap(parts.Length > 1 ? parts[1] : "", FontKind.Note, fs, w - 28);
        float h = 44 + lines.Count * lh + 6;
        float x = Math.Clamp(mx + 18, 8, Gfx.LW - w - 8), y = Math.Clamp(my + 18, 8, Gfx.LH - h - 8);
        Gfx.Box(x, y, w, h, NavyStrong, Pal.Ink, 2, 6);
        Gfx.Text(Gfx.Ellipsize(parts[0], FontKind.Ui700, 18, w - 28), x + 14, y + 9, FontKind.Ui700, 18, Pal.Lantern);
        Lines(lines, x + 14, y + 38, lh, FontKind.Note, fs, Pal.Paper);
    }

    /* ---------- Pip's Dress stall ---------- */
    void DrawDressTab(float x, float y, float w, float bottom)
    {
        Gfx.Text("Dyes, cloth and hats for you, trim for your boat and paint for your rod. Buy once, wear whenever.", x, y + 2, FontKind.Note, 19, Muted);
        // Slot chips, then what Pip has in that slot.
        float cx = x, cy = y + 40;
        foreach (var slot in Wardrobe.Slots)
        {
            string label = Wardrobe.SlotName(slot);
            float bw = SmallW(label);
            if (Button(label, cx, cy, bw, 40, FontKind.Ui700, 18, dressSlot == slot ? Pal.Lantern : Pal.Sand, Pal.Ink, 3)) dressSlot = slot;
#if DEBUG
            Gfx.Seen["dress:slot:" + slot] = new Rectangle(cx, cy, bw, 40);
#endif
            cx += bw + 8;
        }
        string wl = "Open the wardrobe";
        if (SmallButton(wl, x + w - SmallW(wl), cy)) { OpenWardrobe(); return; }
        float ry = cy + 54;
        foreach (var c in Wardrobe.InSlot(dressSlot).Where(c => c.InShop))
        {
            Gfx.Box(x, ry, w, 70, CardBg, Pal.C("#c9b48f"), 2, 5);
            var look = CopyLook(state.look);
            if (c.Slot == "hat") { look.hat = c.Style; look.hatId = c.Id; PixelScene(x + 14, ry + 5, 20, 24, 2, p => LookData.DrawPerson(p, look, 10, 22, "down", 0, shadow: false, idle: false), 70 + Array.IndexOf(Wardrobe.Catalog, c)); }
            else { Gfx.Circle(x + 38, ry + 35, 20, Pal.Ink); Gfx.Circle(x + 38, ry + 35, 18, Pal.C(c.Value)); }
            Gfx.Text(c.Name, x + 82, ry + 12, FontKind.Ui700, 21, Pal.PaperInk);
            Gfx.Text(c.Slot switch { "hull" => "Paints the trim of your banca", "rod" => "Paints your rod", "hat" => "A hat for your fisher", _ => "For your fisher" }, x + 82, ry + 40, FontKind.Ui500, 16, Muted);
            float bx = x + w - 190;
            if (Owns(c)) Gfx.Text("Owned", bx + 50, ry + 22, FontKind.Ui700, 21, Pal.C("#3f7d35"));
            else
            {
                bool can = state.coins >= c.Price;
                if (Button($"Buy: {c.Price}", bx, ry + 12, 170, 46, FontKind.Ui700, 20, can ? Pal.Buoy : Pal.Sand, can ? White : Pal.Ink, 4)) BuyCosmetic(c);
#if DEBUG
                Gfx.Seen["dress:buy:" + c.Id] = new Rectangle(bx, ry + 12, 170, 46);
#endif
            }
            ry += 78;
            lastDressBottom = MathF.Max(lastDressBottom, ry - 8);
        }
    }
}
