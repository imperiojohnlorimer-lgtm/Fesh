#if DEBUG
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

partial class Game
{
    // Boat cosmetics (1.46, FESH_BOATCOS_TEST=1): the lantern and the bunting are workbench accessories that change only how the
    // banca looks (no speed, no luck), are drawn in every facing, and the lantern lights the water after dark and not by day or
    // with the light off; the trim paint recolours only the teal parts of the hull.
    IEnumerable<int> BoatCosmeticsScript()
    {
        Note("Boat cosmetics");
        bool effectsWas = EffectsOff;
        EffectsOff = false;
        state = new State { created = true, look = new Look { name = "Boatwright" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 5; Settings.Data.light = 2; Inp.ScriptMouse = Offscreen;
        yield return 3;

        /* ---------- the items ---------- */
        Check("both are accessories with icons", new[] { "boat_lantern", "boat_bunting" }.All(id => Items.ById.TryGetValue(id, out var d) && d.Kind == "accessory" && d.Icon != null));
        Check("both have a workbench recipe from things you can get", new[] { "boat_lantern", "boat_bunting" }.All(id => Items.Recipes.Any(r => r.Out == id && r.Station == "workbench")));
        float speedBefore = BoatSpeed; int luckBefore = Luck("lagoon", null);
        Give("boat_lantern"); Give("boat_bunting");
        Check("they change nothing about the boat's speed or the fishing", BoatSpeed == speedBefore && Luck("lagoon", null) == luckBefore);

        /* ---------- drawn in every facing ---------- */
        int Hash(string face, bool night)
        {
            var p = new Pix(90, 70);
            var keep = pix; pix = p; string faceWas = boatFace; boatFace = face;
            Array.Fill(p.Buf, Pal.C("#2a6f97"));
            DrawBoat(45, 40, time, occupied: true);
            pix = keep; boatFace = faceWas;
            int h = 17; foreach (var c in p.Buf) h = unchecked(h * 31 + c.R * 5 + c.G * 3 + c.B); return h;
        }
        bool differs = true;
        foreach (var face in new[] { "right", "left", "up", "down" })
        {
            state.inv["boat_lantern"] = 1; state.inv["boat_bunting"] = 1; int both = Hash(face, false);
            state.inv.Remove("boat_lantern"); int bunting = Hash(face, false);
            state.inv.Remove("boat_bunting"); int none = Hash(face, false);
            state.inv["boat_lantern"] = 1; int lantern = Hash(face, false);
            state.inv.Remove("boat_lantern");
            if (both == none || bunting == none || lantern == none || both == bunting) differs = false;
        }
        Check("bunting and lantern each change the picture, in all four facings", differs);
        Give("boat_lantern"); Give("boat_bunting");
        {
            var sheet = new Pix(240, 60); Array.Fill(sheet.Buf, Pal.C("#2a6f97"));
            var keep = pix; pix = sheet; string was = boatFace;
            int ax = 30; foreach (var face in new[] { "right", "left", "up", "down" }) { boatFace = face; DrawBoat(ax, 36, 1.5f, occupied: true); ax += 60; }
            boatFace = was; pix = keep;
            var img = GenImageColor(240, 60, Color.Black);
            for (int y = 0; y < 60; y++) for (int x = 0; x < 240; x++) ImageDrawPixel(ref img, x, y, sheet.Buf[y * 240 + x]);
            ImageResizeNN(ref img, 240 * 5, 60 * 5);
            ExportImage(img, Path.Combine(testDir, "boat-cosmetics.png"));
            UnloadImage(img);
        }

        /* ---------- the lantern at night ---------- */
        Give("boat"); state.aboard = true; state.hullId = null;
        player.X = SaltJettyX + 40; player.Y = SaltJettyY + 10; player.Face = "right"; boatFace = "right"; yield return 4;
        SetNight(true); state.clock = 22 * 60; yield return 5;
        Check("with the lantern up the boat knows where its lamp is", BoatLampOn);
        float lit = -1; screenProbe = im => { var p = ScreenOfLamp(); lit = ProbeBrightness(im, p.X, p.Y); }; yield return 3;
        pendingShot = "b1-night-lantern"; yield return 3;
        state.inv.Remove("boat_lantern"); yield return 4;
        Check("without it there is no lamp", !BoatLampOn);
        var lampAt = (X: boatLampX, Y: boatLampY);
        float dark = -1; screenProbe = im => { var p = ScreenOfLamp(lampAt.X, lampAt.Y); dark = ProbeBrightness(im, p.X, p.Y); }; yield return 3;
        Check($"after dark the lantern lights the water round it ({lit:0} against {dark:0})", lit > dark + 25);
        EffectsOff = effectsWas;
        Settings.Data.light = -1;
    }

    (float X, float Y) ScreenOfLamp(float? lx = null, float? ly = null)
    {
        var p = ScreenOf(lx ?? boatLampX, ly ?? boatLampY);
        return (p.X, p.Y);
    }

    static float ProbeBrightness(Image im, float px, float py)
    {
        float sum = 0; int n = 0;
        for (int dx = -8; dx <= 8; dx += 2) for (int dy = -8; dy <= 8; dy += 2) { var c = Raylib.GetImageColor(im, (int)(Gfx.OX + (px + dx) * Gfx.Z), (int)(Gfx.OY + (py + dy) * Gfx.Z)); sum += c.R + c.G + c.B; n++; }
        return sum / n;
    }
}
#endif
