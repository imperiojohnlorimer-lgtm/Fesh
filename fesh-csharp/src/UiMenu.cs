using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// The title screen, the save slots, and the Esc menu (Game, Settings and Controls tabs). The title opens the same
// Settings and Controls tabs. Settings are global (Settings.cs); everything else here belongs to a save slot.
partial class Game
{
    string titleView = "main";        // "main", "slots" or "settings"
    string slotsFor = "load";         // the slot screen: "load" (play or delete) or "new" (every slot is full: pick one to replace)
    int newSlot = 1;                  // where a new game goes once the fisher is made
    int slotConfirm;                  // the slot whose delete (or replace) button is waiting for a second click
    int renameSlot;                   // the slot whose name is being typed (0 when none)
    string renameText = "";
    string menuTab = "game";          // "game", "settings" or "controls"
    (string id, int slot)? rebind;    // the control waiting for a key
    string rebindNote = "";
    bool controlsPad;                 // the Controls tab is showing the gamepad layout
    string dragSlider;
    State[] slotInfo;                 // what's in each slot (index 1 to 3), read when the slot list is shown
    bool[] slotBad;                   // a slot whose file exists but can't be read
    int newestSlot;                   // the slot Continue loads (0 if there are no saves)
    readonly Texture2D[] slotTex = new Texture2D[SaveFile.Slots + 1];
    static readonly Color MenuRule = Pal.C("rgba(16,36,58,0.12)");

    /* ---------- Title ---------- */
    void TitleKeys()
    {
        if (renameSlot > 0 && titleView == "slots") { RenameKeys(); return; }
        renameSlot = 0;
        if (titleView == "main")
        {
            // (On a gamepad A clicks whatever the pointer is on instead.)
            if ((Inp.Pressed(KeyboardKey.Enter) || Bind.Pressed("act")) && !Inp.PadClick) TitlePrimary();
            return;
        }
        if (BackPressed()) TitleBack();
    }

    void TitleBack()
    {
        Sfx.Play("ui");
        renameSlot = 0;
        titleView = "main";
        slotConfirm = 0;
        rebind = null;
        EndSliderDrag();
    }

    void OpenSlots(string forWhat)
    {
        mode = "title";
        renameSlot = 0;
        slotsFor = forWhat;
        slotConfirm = 0;
        titleView = "slots";
        slotInfo = null;
    }

    void OpenTitleSettings()
    {
        Sfx.Play("ui");
        menuTab = "settings";
        titleView = "settings";
    }

    // Reads every slot once, with a little portrait of each fisher.
    void RefreshSlots()
    {
        slotInfo = new State[SaveFile.Slots + 1];
        slotBad = new bool[SaveFile.Slots + 1];
        var face = new Pix(16, 18);
        for (int n = 1; n <= SaveFile.Slots; n++)
        {
            slotInfo[n] = SaveFile.Read(n);
            slotBad[n] = slotInfo[n] == null && SaveFile.Exists(n);
            if (slotTex[n].Id != 0) { UnloadTexture(slotTex[n]); slotTex[n] = default; }
            if (slotInfo[n] == null) continue;
            Array.Fill(face.Buf, Color.Blank);
            LookData.DrawPerson(face, slotInfo[n].look, 8, 16, "down", 0, shadow: false);
            slotTex[n] = Gfx.ToTexture(face.Buf, 16, 18, TextureFilter.Point);
        }
        hasSave = SaveFile.Any();
        newestSlot = SaveFile.Newest();
    }

    void DrawTitle()
    {
        DrawRectangleGradientV(0, 0, GetScreenWidth(), GetScreenHeight(), Pal.C("rgba(16,36,58,0.05)"), Pal.C("rgba(16,36,58,0.78)"));
        if (slotInfo == null) RefreshSlots();
        if (titleView == "slots") { DrawSlots(); return; }
        if (titleView == "settings") { DrawMenu(); return; }
        const float logo = 176, tfs = 26, tlh = 35;
        var tag = Gfx.Wrap("A fishing mystery on Saltmere Island. Catch what shouldn't exist, and find out how it got here.", FontKind.Note, tfs, 540);
        int newest = newestSlot;
        float total = 160 + 22 + tag.Count * tlh + 26 + 56 + 14 + 44 + (newest > 0 ? 30 : 0);
        float y = (Gfx.LH - total) / 2 - 10;
        float cx = Gfx.LW / 2;
        Gfx.TextCenter("Fesh", cx, y + 21, FontKind.Ui700, logo, Pal.C("rgba(0,0,0,0.22)"));
        Gfx.TextCenter("Fesh", cx, y + 10, FontKind.Ui700, logo, Pal.Ink);
        Gfx.TextCenter("Fesh", cx, y, FontKind.Ui700, logo, Pal.Sand);
        y += 160 + 22;
        for (int i = 0; i < tag.Count; i++) Gfx.TextCenter(tag[i], cx, y + i * tlh, FontKind.Note, tfs, Pal.Paper);
        y += tag.Count * tlh + 26;
        var row = hasSave ? new[] { "Continue", "Load game", "New game" } : new[] { "New game" };
        float w = row.Sum(BigW) + 13 * (row.Length - 1), x = cx - w / 2;
        foreach (var label in row)
        {
            if (BigButton(label, x, y, label == row[0]))
            {
                if (label == "Continue") TitleContinue();
                else if (label == "Load game") { Sfx.Play("ui"); OpenSlots("load"); }
                else TitleNew();
            }
            x += BigW(label) + 13;
        }
        // A button may have just moved on (to the slot list, the creator or the game) and let go of the slot list it
        // read: draw the rest of this screen next frame.
        if (mode != "title" || titleView != "main" || slotInfo == null) return;
        y += 56;
        if (newest > 0 && slotInfo[newest] is State last)
        {
            Gfx.TextCenter($"Continue: {SlotTitle(last)}, day {last.day} (slot {newest})", cx, y + 8, FontKind.Ui500, 18, Pal.WithAlpha(Pal.Paper, 0.85f));
            y += 30;
        }
        y += 14;
        float sw = SmallW("Settings") + 10 + SmallW("Quit");
        if (SmallButton("Settings", cx - sw / 2, y)) OpenTitleSettings();
        if (SmallButton("Quit", cx - sw / 2 + SmallW("Settings") + 10, y)) quit = true;
        Gfx.TextCenter(Bind.Fix("<move> to walk  ·  <act> to act  ·  <bag> bag  ·  <build> build  ·  <map> map  ·  <dex> Fesh-dex  ·  Esc menu"),
            cx, Gfx.LH - 44, FontKind.Ui500, 18, Pal.WithAlpha(Pal.Paper, 0.85f));
    }

    /* ---------- Save slots ---------- */
    void DrawSlots()
    {
        const float cw = 900, pad = 26, cardH = 140, gap = 12;
        float h = pad + 44 + 8 + 28 + 18 + SaveFile.Slots * (cardH + gap) + 6 + 44 + pad;
        float x = (Gfx.LW - cw) / 2, y = (Gfx.LH - h) / 2;
        Gfx.Box(x, y, cw, h, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Block(x, y, cw, h);
        float cy = y + pad;
        Gfx.Text(slotsFor == "new" ? "Pick a slot for your new game" : "Saved games", x + pad, cy, FontKind.Ui700, 36, Pal.PaperInk);
        cy += 44 + 8;
        Gfx.Text(slotsFor == "new" ? "All three slots are in use. Starting a new game in one erases the game that's there."
            : "Carry on with a game, or start a new one in an empty slot.", x + pad, cy, FontKind.Note, 19, Muted);
        cy += 28 + 18;
        for (int n = 1; n <= SaveFile.Slots; n++)
        {
            DrawSlotCard(n, x + pad, cy, cw - 2 * pad, cardH);
            if (mode != "title" || titleView != "slots" || slotInfo == null) return;
            cy += cardH + gap;
        }
        cy += 6;
        if (SmallButton("Back", x + pad, cy)) TitleBack();
    }

    void DrawSlotCard(int n, float x, float y, float w, float h)
    {
        var s = slotInfo[n];
        bool confirm = slotConfirm == n;
        Gfx.Box(x, y, w, h, s != null ? CardBg : Pal.C("#eadfc0"), Pal.C("rgba(16,36,58,0.35)"), 2, 6);
        // Portrait: the fisher on a patch of sky and sand.
        float px = x + 14, py = y + 14, pw = 92, ph = h - 28;
        Gfx.Rect(px, py, pw, ph, Pal.C("#9fd0e0"), 4);
        Gfx.Rect(px, py + ph - 24, pw, 24, Pal.Sand, 0);
        if (s != null && slotTex[n].Id != 0)
            DrawTexturePro(slotTex[n], new Rectangle(0, 0, 16, 18), Gfx.S(px + pw / 2 - 40, py + ph - 94, 80, 90), Vector2.Zero, 0, Color.White);
        else Gfx.TextCenter(slotBad[n] ? "!" : "?", px + pw / 2, py + ph / 2 - 24, FontKind.Ui700, 40, Pal.C("rgba(16,36,58,0.35)"));
        float tx = px + pw + 18, ty = y + 14;
        Gfx.Text(s != null && s.slotName is { Length: > 0 } ? $"Slot {n}  ·  {s.look.name}" : $"Slot {n}", tx, ty + 4, FontKind.Ui600, 17, Muted);
        float bw = 380, bx = x + w - 14 - bw, by = y + h / 2 - 22;
        if (s == null)
        {
            Gfx.Text(slotBad[n] ? "This save can't be read" : "Empty slot", tx, ty + 26, FontKind.Ui700, 27, Pal.PaperInk);
            if (slotBad[n]) Gfx.Text("It may be damaged. Deleting it frees the slot.", tx, ty + 62, FontKind.Note, 18, Muted);
            if (slotBad[n]) SlotDelete(n, bx, by, confirm);
            else if (SlotButton("New game", bx + bw - SmallW("New game"), by, true)) NewGameIn(n);
            return;
        }
        Gfx.Text(SlotTitle(s), tx, ty + 24, FontKind.Ui700, 27, Pal.PaperInk);
        string where = s.scene == "cave" ? "Frostfang Caverns" : s.scene.StartsWith("house:") ? (s.scene == "house:tomas" ? "Tomas's hut" : "Your shack")
            : Data.Biomes[BiomeAt((int)(s.px / T), (int)((s.py - 1.5f) / T))].Name;
        Gfx.Text($"Day {s.day}, {ClockText(s.clock, 10)}  ·  {where}", tx, ty + 60, FontKind.Ui500, 18, Pal.PaperInk);
        Gfx.Text($"Creatures {s.caught.Count}/5  ·  {s.commons.Values.Sum()} fish caught  ·  {s.coins} coins", tx, ty + 84, FontKind.Ui500, 18, Pal.PaperInk);
        Gfx.Text($"Played {PlayTime(s.playSecs)}{SavedWhen(s.savedAt)}", tx, ty + 108, FontKind.Ui500, 16, Muted);
        if (slotsFor == "new")
        {
            if (confirm)
            {
                Gfx.Text($"Erase {SlotTitle(s)}?", bx, by - 26, FontKind.Ui700, 18, Rust);
                if (SlotButton("Yes, start over", bx, by, true)) NewGameIn(n);
                if (SlotButton("Cancel", bx + SmallW("Yes, start over") + 8, by, false)) slotConfirm = 0;
            }
            else if (SlotButton("Use this slot", bx + bw - SmallW("Use this slot"), by, false)) slotConfirm = n;
            return;
        }
        if (confirm) { SlotDelete(n, bx, by, true); return; }
        if (renameSlot == n)
        {
            // Typing a new name over the old one: Enter keeps it, Esc goes back.
            Gfx.Box(tx - 4, ty + 20, 300, 36, CardBg, Pal.Ink, 2, 4);
            Gfx.Text(renameText + ((int)(time * 2) % 2 == 0 ? "_" : ""), tx + 4, ty + 24, FontKind.Ui700, 25, Pal.PaperInk);
            float sw = SmallW("Save name");
            if (SlotButton("Cancel", bx + bw - SmallW("Cancel"), by, false)) renameSlot = 0;
            if (SlotButton("Save name", bx + bw - SmallW("Cancel") - 8 - sw, by, true)) FinishRename();
            return;
        }
        // Play, Copy (into the first empty slot), Rename and Delete, right to left from the edge.
        float rx = bx + bw;
        bool canCopy = SaveFile.FirstFree() > 0;
        foreach (var label in new[] { "Delete", "Rename", "Copy", "Play" })
        {
            float lw = SmallW(label);
            rx -= lw;
            bool live = (label != "Copy" || canCopy) && renameSlot == 0;
            if (Button(label, rx, by, lw, 44, FontKind.Ui700, 20, label == "Play" ? Pal.Buoy : live ? Pal.Sand : Pal.C("#d8cbb0"), label == "Play" ? White : live ? Pal.Ink : Muted, 4, 3, 6, live))
            {
                switch (label)
                {
                    case "Play": LoadSlot(n); break;
                    case "Copy": CopySlot(n); break;
                    case "Rename": Sfx.Play("ui"); renameSlot = n; renameText = SlotTitle(s); break;
                    default: slotConfirm = n; break;
                }
            }
            rx -= 8;
        }
    }

    void CopySlot(int n)
    {
        int to = SaveFile.FirstFree();
        if (to == 0) return;
        Sfx.Play("ui");
        bool ok = SaveFile.Copy(n, to);
        slotInfo = null;   // read again before the next frame is drawn, not halfway through this one
        Toast(ok ? $"Copied slot {n} to slot {to}." : "Couldn't copy that save.");
    }

    void RenameKeys()
    {
        foreach (int c in Inp.Typed())
            if (c >= 32 && c < 127 && renameText.Length < 24) renameText += (char)c;
        if ((Inp.Pressed(KeyboardKey.Backspace) || IsKeyPressedRepeat(KeyboardKey.Backspace)) && renameText.Length > 0) renameText = renameText[..^1];
        if (Inp.Pressed(KeyboardKey.Enter)) FinishRename();
        else if (BackPressed()) renameSlot = 0;
    }

    // A slot's own name (say, "Robin, before the storm"), shown instead of the fisher's. Clearing it, or typing the
    // fisher's name, goes back to the fisher's. The save keeps its date, so Continue still picks the same slot.
    static string SlotTitle(State s) => s.slotName is { Length: > 0 } ? s.slotName : s.look.name;

    void FinishRename()
    {
        var s = SaveFile.Read(renameSlot);
        string name = renameText.Trim();
        if (s != null && name != SlotTitle(s))
        {
            s.slotName = name == s.look.name ? "" : name;
            Sfx.Play("craft");
            if (!SaveFile.Rewrite(renameSlot, s)) Toast("Couldn't rename that save.");
        }
        renameSlot = 0;
        slotInfo = null;
    }

    void SlotDelete(int n, float bx, float by, bool confirm)
    {
        if (!confirm)
        {
            if (SlotButton("Delete", bx + 380 - SmallW("Delete"), by, false)) slotConfirm = n;
            return;
        }
        Gfx.Text("Delete this save for good?", bx, by - 26, FontKind.Ui700, 18, Rust);
        if (SlotButton("Yes, delete", bx, by, true))
        {
            SaveFile.Clear(n);
            Sfx.Play("ui");
            slotConfirm = 0;
            slotInfo = null;
            hasSave = SaveFile.Any();
            if (!hasSave) titleView = "main";
        }
        if (SlotButton("Cancel", bx + SmallW("Yes, delete") + 8, by, false)) slotConfirm = 0;
    }

    static bool SlotButton(string label, float x, float y, bool primary) =>
        Button(label, x, y, SmallW(label), 44, FontKind.Ui700, 20, primary ? Pal.Buoy : Pal.Sand, primary ? White : Pal.Ink, 4);

    static string PlayTime(double secs)
    {
        int m = (int)(secs / 60);
        return m < 60 ? $"{Math.Max(1, m)} min" : $"{m / 60}h {m % 60:00}m";
    }

    static string SavedWhen(string iso)
    {
        if (!DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var t)) return "";
        t = t.ToLocalTime();
        string clock = Settings.Data.clock24 ? t.ToString("HH:mm") : t.ToString("h:mm tt");
        return t.Date == DateTime.Today ? $"  ·  Saved today, {clock}" : $"  ·  Saved {t:MMM d}, {clock}";
    }

    /* ---------- The Esc menu ---------- */
    void DrawMenu()
    {
        bool title = mode == "title";
        if (!title) Backdrop();
        const float cw = 960, ch = 610, pad = 28;
        float x = (Gfx.LW - cw) / 2, y = (Gfx.LH - ch) / 2;
        Gfx.Box(x, y, cw, ch, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Block(x, y, cw, ch);
        float cy = y + pad;
        string head = title ? "Settings" : "Paused";
        Gfx.Text(head, x + pad, cy, FontKind.Ui700, 40, Pal.PaperInk);
        float tx = x + pad + Gfx.Measure(head, FontKind.Ui700, 40) + 28;
        var tabs = title ? new[] { ("settings", "Settings"), ("controls", "Controls") } : new[] { ("game", "Game"), ("settings", "Settings"), ("controls", "Controls") };
        foreach (var (id, label) in tabs)
        {
            float w = SmallW(label);
            if (Button(label, tx, cy, w, 44, FontKind.Ui700, 20, menuTab == id ? Pal.Lantern : Pal.Sand, Pal.Ink, 4)) { menuTab = id; rebind = null; Sfx.Play("ui"); }
            tx += w + 10;
        }
        string back = title ? "Back" : "Resume";
        if (SmallButton(back, x + cw - pad - SmallW(back), cy)) { if (title) TitleBack(); else ClosePause(); }
        cy += 44 + 22;
        Gfx.Rect(x + pad, cy - 10, cw - 2 * pad, 2, MenuRule);
        float cx = x + pad, inner = cw - 2 * pad, bottom = y + ch - pad;
        switch (menuTab)
        {
            case "game": DrawGameTab(cx, cy, inner, bottom); break;
            case "settings": DrawSettingsTab(cx, cy, inner); break;
            default: DrawControlsTab(cx, cy, inner); break;
        }
        if (!Gfx.Down) EndSliderDrag();
    }

    void DrawGameTab(float x, float y, float w, float bottom)
    {
        float bx = x, by = y + 4;
        foreach (var label in new[] { "Resume", "Save game", "Change your look", "Quit to title", "Quit game" })
        {
            // Changing your look waits until you're not in the middle of fishing (the menu can open mid-cast).
            bool live = label != "Change your look" || pausedFrom == "play";
            if (Button(label, bx, by, 300, 52, FontKind.Ui700, 22, label == "Resume" ? Pal.Buoy : live ? Pal.Sand : Pal.C("#d8cbb0"), label == "Resume" ? White : live ? Pal.Ink : Muted, 4, 3, 6, live))
            {
                switch (label)
                {
                    case "Resume": ClosePause(); break;
                    case "Save game":
                        Sfx.Play("ui");
                        Toast(Save() ? $"Saved to slot {SaveFile.Slot}." : "Couldn't save the game! Check there's space on the disk.");
                        break;
                    case "Change your look": OpenCreator("edit"); break;
                    case "Quit to title": Sfx.Play("ui"); QuitToTitle(); break;
                    default: Save(); quit = true; break;
                }
                return;
            }
            by += 52 + 12;
        }

        // What's going on in this game: the slot, the time and weather, the moon.
        float ix = x + 340, iw = w - 340, iy = y + 4;
        Gfx.Box(ix, iy, iw, bottom - iy, CardBg, Pal.C("rgba(16,36,58,0.35)"), 2, 6);
        ix += 22; iw -= 44; iy += 18;
        Gfx.Text($"Slot {SaveFile.Slot}  ·  {SlotTitle(state)}", ix, iy, FontKind.Ui600, 19, Muted);
        iy += 32;
        Gfx.Text($"Day {state.day}, {ClockText(state.clock, 10)}", ix, iy, FontKind.Ui700, 32, Pal.PaperInk);
        iy += 46;
        int toFull = (4 - MoonPhase + 8) % 8;
        string moon = Night && MoonPhase == 4 ? "The moon is full tonight." : toFull == 0 ? "Full moon tonight." : toFull == 1 ? "Full moon tomorrow night." : $"Full moon in {toFull} days.";
        string now = state.weather switch { "rain" => "Raining.", "storm" => "A storm is raging. The bridges are closed.", _ => "Fair weather." };
        string dark = Night ? $"Night. Morning comes at {HourText(DawnMin)}." : $"Day. Night falls at {HourText(DuskMin)}.";
        string pace = Settings.Data.dayLength > 0 ? $"A whole day takes {Settings.Data.dayLength} minutes of play." : "The clock is stopped (Settings). Rest to move it on.";
        string tomorrow = KnowTomorrow ? $"Tomorrow: {DescribeDay(state.tomorrow)}." : "Ask Tomas or Pip what tomorrow's weather will do.";
        foreach (var line in new[] { dark, moon, now + (Forecast() is string f ? " " + f : " No change expected today."), tomorrow, pace, $"Time played: {PlayTime(state.playSecs)}." })
        {
            var wrapped = Gfx.Wrap(line, FontKind.Ui500, 20, iw);
            Lines(wrapped, ix, iy, 27, FontKind.Ui500, 20, Pal.PaperInk);
            iy += wrapped.Count * 27 + 10;
        }
        var note = Gfx.Wrap("Your game saves itself every few seconds and each morning. Quitting saves it too.", FontKind.Note, 18, iw);
        Lines(note, ix, bottom - 18 - note.Count * 25, 25, FontKind.Note, 18, Muted);
    }

    void DrawSettingsTab(float x, float y, float w)
    {
        const float rowH = 48, labelW = 250;
        var d = Settings.Data;
        float cx = x + labelW;
        void Label(string s) => Gfx.Text(s, x, y + 9, FontKind.Ui600, 21, Pal.PaperInk);
        bool changed = false;

        Label("Music volume");
        float mv = Slider("music", d.music, cx, y + 2, 360);
        if (mv != d.music) { d.music = mv; Settings.ApplyAudio(); }
        Gfx.Text($"{d.music * 100:0}%", cx + 380, y + 9, FontKind.Ui600, 20, Muted);
        y += rowH;
        Label("Sound volume");
        float sv = Slider("sound", d.sound, cx, y + 2, 360);
        if (sv != d.sound) { d.sound = sv; Settings.ApplyAudio(); }
        Gfx.Text($"{d.sound * 100:0}%", cx + 380, y + 9, FontKind.Ui600, 20, Muted);
        y += rowH;

        int Pick(string label, string[] options, int sel)
        {
            Label(label);
            int c = Choices(options, sel, cx, y);
            y += rowH;
            if (c >= 0 && c != sel) { changed = true; Sfx.Play("ui"); return c; }
            return sel;
        }
        d.musicOn = Pick("Music", new[] { "On", "Off" }, d.musicOn ? 0 : 1) == 0;
        d.muted = Pick($"All sound ({Bind.Name("mute")})", new[] { "On", "Off" }, d.muted ? 1 : 0) == 1;
        bool full = IsWindowState(ConfigFlags.BorderlessWindowMode);
        if ((Pick($"Fullscreen ({Bind.Name("fullscreen")})", new[] { "On", "Off" }, full ? 0 : 1) == 0) != full) ToggleFullscreen();
        d.shake = Pick("Screen shake", new[] { "On", "Off" }, d.shake ? 0 : 1) == 0;
        d.pauseUnfocused = Pick("Pause in the background", new[] { "On", "Off" }, d.pauseUnfocused ? 0 : 1) == 0;
        var lengths = Settings.DayLengths;
        int li = Array.IndexOf(lengths, d.dayLength);
        d.dayLength = lengths[Pick("Length of a day", lengths.Select(m => m > 0 ? $"{m} min" : "Stopped").ToArray(), li < 0 ? 1 : li)];
        d.clock24 = Pick("Clock", new[] { "12-hour", "24-hour" }, d.clock24 ? 1 : 0) == 1;
        if (changed) { Settings.ApplyAudio(); Settings.Save(); }
        var note = d.dayLength > 0 ? $"A whole day and night on the island takes {d.dayLength} minutes of play. The clock stops while a menu, a panel or a conversation is open."
            : "The clock is stopped: it's only ever as late as resting makes it.";
        Lines(Gfx.Wrap(note, FontKind.Note, 17, w), x, y + 2, 23, FontKind.Note, 17, Muted);
    }

    // Letting go of a slider (or leaving the menu while holding one) saves the new volume.
    void EndSliderDrag()
    {
        if (dragSlider == null) return;
        dragSlider = null;
        Settings.Save();
    }

    // A volume slider: click or drag along it.
    float Slider(string id, float v, float x, float y, float w)
    {
        const float h = 34;
        bool grabbed = Gfx.Click(x - 12, y, w + 24, h);
        if (grabbed) dragSlider = id;
        if (dragSlider == id && (grabbed || Gfx.Down)) v = MathF.Round(Math.Clamp((Gfx.Mouse.X - x) / w, 0, 1) * 100) / 100;
        float my = y + h / 2;
        Gfx.Rect(x, my - 5, w, 10, Pal.C("rgba(16,36,58,0.22)"), 5);
        if (v > 0) Gfx.Rect(x, my - 5, Math.Max(10, w * v), 10, Pal.C("#3f7d35"), 5);
        Gfx.Circle(x + w * v, my, 13, Pal.Ink);
        Gfx.Circle(x + w * v, my, 10, Gfx.Hover(x - 12, y, w + 24, h) || dragSlider == id ? Lighten(Pal.Sand, 0.3f) : Pal.Sand);
        return v;
    }

    // Keyboard (rebindable) or gamepad (a fixed layout) controls.
    void DrawControlsTab(float x, float y, float w)
    {
        float sx = x;
        foreach (var (pad, label) in new[] { (false, "Keyboard"), (true, "Gamepad") })
        {
            float lw = SmallW(label);
            if (Button(label, sx, y - 6, lw, 36, FontKind.Ui700, 18, controlsPad == pad ? Pal.Lantern : Pal.Sand, Pal.Ink, 3, 2, 5)) { controlsPad = pad; rebind = null; }
            sx += lw + 8;
        }
        y += 42;
        if (controlsPad) { DrawPadControls(x, y, w); return; }
        const float rowH = 38, keyW = 118, gap = 30;
        float colW = (w - gap) / 2;
        int rows = (Bind.Acts.Length + 1) / 2;
        bool clicked = false;
        for (int i = 0; i < Bind.Acts.Length; i++)
        {
            var a = Bind.Acts[i];
            float rx = x + (i / rows) * (colW + gap), ry = y + (i % rows) * rowH;
            if (i % rows % 2 == 0) Gfx.Rect(rx - 8, ry - 3, colW + 16, rowH, Pal.C("rgba(16,36,58,0.05)"), 4);
            Gfx.Text(a.Name, rx, ry + 7, FontKind.Ui500, 19, Pal.PaperInk);
            for (int k = 0; k < 2; k++)
            {
                float kx = rx + colW - (2 - k) * (keyW + 8) + 8;
                bool waiting = rebind.HasValue && rebind.Value.id == a.Id && rebind.Value.slot == k;
                var key = Bind.Key(a.Id, k);
                string label = waiting ? "Press a key" : key == KeyboardKey.Null ? "-" : Bind.KeyName(key);
                if (Button(label, kx, ry, keyW, rowH - 6, FontKind.Ui700, waiting ? 15 : 18, waiting ? Pal.Lantern : key == KeyboardKey.Null ? Pal.C("#eadfc0") : Pal.Sand, Pal.Ink, 2, 2, 4))
                {
                    clicked = true;
                    rebind = waiting ? null : (a.Id, k);
                    rebindNote = "";
                    Sfx.Play("ui");
                }
            }
        }
        if (Gfx.Pressed && !clicked && rebind != null) rebind = null;
        float fy = y + rows * rowH + 12;
        string help = rebind != null ? (rebindNote != "" ? rebindNote : "Press the new key. Esc cancels; Backspace leaves it empty.")
            : "Click a key to change it. A key only does one thing: taking it from another action swaps the two.";
        Gfx.Text(help, x, fy, FontKind.Note, 18, rebind != null ? Rust : Muted);
        fy += 32;
        float resetW = SmallW("Reset to defaults");
        Lines(Gfx.Wrap("Always the same: Esc opens this menu and goes back, Enter acts too, 1 to 9 pick a piece to build, and a click acts on whatever you point at.",
            FontKind.Ui500, 17, w - resetW - 30), x, fy, 23, FontKind.Ui500, 17, Muted);
        if (SmallButton("Reset to defaults", x + w - resetW, fy))
        {
            Bind.Reset();
            Settings.Save();
            rebind = null;
            Sfx.Play("ui");
        }
    }

    // The gamepad layout, which is fixed. In menus the left stick moves a pointer and A clicks.
    void DrawPadControls(float x, float y, float w)
    {
        string connected = Inp.PadIndex >= 0 ? $"Connected: {GetGamepadName_(Inp.PadIndex)}" : "No gamepad connected. Plug one in any time.";
        Gfx.Text(connected, x, y, FontKind.Ui600, 19, Inp.PadIndex >= 0 ? Pal.C("#3f7d35") : Muted);
        y += 36;
        var rows = new (string pad, string what)[]
        {
            ("Left stick / D-pad", "Walk, aim the spear, the chest arrows"), ("A", "Talk, fish, use; hold to cast and reel"),
            ("B", "Back, reel in, close"), ("X", "Cook, throw chum; take down when building"), ("Y", "Bag"),
            ("LB / RB", "Tackle box / map; pick a piece when building"), ("LT", "Build"), ("RT", "Ride your mount"),
            ("Back", "Fesh-dex"), ("L3 / R3", "Case board / spearfish"), ("Start", "This menu"),
            ("In menus", "The left stick moves a pointer and A clicks")
        };
        float colW = (w - 30) / 2;
        int half = (rows.Length + 1) / 2;
        for (int i = 0; i < rows.Length; i++)
        {
            float rx = x + (i / half) * (colW + 30), ry = y + (i % half) * 38;
            if (i % half % 2 == 0) Gfx.Rect(rx - 8, ry - 3, colW + 16, 38, Pal.C("rgba(16,36,58,0.05)"), 4);
            Gfx.Text(rows[i].pad, rx, ry + 6, FontKind.Ui700, 19, Pal.PaperInk);
            Gfx.Text(rows[i].what, rx + 170, ry + 7, FontKind.Ui500, 17, Muted);
        }
        Lines(Gfx.Wrap("Prompts and tips name the gamepad's buttons while you're using it, and the keyboard's again as soon as you type or move the mouse.",
            FontKind.Note, 18, w), x, y + half * 38 + 14, 24, FontKind.Note, 18, Muted);
    }

    // Raylib-cs hands the name back as a C string.
    static unsafe string GetGamepadName_(int pad) => new string((sbyte*)GetGamepadName(pad));

    // Waiting for a key in the Controls tab. Nothing else reacts to keys meanwhile.
    void CaptureRebind()
    {
        var (id, slot) = rebind.Value;
        if (BackPressed()) { rebind = null; return; }
        if (Inp.Pressed(KeyboardKey.Backspace)) { Bind.Set(id, slot, KeyboardKey.Null); Settings.Save(); rebind = null; return; }
        if (Inp.FirstPressed() is not KeyboardKey k) return;
        if (!Bind.IsBindable(k)) { rebindNote = $"{Bind.KeyName(k)} can't be used: Esc, Enter and the number keys are kept for menus and building."; return; }
        Bind.Set(id, slot, k);
        Settings.Save();
        Sfx.Play("ui");
        rebind = null;
    }
}
