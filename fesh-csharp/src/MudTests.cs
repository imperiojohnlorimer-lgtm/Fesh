#if DEBUG
using Raylib_cs;

namespace Fesh;

partial class Game
{
    // Tidemane on the drained pond (FESH_MUD_TEST=1): its bed is ground, not water, so it gallops over it instead of swimming.
    IEnumerable<int> MudScript()
    {
        Note("Riding over the drained bangus pond");
        state = new State { created = true, tamed = true, look = new Look { name = "Rider" } };
        state.flags.metTomas = true; state.hinted["amihan"] = true; state.hinted["rs:tremor"] = true;
        mode = "play"; StartGame(false); ClearSkies(); SetNight(false); quietWildlife = true; standStill = true;
        Inp.ScriptMouse = Offscreen;
        state.hinted["rs:tremor"] = true; state.riding = true; yield return 3;
        Check("the pond is drained", PondDry && PondClosed);
        float mudX = 1622, poolX = 1645, y = 240;
        Check($"the mud is ground, the puddle in the middle still water ({DryMud(mudX, y)}, {DryMud(poolX, y)})", DryMud(mudX, y) && !DryMud(poolX, y) && TileAt((int)(mudX / T), (int)(y / T)) == 'l');
        (player.X, player.Y) = (mudX, y); yield return 2;
        Check("riding over the mud isn't swimming", Riding && !Swimming && !InWater);
        (player.X, player.Y) = (poolX, y); yield return 2;
        Check("in the puddle it still swims", Swimming);
        // What's drawn: over the mud Tidemane isn't hidden below a waterline.
        int Teal(float px)
        {
            (player.X, player.Y) = (px, y); player.Face = "right"; state.mountX = px; state.mountY = y;
            UpdateCamera(); pix.CamX = camX; pix.CamY = camY;
            RenderWorld(time);
            int n = 0;
            for (int j = (int)(y - camY) - 24; j <= (int)(y - camY) + 4; j++)
                for (int i = (int)(px - camX) - 22; i <= (int)(px - camX) + 22; i++)
                    if (i >= 0 && j >= 0 && i < W && j < H) { var c = pix.Buf[j * W + i]; if (c.G > c.R + 40 && c.B > c.R + 30 && c.G > 120) n++; }
            return n;
        }
        Teal(mudX);
        pendingShot = "mud-ride"; yield return 2;
        // Once the pond has filled again the same spot is water.
        state.hinted["rs:pondFixed"] = true; state.gifts["rs_pondFixed"] = state.day - 3;
        Check("the pond is full again", !PondClosed);
        Check("and with the pond full it swims there again", (player.X = mudX) > 0 && Swimming);
    }
}
#endif
