namespace Fesh;

sealed class Animal { public string Kind, Key; public float X, Y, HomeX, HomeY, Vx, Vy, Timer, Pause, Hearts; public bool Flip; }
sealed class Particle { public float X, Y, Vx, Vy, Life; public string Color; }

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
    bool Starving => state.food <= 0;

    /* ---------- Food ---------- */
    // The food meter drops about one point every seven seconds of play.
    void TickFood(float dt)
    {
        float before = state.food;
        state.food = Math.Max(0, state.food - dt / 7f);
        if (before >= 25 && state.food < 25) Toast("You're getting hungry. Open your bag (<bag>) and eat something.", 4);
        if (before > 0 && state.food <= 0) Toast("You're starving! You walk slower and reeling is harder until you eat.", 4.5f);
    }

    void Eat(string id)
    {
        var d = Items.ById[id];
        if (d.Food <= 0) return;
        if (state.food >= 99.5f && state.hp >= 99.5f) { Toast("You're full."); Sfx.Play("nope"); return; }
        if (!Take(id)) return;
        Learned("eat");
        float gain = Math.Min(d.Food, 100 - state.food), heal = Math.Min(d.Food / 2f, 100 - state.hp);
        state.food = Math.Min(100, state.food + d.Food);
        state.hp = Math.Min(100, state.hp + d.Food / 2f);
        Sfx.Play("eat");
        string what = $"(+{gain:0} food{(heal >= 1 ? $", +{heal:0} health" : "")})";
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
        var d = Items.ById[r.Out];
        Sfx.Play("craft");
        string tip = r.Out switch
        {
            "axe" => " Face a tree and press <act> to chop it.",
            "pickaxe" => " It breaks boulders, and mines copper in Frostfang Caverns, up north.",
            "copper_pickaxe" => " Now you can mine iron ore.",
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
        Save();
    }

    /* ---------- Chopping, mining and picking ---------- */
    static string TreeName(char k) => k switch { 't' => "oak", 'f' => "fir", 'h' => "palm", 'c' => "cactus", 'R' => "boulder", 'y' => "berry bush", _ => "tree" };

    void Burst(float x, float y, string color, int n)
    {
        for (int i = 0; i < n; i++)
            particles.Add(new Particle { X = x, Y = y, Vx = Rand(-30, 30), Vy = Rand(-50, -15), Life = Rand(0.3f, 0.6f), Color = color });
    }

    void UpdateParticles(float dt)
    {
        shakeT = Math.Max(0, shakeT - dt);
        swingT = Math.Max(0, swingT - dt);
        quake = Math.Max(0, quake - dt);
        for (int i = particles.Count - 1; i >= 0; i--)
        {
            var p = particles[i];
            p.Life -= dt;
            if (p.Life <= 0) { particles.RemoveAt(i); continue; }
            p.Vy += 140 * dt;
            p.X += p.Vx * dt;
            p.Y += p.Vy * dt;
        }
    }

    void DrawParticles()
    {
        foreach (var p in particles) pix.Rect(p.X, p.Y, 1, 1, p.Color);
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
        state.felled[$"{x},{y}"] = state.day;
        stumps[(x, y)] = k;
        worldMap[y, x] = Ground(k, x, y);
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
    }

    // Animals wander near home: walk a little, stand around, walk again.
    void UpdateAnimals(float dt)
    {
        if (scene != "world") return;
        foreach (var a in animals)
        {
            a.Hearts = Math.Max(0, a.Hearts - dt);
            if (a.Pause > 0) { a.Pause -= dt; continue; }
            a.Timer -= dt;
            if (a.Timer <= 0)
            {
                if (a.Vx != 0 || a.Vy != 0) { a.Vx = a.Vy = 0; a.Timer = Rand(1f, 3.5f); }
                else
                {
                    float ang = Rand(0, MathF.Tau);
                    if (Dist(a.X, a.Y, a.HomeX, a.HomeY) > 45) ang = MathF.Atan2(a.HomeY - a.Y, a.HomeX - a.X);
                    float sp = Items.Animals[a.Kind].Speed;
                    a.Vx = MathF.Cos(ang) * sp; a.Vy = MathF.Sin(ang) * sp;
                    a.Timer = Rand(0.6f, 1.8f);
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
        return animals.Where(a =>
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
        a.Vx = a.Vy = 0;
        a.Hearts = 1.5f;
        a.Flip = player.X < a.X;
        Sfx.Play("pet");
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

    static string AnimalLabel(Animal a) => a.Kind == "dog" ? "Pet Biscuit" : $"{(a.Kind is "tarsier" or "hornbill" ? "Observe" : "Pet")} the {Items.Animals[a.Kind].Name}";
}
