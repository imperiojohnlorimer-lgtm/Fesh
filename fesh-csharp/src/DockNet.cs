using System.Diagnostics;

namespace Fesh;

// The Tournament Dock over a LAN (1.30), with no screen and no sockets of its own: it runs over any ILink, so the tests
// use the in-memory one and real loopback connections.
//
// One player hosts the dock. Every player, the host included (through an in-memory link), is a DockClientCore talking to the
// DockHostCore, which is the referee of presence: it hands out ids, takes each player's pose and publishes a roster, relays
// quick phrases, and brokers challenges. A match between two players is carried over the same connections (`route`
// messages for a channel), so nobody opens a port or trusts an address the host supplies. Nothing a client sends can name
// another player as the sender, and every number is checked: positions are clamped to the dock and to how fast a person
// can walk, names are only ever picked from the lists by index, looks are indexes, phrases are indexes.
static class DockRules
{
    public const float X0 = 253, X1 = 327, Y0 = 187, Y1 = 211, SpawnX = 285, SpawnY = 202;
    public const int MaxPlayers = 8, Protocol = 1, Stride = 14;
    public const float MaxSpeed = 70;                   // the walk is 52 px/s
    public const double InviteSecs = 15, SilentLimit = 6, HelloSecs = 5;
    public const int MaxRoute = 700;                    // bytes of one routed message
    public static readonly string[] Emotes = { "Good luck!", "Nice catch!", "Ready?", "Rematch?", "Good game!", "Wow!", "Thanks!", "Oops!" };
    public static readonly string[] Modes = { "Sungka", "Derby Duel", "Regatta", "Sea School Duel" };
    public static int Wrap(int v, int count) => count <= 0 ? 0 : ((v % count) + count) % count;
}

// One person on the dock, as the host and every client see them.
sealed class DockPlayer
{
    public int Id, Adj, Noun, Skin, Hair, HairColor, Hat, Shirt, Pants;
    public int State;                                   // 0 on the dock, 1 in a match
    public int Face, Step;                              // 0 up, 1 right, 2 down, 3 left; 0 standing or 1-4 walking
    public float X, Y;                                  // where they are (a client draws them moving toward TX, TY)
    public float TX, TY;
    public string Emote = "";
    public double EmoteUntil;
    public string BaseName => Leaderboards.NameOf(Adj, Noun);
    public string Name = "";                            // what to call them: the base name, with " 2", " 3" for later players of the same name
    public string FaceName => Face switch { 0 => "up", 1 => "right", 2 => "down", _ => "left" };
    public Look ToLook() => new() { name = Name, skin = Skin, hair = Hair, hairColor = HairColor, hat = Hat, shirt = Shirt, pants = Pants };
    public void Fill(int[] n, int at)
    {
        // n: id, adj, noun, skin, hair, hairColor, hat, shirt, pants, state, x4, y4, face, step
        Id = n[at]; Adj = DockRules.Wrap(n[at + 1], Leaderboards.Adjectives); Noun = DockRules.Wrap(n[at + 2], Leaderboards.Nouns);
        Skin = DockRules.Wrap(n[at + 3], LookData.Skins.Length); Hair = DockRules.Wrap(n[at + 4], LookData.Hairs.Length);
        HairColor = DockRules.Wrap(n[at + 5], LookData.HairColors.Length); Hat = DockRules.Wrap(n[at + 6], LookData.Hats.Length);
        Shirt = DockRules.Wrap(n[at + 7], LookData.Shirts.Length); Pants = DockRules.Wrap(n[at + 8], LookData.Pants.Length);
        State = n[at + 9] == 1 ? 1 : 0;
        TX = Math.Clamp(n[at + 10] / 4f, DockRules.X0, DockRules.X1); TY = Math.Clamp(n[at + 11] / 4f, DockRules.Y0, DockRules.Y1);
        Face = DockRules.Wrap(n[at + 12], 4); Step = Math.Clamp(n[at + 13], 0, 4);
        Name = BaseName;
    }
    public void WriteTo(List<int> n) =>
        n.AddRange(new[] { Id, Adj, Noun, Skin, Hair, HairColor, Hat, Shirt, Pants, State, (int)MathF.Round(X * 4), (int)MathF.Round(Y * 4), Face, Step });
}

sealed class Bucket
{
    readonly double rate, cap;
    double tokens, last = -1;
    public Bucket(double perSecond, double burst) { rate = perSecond; cap = burst; tokens = burst; }
    public bool Take(double now)
    {
        if (last >= 0) tokens = Math.Min(cap, tokens + (now - last) * rate);
        last = now;
        if (tokens < 1) return false;
        tokens -= 1;
        return true;
    }
}

sealed class DockChallenge { public int Id, From, To, Mode; public bool Active; public double Expires; }

sealed class DockPeer
{
    public int Id;
    public ILink Link;
    public bool In, Dead;
    public double Joined, LastPose = -1, LastBudget;
    public float Budget = 10;                          // pixels of walking the peer may still claim
    public readonly DockPlayer P = new();
    public int Busy;                                   // the id of the challenge this player is part of (0: none)
    public bool Away;                                  // off at a match of their own against someone the dock can't see (an angler)
    public readonly Bucket Pose = new(12, 8), Emote = new(0.5, 1), Invite = new(0.34, 2), Route = new(80, 40), Misc = new(20, 20);
}

sealed class DockHostCore
{
    readonly List<DockPeer> peers = new();
    readonly Dictionary<int, DockChallenge> challenges = new();
    int nextPeer = 1, nextChallenge = 1;
    double lastRoster = -9, lastHb;
    bool dirty = true;
    readonly Stopwatch clock = Stopwatch.StartNew();
    public Func<double> Now;
    public string Closed = "";                          // why the dock ended, once it has
    public DockHostCore() { Now = () => clock.Elapsed.TotalSeconds; }

    public IReadOnlyList<DockPlayer> Players => peers.Where(p => p.In).Select(p => p.P).ToList();
    public int Count => peers.Count(p => p.In);
    public int Pending => challenges.Count;

    // A new connection (or the host's own player). A full dock turns it away.
    public int AddLink(ILink link)
    {
        if (Closed != "" || peers.Count >= DockRules.MaxPlayers)
        {
            link.Send(new NetMsg { t = "dbye", why = Closed != "" ? "closed" : "full" });
            link.Close();
            return -1;
        }
        var p = new DockPeer { Id = nextPeer++, Link = link, Joined = Now() };
        p.P.Id = p.Id;
        peers.Add(p);
        return p.Id;
    }

    DockPeer Find(int id) => peers.FirstOrDefault(p => p.Id == id && p.In);

    public void Tick(float dt)
    {
        if (Closed != "") return;
        double now = Now();
        foreach (var peer in peers.ToList())
        {
            if (peer.Dead) continue;
            for (int i = 0; i < 48; i++)
            {
                var m = peer.Link.Receive();
                if (m == null) break;
                Handle(peer, m, now);
                if (peer.Dead) break;
            }
            if (peer.Dead) continue;
            if (!peer.Link.Alive || peer.Link.SilentFor > DockRules.SilentLimit) Drop(peer, "left");
            else if (!peer.In && now - peer.Joined > DockRules.HelloSecs) Drop(peer, "no hello");
        }
        foreach (var c in challenges.Values.Where(c => !c.Active && now > c.Expires).ToList()) EndChallenge(c, "expired");
        if (now - lastHb >= 1) { lastHb = now; foreach (var p in peers) p.Link.Send(new NetMsg { t = "hb" }); }
        if ((dirty && now - lastRoster >= 0.2) || now - lastRoster >= 1.5) { lastRoster = now; dirty = false; SendRoster(); }
    }

    void SendRoster()
    {
        var list = new List<int>();
        foreach (var p in peers.Where(p => p.In)) p.P.WriteTo(list);
        var msg = new NetMsg { t = "roster", n = list.ToArray() };
        foreach (var p in peers.Where(p => p.In)) p.Link.Send(msg);
    }

    void Handle(DockPeer peer, NetMsg m, double now)
    {
        if (m.t == "bye") { Drop(peer, "left"); return; }
        if (m.t == "hb") return;
        if (!peer.In)
        {
            if (m.t != "dhello") return;
            if (m.v != DockRules.Protocol || m.n == null || m.n.Length != 8) { peer.Link.Send(new NetMsg { t = "dbye", why = "version" }); Drop(peer, "version", tell: false); return; }
            var p = peer.P;
            p.Adj = DockRules.Wrap(m.n[0], Leaderboards.Adjectives); p.Noun = DockRules.Wrap(m.n[1], Leaderboards.Nouns);
            p.Skin = DockRules.Wrap(m.n[2], LookData.Skins.Length); p.Hair = DockRules.Wrap(m.n[3], LookData.Hairs.Length);
            p.HairColor = DockRules.Wrap(m.n[4], LookData.HairColors.Length); p.Hat = DockRules.Wrap(m.n[5], LookData.Hats.Length);
            p.Shirt = DockRules.Wrap(m.n[6], LookData.Shirts.Length); p.Pants = DockRules.Wrap(m.n[7], LookData.Pants.Length);
            p.Name = p.BaseName;
            p.X = p.TX = DockRules.SpawnX + (peer.Id % 5 - 2) * 5; p.Y = p.TY = DockRules.SpawnY - peer.Id % 3 * 3;
            peer.In = true; peer.LastPose = peer.LastBudget = now;
            peer.Link.Send(new NetMsg { t = "dwelcome", from = peer.Id, v = DockRules.Protocol });
            dirty = true;
            return;
        }
        switch (m.t)
        {
            case "pose":
            {
                if (m.n == null || m.n.Length is not (4 or 5) || !peer.Pose.Take(now)) return;
                float x = m.n[0] / 4f, y = m.n[1] / 4f;
                var p = peer.P;
                if (!float.IsFinite(x) || !float.IsFinite(y)) return;
                // However far they claim to be, they get as far as a person could have walked: a budget that grows with real time
                // (never more than a few pixels saved up), so a burst of poses can't add up to a run.
                peer.Budget = MathF.Min(10, peer.Budget + DockRules.MaxSpeed * (float)Math.Max(0, now - peer.LastBudget));
                peer.LastBudget = now;
                float dx = x - p.X, dy = y - p.Y, d = MathF.Sqrt(dx * dx + dy * dy);
                if (d > peer.Budget) { dx *= peer.Budget / d; dy *= peer.Budget / d; d = peer.Budget; }
                peer.Budget -= d;
                p.X = Math.Clamp(p.X + dx, DockRules.X0, DockRules.X1); p.Y = Math.Clamp(p.Y + dy, DockRules.Y0, DockRules.Y1);
                p.TX = p.X; p.TY = p.Y;
                p.Face = DockRules.Wrap(m.n[2], 4); p.Step = Math.Clamp(m.n[3], 0, 4);
                peer.Away = m.n.Length == 5 && m.n[4] == 1;
                p.State = peer.Busy != 0 && challenges.TryGetValue(peer.Busy, out var bc) && bc.Active || peer.Away ? 1 : 0;
                peer.LastPose = now;
                dirty = true;
                break;
            }
            case "emote":
            {
                if (m.n == null || m.n.Length != 1 || m.n[0] < 0 || m.n[0] >= DockRules.Emotes.Length || !peer.Emote.Take(now)) return;
                var msg = new NetMsg { t = "emote", from = peer.Id, n = new[] { m.n[0] } };
                foreach (var o in peers.Where(o => o.In && o != peer)) o.Link.Send(msg);
                break;
            }
            case "invite":
            {
                if (!peer.Invite.Take(now) || m.n == null || m.n.Length != 2) return;
                int target = m.n[0], mode = m.n[1];
                var to = Find(target);
                // Both free, someone else, a mode that exists: otherwise nothing is reserved and the asker is told.
                if (to == null || to == peer || mode < 0 || mode >= DockRules.Modes.Length || peer.Busy != 0 || to.Busy != 0)
                {
                    peer.Link.Send(new NetMsg { t = "cancel", ch = 0, why = to == null || to == peer ? "gone" : "busy" });
                    return;
                }
                var c = new DockChallenge { Id = nextChallenge++, From = peer.Id, To = to.Id, Mode = mode, Expires = now + DockRules.InviteSecs };
                challenges[c.Id] = c;
                peer.Busy = to.Busy = c.Id;
                to.Link.Send(new NetMsg { t = "invite", ch = c.Id, from = peer.Id, n = new[] { mode } });
                peer.Link.Send(new NetMsg { t = "invsent", ch = c.Id, to = to.Id, n = new[] { mode } });
                break;
            }
            case "reply":
            {
                if (!challenges.TryGetValue(m.ch, out var c) || c.To != peer.Id || c.Active || m.n == null || m.n.Length != 1) return;
                if (now > c.Expires) { EndChallenge(c, "expired"); return; }
                if (m.n[0] != 1) { EndChallenge(c, "declined"); return; }
                var from = Find(c.From);
                if (from == null) { EndChallenge(c, "gone"); return; }
                c.Active = true;
                from.P.State = peer.P.State = 1;
                dirty = true;
                from.Link.Send(new NetMsg { t = "go", ch = c.Id, to = peer.Id, n = new[] { 0, c.Mode } });
                peer.Link.Send(new NetMsg { t = "go", ch = c.Id, to = from.Id, n = new[] { 1, c.Mode } });
                break;
            }
            case "route":
            {
                if (!peer.Route.Take(now) || !challenges.TryGetValue(m.ch, out var c) || !c.Active || m.data == null || m.data.Length > DockRules.MaxRoute) return;
                int other = c.From == peer.Id ? c.To : c.To == peer.Id ? c.From : 0;
                if (other == 0) return;
                Find(other)?.Link.Send(new NetMsg { t = "route", ch = c.Id, data = m.data });
                break;
            }
            case "done":
            {
                if (!challenges.TryGetValue(m.ch, out var c) || c.From != peer.Id && c.To != peer.Id) return;
                EndChallenge(c, "ended");
                break;
            }
        }
    }

    // Ends a challenge or a match for both players, once.
    void EndChallenge(DockChallenge c, string why, int leaver = 0)
    {
        if (!challenges.Remove(c.Id)) return;
        foreach (int id in new[] { c.From, c.To })
        {
            var p = Find(id);
            if (p == null) continue;
            p.Busy = 0; p.P.State = p.Away ? 1 : 0;
            if (id != leaver)
            {
                // A match still running sees its opponent leave, in the words its own session understands.
                if (c.Active) p.Link.Send(new NetMsg { t = "route", ch = c.Id, data = Wire.ToText(new NetMsg { t = "bye", why = "left" }) });
                p.Link.Send(new NetMsg { t = "cancel", ch = c.Id, why = why });
            }
        }
        dirty = true;
    }

    void Drop(DockPeer peer, string why, bool tell = true)
    {
        if (peer.Dead) return;
        peer.Dead = true;
        foreach (var c in challenges.Values.Where(c => c.From == peer.Id || c.To == peer.Id).ToList()) EndChallenge(c, why, leaver: peer.Id);
        peers.Remove(peer);
        if (tell && peer.In) peer.Link.Send(new NetMsg { t = "dbye", why = why });
        peer.Link.Close();
        dirty = true;
    }

    // The dock host leaves: everyone is told.
    public void Close(string why = "closed")
    {
        if (Closed != "") return;
        Closed = why;
        foreach (var p in peers) { p.Link.Send(new NetMsg { t = "dbye", why = why }); p.Link.Close(); }
        peers.Clear(); challenges.Clear();
    }
}

// A match carried over the dock: the same ILink as a socket, but its messages travel as `route` messages for one channel.
sealed class RoutedLink : ILink
{
    readonly Action<NetMsg> send;
    readonly Queue<NetMsg> inbox = new();
    readonly Stopwatch quiet = Stopwatch.StartNew();
    bool dead;
    public readonly int Ch;
    public RoutedLink(int ch, Action<NetMsg> send) { Ch = ch; this.send = send; }
    public string Peer => "dock";
    public bool Alive => !dead;
    public double SilentFor => quiet.Elapsed.TotalSeconds;
    public void Send(NetMsg m)
    {
        if (dead) return;
        var text = Wire.ToText(m);
        if (text.Length > DockRules.MaxRoute) return;
        send(new NetMsg { t = "route", ch = Ch, data = text });
    }
    public void Deliver(string data)
    {
        if (dead || inbox.Count > 256) return;
        var m = Wire.ParseText(data);
        if (m == null) return;
        inbox.Enqueue(m);
        quiet.Restart();
    }
    public NetMsg Receive() => inbox.Count > 0 ? inbox.Dequeue() : null;
    public void Close() => dead = true;
}

// What happens to a client that the game should react to.
sealed class DockEvent
{
    public string Kind = "";                           // invite, invsent, go, cancel, emote, closed
    public int Ch, From, Mode, Role;
    public string Why = "";
    public RoutedLink Link;
}

sealed class DockClientCore
{
    readonly ILink link;
    readonly Stopwatch clock = Stopwatch.StartNew();
    public Func<double> Now;
    public string State = "joining";                    // joining, in, closed
    public string Why = "";
    public int MyId;
    public readonly List<DockPlayer> Roster = new();    // everyone, you included
    public readonly Queue<DockEvent> Events = new();
    readonly Dictionary<int, RoutedLink> routes = new();
    double lastPoseSent = -9, lastHb;
    int[] pose = { (int)(DockRules.SpawnX * 4), (int)(DockRules.SpawnY * 4), 0, 0, 0 };
    bool poseDirty = true;
    public int SilentCount;

    public DockClientCore(ILink link, int adj, int noun, Look look)
    {
        this.link = link;
        Now = () => clock.Elapsed.TotalSeconds;
        link.Send(new NetMsg { t = "dhello", v = DockRules.Protocol, n = new[] { adj, noun, look.skin, look.hair, look.hairColor, look.hat, look.shirt, look.pants } });
    }

    public DockPlayer Me => Roster.FirstOrDefault(p => p.Id == MyId);
    public IEnumerable<DockPlayer> Others => Roster.Where(p => p.Id != MyId);

    public void Tick(float dt)
    {
        if (State == "closed") return;
        double now = Now();
        for (int i = 0; i < 64; i++)
        {
            var m = link.Receive();
            if (m == null) break;
            Handle(m, now);
            if (State == "closed") return;
        }
        if (!link.Alive || link.SilentFor > DockRules.SilentLimit) { Close("The dock stopped answering."); return; }
        if (now - lastHb >= 1) { lastHb = now; link.Send(new NetMsg { t = "hb" }); }
        if (State == "in" && poseDirty && now - lastPoseSent >= 0.125 || State == "in" && now - lastPoseSent >= 1.5)
        {
            lastPoseSent = now; poseDirty = false;
            link.Send(new NetMsg { t = "pose", n = pose });
        }
        // Everyone else glides toward where the host last said they were.
        foreach (var p in Roster)
        {
            if (p.Id == MyId) continue;
            float k = MathF.Min(1, dt * 10);
            p.X += (p.TX - p.X) * k; p.Y += (p.TY - p.Y) * k;
            if (MathF.Abs(p.TX - p.X) + MathF.Abs(p.TY - p.Y) > 40) { p.X = p.TX; p.Y = p.TY; }
        }
    }

    public void SetPose(float x, float y, int face, int step, bool away = false)
    {
        var next = new[] { (int)MathF.Round(x * 4), (int)MathF.Round(y * 4), face, step, away ? 1 : 0 };
        if (!next.SequenceEqual(pose)) { pose = next; poseDirty = true; }
    }

    public void Emote(int idx) { if (State == "in") link.Send(new NetMsg { t = "emote", n = new[] { idx } }); }
    public void Invite(int target, int mode) { if (State == "in") link.Send(new NetMsg { t = "invite", n = new[] { target, mode } }); }
    public void Reply(int ch, bool accept) { if (State == "in") link.Send(new NetMsg { t = "reply", ch = ch, n = new[] { accept ? 1 : 0 } }); }
    public void Done(int ch) { routes.Remove(ch); if (State == "in") link.Send(new NetMsg { t = "done", ch = ch }); }

    public void Close(string why = "")
    {
        if (State == "closed") return;
        if (State == "in" || State == "joining") link.Send(new NetMsg { t = "bye" });
        State = "closed"; Why = why;
        foreach (var r in routes.Values) r.Close();
        routes.Clear();
        link.Close();
        Events.Enqueue(new DockEvent { Kind = "closed", Why = why });
    }

    void Handle(NetMsg m, double now)
    {
        switch (m.t)
        {
            case "dwelcome" when State == "joining":
                if (m.v != DockRules.Protocol) { Close("That dock runs a different version of Fesh."); return; }
                MyId = m.from; State = "in";
                break;
            case "dbye":
                Close(m.why switch { "full" => "That dock is full.", "version" => "That dock runs a different version of Fesh.", "closed" => "The host closed the dock.", _ => "You left the dock." });
                break;
            case "roster" when State == "in":
            {
                if (m.n == null || m.n.Length % DockRules.Stride != 0 || m.n.Length / DockRules.Stride > DockRules.MaxPlayers) return;
                var seen = new HashSet<int>();
                for (int at = 0; at < m.n.Length; at += DockRules.Stride)
                {
                    int id = m.n[at];
                    if (id <= 0 || !seen.Add(id)) continue;
                    var p = Roster.FirstOrDefault(r => r.Id == id);
                    bool isNew = p == null;
                    if (isNew) Roster.Add(p = new DockPlayer());
                    float ox = p.X, oy = p.Y;
                    p.Fill(m.n, at);
                    if (isNew) { p.X = p.TX; p.Y = p.TY; } else { p.X = ox; p.Y = oy; }
                }
                Roster.RemoveAll(r => !seen.Contains(r.Id));
                // Two people with the same name are told apart by their order of arrival, the same on every screen.
                foreach (var grp in Roster.GroupBy(rp => rp.BaseName).Where(g => g.Count() > 1))
                {
                    int k = 1;
                    foreach (var rp in grp.OrderBy(rp => rp.Id)) { rp.Name = k == 1 ? rp.BaseName : $"{rp.BaseName} {k}"; k++; }
                }
                break;
            }
            case "emote" when State == "in":
            {
                if (m.n == null || m.n.Length != 1 || m.n[0] < 0 || m.n[0] >= DockRules.Emotes.Length) return;
                var p = Roster.FirstOrDefault(r => r.Id == m.from);
                if (p != null) { p.Emote = DockRules.Emotes[m.n[0]]; p.EmoteUntil = now + 3; }
                Events.Enqueue(new DockEvent { Kind = "emote", From = m.from, Mode = m.n[0] });
                break;
            }
            case "invite" when State == "in":
                if (m.n == null || m.n.Length != 1 || m.n[0] < 0 || m.n[0] >= DockRules.Modes.Length) return;
                Events.Enqueue(new DockEvent { Kind = "invite", Ch = m.ch, From = m.from, Mode = m.n[0] });
                break;
            case "invsent" when State == "in":
                Events.Enqueue(new DockEvent { Kind = "invsent", Ch = m.ch, From = m.to, Mode = m.n != null && m.n.Length == 1 ? m.n[0] : 0 });
                break;
            case "go" when State == "in":
            {
                if (m.n == null || m.n.Length != 2 || m.n[0] is not (0 or 1) || m.n[1] < 0 || m.n[1] >= DockRules.Modes.Length) return;
                int ch = m.ch;
                var rl = new RoutedLink(ch, msg => link.Send(msg));
                routes[ch] = rl;
                Events.Enqueue(new DockEvent { Kind = "go", Ch = ch, From = m.to, Role = m.n[0], Mode = m.n[1], Link = rl });
                break;
            }
            case "cancel" when State == "in":
                if (routes.Remove(m.ch, out var dead)) dead.Close();
                Events.Enqueue(new DockEvent { Kind = "cancel", Ch = m.ch, Why = m.why ?? "" });
                break;
            case "route" when State == "in":
                if (routes.TryGetValue(m.ch, out var r)) r.Deliver(m.data);
                break;
        }
    }
}
