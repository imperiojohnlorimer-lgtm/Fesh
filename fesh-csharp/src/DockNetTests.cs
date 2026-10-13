#if DEBUG
using System.Net.Sockets;

namespace Fesh;

partial class Game
{
    // A host and its players over in-memory links, all on one fake clock.
    sealed class DockRig
    {
        public double T;
        public readonly DockHostCore Host = new();
        public readonly List<(DockClientCore c, MemoryLink hostEnd, MemoryLink clientEnd)> Clients = new();
        public DockRig() { Host.Now = () => T; }
        public (DockClientCore c, MemoryLink hostEnd, MemoryLink clientEnd) Join(int adj = 0, int noun = 0, Look look = null)
        {
            var (h, c) = MemoryLink.Pair();
            int id = Host.AddLink(h);
            var cl = new DockClientCore(c, adj, noun, look ?? new Look());
            cl.Now = () => T;
            var entry = (cl, h, c);
            Clients.Add(entry);
            return entry;
        }
        public void Step(float dt = 0.1f, int times = 1)
        {
            for (int i = 0; i < times; i++)
            {
                T += dt;
                foreach (var e in Clients) e.c.Tick(dt);
                Host.Tick(dt);
                foreach (var e in Clients) e.c.Tick(dt);
            }
        }
    }

    // The Tournament Dock over a LAN (1.30): joining, poses, phrases, challenges and a sungka game routed through the dock,
    // hostile messages, and real sockets. FESH_DOCKNET_TEST=1 runs only this.
    IEnumerable<int> DockNetScript()
    {
        Note("Dock net: joining");
        var rig = new DockRig();
        var a = rig.Join(1, 2, new Look { skin = 2, shirt = 3 });
        rig.Step();
        Check("the first player joins: welcomed with an id, on the roster", a.c.State == "in" && a.c.MyId == 1 && rig.Host.Count == 1 && a.c.Roster.Count == 1 && a.c.Me.Name == Leaderboards.NameOf(1, 2));
        var b = rig.Join(3, 4); var c = rig.Join(5, 6);
        rig.Step(0.1f, 5);
        Check("three players all see all three, with their own names and looks", rig.Clients.All(x => x.c.Roster.Count == 3) && b.c.Roster.Select(p => p.Name).OrderBy(s => s).SequenceEqual(new[] { Leaderboards.NameOf(1, 2), Leaderboards.NameOf(3, 4), Leaderboards.NameOf(5, 6) }.OrderBy(s => s)) && b.c.Roster.First(p => p.Id == 1).Skin == 2 && b.c.Roster.First(p => p.Id == 1).Shirt == 3);
        Check("ids are the host's, in order of arrival", a.c.MyId == 1 && b.c.MyId == 2 && c.c.MyId == 3);
        // Names and looks are only ever indexes into the lists.
        var weird = rig.Join(-7, 9999, new Look { skin = -3, hair = 99, hairColor = 1000, hat = -1, shirt = 12345, pants = -99 });
        rig.Step(0.1f, 3);
        var wp = a.c.Roster.First(p => p.Id == weird.c.MyId);
        Check("wild name and look numbers wrap into the lists (nobody can be called anything else)", wp.Adj >= 0 && wp.Adj < Leaderboards.Adjectives && wp.Noun >= 0 && wp.Noun < Leaderboards.Nouns && wp.Skin < LookData.Skins.Length && wp.Shirt < LookData.Shirts.Length && wp.Hat < LookData.Hats.Length && wp.Pants < LookData.Pants.Length && wp.Hair < LookData.Hairs.Length && wp.Name.Split(' ').Length >= 2);
        // Two people with the same name are told apart.
        var rd = new DockRig(); var d1 = rd.Join(3, 3); var d2 = rd.Join(3, 3); var d3 = rd.Join(3, 3); var d4 = rd.Join(5, 5);
        rd.Step(0.1f, 5);
        var dn = d4.c.Roster.OrderBy(p => p.Id).Select(p => p.Name).ToArray();
        string bn = Leaderboards.NameOf(3, 3);
        Check($"players with the same name are told apart by order of arrival, the same on every screen ({string.Join(", ", dn)})", dn[0] == bn && dn[1] == bn + " 2" && dn[2] == bn + " 3" && dn[3] == Leaderboards.NameOf(5, 5) && d1.c.Roster.OrderBy(p => p.Id).Select(p => p.Name).SequenceEqual(dn));
        // Full dock, wrong version, no hello.
        for (int i = 0; i < 4; i++) rig.Join(i, i);
        rig.Step(0.1f, 3);
        Check("eight can be on the dock", rig.Host.Count == 8);
        var ninth = rig.Join();
        rig.Step(0.1f, 3);
        Check("a ninth is turned away: the dock is full", ninth.c.State == "closed" && ninth.c.Why.Contains("full") && rig.Host.Count == 8);
        var (vh, vc) = MemoryLink.Pair();
        var rig2 = new DockRig();
        rig2.Host.AddLink(vh);
        vc.Send(new NetMsg { t = "dhello", v = 99, n = new[] { 1, 1, 1, 1, 1, 1, 1, 1 } });
        rig2.Host.Tick(0.1f);
        Check("a different protocol version is turned away", rig2.Host.Count == 0 && vc.Receive() is { t: "dbye", why: "version" });
        var (sh, sc) = MemoryLink.Pair();
        var rig3 = new DockRig(); rig3.Host.AddLink(sh);
        rig3.T = 10; rig3.Host.Tick(0.1f);
        Check("someone who connects and never says hello is dropped", rig3.Host.Count == 0 && !sh.Alive);
        // A roster of eight fits a frame with room to spare.
        var list = new List<int>(); foreach (var p in rig.Host.Players) p.WriteTo(list);
        var frame = Wire.Frame(new NetMsg { t = "roster", n = list.ToArray() });
        Check($"a full roster is {frame?.Length} bytes: well inside the {Wire.MaxFrame} byte limit", frame != null && frame.Length < Wire.MaxFrame / 2);
        yield return 1;

        // --- Poses ---
        Note("Dock net: poses and phrases");
        var rg = new DockRig();
        var p1 = rg.Join(0, 0); var p2 = rg.Join(1, 1);
        rg.Step(0.1f, 3);
        p1.c.SetPose(295, 202, 1, 2);
        rg.Step(0.15f, 3);
        var seen = p2.c.Roster.First(p => p.Id == p1.c.MyId);
        Check("a pose reaches the others: where, facing, and which step of the walk", MathF.Abs(seen.TX - 295) < 20 && seen.Face == 1 && seen.Step == 2);
        // Walk to a far corner the claimed way: speed-limited, then clamped to the dock.
        p1.c.SetPose(1000, 1000, 2, 1);
        rg.Step(0.15f, 3);
        var host1 = rg.Host.Players.First(p => p.Id == p1.c.MyId);
        Check("a pose claiming to be somewhere impossible is held to the dock", host1.X <= DockRules.X1 && host1.Y <= DockRules.Y1 && host1.X >= DockRules.X0 && host1.Y >= DockRules.Y0);
        // Teleporting: a big jump moves only as far as a person can walk since the last pose.
        var rg2 = new DockRig();
        var q1 = rg2.Join(); rg2.Step(0.1f, 3);
        float startX = rg2.Host.Players.First().X;
        q1.c.SetPose(268, 200, 3, 0); rg2.Step(0.15f, 1);
        float afterX = rg2.Host.Players.First().X;
        Check($"a jump across the dock in one pose moves only a walking distance ({startX:0.0} to {afterX:0.0})", MathF.Abs(afterX - startX) < 20);
        // Pose floods and bad poses.
        var rg3 = new DockRig(); var f1 = rg3.Join(); rg3.Step(0.1f, 3);
        for (int i = 0; i < 300; i++) f1.clientEnd.Send(new NetMsg { t = "pose", n = new[] { 560, 520, 1, 1 } });
        for (int i = 0; i < 20; i++) rg3.Step(0.01f);
        Check("a flood of poses is rate-limited and the dock keeps working", rg3.Host.Count == 1 && f1.c.State == "in");
        f1.clientEnd.Send(new NetMsg { t = "pose", n = new[] { 1, 2, 3 } }); f1.clientEnd.Send(new NetMsg { t = "pose" }); f1.clientEnd.Send(new NetMsg { t = "pose", n = new[] { int.MaxValue, int.MinValue, 99, -5 } });
        rg3.Step(0.3f, 3);
        var hp = rg3.Host.Players.First();
        Check("short, missing and extreme pose numbers change nothing wrong", hp.X >= DockRules.X0 && hp.X <= DockRules.X1 && hp.Y >= DockRules.Y0 && hp.Y <= DockRules.Y1 && hp.Face is >= 0 and < 4 && hp.Step is >= 0 and <= 4);
        // Phrases.
        var re = new DockRig(); var e1 = re.Join(); var e2 = re.Join(); var e3 = re.Join(); re.Step(0.1f, 4);
        e1.c.Emote(2); re.Step(0.1f, 3);
        Check("a phrase reaches everyone else but not the sender, with the sender's id", e2.c.Events.Any(x => x.Kind == "emote" && x.From == e1.c.MyId && x.Mode == 2) && e3.c.Events.Any(x => x.Kind == "emote") && !e1.c.Events.Any(x => x.Kind == "emote"));
        Check("and it shows over them for a few seconds as the listed words", e2.c.Roster.First(p => p.Id == e1.c.MyId).Emote == DockRules.Emotes[2]);
        e2.c.Events.Clear(); e3.c.Events.Clear();
        e1.c.Emote(3); e1.c.Emote(4); e1.c.Emote(5); re.Step(0.1f, 3);
        Check("a second phrase right away is dropped (one every two seconds)", !e2.c.Events.Any(x => x.Kind == "emote" && x.Mode != 3 && x.Mode != 2) || e2.c.Events.Count(x => x.Kind == "emote") <= 1);
        e2.c.Events.Clear();
        re.T += 3; e1.clientEnd.Send(new NetMsg { t = "emote", n = new[] { 99 } }); e1.clientEnd.Send(new NetMsg { t = "emote", n = new[] { -1 } }); e1.clientEnd.Send(new NetMsg { t = "emote", from = e3.c.MyId, n = new[] { 1 } }); re.Step(0.1f, 3);
        Check("a phrase that isn't on the list is ignored, and nobody can speak as someone else", e2.c.Events.Count(x => x.Kind == "emote") == 1 && e2.c.Events.First(x => x.Kind == "emote").From == e1.c.MyId && e3.c.Events.All(x => x.From != e3.c.MyId));
        yield return 1;

        // --- Challenges ---
        Note("Dock net: challenges and a routed match");
        var rc = new DockRig();
        var A = rc.Join(1, 1); var B = rc.Join(2, 2); var C = rc.Join(3, 3); var D = rc.Join(4, 4);
        rc.Step(0.1f, 4);
        A.c.Invite(B.c.MyId, 0); rc.Step(0.1f, 3);
        var inv = B.c.Events.FirstOrDefault(x => x.Kind == "invite");
        Check("a challenge reaches its target with who and which game", inv != null && inv.From == A.c.MyId && inv.Mode == 0 && A.c.Events.Any(x => x.Kind == "invsent"));
        Check("and nobody else hears of it", !C.c.Events.Any(x => x.Kind == "invite") && !D.c.Events.Any(x => x.Kind == "invite"));
        // One at a time.
        C.c.Invite(B.c.MyId, 1); rc.Step(0.1f, 3);
        Check("a second challenge to someone already challenged is refused (busy)", C.c.Events.Any(x => x.Kind == "cancel" && x.Why == "busy") && !B.c.Events.Any(x => x.Kind == "invite" && x.From == C.c.MyId));
        B.c.Invite(D.c.MyId, 0); rc.Step(0.1f, 3);
        Check("someone with a challenge waiting can't start another", B.c.Events.Any(x => x.Kind == "cancel" && x.Why == "busy"));
        A.c.Invite(A.c.MyId, 0); rc.Step(0.1f, 3);
        A.c.Invite(77, 0); rc.Step(0.1f, 3);
        Check("a challenge to yourself or to nobody does nothing", A.c.Events.Count(x => x.Kind == "cancel" && x.Why == "gone") >= 1);
        // Decline frees both.
        B.c.Reply(inv.Ch, false); rc.Step(0.1f, 3);
        Check("declining ends it for both", A.c.Events.Any(x => x.Kind == "cancel" && x.Why == "declined") && rc.Host.Pending == 0);
        // Expiry.
        rc.T += 3; C.c.Events.Clear();
        C.c.Invite(D.c.MyId, 0); rc.Step(0.1f, 3);
        rc.T += DockRules.InviteSecs + 1; rc.Step(0.1f, 2);
        Check("an unanswered challenge expires for both", C.c.Events.Any(x => x.Kind == "cancel" && x.Why == "expired") && D.c.Events.Any(x => x.Kind == "cancel" && x.Why == "expired") && rc.Host.Pending == 0);
        // Accept: a sungka game over the dock.
        rc.T += 3;
        A.c.Events.Clear(); B.c.Events.Clear();
        A.c.Invite(B.c.MyId, 0); rc.Step(0.1f, 3);
        inv = B.c.Events.First(x => x.Kind == "invite");
        // Only the target can answer.
        C.c.Reply(inv.Ch, true); rc.Step(0.1f, 2);
        Check("someone who wasn't asked can't accept for the target", !A.c.Events.Any(x => x.Kind == "go"));
        B.c.Reply(inv.Ch, true); rc.Step(0.1f, 3);
        var goA = A.c.Events.FirstOrDefault(x => x.Kind == "go"); var goB = B.c.Events.FirstOrDefault(x => x.Kind == "go");
        Check("accepting starts the match: both get the channel, the challenger as referee (role 0), the target as guest (1)", goA != null && goB != null && goA.Ch == goB.Ch && goA.Role == 0 && goB.Role == 1 && goA.Link != null && goB.Link != null && goA.From == B.c.MyId && goB.From == A.c.MyId);
        Check("both are marked in a match on everyone's roster", C.c.Roster.First(p => p.Id == A.c.MyId).State == 1 && C.c.Roster.First(p => p.Id == B.c.MyId).State == 1 && C.c.Roster.First(p => p.Id == D.c.MyId).State == 0);
        var refSes = new SungkaSession("Alpha"); refSes.AttachGuest(goA.Link);
        var guestSes = new SungkaSession("Beta", goB.Link);
        void Pump(int n = 6) { for (int i = 0; i < n; i++) { rc.Step(0.02f); refSes.Tick(0.02f); guestSes.Tick(0.02f); } }
        Pump(10);
        Check("a sungka game starts over the routed link: names, seats and one match id", refSes.State == "play" && guestSes.State == "play" && refSes.RivalName == "Beta" && guestSes.RivalName == "Alpha" && refSes.MatchId == guestSes.MatchId);
        var rr = new Random(5);
        for (int n = 0; n < 400 && refSes.State == "play"; n++)
        {
            var mover = refSes.Board.Turn == 0 ? refSes : guestSes;
            var legal = Enumerable.Range(0, 16).Where(p => mover.Board.CanMove(mover.MySeat, p)).ToList();
            if (mover.MyTurn && legal.Count > 0) mover.Move(legal[rr.Next(legal.Count)]);
            Pump(4);
        }
        Pump(10);
        Check($"a whole game through the dock ends the same on both sides ({refSes.MyScore} to {refSes.RivalScore})", refSes.State == "over" && guestSes.State == "over" && refSes.Board.Digest() == guestSes.Board.Digest() && refSes.MyScore + refSes.RivalScore == 98);
        // Hostile routes.
        D.clientEnd.Send(new NetMsg { t = "route", ch = goA.Ch, data = Wire.ToText(new NetMsg { t = "bye", why = "left" }) }); Pump(4);
        Check("a player outside the match can't send into its channel", guestSes.State == "over" && guestSes.EndWhy == "finished" && !guestSes.PeerGone);
        A.clientEnd.Send(new NetMsg { t = "route", ch = goA.Ch, data = new string('x', DockRules.MaxRoute + 1) }); Pump(3);
        A.clientEnd.Send(new NetMsg { t = "route", ch = 999, data = "{}" }); A.clientEnd.Send(new NetMsg { t = "route", ch = goA.Ch, data = "not json" }); Pump(3);
        Check("oversized, unknown-channel and unreadable routed messages are dropped and nothing breaks", rc.Host.Count == 4 && A.c.State == "in");
        // The match is over: done frees both and the dock goes on.
        A.c.Done(goA.Ch); Pump(4);
        Check("done frees both players for new challenges", rc.Host.Pending == 0 && rc.Host.Players.All(p => p.State == 0));
        Check("and the other side is told the channel is closed", B.c.Events.Any(x => x.Kind == "cancel" && x.Ch == goA.Ch) && !guestSes.Link.Alive);
        // Someone leaves mid-match.
        rc.T += 3;
        C.c.Invite(D.c.MyId, 0); rc.Step(0.1f, 3);
        D.c.Reply(D.c.Events.Last(x => x.Kind == "invite").Ch, true); rc.Step(0.1f, 3);
        var goC = C.c.Events.Last(x => x.Kind == "go"); var goD = D.c.Events.Last(x => x.Kind == "go");
        var sC = new SungkaSession("C"); sC.AttachGuest(goC.Link); var sD = new SungkaSession("D", goD.Link);
        for (int i = 0; i < 10; i++) { rc.Step(0.02f); sC.Tick(0.02f); sD.Tick(0.02f); }
        Check("a second match is running between the other two", sC.State == "play" && sD.State == "play");
        D.c.Close("test");
        for (int i = 0; i < 10; i++) { rc.Step(0.02f); sC.Tick(0.02f); }
        Check("when one leaves the dock mid-match the other's game ends as a win by forfeit", sC.State == "over" && sC.Result == "win" && sC.EndWhy == "left");
        Check("and the dock forgets the leaver and frees the challenger", rc.Host.Count == 3 && rc.Host.Pending == 0 && C.c.Roster.All(p => p.Id != D.c.MyId));
        // The host goes away.
        rc.Host.Close();
        rc.Step(0.1f, 3);
        Check("when the host closes the dock everyone is told", rc.Clients.Where(x => x.c.State == "closed").Count() >= 3 && A.c.Why.Contains("closed"));
        yield return 1;

        // --- Review round ---
        Note("Dock net: review round");
        // A burst of poses at one instant can't add up to a run.
        var rb = new DockRig(); var bp = rb.Join(); rb.Step(0.1f, 3);
        float bx0 = rb.Host.Players.First().X;
        for (int i = 0; i < 8; i++) bp.clientEnd.Send(new NetMsg { t = "pose", n = new[] { 1000, 520, 1, 1, 0 } });
        rb.Step(0.001f, 2);
        float bx1 = rb.Host.Players.First().X;
        Check($"eight poses at the same instant move a person no further than a few steps ({bx1 - bx0:0.0} px)", bx1 - bx0 <= 11);
        // An answer to a challenge after it has expired starts nothing.
        var rx = new DockRig(); var xa = rx.Join(); var xb = rx.Join(); rx.Step(0.1f, 4);
        xa.c.Invite(xb.c.MyId, 0); rx.Step(0.1f, 2);
        int xch = xb.c.Events.First(x => x.Kind == "invite").Ch;
        rx.T += DockRules.InviteSecs + 2;
        xb.clientEnd.Send(new NetMsg { t = "reply", ch = xch, n = new[] { 1 } });
        rx.Step(0.01f, 2);
        Check("an acceptance that arrives after the invitation expired starts no match", !xa.c.Events.Any(x => x.Kind == "go") && !xb.c.Events.Any(x => x.Kind == "go") && rx.Host.Pending == 0);
        yield return 1;
        // --- A hostile host ---
        Note("Dock net: a hostile host");
        var (hh, hc) = MemoryLink.Pair();
        var victim = new DockClientCore(hc, 0, 0, new Look());
        hh.Send(new NetMsg { t = "dwelcome", from = 5, v = DockRules.Protocol });
        victim.Tick(0.1f);
        bool threw = false;
        try
        {
            foreach (var bad in new[]
            {
                new NetMsg { t = "roster", n = new int[] { 1, 2, 3 } }, new NetMsg { t = "roster" },
                new NetMsg { t = "roster", n = Enumerable.Repeat(-1, 14 * 8).ToArray() },
                new NetMsg { t = "roster", n = new int[14 * 9] }, new NetMsg { t = "roster", n = Enumerable.Repeat(int.MaxValue, 28).ToArray() },
                new NetMsg { t = "emote", from = 1, n = new[] { 500 } }, new NetMsg { t = "invite", ch = 1, from = 2, n = new[] { 99 } },
                new NetMsg { t = "go", ch = 1, n = new[] { 7, 7 } }, new NetMsg { t = "go", ch = 2 }, new NetMsg { t = "route", ch = 77, data = null },
                new NetMsg { t = "cancel", ch = -1 }, new NetMsg { t = "invsent" }, new NetMsg { t = "mystery" }
            }) { hh.Send(bad); victim.Tick(0.1f); }
        }
        catch (Exception) { threw = true; }
        Check("malformed roster, phrase, challenge and route messages from the host change nothing and never crash", !threw && victim.State == "in" && victim.Roster.All(p => p.Adj >= 0 && p.Adj < Leaderboards.Adjectives && p.TX >= DockRules.X0 && p.TX <= DockRules.X1 && p.TY >= DockRules.Y0 && p.TY <= DockRules.Y1) && victim.Roster.Count <= DockRules.MaxPlayers);
        hh.Send(new NetMsg { t = "go", ch = 4, to = 2, n = new[] { 0, 1 } }); victim.Tick(0.1f);
        Check("a match the host starts for a game that exists is accepted as an event the game decides on", victim.Events.Any(x => x.Kind == "go" && x.Mode == 1));
        hc.Quiet = DockRules.SilentLimit + 1; victim.Tick(0.1f);
        Check("a host that goes quiet closes the client's dock", victim.State == "closed" && victim.Why.Contains("answer"));
        yield return 1;

        // --- Real sockets ---
        Note("Dock net: over real sockets");
        using var dockLan = new LanHost("Dock host", "dock", 47866, 47867);
        var rigHost = new DockHostCore();
        var (loopA, loopB) = MemoryLink.Pair();
        rigHost.AddLink(loopA);
        var hostPlayer = new DockClientCore(loopB, 1, 1, new Look());
        var conn = new LanConnect("127.0.0.1", dockLan.Port);
        ILink accepted = null, joined = null;
        for (int i = 0; i < 300 && (accepted == null || joined == null); i++) { accepted ??= dockLan.Poll(); joined ??= conn.Poll(); yield return 1; }
        Check("a guest reaches the dock host's port", accepted != null && joined != null);
        rigHost.AddLink(accepted);
        var guestPlayer = new DockClientCore(joined, 2, 2, new Look { shirt = 2 });
        for (int i = 0; i < 90; i++) { hostPlayer.Tick(1f / 60); guestPlayer.Tick(1f / 60); rigHost.Tick(1f / 60); yield return 1; }
        Check("both see each other over the real connection", hostPlayer.State == "in" && guestPlayer.State == "in" && hostPlayer.Roster.Count == 2 && guestPlayer.Roster.Count == 2 && guestPlayer.Others.First().Name == Leaderboards.NameOf(1, 1));
        guestPlayer.SetPose(275, 198, 3, 2); guestPlayer.Emote(6);
        for (int i = 0; i < 60; i++) { hostPlayer.Tick(1f / 60); guestPlayer.Tick(1f / 60); rigHost.Tick(1f / 60); yield return 1; }
        var gp = hostPlayer.Roster.First(p => p.Id == guestPlayer.MyId);
        Check("a pose and a phrase cross it: where the guest is and what they said", MathF.Abs(gp.TX - 275) < 2 && gp.Face == 3 && gp.Emote == DockRules.Emotes[6]);
        guestPlayer.Close("bye");
        for (int i = 0; i < 40; i++) { hostPlayer.Tick(1f / 60); rigHost.Tick(1f / 60); yield return 1; }
        Check("closing the guest's connection takes them off the dock", rigHost.Count == 1 && hostPlayer.Roster.Count == 1);
        rigHost.Close(); dockLan.Dispose();
        yield return 2;
    }
}
#endif
