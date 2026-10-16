#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // FESH_SPRITES also writes -lighthouse: the old hand-drawn tower and the Blender one side by side, broken (with the
    // stair ladder), mended by day, and lit at night, on Parola's sand.
    void ExportLighthouseSprites(string path)
    {
        var keep = pix;
        bool keepOld = OldArt;
        var sheet = new Pix(W, H);
        pix = sheet;
        Array.Fill(sheet.Buf, Pal.C("#e3cf98"));
        int col = 0;
        foreach (bool old in new[] { true, false })
        {
            OldArt = old;
            foreach (var (stage, night) in new[] { (0, false), (3, false), (3, true) })
            {
                int x = 24 + col * 42, y = 100;
                if (night) pix.Rect(x - 20, 0, 40, 106, "#1f2f45");
                DrawLighthouseAt(x, y, stage, night);
                col++;
            }
            col++;
        }
        OldArt = keepOld;
        pix = keep;
        int w = 24 + col * 42, h = 106;
        var img = Raylib.GenImageColor(w, h, Color.Black);
        for (int yy = 0; yy < h; yy++)
            for (int xx = 0; xx < w; xx++) Raylib.ImageDrawPixel(ref img, xx, yy, sheet.Buf[yy * W + xx]);
        Raylib.ImageResizeNN(ref img, w * 6, h * 6);
        Raylib.ExportImage(img, path.Replace(".png", "-lighthouse.png"));
        Raylib.UnloadImage(img);
        ExportBuildingSprites(path);
    }

    // -buildings: the old hand-drawn bahay, shack, kubo and Tomas's hut on the top row, the Blender ones below, by day.
    void ExportBuildingSprites(string path)
    {
        var keep = pix;
        bool keepOld = OldArt;
        var sheet = new Pix(W, H);
        pix = sheet;
        Array.Fill(sheet.Buf, Pal.C("#86b86a"));
        foreach (bool old in new[] { true, false })
        {
            OldArt = old;
            int y = old ? 0 : 80;
            DrawBahay(30, y + 60, "#c0392b");
            DrawShack(70, y + 55);
            DrawKubo(105, y + 54);
            // Tomas's hut is drawn at its fixed place on Saltmere (x 150..170, y 47..71), so shift the sheet's camera.
            sheet.CamX = 150 - 140; sheet.CamY = 47 - (y + 22);
            DrawHut();
            sheet.CamX = 0; sheet.CamY = 0;
        }
        OldArt = keepOld;
        pix = keep;
        int w = 180, h = 160;
        var img = Raylib.GenImageColor(w, h, Color.Black);
        for (int yy = 0; yy < h; yy++)
            for (int xx = 0; xx < w; xx++) Raylib.ImageDrawPixel(ref img, xx, yy, sheet.Buf[yy * W + xx]);
        Raylib.ImageResizeNN(ref img, w * 6, h * 6);
        Raylib.ExportImage(img, path.Replace(".png", "-buildings.png"));
        Raylib.UnloadImage(img);
        ExportSeaLifeSprites(path);
    }

    // -sealife: the sanctuary's turtle, dugong and whale shark under the water, through their swim (columns), heading
    // east then west (rows).
    void ExportSeaLifeSprites(string path)
    {
        var keep = pix;
        var sheet = new Pix(W, H);
        pix = sheet;
        Array.Fill(sheet.Buf, Pal.C("#2f7f9a"));
        var kinds = new[] { ("pawikan", 18), ("dugong", 26), ("butanding", 50) };
        int y = 14;
        foreach (var (kind, w) in kinds)
        {
            foreach (int d in new[] { 1, -1 })
            {
                for (int f = 0; f < 6; f++)
                {
                    var c = new SeaCreature { Kind = kind, Phase = 0 };
                    DrawSeaAnimal(c, 10 + w / 2 + f * (w + 6), y, d, f / 5f, false, 1);
                }
                y += kind == "butanding" ? 26 : 18;
            }
        }
        pix = keep;
        int ww = 320, h = Math.Min(H, y + 6);
        var img = Raylib.GenImageColor(ww, h, Color.Black);
        for (int yy = 0; yy < h; yy++)
            for (int xx = 0; xx < ww; xx++) Raylib.ImageDrawPixel(ref img, xx, yy, sheet.Buf[yy * W + xx]);
        Raylib.ImageResizeNN(ref img, ww * 4, h * 4);
        Raylib.ExportImage(img, path.Replace(".png", "-sealife.png"));
        Raylib.UnloadImage(img);
    }
}
#endif
