using Raylib_cs;

namespace Fesh;

sealed class Animal { public string Kind, Key; public float X, Y, HomeX, HomeY, Vx, Vy, Timer, Pause, Hearts; public bool Flip, Asleep; }
// A speck. With Light off it is the old opaque 1 px square; with light on every particle fades out over the end of its life,
// and the soft ones (Puff) also grow, as dust and smoke do (Life0 is read once when it is first drawn).
sealed class Particle
{
    public float X, Y, Vx, Vy, Life;
    public string Color;
    public float Life0, Size0 = 1, Size1 = 1, Alpha0 = 1, Gy = 140;
    public bool Soft, HasCol;
    public Color Col;
}

partial class Game
{
    readonly List<Animal> animals = new();
    readonly List<Particle> particles = new();
    (int x, int y) chopTile = (-1, -1);
    int chopHits;
    float shakeT, swingT;

    /* ---------- Bag ---------- */
    int Has(string id) => id == "fish" ? FishCount() : id == Items.TinapaFishId ? state.inv.Where(kv => Items.TinapaFish.Contains(kv.Key)).Sum(kv => kv.Value)
        : id == Items.SeaweedId ? Items.Seaweeds.Sum(state.inv.GetValueOrDefault) : state.inv.GetValueOrDefault(id);
    void Give(string id, int n = 1)
    {
        state.inv[id] = state.inv.GetValueOrDefault(id) + n;
        HotbarGained(id);
    }

    bool Take(string id, int n = 1)
    {
        if (state.inv.GetValueOrDefault(id) < n) return false;
        state.inv[id] -= n;
        if (state.inv[id] <= 0) state.inv.Remove(id);
        return true;
    }

    // Raw fish you can cook or cut up. Legendary fish are never used up as an ingredient.
    int FishCount() => state.inv.Where(kv => Items.ById[kv.Key].Kind == "fish" && !Data.FishById[kv.Key].Legend).Sum(kv => kv.Value);

    // Uses up raw fish for cooking, most plentiful ordinary fish first so rare catches are kept. Tinapa only takes the
    // fish that are smoked for tinapa (Items.TinapaFish).
    void TakeFish(int n, bool tinapa = false)
    {
        for (int i = 0; i < n; i++)
        {
            var pick = state.inv.Where(kv => Items.ById[kv.Key].Kind == "fish" && !Data.FishById[kv.Key].Legend && (!tinapa || Items.TinapaFish.Contains(kv.Key)))
                .OrderBy(kv => Data.FishById[kv.Key].Rare ? 1 : 0).ThenByDescending(kv => kv.Value).FirstOrDefault();
            if (pick.Key == null) return;
            Take(pick.Key);
            // Ordinary fish of this kind go first. Any premium left must belong to a fish still in the bag.
            if (state.big.TryGetValue(pick.Key, out int big))
            {
                int remaining = Math.Min(big, Has(pick.Key));
                if (remaining > 0) state.big[pick.Key] = remaining;
                else state.big.Remove(pick.Key);
            }
        }
    }

    string BestRod() => Items.Rods.Last(r => r == "rod_old" || Has(r) > 0);
    RodStats Rod => Items.Rod[BestRod()];
    // 0 with no pickaxe, otherwise 1 (stone) up to 5 (crystal).
    int PickTier
    {
        get
        {
            for (int i = Items.Pickaxes.Length - 1; i >= 0; i--) if (Has(Items.Pickaxes[i]) > 0) return i + 1;
            return 0;
        }
    }
    bool Starving => HungerOn && state.food <= 0;

    /* ---------- Food ---------- */
    // Hunger can be switched off in Settings (1.45): then the meter is gone, nothing makes you hungry and food only heals.
    // The food you had is kept, so switching it back on carries on from there.
    static bool HungerOn => Settings.Data.hunger;
    void SpendFood(float n) { if (HungerOn) state.food = Math.Max(0, state.food - n); }

    // The food meter drops about one point every seven seconds of play.
    void TickFood(float dt)
    {
        if (!HungerOn) return;
        float before = state.food;
        state.food = Math.Max(0, state.food - dt / 7f);
        if (before >= 25 && state.food < 25) Toast("You're getting hungry. Open your bag (<bag>) and eat something.", 4);
        if (before > 0 && state.food <= 0) Toast("You're starving! You walk slower and reeling is harder until you eat.", 4.5f);
    }

    void Eat(string id)
    {
        var d = Items.ById[id];
        if (d.Food <= 0) return;
        if ((state.food >= 99.5f || !HungerOn) && state.hp >= 99.5f) { Toast("You're full."); Sfx.Play("nope"); return; }
        if (!Take(id)) return;
        Learned("eat");
        Tick("eaten");
        float gain = Math.Min(d.Food, 100 - state.food), heal = Math.Min(d.Food / 2f, 100 - state.hp);
        if (HungerOn) state.food = Math.Min(100, state.food + d.Food);
        state.hp = Math.Min(100, state.hp + d.Food / 2f);
        Sfx.Play("eat");
        string what = !HungerOn ? (heal >= 1 ? $"(+{heal:0} health)" : "") : $"(+{gain:0} food{(heal >= 1 ? $", +{heal:0} health" : "")})";
        Toast(d.Kind == "fish" ? $"You eat the {d.Name.ToLowerInvariant()} raw. Not great, but it helps. {what}" : $"You eat the {d.Name.ToLowerInvariant()}. {what}");
        Save();
    }

    /* ---------- Crafting ---------- */
    bool CanCraft(Recipe r) => r.Needs.All(kv => Has(kv.Key) >= kv.Value);

    void Craft(Recipe r)
    {
        if (!CanCraft(r)) { Sfx.Play("nope"); return; }
        foreach (var (id, n) in r.Needs)
        {
            if (id == "fish") TakeFish(n);
            else if (id == Items.TinapaFishId) TakeFish(n, tinapa: true);
            else if (id == Items.SeaweedId)
            {
                // Seaweed from a crab pot goes first; dried guso is worth more at Pip's.
                int left = n;
                foreach (var w in Items.Seaweeds) { int k = Math.Min(left, Has(w)); if (k > 0) Take(w, k); left -= k; }
            }
            else Take(id, n);
        }
        Give(r.Out, r.Count);
        Learned(r.Station is "fire" or "stove" ? "cook" : "craft");
        Tick(r.Station is "fire" or "stove" ? "cooked" : "crafted");
        // Your first bar of a kind: Pip stocks them from now on (Chapters.cs).
        bool firstBar = r.Station == "furnace" && !state.Hinted("tut:smelted:" + r.Out) && !state.Hinted("pipBars") && Items.Shop.Any(s => s.id == r.Out);
        if (r.Station == "furnace") Learned("smelted:" + r.Out);
        var d = Items.ById[r.Out];
        Sfx.Play("craft");
        string tip = r.Out switch
        {
            "axe" => " Face a tree and press <act> to chop it.",
            "pickaxe" => " It breaks boulders, and mines copper in Frostfang Caverns, across the bridge east.",
            "copper_pickaxe" => " Now you can mine iron ore, from the third floor of the caverns down.",
            "iron_pickaxe" => " Now you can mine gold ore.",
            "gold_pickaxe" => " Now you can mine crystal.",
            "crystal_pickaxe" => " Now you can mine abyssite on the Ancient Floor.",
            "copper_sword" or "iron_sword" or "gold_sword" or "crystal_blade" => " Your best weapon is used automatically in the caves.",
            "shell_armor" => " You wear it automatically. Cave monsters hurt less.",
            "plate_armor" => " You wear it automatically. Cave monsters hurt half as much.",
            "glow_bait" => " It's used before plain bait when you cast.",
            "spear" => " At the coral shallows, the atoll lagoon or Daang Pulo's islet reef, press <spear> to spearfish.",
            "crab_pot" => " Press <build> outdoors to set it in shallow water.",
            "bubo" => " Press <build> outdoors to set it in fresh water: the last piece in the build bar.",
            "cut_bait" or "fly_lure" or "spinner_lure" => " Pick it as your bait in the tackle box (<tackle>).",
            "chum" => " At a fishing spot, press <alt> to throw it.",
            "sunglasses" => " You wear them automatically. Look for fish shadows at fishing spots.",
            "fish_finder" or "waders" or "lucky_charm" or "headlamp" or "cooler" => " You use it automatically.",
            _ when Items.Tackle.ContainsKey(r.Out) => " Your best tackle is equipped automatically; change it in the tackle box (<tackle>).",
            "rod_copper" or "rod_iron" or "rod_crystal" => " Your best rod is used automatically.",
            _ => ""
        };
        Toast($"Made a {d.Name.ToLowerInvariant()}.{tip}", tip == "" ? 2.2f : 4.5f);
        // Queued, so making a second one straight away doesn't hide it.
        if (firstBar) ToastLater($"Your first {d.Name.ToLowerInvariant()}! Pip will keep them on the stall now, for when you'd rather buy them.");
        Save();
    }

    /* ---------- Chopping, mining and picking ---------- */
    static string TreeName(char k) => k switch { 't' => "oak", 'f' => "fir", 'h' => "palm", 'c' => "cactus", 'R' => "boulder", 'y' => "berry bush", _ => "tree" };

    void Burst(float x, float y, string color, int n)
    {
        for (int i = 0; i < n; i++)
            particles.Add(new Particle { X = x, Y = y, Vx = Rand(-30, 30), Vy = Rand(-50, -15), Life = Rand(0.3f, 0.6f), Color = color });
    }

    const int MaxParticles = 400;

    void UpdateParticles(float dt)
    {
        shakeT = Math.Max(0, shakeT - dt);
        swingT = Math.Max(0, swingT - dt);
        quake = Math.Max(0, quake - dt);
        screenFlash = Math.Max(0, screenFlash - dt * 4);
        UpdateCatchFx(dt);
        if (particles.Count > MaxParticles) particles.RemoveRange(0, particles.Count - MaxParticles);   // the oldest go first
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            var p = particles[i];
            p.Life -= dt;
            if (p.Life <= 0) { particles.RemoveAt(i); continue; }
            p.Vy += p.Gy * dt;
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
        }
    }

    void DrawParticles()
    {
        if (LightMode == 0) { foreach (var p in particles) pix.Rect(p.X, p.Y, 1, 1, p.Color); return; }
        foreach (var p in particles)
        {
            if (!p.HasCol) { p.Col = Pal.C(p.Color ?? "#ffffff"); p.HasCol = true; }
            if (p.Life0 <= 0) p.Life0 = p.Life;
            float k = Math.Clamp(p.Life / p.Life0, 0, 1);
            float a = p.Alpha0 * (k > 0.55f ? 1 : k / 0.55f);   // solid for the first half, then gone by the end
            SoftDot(p.X, p.Y, p.Size0 + (p.Size1 - p.Size0) * (1 - k), p.Col, a);
        }
    }

    // A round dot of any size in pixel art: one pixel, a 2x2, or a disc whose rim is dithered away. `a` fades the lot.
    void SoftDot(float cx, float cy, float size, Color c, float a)
    {
        if (a <= 0.01f) return;
        if (size < 1.5f) { pix.Rect(cx, cy, 1, 1, Pal.WithAlpha(c, a)); return; }
        if (size < 2.5f) { pix.Rect(cx - 1, cy - 1, 2, 2, Pal.WithAlpha(c, a)); return; }
        float rr = size / 2;
        int r = (int)MathF.Ceiling(rr), ix = (int)MathF.Round(cx), iy = (int)MathF.Round(cy);
        for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                float d = MathF.Sqrt(dx * dx + dy * dy) / rr;
                if (d > 1) continue;
                if (d > 0.6f && BayerAt(ix + dx, iy + dy) < (d - 0.6f) / 0.4f) continue;
                pix.Rect(ix + dx, iy + dy, 1, 1, Pal.WithAlpha(c, a * (1 - d * 0.35f)));
            }
    }

    // A puff of dust, smoke or mist: drifts, grows from s0 to s1 pixels and fades. Looks only, so it uses fxRng. With light off
    // it is a couple of the old hard specks instead, so that look doesn't change.
    void Puff(float x, float y, string colour, float vx, float vy, float life, float s0, float s1, float alpha, float gy = -3)
    {
        if (LightMode == 0) { particles.Add(new Particle { X = x, Y = y, Vx = vx, Vy = vy, Life = Math.Min(life, 0.3f), Color = colour }); return; }
        particles.Add(new Particle { X = x, Y = y, Vx = vx, Vy = vy, Life = life, Color = colour, Soft = true, Size0 = s0, Size1 = s1, Alpha0 = alpha, Gy = gy });
    }

    // One swing at a tree or boulder. Trees take three hits, boulders four.
    void HitTree(int tx, int ty)
    {
        char k = TileAt(tx, ty);
        bool boulder = k == 'R';
        if (boulder ? PickTier == 0 : Has("axe") == 0)
        {
            Sfx.Play("nope");
            Toast(boulder ? "You need a pickaxe for that. Make one at a workbench." : "You need an axe to chop that. Tomas has a workbench in his hut.", 3.5f);
            return;
        }
        if (chopTile != (tx, ty)) { chopTile = (tx, ty); chopHits = 0; }
        chopHits++;
        shakeT = 0.25f;
        Swing(boulder ? "pick" : "axe", 0.25f);
        FaceToward(tx * T + 5, ty * T + 5);
        Sfx.Play(boulder ? "mine" : "chop");
        Burst(tx * T + 5, ty * T + 3, boulder ? "#9aa0a5" : k == 'c' ? "#6aa84f" : "#c9a06a", 5);
        if (chopHits >= (boulder ? 4 : 3)) Fell(tx, ty, k);
    }

    void Fell(int x, int y, char k)
    {
        var drops = k switch
        {
            'h' => new[] { ("wood", 2), ("coconut", 1) },
            'c' => new[] { ("cactus_fruit", 1), ("wood", 1) },
            'R' => rng.NextDouble() < 0.25 ? new[] { ("stone", 3), ("copper_ore", 1) } : new[] { ("stone", 3) },
            _ => new[] { ("wood", 3) }
        };
        foreach (var (id, n) in drops) Give(id, n);
        Learned(k == 'R' ? "boulder" : "chop");
        Tick("felled");
        state.felled[$"{x},{y}"] = state.day;
        stumps[(x, y)] = k;
        worldMap[y, x] = FelledGround(k, x, y);
        trees.RemoveAll(t => t.x == x && t.y == y);
        RenderTile(x, y);
        mapTexDirty = true;
        chopTile = (-1, -1);
        Burst(x * T + 5, y * T, k == 'R' ? "#7d8288" : "#3f7d35", 10);
        Sfx.Play("pickup");
        Toast($"The {TreeName(k)} {(k == 'R' ? "breaks apart" : "comes down")}. Got {string.Join(", ", drops.Select(d => $"{d.Item2} {Items.ById[d.Item1].Name.ToLowerInvariant()}"))}.");
        Save();
    }

    void PickBush(int x, int y)
    {
        string key = $"{x},{y}";
        // The wild bushes of Habagat are calamansi.
        bool calamansi = BiomeAt(x, y) == 6;
        if (state.picked.GetValueOrDefault(key) == state.day) { Toast(calamansi ? "None left. The calamansi ripen again by tomorrow." : "No berries left. They grow back by tomorrow."); return; }
        int n = 2 + rng.Next(2);
        state.picked[key] = state.day;
        Give(calamansi ? "calamansi" : "berries", n);
        Sfx.Play("pickup");
        Burst(x * T + 5, y * T + 4, calamansi ? "#7fb53a" : "#e04b3a", 6);
        Toast(calamansi ? $"Picked {n} calamansi." : $"Picked {n} berries.");
    }

    /* ---------- Animals ---------- */
    void SpawnAnimals()
    {
        animals.Clear();
        for (int i = 0; i < Items.AnimalSpawns.Length; i++)
        {
            var (kind, x, y) = Items.AnimalSpawns[i];
            // Start on the nearest open ground to the spawn point.
            (float x, float y)? at = null;
            for (int r = 0; r <= 6 && at == null; r++)
                for (int oy = -r; oy <= r && at == null; oy++)
                    for (int ox = -r; ox <= r; ox++)
                    {
                        float cx = x + ox * T, cy = y + oy * T;
                        if (CanStand(cx, cy)) { at = (cx, cy); break; }
                    }
            if (at is not (float ax, float ay)) continue;
            animals.Add(new Animal { Kind = kind, Key = $"{kind}{i}", X = ax, Y = ay, HomeX = ax, HomeY = ay, Timer = Rand(0.2f, 2f) });
        }
        PlacePet();
    }

    // Animals wander near home: walk a little, stand around, walk again.
    // When an animal beds down and gets up (minutes since midnight). Each has its own times, from where it lives, so a
    // field of sheep doesn't all lie down on the same minute: bed between 20:30 and 23:00, up between 05:00 and 06:30.
    static (float bed, float wake) AnimalHours(Animal a) =>
        (20.5f * 60 + 150 * (float)Pix.Hash((int)a.HomeX, (int)a.HomeY, 411), 5 * 60 + 90 * (float)Pix.Hash((int)a.HomeY, (int)a.HomeX, 412));
    // The night birds and the tarsier are up when it's dark; the cat keeps its own hours.
    static bool SleepsAtNight(Animal a) => a.Kind is not ("tarsier" or "hornbill" or "cat" or "parrot");

    bool AnimalBedtime(Animal a)
    {
        var (bed, wake) = AnimalHours(a);
        return state.clock >= bed || state.clock < wake;
    }

    void UpdateAnimals(float dt)
    {
        if (scene != "world") return;
        foreach (var a in animals)
        {
            if (IsPet(a)) { UpdatePet(a, dt); continue; }   // your pet follows you instead (Pet.cs)
            a.Hearts = Math.Max(0, a.Hearts - dt);
            bool bedtime = SleepsAtNight(a) && AnimalBedtime(a);
            // Asleep, it stays put until morning (or until you wake it by petting it): no more wandering a few steps and
            // lying down again.
            if (a.Asleep)
            {
                if (bedtime && a.Hearts <= 0) { a.Vx = a.Vy = 0; continue; }
                a.Asleep = false; a.Timer = Rand(0.5f, 4f);
            }
            if (a.Pause > 0) { a.Pause -= dt; continue; }
            a.Timer -= dt;
            if (a.Timer <= 0)
            {
                if (a.Vx != 0 || a.Vy != 0) { a.Vx = a.Vy = 0; a.Timer = Rand(1f, 3.5f); }
                else if (bedtime) { a.Asleep = true; continue; }
                else
                {
                    float ang = Rand(0, MathF.Tau);
                    if (Dist(a.X, a.Y, a.HomeX, a.HomeY) > 45) ang = MathF.Atan2(a.HomeY - a.Y, a.HomeX - a.X);
                    // A flock moves together: chickens, sheep and goats set off the way a walking neighbour is going.
                    if (a.Kind is "chicken" or "sheep" or "goat" && Dist(a.X, a.Y, a.HomeX, a.HomeY) <= 45)
                    {
                        var mate = animals.FirstOrDefault(m => m != a && m.Kind == a.Kind && !IsPet(m) && !m.Asleep && (m.Vx != 0 || m.Vy != 0) && Dist(m.X, m.Y, a.X, a.Y) < 36);
                        if (mate != null) ang = MathF.Atan2(mate.Vy, mate.Vx) + Rand(-0.35f, 0.35f);
                    }
                    float sp = Items.Animals[a.Kind].Speed;
                    a.Vx = MathF.Cos(ang) * sp; a.Vy = MathF.Sin(ang) * sp;
                    a.Timer = Rand(0.6f, 1.8f);
                    // ...and its neighbours notice and follow in a moment.
                    if (a.Kind is "chicken" or "sheep" or "goat")
                        foreach (var m in animals) if (m != a && m.Kind == a.Kind && !IsPet(m) && !m.Asleep && m.Vx == 0 && m.Vy == 0 && m.Pause <= 0 && m.Timer > 0.5f && Dist(m.X, m.Y, a.X, a.Y) < 36) m.Timer = Rand(0.15f, 0.5f);
                }
            }
            float nx = a.X + a.Vx * dt, ny = a.Y + a.Vy * dt;
            if (CanStand(nx, a.Y) && TileAt((int)(nx / T), (int)(a.Y / T)) is not ('d' or 'b')) a.X = nx; else a.Vx = -a.Vx;
            if (CanStand(a.X, ny) && TileAt((int)(a.X / T), (int)(ny / T)) is not ('d' or 'b')) a.Y = ny; else a.Vy = -a.Vy;
            if (a.Vx != 0) a.Flip = a.Vx < 0;
        }
    }

    // Only an animal you're facing, so one wandering past doesn't take over the fishing button.
    Animal NearestAnimal()
    {
        if (scene != "world") return null;
        float fx = player.Face == "left" ? -1 : player.Face == "right" ? 1 : 0, fy = player.Face == "up" ? -1 : player.Face == "down" ? 1 : 0;
        return animals.Where(a => !IsPet(a)).Where(a =>
        {
            float dx = a.X - player.X, dy = a.Y - player.Y, d = MathF.Sqrt(dx * dx + dy * dy);
            return d < 16 && (d < 4 || (dx * fx + dy * fy) / d > 0.5f);
        }).OrderBy(a => Dist(a.X, a.Y, player.X, player.Y)).FirstOrDefault();
    }

    void Pet(Animal a)
    {
        var k = Items.Animals[a.Kind];
        FaceToward(a.X, a.Y);
        a.Pause = 2;
        a.Asleep = false;   // petting wakes it
        a.Vx = a.Vy = 0;
        a.Hearts = 1.5f;
        a.Flip = player.X < a.X;
        Sfx.Play("pet");
        NotePetted(a);   // three days of this and it can come home with you (Pet.cs)
        // The tarsier and the hornbill are only watched, and Tala keeps count of them (Bakawan.cs).
        if (a.Kind is "tarsier" or "hornbill") { RecordSighting(a.Kind, a.X, a.Y - 8, k.Pet); return; }
        if (k.Gift != null && state.gifts.GetValueOrDefault(a.Key) != state.day)
        {
            state.gifts[a.Key] = state.day;
            Give(k.Gift);
            Toast(k.GiftText, 3.5f);
            Save();
        }
        else Toast(k.Pet + (k.Gift != null ? " It has nothing more for you today." : ""), 3);
    }

    static string AnimalLabel(Animal a) => a.Kind == "dog" ? "Pet Biscuit" : $"{(a.Kind is "tarsier" or "hornbill" or "parrot" ? "Observe" : "Pet")} the {Items.Animals[a.Kind].Name}";
}
