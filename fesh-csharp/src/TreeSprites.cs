#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // FESH_SPRITES also writes -trees: each row a kind, the 1.32 hand-drawn trees on the left (three sizes), then the
    // Blender ones (small to big); then bushes: 1.32's, 1.34's hand-drawn and the Blender ones (four shapes each).
    void ExportTreeSprites(string path)
    {
        var keep = pix;
        const int SW = 560, SH = 330;
        var sheet = new Pix(SW, SH);
        pix = sheet;
        Array.Fill(sheet.Buf, Pal.C("#5d9b45"));
        sheet.Rect(0, 150, SW, 75, "#e8cf96");    // sand behind the palms
        sheet.Rect(0, 75, SW, 75, "#e8f0f4");     // snow behind the firs
        string keepTree = treeStyle, keepBush = bushStyle;
        float[] olds = { 0.8f, 1.0f, 1.2f };
        foreach (var (kind, y, tx, ty) in new[] { ('t', 66, 10, 10), ('f', 140, 60, 5), ('h', 215, 110, 25) })
        {
            int x = 22;
            treeStyle = "old";
            foreach (var r in olds)
            {
                int m = 1;
                float s = r * TreeScale(kind);
                switch (kind) { case 'f': DrawFir(tx, ty, x, y, 0, s); break; case 'h': DrawPalm(tx, ty, x, y, 0, s, m); break; default: DrawOak(tx, ty, x, y, 0, s, m); break; }
                x += 46;
            }
            x += 20;
            treeStyle = "new";
            LoadTreeSprites();
            var set = kind == 't' ? oakSprites : kind == 'f' ? firSprites : palmSprites;
            foreach (var spr in set) { BlitTree(spr, x, y, 1, 0); x += 52; }
        }
        int bx = 22;
        foreach (var style in new[] { "leafy", "hand", "blender" })
        {
            bushStyle = style;
            for (int i = 0; i < 4; i++)
            {
                // Tiles whose hashes give shapes 0..3 (Pix.Hash(tx, ty, 64)).
                int tx = 20 + i, ty = 12;
                for (int k = 0; k < 400 && (int)(Pix.Hash(tx, ty, 64) * 4) % 4 != i; k++) tx += 7;
                DrawBush(tx, ty, bx, 300);
                bx += 26;
            }
            bx += 18;
        }
        treeStyle = keepTree; bushStyle = keepBush;
        pix = keep;
        var img = Raylib.GenImageColor(SW, SH, Color.Black);
        for (int yy = 0; yy < SH; yy++)
            for (int xx = 0; xx < SW; xx++) Raylib.ImageDrawPixel(ref img, xx, yy, sheet.Buf[yy * SW + xx]);
        Raylib.ImageResizeNN(ref img, SW * 3, SH * 3);
        Raylib.ExportImage(img, path.Replace(".png", "-trees.png"));
        Raylib.UnloadImage(img);
        // -bushes: the hand-drawn bush's four shapes at 8x, with berries and picked.
        var bush = new Pix(140, 32);
        pix = bush;
        Array.Fill(bush.Buf, Pal.C("#5d9b45"));
        bushStyle = "hand";
        for (int i = 0; i < 4; i++)
        {
            int tx = 20 + i, ty = 12;
            for (int k = 0; k < 400 && (int)(Pix.Hash(tx, ty, 64) * 4) % 4 != i; k++) tx += 7;
            DrawBush(tx, ty, 16 + i * 34, 27);
        }
        bushStyle = keepBush;
        pix = keep;
        img = Raylib.GenImageColor(140, 32, Color.Black);
        for (int yy = 0; yy < 32; yy++)
            for (int xx = 0; xx < 140; xx++) Raylib.ImageDrawPixel(ref img, xx, yy, bush.Buf[yy * 140 + xx]);
        Raylib.ImageResizeNN(ref img, 140 * 8, 32 * 8);
        Raylib.ExportImage(img, path.Replace(".png", "-bushes.png"));
        Raylib.UnloadImage(img);
        // -ironbill: the marlin at 4x: level, a breach's rise, top and dive both ways, the charge, the tail-walk, sail
        // folded, and under the surface with only the sail out.
        var mb = new Pix(400, 70);
        pix = mb;
        Array.Fill(mb.Buf, Pal.C("#2f7fa3"));
        var poses = new (float a, float sail, float sub, bool fin)[] { (0, 1, 0, false), (-1.05f, 1, 0, false), (0, 1, 0, false), (1.05f, 1, 0, false),
            (MathF.PI + 1.05f, 1, 0, false), (MathF.PI - 1.05f, 1, 0, false), (-1.32f, 0.9f, 0, false), (0.3f, 0, 0, false), (0.4f, 0, 1, false) };
        for (int i = 0; i < poses.Length; i++)
        {
            var p = poses[i];
            DrawMarlin(24 + i * 44, 35, p.a, 0.85f, 0.6f, p.sail, p.sub);
            if (p.sub > 0) DrawMarlin(24 + i * 44, 35, p.a, 0.85f, 0.6f, 0.85f, finOnly: true);
        }
        pix = keep;
        img = Raylib.GenImageColor(400, 70, Color.Black);
        for (int yy = 0; yy < 70; yy++)
            for (int xx = 0; xx < 400; xx++) Raylib.ImageDrawPixel(ref img, xx, yy, mb.Buf[yy * 400 + xx]);
        Raylib.ImageResizeNN(ref img, 400 * 4, 70 * 4);
        Raylib.ExportImage(img, path.Replace(".png", "-ironbill.png"));
        Raylib.UnloadImage(img);
    }
}
#endif
