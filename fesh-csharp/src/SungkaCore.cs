namespace Fesh;

// The sungka board with no screen and no Lola in it (1.30): the rules shared by Lola Pacing's board (Sungka.cs) and the
// LAN game between two people (SungkaSession.cs). Seat 0 owns pits 0-6 and the head at 7; seat 1 owns pits 8-14 and the
// head at 15. Seven shells in each of the fourteen houses: 98 in all, and every move keeps all 98 on the board.
static class SungkaRules
{
    public const int Shells = 98;
    public static int HeadOf(int seat) => seat == 0 ? 7 : 15;
    public static bool Owns(int seat, int pit) => seat == 0 ? pit is >= 0 and <= 6 : pit is >= 8 and <= 14;
    public static int Opposite(int pit) => 14 - pit;

    public static int[] Start()
    {
        var b = new int[16];
        for (int i = 0; i < 16; i++) b[i] = i is 7 or 15 ? 0 : 7;
        return b;
    }

    // Sows from a pit, with relay sowing and captures. `second` is the player whose head is 15. Returns true if the mover
    // gets another turn. The old guard against a relay that never ends dropped the shells still in hand; now they go to
    // the mover's head, so no shell is ever lost.
    public static bool Sow(int[] b, int pit, bool second, out int last)
    {
        int own = second ? 15 : 7, theirs = second ? 7 : 15;
        int hand = b[pit];
        b[pit] = 0;
        last = pit;
        int guard = 0;
        for (; guard < 2000 && hand > 0; guard++)
        {
            last = (last + 1) % 16;
            if (last == theirs) continue;
            b[last]++;
            hand--;
            if (hand > 0) continue;
            if (last == own) return true;
            // A house that had shells before this one: pick them all up and keep going.
            if (b[last] > 1) { hand = b[last]; b[last] = 0; continue; }
            bool mine = second ? last is >= 8 and <= 14 : last is >= 0 and <= 6;
            if (mine && b[Opposite(last)] > 0)
            {
                b[own] += b[Opposite(last)] + 1;
                b[Opposite(last)] = 0;
                b[last] = 0;
            }
        }
        if (hand > 0) b[own] += hand;
        return false;
    }

    public static bool SideEmpty(int[] b, int seat)
    {
        int from = seat == 0 ? 0 : 8;
        for (int i = from; i < from + 7; i++) if (b[i] != 0) return false;
        return true;
    }
}

// One game between two seats. Pure: moves in, board out, the same on both computers.
sealed class SungkaMatch
{
    public int[] Pits = SungkaRules.Start();
    public int Turn, FirstSeat;            // the seat to move; who began
    public int Revision;                    // moves played so far
    public bool Over;
    public int Last = -1;                   // where the last shell fell, for the highlight
    public SungkaMatch(int first) { Turn = FirstSeat = first; }

    public bool CanMove(int seat, int pit) => !Over && seat == Turn && SungkaRules.Owns(seat, pit) && Pits[pit] > 0;

    // Plays a move if it's legal (it never throws on bad input: a stranger on the network can send anything).
    public bool Apply(int seat, int pit)
    {
        if (!CanMove(seat, pit)) return false;
        bool again = SungkaRules.Sow(Pits, pit, seat == 1, out Last);
        Revision++;
        if (SungkaRules.SideEmpty(Pits, 0) || SungkaRules.SideEmpty(Pits, 1)) { Settle(); Over = true; return true; }
        Turn = again ? seat : 1 - seat;
        return true;
    }

    // What's left on each side goes to its owner's head (the end of the game, or the clock running out).
    public void Settle()
    {
        for (int i = 0; i < 7; i++) { Pits[7] += Pits[i]; Pits[i] = 0; Pits[15] += Pits[8 + i]; Pits[8 + i] = 0; }
    }

    public int Score(int seat) => Pits[SungkaRules.HeadOf(seat)];
    // The winning seat, or -1 for a draw (only meaningful once Over).
    public int Winner => Score(0) > Score(1) ? 0 : Score(1) > Score(0) ? 1 : -1;
    public int Total => Pits.Sum();

    // The leftmost house with shells, for a move played for someone who ran out of time.
    public int AutoPit()
    {
        int from = Turn == 0 ? 0 : 8;
        for (int i = from; i < from + 7; i++) if (Pits[i] > 0) return i;
        return -1;
    }

    // A fingerprint of the whole position, so each side can tell the other it sees the same board.
    public int Digest()
    {
        unchecked
        {
            int h = (int)2166136261;
            foreach (int p in Pits) h = (h ^ p) * 16777619;
            h = (h ^ Turn) * 16777619;
            h = (h ^ Revision) * 16777619;
            h = (h ^ (Over ? 1 : 0)) * 16777619;
            return h;
        }
    }
}
