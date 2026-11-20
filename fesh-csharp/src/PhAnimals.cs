namespace Fesh;

// Animals on the Philippines map (1.48, step 2): never the whole country at once, only round you. The land is cut into cells of
// PhCell tiles, and each cell's animals come from a hash of the cell (so the same field always has the same herd): chickens,
// dogs and cats about the towns, a carabao or goats on open grass, a pig or two by the houses, and a tarsier in Bohol's
// forests (watched, never handled). They come when their cell is near you and go when it's far behind.
partial class Game
{
    const int PhAnimalCell = 24, PhAnimalNear = 3;   // cells of 24 tiles; the 7 x 7 cells round you have their animals
    float phAnimalT;
    (int cx, int cy) phAnimalAt = (-999, -999);

    void UpdatePhAnimals(float dt)
    {
        if (!PhWorld || scene != "world") return;
        phAnimalT -= dt;
        int cx = (int)(player.X / T) / PhAnimalCell, cy = (int)(player.Y / T) / PhAnimalCell;
        if (phAnimalT > 0 && (cx, cy) == phAnimalAt) return;
        phAnimalT = 2;
        phAnimalAt = (cx, cy);
        // Those whose cell is now far off go (not your pet).
        animals.RemoveAll(a => !IsPet(a) && a.Key.StartsWith("ph:") && (Math.Abs((int)(a.HomeX / T) / PhAnimalCell - cx) > PhAnimalNear + 1 || Math.Abs((int)(a.HomeY / T) / PhAnimalCell - cy) > PhAnimalNear + 1));
        var have = new HashSet<string>(animals.Select(a => a.Key));
        for (int gy = cy - PhAnimalNear; gy <= cy + PhAnimalNear; gy++)
            for (int gx = cx - PhAnimalNear; gx <= cx + PhAnimalNear; gx++)
                foreach (var (kind, x, y, key) in PhCellAnimals(gx, gy))
                {
                    if (have.Contains(key) || !CanStand(x, y)) continue;
                    animals.Add(new Animal { Kind = kind, Key = key, X = x, Y = y, HomeX = x, HomeY = y, Timer = Rand(0.2f, 2f) });
                    have.Add(key);
                }
    }

    // A cell's animals: (kind, where, a key that stays the same for that animal).
    IEnumerable<(string kind, float x, float y, string key)> PhCellAnimals(int gx, int gy)
    {
        if (gx < 0 || gy < 0) yield break;
        // In a town: chickens, a dog, a cat, maybe a pig, by its houses.
        foreach (var t in PhTowns())
        {
            if (t.Cx / PhAnimalCell != gx || t.Cy / PhAnimalCell != gy) continue;
            string[] kinds = { "chicken", "chicken", "chicken", "dog", "cat", "pig", "goat" };
            int i = 0;
            foreach (var h in t.Things.Where(h => h.Kind is "house" or "inn"))
            {
                string kind = kinds[(int)(Pix.Hash(h.X, h.Y, 751) * kinds.Length) % kinds.Length];
                if (kind == "dog" && t.Def.Id == "boac" && i == 0) kind = "chicken";
                yield return (kind, h.X + (Pix.Hash(h.X, h.Y, 752) < 0.5 ? -16 : 16), h.Y + 10, $"ph:{t.Def.Id}:{kind}{i++}");
            }
        }
        // Open land: a carabao or a few goats on grass, now and then.
        double r = Pix.Hash(gx, gy, 753);
        if (r > 0.45) yield break;
        int tx = gx * PhAnimalCell + (int)(Pix.Hash(gx, gy, 754) * PhAnimalCell), ty = gy * PhAnimalCell + (int)(Pix.Hash(gx, gy, 755) * PhAnimalCell);
        if (PhKindAt(tx, ty) != 3) yield break;
        char g = PhGround(tx, ty);
        var (lat, lon) = PhLatLon(tx * T, ty * T);
        bool bohol = lat > 9.55 && lat < 10.15 && lon > 123.75 && lon < 124.6;
        if (g == 'j' && bohol) { yield return ("tarsier", tx * T + 5, ty * T + 8, $"ph:c{gx},{gy}:tarsier"); yield break; }
        if (g != 'g') yield break;
        if (r < 0.2) yield return ("carabao", tx * T + 5, ty * T + 8, $"ph:c{gx},{gy}:carabao");
        else
            for (int k = 0; k < 2 + (int)(r * 10) % 2; k++)
                yield return ("goat", tx * T + 5 + k * 12, ty * T + 8 + (k % 2) * 8, $"ph:c{gx},{gy}:goat{k}");
    }
}
