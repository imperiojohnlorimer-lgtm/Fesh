namespace Fesh;

// 1.34: where trees, bushes and boulders grow. Instead of one even roll per tile, a slow noise lays out groves (trees
// close together) and open meadows (a lone tree now and then), bushes gather along the groves' edges, boulders lie in
// small clusters where a second noise says the ground is stony, and palms line the beaches of the tropical islands.
// Deterministic (hashes of the tile), so the map is the same every time. Saltmere keeps its hand-placed trees, and
// Marilaya its own rule (Marilaya.cs).
partial class Game
{
    // Per biome: how thick a grove is, how many trees stand in the meadows, how big the groves are (px), bushes at the
    // edges, how stony, and palms along the beach. Same order as Data.Biomes.
    static readonly (float grove, float meadow, int size, float edge, float stone, float beach)[] PlantRules =
    {
        (0, 0, 0, 0, 0, 0),                    // 0 Saltmere: hand placed
        (0.55f, 0.02f, 80, 0.10f, 0.30f, 0),   // 1 Frostfang: stands of firs, snowfields between
        (0.12f, 0.03f, 110, 0, 0.28f, 0),      // 2 Sunscald: cacti spread thin, sandstone outcrops
        (0.48f, 0.08f, 70, 0.14f, 0.14f, 0),   // 3 Mirewood: thick jungle with glades
        (0.45f, 0.04f, 70, 0, 0, 0.16f),       // 4 Starfall Atoll: palm groves, palms along the beach
        (0.42f, 0.03f, 80, 0.06f, 0.18f, 0.12f), // 5 Amihan: palm groves inland, palms on the beaches
        (0.30f, 0.02f, 70, 0.12f, 0.18f, 0.12f), // 6 Habagat
        (0, 0, 0, 0, 0, 0)                     // 7 Marilaya: its own rule
    };

    // How much of a grove a tile is in: 0 out in a meadow, 1 deep in the trees.
    static float GroveAt(int x, int y, byte bi, int size)
    {
        float n = VNoise(x * T + 5, y * T + 5, size, 900 + bi) * 0.7f + VNoise(x * T + 5, y * T + 5, Math.Max(10, size / 3), 910 + bi) * 0.3f;
        return Math.Clamp((n - 0.46f) / 0.16f, 0, 1);
    }

    // What grows on an inland tile ('\0' nothing): a tree, a berry bush or a boulder.
    static char PlantRoll(int x, int y, byte bi, char tree)
    {
        var r = PlantRules[bi];
        if (r.size == 0) return '\0';
        float grove = GroveAt(x, y, bi, r.size);
        double roll = Pix.Hash(x, y, 77), roll2 = Pix.Hash(x, y, 79), roll3 = Pix.Hash(x, y, 81);
        // Boulders in clusters on stony ground, never in a thick grove.
        float stony = Math.Clamp((VNoise(x * T + 5, y * T + 5, 60, 920 + bi) - 0.62f) / 0.12f, 0, 1);
        if (stony > 0 && grove < 0.7f && roll3 < r.stone * stony) return 'R';
        // Trees: thick in a grove, a lone one now and then out in the open.
        float p = r.meadow + (r.grove - r.meadow) * grove;
        if (roll < p) return tree;
        // Bushes where a grove thins out into the open.
        float edge = grove > 0.05f && grove < 0.6f ? 1 - MathF.Abs(grove - 0.3f) / 0.3f : 0;
        if (r.edge > 0 && roll2 < r.edge * edge) return 'y';
        return '\0';
    }

    // Every tile round this one is sea (shallow, deep or another floe): for Frostfang's ice floes.
    bool OffShore(int x, int y)
    {
        for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx < 0 || ny < 0 || nx >= COLS || ny >= ROWS || map[ny, nx] is not ('w' or '~' or 'I')) return false;
            }
        return true;
    }

    // A plant never grows on something you've built there (an old save's shack on what is now a grove).
    bool BuiltOn(int x, int y) => state.builds.Any(b => b.y == y && x >= b.x && x < b.x + Data.BuildById[b.id].W);

    // Palms along a tropical beach: on sand within two tiles of the sea.
    bool BeachPalm(int x, int y, byte bi)
    {
        var r = PlantRules[bi];
        if (r.beach <= 0 || map[y, x] != 's') return false;
        bool sea = false;
        for (int dy = -2; dy <= 2 && !sea; dy++)
            for (int dx = -2; dx <= 2 && !sea; dx++)
            {
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < COLS && ny < ROWS && map[ny, nx] is '~' or 'w') sea = true;
            }
        if (!sea) return false;
        // Not right on the waterline, so the trunk stands on sand, and in loose rows rather than a fence.
        if (map[y + 1, x] is '~' or 'w' || map[y - 1, x] is '~' or 'w' || map[y, x + 1] is '~' or 'w' || map[y, x - 1] is '~' or 'w') return false;
        return Pix.Hash(x, y, 83) < r.beach * (0.5f + VNoise(x * T, y * T, 40, 930 + bi));
    }
}
