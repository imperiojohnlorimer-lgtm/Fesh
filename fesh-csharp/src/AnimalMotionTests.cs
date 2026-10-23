#if DEBUG
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Fesh;

partial class Game
{
    // Richer animal motion (1.45.1, FESH_ANIMAL_TEST=1): the head-up and ear-twitch poses are drawn (and leave the big carabao
    // alone), an animal looks up when you stop near it and not when you're walking or far off, ears twitch now and then, all
    // of it stops under Reduce motion, each animal reacts to a pat in its own way, and a flock sets off together.
    IEnumerable<int> AnimalMotionScript()
    {
        Note("Animal motion");
        state = new State { created = true, look = new Look { name = "Herder" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 5; Inp.ScriptMouse = Offscreen;
        yield return 3;

        /* ---------- the drawings ---------- */
        int Hash(string id, int pose, bool flip = false)
        {
            var p = new Pix(40, 30);
            AnimalArt.Draw(p, id, 20, 26, flip, 0, pose);
            int h = 17;
            foreach (var c in p.Buf) h = unchecked(h * 31 + c.R * 5 + c.G * 3 + c.B + c.A);
            return h;
        }
        var small = new[] { "pig", "sheep", "goat", "dog", "cat", "chicken" };
        Check("head up is a new picture for every small animal", small.All(id => Hash(id, 4) != Hash(id, 0)));
        Check("an ear twitch is a new picture too, and not the head-up one", small.All(id => Hash(id, 5) != Hash(id, 0) && Hash(id, 5) != Hash(id, 4)));
        Check("the big carabao and the tarsier stay as they are (their drawings include horns and big eyes)", Hash("carabao", 4) == Hash("carabao", 0) && Hash("carabao", 5) == Hash("carabao", 0));
        Check("mirrored, the twitch moves the same part", small.All(id => Hash(id, 5, true) != Hash(id, 0, true)));
        // A sheet of every pose (stand, graze, tail, head up, ear twitch), one animal a row, to look at.
        {
            var sheet = new Pix(120, 112);
            Array.Fill(sheet.Buf, Pal.C("#5d9b45"));
            for (int i = 0; i < small.Length; i++)
            {
                var (aw, _) = AnimalArt.Size(small[i]);
                int ax = 4;
                foreach (int pose in new[] { 0, 1, 2, 4, 5 }) { AnimalArt.Draw(sheet, small[i], ax + aw / 2, 14 + i * 18, false, 0, pose); ax += aw + 4; }
            }
            var img = GenImageColor(120, 112, Color.Black);
            for (int y = 0; y < 112; y++) for (int x = 0; x < 120; x++) ImageDrawPixel(ref img, x, y, sheet.Buf[y * 120 + x]);
            ImageResizeNN(ref img, 120 * 6, 112 * 6);
            ExportImage(img, Path.Combine(testDir, "animal-poses.png"));
            UnloadImage(img);
        }

        /* ---------- looking up ---------- */
        animals.Clear();
        var sheep = new Animal { Kind = "sheep", Key = "t-sheep", X = 150, Y = 112, HomeX = 150, HomeY = 112, Timer = 99 };
        animals.Add(sheep);
        player.X = 168; player.Y = 112; player.Moving = false;
        var poses = new HashSet<int>();
        Settings.Data.reduceMotion = false;
        float t0 = time;
        for (int i = 0; i < 120; i++) { time = t0 + i * 0.1f; poses.Add(AnimalPose(sheep, false, out _)); }
        time = t0;
        Check($"stopped near it, the sheep keeps its head up ({string.Join(",", poses.OrderBy(v => v))})", poses.Contains(4) && !poses.Contains(1));
        player.Moving = true;
        poses.Clear(); for (int i = 0; i < 120; i++) { time = t0 + i * 0.1f; poses.Add(AnimalPose(sheep, false, out _)); }
        Check("walking past, it carries on grazing", !poses.Contains(4) && poses.Contains(1));
        player.Moving = false; player.X = 260;
        poses.Clear(); for (int i = 0; i < 1200; i++) { time = t0 + i * 0.1f; poses.Add(AnimalPose(sheep, false, out _)); }
        Check($"far away it grazes, and now and then an ear twitches ({string.Join(",", poses.OrderBy(v => v))})", !poses.Contains(4) && poses.Contains(1) && poses.Contains(5));
        player.X = 168;
        Settings.Data.reduceMotion = true;
        poses.Clear(); for (int i = 0; i < 120; i++) { time = t0 + i * 0.1f; poses.Add(AnimalPose(sheep, false, out _)); }
        Check("under Reduce motion there is no head-up and no ear twitch", !poses.Contains(4) && !poses.Contains(5));
        Settings.Data.reduceMotion = false; time = t0;
        sheep.Asleep = true; SetNight(true);
        Check("a sleeping animal is drawn asleep, whatever else", sheep.Asleep);
        SetNight(false); sheep.Asleep = false;

        /* ---------- a pat, each its own way ---------- */
        string Pat(string kind)
        {
            var a = new Animal { Kind = kind, Key = "p-" + kind, X = 150, Y = 112, HomeX = 150, HomeY = 112, Hearts = 1.5f };
            var seen = new HashSet<string>();
            for (int i = 0; i < 40; i++) { time = t0 + i * 0.07f; seen.Add($"{AnimalPose(a, false, out int hop)}/{hop}"); }
            time = t0;
            return string.Join(" ", seen.OrderBy(s => s));
        }
        var pats = new[] { "sheep", "goat", "pig", "chicken", "dog", "cat" }.ToDictionary(k => k, Pat);
        Note("pat poses/hops: " + string.Join("; ", pats.Select(kv => kv.Key + " " + kv.Value)));
        Check("a patted sheep hops", pats["sheep"].Contains("0/1") && pats["sheep"].Contains("0/0"));
        Check("a patted goat nods its head", pats["goat"].Contains("1/0") && pats["goat"].Contains("4/0"));
        Check("a patted pig snuffles", pats["pig"].Contains("1/0") && pats["pig"].Contains("0/0"));
        Check("a patted chicken flutters up and down", pats["chicken"].Contains("1/0") && pats["chicken"].Contains("2/1"));
        Check("a dog still wags and a cat flicks", pats["dog"].Contains("2/0") && pats["cat"].Contains("2/0"));
        Check("the five reactions are different from each other", new[] { "sheep", "goat", "pig", "chicken" }.Select(k => pats[k]).Distinct().Count() == 4);

        /* ---------- a flock ---------- */
        int together = 0, trials = 40;
        for (int trial = 0; trial < trials; trial++)
        {
            animals.Clear();
            var lead = new Animal { Kind = "chicken", Key = "c0", X = 140, Y = 110, HomeX = 140, HomeY = 110, Timer = 0.01f };
            var f1 = new Animal { Kind = "chicken", Key = "c1", X = 146, Y = 112, HomeX = 146, HomeY = 112, Timer = 3 };
            var f2 = new Animal { Kind = "chicken", Key = "c2", X = 134, Y = 109, HomeX = 134, HomeY = 109, Timer = 3 };
            animals.AddRange(new[] { lead, f1, f2 });
            player.X = 200; player.Y = 150;
            for (int i = 0; i < 12 && (f1.Vx == 0 && f1.Vy == 0 || f2.Vx == 0 && f2.Vy == 0); i++) UpdateAnimals(0.05f);
            float Dot(Animal a, Animal b) { float la = MathF.Sqrt(a.Vx * a.Vx + a.Vy * a.Vy), lb = MathF.Sqrt(b.Vx * b.Vx + b.Vy * b.Vy); return la < 0.01f || lb < 0.01f ? -1 : (a.Vx * b.Vx + a.Vy * b.Vy) / (la * lb); }
            if (Dot(lead, f1) > 0.5f && Dot(lead, f2) > 0.5f) together++;
        }
        Check($"neighbours set off the way the leader goes ({together} of {trials} trials)", together >= trials * 0.7);
        animals.Clear();
        var alone = new Animal { Kind = "chicken", Key = "a0", X = 140, Y = 110, HomeX = 140, HomeY = 110, Timer = 0.01f };
        var far = new Animal { Kind = "chicken", Key = "a1", X = 300, Y = 110, HomeX = 300, HomeY = 110, Timer = 3 };
        animals.AddRange(new[] { alone, far });
        for (int i = 0; i < 6; i++) UpdateAnimals(0.05f);
        Check("a chicken far off isn't called along", far.Timer > 2);
        var other = new Animal { Kind = "sheep", Key = "a2", X = 146, Y = 112, HomeX = 146, HomeY = 112, Timer = 3 };
        animals.Clear(); alone.Timer = 0.01f; alone.Vx = alone.Vy = 0; animals.AddRange(new[] { alone, other });
        for (int i = 0; i < 6; i++) UpdateAnimals(0.05f);
        Check("and a different animal isn't either", other.Timer > 2);
        animals.Clear();
        SpawnAnimals();
        yield return 2;
    }
}
#endif
