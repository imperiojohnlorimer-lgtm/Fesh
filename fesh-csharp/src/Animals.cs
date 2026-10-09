using Raylib_cs;

namespace Fesh;

// Pixel sprites for the land animals you can accidentally hook. Each sprite faces right; '.' is transparent.
static class AnimalArt
{
    sealed record Sprite(string[] Rows, Dictionary<char, string> Colors);

    static readonly Dictionary<string, Sprite> Sprites = new()
    {
        ["carabao"] = new(new[]
        {
            "..........H...H..",
            "..........HH.HH..",
            "...........GGG...",
            "..GGGGGGGGGGeGG..",
            ".GgGGGGGGGGGGnn..",
            "GGggggggggGGGG...",
            "..GgGGGGGGGGG....",
            "..G.G.....G.G....",
            "..K.K.....K.K...."
        }, new() { ['H'] = "#ddd3b0", ['G'] = "#707779", ['g'] = "#525c60", ['e'] = "#f5e9c9", ['n'] = "#3b4144", ['K'] = "#333b40" }),
        ["tarsier"] = new(new[]
        {
            "..B.....B...",
            "..bBBBBBb...",
            "..BYYBYYB...",
            "..BYeBYeB...",
            "...BBBBB....",
            "....bbbB....",
            "B...BbbB....",
            ".BBBB..BB..."
        }, new() { ['B'] = "#8a6749", ['b'] = "#bb9872", ['Y'] = "#e5c56b", ['e'] = "#201b1c" }),
        ["hornbill"] = new(new[]
        {
            ".......OOO.....",
            "......ROOOO....",
            ".....RReYYYYY..",
            ".....RRRYYYY...",
            "..KKKKRR.......",
            ".KKkkKKR.......",
            "WWWKKKK........",
            "WW...L.L......."
        }, new() { ['O'] = "#c7793f", ['R'] = "#a94e31", ['Y'] = "#e2b959", ['K'] = "#283c3e", ['k'] = "#405456", ['W'] = "#e6dfbf", ['e'] = "#faf1cb", ['L'] = "#6c634c" }),
        // The atoll's odd catch (1.18.1: it had no sprite, so landing one crashed the catch card).
        ["parrot"] = new(new[]
        {
            "......GGG...",
            ".....GGGGY..",
            ".....GGeGYY.",
            ".....GGGG.y.",
            "....RRGGG...",
            "...RRRggG...",
            "...RRRggG...",
            "..BBRRgG....",
            ".BBB.K.K....",
            "BB.........."
        }, new() { ['G'] = "#3f9a45", ['g'] = "#7cc456", ['R'] = "#d8433a", ['B'] = "#3f6fc8", ['Y'] = "#f2d16a", ['y'] = "#3a3a3a", ['e'] = "#1b1b1b", ['K'] = "#6b5a4a" }),
        ["goat"] = new(new[]
        {
            "..........hh.",
            ".........hGG.",
            "w........GGeG",
            ".w.....GGGGgb",
            "..GGGGGGGG..b",
            "..GgggggGG...",
            "..G.G...G.G..",
            "..K.K...K.K.."
        }, new() { ['G'] = "#e8e2d4", ['g'] = "#c8c0b0", ['h'] = "#8a7a62", ['e'] = "#2a2018", ['w'] = "#c8c0b0", ['b'] = "#b0a890", ['K'] = "#4a4038" }),
        ["dog"] = new(new[]
        {
            "........DD...",
            ".......BBBB..",
            "D......BBeBBn",
            ".D.....BBBBt.",
            "..BBBBBBBB...",
            "..BbbbbbBB...",
            "..B.B...B.B..",
            "..D.D...D.D.."
        }, new() { ['B'] = "#a0703c", ['b'] = "#c08a55", ['D'] = "#6b4a2b", ['e'] = "#1b1b1b", ['n'] = "#1b1b1b", ['t'] = "#e8939a" }),
        ["cat"] = new(new[]
        {
            ".........K.K.",
            "K........GGG.",
            "K.......GGeGp",
            ".K......GGGG.",
            "..GGGGGGGG...",
            "..GgggggGG...",
            "..G.G...G.G..",
            "..K.K...K.K.."
        }, new() { ['G'] = "#8a8f93", ['g'] = "#b4b9bc", ['K'] = "#5e6468", ['e'] = "#f3c25b", ['p'] = "#e8939a" }),
        ["sheep"] = new(new[]
        {
            "..wWWWWw.....",
            ".WWWWWWWWKK..",
            ".WWWWWWWWKeK.",
            ".WWWWWWWWKKK.",
            "..wWWWWWw.K..",
            "...K.K.K.K...",
            "...K.K.K.K..."
        }, new() { ['W'] = "#f2efe6", ['w'] = "#d6d1c4", ['K'] = "#3b3b3b", ['e'] = "#ffffff" }),
        ["chicken"] = new(new[]
        {
            ".....r..",
            "....WWo.",
            "....WeW.",
            "WW..WW..",
            "WWWWWW..",
            ".WWwWW..",
            "..y.y..."
        }, new() { ['W'] = "#f2efe6", ['w'] = "#d6d1c4", ['r'] = "#e04b3a", ['o'] = "#f3a83b", ['e'] = "#1b1b1b", ['y'] = "#f3a83b" }),
        ["pig"] = new(new[]
        {
            "........p.p..",
            "........PPPP.",
            ".pp....PPePSS",
            "..pPPPPPPPPSS",
            "..PPPPPPPPPP.",
            "..PPPPPPPPP..",
            "..p.p...p.p.."
        }, new() { ['P'] = "#f2a5b5", ['p'] = "#d9788e", ['S'] = "#e88aa0", ['e'] = "#1b1b1b" })
    };

    static readonly Dictionary<string, Texture2D> textures = new();

    public static (int w, int h) Size(string id) => (Sprites[id].Rows[0].Length, Sprites[id].Rows.Length);

    // Draws the animal in the world with its feet at (x, y). Frame 1 bobs it up a pixel, for a trotting look.
    // Pose 1 dips the head (grazing, pecking); pose 2 lifts the tail (a wag or a flick).
    public static void Draw(Pix p, string id, float x, float y, bool flip, int frame, int pose = 0)
    {
        var s = Sprites[id];
        int w = s.Rows[0].Length, h = s.Rows.Length;
        int left = (int)MathF.Round(x) - w / 2, top = (int)MathF.Round(y) - h + 1 - frame;
        int headCol = w <= 8 ? 4 : w - 6;
        p.Rect(left + 1, (int)MathF.Round(y) + 1, w - 2, 1, "rgba(0,0,0,0.2)");
        for (int r = 0; r < h; r++)
            for (int c = 0; c < w; c++)
            {
                char ch = s.Rows[r][c];
                if (ch == '.') continue;
                int dr = pose == 1 && c >= headCol && r <= 3 ? 1 : pose == 2 && c <= 1 && r <= 3 ? -1 : 0;
                p.Rect(left + (flip ? w - 1 - c : c), top + r + dr, 1, 1, s.Colors[ch]);
            }
    }

    // A one-pixel-per-pixel texture of the sprite, drawn scaled up with point filtering on the catch card.
    public static Texture2D Texture(string id)
    {
        if (textures.TryGetValue(id, out var tex)) return tex;
        var s = Sprites[id];
        int w = s.Rows[0].Length, h = s.Rows.Length;
        var px = new Color[w * h];
        for (int r = 0; r < h; r++)
            for (int c = 0; c < w; c++)
                if (s.Rows[r][c] != '.') px[r * w + c] = Pal.C(s.Colors[s.Rows[r][c]]);
        tex = Gfx.ToTexture(px, w, h, TextureFilter.Point);
        textures[id] = tex;
        return tex;
    }

    public static void Shutdown()
    {
        foreach (var t in textures.Values) Raylib.UnloadTexture(t);
        textures.Clear();
    }
}
