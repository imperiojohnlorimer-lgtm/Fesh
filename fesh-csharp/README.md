# Fesh

A fishing mystery game on Saltmere Island, written in C# with [Raylib-cs](https://github.com/raylib-cs/raylib-cs).

## Play it
- **Install:** double-click `dist\Fesh-Setup.msi`. It installs for your Windows user only (no admin prompt),
  adds Fesh to the Start menu and desktop, and can be removed from Settings > Apps.
- **No install:** run `dist\Fesh.exe` directly. It is self-contained, so it does not need .NET installed.

Progress saves automatically to `%AppData%\Fesh\save.json`.

## Run from source
Needs the .NET 8 SDK.
```
dotnet run
```

## Build the installer
```
.\build-installer.ps1
```
This publishes a single self-contained `Fesh.exe` and wraps it in `dist\Fesh-Setup.msi` using WiX
(installed as a local .NET tool, see `.config\dotnet-tools.json`). To release a new version, bump
`<Version>` in `Fesh.csproj`; installing the newer MSI replaces the old one.

## Controls
| Keys | Action |
|------|--------|
| WASD / arrow keys | Walk |
| E or Space | Talk, fish, chop, mine, pet, use, place |
| Hold E (or the mouse) at a spot | Power up a cast; let go to throw |
| F | Cook at a campfire, or throw chum at a fishing spot |
| G | Spearfish (on a reef, with a spear) |
| T | Tackle box |
| Hold Space (or mouse) | Lift the green bar while reeling; tap it for leaps and pumps |
| Arrow keys / WASD | Haul up a sunken chest |
| I | Bag (eat food here) |
| B | Build outdoors, or furnish your shack |
| 1–9, X | Pick a piece, take-down tool |
| Click a nearby tile | Build there |
| Tab | Map of the islands |
| J / C | Fesh-dex (creatures and Fish log) / case board |
| M | Sound on or off |
| F11 | Fullscreen |
| Esc | Back, reel in, stop spearfishing, or the pause menu (change your look, sound and music here) |

## Your fisher
A new game starts with character creation: name, skin, hair, hair colour, hat, shirt and trousers.
Saves from older versions ask you to make your fisher once. You can change your look any time from the pause menu.

## The islands
The world is four islands joined by old bridges, each with its own climate and its own fish.
The mystery stays on Saltmere; the other three are there to explore and fish.

| Island | Climate | Fishing spots and fish | Odd catch |
|--------|---------|------------------------|-----------|
| Saltmere Island | Temperate | Lagoon: pond perch, mud carp, *moon carp* (rare, full moon), **Old Whiskers** (legend). Rocky shore, old wreck (and the *storm eel*, storms only), deep water. Plus the five strange creatures | Dog, at the lagoon |
| Frostfang Isle (north-east) | Snow | Ice hole: Arctic char, frost smelt, *crystal pike* (rare, night). Glacier shore: polar cod, snow crab, *aurora trout* (rare, full moon). Cave pools: blind cavefish, glow shrimp, *ghost eel* (rare). Ancient pool (cave floor 12): abyssal lanternfish, pale cave shark, **Ancient coelacanth** (legend) | Sheep, at the glacier shore |
| Sunscald Dunes (south-east) | Desert | Oasis: oasis tilapia, desert pupfish, *mirage koi* (rare, day), **Sunscale lungfish** (legend). Mirage coast: sun mackerel, sand ray, *thunderfin* (rare, storms only) | Cat, at the oasis |
| Mirewood (south-west) | Jungle | Mangrove swamp: mudskipper, swamp catfish, *emerald arowana* (rare), **Mire leviathan** (legend). Coral shallows: parrotfish, jungle piranha | Pig, in the swamp |
| Starfall Atoll (east, boat only) | Tropical | Atoll lagoon: clownfish, Moorish idol, *pearl angelfish* (rare), *silver moonfish* (rare, full moon). Deep drop-off: bluefin tuna, sailfish, *golden marlin* (rare, day), *giant squid* (rare, night), **Starfall ray** (legend) | Parrot, in the lagoon |

Each odd catch has a 7% chance per bite at its spot. You get a card for it, then the animal runs off.
Everything you catch is tracked in the Fesh-dex's **Fish log**, island by island, with your heaviest catch of each.

## Fishing
- **Power cast:** hold E at a spot to power up, let go to throw. A short cast lands in **shallow** water, a long one in
  **deep** water, and each fish prefers a depth (the Fish log says which once you've caught it). The ice hole is a hole:
  your line just drops in.
- **The perfect hook:** hook a bite the instant the bobber goes under (two gold marks over your head) and the fight
  starts half won, and the fish comes out a little heavier.
- **Fight styles:** *darters* dart about (hold Space to keep the green bar on them). *Runners* make runs for the
  bottom: let go while they run, or the tension gauge fills and the line snaps. *Jumpers* leap clear of the water:
  press Space as the marker crosses the gold. *Bottom-huggers* sulk deep: tap Space in short pumps, because holding too
  long lets them dig in.
- **Sizes, records and trophies:** every catch is weighed. Big ones (a quarter over the usual size) sell to Pip for 60%
  more; trophies are bigger still. Your heaviest of each kind is your record.
- **Fishing level:** catches earn experience. Each level up to 10 unlocks a perk: a wider reel bar, faster bites, a
  longer perfect-hook window, rare luck, a stronger line, heavier fish, more sunken chests, slower escapes, and finally
  the odd double catch.
- **Sunken chests** (3% of bites): press the arrows shown in order before time runs out. Inside: coins, bars, crystal,
  bait, pearls, and sometimes a golden hook.
- **The ice hole** freezes over every night. Mash E to chip it open (much faster with a pickaxe). While you wait, jig
  with E each time the ring closes on the bobber; off the beat scares the fish.
- **Spearfishing:** with a fishing spear, press G at the coral shallows or the atoll lagoon. Steer the target onto a
  fish shadow (WASD or the mouse) and press E. Three spears, 25 seconds.
- **Crab pots:** set one in shallow water with B (it's in the outdoor build bar). The next morning face its float and
  press E to haul it up: a crab or lobster for that island, often seaweed, now and then an old boot or a pearl.
- **Chum:** at a fishing spot press F to throw it. For two minutes fish bite twice as fast, with a little more rare luck.
- **Legends:** each island has one legendary fish that bites once, under the right conditions and only on the right
  bait. The Fish log's "Legends & more" page has a hint for each.
- **The moon and the weather:** the moon goes through a cycle every eight days (the HUD shows it at night, and the pause
  menu says when it's next full). Some fish only bite under a full moon, some only in storms.
- **Pip's derby:** once a day, from the Derby tab at Pip's stall. Catch the heaviest fish you can in three minutes to beat
  three rival anglers. First place wins 120 coins and tackle; the rivals get better each time you win.

### Bait
Pick your bait in the tackle box (T). On "Automatic", glow bait is used first, then plain bait. Fish that like a bait
are three times as likely to bite on it.

| Bait | Where it comes from | Liked by |
|------|---------------------|----------|
| Bait / glow bait | Pip / workbench (2 bait + 1 slime gel make 3) | Everything (glow bait: much more rare luck) |
| Worms | Dig up worm mounds in the grass (E) | Freshwater fish (lagoon, oasis, swamp, ice hole) and bottom-huggers |
| Crickets | Catch them in the grass (they hop away, so sneak up) | Jumpers and shallow-water fish |
| Cut bait | Workbench: 1 raw fish makes 4 | Sea fish that run or hug the bottom |
| Glow shrimp | Cave pools | Deep-water fish |
| Slime gel | Cave slimes | Cave fish |
| Berries | Berry bushes | Carp and other plant-eaters |
| Spinner lure / fly lure | Workbench (never used up) | Runners / jumpers |

### Tackle and accessories
Your best tackle is equipped automatically until you choose in the tackle box (sinkers are only ever chosen).

| Tackle | Effect |
|--------|--------|
| Reels (copper, iron, gold) | The catch meter fills 15% / 30% / 45% faster |
| Lines (silk, crystal) | Take 40% / 90% more tension before a runner snaps them |
| Hooks (barbed, big-game, golden) | Fish come out 10% / 25% / 20% heavier (golden also +1 rare luck) |
| Bobbers (cork, glow) | Bites last 0.3 / 0.5 s longer, with a longer perfect-hook moment |
| Sinkers (stone, iron) | Your cast sinks one / two depths deeper |

| Accessory (worn automatically) | Effect |
|--------------------------------|--------|
| Polarized sunglasses | See fish shadows at fishing spots; cast onto one for a quick bite (the big one gives a heavier fish) |
| Fish finder | Shows what's biting at a spot right now, with the odds |
| Waders | Walk into shallow water; casting from the water reaches one depth deeper |
| Abyssite charm | Rare luck +2 |
| Headlamp | A much bigger circle of light at night and in the caves |
| Cooler | Pip pays 25% more for fish |

### Aquarium collections
Put all four fish of a set on show (in any of your aquariums) for a bonus while they're displayed:
Saltmere shore (bites 15% faster on Saltmere), Frozen north (rare luck on Frostfang), Desert springs (Pip pays 10% more
for fish), Jungle waters (15% more XP), Coral reef (fish 10% heavier), Deep dark (reel bar +3). The aquarium panel shows
each set's progress.

## Pip, coins and Tomas's requests
- **Pip** keeps a stall just east of Tomas's camp. Pip buys fish and materials (rare fish are worth far more, big fish
  more too) and sells bait (5), chum (8), cork bobbers (20), crab pots (40), spinner lures (45), berry saplings (15),
  sailcloth (120), copper bars (30), iron bars (55) and crystal (90). Your coins show in the HUD.
- **Tomas's requests:** once you've met him, Tomas asks for things (3 pond perch, 6 wood, 2 Arctic char, ...)
  and pays coins, sometimes with bait on top. The first twelve are a set list that sends you around the islands;
  after that he asks for random fish. When you have what he wants, his prompt says so. The bag shows the current request.

## Weather
Each morning rolls the day's weather: clear, rain, or (from day 3) a storm. Rain makes fish bite 25% faster;
a storm makes them bite 40% faster and adds a little rare luck, but closes the bridges (not the jetties)
until the next morning. You can always shelter and wait out a storm wherever you are. On Frostfang the rain
falls as snow.

## The boat
Make a **sailboat** at a workbench from 20 wood, 4 iron bars and sailcloth from Pip. Then go to the end of
**Pip's jetty** (Saltmere's east beach) and press E to sail to Starfall Atoll, and back from the atoll's jetty.
You can't sail in a storm.

## Music
Every island has its own looping tune, and so do Frostfang Caverns and the insides of houses. Rain adds its own
patter. Like the sound effects, the music is composed by code at startup, so there are no audio files.
Turn it on or off from the pause menu.

## Health
The heart bar (top left, under the clock) is your health. Only cave monsters hurt you. Health comes back slowly
while your food meter is above 30 and nothing has hit you for a few seconds, food heals half of what it fills,
and resting restores 50. Starving wears health down, but never below 10. If it reaches zero in the caves you
black out and wake up at the cave mouth with 35 health and 10% fewer coins.

## Food and your bag
Your food meter (next to the heart bar) drops slowly as you play and by 10 each time you rest. Below a quarter you get a
warning; at zero you walk slower and the reel's green bar shrinks. Eat from the bag (I).

| Food | Fills | Where it comes from |
|------|-------|---------------------|
| Any raw fish | 6 | Fishing |
| Berries | 6 | Berry bushes (once a day) |
| Egg | 5 | Chickens on Saltmere |
| Cactus fruit | 8 | Chopping a cactus |
| Coconut | 10 | Chopping a palm |
| Truffle | 12 | Pigs in Mirewood |
| Fried egg | 18 | Cook an egg (campfire or stove) |
| Grilled fish | 25 | Cook any raw fish (campfire or stove) |
| Fish stew | 55 | 2 raw fish and 1 berries (stove) |

## Gathering
- **Trees:** with an axe, face an oak, fir, palm or cactus and press E three times. Trees grow back after 3 days.
- **Boulders:** a pickaxe breaks them for stone (sometimes copper ore). They come back after 2 days.
- **Berry bushes:** pick once a day.
- **Animals** wander their islands. Pet them with E; once a day they give you something:
  chickens an egg, sheep wool, pigs a truffle, and Biscuit the dog a stick. The cats just enjoy it.

Each morning after resting is a new day.

## Frostfang Caverns
The cave on Frostfang's north shore goes down twelve floors. Floors 1 to 11 are generated fresh every time you
climb down to them (60x40 tiles, a different layout, ore, monsters and sometimes an underground pool each visit).
Find the **hole** to go down a floor; the **ladder** you arrived by takes you back to the surface, and monsters
won't follow you onto it. Once you've reached floor 5, 10 or the bottom, the cave mouth offers to take you
straight down to that floor.

| Ore | Floors | Needs |
|-----|--------|-------|
| Copper | 1–6 | Stone pickaxe |
| Iron | 3–10 | Copper pickaxe |
| Gold | 6–12 | Iron pickaxe |
| Crystal | 9–12 | Gold pickaxe |
| Abyssite | 12 only | Crystal pickaxe |

| Monster | From floor | Health | Hits for | Drops |
|---------|-----------|--------|----------|-------|
| Cave slime (hops) | 1 | 5 | 8 | Slime gel |
| Cave bat (flies, darts about) | 2 | 3 | 6 | Bat wing |
| Rock crab (slow, tough) | 5 | 12 | 12 | Crab shell |
| Shade (flies) | 9 | 16 | 16 | Shadow essence |

Face a monster and press E to attack. Your best sword is used automatically (copper 3, iron 5, gold 7, crystal
blade 10 damage); without one you swing your pickaxe (2) or your fists (1). Shell armour cuts damage by a third.

**The Ancient Floor** (floor 12) is always the same: a flooded ruin of pillars and glowing runes around the
Ancient pool, where the legendary **Ancient coelacanth** lives. Shades and rock crabs guard it, and it's the only
place with abyssite, which makes the Ancient rod.

## Crafting
Tomas's hut (walk to his door and press E) has a workbench, a stove and a bed you can use from the start.
Your own shacks start empty: go inside and press B to place a workbench, furnace, stove, bed and decorations.

| Station | Makes |
|---------|-------|
| Workbench: Tools | Stone axe (3 wood, 2 stone), pickaxes: stone (3 wood, 3 stone), copper (2 wood, 2 copper bar), iron (2 wood, 3 iron bar), gold (2 wood, 3 gold bar), crystal (2 gold bar, 3 crystal, 1 shadow essence); sailboat (20 wood, 4 iron bar, 1 sailcloth); fishing spear (2 wood, 1 copper bar); crab pot (4 wood, 1 wool) |
| Workbench: Rods | Copper rod (2 wood, 3 copper bar), iron rod (copper rod, 3 iron bar), crystal rod (iron rod, 1 iron bar, 3 crystal), Ancient rod (crystal rod, 2 gold bar, 3 abyssite) |
| Workbench: Tackle | Reels: copper (1 wood, 2 copper bar), iron (copper reel, 2 iron bar), gold (iron reel, 2 gold bar). Lines: silk (3 wool), crystal (silk line, 1 crystal, 1 slime gel). Hooks: barbed (1 copper bar), big-game (barbed hook, 2 iron bar). Bobbers: cork (2 wood), glow (cork bobber, 2 slime gel). Sinkers: stone (3 stone), iron (stone sinker, 1 iron bar). Lures: spinner (1 copper bar), fly (1 wool, 1 bat wing) |
| Workbench: Gear | Sunglasses (1 copper bar, 1 crystal), fish finder (2 copper bar, 1 iron bar, 1 crystal), waders (3 slime gel, 2 wool), abyssite charm (2 abyssite, 1 gold bar), headlamp (2 copper bar, 1 crystal), cooler (6 wood, 1 iron bar, 1 wool) |
| Workbench: Combat | Swords: copper (1 wood, 2 copper bar), iron (1 wood, 3 iron bar), gold (1 wood, 3 gold bar); crystal blade (2 gold bar, 3 crystal, 2 shadow essence); shell armour (4 crab shell, 2 iron bar) |
| Workbench: Bait | Glow bait ×3 (2 bait, 1 slime gel), cut bait ×4 (1 raw fish), chum ×2 (1 raw fish, 1 berries) |
| Furnace | Copper bar (2 copper ore, 1 wood), iron bar (2 iron ore, 1 wood), gold bar (2 gold ore, 1 wood) |
| Smoking rack | Smoked fish (1 raw fish; fills 30, sells for 20), fish jerky ×2 (3 cut bait) |
| Campfire (F) | Grilled fish, fried egg |
| Cooking stove | Everything the campfire makes, plus fish stew, sushi rolls (1 raw fish, 1 seaweed) and maki platters (3 raw fish, 2 seaweed, 1 berries; fills 80) |

Legendary fish are never used up as a cooking ingredient.

**Rods:** your best rod is used automatically. Each step up widens the green reel bar (+5, +9, +13, +17), makes
bites come faster (15%, 30%, 45%, 55%) and raises your luck with rare fish.

## Building
Outdoors you build on grass, sand, snow, dunes and jungle floor. Taking something down refunds its full cost;
taking down a shack also packs up everything inside it.

| Outdoor piece | Cost | What it does |
|---------------|------|--------------|
| Stone path | 1 stone | Decoration you can walk on |
| Fence | 1 wood | Blocks the way, joins up with neighbors |
| Lantern | 1 wood, 1 stone | Lights up the night |
| Bait box | 2 wood, 1 stone | Fish bite faster when you cast nearby |
| Campfire | 3 wood, 2 stone | Rest here (E) or cook (F) |
| Shack | 8 wood, 4 stone | Two tiles wide; go inside to furnish it |
| Berry bush | 1 berry sapling (from Pip) | Pick berries from it every day |
| Smoking rack | 5 wood, 3 stone | Smoke fish (E) |
| Crab pot | 1 crab pot (workbench or Pip) | Goes in shallow water; haul it up each morning |

| Indoor piece | Cost | What it does |
|--------------|------|--------------|
| Workbench | 6 wood, 2 stone | Tools and rods |
| Furnace | 10 stone, 2 wood | Smelts ore into bars |
| Cooking stove | 6 stone, 2 wood | All cooking |
| Bed | 6 wood, 2 wool | Sleep to pass the time |
| Table, rug, potted plant, lamp | Wood, wool, stone/berries, copper | Decoration (the lamp lights the room at night) |
| Aquarium | 4 wood, 6 stone, 2 copper bar | Holds up to four fish from your bag, swimming about. Taking it down returns them |

## Where things are
- `src/Data.cs`: the five creatures, clues, theories, biomes, fish per spot, odd catches, fishing spots, and outdoor and indoor pieces
- `src/Items.cs`: every item, recipe, rod's stats and island animal, plus the item icons
- `src/Game.cs`: the main loop, input, interactions, story (`TalkTomas`, `AfterCatch`, `Ending`), fishing and the reeling minigame
- `src/Survival.cs`: bag helpers, food, crafting, chopping, mining, berry picking and the wandering animals
- `src/Scenes.cs`: house interiors and moving between scenes
- `src/Cave.cs`: Frostfang Caverns: floor generation, ore, monsters, combat and health
- `src/Fishing.cs`: rod fishing: power casts, depth and bait, the bite and the perfect hook, the four fight styles, sizes, records, the fishing level, chests, chum and the ice hole
- `src/Minigames.cs`: spearfishing, crab pots, worms and crickets, Pip's derby and aquarium collections
- `src/FishingUi.cs`: fishing overlays (cast power, leap timing, chest arrows, derby clock, fish finder, floating text) and the tackle box
- `src/Life.cs`: gulls, butterflies and Pip waving
- `src/Trade.cs`: Pip's shop, Tomas's requests, aquariums, sailing and planted berry bushes
- `src/Weather.cs`: the daily weather, rain, storms and closed bridges
- `src/Music.cs`: the music, composed by code on a background thread at startup
- `src/UiTrade.cs`: the shop and aquarium panels
- `src/Build.cs`: collision, placing and removing pieces, driftwood and stone spawning
- `src/World.cs`: outdoor map generation (islands, bridges, trees), the camera, and the pixel-art drawing, including snow, fireflies and night lighting
- `src/Look.cs`: appearance options and how the player is drawn
- `src/Animals.cs`: pixel sprites for the animals
- `src/Ui.cs`: HUD, prompts, dialogue box, build bar, title and end screens, catch cards, Fesh-dex and Fish log, map, case board, pause menu
- `src/UiPanels.cs`: the bag, crafting stations and the character creator
- `src/Gfx.cs`: UI scaling, fonts, text wrapping, buttons, input
- `src/Art.cs`: a small vector renderer and the smooth creature portraits
- `src/Sfx.cs`: sound effects, synthesized at startup
- `src/State.cs`: the save file
- `src/AutoTest.cs`: debug-only scripted play-through (see below)
- `installer/Fesh.wxs`: the installer definition

## Automated check
Debug builds include a scripted play-through that exercises character creation, building, fishing (power casts,
depth, bait, every fight style, perfect hooks, sizes, records, chests, the ice hole, spearfishing, crab pots, chum,
worms and crickets, the tackle box, the derby, legends, the moon and storms), all five
islands, odd catches, chopping, mining, crafting, cooking, eating, houses, the cave (ore tiers, monsters,
health, fainting, the Ancient Floor and the lift), animals, the shop, requests, bait, weather, planters, the
aquarium, the boat, music and every screen.
It checks every island and fishing spot can be reached on foot, generates 33 random cave floors and checks each
one can be crossed (hole, ore and pool all reachable), saves screenshots, writes the generated map to
`map.txt`, and writes a PASS/FAIL log:
```
$env:FESH_AUTOTEST = "$PWD\test-output"; $env:FESH_SAVE = "$PWD\test-output\save.json"; dotnet run
```

Fonts: Pixelify Sans and Special Elite, both under the SIL Open Font License.
