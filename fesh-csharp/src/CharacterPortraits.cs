using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// Portrait textures are created only for people the player meets. The world sprites and saved looks stay independent.
partial class Game
{
    static readonly Dictionary<string, Texture2D> folkPortraitTextures = new();
    const string PortraitKeys = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVW";

    static (Color[] px, int w, int h) PortraitPixels(string[] palette, string[] rows)
    {
        int w = rows[0].Length, h = rows.Length;
        var colors = palette.Select(Pal.C).ToArray();
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int c = PortraitKeys.IndexOf(rows[y][x]);
                if (c >= 0 && c < colors.Length) px[y * w + x] = colors[c];
            }
        return (px, w, h);
    }

    static string PortraitSpeaker(string speaker) => speaker switch
    {
        "Mang Tomas" => "Tomas",
        "Joy" => "Bantay Joy",
        _ => speaker
    };

    static bool HasFolkPortrait(string speaker) => FolkPortraitArt.All.ContainsKey(PortraitSpeaker(speaker));

    static bool DrawFolkPortrait(string speaker, float x, float y, float scale = 2)
    {
        if (OldArt) return false;
        speaker = PortraitSpeaker(speaker);
        if (!FolkPortraitArt.All.TryGetValue(speaker, out var sprite)) return false;
        if (!folkPortraitTextures.TryGetValue(speaker, out var tex))
        {
            var (px, w, h) = PortraitPixels(sprite.Palette, sprite.Rows);
            tex = Gfx.ToTexture(px, w, h, TextureFilter.Point);
            folkPortraitTextures[speaker] = tex;
        }
        float dw = tex.Width * scale, dh = tex.Height * scale;
        Gfx.Box(x - 4, y - 4, dw + 8, dh + 8, Pal.C("#244b50"), Pal.Ink, 2, 3, 3);
        Gfx.Rect(x, y, dw, 2, Pal.C("#b6c5a2"));
        Gfx.Rect(x, y + dh - 3, dw, 3, Pal.C("#d4ad68"));
        DrawTexturePro(tex, new Rectangle(0, 0, tex.Width, tex.Height), Gfx.S(x, y, dw, dh), System.Numerics.Vector2.Zero, 0, Color.White);
        return true;
    }
}
