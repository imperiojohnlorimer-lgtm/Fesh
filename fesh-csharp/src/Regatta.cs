namespace Fesh;

// Dado's regatta round the islets of Daang Pulo, after the old fiesta boat races. Once every islet is on your chart, Dado
// will race you: sail through the start buoys north of his jetty, then through each gate in turn (the next one flashes),
// and back through the start. Medals by time; beating his lolo's record (the silver time) wins the old agong, a clue to
// the vanishing moon, so the story never needs a gold.
sealed class Race { public float T; public int Next; }

partial class Game
{
    // Gate centres, in pixels: [0] is the start and finish line. Each gate is a pair of buoys across the course.
    static readonly (float x, float y)[] RaceGates =
    {
        (645, 535), (835, 565), (935, 645), (865, 735), (765, 705), (620, 698), (440, 725), (420, 590), (545, 600)
    };
    // About 1,330 px of water at the boat's best line: 14 s flat out with the plain sail, 11 s with the big one.
    const float GateR = 18, GoldTime = 20, SilverTime = 27, BronzeTime = 38, RaceLimit = 300;
    bool raceArmed;     // Dado has said go: crossing the start line starts the clock
    Race race;

    void TalkRegatta()
    {
        Say D(string t) => new("Dado", t);
        if (Has("boat") == 0) { Talk(new() { D("A regatta needs a boat, my friend! Build one and come back.") }); return; }
        if (Stormy) { Talk(new() { D("Not in this storm. The islets will still be here tomorrow.") }); return; }
        raceArmed = true;
        string best = state.regattaBest > 0 ? $" Your best is {RaceTime(state.regattaBest)}." : "";
        Talk(new()
        {
            D($"A race! Take your boat through the start buoys north of my jetty, then through every gate in turn, and back through the start.{best}"),
            D($"My lolo's record is {RaceTime(SilverTime)}. Beat it and his old agong is yours. Under {RaceTime(GoldTime)} is gold, under {RaceTime(BronzeTime)} bronze. The gates are on your chart. Go!")
        }, () => Toast("Board your boat and sail through the start buoys north of Dado's jetty. The next gate flashes gold.", 5));
    }

    static string RaceTime(float s) => $"{(int)s / 60}:{(int)s % 60:00}";

    // At the helm in play: the start line starts the race; each gate in order; back through the start finishes it.
    void UpdateRegatta(float dt)
    {
        if (race == null)
        {
            if (raceArmed && Aboard && Dist(player.X, player.Y, RaceGates[0].x, RaceGates[0].y) < GateR)
            {
                race = new Race { Next = 1 };
                raceArmed = false;
                Sfx.Play("coin");
                Floater("Go!", player.X, player.Y - 26, "#7fd36b");
            }
            return;
        }
        race.T += dt;
        string stop = !Aboard ? "You left your boat, so the race is off." : Stormy ? "A storm! Dado calls the race off." : race.T > RaceLimit ? "Too slow: Dado has gone home for supper." : null;
        if (stop != null) { race = null; Sfx.Play("fail"); Toast(stop + " Talk to Dado to race again."); return; }
        int target = race.Next < RaceGates.Length ? race.Next : 0;
        if (Dist(player.X, player.Y, RaceGates[target].x, RaceGates[target].y) >= GateR) return;
        if (target != 0)
        {
            race.Next++;
            Sfx.Play("blip");
            Floater($"Gate {target} of {RaceGates.Length - 1}", player.X, player.Y - 26, "#ffd76a");
            return;
        }
        FinishRace();
    }

    void FinishRace()
    {
        float t = race.T;
        race = null;
        bool record = state.regattaBest <= 0 || t < state.regattaBest;
        if (record) state.regattaBest = t;
        string medal = t <= GoldTime ? "gold" : t <= SilverTime ? "silver" : t <= BronzeTime ? "bronze" : null;
        int prize = medal switch { "gold" => 120, "silver" => 60, "bronze" => 30, _ => 0 };
        bool paid = prize > 0 && state.regattaDay != state.day;
        if (paid) { state.regattaDay = state.day; state.coins += prize; }
        Sfx.Play(medal != null ? "rare" : "catch");
        string msg = $"Finished in {RaceTime(t)}{(record ? " (your best!)" : "")}. " + (medal == null ? "No medal this time." : $"A {medal} medal!")
            + (paid ? $" Dado sends {prize} coins." : medal != null ? " (One prize a day.)" : "");
        if (t <= SilverTime && !state.Hinted("bk:agong"))
        {
            state.hinted["bk:agong"] = true;
            Give("agong");
            Save();
            Talk(new()
            {
                new("", msg),
                new("Dado", "You beat my lolo's record! Nobody has since he set it. Then this is yours, by the old rules: his agong."),
                new("Dado", "They say the keepers at Parola beat it on full-moon nights, to keep the moon safe. Tatay Celso would know."),
                new("", "New clue on your Case board (Habagat): The regatta agong.")
            });
            return;
        }
        Toast(msg, 5);
        Save();
    }

    // The buoys at each gate (always there; the next one flashes during a race).
    void DrawRaceBuoys(float t)
    {
        if (player.Y < (HabagatTop - 20) * T || player.X > 1100) return;
        for (int i = 0; i < RaceGates.Length; i++)
        {
            var (gx, gy) = RaceGates[i];
            if (gx < camX - 30 || gx > camX + W + 30 || gy < camY - 30 || gy > camY + H + 30) continue;
            bool next = race != null && (race.Next < RaceGates.Length ? race.Next : 0) == i || race == null && raceArmed && i == 0;
            bool flash = next && (int)(t * 4) % 2 == 0;
            foreach (int side in new[] { -1, 1 })
            {
                float bx = gx + side * (GateR - 4), by = gy;
                int bob = (int)MathF.Round(MathF.Sin(t * 2.2f + i + side) * 0.8f);
                pix.Ring(bx, by + 2, 2.5 + MathF.Sin(t * 2 + i) * 0.4f, Pal.Rgba(230, 246, 250, 0.4f));
                pix.Rect(bx - 1, by - 2 + bob, 3, 3, flash ? "#ffd76a" : i == 0 ? "#f2efe6" : "#e04b3a");
                pix.Rect(bx, by - 6 + bob, 1, 4, "#5b3a24");
                pix.Rect(bx + 1, by - 6 + bob, 3, 2, i == 0 ? "#2f7fa3" : flash ? "#ffd76a" : "#f2efe6");
            }
            if (next) pix.Ring(gx, gy, 6 + (t * 6 % 6), Pal.Rgba(255, 215, 106, 0.5f));
        }
    }
}
