namespace Fesh;

// 1.48: a boss fight's big moments land harder. When one of a boss's moves goes your way the progress jumps up, and when it
// goes the fish's way it drops: either way, a jump of more than a few hundredths in one frame shakes the view, flashes it
// (white for you, red for the fish) and throws up water where the fish is. Ordinary fish never jump like that.
partial class Game
{
    bool BossFight => reel?.Roll is { } r && (r.Boss || r.Id is TowFish or MireFish or LungfishFish or WhiskersFish or RayFish or TarponFish);

    // Where the fish is right now: Ironbill's mouth when it's out of the water, else the end of the line (the boss fights keep
    // fish.Bx/By on the fish, in the water or up in a leap).
    (float x, float y) BossLineEnd() => reel?.Roll?.Id == TowFish && IronbillLineEnd(time) is (float ix, float iy) ? (ix, iy) : (fish.Bx, fish.By);

    void BossBeat(float delta)
    {
        if (!BossFight || fish == null || MathF.Abs(delta) < 0.03f) return;
        bool good = delta > 0;
        Impact(good ? 0.28f : 0.22f, good ? "#ffffff" : "#ff5040", good ? 0.16f : 0.2f);
        var (x, y) = BossLineEnd();
        Burst(x, y, "#eef9fb", good ? 16 : 10);
        if (good) Burst(x, y - 2, "#ffffff", 6);
        Sfx.Play("splash");
    }
}
