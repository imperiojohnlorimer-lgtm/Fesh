using Raylib_cs;

namespace Fesh;

// A pet (1.45): Biscuit the dog on Saltmere, or one of the island cats, adopted after you've made friends with it on three
// different days. It's the same animal it always was (Survival.cs), with a new way of moving: it trots after you on foot,
// waits where you leave it when you go indoors, aboard or on Tidemane, and turns up beside you again the next time you're
// on foot far from it. F tells it to stay or calls it. It lies down beside you at night, is glad when you land something
// special, and once a day it finds you something.
sealed class PetData { public string kind, key, name; public float x, y; public bool follow = true; public int foundDay = -1; public float walked; }

partial class Game
{
    internal static bool PetsOff;   // the old scripts' animals stay as they were (AutoTestStart); PetScript turns it on
    const int AdoptDays = 3;
    const float PetGap = 14, PetSpeed = 66, PetFar = 220, FindAfter = 180;
    static readonly string[] DogNames = { "Biscuit", "Bantay", "Kulot", "Mango", "Tisoy", "Ube", "Bibo", "Choco" };
    static readonly string[] CatNames = { "Mingming", "Muning", "Tagpi", "Kiki", "Ube", "Mango" };

    bool HasPet => !PetsOff && state?.pet != null;
    bool IsPet(Animal a) => HasPet && a.Key == state.pet.key;
    Animal PetAnimal => HasPet ? animals.FirstOrDefault(a => a.Key == state.pet.key) : null;
    string PetName => state.pet?.name ?? "";

    // Petting on a new day counts toward adopting it (dogs and cats, before you have a pet).
    void NotePetted(Animal a)
    {
        if (PetsOff || state.pet != null || a.Kind is not ("dog" or "cat") || MatchActive) return;
        string k = "pet:" + a.Key;
        if (state.gifts.GetValueOrDefault(k + ":day") == state.day) return;
        state.gifts[k + ":day"] = state.day;
        state.gifts[k] = state.gifts.GetValueOrDefault(k) + 1;
        // How the friendship is going, over its head (Codex: show the progress).
        if (state.gifts[k] < AdoptDays) Floater($"Friends: {state.gifts[k]} of {AdoptDays} days", a.X, a.Y - 16, "#ffd6d0");
        if (state.gifts[k] == AdoptDays) ToastLater($"The {(a.Kind == "dog" ? "dog" : "cat")} follows you a few steps whenever you leave. Press <alt> beside it to take it home.");
    }

    bool CanAdopt(Animal a) => !PetsOff && state.pet == null && a.Kind is "dog" or "cat" && state.gifts.GetValueOrDefault("pet:" + a.Key) >= AdoptDays && !MatchActive;

    /* ---------- Naming ---------- */
    Animal adopting;
    int nameAt;

    void StartAdopt(Animal a)
    {
        adopting = a;
        nameAt = 0;
        Sfx.Play("pet");
        panel = "petname"; mode = "panel"; SetPrompt("");
    }

    void DrawPetName()
    {
        if (adopting == null) { ClosePanels(); return; }
        var names = adopting.Kind == "dog" ? DogNames : CatNames;
        var (x, y, w, h) = PanelFrame(adopting.Kind == "dog" ? "A dog of your own" : "A cat of your own", "Pick a name. It comes along with you from now on.", 640, 330);
        if (PanelClose()) { adopting = null; ClosePanels(); return; }
        float cx = x + w / 2, ny = y + 140;
        if (Button("<", cx - 170, ny, 50, 56, FontKind.Ui700, 26, Pal.Sand, Pal.Ink, 4)) { nameAt = (nameAt + names.Length - 1) % names.Length; Sfx.Play("ui"); }
        if (Button(">", cx + 120, ny, 50, 56, FontKind.Ui700, 26, Pal.Sand, Pal.Ink, 4)) { nameAt = (nameAt + 1) % names.Length; Sfx.Play("ui"); }
#if DEBUG
        Gfx.Seen["petname:prev"] = new Rectangle(cx - 170, ny, 50, 56);
        Gfx.Seen["petname:next"] = new Rectangle(cx + 120, ny, 50, 56);
#endif
        Gfx.Box(cx - 110, ny, 220, 56, CardBg, Pal.Ink, 3, 6);
        Gfx.TextCenter(names[nameAt], cx, ny + 14, FontKind.Ui700, 26, Pal.PaperInk);
        if (BigButton("That's the one", cx - BigW("That's the one") / 2, y + h - 90, true)) { Adopt(adopting, names[nameAt]); return; }
    }

    void Adopt(Animal a, string name)
    {
        state.pet = new PetData { kind = a.Kind, key = a.Key, name = name, x = a.X, y = a.Y };
        a.HomeX = a.X; a.HomeY = a.Y; a.Asleep = false; a.Hearts = 2;
        adopting = null;
        ClosePanels();
        Sfx.Play("levelup");
        Save();
        Celebrate(new Celeb { Kind = "pet", Icon = "star", Sting = "badge", Tag = "A friend for life", Title = $"{name} comes home with you", Sub = $"{name} follows you on foot. <alt> beside {(a.Kind == "dog" ? "him" : "her")} to say stay or come." },
            () => Toast($"{name} is yours now. {name} follows you on foot; <alt> says stay or come.", 5));
    }

    /* ---------- Moving ---------- */
    // UpdateAnimals: the pet follows you instead of wandering.
    void UpdatePet(Animal a, float dt)
    {
        var p = state.pet;
        a.Hearts = Math.Max(0, a.Hearts - dt);
        bool onFoot = scene == "world" && !Aboard && !Riding && mode != "title";
        // Far from you (you sailed or rode off, or came out of a door elsewhere): it turns up beside you, like it came along.
        if (onFoot && p.follow && Dist(a.X, a.Y, player.X, player.Y) > PetFar && PetSpotNear() is (float sx, float sy))
        {
            a.X = sx; a.Y = sy; a.Vx = a.Vy = 0; a.Asleep = false;
        }
        float d = Dist(a.X, a.Y, player.X, player.Y);
        if (!onFoot || !p.follow || d <= PetGap)
        {
            a.Vx = a.Vy = 0;
            // At night, settled beside you while you stand about, it lies down.
            a.Asleep = Night && onFoot && d <= PetGap + 6 && player.WalkT == lastPetWalk && a.Hearts <= 0;
        }
        else
        {
            a.Asleep = false;
            float sp = MathF.Min(PetSpeed, 30 + (d - PetGap) * 2.2f);
            float ux = (player.X - a.X) / d, uy = (player.Y - a.Y) / d;
            a.Vx = ux * sp; a.Vy = uy * sp;
            float nx = a.X + a.Vx * dt, ny = a.Y + a.Vy * dt, ox = a.X, oy = a.Y;
            // Each step where it could stand, and never into a closed zone (Codex).
            if (CanStand(nx, a.Y) && Restriction(nx, a.Y) == null) a.X = nx;
            if (CanStand(a.X, ny) && Restriction(a.X, ny) == null) a.Y = ny;
            if (a.Vx != 0) a.Flip = a.Vx < 0;
            bool moved = Dist(a.X, a.Y, ox, oy) > 0.01f;
            // Only real walking together counts toward its find (Codex: not pushing against a tree).
            if (moved) { p.walked += dt; petStuck = 0; }
            // Stuck behind something (a tree, a bridge a storm closed): after a few seconds it finds its way round to you.
            else if ((petStuck += dt) > 3 && PetSpotNear() is (float jx, float jy)) { a.X = jx; a.Y = jy; petStuck = 0; }
        }
        lastPetWalk = player.WalkT;
        p.x = a.X; p.y = a.Y;
        a.HomeX = a.X; a.HomeY = a.Y;
        // Once a day, after a while on the road together, it finds you something.
        if (p.follow && onFoot && p.walked >= FindAfter && p.foundDay != state.day) PetFinds(a);
    }
    float lastPetWalk, petStuck;

    // An open spot just behind you (never in the water, never on a closed bridge).
    (float x, float y)? PetSpotNear()
    {
        float bx = player.Face == "left" ? 1 : player.Face == "right" ? -1 : 0, by = player.Face == "up" ? 1 : player.Face == "down" ? -1 : 0;
        if (bx == 0 && by == 0) by = 1;
        foreach (var (ox, oy) in new[] { (bx * 12, by * 12), (-by * 12, bx * 12), (by * 12, -bx * 12), (bx * 20, by * 20), (-bx * 12, -by * 12) })
            if (CanStand(player.X + ox, player.Y + oy) && !InWaterAt(player.X + ox, player.Y + oy) && Restriction(player.X + ox, player.Y + oy) == null) return (player.X + ox, player.Y + oy);
        return null;
    }

    bool InWaterAt(float x, float y) => Wadeable(TileAt((int)MathF.Floor(x / T), (int)MathF.Floor((y - 1.5f) / T)));

    void PetFinds(Animal a)
    {
        var p = state.pet;
        p.foundDay = state.day;
        p.walked = 0;
        string what;
        if (p.kind == "dog") { Give("worm", 2); what = $"{p.name} digs about and turns up 2 worms."; }
        else { Give("cricket", 1); what = $"{p.name} pounces, and brings you a cricket."; }
        a.Hearts = 1.5f;
        Sfx.Play("pet");
        Toast(what, 3.5f);
        Save();
    }

    // A rare fish, a trophy or a record: it's as pleased as you are (LandCatch).
    void PetCheers()
    {
        if (PetAnimal is not Animal a || Dist(a.X, a.Y, player.X, player.Y) > 60) return;
        a.Hearts = 2;
        a.Asleep = false;
        Floater(state.pet.kind == "dog" ? "Woof!" : "Mrrp!", a.X, a.Y - 14, "#ffd6d0");
    }

    /* ---------- Targets ---------- */
    // The pet: E pets it, <alt> says stay or come. Asked for last of all (after the fishing spots), so it never takes E
    // from anything else just by trotting along beside you.
    Target PetTarget()
    {
        if (PetAnimal is not Animal a || scene != "world" || Aboard || Riding) return null;
        if (Dist(a.X, a.Y, player.X, player.Y) > 16) return null;
        return new Target { Type = "animal", Ref = a, Label = $"Pet {PetName}", AltType = "petstay", AltLabel = state.pet.follow ? $"Tell {PetName} to stay" : $"Call {PetName}" };
    }

    void TogglePetStay()
    {
        if (!HasPet) return;
        state.pet.follow = !state.pet.follow;
        Sfx.Play("ui");
        Toast(state.pet.follow ? $"{PetName} trots over, tail up." : $"{PetName} sits and waits for you.", 2.5f);
        Save();
    }

    // SpawnAnimals: the pet goes back where it was (or beside you, if that isn't somewhere it can stand any more).
    void PlacePet()
    {
        if (!HasPet || PetAnimal is not Animal a) return;
        var p = state.pet;
        if (p.x != 0 && CanStand(p.x, p.y)) { a.X = p.x; a.Y = p.y; }
        else if (PetSpotNear() is (float sx, float sy)) { a.X = sx; a.Y = sy; }
        a.HomeX = a.X; a.HomeY = a.Y;
    }
}
