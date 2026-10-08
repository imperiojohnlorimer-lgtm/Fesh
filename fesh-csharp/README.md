# Fesh

A fishing mystery game on Saltmere Island, written in C# with [Raylib-cs](https://github.com/raylib-cs/raylib-cs).

## Play it
- **Install:** double-click `dist\Fesh-Setup.msi`. It installs for your Windows user only (no admin prompt),
  adds Fesh to the Start menu and desktop, and can be removed from Settings > Apps.
- **No install:** run `dist\Fesh.exe` directly. It is self-contained, so it does not need .NET installed.

There are three save slots (Load game on the title screen). Each game saves itself every few seconds to
`%AppData%\Fesh\save1.json` to `save3.json`; settings and key bindings live in `settings.json` next to them.
A `save.json` from an older version moves into the first free slot the first time you start this one.

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
These are the defaults. Every one of them can be changed in the menu (Esc, then Controls); Esc, Enter and 1–9 stay fixed.

| Keys | Action |
|------|--------|
| WASD / arrow keys | Walk, or steer your boat while aboard |
| E or Space | Talk, fish (from the boat too, anywhere over deep water), chop, mine, pet, use, place |
| Hold E (or the mouse) at a spot | Power up a cast; let go to throw |
| F | Cook, throw chum, take the helm at Pip's / Starfall's jetty, or (at the helm over deep water) troll a lure |
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
| R | Get on and off your boat: board it from beside it, land it beside a shore. Ride Tidemane, or hop off (once you have it); from anywhere outdoors it whistles Tidemane over |
| F11 | Fullscreen |
| Esc | Back, reel in, stop spearfishing, or the menu (also the button at the top right): save, change your look, quit to the title, Settings and Controls |

## Your fisher
A new game starts with character creation: name, skin, hair, hair colour, hat, shirt and trousers.
Saves from older versions ask you to make your fisher once. You can change your look any time from the menu.
Your fisher walks with a proper stride (arms swinging against the legs), looks about and now and then stretches
when you stand still, leans back to load a cast and into the throw, and pumps the rod as you reel.

## Day, night and the menu
An island clock runs while you play: the HUD shows the day and the time. A whole day and night takes 24 minutes of play
by default (12, 36 or 48 in Settings, or stop the clock so only resting moves it). Night falls at 8 PM and the sun is
up again at 6 AM, with a sunset and a pink dawn on the way; a new day starts at 6 AM. The clock stops while a menu, a
panel or a conversation is open. Resting at a campfire or in a bed still skips ahead: to 9 PM by day, or to 6:30 the
next morning by night (it costs some food and heals you). Hover over the clock for the moon and the forecast.

The islanders keep their own hours. **Pip's stall** is open from 7 AM to 9 PM (night catches keep in your bag until
morning). **Tomas** goes to bed in his hut at 10 PM and is up at 6; you can still go in and wake him, so nothing in
the story ever has to wait. Once a day, Tomas or Pip will tell you **tomorrow's weather**, and it comes true; after
that the menu and the clock's tooltip remember it. Outdoors you hear a dawn chorus that thins to the odd bird by day,
and crickets after dark (not on snowy Frostfang, and hushed by rain).

Nobody stands rooted to the spot any more: **Tomas** potters about his camp, **Pip** shuffles about behind the counter,
and the **Amihan villagers** stroll up and down in front of their houses. Come over and they stop and turn to you.

The game pauses itself when you switch to another window (or unplug the gamepad you were using), even in the middle of
a cast or a fight, and Resume picks up exactly where you were. Turn that off in Settings if you like.

The menu (Esc, Start, or the button at the top right) has three tabs. **Game:** resume, save now, change your look,
quit to the title or quit; plus the day, the moon, today's and tomorrow's forecast and time played. **Settings:** music
and sound volume, music and sound on or off, fullscreen, screen shake, pausing in the background, the length of a day,
and a 12- or 24-hour clock. **Controls:** two keys for every action (click one and press the new key), and the gamepad
layout. The title screen has Continue (your most recent save), Load game, New game and Settings. Load game shows each
slot with Play, Copy (into an empty slot), Rename (give the slot its own name, like "Robin, before the storm") and
Delete.

### Gamepad
Plug in any gamepad Raylib knows. The layout is fixed:

| Button | Action |
|--------|--------|
| Left stick / D-pad | Walk, aim the spear, the chest arrows; in menus, move the pointer |
| A | Talk, fish, use; hold to power up a cast and to reel; in menus, click |
| B | Back, reel in, close |
| X | Cook, throw chum; take down while building |
| Y | Bag |
| LB / RB | Tackle box / map; pick a piece while building |
| LT | Build |
| RT | Ride your mount, or board your boat beside you |
| Back | Fesh-dex |
| L3 / R3 | Case board / spearfish |
| Start | The menu |

Prompts and tips name the gamepad's buttons while you're using it, and the keyboard's again as soon as you type or
move the mouse. Naming a new fisher or a slot needs the keyboard ("Surprise me" in the creator works without one).

## The islands
The world is four islands joined by old bridges, each with its own climate and its own fish.
The mystery stays on Saltmere; the other three are there to explore and fish.

| Island | Climate | Fishing spots and fish | Odd catch |
|--------|---------|------------------------|-----------|
| Saltmere Island | Temperate | Lagoon: pond perch, mud carp, *moon carp* (rare, full moon), **Old Whiskers** (legend). Rocky shore, old wreck (and the *storm eel*, storms only), deep water. Plus the five strange creatures | Dog, at the lagoon |
| Frostfang Isle (north-east) | Snow | Ice hole: Arctic char, frost smelt, *crystal pike* (rare, night). Glacier shore: polar cod, snow crab, *aurora trout* (rare, full moon). Cave pools: blind cavefish, glow shrimp, *ghost eel* (rare). Ancient pool (cave floor 12): abyssal lanternfish, pale cave shark, **Ancient coelacanth** (legend) | Sheep, at the glacier shore |
| Sunscald Dunes (south-east) | Desert | Oasis: oasis tilapia, desert pupfish, *mirage koi* (rare, day), **Sunscale lungfish** (legend). Mirage coast: sun mackerel, sand ray, *thunderfin* (rare, storms only) | Cat, at the oasis |
| Mirewood (south-west) | Jungle | Mangrove swamp: mudskipper, swamp catfish, *emerald arowana* (rare), **Mire leviathan** (legend). Coral shallows: parrotfish, jungle piranha | Pig, in the swamp |
| Starfall Atoll (far east, boat only) | Tropical | A big ring of sand and palms around a lagoon. Atoll lagoon: clownfish, Moorish idol, *pearl angelfish* (rare), *silver moonfish* (rare, full moon). Deep drop-off: bluefin tuna, sailfish, *golden marlin* (rare, day), *giant squid* (rare, night), **Starfall ray** (legend). And something hidden in the palms (see Tidemane below) | Parrot, in the lagoon |

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
- **The ice hole** freezes over again by each morning. Mash E to chip it open (much faster with a pickaxe). While you wait, jig
  with E each time the ring closes on the bobber; off the beat scares the fish.
- **Spearfishing:** with a fishing spear, press G at the coral shallows or the atoll lagoon. Steer the target onto a
  fish shadow (WASD or the mouse) and press E. Three spears, 25 seconds.
- **Crab pots:** set one in shallow water with B (it's in the outdoor build bar). The next morning face its float and
  press E to haul it up: a crab or lobster for that island, often seaweed, now and then an old boot or a pearl.
- **The open sea:** from your boat (or swimming on Tidemane), press E anywhere over deep water to fish the open sea.
  West of Amihan: flying fish, mahi-mahi, *swordfish* (rare, night, fights back) and *ocean sunfish* (rare, clear
  days). In Amihan's waters: galunggong, tulingan, *pating* (rare, night, fights back) and *malasugi* (rare, day,
  fights back). Out there, **feeding frenzies** come and go: dark, splashing water with birds wheeling over it by day.
  Your cast aims for one in reach, and a bait that lands in it gets a much quicker bite. A named spot in reach of the
  boat (the deep water, Baga reef and so on) can be fished from the boat too.
- **Trolling:** at the helm over deep water, with a spinner or fly lure, press F to let a line out behind the boat and
  keep sailing (slowly). Fish that chase (runners and jumpers) strike the moving lure, much sooner when you steer it
  through a feeding frenzy; then it's a normal bite and fight. E or F winds the line in, and landing or shallow water
  winds it in for you.
- **Chum:** at a fishing spot press F to throw it. For two minutes fish bite twice as fast, with a little more rare luck.
- **Legends:** each island has one legendary fish that bites once, under the right conditions and only on the right
  bait. The Fish log's "Legends & more" page has a hint for each. The open sea has one too: **Ironbill**, a giant black
  marlin that only chases a lure trolled through a feeding frenzy by day, and tows your boat across the sea while you
  fight it.
- **The moon and the weather:** the moon goes through a cycle every eight days (the HUD shows it at night, and the menu
  and the clock's tooltip say when it's next full). Some fish only bite under a full moon, some only in storms.
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
- **Pip** keeps a stall just east of Tomas's camp, open from 7 AM to 9 PM. Pip buys fish and materials (rare fish are worth far more, big fish
  more too) and sells bait (5), chum (8), cork bobbers (20), crab pots (40), spinner lures (45), berry saplings (15),
  sailcloth (120), copper bars (30), iron bars (55) and crystal (90). Your coins show in the HUD.
- **Tomas's requests:** once you've met him, Tomas asks for things (3 pond perch, 6 wood, 2 Arctic char, ...)
  and pays coins, sometimes with bait on top. The first twelve are a set list that sends you around the islands;
  after that he asks for random fish. When you have what he wants, his prompt says so. The bag shows the current request.

## Weather
Each morning rolls a forecast for the day, and the weather follows it as the clock runs: most days are fair (some
with a passing shower), some rain for hours, and from day 3 a storm day builds from rain into a few hours of storm
before it eases off. Rain makes fish bite 25% faster; a storm makes them bite 40% faster and adds a little rare luck,
but closes the bridges (not the jetties) until it passes. You get half an hour's warning before a storm, and if one
catches you halfway across a bridge you can still walk off it. You can always shelter wherever you are, which waits
until the storm is over. The morning message and the clock's tooltip tell you what's coming. On Frostfang the rain
falls as snow.

## The boat
Make a **sailboat** at a workbench from 20 wood, 4 iron bars and sailcloth from Pip. Then go to the end of
**Pip's jetty** (Saltmere's east beach) and press E to sail to Starfall Atoll, and back from the atoll's jetty.
You can't sail in a storm; wait for it to pass.

Press **F at either jetty** to take the helm and explore freely. Steer with **WASD / arrow keys** (or the gamepad
stick). **R** (RT on a gamepad) gets you on and off: come alongside a beach or jetty and press R to land, and press R
beside your moored boat to board it again. E does the same when there's nothing else to do there; over deep water E
fishes the open sea instead (see Fishing), and a fishing spot, a villager or the storm shelter next to the moored boat
keeps E.
**Tab** opens the sea chart, including the boat's location. Boat positions and an ongoing voyage survive saving.
A storm prevents launching, but a boat already at sea can still steer home at reduced speed.
The banca-inspired boat has twin bamboo outriggers, a cream sail and a seated fisher at the helm. Under way the sail
fills, the pennant streams back, spray flies off the bow and a wake spreads behind; stop and the sail flaps loose while
the water laps at the floats; stop to fish (or tie up) and the sail comes down onto its boom. A storm rocks it harder.
If a fish knocks you out at sea west of Amihan, a passing boat tows you home to Pip's jetty, your boat with you.

**Boat upgrades** (workbench, Gear tab): a **big sail** (2 sailcloth, 4 wood, 1 iron bar) stands taller and sails a third
faster. An **echo sounder** (2 copper bar, 1 gold bar, 1 crystal) finds more feeding frenzies, further out: a sonar ping
spreads round the boat, little arrows at the edge of the view point to frenzies out of sight, and the sea chart marks
them as "Feeding fish".

## Amihan Archipelago
New in **1.9.0**: a Philippine-inspired region across the open sea **east of Starfall Atoll**, with its own
**Amihan chart tab**. Navigate around Starfall's coastline and continue east to the western village landing.
There are no bridges or wading routes: use the boat or swim there on the secret mount, Tidemane.

| Island | People and wildlife | Fishing |
|---|---|---|
| Amihan Village | Lira offers a daily meal; Niko requests two bangus. Carabao graze nearby. | Bangus, banak, kitang |
| Luntian Karsts | Maya studies the limestone lagoon; rufous hornbills wander the trees. | Lapu-lapu, maya-maya, talakitok |
| Bakawan Island | Tala watches the mangroves, tarsiers and hornbills. | Hito, dalag, mangrove mudskipper |
| Baga Island | A volcanic island with a northern fishing jetty. | Tanigue, yellowfin tuna, great barracuda |
| The open sea between them | From the boat or Tidemane, over deep water. | Galunggong, tulingan, pating, malasugi |

Crab pots in the region catch **alimasag**. Catches use the existing records, size, selling, cooking and aquarium systems.
The village has raised timber houses and woven hats; the region also has its own music.

**Some fish fight back:** talakitok, dalag, tanigue, barracuda, pating and malasugi (and, west of Amihan, the
swordfish) telegraph an attack with a red warning.
Release the reel key **and mouse button** until it passes to duck. The normal reel timer pauses during the warning,
so dodging does not cost your catch. Holding on takes health damage; shell armour reduces it. A knockout brings you
to Lira in the village with 35 health and retrieves your owned boat to the village landing.

The islands and combat are fictional. Fish naming draws on [BFAR's Philippine catch records](https://www.bfar.da.gov.ph/wp-content/uploads/2021/05/Species_and_volume_landed.pdf);
wildlife references include [DENR's tarsier research](https://fasps.denr.gov.ph/projects/special-projects/) and
[rufous hornbill information](https://ncr.denr.gov.ph/news-events/denr-meo-west-retrieves-luzon-rufous-hornbill-in-makati-city/).

## Tidemane (spoilers)
Starfall Atoll hides a secret. A trail of hoofprints leads from the jetty into a ring of palms on the south-east side,
where a gap opens onto **the Starwell**, a deep blue hole with a coral carving beside it. The Starwell stays off the
map and shows as ??? in the Fish log until you find it. By day it has its own fish (seafoam goby, blue hole grouper,
*moonglass fish*, rare, night). Pip passes on a rumour about the hoofprints once you've been to the atoll.

The carving tells you the rest: at **night**, fish the Starwell with a **coconut** (chop a palm, then pick it as your
bait in the tackle box). Something enormous takes it, and you get a long reel fight that switches between all four
fight styles every few seconds. Land it and **Tidemane**, a hippocamp (front half horse, back half fish, with coral
horns and a mane of sea foam), bursts out onto the sand. Then you fight it in the glade:

- It circles you, then **rears and charges** in a straight line (16 damage). Charging into a palm or the water's
  edge leaves it dazed for longer.
- After a charge or a stomp it's **winded**: blows land two and a half times as hard. Any blow tires it out more with a
  better weapon, and it shrugs off blows for a moment after each one.
- Once it's tired it also **stomps**, sending a shockwave across the sand (12 damage). Get right under its hooves,
  or well away.
- Near the end it **dives** into the pool and bursts out on your side, spitting water bolts (9 each).
- Wear its wild spirit down to nothing and it lies down and lets you near. It's yours.

If you black out you wake on the atoll's jetty and it goes back under; try again with another coconut. Walking far
away from the Starwell ends the fight too.

**Riding:** press R to climb on and R to hop off (on dry land). Tidemane gallops much faster than you walk, and
swims through any water, including the open sea between islands (but not deep water in a storm). From anywhere
outdoors, R whistles it over. Going indoors, into the caves or out by boat leaves it waiting where you got off; the
map shows where.

## Music
Every island has its own looping tune, and so do Frostfang Caverns, the insides of houses and the fight with
Tidemane. Rain adds its own patter. Like the sound effects, the music is composed by code at startup, so there are
no audio files. Turn it on or off from the pause menu.

## Health
The heart bar (top left, under the clock) is your health. Cave monsters, Tidemane and some Amihan fish can hurt you. Health comes
back slowly while your food meter is above 30 and nothing has hit you for a few seconds, food heals half of what it
fills, and resting restores 50. Starving wears health down, but never below 10. If it reaches zero in the caves you
black out and wake up at the cave mouth with 35 health and 10% fewer coins. At the Starwell you wake on the atoll's
jetty with 35 health.

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
| Workbench: Gear | Big sail (2 sailcloth, 4 wood, 1 iron bar), echo sounder (2 copper bar, 1 gold bar, 1 crystal), sunglasses (1 copper bar, 1 crystal), fish finder (2 copper bar, 1 iron bar, 1 crystal), waders (3 slime gel, 2 wool), abyssite charm (2 abyssite, 1 gold bar), headlamp (2 copper bar, 1 crystal), cooler (6 wood, 1 iron bar, 1 wool) |
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
- `src/Weather.cs`: the daily forecast, rain and storms coming and going, closed bridges
- `src/Clock.cs`: the island clock: day and night, dusk and dawn, a new day at 6 AM, regrowing trees
- `src/Tidemane.cs`: the Starwell's secret, the fight with Tidemane, and riding it
- `src/Archipelago.cs`: Amihan's four islands, village NPCs, landmarks and regional scenery
- `src/Boating.cs`: boat steering, boarding, safe landings, persistent moorings, and how the boat moves (sail, pennant, spray, wake)
- `src/OpenSea.cs`: fishing the open sea from the boat or Tidemane, feeding frenzies, trolling, Ironbill's tow and the echo sounder
- `src/Folk.cs`: Tomas, Pip and the villagers strolling about near home
- `src/FishAttacks.cs`: telegraphed fish attacks, dodging and health damage
- `src/Music.cs`: the music, composed by code on a background thread at startup
- `src/UiTrade.cs`: the shop and aquarium panels
- `src/Build.cs`: collision, placing and removing pieces, driftwood and stone spawning
- `src/World.cs`: outdoor map generation (islands, bridges, trees), the camera, and the pixel-art drawing, including snow, fireflies and night lighting
- `src/Look.cs`: appearance options and how people are drawn: the four-frame walk, glances, and the rod and overhead poses
- `src/Animals.cs`: pixel sprites for the animals
- `src/Ui.cs`: HUD, prompts, dialogue box, build bar, end screen, catch cards, Fesh-dex and Fish log, map, case board
- `src/UiMenu.cs`: the title screen, the save slots, and the menu (Game, Settings and Controls tabs)
- `src/Settings.cs`: settings (`settings.json`) and the rebindable controls
- `src/UiPanels.cs`: the bag, crafting stations and the character creator
- `src/Gfx.cs`: UI scaling, fonts, text wrapping, buttons, input
- `src/Art.cs`: a small vector renderer and the smooth creature portraits
- `src/Sfx.cs`: sound effects, synthesized at startup
- `src/State.cs`: the save file and the three save slots
- `src/AutoTest.cs`: debug-only scripted play-through (see below)
- `src/AmihanTests.cs`: navigation, village, wildlife, map and fish-attack checks
- `installer/Fesh.wxs`: the installer definition

## Automated check
Debug builds include a scripted play-through that exercises character creation, building, fishing (power casts,
depth, bait, every fight style, perfect hooks, sizes, records, chests, the ice hole, spearfishing, crab pots, chum,
worms and crickets, the tackle box, the derby, legends, the moon and storms), all five
islands, odd catches, chopping, mining, crafting, cooking, eating, houses, the cave (ore tiers, monsters,
health, fainting, the Ancient Floor and the lift), animals, the shop, requests, bait, weather, planters, the
aquarium, the boat, the Starwell and the whole fight with Tidemane, riding and swimming, music, save slots, settings,
rebinding keys, the clock (dusk, dawn, resting, regrowth), the forecast and tomorrow's, Pip's and Tomas's hours, birds and
crickets, pausing in the background, copying and renaming slots, the gamepad (scripted), and every screen. It refuses to run without
`FESH_SAVE`, because it wipes the slots and settings wherever that points.
It checks every island and fishing spot can be reached on foot, generates 33 random cave floors and checks each
one can be crossed (hole, ore and pool all reachable), saves screenshots, writes the generated map to
`map.txt`, and writes a PASS/FAIL log:
```
$env:FESH_AUTOTEST = "$PWD\test-output"; $env:FESH_SAVE = "$PWD\test-output\save.json"; dotnet run
```
For pixel work, `FESH_SPRITES` set to a `.png` path draws every person pose and the boat in each state to zoomed
sheets next to it (`-a`, `-b` and `-c`), touches no saves, and quits.

Fonts: Pixelify Sans and Special Elite, both under the SIL Open Font License.
