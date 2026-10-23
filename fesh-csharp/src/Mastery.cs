namespace Fesh;

// Mastery and trophy tiers (1.46). Fishing level stops at 10 (1,800 XP); XP keeps counting after it, and each further step is a
// mastery level, ten of them. They give titles and cosmetics only, never a stat (Codex: this is a cosmetic release). A trophy
// was "half again the usual size" and about half of a well-equipped fisher's catches qualify, so it now has three tiers by the
// same size measure m: silver 1.5 (the old trophy), gold 2.0 and master 2.5. Gold needs good tackle (the usual size roll tops
// out near 1.7 before gear), master the best tackle and a very lucky roll: about 1 catch in 60 with the best gear.
partial class Game
{
    const int MasteryMax = 10;
    static int MasteryNeed(int n) => 100 + 25 * (n - 1);                          // XP for mastery level n, after n - 1
    static int MasteryTotal(int n) { int t = 0; for (int k = 1; k <= n; k++) t += MasteryNeed(k); return t; }

    int MasteryXp => Math.Max(0, state.xp - XpFor(10));
    int MasteryLevel { get { int l = 0; while (l < MasteryMax && MasteryXp >= MasteryTotal(l + 1)) l++; return l; } }
    string MasteryLine => MasteryLevel == 0 ? $"{Math.Max(0, XpFor(10) - state.xp)} XP to fishing level 10, then mastery"
        : MasteryLevel >= MasteryMax ? "Mastery 10: the top" : $"Mastery {MasteryLevel} · {MasteryTotal(MasteryLevel + 1) - MasteryXp} XP to the next";

    // The titles at levels 1, 3, 4, 6, 7 and 9; the others unlock cosmetics (Wardrobe.cs).
    static readonly Dictionary<int, string> MasteryTitles = new()
    {
        [1] = "Deep-water hand", [3] = "Salt-handed", [4] = "Steady line", [6] = "Reef keeper", [7] = "Old hand of the sea", [9] = "Sea elder"
    };

    // 0 none, 1 silver, 2 gold, 3 master. m is the catch's size against the usual (before display rounding).
    static int TrophyTier(float m) => m >= 2.5f ? 3 : m >= 2.0f ? 2 : m >= 1.5f ? 1 : 0;
    static string TierName(int tier) => tier switch { 3 => "Master", 2 => "Gold", 1 => "Silver", _ => "" };

    static IEnumerable<Ach> ExtraAchievements() => MasteryAchievements().Concat(HomeAchievements());

    static IEnumerable<Ach> MasteryAchievements()
    {
        for (int n = 1; n <= MasteryMax; n++)
        {
            int level = n;
            MasteryTitles.TryGetValue(n, out var title);
            string unlock = CosmeticUnlockedBy("mastery:" + n) is Cosmetic c ? $" Unlocks the {c.Name.ToLowerInvariant()} in your wardrobe." : "";
            yield return new Ach("mastery" + n, "Mastery", n == MasteryMax ? "Mastery 10" : $"Mastery {n}", $"Reach fishing mastery {n}, after level 10.{unlock}", g => g.MasteryLevel, n, 30 + 10 * n, title);
        }
        yield return new Ach("gold1", "Mastery", "Gold size", "Land a gold trophy: twice the usual size.", g => g.state.golds, 1, 60);
        yield return new Ach("gold10", "Mastery", "Golden hands", "Land 10 gold trophies.", g => g.state.golds, 10, 150, "Golden hands");
        yield return new Ach("master1", "Mastery", "Master size", "Land a master trophy: two and a half times the usual size.", g => g.state.masters, 1, 200, "Master of the line");
    }
}
