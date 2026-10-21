using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

// The celebration ladder (1.44): a moment gets a size that fits it. An ordinary catch is a toast, as before. A first
// catch of a species, a first trophy, a real new record, the day's biggest fish, an album chapter or a Sea school badge
// is a banner: a ribbon that slides in at the top, never takes a key and goes by itself. A fishing level, a whole Fish
// log page and the diploma are cards you close. Each has its own sting (Sfx.cs), played when it shows.
//
// Everything is queued with its words fixed at the moment it happened, and shown in turn: a banner while you're out in
// the world, a card only once nothing else is going on (no fish on the line or just landed, no fight, no story event,
// no conversation, no toast still up), so it can't land in the middle of something. Nothing is saved: the reward was
// paid when it happened, a quit only loses the ceremony.
sealed class Celeb
{
    public string Kind;               // "fish", "album", "badge", "level", "page", "diploma"
    public bool Card;                 // a card you close; otherwise a banner
    public string Tag, Title, Sub, Note, Sting, Button = "Wonderful!";
    public string Fish, Icon, Badge;  // the picture: a fish, an item icon, or a Sea school badge (with BadgeTier)
    public int BadgeTier, Level0, Level1;
    public List<string> Body = new(), Stats = new();
}

partial class Game
{
    // Test runs keep the old toasts (AutoTestStart), so the old scripts' messages and modes are as they were;
    // CelebrateScript turns it off.
    internal static bool OldCelebrate;

    readonly List<Celeb> celebQueue = new();
    Celeb banner, celebCard;
    float bannerT, cotdCool;
    float lastToastBottom;   // where the toast ended last frame: the banner goes under it
    float lastBannerBottom, lastCelebBottom;

    const float BannerIn = 0.3f, BannerOut = 0.4f;
    static float BannerHold(Celeb c) => c.Note != null ? 4.6f : 3.6f;
    static float BannerLen(Celeb c) => BannerIn + BannerHold(c) + BannerOut;

    // Where a banner can be out: in the world, fishing included. It waits (and doesn't count down) behind a panel, a
    // conversation, a card or the pause menu.
    bool BannerLive => mode is "play" or "build" or "waiting" or "charging" or "casting" or "bite" or "reeling" or "chest" or "spear" or "drill";

    // A card stops everything, so it waits until nothing else is going on.
    bool CardMayOpen => mode == "play" && heldT <= 0 && landed == null && fish == null && reel == null && toastTimer <= 0
        && !guardianDue && boss == null && guardian == null && eclipse == null && race == null && !raceArmed && tremor == null && evac == null
        && !VolcanoControlled && !MatchActive && banner == null;

    void Celebrate(Celeb c, Action old)
    {
        if (MatchActive) return;
        if (OldCelebrate) { old?.Invoke(); return; }
        // A same-fish banner still waiting is replaced by the newer news; levels gained before the card shows join it.
        if (c.Kind == "fish") celebQueue.RemoveAll(q => q.Kind == "fish" && q.Fish == c.Fish);
        if (c.Kind == "level" && celebQueue.FirstOrDefault(q => q.Kind == "level") is Celeb lv) { c.Level0 = lv.Level0; celebQueue.Remove(lv); FillLevelCard(c); }
        celebQueue.Add(c);
    }

    void ClearCelebrations() { celebQueue.Clear(); banner = null; celebCard = null; bannerT = 0; cotdCool = 0; }

    void UpdateCelebrations(float dt)
    {
        if (MatchActive) return;
        if (banner != null)
        {
            if (BannerLive) bannerT += dt;
            if (bannerT >= BannerLen(banner)) banner = null;
            return;
        }
        if (celebQueue.Count == 0 || mode == "celebrate") return;
        var next = celebQueue[0];
        if (next.Card ? !CardMayOpen : !BannerLive) return;
        celebQueue.RemoveAt(0);
        if (next.Sting != null) Sfx.Play(next.Sting);
        if (next.Card)
        {
            celebCard = next;
            mode = "celebrate";
            catchOpenedAt = GetTime();
            target = null;
            SetPrompt("");
        }
        else { banner = next; bannerT = 0; }
    }

    void CloseCelebration()
    {
        if (mode != "celebrate" || GetTime() - catchOpenedAt < 0.6) return;
        bool ledger = celebCard?.Kind == "ledger";
        celebCard = null;
        mode = "play";
        target = null;   // the key that closed it doesn't also cast at the spot in front of you
        if (ledger) OpenLedger();   // "Let's see": the Ledger's first card opens it (Ledger.cs)
    }

    /* ---------- The moments ---------- */

    // What one AddCatch found, for the landing's banner (a double catch adds a second).
    sealed class CatchNews { public bool First, Trophy, FirstTrophy, Record, Cotd; public float Kg, M, RecordWas; }
    CatchNews catchNews;

    // After a rod catch: one banner at most, for the best thing that happened. True when there is one (the caller then
    // skips the plain catch toast and its sound: the banner has its own).
    bool CelebrateCatch(CommonFish f, CatchNews n, int count)
    {
        if (n == null || f.Legend || MatchActive) return false;   // (a match never lands here: LandCatch hands it to MatchLand)
        bool bigRecord = n.Record && !n.First && n.RecordWas > 0 && n.Kg >= n.RecordWas * 1.1f;
        bool cotd = n.Cotd && n.M >= 1.25f && time >= cotdCool;
        if (!n.First && !n.FirstTrophy && !bigRecord && !cotd) return false;
        if (OldCelebrate) return false;
        var c = new Celeb { Kind = "fish", Fish = f.Id, Title = f.Name };
        var chips = new List<string>();
        if (n.First)
        {
            c.Tag = f.Rare ? "New species - a rare one!" : "New species!";
            c.Sting = "newfish";
            int kinds = state.commons.Count(kv => kv.Value > 0 && Data.FishById.ContainsKey(kv.Key));
            c.Sub = $"{Kg(n.Kg)} · fish no. {kinds} in your Fesh-dex";
            c.Note = "Read about the real fish in your Fesh-dex (<dex>).";
            if (n.Trophy) chips.Add("a trophy too");
        }
        else if (n.FirstTrophy)
        {
            c.Tag = "Trophy!";
            c.Sting = "trophy";
            c.Sub = $"{Kg(n.Kg)} · {n.M:0.0}x the usual size";
            if (n.Record) chips.Add("new record");
        }
        else if (bigRecord)
        {
            c.Tag = "New record!";
            c.Sting = "record";
            c.Sub = $"{Kg(n.Kg)}, up from {Kg(n.RecordWas)}";
        }
        else
        {
            c.Tag = "Biggest of the day so far";
            c.Sting = "badge";
            c.Sub = $"{Kg(n.Kg)} · {n.M:0.0}x the usual size";
            cotdCool = time + 120;   // not every bigger fish: at most once every couple of minutes
        }
        if (cotd) cotdCool = time + 120;
        if (chips.Count > 0) c.Sub += " · " + string.Join(" · ", chips);
        if (count > 1 && !n.First) c.Sub += $" · {count} caught";
        Celebrate(c, null);
        return true;
    }

    // Keeps the day's biggest (size against the usual, so heavy kinds don't always win). True when this one beat an
    // earlier one today (the first fish of the day just sets it, quietly).
    bool NoteCatchOfDay(string id, float m, float kg)
    {
        bool had = state.cotdDay == state.day && state.cotdId != null;
        if (had && m <= state.cotdM) return false;
        state.cotdDay = state.day; state.cotdId = id; state.cotdM = m; state.cotdKg = kg;
        return had;
    }

    void CelebrateLevel(int before, int after, Action old)
    {
        var c = new Celeb { Kind = "level", Card = true, Level0 = before, Level1 = after, Sting = "levelup", Button = "Back to fishing" };
        FillLevelCard(c);
        Celebrate(c, old);
    }

    void FillLevelCard(Celeb c)
    {
        int l = c.Level1;
        c.Tag = "Level up!";
        c.Title = $"Fishing level {l}";
        c.Body.Clear();
        for (int k = c.Level0 + 1; k <= l; k++) c.Body.Add($"New perk: {PerkText[k]}.");
        c.Stats.Clear();
        c.Stats.Add($"{state.xp} XP · {state.commons.Values.Sum()} fish caught");
        c.Stats.Add(l < 10 ? $"Level {l + 1} at {XpFor(l + 1)} XP" : "The top level: every perk is yours.");
    }

    void CelebratePage(Biome b, int page, Action old)
    {
        int done = Data.Biomes.Count(x => state.Hinted("dexdone:" + x.Id)), total = PageFish(page).Count;
        var c = new Celeb
        {
            Kind = "page", Card = true, Icon = "star", Sting = "page", Tag = "Fish log page complete", Title = $"Every fish of {b.Name}!",
            Button = "Wonderful!"
        };
        c.Body.Add($"Pip sends {PageReward} coins for the collection.");
        c.Stats.Add($"{total} of {total} fish on this page");
        c.Stats.Add($"{done} of {Data.Biomes.Length} pages complete");
        Celebrate(c, old);
    }

    /* ---------- Drawing ---------- */

    // A dark ribbon with a gold edge, top middle, under any toast: the picture, a tag, the name and one line.
    void DrawBanner()
    {
        if (banner == null || !BannerLive) return;
        var c = banner;
        float t = bannerT, len = BannerLen(c);
        float a = t < BannerIn ? t / BannerIn : t > len - BannerOut ? (len - t) / BannerOut : 1;
        a = Math.Clamp(a, 0, 1);
        float slide = Calm ? 0 : (1 - a) * -18;
        const float w = 620, pic = 76;
        float h = c.Note != null ? 116 : 96;
        // Centred where the toast is, right of the status and goal cards.
        float cx = (HudX + HudW + 14 + Gfx.LW - 13) / 2;
        float x = cx - w / 2, y = MathF.Max(70, lastToastBottom + 10) + slide;
        lastBannerBottom = y + h;
        var gold = Pal.C("#e8b83a");
        Gfx.Rect(x - 5, y - 5, w + 10, h + 10, Pal.WithAlpha(gold, 0.22f * a), 10);
        Gfx.Box(x, y, w, h, Pal.WithAlpha(NavyStrong, (Gfx.HighContrast ? 1 : 0.94f) * a), Pal.WithAlpha(gold, a), 3, 7);
        // Ribbon tails.
        Gfx.Triangle(x - 2, y + 14, x - 22, y + 6, x - 14, y + 26, Pal.WithAlpha(gold, a));
        Gfx.Triangle(x + w + 2, y + 14, x + w + 22, y + 6, x + w + 14, y + 26, Pal.WithAlpha(gold, a));
        float px = x + 12, py = y + (h - pic) / 2;
        Gfx.Rect(px, py, pic, pic, Pal.WithAlpha(Pal.C("#173a5a"), a), 6);
        DrawCelebPicture(c, px, py, pic, pic, a);
        float tx = px + pic + 16, tw = w - (tx - x) - 16;
        Gfx.Text(Gfx.Ellipsize(c.Tag.ToUpperInvariant(), FontKind.Ui700, 15, tw), tx, y + 10, FontKind.Ui700, 15, Pal.WithAlpha(gold, a));
        float ts = Gfx.Measure(c.Title, FontKind.Ui700, 30) <= tw ? 30 : 24;
        Gfx.Text(Gfx.Ellipsize(c.Title, FontKind.Ui700, ts, tw), tx, y + 30 + (30 - ts) / 2, FontKind.Ui700, ts, Pal.WithAlpha(Pal.Paper, a));
        Gfx.Text(Gfx.Ellipsize(c.Sub ?? "", FontKind.Ui600, 17, tw), tx, y + 66, FontKind.Ui600, 17, Pal.WithAlpha(CardText("#cfe2ee"), a));
        if (c.Note != null)
            Gfx.Text(Gfx.Ellipsize(Bind.Fix(c.Note), FontKind.Ui500, 15, tw), tx, y + 90, FontKind.Ui500, 15, Pal.WithAlpha(CardText("#a9c4d6"), a));
    }

    void DrawCelebPicture(Celeb c, float x, float y, float w, float h, float a)
    {
        if (c.Fish != null)
        {
            var tex = FishArt.Picture(c.Fish, false);
            float k = MathF.Min((w - 8) / FishArt.BigW, (h - 8) / FishArt.BigH);
            DrawTexturePro(tex, new Rectangle(0, 0, FishArt.BigW, FishArt.BigH),
                Gfx.S(x + w / 2 - FishArt.BigW * k / 2, y + h / 2 - FishArt.BigH * k / 2, FishArt.BigW * k, FishArt.BigH * k),
                System.Numerics.Vector2.Zero, 0, Pal.WithAlpha(Color.White, a));
        }
        else if (c.Badge != null) DrawBadgeIcon(c.Badge, x + w / 2, y + h / 2, c.BadgeTier);
        else if (c.Kind == "level") DrawLevelMedal(x + w / 2, y + h / 2, MathF.Min(w, h) / 2 - 4, c.Level1);
        else if (c.Icon != null) DrawIcon(c.Icon, x + w / 2 - w * 0.3f, y + h / 2 - h * 0.3f, MathF.Min(w, h) * 0.6f);
    }

    // A gold medal with the level on it.
    static void DrawLevelMedal(float cx, float cy, float r, int level)
    {
        Gfx.Circle(cx, cy, r, Pal.C("#9a6a1a"));
        Gfx.Circle(cx, cy, r - 4, Pal.C("#e8b83a"));
        Gfx.Circle(cx, cy, r - 9, Pal.C("#f6d778"));
        string s = level.ToString();
        float fs = r * 1.05f;
        Gfx.TextCenter(s, cx, cy - fs / 2 - 2, FontKind.Ui700, fs, Pal.C("#5a3a0a"));
    }

    // A card for the big moments: a picture or medal at the top, a title, a tag, what you got, a few numbers, one button.
    void DrawCelebrationCard()
    {
        if (celebCard is not { } c) return;
        Backdrop();
        const float cw = 520, pad = 22, iw = cw - pad * 2, artH = 130;
        var body = c.Body.SelectMany(l => Gfx.Wrap(Bind.Fix(l), FontKind.Note, 21, iw)).ToList();
        float h = pad + artH + 16 + 40 + 8 + 28 + 14 + body.Count * 30 + 10 + c.Stats.Count * 26 + 18 + 56 + pad;
        float x = (Gfx.LW - cw) / 2, y = MathF.Max(10, (Gfx.LH - h) / 2);
        lastCelebBottom = y + h;
        Gfx.Rect(x - 7, y - 7, cw + 14, h + 14, Pal.WithAlpha(Pal.Lantern, 0.4f + (Calm ? 0 : 0.15f * MathF.Sin(time * 3))), 12);
        CardGlow(x - 7, y - 7, cw + 14, h + 14, 1.2f);
        PanelBox(x, y, cw, h);
        float cy = y + pad;
        // Rays behind the medal (still with Reduce motion).
        float mx = x + cw / 2, my = cy + artH / 2;
        for (int i = 0; i < 12; i++)
        {
            float ang = i * MathF.Tau / 12 + (Calm ? 0 : time * 0.25f);
            Gfx.Triangle(mx, my, mx + MathF.Cos(ang - 0.11f) * 120, my + MathF.Sin(ang - 0.11f) * 66, mx + MathF.Cos(ang + 0.11f) * 120, my + MathF.Sin(ang + 0.11f) * 66, Pal.Rgba(232, 184, 58, 0.18f));
        }
        DrawCelebPicture(c, mx - artH / 2, cy, artH, artH, 1);
        cy += artH + 16;
        float ts = Gfx.Measure(c.Title, FontKind.Ui700, 36) <= iw ? 36 : 28;
        Gfx.TextCenter(c.Title, mx, cy + (36 - ts) / 2, FontKind.Ui700, ts, Pal.PaperInk);
        cy += 40 + 8;
        float rw = Gfx.Measure(c.Tag, FontKind.Ui600, 17) + 18;
        Gfx.Rect(mx - rw / 2, cy, rw, 28, Pal.Ink, 3);
        Gfx.TextCenter(c.Tag, mx, cy + 5, FontKind.Ui600, 17, Pal.Lantern);
        cy += 28 + 14;
        foreach (var l in body) { Gfx.TextCenter(l, mx, cy, FontKind.Note, 21, Pal.PaperInk); cy += 30; }
        cy += 10;
        foreach (var s in c.Stats) { Gfx.TextCenter(s, mx, cy, FontKind.Ui600, 17, Muted); cy += 26; }
        cy += 18;
        if (BigButton(c.Button, mx - BigW(c.Button) / 2, cy, true) && GetTime() - catchOpenedAt >= 0.6) CloseCelebration();
    }
}
