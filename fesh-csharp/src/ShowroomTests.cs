#if DEBUG
namespace Fesh;

partial class Game
{
    // The dock showroom's messages (1.46, FESH_SHOWROOM_TEST=1): a card is four small numbers, clamped by the host and sent
    // under the sender's own id; a newcomer gets the cards already given; a wrong-sized or hostile card is ignored; and what the
    // game turns the numbers into can only be a title or fish from its fixed lists (nothing out of range, no legends).
    IEnumerable<int> ShowroomNetScript()
    {
        Note("Showroom messages");
        var rig = new DockRig();
        var a = rig.Join(1, 2); var b = rig.Join(3, 4);
        rig.Step(0.1f, 5);
        a.c.SendProfile(new[] { 2, 5, 3, 40, 1, 0, 0, 0 });
        rig.Step(0.1f, 5);
        Check("a card reaches everyone under its sender's id", b.c.Profiles.TryGetValue(a.c.MyId, out var pa) && pa.SequenceEqual(new[] { 2, 5, 3, 40, 1, 0, 0, 0 }) && a.c.Profiles.ContainsKey(a.c.MyId));
        Check("one nobody sent isn't there", !b.c.Profiles.ContainsKey(b.c.MyId));
        a.c.SendProfile(new[] { 99999, -5, 99, 1 << 30, 70000, -1, 9999, 1 << 30 });
        rig.Step(0.1f, 5);
        Check($"wild numbers are clamped by the host ({string.Join(",", b.c.Profiles[a.c.MyId])})", b.c.Profiles[a.c.MyId].SequenceEqual(new[] { 255, 0, 10, 999, 255, 0, 255, 255 }));
        a.c.SendProfile(new[] { 1, 1, 1, 1 });
        a.c.SendProfile(new int[0]);
        rig.Step(0.1f, 5);
        Check("a card of the wrong length is ignored", b.c.Profiles[a.c.MyId].SequenceEqual(new[] { 255, 0, 10, 999, 255, 0, 255, 255 }));
        // Nobody can send a card for someone else: the message has no id, the host uses the connection's.
        b.c.SendProfile(new[] { 1, 1, 1, 1, 0, 0, 0, 0 });
        rig.Step(0.1f, 5);
        Check("each card is its sender's own", a.c.Profiles[b.c.MyId].SequenceEqual(new[] { 1, 1, 1, 1, 0, 0, 0, 0 }) && a.c.Profiles[a.c.MyId].SequenceEqual(new[] { 255, 0, 10, 999, 255, 0, 255, 255 }));
        var late = rig.Join(7, 7);
        rig.Step(0.1f, 6);
        Check("someone who arrives later is given the cards already shown", late.c.Profiles.Count == 2 && late.c.Profiles.ContainsKey(a.c.MyId) && late.c.Profiles.ContainsKey(b.c.MyId));
        Check("a profile sent before joining is dropped", ((Func<bool>)(() => { var (h, c) = MemoryLink.Pair(); var rig2 = new DockRig(); rig2.Host.AddLink(h); c.Send(new NetMsg { t = "dprofile", n = new[] { 1, 1, 1, 1, 0, 0, 0, 0 } }); rig2.Step(); return rig2.Host.Count == 0; }))());
        // What a visitor says they wear: only real things for the right slot, and it only changes how they look.
        int Ix(string id) => Array.IndexOf(Wardrobe.Catalog, Wardrobe.ById[id]) + 1;
        var vl = ApplyProfileLook(new Look { name = "v", hat = 1, shirt = 2 }, new[] { 0, 0, 0, 0, Ix("shirt_teal"), Ix("pants_moss"), Ix("hair_copper"), Ix("hat_salakot") });
        Check("a visitor's wardrobe things show on them", vl.shirtId == "shirt_teal" && vl.pantsId == "pants_moss" && vl.hairId == "hair_copper" && vl.hatId == "hat_salakot" && vl.hat == 6);
        var wl = ApplyProfileLook(new Look { name = "v", hat = 1 }, new[] { 0, 0, 0, 0, Ix("pants_moss"), 9999, 0, Ix("shirt_teal") });
        Check("a thing for the wrong slot, an index out of range or none are ignored", wl.shirtId == null && wl.pantsId == null && wl.hatId == null && wl.hat == 1);
        Check("no card at all leaves the creator's look alone", ApplyProfileLook(new Look { hat = 2, shirt = 4 }, null).shirt == 4 && ApplyProfileLook(new Look { hat = 2 }, new[] { 1, 2, 3 }).hat == 2);
        // What the numbers can mean.
        Check("a title index outside the list is no title; so is 0", TitleAt(0) == null && TitleAt(-1) == null && TitleAt(PublicTitles.Length + 1) == null && TitleAt(1) == PublicTitles[0]);
        Check("a favourite index outside the list is no fish", FavAt(0) == null && FavAt(ShowcaseFish.Length + 1) == null && FavAt(1) == ShowcaseFish[0]);
        Check("no legend can be a favourite, and every favourite has a picture", ShowcaseFish.All(id => !Data.FishById[id].Legend && FishArt.Looks.ContainsKey(id)));
        Check("the title list holds every title an achievement gives, once", PublicTitles.Length == PublicTitles.Distinct().Count() && Achievements.Where(a => a.Title != null).All(a => PublicTitles.Contains(a.Title)));
        // My own card, from a save.
        var s = new State { created = true, look = new Look { name = "Me" }, xp = XpFor(10) + MasteryTotal(2) };
        s.commons["pond_perch"] = 2; s.commons["mud_carp"] = 1; s.records["pond_perch"] = 0.5f; s.favFish = "pond_perch";
        s.hinted["ach:fish100"] = true; s.title = "Angler";
        Check("my card says my title, favourite, mastery and kinds", WornTitleOf(s) == "Angler" && MasteryOf(s) == 2 && KindsOf(s) == 2 && Array.IndexOf(ShowcaseFish, s.favFish) >= 0);
        s.title = "Master angler";
        Check("a title I haven't earned isn't shown", WornTitleOf(s) == null);
        yield return 1;
    }
}
#endif
