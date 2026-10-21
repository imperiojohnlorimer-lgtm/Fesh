using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// Photo mode (1.44): from the pause menu, the HUD goes away, the clock stops (the sea, the trees and the people still move),
// and you can look about (the move keys or the stick), zoom (the buttons, the wheel or the bumpers), put a frame round it
// (a snapshot, a postcard or a strip of film, with where and when written on it) and take the picture. A picture, and a
// card from the Fesh-dex ("Save as image"), are written as PNG files to Pictures\Fesh (a test run: next to its save).
// Taking one waits a frame so the buttons are left out of it.
partial class Game
{
    // Still: whether what it's a picture of is still on the screen when the frame comes (a card closed meanwhile isn't).
    sealed class ShotRequest { public Rectangle Area; public string Name; public int Wait = 2; public Func<bool> Still; }
    ShotRequest shotReq;
    string lastShotPath;
    bool photoBarDrawn, shotHadBar, photoFlashDrawn, shotHadFlash;   // (for the autotest: the buttons and the flash are never in a picture)
    (int w, int h) lastShotSize;

    int photoFrame;                 // 0 none, 1 snapshot, 2 postcard, 3 film
    bool photoCaption = true;       // where and when, written on the frame
    float photoPanX, photoPanY;     // how far the view has been moved from you
    int photoZoom;                  // the zoom while in photo mode (0: as in Settings)
    float photoFlash;               // the white flash after a picture
    static readonly string[] PhotoFrames = { "No frame", "Snapshot", "Postcard", "Film" };
    const float PhotoPanMax = 220;

    string PhotoDir => Platform.PhotoDir
        ?? (Environment.GetEnvironmentVariable("FESH_AUTOTEST") is { Length: > 0 } ? Path.Combine(SaveFile.Dir, "photos")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Fesh"));

    // The zoom the view should have: photo mode's own, else the setting.
    int WantZoom => mode == "photo" && photoZoom is >= 2 and <= 5 ? photoZoom : Settings.Data.zoom;

    void OpenPhoto()
    {
        mode = "photo";
        photoPanX = photoPanY = 0;
        photoZoom = ViewK;
        photoFlash = 0;
        SetPrompt("");
        Sfx.Play("ui");
    }

    void ClosePhoto()
    {
        if (mode != "photo") return;
        mode = "play";
        photoZoom = 0;   // the view goes back to the setting's zoom next frame
        shotReq = null;
        Sfx.Play("ui");
    }

    void TakePhoto()
    {
        if (shotReq != null) return;
        shotReq = new ShotRequest { Area = new Rectangle(0, 0, Gfx.LW, Gfx.LH), Name = "photo", Still = () => mode == "photo" };
    }

    void PhotoZoomStep(int d)
    {
        // ZoomLevels runs closest first (5, 4, 3, 2): +1 is closer.
        int i = Array.IndexOf(ZoomLevels, photoZoom is >= 2 and <= 5 ? photoZoom : ViewK);
        i = Math.Clamp(i - d, 0, ZoomLevels.Length - 1);
        if (ZoomLevels[i] != photoZoom) { photoZoom = ZoomLevels[i]; Sfx.Play("ui"); }
    }

    // Keys in photo mode (HandleKeys sends them here): Esc or B leaves, E, Enter or A takes it, F changes the frame,
    // the wheel and the bumpers zoom.
    void PhotoKeys()
    {
        if (shotReq != null) return;
        if (BackPressed()) { ClosePhoto(); return; }
        if (Bind.Pressed("act") || Inp.Pressed(KeyboardKey.Enter)) { TakePhoto(); return; }
        if (Bind.Pressed("alt")) { photoFrame = (photoFrame + 1) % PhotoFrames.Length; Sfx.Play("ui"); }
        if (Inp.PadPressed(GamepadButton.RightTrigger1)) PhotoZoomStep(1);
        if (Inp.PadPressed(GamepadButton.LeftTrigger1)) PhotoZoomStep(-1);
        float wheel = GetMouseWheelMove();
        if (wheel > 0) PhotoZoomStep(1); else if (wheel < 0) PhotoZoomStep(-1);
    }

    void UpdatePhoto(float dt)
    {
        photoFlash = MathF.Max(0, photoFlash - dt);
        if (mode != "photo" || shotReq != null) return;
        float dx = 0, dy = 0;
        if (Bind.Down("left")) dx -= 1;
        if (Bind.Down("right")) dx += 1;
        if (Bind.Down("up")) dy -= 1;
        if (Bind.Down("down")) dy += 1;
        if (Inp.Stick != System.Numerics.Vector2.Zero) (dx, dy) = (Inp.Stick.X, Inp.Stick.Y);
        float sp = 110 * dt;
        // Dragging the world (the mouse, or a finger on a phone) moves the view the other way, pixel for pixel.
        var m = Gfx.Mouse;
        if (Gfx.Down && !Gfx.OverUiPrev && photoDrag is { } last) { dx = 0; dy = 0; photoPanX -= (m.X - last.X) / ViewK; photoPanY -= (m.Y - last.Y) / ViewK; }
        photoDrag = Gfx.Down && !Gfx.OverUiPrev ? m : null;
        photoPanX = Math.Clamp(photoPanX + dx * sp, -PhotoPanMax, PhotoPanMax);
        photoPanY = Math.Clamp(photoPanY + dy * sp, -PhotoPanMax, PhotoPanMax);
    }
    System.Numerics.Vector2? photoDrag;

    // After everything is drawn: a picture asked for last frame is taken now (this frame was drawn without the buttons).
    void TakeShotAfterDraw()
    {
        if (shotReq == null) return;
        if (--shotReq.Wait > 0) return;
        var req = shotReq;
        shotReq = null;
        if (req.Still?.Invoke() == false) return;
        Image img = default;
        try
        {
            Rlgl.DrawRenderBatchActive();
            img = LoadImageFromScreen();
            var s = Gfx.S(req.Area.X, req.Area.Y, req.Area.Width, req.Area.Height);
            var crop = new Rectangle(MathF.Max(0, s.X), MathF.Max(0, s.Y), MathF.Min(s.Width, img.Width - MathF.Max(0, s.X)), MathF.Min(s.Height, img.Height - MathF.Max(0, s.Y)));
            ImageCrop(ref img, crop);
            shotHadBar = mode == "photo" && photoBarDrawn;
            shotHadFlash = mode == "photo" && photoFlashDrawn;
            lastShotSize = (img.Width, img.Height);
            Directory.CreateDirectory(PhotoDir);
            string path = Path.Combine(PhotoDir, $"fesh-{req.Name}-{DateTime.Now:yyyyMMdd-HHmmss}.png");
            for (int n = 2; File.Exists(path); n++) path = Path.Combine(PhotoDir, $"fesh-{req.Name}-{DateTime.Now:yyyyMMdd-HHmmss}-{n}.png");
            if (!ExportImage(img, path)) throw new IOException("not written");
            lastShotPath = path;
            // The phone copies it into the gallery; if that fails it's still in the app's own pictures folder.
            bool shared = Platform.PhotoSaved?.Invoke(path) ?? true;
            if (req.Name == "photo") photoFlash = Calm ? 0.12f : 0.35f;
            Sfx.Play("pickup");
            Toast(!Platform.Touch ? $"Saved to {ShortPath(path)}." : shared ? "Saved to your pictures." : "Saved in Fesh's own pictures folder (the gallery wouldn't take it).", 3);
        }
        catch (Exception)
        {
            Sfx.Play("nope");
            Toast("Couldn't save the picture. Check there's space on the disk.", 3.5f);
        }
        finally { if (img.Width > 0) UnloadImage(img); }   // (the screen copy is freed whatever happened)
    }

    // Under a Fesh-dex card: saves the card itself as a picture (the button is outside it, so it isn't in it).
    void CardImageButton(float x, float y, float w, float h, string name)
    {
        const string label = "Save as image";
        if (shotReq == null && SmallButton(label, x + w / 2 - SmallW(label) / 2, y + h + 8))
        {
            string fishNow = dexFish, extraNow = dexExtra;
            shotReq = new ShotRequest { Area = new Rectangle(x - 4, y - 4, w + 8, h + 8), Name = name.Replace(':', '-'), Still = () => mode == "panel" && dexFish == fishNow && dexExtra == extraNow };
        }
    }

    static string ShortPath(string path)
    {
        string pics = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return pics.Length > 0 && path.StartsWith(pics) ? "Pictures" + path[pics.Length..] : path;
    }

    // Photo mode's screen: the frame (part of the picture), then (not while one is being taken) the bar of buttons.
    void DrawPhotoUi()
    {
        DrawPhotoFrame();
        // The flash after a picture (never in the next one).
        photoFlashDrawn = photoFlash > 0 && shotReq == null;
        if (photoFlashDrawn) Gfx.Rect(0, 0, Gfx.LW, Gfx.LH, Pal.Rgba(255, 255, 255, Math.Clamp(photoFlash / 0.35f, 0, 1) * 0.85f));
        photoBarDrawn = false;
        if (shotReq != null) return;
        photoBarDrawn = true;
        const float h = 64;
        float y = 12;
        string[] labels = { "Take photo", PhotoFrames[photoFrame], photoCaption ? "Caption: on" : "Caption: off", "-", "+", "Done" };
        float w = labels.Sum(l => l.Length <= 1 ? 52 : SmallW(l)) + 10 * (labels.Length - 1) + 32, x = Gfx.LW / 2 - w / 2;
        Gfx.Rect(x, y, w, h, Pal.WithAlpha(NavyStrong, 0.9f), 8);
        float bx = x + 16, by = y + 10;
        foreach (var l in labels)
        {
            float bw = l.Length <= 1 ? 52 : SmallW(l);
            bool primary = l == "Take photo";
            if (Button(l, bx, by, bw, 44, FontKind.Ui700, 20, primary ? Pal.Buoy : Pal.Sand, primary ? White : Pal.Ink, 4))
            {
                switch (l)
                {
                    case "Take photo": TakePhoto(); break;
                    case "-": PhotoZoomStep(-1); break;
                    case "+": PhotoZoomStep(1); break;
                    case "Done": ClosePhoto(); return;
                    default:
                        if (l.StartsWith("Caption")) photoCaption = !photoCaption;
                        else photoFrame = (photoFrame + 1) % PhotoFrames.Length;
                        Sfx.Play("ui");
                        break;
                }
            }
#if DEBUG
            Gfx.Seen["photo:" + (l.StartsWith("Caption") ? "caption" : l == PhotoFrames[photoFrame] ? "frame" : l)] = new Rectangle(bx, by, bw, 44);
#endif
            bx += bw + 10;
        }
        string tip = Platform.Touch ? "Drag to look about" : Bind.Fix("<move> or drag to look about  ·  <alt> frame  ·  <act> take  ·  Esc done");
        Outlined(tip, Gfx.LW / 2 - Gfx.Measure(tip, FontKind.Ui600, 16) / 2, y + h + 8, FontKind.Ui600, 16, Pal.C("#ffffff"));
    }

    string PhotoCaption() => $"{AreaName()}  ·  Day {state.day}, {ClockText(state.clock, 10)}";

    void DrawPhotoFrame()
    {
        float W0 = Gfx.LW, H0 = Gfx.LH;
        string cap = PhotoCaption();
        switch (photoFrame)
        {
            case 1:   // a snapshot: a white border, broader at the bottom, with the caption written on it
            {
                var paper = Pal.C("#f7f2e4");
                Gfx.Rect(0, 0, W0, 28, paper); Gfx.Rect(0, 0, 28, H0, paper); Gfx.Rect(W0 - 28, 0, 28, H0, paper);
                Gfx.Rect(0, H0 - 104, W0, 104, paper);
                Gfx.Rect(28, 28, W0 - 56, 2, Pal.C("rgba(0,0,0,0.18)"));
                if (photoCaption) Gfx.Text(cap, 46, H0 - 76, FontKind.Note, 30, Pal.C("#3a3326"));
                break;
            }
            case 2:   // a postcard: a cream border, "Greetings from" the island, and a stamp
            {
                var paper = Pal.C("#efe2c4");
                Gfx.Rect(0, 0, W0, 22, paper); Gfx.Rect(0, H0 - 22, W0, 22, paper); Gfx.Rect(0, 0, 22, H0, paper); Gfx.Rect(W0 - 22, 0, 22, H0, paper);
                Gfx.Dashed(30, 30, W0 - 60, H0 - 60, 3, 10, Pal.C("#b5523b"));
                string isle = AreaName().ToUpperInvariant();
                Outlined("Greetings from", 58, 52, FontKind.Note, 30, Pal.C("#fff3cf"));
                Outlined(isle, 58, 86, FontKind.Ui700, 58, Pal.C("#ffd76a"));
                // The stamp: a perforated square with a little fish.
                float sx = W0 - 170, sy = 46;
                for (int i = 0; i < 9; i++) { Gfx.Circle(sx + i * 14, sy, 5, Pal.C("#efe2c4")); Gfx.Circle(sx + i * 14, sy + 120, 5, Pal.C("#efe2c4")); }
                Gfx.Rect(sx, sy, 112, 120, Pal.C("#f7f2e4"));
                Gfx.Rect(sx + 8, sy + 8, 96, 104, Pal.C("#2f7fa3"));
                DrawIcon("pond_perch", sx + 26, sy + 30, 60);
                if (photoCaption) Outlined($"Day {state.day}", 58, H0 - 84, FontKind.Note, 26, Pal.C("#fff3cf"));
                break;
            }
            case 3:   // a strip of film: black bars with sprocket holes, and the date stamped in orange
            {
                var black = Pal.C("#111111");
                Gfx.Rect(0, 0, W0, 54, black); Gfx.Rect(0, H0 - 54, W0, 54, black);
                for (float hx = 18; hx < W0; hx += 46) { Gfx.Rect(hx, 16, 24, 20, Pal.C("#e9e2cf"), 3); Gfx.Rect(hx, H0 - 36, 24, 20, Pal.C("#e9e2cf"), 3); }
                if (photoCaption)
                {
                    string stamp = $"'{DateTime.Now:yy  MM  dd}   DAY {state.day}";
                    Gfx.Text(stamp, W0 - 46 - Gfx.Measure(stamp, FontKind.Ui700, 26), H0 - 104, FontKind.Ui700, 26, Pal.C("#ff8a2a"));
                }
                break;
            }
            default:
                if (photoCaption) Outlined(cap, 24, H0 - 52, FontKind.Ui600, 22, Pal.C("#ffffff"));
                break;
        }
    }
}
