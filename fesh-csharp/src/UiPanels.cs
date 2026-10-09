using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

partial class Game
{
    string bagSel;

    /* ---------- Bag ---------- */
    List<string> BagItems() => state.inv.Where(kv => kv.Value > 0).Select(kv => kv.Key)
        .OrderBy(id => Array.IndexOf(Items.KindOrder, Items.ById[id].Kind)).ThenBy(id => Items.All.FindIndex(d => d.Id == id)).ToList();

    void DrawBag()
    {
        Backdrop();
        const float bw = 1120, bh = 600, pad = 24, slot = 66, gapS = 8;
        const int cols = 10;
        float bx = (Gfx.LW - bw) / 2, by = (Gfx.LH - bh) / 2;
        Gfx.Box(bx, by, bw, bh, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Text("Bag", bx + pad, by + pad, FontKind.Ui700, 40, Pal.PaperInk);
        if (SmallButton("Close", bx + bw - pad - SmallW("Close"), by + pad)) ClosePanels();

        // Food and the rod in use.
        float fx = bx + 140, fy = by + pad + 6;
        DrawIcon("grilled_fish", fx, fy, 28);
        Gfx.Rect(fx + 36, fy + 8, 160, 14, Pal.C("rgba(0,0,0,0.15)"), 3);
        float f = state.food / 100f;
        if (f > 0) Gfx.Rect(fx + 36, fy + 8, Math.Max(6, 160 * f), 14, f > 0.5f ? Pal.C("#5fb04f") : f > 0.25f ? Pal.Lantern : Pal.Buoy, 3);
        Gfx.Text($"Food {state.food:0}/100", fx + 206, fy + 5, FontKind.Ui600, 18, Pal.PaperInk);
        string rod = BestRod();
        DrawIcon(rod, fx + 360, fy, 28);
        Gfx.Text($"Fishing with: {Items.ById[rod].Name}", fx + 396, fy + 5, FontKind.Ui600, 18, Pal.PaperInk);
        DrawCoins(fx + 640, fy + 1);
        if (state.req != null)
            Gfx.Text($"Tomas wants {Items.Amount(state.req.item, state.req.count)} (you have {Has(state.req.item)}) for {RewardText(state.req)}.",
                bx + pad, by + bh - pad - 20, FontKind.Ui600, 16, CanHandIn ? Pal.C("#3f7d35") : Muted);

        var items = BagItems();
        if (bagSel == null || !items.Contains(bagSel)) bagSel = items.FirstOrDefault();
        float gx = bx + pad, gy = by + pad + 64;
        if (items.Count == 0)
            Gfx.Text("Nothing yet. Fish, chop, mine and forage to fill it.", gx, gy + 10, FontKind.Note, 20, Muted);
        for (int i = 0; i < items.Count && i < cols * 6; i++)
        {
            string id = items[i];
            float sx = gx + (i % cols) * (slot + gapS), sy = gy + (i / cols) * (slot + gapS);
            bool sel = id == bagSel;
            Gfx.Box(sx, sy, slot, slot, sel ? Pal.Lantern : CardBg, sel ? Pal.Ink : Pal.C("#c9b48f"), 2, 5);
            DrawIcon(id, sx + 9, sy + 7, 48);
            string n = state.inv[id].ToString();
            Gfx.Text(n, sx + slot - 8 - Gfx.Measure(n, FontKind.Ui700, 16), sy + slot - 20, FontKind.Ui700, 16, Pal.Ink);
            if (Gfx.Click(sx, sy, slot, slot)) { bagSel = id; Sfx.Play("blip"); }
        }

        // Details for the selected item.
        float dx = bx + pad + cols * (slot + gapS) + 12, dy = gy, dw = bx + bw - pad - dx;
        Gfx.Box(dx, dy, dw, bh - (dy - by) - pad, CardBg, Pal.C("#c9b48f"), 2, 6);
        if (bagSel == null) { if (Gfx.PressedOutside(bx, by, bw, bh)) ClosePanels(); return; }
        var d = Items.ById[bagSel];
        float cx = dx + 16, cy = dy + 16;
        DrawIcon(bagSel, cx, cy, 72);
        // A long name goes onto two lines, with the line below it moved down to match.
        var (title, ts) = BagTitle(d.Name, dw - 118);
        float ty = title.Count == 1 ? cy + 6 : cy - 3;
        foreach (var line in title) { Gfx.Text(line, cx + 86, ty, FontKind.Ui700, ts, Pal.PaperInk); ty += ts + 3; }
        string kind = d.Kind switch
        {
            "tool" => "Tool", "rod" => "Fishing rod", "material" => "Material", "food" => "Food", "tackle" => "Tackle", "accessory" => "Worn automatically",
            "gear" => "Fishing gear", "weapon" => "Weapon", "armor" => "Armour", _ => "Fish"
        };
        Gfx.Text($"{kind}  ·  You have {Has(bagSel)}", cx + 86, title.Count == 1 ? cy + 38 : ty + 5, FontKind.Ui500, 16, Muted);
        cy += 90;
        var desc = Gfx.Wrap(Bind.Fix(d.Desc), FontKind.Note, 18, dw - 32);
        Lines(desc, cx, cy, 25, FontKind.Note, 18, Pal.PaperInk);
        cy += desc.Count * 25 + 10;
        // Fish: your record and how many big ones you're carrying. Bait: what it's good for, and whether it's in use.
        if (d.Kind == "fish" && state.records.TryGetValue(bagSel, out float best))
        {
            int big = BigCount(bagSel);
            Gfx.Text($"Your biggest: {Kg(best)}" + (big > 0 ? $"  ·  {big} big (Pip pays more)" : ""), cx, cy, FontKind.Ui600, 16, Pal.C("#3f7d35"));
            cy += 26;
        }
        if (Items.Baits.TryGetValue(bagSel, out var bait))
        {
            Lines(Gfx.Wrap("As bait: " + bait.Summary, FontKind.Ui500, 16, dw - 32), cx, cy, 21, FontKind.Ui500, 16, Muted);
            cy += 46;
            Gfx.Text(NextBait() == bagSel ? "Your next cast uses this." : Bind.Fix("Pick it in the tackle box (<tackle>)."), cx, cy, FontKind.Ui600, 16, NextBait() == bagSel ? Pal.C("#3f7d35") : Muted);
            cy += 30;
        }
        if (Items.Tackle.TryGetValue(bagSel, out var tk))
        {
            bool on = GearId(tk.Slot) == bagSel;
            Gfx.Text(on ? "Equipped in your tackle box." : Bind.Fix("Equip it in the tackle box (<tackle>)."), cx, cy, FontKind.Ui600, 16, on ? Pal.C("#3f7d35") : Muted);
            cy += 30;
        }
        if (d.Kind == "rod")
        {
            var rs = Items.Rod[bagSel];
            Lines(Gfx.Wrap(rs.Summary, FontKind.Ui500, 16, dw - 32), cx, cy, 21, FontKind.Ui500, 16, Muted);
            cy += 50;
            Gfx.Text(bagSel == rod ? "This is the rod you fish with." : "You have a better rod.", cx, cy, FontKind.Ui600, 17, bagSel == rod ? Pal.C("#3f7d35") : Muted);
        }
        if (d.Food > 0)
        {
            Gfx.Text($"Fills {d.Food} food" + (d.Kind == "fish" ? " raw. Cook it for more." : "."), cx, cy, FontKind.Ui600, 17, Pal.C("#3f7d35"));
            cy += 34;
            if (BigButton(d.Kind == "fish" ? "Eat it raw" : "Eat", cx, cy, true)) Eat(bagSel);
        }
        if (Gfx.PressedOutside(bx, by, bw, bh)) ClosePanels();
    }

    // The selected item's name on the bag card, in the width beside its picture: one line at 24 if it fits, else a size
    // smaller, else two lines (breaking before the English name in brackets, if there is one), as big as fits.
    static (List<string> lines, float size) BagTitle(string name, float maxW)
    {
        bool Fits(IEnumerable<string> lines, float size) => lines.All(l => Gfx.Measure(l, FontKind.Ui700, size) <= maxW);
        foreach (float size in new[] { 24f, 21f })
            if (Fits(new[] { name }, size)) return (new() { name }, size);
        int bracket = name.IndexOf(" (");
        foreach (float size in new[] { 21f, 19f, 17f })
        {
            var lines = bracket > 0 ? new List<string> { name[..bracket], name[(bracket + 1)..] } : Gfx.Wrap(name, FontKind.Ui700, size, maxW);
            if (lines.Count <= 2 && Fits(lines, size)) return (lines, size);
        }
        var two = Gfx.Wrap(name, FontKind.Ui700, 17, maxW);
        return (new() { Gfx.Ellipsize(two[0], FontKind.Ui700, 17, maxW), Gfx.Ellipsize(string.Join(" ", two.Skip(1)), FontKind.Ui700, 17, maxW) }, 17);
    }
    public const float BagNameW = 202;   // the bag card is 320 wide: the name starts 102 in and keeps 16 clear of the edge

    /* ---------- Crafting ---------- */
    string IngredientName(string id) => id == "fish" ? "raw fish" : id == Items.TinapaFishId ? "fish for tinapa" : id == Items.SeaweedId ? "seaweed"
        : Items.ById[id].Name.ToLowerInvariant();

    // The bubo only shows at the workbench once Tala has shown you how it's woven.
    bool RecipeKnown(Recipe r) => r.Out switch { "bubo" => state.Hinted("bubo"), "plate_armor" => GuardianBeaten, _ => true };

    // The workbench makes too much for one list, so it's split into tabs.
    static readonly string[] CraftTabs = { "Tools", "Rods", "Tackle", "Gear", "Combat", "Bait" };
    string craftTab = "Tools";
    int craftPage;
    const int CraftPerPage = 6;

    void DrawCraft()
    {
        Backdrop();
        bool tabs = craftStation == "workbench";
        var all = Items.Recipes.Where(r => Items.StationMakes(craftStation, r) && RecipeKnown(r)).ToList();
        var inTab = tabs ? all.Where(r => Items.Category(r) == craftTab).ToList() : all;
        int pages = Math.Max(1, (inTab.Count + CraftPerPage - 1) / CraftPerPage);
        craftPage = Math.Clamp(craftPage, 0, pages - 1);
        var recipes = inTab.Skip(craftPage * CraftPerPage).Take(CraftPerPage).ToList();
        const float cw = 900, pad = 24, rowH = 66, tabH = 44;
        float top = pad + 48 + 34 + (tabs ? tabH + 12 : 0);
        float h = top + Math.Min(inTab.Count, CraftPerPage) * (rowH + 8) + pad + (pages > 1 ? 48 : 0);
        float x = (Gfx.LW - cw) / 2, y = Math.Max(10, (Gfx.LH - h) / 2);
        Gfx.Box(x, y, cw, h, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Text(Items.StationName[craftStation], x + pad, y + pad, FontKind.Ui700, 36, Pal.PaperInk);
        if (SmallButton("Close", x + cw - pad - SmallW("Close"), y + pad - 2)) ClosePanels();
        string hint = craftStation switch
        {
            "workbench" => craftTab switch
            {
                "Rods" => "Each rod upgrades the one before, so you always keep your best.",
                "Tackle" => "Reels, lines, hooks, bobbers, sinkers and lures. Equip them in the tackle box (<tackle>).",
                "Gear" => "Accessories you wear automatically once they're made.",
                "Combat" => "Swords hit cave monsters harder. Armour softens their blows.",
                "Bait" => "Pick which bait to use in the tackle box (<tackle>). Throw chum at a spot with <alt>.",
                _ => "Stronger pickaxes mine rarer ore deeper in Frostfang Caverns."
            },
            "furnace" => "Two ore and a piece of wood for fuel make one bar.",
            "smoker" => $"Smoked fish sells for double. Tinapa: galunggong, tamban, sapsap, bangus or tilapia, with salt.",
            _ => $"Cook any raw fish from your bag. You have {FishCount()} raw fish."
        };
        Gfx.Text(Bind.Fix(hint), x + pad, y + pad + 48, FontKind.Note, 18, Muted);
        float ry = y + pad + 48 + 34;
        if (tabs)
        {
            float tx = x + pad;
            foreach (var tab in CraftTabs)
            {
                // A dot marks a tab with something you can make right now.
                bool ready = all.Any(r => Items.Category(r) == tab && CanCraft(r));
                float w = Gfx.Measure(tab, FontKind.Ui700, 20) + 40 + (ready ? 14 : 0);
                if (Button(tab, tx, ry, w, tabH, FontKind.Ui700, 20, craftTab == tab ? Pal.Lantern : Pal.Sand, Pal.Ink, 3, 3, 6) && craftTab != tab)
                {
                    craftTab = tab;
                    craftPage = 0;
                    Sfx.Play("blip");
                }
                if (ready) Gfx.Circle(tx + w - 13, ry + 12, 5, Pal.Buoy);
                tx += w + 10;
            }
            ry += tabH + 12;
        }
        foreach (var r in recipes)
        {
            var d = Items.ById[r.Out];
            bool can = CanCraft(r);
            Gfx.Box(x + pad, ry, cw - pad * 2, rowH, CardBg, Pal.C("#c9b48f"), 2, 6);
            DrawIcon(r.Out, x + pad + 11, ry + 9, 48);
            float tx = x + pad + 72;
            string name = r.Count > 1 ? $"{d.Name} ×{r.Count}" : d.Name;
            Gfx.Text(name, tx, ry + 8, FontKind.Ui700, 21, Pal.PaperInk);
            float nx = tx + Gfx.Measure(name, FontKind.Ui700, 21) + 12;
            if (Has(r.Out) > 0) Gfx.Text($"(have {Has(r.Out)})", nx, ry + 11, FontKind.Ui500, 16, Muted);
            float ix = tx;
            foreach (var (id, n) in r.Needs)
            {
                string part = $"{n} {IngredientName(id)} ({Has(id)})";
                bool enough = Has(id) >= n;
                Gfx.Text(part, ix, ry + 38, FontKind.Ui600, 16, enough ? Pal.C("#3f7d35") : Rust);
                ix += Gfx.Measure(part, FontKind.Ui600, 16) + 20;
            }
            string label = can ? "Make" : "Need more";
            float bw = 150;
            if (Button(label, x + cw - pad - bw - 12, ry + 10, bw, 46, FontKind.Ui700, 20, can ? Pal.Buoy : Pal.C("#d9ccb0"), can ? White : Muted, 4, 3, 6, can))
                Craft(r);
            ry += rowH + 8;
        }
        if (pages > 1)
        {
            ry = y + top + CraftPerPage * (rowH + 8);
            Gfx.Text($"Page {craftPage + 1} of {pages}", x + pad, ry + 10, FontKind.Ui600, 17, Muted);
            if (Button("Previous", x + pad + 130, ry, 120, 38, FontKind.Ui700, 17, Pal.Sand, Pal.Ink, 3, 2, 5, craftPage > 0)) craftPage--;
            if (Button("Next", x + pad + 260, ry, 100, 38, FontKind.Ui700, 17, Pal.Sand, Pal.Ink, 3, 2, 5, craftPage < pages - 1)) craftPage++;
        }
        if (Gfx.PressedOutside(x, y, cw, h)) ClosePanels();
    }

    /* ---------- Character creator ---------- */
    string creatorFor;   // "new", "continue" or "edit"
    Look editLook;
    string previewFace = "down";
    Texture2D previewTex;
    readonly Pix previewPix = new(16, 18);

    void OpenCreator(string kind)
    {
        creatorFor = kind;
        var src = kind == "new" ? new Look() : state.look;
        editLook = new Look { name = src.name, skin = src.skin, hair = src.hair, hairColor = src.hairColor, hat = src.hat, shirt = src.shirt, pants = src.pants };
        if (kind == "new") editLook.name = "";
        previewFace = "down";
        mode = "create";
        SetPrompt("");
    }

    void CreatorKeys()
    {
        foreach (int c in Inp.Typed())
            if (c >= 32 && c < 127 && editLook.name.Length < 14) editLook.name += (char)c;
        if ((Inp.Pressed(KeyboardKey.Backspace) || IsKeyPressedRepeat(KeyboardKey.Backspace)) && editLook.name.Length > 0)
            editLook.name = editLook.name[..^1];
        if (Inp.Pressed(KeyboardKey.Enter)) FinishCreator();
        else if (BackPressed()) CancelCreator();
    }

    void CancelCreator()
    {
        mode = creatorFor == "edit" ? "play" : "title";
    }

    void FinishCreator()
    {
        editLook.name = editLook.name.Trim();
        if (editLook.name.Length == 0) editLook.name = "Fisher";
        Sfx.Play("craft");
        switch (creatorFor)
        {
            case "new": StartGame(true, editLook); break;
            case "continue":
                state.look = editLook;
                state.created = true;
                StartGame(false);
                break;
            default:
                state.look = editLook;
                state.created = true;
                mode = "play";
                Save();
                Toast($"Looking good, {editLook.name}.");
                break;
        }
    }

    // A row of colour circles; returns the clicked index or -1.
    static int Swatches(string[] colors, int sel, float x, float y)
    {
        int clicked = -1;
        for (int i = 0; i < colors.Length; i++)
        {
            float cx = x + 20 + i * 46, cy = y + 20;
            if (i == sel) Gfx.Circle(cx, cy, 20, Pal.Ink);
            Gfx.Circle(cx, cy, i == sel ? 16 : 17, Pal.C(colors[i]));
            if (i != sel) DrawCircleLinesV(Gfx.P(cx, cy), 17 * Gfx.Z, Pal.C("rgba(16,36,58,0.35)"));
            if (Gfx.Click(cx - 20, cy - 20, 40, 40)) clicked = i;
        }
        return clicked;
    }

    static int Choices(string[] options, int sel, float x, float y)
    {
        int clicked = -1;
        foreach (var (opt, i) in options.Select((o, i) => (o, i)))
        {
            float w = Gfx.Measure(opt, FontKind.Ui700, 18) + 30;
            if (Button(opt, x, y, w, 40, FontKind.Ui700, 18, i == sel ? Pal.Lantern : Pal.Sand, Pal.Ink, 3, 2, 5)) clicked = i;
            x += w + 8;
        }
        return clicked;
    }

    void DrawCreator()
    {
        Backdrop();
        const float cw = 1000, ch = 620, pad = 28;
        float x = (Gfx.LW - cw) / 2, y = (Gfx.LH - ch) / 2;
        Gfx.Box(x, y, cw, ch, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Text(creatorFor == "edit" ? "Change your look" : "Make your fisher", x + pad, y + pad, FontKind.Ui700, 40, Pal.PaperInk);

        // Preview, walking on the spot.
        float px = x + pad, py = y + pad + 66, pw = 300, ph = 400;
        var sky = Gfx.S(px, py, pw, ph);
        DrawRectangleGradientV((int)sky.X, (int)sky.Y, (int)sky.Width, (int)sky.Height, Pal.C("#3a8db0"), Pal.C("#1d4f78"));
        Gfx.Rect(px, py + ph - 70, pw, 70, Pal.C("#e8cf96"));
        Array.Fill(previewPix.Buf, default);
        LookData.DrawPerson(previewPix, editLook, 8, 16, previewFace, 1 + (int)(time * 8) % 4, shadow: false);
        if (previewTex.Id == 0) previewTex = Gfx.ToTexture(previewPix.Buf, 16, 18, TextureFilter.Point);
        UpdateTexture(previewTex, previewPix.Buf);
        DrawTexturePro(previewTex, new Rectangle(0, 0, 16, 18), Gfx.S(px + pw / 2 - 16 * 7, py + ph - 70 - 17 * 14 + 14, 16 * 14, 18 * 14), Vector2.Zero, 0, Color.White);
        if (Button("Turn", px + pw / 2 - 50, py + ph + 14, 100, 40, FontKind.Ui700, 18, Pal.Sand, Pal.Ink, 3, 2, 5))
            previewFace = previewFace switch { "down" => "right", "right" => "up", "up" => "left", _ => "down" };

        // Options
        float ox = x + pad + pw + 36, oy = y + pad + 66, lw = 140;
        Gfx.Text("Name", ox, oy + 10, FontKind.Ui700, 20, Pal.PaperInk);
        Gfx.Box(ox + lw, oy, 420, 46, White, Pal.Ink, 2, 5);
        string shown = editLook.name.Length > 0 ? editLook.name : "Type a name";
        Gfx.Text(shown, ox + lw + 14, oy + 11, FontKind.Ui600, 22, editLook.name.Length > 0 ? Pal.PaperInk : Pal.C("#9a8a70"));
        if ((int)(time * 2) % 2 == 0)
            Gfx.Rect(ox + lw + 16 + (editLook.name.Length > 0 ? Gfx.Measure(editLook.name, FontKind.Ui600, 22) : 0), oy + 10, 2, 26, Pal.Ink);
        oy += 66;
        int r;
        Gfx.Text("Skin", ox, oy + 10, FontKind.Ui700, 20, Pal.PaperInk);
        if ((r = Swatches(LookData.Skins, editLook.skin, ox + lw, oy)) >= 0) editLook.skin = r;
        oy += 56;
        Gfx.Text("Hair", ox, oy + 10, FontKind.Ui700, 20, Pal.PaperInk);
        if ((r = Choices(LookData.Hairs, editLook.hair, ox + lw, oy)) >= 0) editLook.hair = r;
        oy += 54;
        Gfx.Text("Hair colour", ox, oy + 10, FontKind.Ui700, 20, Pal.PaperInk);
        if ((r = Swatches(LookData.HairColors, editLook.hairColor, ox + lw, oy)) >= 0) editLook.hairColor = r;
        oy += 56;
        Gfx.Text("Hat", ox, oy + 10, FontKind.Ui700, 20, Pal.PaperInk);
        if ((r = Choices(LookData.Hats, editLook.hat, ox + lw, oy)) >= 0) editLook.hat = r;
        oy += 54;
        Gfx.Text("Shirt", ox, oy + 10, FontKind.Ui700, 20, Pal.PaperInk);
        if ((r = Swatches(LookData.Shirts, editLook.shirt, ox + lw, oy)) >= 0) editLook.shirt = r;
        oy += 56;
        Gfx.Text("Trousers", ox, oy + 10, FontKind.Ui700, 20, Pal.PaperInk);
        if ((r = Swatches(LookData.Pants, editLook.pants, ox + lw, oy)) >= 0) editLook.pants = r;

        float byy = y + ch - pad - 56;
        string done = creatorFor switch { "new" => "Start", "continue" => "Continue", _ => "Done" };
        float bx = x + cw - pad - BigW(done);
        if (BigButton(done, bx, byy, true)) FinishCreator();
        bx -= BigW(creatorFor == "edit" ? "Cancel" : "Back") + 13;
        if (BigButton(creatorFor == "edit" ? "Cancel" : "Back", bx, byy, false)) CancelCreator();
        bx -= BigW("Surprise me") + 13;
        if (BigButton("Surprise me", bx, byy, false))
        {
            var rnd = LookData.Random(rng);
            rnd.name = editLook.name;
            editLook = rnd;
            Sfx.Play("blip");
        }
    }
}
