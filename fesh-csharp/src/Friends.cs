using Raylib_cs;

namespace Fesh;

// Villager friendship (1.45): hearts with the people of the islands, earned by talking (once a day, when a conversation
// really starts) and by a gift they love (once a day, from what you can spare: nothing a request is waiting for). At
// three hearts they give you something useful, at five a hundred coins. Codex shaped the numbers (about two weeks of
// daily visits to five hearts), the rule that only a real conversation counts, and keeping requests' items safe.
sealed class Friendship { public int points, talkDay = -1, giftDay = -1; public bool got3, got5; }

sealed record Friend(string Id, string Name, string Home, string[] Loves, string Present, int PresentN, string Thanks);

partial class Game
{
    internal static bool FriendsOff;   // the old scripts keep their toasts and items (AutoTestStart); FriendScript turns it on
    static readonly int[] HeartAt = { 4, 12, 24, 40, 56 };
    const int TalkPoints = 1, GiftPoints = 3, BestFriendCoins = 100;

    static readonly Friend[] Friends =
    {
        new("tomas", "Tomas", "Saltmere", new[] { "rusty_grouper", "silver_tuna", "grilled_fish" }, "chum", 4, "Now that's a supper. Salamat."),
        new("pip", "Pip", "Saltmere", new[] { "moon_carp", "pearl", "sinigang" }, "bait", 10, "For me? Ha! I'll keep this one off the stall."),
        new("lira", "Lira", "Amihan Village", new[] { "bangus", "sinigang" }, "glow_bait", 3, "Salamat! This will make a good supper."),
        new("niko", "Niko", "Amihan Village", new[] { "talakitok", "kitang", "bangus" }, "cricket", 6, "Ay, a good one. Salamat!"),
        new("maya", "Maya", "Luntian", new[] { "ensaladang_guso", "dried_guso", "lapu_lapu" }, "guso", 4, "My favourite. Salamat!"),
        new("tala", "Tala", "Bakawan", new[] { "hito", "dalag" }, "worm", 6, "From the mangroves? Salamat. I'll cook it tonight."),
        new("isay", "Ma'am Isay", "Amihan Village", new[] { "asohos", "maya_maya", "kinilaw" }, "glow_bait", 2, "How kind. Salamat."),
        new("mila", "Manay Mila", "Baga", new[] { "tanigue", "ginataan" }, "bait", 6, "Salamat, anak. You're thoughtful."),
        new("ben", "Ben", "Baga", new[] { "yellowfin_tuna", "tulingan" }, "chum", 3, "Good food for a long night of readings. Salamat!"),
        new("joy", "Bantay Joy", "the sanctuary", new[] { "sinigang", "kinilaw", "calamansi" }, "bait", 6, "From outside the sanctuary, I hope! Salamat."),
        new("rosa", "Manang Rosa", "Asinan", new[] { "danggit", "sapsap", "tuyo" }, "salt", 6, "Salamat. You know what a good table needs."),
        new("pacing", "Lola Pacing", "Daang Pulo", new[] { "pugita", "labahita", "kinilaw" }, "calamansi", 4, "Salamat, apo."),
        new("dado", "Dado", "Daang Pulo", new[] { "bisugo", "talang_talang" }, "chum", 3, "That'll feed the crew. Salamat!"),
        new("celso", "Tatay Celso", "Parola", new[] { "tamban", "pusit" }, "glow_bait", 3, "Salamat. A keeper eats well tonight."),
        new("sela", "Aling Sela", "Marilaya", new[] { "pampano", "dapa" }, "bait", 6, "Salamat! I'll share it with the trail's helpers."),
        new("lorna", "Lorna", "Marilaya", new[] { "kanduli", "biya", "daing" }, "daing", 3, "Salamat. It dries just right in my dryer."),
        new("ambo", "Mang Ambo", "Marilaya", new[] { "dilis", "hasa_hasa" }, "cut_bait", 5, "Salamat. I'll have it with my coffee."),
        new("iyo", "Mang Iyo", "Marilaya", new[] { "suno", "pakol" }, "chum", 3, "Salamat! A fine one."),
        new("nita", "Aling Nita", "Marilaya", new[] { "igat", "gulyasan" }, "glow_bait", 2, "Salamat. You're always welcome here."),
    };
    static readonly Dictionary<string, Friend> FriendById = Friends.ToDictionary(f => f.Id);
    static readonly Dictionary<string, Friend> FriendByName = Friends.ToDictionary(f => f.Name);

    Friendship FriendData(string id) => state.friends.TryGetValue(id, out var f) ? f : null;
    int Hearts(string id) => FriendData(id) is Friendship f ? HeartAt.Count(h => f.points >= h) : 0;
    int FriendsAt(int hearts) => Friends.Count(f => Hearts(f.Id) >= hearts);

    // Who a target is, if it's one of them (and they're up and about: asleep is an info line).
    string FriendOf(Target t) => t?.Type switch
    {
        "npc" when !TomasInBed => "tomas",
        "pip" => "pip",
        "joy" => "joy",
        "islander" or "habagatfolk" or "marilaya" when t.Id != null && FriendById.ContainsKey(t.Id) => t.Id,
        _ => null
    };

    /* ---------- Talking ---------- */
    bool noTalkCredit;   // the gift's own thanks isn't a conversation
    string giftTo;       // who the <alt> gift on this frame's target is for (not on the target: Ref belongs to it)
    string talkingTo;    // who you pressed <act> on this frame: only a conversation you start with them counts

    // Talk(): a conversation with one of them has started. Once a day it's a point.
    void NoteTalkLines(List<Say> lines)
    {
        if (FriendsOff || noTalkCredit || MatchActive || state?.created != true) return;
        foreach (var name in lines.Select(l => l.S).Distinct().ToList())   // a copy: a new heart adds its line to `lines`
            if (FriendByName.TryGetValue(name, out var f) && f.Id == talkingTo) NoteTalk(f.Id, lines);
    }

    void NoteTalk(string id, List<Say> lines = null)
    {
        if (FriendsOff || MatchActive || state?.created != true) return;
        var f = state.friends.TryGetValue(id, out var have) ? have : state.friends[id] = new Friendship();
        if (f.talkDay == state.day) return;
        f.talkDay = state.day;
        AddFriendPoints(id, TalkPoints, lines);
    }

    /* ---------- Gifts ---------- */
    // What requests are waiting for: never given away as a gift (Codex).
    int Reserved(string id)
    {
        int n = 0;
        if (state.req?.item == id) n += state.req.count;
        if (id == "bangus" && state.Hinted("nikoAsked") && !state.Hinted("niko_request")) n += 2;
        if (id == "asohos" && state.Hinted("nikoAsohosAsked") && !state.Hinted("niko_asohos")) n += NikoAsohos;
        if (SupperDishes.Contains(id) && state.Hinted("liraSupperAsked") && !state.Hinted("rondalla")) n += 1;
        if (id == "dried_guso" && state.Hinted("guso") && !state.Hinted("gusoCoop")) n += GusoHandIn;
        // Rosa's order takes any of its kinds, so only the total matters (GiftFor; Codex).
        return n;
    }

    // The first thing they love that you can spare today, if they haven't had a gift today.
    string GiftFor(string id)
    {
        if (!FriendById.TryGetValue(id, out var fr) || FriendData(id)?.giftDay == state.day) return null;
        return fr.Loves.FirstOrDefault(item => Items.ById.ContainsKey(item) && Has(item) - Reserved(item) >= 1 && !ClosedSeason(item) && RosaSpares(item));
    }

    // Her order is so many of any of its kinds: giving one away must leave enough of them all together.
    bool RosaSpares(string item)
    {
        if (!state.Hinted("metRosa") || RosaOrderDone || !RosaOrder.items.Contains(item)) return true;
        return RosaOrder.items.Sum(i => Has(i) - Reserved(i)) - 1 >= RosaOrder.count;
    }

    // Update: a gift goes on the person's <alt>, if nothing else is there.
    void AddGiftAlt()
    {
        if (FriendsOff || target == null || target.AltType != null || MatchActive) return;
        if (FriendOf(target) is not string id || GiftFor(id) is not string item) return;
        target.AltType = "gift";
        string what = Items.ById[item].Name.Split(" (")[0];
        target.AltLabel = $"Give {FriendById[id].Name} {char.ToLowerInvariant(what[0]) + what[1..]}";
        giftTo = id;
    }

    void GiveGift(string id)
    {
        string item = GiftFor(id);   // checked again on the press
        if (item == null) { Sfx.Play("nope"); return; }
        var fr = FriendById[id];
        var f = state.friends.TryGetValue(id, out var have) ? have : state.friends[id] = new Friendship();
        if (!Take(item)) return;
        // A big fish given away takes its premium with it (only as many big ones as are left).
        if (state.big.GetValueOrDefault(item) > Has(item)) state.big[item] = Has(item);
        f.giftDay = state.day;
        Swing("hands", 0.4f);
        Sfx.Play("coin");
        var lines = new List<Say> { new(fr.Name, fr.Thanks) };
        AddFriendPoints(id, GiftPoints, lines);
        Save();
        noTalkCredit = true;
        Talk(lines);
        noTalkCredit = false;
    }

    /* ---------- Hearts ---------- */
    // Points in, and anything a new heart brings, all saved with them; the news is a banner (and their present's line).
    void AddFriendPoints(string id, int n, List<Say> lines = null)
    {
        var f = state.friends[id];
        var fr = FriendById[id];
        int before = Hearts(id);
        f.points = Math.Min(f.points + n, HeartAt[^1]);
        int after = Hearts(id);
        if (after <= before) return;
        string got = "";
        // What they say at this heart: after the conversation that earned it (the Friends panel keeps all of them).
        if (HeartLine(id, after) is string said) lines?.Add(new Say(fr.Name, said));
        if (after >= 3 && !f.got3)
        {
            f.got3 = true;
            Give(fr.Present, fr.PresentN);
            got = $"{fr.Name} gives you {Items.Amount(fr.Present, fr.PresentN)}.";
            lines?.Add(new Say(fr.Name, "Here, take these. Friends look after each other."));
        }
        if (after >= 5 && !f.got5)
        {
            f.got5 = true;
            state.coins += BestFriendCoins;
            got += (got != "" ? " " : "") + $"And {BestFriendCoins} coins, for a best friend.";
            lines?.Add(new Say(fr.Name, "You're family here now, you know that?"));
        }
        Save();
        Celebrate(new Celeb
        {
            Kind = "friend", Icon = "star", Sting = after >= 5 ? "levelup" : "badge",
            Tag = "Friendship", Title = $"{fr.Name}: {after} heart{(after == 1 ? "" : "s")}",
            Sub = got != "" ? got : "Talk each day, and bring something they love.",
        }, () => Toast($"{fr.Name}: {after} heart{(after == 1 ? "" : "s")}. {got}"));
    }

    /* ---------- The panel ---------- */
    float lastFriendsBottom;
    string friendTip;   // whose card the mouse is on: what they have said at each heart (1.45.1)
    int friendsPage;

    void OpenFriends() { Sfx.Play("ui"); panel = "friends"; mode = "panel"; SetPrompt(""); }

    void DrawFriends()
    {
        var met = Friends.Where(f => state.friends.ContainsKey(f.Id)).ToList();
        var (x, y, w, h) = PanelFrame("Friends", "Talk each day, and bring something they love. Requests' fish are never given away.");
        if (PanelClose()) { ClosePanels(); return; }
        if (SmallButton("Journal", PanelCloseX - 12 - SmallW("Journal"), y + PanelPad - 6)) { panel = "journal"; Sfx.Play("ui"); return; }
        float lx = x + PanelPad, ly = y + 112, gap = 12;
        const int cols = 3, rows = 4, per = cols * rows;
        float cw = (w - PanelPad * 2 - gap * (cols - 1)) / cols, ch = 112;
        if (met.Count == 0) { Gfx.Text("Nobody yet. Say hello to the people of the islands.", lx, ly, FontKind.Note, 20, Muted); return; }
        int pages = (met.Count + per - 1) / per;
        friendsPage = Math.Clamp(friendsPage, 0, pages - 1);
        for (int i = 0; i < per && friendsPage * per + i < met.Count; i++)
        {
            var fr = met[friendsPage * per + i];
            var f = state.friends[fr.Id];
            float cx = lx + (i % cols) * (cw + gap), cy = ly + (i / cols) * (ch + gap);
            int hearts = Hearts(fr.Id);
            Gfx.Box(cx, cy, cw, ch, hearts >= 5 ? Pal.C("#fff3cf") : CardBg, hearts >= 5 ? Pal.C("#d9a640") : Pal.C("#c9b48f"), 2, 6);
            Gfx.Text(Gfx.Ellipsize(fr.Name, FontKind.Ui700, 20, cw - 150), cx + 14, cy + 10, FontKind.Ui700, 20, Pal.PaperInk);
            for (int k = 0; k < 5; k++) DrawHeart(cx + cw - 118 + k * 24, cy + 20, k < hearts);
            Gfx.Text(fr.Home, cx + 14, cy + 36, FontKind.Ui500, 15, Muted);
            string loves = "Loves " + string.Join(", ", fr.Loves.Select(l => Items.ById.TryGetValue(l, out var d) ? d.Name.Split(" (")[0] : l));
            Gfx.Text(Gfx.Ellipsize(loves, FontKind.Ui500, 15, cw - 28), cx + 14, cy + 58, FontKind.Ui500, 15, Pal.PaperInk);
            string today = (f.talkDay == state.day ? "Talked today" : "Not talked today") + (f.giftDay == state.day ? " · gift given" : "");
            Gfx.Text(today, cx + 14, cy + 82, FontKind.Ui600, 14, f.talkDay == state.day ? Pal.C("#3f7d35") : Muted);
#if DEBUG
            Gfx.Seen["friend:" + fr.Id] = new Rectangle(cx, cy, cw, ch);
#endif
            if (Gfx.Hover(cx, cy, cw, ch)) friendTip = fr.Id;
            lastFriendsBottom = MathF.Max(lastFriendsBottom, cy + ch);
        }
        if (friendTip != null) { DrawFriendTip(friendTip, Gfx.Mouse.X, Gfx.Mouse.Y); friendTip = null; }
        if (pages > 1)
        {
            float by = ly + rows * (ch + gap) + 4;
            if (friendsPage > 0 && SmallButton("Back", lx, by)) { friendsPage--; Sfx.Play("ui"); return; }
            Gfx.TextCenter($"Page {friendsPage + 1} of {pages}", x + w / 2, by + 12, FontKind.Ui600, 17, Muted);
            if (friendsPage < pages - 1 && SmallButton("More", x + w - PanelPad - SmallW("More"), by)) { friendsPage++; Sfx.Play("ui"); return; }
            lastFriendsBottom = MathF.Max(lastFriendsBottom, by + 44);
        }
    }
}

partial class Game
{
    // Hovering a card: what they have said at each heart you've earned, so it can be read again.
    void DrawFriendTip(string id, float mx, float my)
    {
        var fr = FriendById[id];
        int hearts = Hearts(id);
        const float w = 440, fs = 16, lh = 21;
        var blocks = new List<List<string>>();
        for (int k = 1; k <= hearts; k++) if (HeartLine(id, k) is string s) blocks.Add(Gfx.Wrap($"{k}. {s}", FontKind.Note, fs, w - 28));
        if (blocks.Count == 0) blocks.Add(Gfx.Wrap($"{fr.Name} will say something once you're friends: talk each day and bring what they love.", FontKind.Note, fs, w - 28));
        float h = 44 + blocks.Sum(b => b.Count * lh + 6) + 8;
        float x = Math.Clamp(mx + 18, 8, Gfx.LW - w - 8), y = Math.Clamp(my + 18, 8, Gfx.LH - h - 8);
        if (my + 18 + h > Gfx.LH - 8) y = Math.Max(8, my - h - 12);
        Gfx.Box(x, y, w, h, NavyStrong, Pal.Ink, 2, 6);
        Gfx.Text(fr.Name, x + 14, y + 10, FontKind.Ui700, 19, Pal.Lantern);
        float ty = y + 40;
        foreach (var b in blocks) { Lines(b, x + 14, ty, lh, FontKind.Note, fs, Pal.Paper); ty += b.Count * lh + 6; }
    }
}
