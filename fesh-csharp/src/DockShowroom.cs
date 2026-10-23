using Raylib_cs;

namespace Fesh;

// The dock showroom (1.46): a third page of the Tournament Dock's notice board showing who is on the dock: their look, the
// title they wear, a favourite fish and two numbers from their Ledger (fishing mastery and kinds of fish caught). It travels
// as four small numbers (DockNet.cs, "dprofile"), never text: a title and a favourite are indexes into fixed lists, so nobody
// can send a word of their own. What visitors show is what they say about themselves (it isn't checked, and nothing here
// gives you anything), and a favourite you haven't caught yourself shows as a "?" so it can't spoil a fish for you. Your own
// card has arrows to pick your favourite from the fish you hold a record for.
partial class Game
{
    static string[] publicTitles, showcaseFish;
    // Every title an achievement can give, in the Ledger's order; a showroom title is a 1-based index into it (0 none).
    static string[] PublicTitles => publicTitles ??= Achievements.Where(a => a.Title != null).Select(a => a.Title).Distinct().ToArray();
    // Ordinary fish a favourite can be (never a legend), a 1-based index into it (0 none).
    static string[] ShowcaseFish => showcaseFish ??= Data.AllCommon.Where(f => !f.Legend && FishArt.Looks.ContainsKey(f.Id)).Select(f => f.Id).ToArray();

    sealed record ShowEntry(string Name, Look Look, string Title, string Fav, int Mastery, int Kinds, bool Mine);

    // The real adventure while a match or the dock has the sandbox in place.
    State RealState => MatchActive && match.Real != null ? match.Real : state;

    static string WornTitleOf(State s) => s.title is { Length: > 0 } t && Achievements.Any(a => a.Title == t && AchEarned(s, a)) ? t : null;
    static int MasteryOf(State s) { int xp = Math.Max(0, s.xp - XpFor(10)), l = 0; while (l < MasteryMax && xp >= MasteryTotal(l + 1)) l++; return l; }
    static int KindsOf(State s) => s.commons.Count(kv => kv.Value > 0 && Data.FishById.ContainsKey(kv.Key));

    int[] MyProfile()
    {
        var s = RealState;
        int title = WornTitleOf(s) is string t ? Array.IndexOf(PublicTitles, t) + 1 : 0;
        int fav = s.favFish != null ? Array.IndexOf(ShowcaseFish, s.favFish) + 1 : 0;
        // And what they wear, as 1-based indexes into the catalogue (0: the creator's), so visitors see the same clothes.
        int Wear(string id) => Wardrobe.Find(id) is Cosmetic c ? Array.IndexOf(Wardrobe.Catalog, c) + 1 : 0;
        return new[] { Math.Max(0, title), Math.Max(0, fav), MasteryOf(s), Math.Min(999, KindsOf(s)), Wear(s.look.shirtId), Wear(s.look.pantsId), Wear(s.look.hairId), Wear(s.look.hatId) };
    }

    string TitleAt(int i) => i >= 1 && i <= PublicTitles.Length ? PublicTitles[i - 1] : null;
    string FavAt(int i) => i >= 1 && i <= ShowcaseFish.Length ? ShowcaseFish[i - 1] : null;

    bool dockProfileSent;
    // Once you're in: tell the dock your card; again whenever you change your favourite.
    void UpdateDockProfile()
    {
        if (dockClient == null || dockClient.State != "in") { dockProfileSent = false; return; }
        if (dockProfileSent) return;
        dockProfileSent = true;
        dockClient.SendProfile(MyProfile());
    }

    List<ShowEntry> ShowEntries()
    {
        var list = new List<ShowEntry>();
        var real = RealState;
        if (dockClient?.Me == null || dockClient.State != "in")
        {
            list.Add(new ShowEntry(real.look.name, real.look, WornTitleOf(real), real.favFish, MasteryOf(real), KindsOf(real), true));
            return list;
        }
        foreach (var p in dockClient.Roster.OrderBy(p => p.Id != dockClient.MyId).ThenBy(p => p.Id))
        {
            if (p.Id == dockClient.MyId) { list.Add(new ShowEntry(p.Name, real.look, WornTitleOf(real), real.favFish, MasteryOf(real), KindsOf(real), true)); continue; }
            var pr = dockClient.Profiles.GetValueOrDefault(p.Id);
            list.Add(new ShowEntry(p.Name, RemoteLook(p), pr == null ? null : TitleAt(pr[0]), pr == null ? null : FavAt(pr[1]), pr?[2] ?? 0, pr?[3] ?? 0, false));
        }
        return list;
    }

    // Changing your favourite: the next (or last) fish you hold a record for.
    void CycleFavourite(int dir)
    {
        var real = RealState;
        var mine = ShowcaseFish.Where(id => real.records.ContainsKey(id)).ToList();
        if (mine.Count == 0) return;
        int at = real.favFish == null ? -1 : mine.IndexOf(real.favFish);
        real.favFish = mine[((at + dir) % mine.Count + mine.Count) % mine.Count];
        dockProfileSent = false;
        Sfx.Play("ui");
    }

    void DrawShowroom(float x, float top, float w, float ch)
    {
        Gfx.Text("Who's on the dock: their title, favourite fish and two numbers from their Ledger. Visitors tell us their own; it isn't checked.", x, top - 4, FontKind.Note, 17, Muted);
        var entries = ShowEntries();
        const int cols = 2; const float gap = 14, rh = 104;
        float cw = (w - gap) / cols, ty = top + 30;
        for (int i = 0; i < entries.Count && i < 8; i++)
        {
            var e = entries[i];
            float cx = x + i % cols * (cw + gap), cy = ty + i / cols * (rh + gap);
            Gfx.Box(cx, cy, cw, rh, e.Mine ? Pal.C("#fff3cf") : CardBg, e.Mine ? Pal.C("#d9a640") : Pal.C("#c9b48f"), 2, 6);
            var look = e.Look;
            PixelScene(cx + 10, cy + 12, 20, 24, 3, p => LookData.DrawPerson(p, look, 10, 22, "down", 0, shadow: false, idle: false), 100 + i);
            Gfx.Text(Gfx.Ellipsize(e.Name + (e.Mine ? " (you)" : ""), FontKind.Ui700, 19, cw - 96 - 150), cx + 84, cy + 10, FontKind.Ui700, 19, Pal.PaperInk);
            Gfx.Text(e.Title != null ? Gfx.Ellipsize(e.Title, FontKind.Ui600, 16, cw - 96 - 150) : "No title", cx + 84, cy + 38, FontKind.Ui600, 16, e.Title != null ? Pal.C("#9a6a1a") : Muted);
            Gfx.Text($"Mastery {e.Mastery} · {e.Kinds} kinds", cx + 84, cy + 66, FontKind.Ui500, 15, Muted);
            // The favourite fish: its picture if you've caught one too, else a question mark.
            float fx = cx + cw - 142;
            Gfx.Box(fx, cy + 12, 56, 56, Pal.C("#f4ecd6"), Pal.C("#b9a27a"), 2, 4);
            bool known = e.Fav != null && (RealState.commons.GetValueOrDefault(e.Fav) > 0 || e.Mine);
            if (e.Fav != null && known) DrawIcon(e.Fav, fx + 4, cy + 16, 48);
            else Gfx.TextCenter(e.Fav != null ? "?" : "-", fx + 28, cy + 22, FontKind.Ui700, 28, Muted);
            string favName = e.Fav == null ? "No favourite" : known ? Data.FishById[e.Fav].Name.Split(" (")[0] : "A fish you haven't caught";
            Gfx.Text(Gfx.Ellipsize("Favourite", FontKind.Ui700, 13, 80), fx + 64, cy + 14, FontKind.Ui700, 13, Muted);
            var lines = Gfx.Wrap(favName, FontKind.Ui500, 14, 76);
            Lines(lines.Take(3).ToList(), fx + 64, cy + 32, 16, FontKind.Ui500, 14, Pal.PaperInk);
            if (e.Mine)
            {
                if (Button("<", fx, cy + 74, 36, 24, FontKind.Ui700, 14, Pal.Sand, Pal.Ink, 2, 2, 4)) CycleFavourite(-1);
                if (Button(">", fx + 42, cy + 74, 36, 24, FontKind.Ui700, 14, Pal.Sand, Pal.Ink, 2, 2, 4)) CycleFavourite(1);
#if DEBUG
                Gfx.Seen["show:prev"] = new Rectangle(fx, cy + 74, 36, 24);
                Gfx.Seen["show:next"] = new Rectangle(fx + 42, cy + 74, 36, 24);
#endif
            }
#if DEBUG
            Gfx.Seen["show:card:" + i] = new Rectangle(cx, cy, cw, rh);
#endif
            lastShowroomBottom = MathF.Max(lastShowroomBottom, cy + rh);
        }
    }
    internal float lastShowroomBottom;
}

partial class Game
{
    // A visitor's look on the dock: the creator's indexes they sent, plus the wardrobe things they say they wear (their showroom
    // card carries them as 1-based indexes into the catalogue; one that isn't a thing for that slot is ignored, and ownership isn't
    // checked: it only changes how they look to you).
    Look RemoteLook(DockPlayer p) => ApplyProfileLook(p.ToLook(), dockClient?.Profiles.GetValueOrDefault(p.Id));

    static Look ApplyProfileLook(Look look, int[] pr)
    {
        if (pr is not { Length: 8 }) return look;
        Cosmetic At(int i, string slot) => i >= 1 && i <= Wardrobe.Catalog.Length && Wardrobe.Catalog[i - 1].Slot == slot ? Wardrobe.Catalog[i - 1] : null;
        look.shirtId = At(pr[4], "shirt")?.Id; look.pantsId = At(pr[5], "pants")?.Id; look.hairId = At(pr[6], "hair")?.Id;
        if (At(pr[7], "hat") is Cosmetic hat) { look.hat = hat.Style; look.hatId = hat.Id; }
        return look;
    }
}
