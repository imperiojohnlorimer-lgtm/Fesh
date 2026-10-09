namespace Fesh;

// Rondalla evenings in Amihan Village (1.15). Lira is cooking for a village supper and asks you for a pot of
// ginataang isda or sinigang na isda; once you've brought one, on fair evenings (18:00 to 22:00) three villagers sit
// out in the square with a bandurria, a guitar and a bass and play, and the village's music turns to strings.
partial class Game
{
    const float RondallaX = 1660, RondallaY = 203;          // the middle player's feet, in the square north of the pond
    static readonly string[] SupperDishes = { "ginataan", "sinigang" };

    // Evening, no storm, after the supper: the band is out...
    bool RondallaOut => state.Hinted("rondalla") && scene == "world" && state.clock >= 18 * 60 && state.clock < 22 * 60 && state.weather != "storm";
    // ...and you can hear them from the village.
    bool RondallaPlaying => RondallaOut && Dist(player.X, player.Y, RondallaX, RondallaY) < 230;

    string SupperDish() => state.Hinted("rondalla") ? null : SupperDishes.FirstOrDefault(d => Has(d) > 0);

    // Lira's supper: true when she talked about it (asked, or took the dish).
    bool LiraSupper()
    {
        if (state.Hinted("rondalla")) return false;
        Say L(string t) => new("Lira", t);
        if (SupperDish() is string dish)
        {
            Take(dish);
            state.hinted["rondalla"] = true;
            state.coins += 60;
            Sfx.Play("coin");
            Save();
            Talk(new()
            {
                L($"{Items.ById[dish].Name}! It smells like my lola's kitchen. Salamat: the whole village eats tonight. Here, 60 coins for the pot."),
                L("Come back to the square any evening after six. Once everyone's eaten, the rondalla comes out: bandurria, guitar and bass, playing the old songs until late.")
            });
            return true;
        }
        if (state.Hinted("liraSupperAsked")) return false;
        state.hinted["liraSupperAsked"] = true;
        Save();
        Talk(new()
        {
            L("Mabuhay! Welcome to Amihan. You've crossed a long stretch of sea to find our village."),
            L("We're having a supper for the whole village, and I'm one pot short. Could you cook one at a stove? Ginataang isda (fish in coconut milk) or sinigang na isda (fish in a sour calamansi broth)."),
            L("Bring it to me and you'll hear something worth the crossing. Meanwhile, here: I always keep a bowl for visiting fishers.")
        }, () =>
        {
            if (state.gifts.GetValueOrDefault("lira_meal") == state.day) return;
            LiraMeal();
        });
        return true;
    }

    // Three players on a bench in the square: a bandurria (small, pear-shaped), a guitar, and a big bass.
    void AddRondalla(List<(float y, Action draw)> list)
    {
        if (MathF.Abs(RondallaX - player.X) > W || MathF.Abs(RondallaY - player.Y) > H) return;
        var band = new[] { (dx: -13, dy: -2, shirt: "#e6a843", inst: "bandurria"), (dx: 0, dy: 0, shirt: "#367caa", inst: "guitar"), (dx: 13, dy: -2, shirt: "#b45b66", inst: "bass") };
        for (int i = 0; i < band.Length; i++)
        {
            var p = band[i];
            int x = (int)RondallaX + p.dx, y = (int)RondallaY + p.dy, k = i;
            list.Add((y, () =>
            {
                pix.Rect(x - 4, y - 2, 9, 2, "#8a6440");
                int strum = (int)(time * (k == 0 ? 12 : 4) + k) % 2;
                LookData.DrawFigure(pix, "#a9714b", "#2b1d14", p.shirt, "#3b3a4a", k == 2, k == 1 ? 3 : 0, "#e04b3a", x, y, "down", 0,
                    bob: (int)(time * 1.5f + k) % 2, arms: 3, swing: strum, blink: (time + k) % 3.3f < 0.12f);
                switch (p.inst)
                {
                    case "bandurria": pix.Rect(x - 2, y - 6, 4, 3, "#c98b3a"); pix.Rect(x - 1, y - 5, 1, 1, "#3b2a1d"); pix.Line(x + 2, y - 5, x + 5, y - 8, "#6b4a2b"); break;
                    case "guitar": pix.Rect(x - 3, y - 6, 5, 4, "#b5764a"); pix.Rect(x - 1, y - 5, 1, 1, "#3b2a1d"); pix.Line(x + 2, y - 5, x + 6, y - 9, "#6b4a2b"); break;
                    default: pix.Rect(x - 3, y - 9, 6, 7, "#8a5a32"); pix.Rect(x - 1, y - 6, 1, 1, "#3b2a1d"); pix.Line(x, y - 9, x, y - 15, "#5b3a24"); break;
                }
                // A note drifting up now and then.
                double ph = (time * 0.6 + k * 0.37) % 1;
                if (ph < 0.6) pix.Rect(x + 3 + (int)(ph * 6), y - 18 - (int)(ph * 10), 1, 2, Pal.Rgba(255, 244, 214, (float)(0.9 * (1 - ph / 0.6))));
            }));
        }
    }
}
