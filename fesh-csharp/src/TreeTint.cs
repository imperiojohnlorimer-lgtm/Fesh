using Raylib_cs;

namespace Fesh;

// Leaf colour for the model trees (1.41, Light: Soft or Full): every tree gets its own narrow shade of green from its tile's
// hash and its island, so a stand of oaks is not a row of the same green. It tints the foliage only (BlitTree's leaf args),
// never bark or outline, and with light off a tree is drawn exactly as before.
partial class Game
{
    static readonly Color AmihanLeaf = Pal.C("#b8a468"), HabagatLeaf = Pal.C("#376b46");
    static readonly Color[] OakShades = Tones("#a8b44a", "#1f6a3a", "#7a8a3a"), PalmShades = Tones("#6fbf4f", "#2a7a4a", "#8fb84a"), FirShades = Tones("#2a5f4a", "#4a7a3a", "#3a6a5a");

    // The tint and how strongly it is mixed over the foliage (null and 0 for none).
    internal (Color? colour, float amount) LeafTint(int tx, int ty, char kind)
    {
        if (LightMode == 0) return (null, 0);
        var shades = kind == 't' ? OakShades : kind == 'f' ? FirShades : PalmShades;
        double h = Pix.Hash(tx, ty, 1100), a = Pix.Hash(tx, ty, 1101);
        var colour = shades[(int)(h * shades.Length) % shades.Length];
        float amount = (kind == 'h' ? 0.1f : 0.12f) * (float)a;
        // The islands lean: Mirewood's trees are deeper, the atoll's and Marilaya's warmer and brighter.
        byte bi = BiomeAt(tx, ty);
        if (bi == 3) { colour = Pix.Mix(colour, Pal.C("#0c2a1c"), 0.5f); amount += 0.03f; }
        else if (bi is 4 or 7) { colour = Pix.Mix(colour, Pal.C("#c8c060"), 0.4f); amount += 0.02f; }
        // The monsoon's own tint on top (halved on Soft): a touch of dry gold in the amihan, deeper green in the habagat.
        float mix = SeasonMix(), half = LightMode == 1 ? 0.5f : 1;
        var seasonColour = Pix.Mix(AmihanLeaf, HabagatLeaf, mix);
        float seasonAmount = (0.055f + 0.015f * (float)a) * half;
        colour = Pix.Mix(colour, seasonColour, seasonAmount / (amount * half + seasonAmount + 0.001f));
        amount = amount * half + seasonAmount;
        return (colour, amount);
    }
}

partial class Game
{
    // The sprite a tree is drawn with. Light off: the band's own sprite, exactly as before. Light on: now and then (by tile hash
    // and island) the same band's twin-trunk oak or leaning palm instead (tools/blender/trees2.py), never the small one.
    // The variants keep the foot anchor and the band's height, so collision, fading and shadows treat them alike.
    internal static float VariantChance(char kind, byte biome) => kind == 't' ? (biome is 0 or 5 ? 0.18f : 0.1f) : kind == 'h' ? (biome is 4 or 5 or 6 ? 0.22f : 0.06f) : 0;

    TreeSprite TreeSpriteFor(int tx, int ty, char kind, float rel)
    {
        var set = kind == 't' ? oakSprites : kind == 'f' ? firSprites : palmSprites;
        int band = TreeBand(rel, tx, ty);
        if (LightMode > 0 && band >= 1 && kind is 't' or 'h' && Pix.Hash(tx, ty, 1110) < VariantChance(kind, BiomeAt(tx, ty)))
            return (kind == 't' ? oakTwinSprites : palmLeanSprites)[band - 1];
        return set[band];
    }
}
