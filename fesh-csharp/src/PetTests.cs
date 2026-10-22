#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // A pet (1.45, FESH_PET_TEST=1): adopting Biscuit after petting him on three days by real E presses, naming him by real
    // clicks, following you, never taking E from a fishing spot, stay and come by F, catching up when you're far off,
    // waiting while you're aboard, the daily find, lying down at night, cheering a catch, a reload, and nothing in the old
    // scripts.
    IEnumerable<int> PetScript()
    {
        Note("A pet");
        OldCelebrate = false; PetsOff = false;
        state = new State { created = true, look = new Look { name = "Dog person" } };
        state.flags.metTomas = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Settings.Data.pauseUnfocused = false; Settings.Data.zoom = 5; Inp.ScriptMouse = Offscreen;
        state.food = 100; state.hp = 100;
        yield return 3;
        var dog = animals.First(a => a.Kind == "dog");
        IEnumerable<int> FaceDog()
        {
            dog.Pause = 99; dog.Vx = dog.Vy = 0; dog.Asleep = false;
            player.X = dog.X - 10; player.Y = dog.Y; player.Face = "right"; yield return 3;
        }
        IEnumerable<int> EndTalk() { for (int i = 0; i < 20 && mode == "dialogue"; i++) { Inp.Tap(KeyboardKey.E); yield return 4; } yield return 2; }

        /* ---------- three days of friendship ---------- */
        for (int d = 0; d < AdoptDays; d++)
        {
            foreach (var s in FaceDog()) yield return s;
            Check($"day {d + 1}: Biscuit in front of you, no adopting yet", target?.Type == "animal" && target.Ref == dog && target.AltType != "adopt");
            Inp.Tap(KeyboardKey.E); yield return 3;
            Inp.Tap(KeyboardKey.E); yield return 3;   // twice the same day counts once
            state.day++; SetNight(false);
        }
        Check($"petted on three days ({state.gifts.GetValueOrDefault("pet:" + dog.Key)})", state.gifts.GetValueOrDefault("pet:" + dog.Key) == AdoptDays);
        foreach (var s in FaceDog()) yield return s;
        Check($"now F takes him home ({target?.AltLabel})", target?.AltType == "adopt");
        Inp.Tap(KeyboardKey.F); yield return 4;
        Check("F opens the naming card", mode == "panel" && panel == "petname");
        ClickButton("petname:next"); yield return 3; Inp.ScriptMouse = Offscreen; yield return 2;
        pendingShot = "p01-name"; yield return 4;
        ClickButton("That's the one"); yield return 4; Inp.ScriptMouse = Offscreen; yield return 2;
        Check($"a real click names him ({state.pet?.name})", state.pet?.name == DogNames[1] && state.pet.key == dog.Key && mode == "play");
        CheckLedger(); celebQueue.Clear(); banner = null;
        Check("the Ledger's \"A friend for life\"", AchEarned(state, Achievements.First(a => a.Id == "pet")));

        /* ---------- following ---------- */
        dog.Pause = 0;
        player.X = 160; player.Y = 115; yield return 3;
        float start = player.X;
        Inp.Hold(KeyboardKey.D, true);
        float t0 = time; while (time - t0 < 1.6f) yield return 1;
        Inp.Hold(KeyboardKey.D, false);
        t0 = time; while (time - t0 < 1.2f) yield return 1;
        pendingShot = "p02-follow"; yield return 4;
        Check($"he follows you ({Dist(dog.X, dog.Y, player.X, player.Y):0} px behind, you walked {player.X - start:0})", player.X - start > 40 && Dist(dog.X, dog.Y, player.X, player.Y) < 26);
        // Beside you at a fishing spot, he never takes E from it.
        var lag = Data.SpotById["lagoon"];
        player.X = lag.X; player.Y = lag.Y + lag.R - 3; player.Face = "up"; dog.X = player.X + 8; dog.Y = player.Y; yield return 3;
        Check($"at a fishing spot E still fishes ({target?.Type})", target?.Type == "spot");

        /* ---------- stay and come ---------- */
        player.X = 160; player.Y = 115; player.Face = "right"; dog.X = 172; dog.Y = 115; yield return 3;
        Check($"facing him: pet him, and F says stay ({target?.AltLabel})", target?.Type == "animal" && target.AltType == "petstay");
        Inp.Tap(KeyboardKey.F); yield return 3;
        Check("he stays", !state.pet.follow);
        float sx = dog.X, sy = dog.Y;
        player.X = 120; yield return 30;
        Check("and waits where he was", Dist(dog.X, dog.Y, sx, sy) < 1);
        player.X = 160; player.Face = "right"; yield return 3;
        Inp.Tap(KeyboardKey.F); yield return 3;
        Check("F again calls him", state.pet.follow);

        /* ---------- far off, aboard ---------- */
        player.X = 1600; player.Y = 205; state.hinted["amihan"] = true; yield return 4;
        Check($"far away on foot, he turns up beside you ({Dist(dog.X, dog.Y, player.X, player.Y):0} px)", Dist(dog.X, dog.Y, player.X, player.Y) < 30 && CanStand(dog.X, dog.Y));
        player.X = 160; player.Y = 115; yield return 4;
        Give("boat"); state.aboard = true; float ax = dog.X, ay = dog.Y;
        player.X = SaltJettyX + 40; player.Y = SaltJettyY + 10; yield return 20;
        Check("while you're aboard he waits", Dist(dog.X, dog.Y, ax, ay) < 1);
        state.aboard = false; player.X = 160; player.Y = 115; yield return 6;

        /* ---------- finds, night, a catch ---------- */
        int w0 = Has("worm");
        state.pet.walked = FindAfter; state.pet.foundDay = -1;
        dog.X = 120; yield return 6;
        Check($"after a long walk together he finds 2 worms, once a day ({Has("worm") - w0})", Has("worm") - w0 == 2 && state.pet.foundDay == state.day);
        state.pet.walked = FindAfter; yield return 6;
        Check("not twice the same day", Has("worm") - w0 == 2);
        // Codex's review: stuck (here, out in the water where it can't take a step), it earns nothing, and soon comes round.
        float stuckWalk = state.pet.walked = 0;
        dog.X = player.X - 40; dog.Y = player.Y; int sea = 0;
        for (int i = 0; i < 40 && sea == 0; i++) { if (!CanStand(dog.X, dog.Y)) sea = 1; else dog.X -= 10; }
        t0 = time; while (time - t0 < 1.5f) yield return 1;
        float midWalk = state.pet.walked;
        t0 = time; while (time - t0 < 3f) yield return 1;
        Check($"stuck, it earns no walking ({midWalk:0.0} s) and comes round to you ({Dist(dog.X, dog.Y, player.X, player.Y):0} px)", sea == 1 && midWalk == 0 && Dist(dog.X, dog.Y, player.X, player.Y) < 30);
        SetNight(true); dog.X = player.X + 8; dog.Y = player.Y; dog.Hearts = 0; yield return 30;
        pendingShot = "p03-night"; yield return 4;
        Check("at night beside you, he lies down", dog.Asleep);
        SetNight(false); yield return 3;
        PetCheers();
        Check("a fine catch: he's glad too", dog.Hearts > 0);

        /* ---------- a reload ---------- */
        Save(); state = SaveFile.Read(SaveFile.Slot); StartGame(false); yield return 4;
        var again = PetAnimal;
        Check($"after a reload he's still yours, beside you ({state.pet?.name})", again != null && state.pet.name == DogNames[1] && Dist(again.X, again.Y, player.X, player.Y) < 60);

        /* ---------- off ---------- */
        PetsOff = true;
        Check("in the old scripts Biscuit is just Biscuit", !IsPet(again));
        OldCelebrate = true;
        yield return 2;
    }
}
#endif
