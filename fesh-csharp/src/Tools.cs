namespace Fesh;

// Tools in hand (1.16): what you swing is held in your hands and your arms do the work. Each action names its tool
// (Swing), and HandsPose works out, frame by frame, where the hands are and which way the tool points: a wind-up, the
// strike landing on the tile in front of you, a moment's hold, then lowering it. The rod is held the same way: cocked
// over your shoulder while you charge, whipped forward on the cast, and while you reel the other hand turns the reel.
// Arms reach from the shoulders to the hands (LookData, arms 4), so the tool turns in the grip instead of floating.
sealed class HandPose
{
    public (int x, int y) Hand;            // the near hand (side view) or the right hand (front and back)
    public (int x, int y)? Hand2;          // the other hand, when it's on the tool too
    public string Tool;                    // what to draw from the hand: axe, pick, sword, mallet, rake, rod, spear, or null (bare hands)
    public float Dx, Dy;                   // the way the tool points from the hand (screen pixels per pixel of length)
    public float Length;                   // how long it is from the hand, in pixels
    public bool ToolBehind, ArmsBehind;    // drawn before the body (pointing away from you), arms tucked behind it
    public string Tint;                    // the tool head's colour (a pickaxe's or sword's metal)
}

partial class Game
{
    string swingTool = "axe";   // what the swing in progress is done with (Swing)
    float swingDur = 0.25f;     // how long that swing lasts, so swingT gives how far into it you are

    // Every action that swings something says what: "axe", "pick", "sword", "fist", "mallet", "rake", "haul", "hands",
    // "dig" or "throw". The strike lands about a third of the way through.
    void Swing(string tool, float secs)
    {
        swingTool = tool;
        swingT = swingDur = secs;
    }

    // A fight is with the best blade you have, else a pickaxe, else your fists.
    string WeaponTool() => Items.Weapons.Any(w => Has(w.id) > 0) ? "sword" : PickTier > 0 ? "pick" : "fist";

    // The arm's elevation (degrees: 0 points straight ahead, 90 straight up, 180 behind, -90 down) and reach (pixels
    // from the shoulder) through a swing, p going 0 to 1; and how much more the tool tips forward than the arm.
    static (float elev, float reach, float bend, bool twoHands) SwingKey(string tool, float p)
    {
        static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0, 1);
        switch (tool)
        {
            case "sword":
                // A quick slash from over the shoulder.
                if (p < 0.12f) return (115, 3, -5, false);
                if (p < 0.28f) return (Lerp(115, -35, (p - 0.12f) / 0.16f), 4, -5, false);
                if (p < 0.5f) return (-35, 4, -5, false);
                return (Lerp(-35, -75, (p - 0.5f) / 0.5f), Lerp(4, 3, (p - 0.5f) / 0.5f), -5, false);
            case "fist":
                if (p < 0.15f) return (0, 1, 0, false);
                if (p < 0.35f) return (0, Lerp(1, 5, (p - 0.15f) / 0.2f), 0, false);
                if (p < 0.6f) return (0, 5, 0, false);
                return (Lerp(0, -70, (p - 0.6f) / 0.4f), Lerp(5, 3, (p - 0.6f) / 0.4f), 0, false);
            case "throw":
                if (p < 0.25f) return (150, 3, 0, false);
                if (p < 0.45f) return (Lerp(150, 10, (p - 0.25f) / 0.2f), 4, 0, false);
                return (Lerp(10, -70, (p - 0.45f) / 0.55f), Lerp(4, 3, (p - 0.45f) / 0.55f), 0, false);
            case "rake":
                // Reach out along the bed, then draw the salt in towards you.
                if (p < 0.2f) return (-15, 5, -35, true);
                if (p < 0.75f) return (Lerp(-15, -55, (p - 0.2f) / 0.55f), Lerp(5, 3, (p - 0.2f) / 0.55f), -35, true);
                return (-55, 3, -35, true);
            case "haul":
                // Both hands down to the trap, then up hand over hand.
                if (p < 0.3f) return (-50, 5, 0, true);
                if (p < 0.7f) return (Lerp(-50, 30, (p - 0.3f) / 0.4f), Lerp(5, 3, (p - 0.3f) / 0.4f), 0, true);
                return (Lerp(30, -40, (p - 0.7f) / 0.3f), 3, 0, true);
            case "hands":
                if (p < 0.5f) return (-20, 5, 0, true);
                return (Lerp(-20, -65, (p - 0.5f) / 0.5f), Lerp(5, 3, (p - 0.5f) / 0.5f), 0, true);
            case "dig":
                return (-60 + 18 * MathF.Sin(p * MathF.PI * 4), 5, 0, true);
            default:
                // An axe, a pickaxe or the agong's mallet: up and back, then down onto the tile in front. The axe
                // bites and follows through; a pickaxe bounces back off the rock; the mallet springs off the gong.
                bool two = tool != "mallet";
                if (p < 0.15f) return (130, 3, -15, two);
                if (p < 0.3f) return (Lerp(130, -25, (p - 0.15f) / 0.15f), 4, -15, two);
                if (tool == "pick" && p < 0.55f) return (p < 0.4f ? -25 : 5, 4, -15, two);
                if (tool == "pick") return (Lerp(5, -70, (p - 0.55f) / 0.45f), Lerp(4, 3, (p - 0.55f) / 0.45f), -15, two);
                if (tool == "mallet") return (p < 0.4f ? -25 : Lerp(-25, 40, (p - 0.4f) / 0.6f), 4, -15, false);
                if (p < 0.55f) return (p < 0.38f ? -17 : -25, 4, -15, two);
                return (Lerp(-25, -70, (p - 0.55f) / 0.45f), Lerp(4, 3, (p - 0.55f) / 0.45f), -15, two);
        }
    }

    // A direction in the swing's plane (elevation, degrees) as it looks on screen for each facing. From the side,
    // ahead is left or right; from the front, ahead comes toward you (lower on screen) and from the back it goes away
    // (higher), at about half scale as the view looks down at a slant, so a swing seen end-on is shorter. From the
    // back, a raised tool tips toward you over your head, and the blow lands out of sight in front of you.
    static (float x, float y) Project(string face, float elev)
    {
        float a = elev * MathF.PI / 180, c = MathF.Cos(a), s = MathF.Sin(a);
        return face switch
        {
            "right" => (c, -s),
            "left" => (-c, -s),
            "down" => (0.18f * c, -s + 0.6f * c),
            _ => (0.18f * c, -s - 0.5f * c)
        };
    }

    // Quantised to 15 degrees, so a swing steps through a few clean frames instead of smearing pixels.
    static float Snap(float deg) => MathF.Round(deg / 15) * 15;

    // Where the hands go and what's in them this frame, for an upper body at (ux, uy) (after bob, lean and the saddle).
    // Null when your hands are free (or holding a catch overhead, which has its own pose).
    HandPose HandsPose(string face, int ux, int uy, bool rod, int pump)
    {
        bool side = face is "left" or "right";
        int dir = face == "left" ? -1 : 1;
        var near = LookData.NearShoulder(ux, uy, face);
        if (mode == "spear" && thrown == null && spears > 0)
        {
            // The spear raised beside your head, ready to throw.
            int sdir = aimX < player.X ? -1 : 1;
            var h = (x: ux + sdir * 3, y: uy - 9);
            return new HandPose { Hand = h, Tool = "spear", Dx = sdir * 0.3f, Dy = -1, Length = 7 };
        }
        if (rod) return RodPose(face, ux, uy, pump);
        if (mode == "spear") return null;
        // Nothing being swung: whatever you're holding on the hotbar, carried (Hotbar.cs).
        if (swingT <= 0) return HeldTool is string carried && mode is "play" or "build" or "dialogue" ? CarryPose(face, ux, uy, carried) : null;
        float p = 1 - swingT / Math.Max(0.01f, swingDur);
        var (elev, reach, bend, two) = SwingKey(swingTool, p);
        elev = Snap(elev);
        var (ax, ay) = Project(face, elev);
        // From the front or back the swing runs down the middle, a pixel right of centre so it clears the face; raised,
        // the hands go up beside your head, not in front of your face.
        var shoulder = side ? near : (ux + 1 + (int)MathF.Round(2 * Math.Clamp((elev - 15) / 60, 0, 1)), uy - 7);
        var hand = (x: (int)MathF.Round(shoulder.Item1 + ax * reach), y: (int)MathF.Round(shoulder.Item2 + ay * reach));
        float toolElev = Snap(elev + bend);
        var (tx, ty) = Project(face, toolElev);
        string tool = swingTool switch { "axe" or "pick" or "sword" or "mallet" or "rake" => swingTool, _ => null };
        float len = swingTool switch { "sword" => 7, "rake" => 8, "mallet" => 5, _ => 7 };
        // The other hand lower down the handle (or beside the first, on a rope or in the dirt).
        (int x, int y)? hand2 = !two ? null
            : tool is "axe" or "pick" or "rake" ? ((int)MathF.Round(hand.x - tx * 2), (int)MathF.Round(hand.y - ty * 2))
            : side ? (hand.x - dir, hand.y + 1) : (hand.x - 2, hand.y);
        if (!side && !two) hand2 = null;
        float ahead = MathF.Cos(toolElev * MathF.PI / 180);
        return new HandPose
        {
            Hand = hand, Hand2 = hand2, Tool = tool, Dx = tx, Dy = ty, Length = len,
            // Seen from the front, a tool raised behind your head is behind you; from the back, one held out in front is.
            ToolBehind = face == "down" ? ahead < -0.2f : face == "up" && ahead > 0.2f,
            ArmsBehind = face == "up" && MathF.Cos(elev * MathF.PI / 180) > 0.2f,
            Tint = tool == "pick" ? Items.ById[Items.Pickaxes[Math.Clamp(PickTier, 1, Items.Pickaxes.Length) - 1]].Tint
                : tool == "sword" ? Items.Weapons.Where(w => Has(w.id) > 0).Select(w => Items.ById[w.id].Tint).FirstOrDefault() : null
        };
    }

    // The rod in both hands: the near hand on the grip and the other on the reel, which turns while you reel in.
    // RodTip says where the tip is; the hands go where a person would hold a rod pointing there.
    HandPose RodPose(string face, int ux, int uy, int pump)
    {
        bool side = face is "left" or "right";
        int dir = face == "left" ? -1 : 1;
        // The grip in front of you (as the old arms-3 pose had it)...
        var rest = side ? (x: ux + (dir > 0 ? 2 : -3), y: uy - 6 + pump) : (x: ux + 2, y: uy - 6 + pump);
        // ...cocked back over your shoulder while you load a cast...
        var cocked = side ? (x: ux - dir * (1 + (int)(charge * 1.5f)), y: uy - 9 - (int)(charge * 1.5f)) : (x: ux + 2, y: uy - 10 - (int)(charge * 1.5f));
        (int x, int y) hand = rest;
        if (mode == "charging") hand = cocked;
        else if (mode == "casting" && fish != null && fish.T < 0.25f)
        {
            // ...and whipped forward on the throw.
            float k = fish.T / 0.25f;
            hand = ((int)MathF.Round(cocked.x + (rest.x + (side ? dir : 0) - cocked.x) * k), (int)MathF.Round(cocked.y + (rest.y - 1 - cocked.y) * k));
        }
        var tip = RodTip();
        float rx = tip.X - hand.x, ry = tip.Y - hand.y, len = MathF.Max(1, MathF.Sqrt(rx * rx + ry * ry));
        float ux1 = rx / len, uy1 = ry / len;
        // The reel sits just below the grip; the other hand is on its handle, going round while you reel in.
        var reel = (x: (int)MathF.Round(hand.x - ux1 * 2), y: (int)MathF.Round(hand.y - uy1 * 2) + 1);
        (int x, int y) hand2 = reel;
        if (mode == "reeling" && ReelHeld())
        {
            float a = time * 16;
            hand2 = ((int)MathF.Round(reel.x + MathF.Cos(a)), (int)MathF.Round(reel.y + 1 + MathF.Sin(a)));
        }
        else hand2 = (reel.x, reel.y + 1);
        return new HandPose
        {
            Hand = hand, Hand2 = hand2, Tool = "rod", Dx = ux1, Dy = uy1, Length = len,
            ArmsBehind = face == "up", ToolBehind = false, Tint = Items.ById.TryGetValue(BestRod(), out var r) ? r.Tint : null
        };
    }

    // The tool from the hand: a handle and its head (a blade, a pick, a mallet, a rake), a sword, the rod or a spear.
    void DrawToolPose(HandPose h)
    {
        if (h.Tool == null) return;
        float x0 = h.Hand.x, y0 = h.Hand.y;
        float n = MathF.Max(0.001f, MathF.Sqrt(h.Dx * h.Dx + h.Dy * h.Dy));
        // Seen end-on, a tool pointing at or away from you is shorter.
        float dx = h.Dx / n, dy = h.Dy / n, len = h.Length * MathF.Min(1, n);
        float ex = x0 + dx * len, ey = y0 + dy * len;
        // Across the handle: the side that leads the swing (ahead of you, or down when it points along your facing).
        float px = -dy, py = dx;
        int facing = player.Face == "left" ? -1 : 1;
        if (player.Face is "left" or "right" ? px * facing < 0 || (MathF.Abs(px) < 0.3f && py < 0) : py < 0) { px = -px; py = -py; }
        void Dot(float x, float y, string c) => pix.Rect(MathF.Round(x), MathF.Round(y), 1, 1, c);
        switch (h.Tool)
        {
            case "rod":
            {
                // A dark grip, the rod in its own colour, and the reel by the hand.
                string rodCol = h.Tint ?? "#8a6440";
                pix.Line(x0, y0, ex, ey, rodCol);
                pix.Line(x0 - dx * 2, y0 - dy * 2, x0, y0, "#3b2a1d");
                var reel = (x: MathF.Round(x0 - dx * 2), y: MathF.Round(y0 - dy * 2) + 1);
                pix.Rect(reel.x, reel.y, 1, 1, "#9aa0a5");
                pix.Rect(reel.x, reel.y + 1, 1, 1, "#5e6468");
                break;
            }
            case "spear":
                pix.Line(x0 - dx * 4, y0 - dy * 4, ex, ey, "#8a6440");
                Dot(ex, ey, "#e8f0f4");
                break;
            case "sword":
            {
                string blade = h.Tint ?? "#c9d4dc";
                Dot(x0 - dx, y0 - dy, "#5b3a24");                                     // the grip, behind the hand
                Dot(x0 + dx + px, y0 + dy + py, "#c9a54a"); Dot(x0 + dx - px, y0 + dy - py, "#c9a54a");   // the guard
                pix.Line(x0 + dx * 2, y0 + dy * 2, ex, ey, blade);
                Dot(ex, ey, "#ffffff");
                break;
            }
            default:
            {
                // A wooden handle with the head at the far end.
                pix.Line(x0 - dx, y0 - dy, ex, ey, "#8a6440");
                switch (h.Tool)
                {
                    case "axe":
                        // A stone blade on the leading side, with a bright edge.
                        for (int k = 0; k < 3; k++)
                        {
                            float bx = ex - dx * k, by = ey - dy * k;
                            Dot(bx + px, by + py, "#9aa0a5");
                            Dot(bx + px * 2, by + py * 2, k == 1 ? "#e6ecef" : "#c8ced2");
                        }
                        Dot(ex - px, ey - py, "#6e737a");
                        break;
                    case "pick":
                    {
                        // A curved head across the handle, in the pickaxe's metal, its points swept back.
                        string metal = h.Tint ?? "#8a8f93";
                        for (int k = -2; k <= 2; k++) Dot(ex + px * k - dx * (Math.Abs(k) == 2 ? 1 : 0), ey + py * k - dy * (Math.Abs(k) == 2 ? 1 : 0), Math.Abs(k) == 2 ? "#4a4e52" : metal);
                        break;
                    }
                    case "mallet":
                        Dot(ex, ey, "#e9dcc0"); Dot(ex + px, ey + py, "#e9dcc0"); Dot(ex + dx, ey + dy, "#d4c4a0"); Dot(ex + dx + px, ey + dy + py, "#d4c4a0");
                        break;
                    case "rake":
                        for (int k = -2; k <= 2; k++) { Dot(ex + px * k, ey + py * k, "#8a6440"); Dot(ex + px * k + dx, ey + py * k + dy, "#5b3a24"); }
                        break;
                }
                break;
            }
        }
    }
}
