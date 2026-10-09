using Raylib_cs;

namespace Fesh;

// Sungka with Lola Pacing on Daang Pulo: a long wooden board with seven little houses on each side and a big "head" at
// each end, seven cowries in every house. You sow round the board (skipping her head), and:
//   - land your last shell in your own head and you go again;
//   - land it in a house that already has shells and you pick them all up and keep sowing;
//   - land it in an empty house on your side and you capture it, with everything in her house opposite.
// The game ends when either side's houses are empty; what's left goes to its owner. (The real game starts with both
// players sowing at once; here you go first.)
partial class Game
{
    // Pits 0-6 are your houses (left to right along the bottom), 7 your head (right end), 8-14 Lola's houses (right to
    // left along the top), 15 her head (left end).
    int[] sungka;
    bool sungkaLolaTurn, sungkaOver;
    float sungkaWait;
    int sungkaLast = -1;
    string sungkaNote = "";

    void TalkPacing()
    {
        Say L(string t) => new("Lola Pacing", t);
        FaceToward(HabagatWalk("pacing").X, HabagatWalk("pacing").Y);
        if (!state.Hinted("metPacing"))
        {
            state.hinted["metPacing"] = true;
            Talk(new()
            {
                L("Ay, a visitor! Sit, sit. Do you play sungka?"),
                L("Seven shells in each little house. You sow them round, one by one, and never into my head, only into yours."),
                L("End in your own head and you go again. End where there are shells already and you pick them up and carry on. End in an empty house on your side and you take mine across from it.")
            }, OpenSungka);
            return;
        }
        // Out on the flats at the low tide you found her (Tides.cs).
        if (ReefWalkOn && ReefThanked)
        {
            Talk(new() { L("Look, apo: under that rock, something moved! Gently now. We'll go home when the sea comes back.") });
            return;
        }
        if (ReefWalkOn)
        {
            ThankReefWalk();
            Talk(new()
            {
                L(FlatsDry ? "You came! And right on the tide. Look how far the sea has gone: you can walk out to where the reef starts."
                    : "You came! And right on the tide. The sea's not gone far today, but the sand by the water is full of good things."),
                L("Take the big shells and leave the little ones to grow. If you turn a stone over, turn it back the way it was: there's a whole family living under it."),
                L("Here, three cowries from my pocket, and 80 coins from the co-op for helping the children. We'll be out here until the tide turns. Come and find me again at the next big low.")
            });
            return;
        }
        // The second time you come by, she teaches you the tides and gives you the tide table.
        if (!TidesLearned) { TeachTides(); return; }
        if (state.Hinted("reefMissed"))
        {
            state.hinted.Remove("reefMissed");
            Talk(new() { L("You missed the low tide, apo! The sea doesn't wait for anyone. Ask me for another riddle tomorrow.") }, OpenSungka);
            return;
        }
        OpenSungka();
    }

    void OpenSungka()
    {
        sungka = new int[16];
        for (int i = 0; i < 16; i++) sungka[i] = i is 7 or 15 ? 0 : 7;
        sungkaLolaTurn = sungkaOver = false;
        sungkaLast = -1; sungkaWait = 0;
        sungkaNote = "Your turn: click one of your houses along the bottom.";
        Sfx.Play("ui");
        panel = "sungka";
        mode = "panel";
        SetPrompt("");
    }

    static int Opposite(int pit) => 14 - pit;

    // Sows from a pit, with relay sowing and captures. Returns true if the mover gets another turn.
    static bool SungkaSow(int[] b, int pit, bool lola, out int last)
    {
        int own = lola ? 15 : 7, theirs = lola ? 7 : 15;
        int hand = b[pit];
        b[pit] = 0;
        last = pit;
        for (int guard = 0; guard < 2000 && hand > 0; guard++)
        {
            last = (last + 1) % 16;
            if (last == theirs) continue;
            b[last]++;
            hand--;
            if (hand > 0) continue;
            if (last == own) return true;
            // A house that had shells before this one: pick them all up and keep going.
            if (b[last] > 1) { hand = b[last]; b[last] = 0; continue; }
            bool mine = lola ? last is >= 8 and <= 14 : last is >= 0 and <= 6;
            if (mine && b[Opposite(last)] > 0)
            {
                b[own] += b[Opposite(last)] + 1;
                b[Opposite(last)] = 0;
                b[last] = 0;
            }
        }
        return false;
    }

    static bool SideEmpty(int[] b, bool lola) => Enumerable.Range(lola ? 8 : 0, 7).All(i => b[i] == 0);

    void SungkaMove(int pit, bool lola)
    {
        bool again = SungkaSow(sungka, pit, lola, out int last);
        sungkaLast = last;
        Sfx.Play("blip");
        if (SideEmpty(sungka, false) || SideEmpty(sungka, true)) { FinishSungka(); return; }
        sungkaLolaTurn = again ? lola : !lola;
        // Someone with no shells on their side has to pass.
        if (SideEmpty(sungka, sungkaLolaTurn)) sungkaLolaTurn = !sungkaLolaTurn;
        sungkaWait = 0.9f;
        sungkaNote = again ? (lola ? "Lola ended in her head and goes again." : "Into your head: go again!") : sungkaLolaTurn ? "Lola is thinking..." : "Your turn.";
    }

    void FinishSungka()
    {
        for (int i = 0; i < 7; i++) { sungka[7] += sungka[i]; sungka[i] = 0; sungka[15] += sungka[8 + i]; sungka[8 + i] = 0; }
        sungkaOver = true;
        bool won = sungka[7] > sungka[15];
        string result = won ? "You win!" : sungka[7] == sungka[15] ? "A draw!" : "Lola wins.";
        if (won)
        {
            state.sungkaWins++;
            if (state.sungkaDay != state.day) { state.sungkaDay = state.day; state.coins += 40; result += " Lola hands you 40 coins."; Sfx.Play("coin"); }
            else Sfx.Play("rare");
        }
        else Sfx.Play("fail");
        sungkaNote = $"{result} ({sungka[7]} to {sungka[15]})";
        Save();
    }

    // Lola looks one move ahead: whatever puts the most in her head (an extra turn counts for a few more).
    int LolaPick()
    {
        int best = -1; float bestScore = float.MinValue;
        for (int pit = 8; pit <= 14; pit++)
        {
            if (sungka[pit] == 0) continue;
            var b = (int[])sungka.Clone();
            bool again = SungkaSow(b, pit, true, out _);
            float score = b[15] - sungka[15] + (again ? 4 : 0) - (sungka[7] - b[7]) + FxRand(0, 1.5f);
            if (score > bestScore) { bestScore = score; best = pit; }
        }
        return best;
    }

    void CloseSungka()
    {
        bool first = !state.Hinted("bk:tale");
        ClosePanels();
        if (!first) return;
        state.hinted["bk:tale"] = true;
        Save();
        Talk(new()
        {
            new("Lola Pacing", "You play like my grandson: too fast! Now listen, I'll tell you why we keep these shells."),
            new("Lola Pacing", "Long ago Bathala hung seven moons in the sky. A sea serpent, Bakunawa, rose out of the deep and swallowed them, one after another."),
            new("Lola Pacing", "When it came for the last one, everyone ran out with their pots and gongs and made such a noise that it spat the moon back out."),
            new("Lola Pacing", "When the lighthouse at Parola still burned, the keepers beat a gong on full-moon nights. Now it's dark... and some nights the moon goes out on the water. Hmm."),
            new("", "New clue on your Case board (Habagat): The seven moons.")
        });
    }

    void DrawSungka()
    {
        Backdrop();
        const float w = 1040, h = 470, pad = 26;
        float x = (Gfx.LW - w) / 2, y = (Gfx.LH - h) / 2;
        Gfx.Box(x, y, w, h, Pal.Paper, Pal.Ink, 3, 8, 6);
        Gfx.Text("Sungka with Lola Pacing", x + pad, y + pad, FontKind.Ui700, 32, Pal.PaperInk);
        if (SmallButton("Close", x + w - pad - SmallW("Close"), y + pad - 4)) { CloseSungka(); return; }
        Gfx.Text(Gfx.Ellipsize(sungkaNote, FontKind.Ui600, 19, w - pad * 2), x + pad, y + pad + 46, FontKind.Ui600, 19, sungkaOver ? Pal.C("#9a6a1a") : Muted);

        // The board: a long boat-shaped plank, Lola's head at the left, yours at the right.
        float bx = x + pad + 10, by = y + 130, bw = w - pad * 2 - 20, bh = 230;
        Gfx.Rect(bx, by, bw, bh, Pal.C("#8a5f36"), 40);
        Gfx.Rect(bx + 6, by + 6, bw - 12, bh - 12, Pal.C("#a8774a"), 36);
        float headW = 110, cell = (bw - headW * 2 - 40) / 7;
        void Shells(float cx, float cy, int n, float spread)
        {
            for (int i = 0; i < Math.Min(n, 24); i++)
            {
                float a = i * 2.4f, r = spread * MathF.Sqrt((i + 0.5f) / 24f);
                Gfx.Circle(cx + MathF.Cos(a) * r, cy + MathF.Sin(a) * r * 0.8f, 4.2f, Pal.C(i % 3 == 0 ? "#e8d2a8" : "#f2e2c0"));
            }
        }
        void Pit(int i, float cx, float cy, float r, bool head)
        {
            bool mine = i is >= 0 and <= 6;
            bool can = mine && !sungkaLolaTurn && !sungkaOver && sungka[i] > 0 && sungkaWait <= 0;
            var fill = i == sungkaLast ? Pal.C("#5a3a20") : Pal.C("#4a2f1d");
            if (can && Gfx.Hover(cx - r, cy - r, r * 2, r * 2)) fill = Pal.C("#6b4a2b");
            if (head) Gfx.Rect(cx - r * 0.8f, cy - r * 1.4f, r * 1.6f, r * 2.8f, fill, r * 0.8f);
            else Gfx.Circle(cx, cy, r, fill);
            Shells(cx, cy, sungka[i], head ? r * 0.7f : r * 0.62f);
            // Counts go on the owner's side: above Lola's, below yours.
            string n = sungka[i].ToString();
            float reach = head ? r * 1.4f : r;
            Gfx.TextCenter(n, cx, i >= 8 ? cy - reach - 22 : cy + reach + 4, FontKind.Ui700, 17, i == 7 ? Pal.C("#fff2c0") : Pal.Paper);
#if DEBUG
            Gfx.Seen[$"sungka:{i}"] = new Rectangle(cx - r, cy - r, r * 2, r * 2);
#endif
            if (can && Gfx.Click(cx - r, cy - r, r * 2, r * 2)) { SungkaMove(i, false); }
        }
        float cy0 = by + bh * 0.3f, cy1 = by + bh * 0.7f;
        for (int k = 0; k < 7; k++)
        {
            float cx = bx + headW + 20 + cell * (k + 0.5f);
            Pit(14 - k, cx, cy0, cell * 0.36f, false);   // Lola's houses, read right to left from her side
            Pit(k, cx, cy1, cell * 0.36f, false);
        }
        Pit(15, bx + headW / 2 + 10, by + bh / 2, 34, true);
        Pit(7, bx + bw - headW / 2 - 10, by + bh / 2, 34, true);
        Gfx.Text("Lola", bx + 20, by - 26, FontKind.Ui700, 18, Muted);
        Gfx.Text("You", bx + bw - 60, by + bh + 8, FontKind.Ui700, 18, Muted);

        if (sungkaOver && BigButton("Play again", x + w - pad - BigW("Play again"), y + h - pad - 56, true)) { OpenSungka(); return; }
        if (Gfx.PressedOutside(x, y, w, h)) CloseSungka();
    }

    // Lola takes her turn after a moment (Update calls this while the board is open).
    void UpdateSungka(float dt)
    {
        if (sungka == null || sungkaOver) return;
        if (sungkaWait > 0) { sungkaWait -= dt; return; }
        if (sungkaLolaTurn && LolaPick() is int pick && pick >= 0) SungkaMove(pick, true);
    }
}
