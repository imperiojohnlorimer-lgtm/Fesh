#if DEBUG
using Raylib_cs;

namespace Fesh;

// FESH_PORTRAIT_TEST=1: the Fish log's boss portraits and words (BossPortraits.cs, Dex.cs): every boss portrait has its
// sprite, the legends with boss fights say they come back (and bite again after the first), the others still say once,
// the let-go ones say so, Platejaw's and Bakunawa's cards say how to fight them again, and each card still fits; with a
// screenshot of each card.
partial class Game
{
    IEnumerable<int> PortraitScript()
    {
        Note("The Fish log: boss portraits and what the cards say");
        state = new State { created = true, flags = new Flags { metTomas = true }, look = new Look { name = "Angler" } };
        mode = "play"; StartGame(false); ClearSkies(); SetNight(true); quietWildlife = true; standStill = true;
        Inp.ScriptMouse = Offscreen;

        foreach (var art in new[] { "marlin", "leviathan", "starray", "tarpon", "lungfish", "whiskers", "platejaw", "tidemane", "bakunawa" })
        {
            var s = BossSprite(art);
            int drawn = s is var (px, _, _) ? px.Count(c => c.A > 0) : 0;
            Check($"the {art} portrait is its sprite ({(s is var (_, w, h) ? $"{w}x{h}" : "none")}, {drawn} pixels)", s != null && drawn > 200);
        }


        // What the cards say about fighting them again.
        foreach (var id in new[] { TowFish, MireFish, RayFish, TarponFish, LungfishFish, WhiskersFish })
        {
            var f = Data.FishById[id];
            Check($"{f.Name}: a boss that comes back ({WhenText(f)})", Refightable(f) && WhenText(f).Contains("again") && !WhenText(f).Contains("only once"));
        }
        foreach (var id in new[] { "ancient_coelacanth" })
            Check($"{Data.FishById[id].Name} still bites only once", !Refightable(Data.FishById[id]) && WhenText(Data.FishById[id]).Contains("only once"));
        // Caught once, a boss still shows as biting when its time comes (the green dot used to go out for good).
        state.commons[MireFish] = 1; state.inv["worm"] = 5;
        Check("caught once, the Mire leviathan still shows as biting at night with a worm", BitingNow(Data.FishById[MireFish]));
        state.commons["old_whiskers"] = 1; state.weather = "rain"; state.inv["berries"] = 5;
        Check("and Old Whiskers, caught once, still shows as biting in the rain with a berry", BitingNow(Data.FishById["old_whiskers"]));
        state.weather = "clear";

        // Every boss's card, open, with a screenshot; it still fits.
        TogglePanel("dex"); dexTab = "log"; yield return 4;
        foreach (var id in new[] { TowFish, MireFish, RayFish, TarponFish, LungfishFish, WhiskersFish })
        {
            state.commons[id] = 1; state.records[id] = Data.FishById[id].Kg;
            dexFish = id; yield return 4;
            pendingShot = "card-" + id; yield return 2;
            Check($"{id}'s card fits ({lastCardBottom:0} of {DexCardH - DexCardPad})", lastCardBottom <= DexCardH - DexCardPad);
        }
        dexFish = null; yield return 2;
        // Platejaw's and Bakunawa's cards say how to fight them again.
        state.hinted["platejaw"] = true; state.hinted["platejawMet"] = true; state.hinted["moonReturned"] = true;
        state.tamed = true;
        foreach (var key in new[] { "tidemane", "platejaw", "bakunawa" })
        {
            var (_, _, _, _, _, facts) = ExtraInfo(key);
            if (key == "tidemane") { dexExtra = key; yield return 4; pendingShot = "card-tidemane"; yield return 2; continue; }
            Check($"{key}'s card says it can be fought again", facts.Any(fct => fct.k == "Again"));
            dexExtra = key; yield return 4;
            pendingShot = "card-" + key; yield return 2;
        }
        dexExtra = null; ClosePanels(); yield return 2;
    }
}
#endif
