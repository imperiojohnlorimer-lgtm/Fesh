using Raylib_cs;

namespace Fesh;

// Every fish its own picture, drawn from a little recipe: a body shape, three colours, a pattern and its colour, plus a
// line about it for the Fish log. Draw fills any box (a 12 px bag icon, the fish held over your head, the Fish log's big
// picture), facing right; crabs, lobsters, shrimp, squid and rays come from 12 px templates scaled to the box.
static class FishArt
{
    // Shape: fish, torpedo, deep, heavy, cat, long, eel, billed, shark, flying, mola, ray, squid, crab, lobster, shrimp.
    // Pattern: stripes (thin, across), bars (broad, across), bands (along), spots, specks, glow, or null.
    public sealed record Look(string Shape, string Body, string Belly, string Fin, string Pattern, string Mark, string About);

    public static readonly Dictionary<string, Look> Looks = new()
    {
        // Amihan
        ["bangus"] = new("torpedo", "#b9c8d0", "#eef3f5", "#8fa7b3", null, null, "The islands' own fish: silvery, bony, and best grilled with a squeeze of calamansi."),
        ["banak"] = new("fish", "#97a5a6", "#e2e8e6", "#6f7f80", "bands", "#7d8b8c", "A grey mullet that leaps clear of the pond whenever something startles it."),
        ["kitang"] = new("deep", "#8a945c", "#cfcd9e", "#5f6a3e", "spots", "#3b3a24", "A round scat freckled with dark spots, nibbling at the edge of the pond."),
        ["asohos"] = new("long", "#d6d2bd", "#f4f1e6", "#b4ad94", "bands", "#c4b484", "A slim, sandy-silver whiting that skims the lagoon's sandy bottom. Sweet, fried whole."),
        ["lapu_lapu"] = new("heavy", "#b2563d", "#d98a6a", "#7d3a28", "spots", "#5a2a1f", "A brick-red grouper that shares its name with Lapulapu, the chief who beat Magellan."),
        ["maya_maya"] = new("fish", "#d8584e", "#f2b1a3", "#a63a33", null, null, "A rosy snapper, the pride of every village fiesta."),
        ["talakitok"] = new("fish", "#8fa3b0", "#e6edf0", "#e3c04a", null, null, "A trevally with yellow fins, a hard charge and a temper to match."),
        ["hito"] = new("cat", "#4a4038", "#8a7a68", "#2f2822", null, null, "A whiskered catfish that gulps air when the water turns to mud."),
        ["dalag"] = new("long", "#5a5a3a", "#a9a27a", "#3c3c26", "bars", "#2f2f1e", "A snakehead that guards its young and bites anything that comes too close."),
        ["amihan_mudskipper"] = new("fish", "#7f765a", "#c9b98f", "#5f5236", "specks", "#3a3020", "It walks across the mangrove mud on its fins, and blinks at you."),
        ["tanigue"] = new("torpedo", "#6f8fa6", "#e3ecf0", "#4d6a80", "bars", "#4a6276", "Spanish mackerel: fast, toothy, and the best kinilaw in the islands."),
        ["yellowfin_tuna"] = new("torpedo", "#2f4d7a", "#dfe7ee", "#f2c53d", "bands", "#e8c840", "Long yellow fins and a gold stripe; it crosses whole seas."),
        ["amihan_barracuda"] = new("long", "#7f8f9a", "#e3e8ea", "#55636c", "bars", "#4a5660", "All teeth and patience: it hangs perfectly still, then strikes like lightning."),
        ["galunggong"] = new("torpedo", "#5a7a9a", "#e6edf3", "#d8604a", null, null, "The everyday fish of the islands, fried crisp for breakfast."),
        ["tulingan"] = new("torpedo", "#2a3a5a", "#e6edf3", "#1f2a44", "stripes", "#4a6a9a", "A little tuna, simmered for hours with kamias until even the bones go soft."),
        ["pating"] = new("shark", "#6f7f8a", "#e6edf0", "#1a1a1a", null, null, "A blacktip shark. The tips of its fins look dipped in ink."),
        ["malasugi"] = new("billed", "#1f3a6a", "#e6edf3", "#2f5aa0", "bars", "#7fb0f0", "The blue marlin of the Amihan Sea, leaping clear in a shower of spray."),
        // Saltmere
        ["pond_perch"] = new("fish", "#8aa04a", "#e3d98f", "#d9823f", "bars", "#4f6a2a", "A little striped perch: the first fish almost everyone on Saltmere catches."),
        ["mud_carp"] = new("fish", "#8a7448", "#c9b07a", "#6b5632", null, null, "A sleepy brown carp that sifts the lagoon mud for snacks."),
        ["moon_carp"] = new("fish", "#e6e0d0", "#ffffff", "#c9c0a8", "glow", "#9fb8e8", "Pale as moonlight. It only rises when the moon is full."),
        ["old_whiskers"] = new("cat", "#c98b3a", "#e0a84a", "#8a5f2a", "spots", "#8a5f2a", "The lagoon's oldest resident, with whiskers like an old sea captain."),
        ["rock_goby"] = new("fish", "#7a6a58", "#c9b9a0", "#5a4c3e", "specks", "#4a3e32", "A stubby goby that wedges itself in between the rocks."),
        ["striped_wrasse"] = new("fish", "#3f8fb0", "#bfe3ee", "#2f6f8a", "bands", "#f2c040", "Blue with gold stripes; it cleans other fishes' teeth for a living."),
        ["rusty_grouper"] = new("heavy", "#b5652a", "#d9965a", "#7a4220", "spots", "#6e3a1a", "Rust-coloured and as heavy as an anchor, it lurks inside the wreck."),
        ["barnacle_bass"] = new("fish", "#5f6a5a", "#b8bfa8", "#3f4a3c", "specks", "#d8d8c8", "So slow-moving that barnacles have settled on its back."),
        ["storm_eel"] = new("eel", "#3a4a6a", "#8fa0c8", "#2a3650", "glow", "#f3e36a", "It crackles in the shallows whenever a storm rolls in."),
        ["silver_tuna"] = new("torpedo", "#8fa8c0", "#eef3f8", "#5f7894", null, null, "A shining tuna that hunts the deep water past the old dock."),
        ["lantern_squid"] = new("squid", "#c96a8a", "#e8a0b8", "#a04a6a", "glow", "#ffe28a", "Its little lantern blinks to call its friends in the dark."),
        ["shore_crab"] = new("crab", "#4a7a5a", "#7fa888", "#2f5a3e", null, null, "A green shore crab with a bad attitude."),
        // Frostfang
        ["arctic_char"] = new("fish", "#6f7f8a", "#e8805a", "#e8805a", "specks", "#f2d0b0", "Pink-spotted and orange-bellied, swimming in water that ought to be ice."),
        ["frost_smelt"] = new("torpedo", "#c9dce6", "#f2f8fb", "#9fbccc", "bands", "#8fb0c8", "Tiny and see-through, and it smells faintly of cucumber."),
        ["crystal_pike"] = new("long", "#9fd8e6", "#e8f8fb", "#6fb8cc", "specks", "#ffffff", "A pike so clear you can watch its heart beating."),
        ["polar_cod"] = new("heavy", "#7f8a7a", "#dfe3d8", "#5a6458", "specks", "#4a5248", "A chunky cod that keeps from freezing with antifreeze in its blood."),
        ["snow_crab"] = new("crab", "#e88a5a", "#f8c8a8", "#b85a3a", null, null, "Long-legged and sweet, it picks its way across the icy sea floor off the glacier."),
        ["aurora_trout"] = new("fish", "#5fb8a8", "#e8f2e8", "#4a8a8a", "bands", "#c98be8", "Its sides shimmer green and violet, like the northern lights."),
        ["king_crab"] = new("crab", "#c8402a", "#e8806a", "#8a2a1a", "specks", "#f2a080", "Spiky, red and enormous: a feast for the whole family."),
        ["blind_cavefish"] = new("fish", "#e8c8c8", "#f8e8e8", "#d8b0b0", null, "blind", "It has no working eyes, and gets along fine without them."),
        ["glow_shrimp"] = new("shrimp", "#7fe8ff", "#bff4ff", "#3fb8d8", "glow", "#ffffff", "It glows a soft blue. Deep-water fish can't resist one."),
        ["ghost_eel"] = new("eel", "#d8e8f0", "#ffffff", "#b0c8d8", "glow", "#bff4ff", "Nearly see-through, it drifts like smoke along the bottom of the pool."),
        ["abyssal_lanternfish"] = new("fish", "#2a3050", "#4a5070", "#1a2038", "glow", "#ffe28a", "Rows of tiny lights run down its sides."),
        ["pale_cave_shark"] = new("shark", "#c8ccd0", "#eef0f2", "#9aa0a8", null, null, "Ghost-white and slow. It has never once seen the sun."),
        ["ancient_coelacanth"] = new("heavy", "#2f5384", "#4572ad", "#22406b", "specks", "#ebf2ff", "A living fossil, older than the islands themselves."),
        // Sunscald
        ["oasis_tilapia"] = new("fish", "#8a9a7a", "#d8d8b8", "#6a7a5a", "bars", "#6a7a5a", "Hardy and plentiful: the oasis keeps it well fed."),
        ["desert_pupfish"] = new("fish", "#5a7ab8", "#c9d8f2", "#2f4a8a", null, null, "Thumb-sized, and happy in water hot enough to bathe in."),
        ["mirage_koi"] = new("fish", "#f2f2f2", "#ffffff", "#f2f2f2", "spots", "#e8603a", "Orange and white, it seems to shimmer out of sight in the heat."),
        ["sunscale_lungfish"] = new("long", "#e8a040", "#f3c25b", "#b5764a", "spots", "#c85a1e", "It breathes air, and can sleep in the mud for years."),
        ["sun_mackerel"] = new("torpedo", "#3f8a8a", "#f2e8c0", "#2f6a6a", "stripes", "#2a5050", "Wavy-backed and quick, it chases bait fish along the dunes' coast."),
        ["sand_ray"] = new("ray", "#c9a87a", "#e8d8b8", "#8a6a4a", "specks", "#8a6a4a", "It buries itself in the sand with only its eyes showing."),
        ["thunderfin"] = new("torpedo", "#4a5a8a", "#c8d0f0", "#f3e36a", "glow", "#f3e36a", "It only bites in storms, and its fins spark like lightning."),
        ["ghost_crab"] = new("crab", "#e8dcc0", "#f8f0e0", "#b8a888", null, null, "Pale as the sand, it's gone the moment you blink."),
        // Mirewood
        ["mudskipper"] = new("fish", "#7a8a4a", "#c9c08f", "#55602f", "specks", "#3a4020", "It hops across the mud on its fins, gulping air."),
        ["swamp_catfish"] = new("cat", "#4f5a3a", "#9a9a72", "#363e28", "spots", "#2a301e", "A big whiskered catfish that rumbles when you lift it."),
        ["emerald_arowana"] = new("long", "#4fae7a", "#c9e8b0", "#2f8a5a", "specks", "#bff2c8", "Scales like green coins; it leaps out of the water after insects."),
        ["mire_leviathan"] = new("long", "#3f6a3a", "#a3352a", "#7a2a1e", "bands", "#2e4f30", "A giant arapaima armoured in scales like old green coins."),
        ["parrotfish"] = new("fish", "#4fb8a8", "#9fe0c8", "#e88ab8", "bars", "#e88ab8", "It chews coral with its beak and turns it into sand."),
        ["jungle_piranha"] = new("deep", "#7a8a8a", "#e85a3a", "#5a6a6a", "specks", "#b8c8c8", "A red-bellied piranha. Count your fingers after unhooking it."),
        ["crayfish"] = new("lobster", "#a8402a", "#c86a50", "#7a2a1a", null, null, "A tiny freshwater lobster that backs away from you, claws up."),
        // Starfall
        ["clownfish"] = new("fish", "#f08a3a", "#f8b070", "#2a2a2a", "bars", "#ffffff", "It lives safe among the anemone's stinging arms."),
        ["moorish_idol"] = new("deep", "#f2e8c0", "#ffffff", "#f3c25b", "bars", "#1a1a1a", "Bold black and gold bars, and a long trailing fin."),
        ["pearl_angelfish"] = new("deep", "#e6e6f0", "#ffffff", "#c8d8f0", "specks", "#ffffff", "Its scales gleam like a string of pearls."),
        ["silver_moonfish"] = new("deep", "#c8d0e0", "#f2f4f8", "#9aa8c8", "glow", "#ffffff", "A silver disc that rises to the surface under the full moon."),
        ["bluefin_tuna"] = new("torpedo", "#1f3a6a", "#dfe7ee", "#2f4a7a", null, null, "A giant of the open ocean, built like a torpedo."),
        ["sailfish"] = new("billed", "#2f5a8a", "#e6edf3", "#3f7ab8", "bars", "#8fc0f0", "Called the fastest fish in the sea; it raises its sail to herd its prey."),
        ["golden_marlin"] = new("billed", "#d9a83a", "#f8e8b0", "#b5852a", "bars", "#f3d070", "A marlin like hammered gold. Few people have ever seen one."),
        ["giant_squid"] = new("squid", "#b5423a", "#d8706a", "#8a2a24", "specks", "#e88a80", "Eyes the size of plates, and feeding tentacles longer than your boat."),
        ["starfall_ray"] = new("ray", "#2a4a8a", "#4a6aaa", "#101c3a", "glow", "#fff6d0", "A manta whose back glitters as if the night sky fell into the sea."),
        ["seafoam_goby"] = new("fish", "#bfe8e0", "#ffffff", "#8fd0c8", "specks", "#ffffff", "A pale goby that hides in the Starwell's foam."),
        ["blue_hole_grouper"] = new("heavy", "#2f5a8a", "#8fb0d0", "#1f3f6a", "spots", "#9fc8f0", "A huge blue grouper that has lived in the Starwell for a hundred years."),
        ["moonglass_fish"] = new("deep", "#bfe8ff", "#ffffff", "#9fd8f8", "glow", "#ffffff", "Clear as glass: you could read through it by moonlight."),
        ["spiny_lobster"] = new("lobster", "#d86a3a", "#f09a6a", "#a04a2a", "specks", "#f8c8a0", "No big claws, but a coat of spines and very long feelers."),
        // The open sea
        ["flying_fish"] = new("flying", "#3f6aa8", "#e6edf3", "#8fb8e8", null, null, "It glides over the waves on wing-like fins to escape the tuna."),
        ["mahi_mahi"] = new("torpedo", "#4fb86a", "#f2e04a", "#3f8ad8", "specks", "#3f8ad8", "Gold, green and blue, though its colours fade soon after it's caught."),
        ["swordfish"] = new("billed", "#3a3a4a", "#c8c8d0", "#2a2a36", null, null, "It slashes through schools of fish with a flat bill like a sword."),
        ["ocean_sunfish"] = new("mola", "#9aa8b0", "#e0e6ea", "#7a8890", "specks", "#c8d0d6", "A giant swimming head that lies flat on the surface, basking in the sun."),
        ["ironbill"] = new("billed", "#16233c", "#a9bccf", "#22365a", "bars", "#8cc8ff", "A black marlin as long as your boat, its bill notched from fights with sharks."),
        ["alimasag_crab"] = new("crab", "#4a7ab8", "#8fb0e0", "#2f5a8a", "specks", "#bfd8f8", "A blue swimming crab, paddling along with its flat back legs."),
        // Habagat
        ["danggit"] = new("fish", "#8a8a5a", "#d8d2a8", "#6a6a3e", "spots", "#5a5a32", "A mottled rabbitfish that grazes the seagrass. Split, salted and sun-dried, it's the islands' favourite breakfast."),
        ["sapsap"] = new("deep", "#c8d4dc", "#f2f6f8", "#9fb0bc", null, null, "A tiny silver ponyfish, all shine and no weight. Slippery as soap."),
        ["pagi"] = new("ray", "#c9a46a", "#e8d4b0", "#8a6a3a", "spots", "#3f9ae8", "A stingray dotted with bright blue spots. Mind the tail: it stings."),
        ["labahita"] = new("deep", "#6a6a72", "#a8a8b0", "#3f3f48", "bands", "#e8e2c8", "A surgeonfish with a sharp blade by its tail, grazing the reef between the islets."),
        ["bisugo"] = new("fish", "#e8a0a8", "#f8e0e2", "#d87880", "bands", "#f2d04a", "A pink threadfin bream with yellow stripes and a long thread trailing from its tail."),
        ["pugita"] = new("squid", "#b5603a", "#d88a60", "#8a3e22", "specks", "#f2b890", "An octopus from the reef. It changes colour while you watch, and tries to climb out of the bucket."),
        ["tamban"] = new("torpedo", "#3f7a8a", "#e8f0f2", "#2f5a68", null, null, "A little sardine that swims in shoals of thousands. Every big fish in the strait hunts it."),
        ["pusit"] = new("squid", "#d8b8d8", "#f2e0f2", "#a87aa8", "glow", "#fff0c0", "A bigfin reef squid, see-through and flickering. It rises to the lighthouse's lamp at night."),
        ["buan_buan"] = new("fish", "#b8c4cc", "#eef2f4", "#8a98a2", "specks", "#ffffff", "A tarpon with scales like silver coins. It leaps clear of the water and shakes its head."),
        ["haring_buan"] = new("fish", "#d8e2ec", "#ffffff", "#a8b8c8", "glow", "#fff6d0", "The king of the tarpon: every scale a little piece of moonlight."),
        ["alumahan"] = new("torpedo", "#3f8a8a", "#e8eef0", "#2f6a6a", "spots", "#1f3a4a", "An Indian mackerel, striped with dark spots along its green back. Best grilled whole."),
        ["matang_baka"] = new("torpedo", "#6a8aa8", "#e6edf3", "#d8c04a", null, null, "Its name means \"cow's eye\": a scad with enormous eyes for hunting in the dark."),
        ["talang_talang"] = new("fish", "#b8c8d0", "#eef3f5", "#e3c04a", "spots", "#3a4a5a", "A queenfish: silver, spotted along the flank, and a great leaper."),
        ["dalagang_bukid"] = new("torpedo", "#3f74b8", "#f0a0a8", "#f2d04a", "bands", "#f2d04a", "A fusilier, blue above and rosy below, with a bright yellow tail. Its name means \"country maiden\". Here it schools in the amihan."),
        ["salay_salay"] = new("torpedo", "#9fb4c0", "#eef3f5", "#c8b04a", "bands", "#f2d04a", "A small silver scad with a bright yellow stripe down each side. Here it runs with the habagat."),
        ["alimango_crab"] = new("crab", "#4f6a3a", "#8aa070", "#33461f", null, null, "A heavy mud crab with big claws. Keep your fingers clear."),
        ["curacha_crab"] = new("crab", "#e0603a", "#f2a080", "#a83a1e", "specks", "#ffd0b0", "A spanner crab, bright red and shaped like a little shield."),
        ["sugpo_shrimp"] = new("shrimp", "#4a5a5a", "#a8b8b0", "#2a3434", "specks", "#e8d84a", "A tiger prawn, banded dark and grey, with a fan of a tail.")
    };

    // The fish in the box (x, y, w, h), facing right (flip faces left). Silhouette draws it all in one colour.
    public static void Draw(Pix p, string id, int x, int y, int w, int h, bool flip = false, string silhouette = null)
    {
        if (!Looks.TryGetValue(id, out var L)) return;
        void Px(float dx, float dy, string c)
        {
            int ix = (int)MathF.Round(dx), iy = (int)MathF.Round(dy);
            if (ix < 0 || iy < 0 || ix >= w || iy >= h) return;
            p.Rect(x + (flip ? w - 1 - ix : ix), y + iy, 1, 1, silhouette ?? c);
        }
        if (L.Shape is "crab" or "lobster" or "shrimp" or "squid" or "ray") { Template(L, w, h, Px); return; }
        Body(L, id, w, h, Px);
    }

    static void Body(Look L, string id, int w, int h, Action<float, float, string> Px)
    {
        string shape = L.Shape, top = Shade(L.Body, 0.8f), pat = L.Mark is { Length: > 0 } m && m[0] == '#' ? m : null;
        bool big = w >= 20;
        float billL = shape == "billed" ? w * 0.2f : 0;
        float tailL = shape switch { "mola" => w * 0.07f, "eel" => 0, "shark" => w * 0.25f, "long" or "cat" or "heavy" => w * 0.18f, _ => w * 0.22f };
        float x0 = tailL, x1 = w - 1 - billL, cy = (h - 1) / 2f;
        float maxH = h * shape switch
        {
            "deep" => 0.31f, "mola" => 0.29f, "heavy" => 0.33f, "fish" => 0.27f, "cat" => 0.26f, "torpedo" => 0.21f, "billed" => 0.19f,
            "shark" => 0.2f, "flying" => 0.17f, "long" => 0.15f, "eel" => 0.12f, _ => 0.27f
        };
        float ped = shape switch { "mola" => 0.75f, "eel" => 0.2f, "deep" => 0.25f, "heavy" or "cat" => 0.4f, _ => 0.3f };
        float Half(float u)
        {
            u = Math.Clamp(u, 0, 1);
            if (shape == "eel") return maxH * (0.25f + 0.75f * MathF.Sin(MathF.PI * (0.1f + 0.8f * u)));
            if (shape == "mola") return maxH * MathF.Max(ped, MathF.Sqrt(MathF.Max(0, 1 - (2 * u - 1) * (2 * u - 1))));
            float k = shape is "heavy" or "cat" ? MathF.Pow(u, 0.8f) : u;
            return maxH * MathF.Max(ped, MathF.Pow(MathF.Sin(MathF.PI * (0.06f + 0.86f * k)), shape is "torpedo" or "billed" or "shark" ? 0.9f : 0.6f));
        }
        float Mid(float u) => shape == "eel" ? cy + MathF.Sin(u * MathF.PI * 2.2f) * h * 0.1f : cy;

        // Fins behind the body: the tail, the dorsal and the anal fin.
        if (tailL > 0)
        {
            float h0 = Half(0), spread = maxH * (shape is "torpedo" or "billed" or "flying" ? 1.5f : shape == "shark" ? 1.3f : shape == "mola" ? 0.3f : 1.05f);
            bool forked = shape is "torpedo" or "billed" or "flying" or "fish" or "shark";
            for (float tx = 0; tx < tailL; tx++)
            {
                float v = 1 - tx / tailL;
                float outerTop = h0 * (1 - v) + v * spread * (shape == "shark" ? 1.3f : 1), outerBot = h0 * (1 - v) + v * spread * (shape == "shark" ? 0.6f : 1);
                float inner = forked ? v * spread * (shape == "fish" ? 0.35f : 0.6f) : 0;
                for (float dy = -outerTop; dy <= outerBot; dy++)
                    if (MathF.Abs(dy) >= inner) Px(tx, Mid(0) + dy, L.Fin);
            }
        }
        float dorsal = shape switch { "billed" => h * 0.3f, "deep" => h * 0.16f, "mola" => h * 0.26f, "shark" => h * 0.24f, "long" or "eel" => h * 0.05f, _ => h * 0.1f };
        (float a, float b) dRange = shape switch { "billed" => (0.15f, 0.7f), "mola" => (0.05f, 0.25f), "shark" => (0.4f, 0.62f), "long" => (0.15f, 0.45f), "eel" => (0, 0.9f), _ => (0.32f, 0.68f) };
        for (float bx = x0 + dRange.a * (x1 - x0); bx <= x0 + dRange.b * (x1 - x0); bx++)
        {
            float u = (bx - x0) / (x1 - x0), k = (u - dRange.a) / (dRange.b - dRange.a);
            float fh = dorsal * (shape is "billed" or "shark" ? MathF.Min(1, k * 2.2f) * (1 - k * 0.6f) : MathF.Sin(MathF.PI * k));
            for (float dy = 0; dy < fh; dy++) Px(bx, Mid(u) - Half(u) - 1 - dy, L.Fin);
            if (shape is "deep" or "mola")
                for (float dy = 0; dy < fh * 0.85f; dy++) Px(bx, Mid(u) + Half(u) + 1 + dy, L.Fin);
        }

        // The body, darker along the back and lighter underneath, with its pattern.
        int seed = id.Length * 31 + id[0];
        for (float bx = x0; bx <= x1; bx++)
        {
            float u = (bx - x0) / MathF.Max(1, x1 - x0), half = Half(u), mid = Mid(u);
            for (float dy = -half; dy <= half; dy++)
            {
                float yy = mid + dy;
                string c = dy > half * 0.25f ? L.Belly : dy < -half + 1 ? top : L.Body;
                if (pat != null && u > 0.08f && u < 0.92f)
                {
                    int ix = (int)bx, iy = (int)MathF.Round(yy);
                    bool on = L.Pattern switch
                    {
                        "stripes" => ix % Math.Max(2, w / 9) == 0 && dy < half * 0.3f,
                        "bars" => (int)((bx - x0) / MathF.Max(2, w / 8f)) % 2 == 1 && u > 0.15f && u < 0.85f && dy < half * 0.5f,
                        "bands" => MathF.Abs(dy + half * 0.15f) < MathF.Max(0.6f, h * 0.035f),
                        "spots" => Pix.Hash(ix, iy, seed) < 0.16 && MathF.Abs(dy) < half - 1,
                        "specks" => Pix.Hash(ix, iy, seed) < 0.09 && MathF.Abs(dy) < half - 0.5f,
                        "glow" => (ix % Math.Max(3, w / 6) == 0) && MathF.Abs(dy) < 1,
                        _ => false
                    };
                    if (on) c = pat;
                }
                Px(bx, yy, c);
            }
        }
        // A small fin behind the gills (big wings on a flying fish).
        float gx = x0 + (x1 - x0) * 0.72f;
        if (shape == "flying")
            for (float k = 0; k < (x1 - x0) * 0.55f; k++) { Px(gx - k, Mid(0.7f) - k * 0.35f, L.Fin); Px(gx - k, Mid(0.7f) - k * 0.35f + 1, L.Fin); }
        else if (big)
            for (float k = 0; k < w * 0.08f; k++) Px(gx - k, cy + Half(0.72f) * 0.2f + k * 0.5f, L.Fin);
        // The bill, whiskers, mouth, gill line and eye.
        if (shape == "billed")
            for (float bx = x1; bx < w; bx++) { Px(bx, cy - Half(1) * 0.3f, Shade(L.Body, 0.7f)); if (big && bx < x1 + billL * 0.4f) Px(bx, cy - Half(1) * 0.3f + 1, Shade(L.Body, 0.7f)); }
        if (shape == "cat")
            for (float k = 1; k <= Math.Max(2, w * 0.12f); k++) { Px(x1 - k * 0.6f, cy + Half(1) * 0.4f + k * 0.8f, L.Fin); if (big) Px(x1 - k * 0.3f, cy + Half(1) * 0.2f + k, L.Fin); }
        if (big)
        {
            Px(x1, cy + Half(1) * 0.2f, Shade(L.Body, 0.6f));
            float gl = x1 - (x1 - x0) * 0.22f;
            for (float dy = -Half(0.78f) * 0.6f; dy <= Half(0.78f) * 0.5f; dy++) Px(gl, cy + dy, Shade(L.Body, 0.75f));
        }
        if (L.Mark == "blind") return;
        float ex = x1 - MathF.Max(1, (x1 - x0) * 0.1f), ey = cy - Half(0.9f) * 0.35f;
        if (big)
        {
            Px(ex, ey, "#10243a"); Px(ex - 1, ey, "#10243a"); Px(ex, ey + 1, "#10243a"); Px(ex - 1, ey + 1, "#10243a");
            Px(ex, ey, "#ffffff");
        }
        else Px(ex, ey, "#10243a");
    }

    // The 12 px shapes for things that aren't fish-shaped, scaled to the box.
    static void Template(Look L, int w, int h, Action<float, float, string> Px)
    {
        var g = new Pix(12, 12);
        string body = L.Body, dark = L.Fin, eye = "#10243a";
        switch (L.Shape)
        {
            case "crab":
                g.Rect(3, 5, 6, 4, body); g.Rect(4, 4, 4, 1, body); g.Rect(1, 3, 2, 2, body); g.Rect(9, 3, 2, 2, body);
                g.Rect(2, 5, 1, 1, body); g.Rect(9, 5, 1, 1, body); g.Rect(2, 9, 1, 2, dark); g.Rect(9, 9, 1, 2, dark); g.Rect(4, 9, 1, 1, dark); g.Rect(7, 9, 1, 1, dark);
                g.Rect(3, 8, 6, 1, L.Belly); g.Rect(4, 5, 1, 1, eye); g.Rect(7, 5, 1, 1, eye);
                break;
            case "lobster":
                g.Rect(3, 4, 3, 6, body); g.Rect(6, 5, 4, 3, body); g.Rect(10, 5, 1, 3, dark); g.Rect(1, 2, 2, 3, body); g.Rect(1, 9, 2, 2, body);
                g.Rect(2, 5, 1, 1, body); g.Rect(2, 8, 1, 1, body); g.Rect(4, 5, 1, 1, eye); g.Line(5, 4, 8, 1, dark); g.Rect(6, 7, 4, 1, L.Belly);
                break;
            case "shrimp":
                g.Rect(3, 4, 5, 3, body); g.Rect(7, 5, 2, 3, body); g.Rect(8, 8, 2, 2, dark); g.Rect(2, 5, 1, 1, eye);
                g.Line(3, 4, 1, 1, dark); g.Rect(4, 7, 1, 2, dark); g.Rect(6, 7, 1, 2, dark); g.Rect(4, 4, 3, 1, L.Belly);
                break;
            case "squid":
                g.Rect(4, 1, 4, 6, body); g.Rect(5, 0, 2, 1, body); g.Rect(3, 3, 1, 3, dark); g.Rect(8, 3, 1, 3, dark);
                for (int i = 0; i < 4; i++) g.Rect(4 + i, 7 + i % 2, 1, 4 - i % 2, i % 2 == 0 ? body : dark);
                g.Rect(5, 2, 1, 2, L.Belly); g.Rect(5, 5, 1, 1, eye); g.Rect(6, 5, 1, 1, eye);
                break;
            case "ray":
                g.Rect(2, 5, 8, 2, body); g.Rect(3, 4, 6, 4, body); g.Rect(4, 3, 4, 6, body); g.Line(6, 9, 6, 11, dark);
                g.Rect(5, 4, 1, 1, eye); g.Rect(6, 4, 1, 1, eye); g.Rect(4, 6, 4, 1, L.Belly);
                break;
        }
        if (L.Mark is { Length: > 0 } m && m[0] == '#')
            for (int yy = 0; yy < 12; yy++)
                for (int xx = 0; xx < 12; xx++)
                    if (g.Buf[yy * 12 + xx].A > 0 && Pix.Hash(xx, yy, 17) < (L.Pattern == "glow" ? 0.12 : 0.18) && g.Buf[yy * 12 + xx].R + g.Buf[yy * 12 + xx].G != 0x10 + 0x24)
                        g.Rect(xx, yy, 1, 1, m);
        // Scale the template into the box, keeping it square and centred.
        int s = Math.Min(w, h);
        float ox = (w - s) / 2f, oy = (h - s) / 2f;
        for (int yy = 0; yy < s; yy++)
            for (int xx = 0; xx < s; xx++)
            {
                var c = g.Buf[(yy * 12 / s) * 12 + xx * 12 / s];
                if (c.A == 0) continue;
                Px(ox + xx, oy + yy, $"#{c.R:x2}{c.G:x2}{c.B:x2}");
            }
    }

    static string Shade(string hex, float k)
    {
        var c = Pal.C(hex);
        return $"#{(int)Math.Min(255, c.R * k):x2}{(int)Math.Min(255, c.G * k):x2}{(int)Math.Min(255, c.B * k):x2}";
    }

    // Big pictures for the Fish log, cached as textures (the silhouette for fish you've only seen).
    static readonly Dictionary<string, Texture2D> big = new();
    public const int BigW = 48, BigH = 28;
    public static Texture2D Picture(string id, bool silhouette)
    {
        string key = id + (silhouette ? ":sil" : "");
        if (big.TryGetValue(key, out var tex)) return tex;
        var p = new Pix(BigW, BigH);
        Draw(p, id, 0, 0, BigW, BigH, silhouette: silhouette ? "#1d3550" : null);
        tex = Gfx.ToTexture(p.Buf, BigW, BigH, TextureFilter.Point);
        big[key] = tex;
        return tex;
    }

    public static void Shutdown()
    {
        foreach (var t in big.Values) Raylib.UnloadTexture(t);
        big.Clear();
    }
}
