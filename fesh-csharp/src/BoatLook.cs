using Raylib_cs;

namespace Fesh;

// 1.38: the banca's hull, floats and outrigger booms are a Blender model (tools/blender/boat.py renders BoatArt.cs), in two
// layers per facing so the fisher sits between them: Back (the far half, the inside, the booms) is drawn first, then the
// rig and the fisher, then Front (the near wall, its rim and the near float). Left is the mirror of right. FESH_OLDART=1
// keeps the hand-drawn hull. The same model, on a cradle, gives the boatyard's construction frames (BoatYard.cs).
partial class Game
{
    static Color[] boatPal;

    // Draws one of BoatArt's grids with its anchor (ax, ay) at (x, y), mirrored about that column if asked.
    void BlitBoat(string[] grid, int ax, int ay, int x, int y, bool mirror = false, float alpha = 1f)
    {
        var palette = BoatPaletteNow();
        int oy = y - ay;
        for (int j = 0; j < grid.Length; j++)
        {
            string row = grid[j];
            for (int i = 0; i < row.Length; i++)
            {
                char ch = row[i];
                if (ch == '.') continue;
                int k = ch >= 'a' ? ch - 'a' : 26 + (ch - 'A');
                var c = palette[k];
                if (alpha < 1f) c.A = (byte)(c.A * alpha);
                pix.Rect(mirror ? x - (i - ax) : x + (i - ax), oy + j, 1, 1, c);
            }
        }
    }

    // Where each facing's model centre sits against the boat's (X, Y), so the old rig and fisher positions still land.
    const int BoatSideDy = 1, BoatUpDy = -3, BoatDownDy = -3;

    // A line of foam where the hull meets the water: one translucent pixel under the lowest pixel of each column of the layer,
    // so the boat sits in the sea and doesn't hover over it.
    void WaterlineFoam(string[] grid, int ax, int ay, int x, int y, bool mirror)
    {
        int oy = y - ay;
        for (int i = 0; i < grid[0].Length; i++)
        {
            int low = -1;
            for (int j = grid.Length - 1; j >= 0 && low < 0; j--) if (grid[j][i] != '.') low = j;
            if (low < 0) continue;
            int px = mirror ? x - (i - ax) : x + (i - ax);
            pix.Rect(px, oy + low + 1, 1, 1, Pal.Rgba(238, 250, 252, 0.6f));
            if ((i + (int)(time * 3)) % 4 == 0) pix.Rect(px, oy + low + 2, 1, 1, Pal.Rgba(220, 244, 248, 0.3f));
        }
    }

    void BlitBoatHull(string face, bool front, int X, int Y, int dir)
    {
        switch (face)
        {
            case "up": BlitBoat(front ? BoatArt.UpFront : BoatArt.UpBack, BoatArt.UpBackX, BoatArt.UpBackY, X, Y + BoatUpDy); if (front) WaterlineFoam(BoatArt.UpFront, BoatArt.UpFrontX, BoatArt.UpFrontY, X, Y + BoatUpDy, false); break;
            case "down": BlitBoat(front ? BoatArt.DownFront : BoatArt.DownBack, BoatArt.DownBackX, BoatArt.DownBackY, X, Y + BoatDownDy); if (front) WaterlineFoam(BoatArt.DownFront, BoatArt.DownFrontX, BoatArt.DownFrontY, X, Y + BoatDownDy, false); break;
            default:
                BlitBoat(front ? BoatArt.RightFront : BoatArt.RightBack, BoatArt.RightBackX, BoatArt.RightBackY, X, Y + BoatSideDy, dir < 0);
                if (front) WaterlineFoam(BoatArt.RightFront, BoatArt.RightFrontX, BoatArt.RightFrontY, X, Y + BoatSideDy, dir < 0);
                break;
        }
    }
}

partial class Game
{
    // The hull's teal trim (palette entries 0, 1 and 9: shade, base, light) in the colour you've painted it (Wardrobe.cs): the
    // chosen colour as the base, a darker and a lighter step either side, so the planks and the rim keep their shading.
    static Color[] paintedPal;
    static string paintedFor;
    Color[] BoatPaletteNow()
    {
        boatPal ??= BoatArt.Palette.Select(Pal.C).ToArray();
        string hex = HullHex;
        if (hex == null) return boatPal;
        if (paintedPal != null && paintedFor == hex) return paintedPal;
        var pal = (Color[])boatPal.Clone();
        var b = Pal.C(hex);
        Color Step(float k) => new((byte)Math.Clamp(b.R * k, 0, 255), (byte)Math.Clamp(b.G * k, 0, 255), (byte)Math.Clamp(b.B * k, 0, 255), (byte)255);
        pal[0] = Step(0.6f); pal[1] = Step(1f); pal[9] = new Color((byte)Math.Min(255, b.R + (255 - b.R) * 0.35f), (byte)Math.Min(255, b.G + (255 - b.G) * 0.35f), (byte)Math.Min(255, b.B + (255 - b.B) * 0.35f), (byte)255);
        paintedFor = hex; paintedPal = pal;
        return pal;
    }
}
