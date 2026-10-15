// Fonts the way Raylib makes them: stb_truetype rasterises each glyph at the pixel size, advances are whole pixels, and the
// glyphs sit in one atlas texture with 4 px of padding. StbTrueTypeSharp is a port of the same library, so the glyph
// shapes, advances and offsets match the desktop game and its layout (wrapping, centring, ellipsising) carries over.
using System.Runtime.InteropServices;
using Raylib_cs;
using StbTrueTypeSharp;

namespace Fesh.Droid;

internal static unsafe class FontBackend
{
    const int Padding = 4;
    // GL texture id -> (codepoint -> glyph index), so a draw finds a glyph without scanning.
    static readonly Dictionary<uint, Dictionary<int, int>> index = new();

    public static Font Load(byte[] ttf, int fontSize, int[] codepoints, int count)
    {
        var info = new StbTrueType.stbtt_fontinfo();
        byte* data = (byte*)Marshal.AllocHGlobal(ttf.Length);
        Marshal.Copy(ttf, 0, (IntPtr)data, ttf.Length);   // the font info keeps this pointer for as long as it's used
        try
        {
            StbTrueType.stbtt_InitFont(info, data, 0);
            float scale = StbTrueType.stbtt_ScaleForPixelHeight(info, fontSize);
            int ascent, descent, lineGap;
            StbTrueType.stbtt_GetFontVMetrics(info, &ascent, &descent, &lineGap);

            var bitmaps = new byte[count][];
            var glyphs = (GlyphInfo*)NativeMemory.AllocZeroed((nuint)(count * sizeof(GlyphInfo)));
            var recs = (Rectangle*)NativeMemory.AllocZeroed((nuint)(count * sizeof(Rectangle)));
            for (int i = 0; i < count; i++)
            {
                int cp = codepoints[i], gw = 0, gh = 0, ox = 0, oy = 0, adv = 0, lsb = 0;
                byte* bmp = StbTrueType.stbtt_GetCodepointBitmap(info, scale, scale, cp, &gw, &gh, &ox, &oy);
                StbTrueType.stbtt_GetCodepointHMetrics(info, cp, &adv, &lsb);
                glyphs[i].Value = cp;
                glyphs[i].OffsetX = ox;
                glyphs[i].OffsetY = oy + (int)(ascent * scale);
                glyphs[i].AdvanceX = (int)(adv * scale);
                if (cp == 32 || bmp == null) { gw = cp == 32 ? glyphs[i].AdvanceX : 0; gh = cp == 32 ? fontSize : 0; bitmaps[i] = null; }
                else
                {
                    bitmaps[i] = new byte[gw * gh];
                    Marshal.Copy((IntPtr)bmp, bitmaps[i], 0, gw * gh);
                }
                if (bmp != null) StbTrueType.stbtt_FreeBitmap(bmp, null);
                recs[i] = new Rectangle(0, 0, gw, gh);
            }

            // Shelf packing: rows of padded cells, in an atlas wide enough that it ends up roughly square.
            int atlasW = 128;
            int[] px = new int[count], py = new int[count];
            int atlasH;
            while (true)
            {
                int x = 0, y = 0, rowH = 0;
                for (int i = 0; i < count; i++)
                {
                    int cw = (int)recs[i].Width + 2 * Padding, ch = (int)recs[i].Height + 2 * Padding;
                    if (x + cw > atlasW) { x = 0; y += rowH; rowH = 0; }
                    px[i] = x; py[i] = y;
                    x += cw; rowH = Math.Max(rowH, ch);
                }
                atlasH = y + rowH;
                if (atlasH <= atlasW || atlasW >= 4096) break;
                atlasW *= 2;
            }
            var rgba = new byte[atlasW * atlasH * 4];
            for (int i = 0; i < count; i++)
            {
                int gw = (int)recs[i].Width, gh = (int)recs[i].Height;
                recs[i].X = px[i] + Padding; recs[i].Y = py[i] + Padding;
                if (bitmaps[i] == null) continue;
                for (int yy = 0; yy < gh; yy++)
                    for (int xx = 0; xx < gw; xx++)
                    {
                        int o = ((py[i] + Padding + yy) * atlasW + px[i] + Padding + xx) * 4;
                        rgba[o] = rgba[o + 1] = rgba[o + 2] = 255;
                        rgba[o + 3] = bitmaps[i][yy * gw + xx];
                    }
            }
            Texture2D tex;
            fixed (byte* p = rgba) tex = Gl.CreateTexture(atlasW, atlasH, (IntPtr)p);
            var map = new Dictionary<int, int>();
            for (int i = 0; i < count; i++) map[codepoints[i]] = i;
            index[tex.Id] = map;
            return new Font { BaseSize = fontSize, GlyphCount = count, GlyphPadding = Padding, Texture = tex, Recs = recs, Glyphs = glyphs };
        }
        finally { Marshal.FreeHGlobal((IntPtr)data); }
    }

    public static void Unload(Font f)
    {
        index.Remove(f.Texture.Id);
        Gl.DeleteTexture(f.Texture);
        NativeMemory.Free(f.Glyphs);
        NativeMemory.Free(f.Recs);
    }

    // Raylib falls back to '?' for a character the font doesn't have.
    public static int GlyphIndex(Font f, int cp)
    {
        var map = index[f.Texture.Id];
        return map.TryGetValue(cp, out int i) ? i : map.TryGetValue('?', out int q) ? q : 0;
    }

    static IEnumerable<int> Codepoints(string s)
    {
        for (int i = 0; i < s.Length; i++)
        {
            if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1])) { yield return char.ConvertToUtf32(s[i], s[i + 1]); i++; }
            else yield return s[i];
        }
    }

    public static float Measure(Font f, string text, float fontSize, float spacing)
    {
        float scale = fontSize / f.BaseSize, line = 0, widest = 0;
        foreach (int cp in Codepoints(text))
        {
            if (cp == '\n') { widest = Math.Max(widest, line); line = 0; continue; }
            int i = GlyphIndex(f, cp);
            line += (f.Glyphs[i].AdvanceX == 0 ? f.Recs[i].Width + f.Glyphs[i].OffsetX : f.Glyphs[i].AdvanceX) * scale + spacing;
        }
        return Math.Max(widest, line - (line > 0 ? spacing : 0));   // no spacing after the last glyph
    }

    // Draws with a 2D transform (position, rotation about it, then the origin shift), which DrawTextEx leaves at identity.
    public static void Draw(Font f, string text, System.Numerics.Vector2 pos, System.Numerics.Vector2 origin, float rotation, float fontSize, float spacing, Color tint)
    {
        float scale = fontSize / f.BaseSize, ox = 0, oy = 0;
        float rad = rotation * MathF.PI / 180f, cs = MathF.Cos(rad), sn = MathF.Sin(rad);
        System.Numerics.Vector2 T(float x, float y)
        {
            x -= origin.X; y -= origin.Y;
            return new System.Numerics.Vector2(pos.X + x * cs - y * sn, pos.Y + x * sn + y * cs);
        }
        foreach (int cp in Codepoints(text))
        {
            if (cp == '\n') { oy += fontSize + 2; ox = 0; continue; }
            int i = GlyphIndex(f, cp);
            ref var g = ref f.Glyphs[i];
            if (cp != ' ' && cp != '\t')
            {
                var r = f.Recs[i];
                float dx = ox + g.OffsetX * scale - Padding * scale, dy = oy + g.OffsetY * scale - Padding * scale;
                float dw = (r.Width + 2 * Padding) * scale, dh = (r.Height + 2 * Padding) * scale;
                float tw = f.Texture.Width, th = f.Texture.Height;
                Gl.TexQuad(f.Texture, T(dx, dy), T(dx + dw, dy), T(dx + dw, dy + dh), T(dx, dy + dh),
                    (r.X - Padding) / tw, (r.Y - Padding) / th, (r.X + r.Width + Padding) / tw, (r.Y + r.Height + Padding) / th, tint);
            }
            ox += (g.AdvanceX == 0 ? f.Recs[i].Width * scale : g.AdvanceX * scale) + spacing;
        }
    }
}
