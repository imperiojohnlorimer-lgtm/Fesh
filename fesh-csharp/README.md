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
| 1–6, mouse wheel / V | Pick what's in your hands from the hotbar / use it (swing, cast, eat) |
| B | Build outdoors, or furnish your shack |
| 1–9, X | Pick a piece, take-down tool |
| Click a nearby tile | Build there |
| Tab | Map of the islands (click the chart to drop a pin) |
| J / C | Fesh-dex (the mystery's creatures and chapters, and the Fish log) / case board |
| Q | Journal: the goals open right now, and the Getting started list |
| M | Sound on or off |
| R | Get on and off your boat: board it from beside it, land it beside a shore. Ride Tidemane, or hop off (once you have it); from anywhere outdoors it whistles Tidemane over |
| F11 | Fullscreen |
| Esc | Back, reel in, stop spearfishing, or the menu (also the button at the top right): save, change your look, quit to the title, Settings and Controls |

## Your fisher
A new game starts with character creation: name, skin, hair, hair colour, hat, shirt and trousers.
Saves from older versions ask you to make your fisher once. You can change your look any time from the menu.
Your fisher walks with a proper stride (arms swinging against the legs), looks about and now and then stretches
when you stand still, leans back to load a cast and into the throw, and pumps the rod as you reel.
Tools are held in your hands and your arms do the work: the axe and pickaxe wind up over your head and come down on
the tile in front (the pick bounces off the rock), a sword slashes, fists punch, the rake draws salt in, and you haul
traps up with both hands. The rod goes back over your shoulder as you load a cast, whips forward as you throw, and
your other hand turns the reel while you reel in.

## The hotbar
Six slots along the bottom of the screen hold what's in your hands: your rod, axe, pickaxe, sword, spear, or food. Pick
one with **1-6**, the mouse wheel or a click (the same key again puts it away); on a gamepad, flick the right stick. A
tool slot always holds your best of that kind, and new tools fill an empty slot; put food or a tool on any slot from
its card in your bag. Whatever you pick is in your hand: tools are carried, and food is held out in front of you.
Use it with **E** when nothing's in front of you, **V**, a click on the ground, or B on a gamepad: tools swing (and
still chop or mine what's there), the rod casts into any water, and food goes up to your mouth for a bite.

## The Saltmere mystery
Five strange creatures, a sunken research ship and Tomas's missing daughter. The **Fesh-dex** (J) shows the creatures
you've caught and, under them, the mystery's **nine chapters**: between the creatures, Tomas helps you settle in.
- After the first creature he asks you to make a **stone axe and a stone pickaxe** at his workbench, from driftwood and
  stones off the beach, and to break up the **rocks fallen over the tide pools** on the rocky shore.
- Later the old wreck's **hatch** is rusted shut. A copper pickaxe would open it, so you build **a shack of your own**
  with **your own workbench and a furnace**, climb down into **Frostfang Caverns** for copper ore, smelt it into bars,
  and make the pickaxe. (Tomas starts you off with five planks and five stones.)
- Then the old dock needs **two iron bars**, from iron ore deeper down the caverns (floor 3 and below).
- Pip only stocks copper and iron bars once you've smelted that kind yourself. Games saved before this change keep
  buying them as before, and any chapter your save had already passed counts as done.

## Finding your way: the guide and the journal
- **The goal card** under your health and food always shows the next step of whatever you're following, with how far
  it is. It works the story out from what you've done, so you can wander off, do things in any order, and it simply
  picks up from there. Story steps that need night or daylight say so ("Wait for night: rest by Tomas's campfire").
- **A gold arrow** floats over the place, or sits at the edge of the view pointing there when it's off screen. Indoors
  it points at the door first, underground at the ladder, and across the sea at the jetty you'd sail from (or your boat)
  with a line saying what to do there.
- **The journal** (Q, a click on the goal card, or Journal in the Esc menu) lists everything open: the story, what
  people have asked you for (Tomas, Niko, Lira, Maya, Tala, Bantay Joy, Tatay Celso, Dado, Manang Rosa's daily order)
  and somewhere new to explore. **Follow** any of them, or leave it on **Automatic** (the story, then a request you can
  hand in, then the rest). When someone asks you for something new, a note says it's in the journal.
- **Getting started** in the journal ticks off the basics as you do them, on three pages (the arrows turn them):
  **First days** (talk, fish, sell, buy bait, eat, cook, rest, open the Fesh-dex and the map), **Tools and a home**
  (pick up driftwood and stones, make a stone axe and pickaxe, chop a tree, break a boulder, build a shack, put a
  workbench and a furnace in it) and **Into the caverns** (climb down, mine copper, smelt a bar, make a copper pickaxe,
  mine iron, smelt an iron bar). Its other tab, **Sea school**, shows your three badges (see below) and opens the tide
  table once Lola Pacing has given it to you.
- Your goal shows on the map too. Don't want it? **Hide the arrow** in the journal, or Settings > Goal and arrow.

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
a 12- or 24-hour clock, and the **font**: Pixel, or **Clear** (Atkinson Hyperlegible, made by the Braille Institute for
readers with low vision) for anyone who finds the pixel and typewriter letters hard to read. **Controls:** two keys for every action (click one and press the new key), and the gamepad
layout. The title screen has Continue (your most recent save), Load game, New game and Settings. Load game shows each
slot with Play, Copy (into an empty slot), Rename (give the slot its own name, like "Robin, before the storm") and
Delete. Behind the title the view tours the islands, from Saltmere and Starfall Atoll to Amihan and Habagat, a few
seconds over each.

The **Case board** has a tab for each of the four stories from the start. One you haven't begun yet is greyed with a
padlock; clicking it says where it starts, without giving anything away.

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

### The Fish log
- **Every fish has its own picture**, in the log, in your bag and held up over your head when you land it.
- **Click a fish** for its card: a big picture, a line about it, where and when it bites, how it fights, the baits it
  likes, your heaviest against trophy size, and what Pip pays. Back (or Esc) returns to the page.
- **The real fish:** under the picture, every fish you've caught (or seen) has a note about the real animal: its
  scientific name and a few true things about it (where it lives, how it feeds, how it's farmed or fished, and whether
  it's in trouble). Fish made up for the game say which real animal they're based on.
- A **green dot** marks fish whose time, weather and moon are right just now; the **Biting now** filter shows only
  those. The other filters are **Not caught** and **Rare**.
- A fish that **gets away mid-fight** counts as seen: its silhouette and name appear before you've caught one.
- Each island's tab shows how much of its page you've caught. Catch them all and you get a **star** on the tab and
  **200 coins** from Pip for the collection.

### The map
- The map opens on the chart you're on (Saltmere, which also shows the Habagat islands along its bottom, or Amihan). On the other chart, an arrow at the edge shows which way
  you are.
- Islands are only **charted** once you set foot on them; until then they're a rough outline, with no spots marked.
- Labels move apart instead of piling up, with a short line back to their dot when they have to.
- **Hover a dot** to see what's there: whether a spot is open (and why not), how many of its fish you've caught, and
  what's biting there now; Pip's hours; whether your crab pots are ready; where Tidemane and your boat are waiting.
- **Click the chart to drop a pin.** An arrow at the edge of the view points to it with the distance, and it goes
  when you get there (or click it again). The header shows the weather, and the derby clock when one's on.

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
- **Spearfishing:** with a fishing spear, press G at the coral shallows, the atoll lagoon or Daang Pulo's islet reef. Steer the target onto a
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
| Tamban (a live sardine) | Caught at the Parola pier | Sea fish that run or leap |

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
for fish), Jungle waters (15% more XP), Coral reef (fish 10% heavier), Deep dark (reel bar +3), Salt and islets (fish dry
twice as fast on drying racks). The aquarium panel shows
each set's progress. Point at any of your fish to see its name; with more than 21 kinds in your bag, the arrows by
"Your fish" turn the page.

## Pip, coins and Tomas's requests
- **Pip** keeps a stall just east of Tomas's camp, open from 7 AM to 9 PM. Pip buys fish and materials (rare fish are worth far more, big fish
  more too) and sells bait (5), chum (8), cork bobbers (20), crab pots (40), spinner lures (45), berry saplings (15),
  sailcloth (120), copper bars (30, once you've smelted one), iron bars (55, likewise), crystal (90) and suka, vinegar
  for paksiw (4). Your coins show in the HUD.
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

**Seasons:** the islands take turns between the two monsoons, five days each, starting with the amihan. The **amihan**
(the cool northeast wind) is mostly dry, with few storms. The **habagat** (the wet southwest monsoon) brings rainy days and
more and longer storms; in the habagat a storm is a *bagyo*. Tomas and Pip warn you the day before the wind turns, the
morning toast says so, and the clock's tooltip and the menu show the season and its day. Forecasts say whether a storm
will be short or long. Under sail the wind helps you or holds you back by up to 15%: sailing southwest is quickest in
the amihan, northeast in the habagat. (Trolling isn't affected.) Five-day seasons are the game's own short calendar; in
the Philippines each monsoon lasts months.

## The boat
Make a **sailboat** at a workbench from 20 wood, 4 iron bars and sailcloth from Pip. Then go to the end of
**Pip's jetty** (Saltmere's east beach) and press E to sail to Starfall Atoll, and back from the atoll's jetty.
Once you've found the Habagat islands, E at a jetty asks where to sail: Saltmere, Starfall Atoll or Asinan.
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
Heading up shows the boat from behind; heading down shows its bow, with the fisher seated at the stern.
The outriggers, wake, spray and trolling rod follow its heading, which stays put when you stop to fish.
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
| Amihan Village | Lira offers a daily meal and needs a pot for the village supper; Niko requests two bangus, then three asohos. Carabao graze nearby. | Bangus, banak, kitang |
| Luntian Karsts | Maya farms guso (seaweed) in the limestone lagoon; rufous hornbills wander the trees. | Lapu-lapu, maya-maya, talakitok, asohos (silver sillago) |
| Bakawan Island | Tala watches the mangroves, tarsiers and hornbills; after dark, fireflies light its west shore. | Hito, dalag, mangrove mudskipper |
| Baga Island | A volcanic island with a northern fishing jetty. | Tanigue, yellowfin tuna, great barracuda |
| The open sea between them | From the boat or Tidemane, over deep water. | Galunggong, tulingan, pating, malasugi |

**Rondalla evenings:** Lira is one pot short for the village supper. Cook her a **ginataang isda** or a **sinigang na
isda** at a stove (60 coins). From then on, on any evening from 6 to 10 PM without a storm, three villagers sit out in
the square with a bandurria, a guitar and a bass, and the village's music turns to plucked strings.

**The Bangus Festival:** on the last day of every amihan (days 5, 15, 25...) the village celebrates, in the spirit of
Dagupan's Bangus Festival. Bunting hangs across the square, a long street grill smokes beside the path from the
landing (Lira's meal that day is a grilled bangus), and Niko judges a **bangus derby**: land the heaviest bangus you
can that day and show it to him (150 coins from 2.6 kg, 80 from 2 kg, 40 for taking part, once a festival). Dado's
regatta in Habagat pays double prizes on festival day. The morning message reminds you.

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

## The Habagat islands
New in **1.13.0**: a band of Philippine-inspired islands along the **bottom of the Saltmere chart**, south of Mirewood
and Sunscald, where the southwest wind (the *habagat*) blows. A deep channel keeps waders out: take the helm at the
atoll's jetty and sail south (a boat launched at Pip's jetty can't get past the bridges), or swim there on Tidemane.
Pip mentions them once you have a boat and have been to the atoll. After your first visit, any sailing jetty can take
you straight to the Asinan landing.

| Island | People | What's there | Fishing |
|---|---|---|---|
| Asinan (west) | Manang Rosa, the salt maker | Salt beds, calamansi bushes, goats | Asinan flats: danggit, sapsap, *pagi* (stingray, rare, fights back) |
| Daang Pulo (middle) | Dado (regatta), Lola Pacing (sungka) | A home islet and eleven limestone islets | Islet reef: labahita, bisugo, *pugita* (octopus, rare, night); spearfishing too |
| Parola (east) | Tatay Celso, the lighthouse keeper | The Parola lighthouse, dark until you help mend it | Parola pier: tamban, pusit (night, once the lamp is lit), *buan-buan* (tarpon, rare, night, once lit), **Haring Buan-buan** (legend) |
| The Habagat Sea | | From the boat or Tidemane, over deep water | Alumahan, matang-baka (night), *talang-talang* (rare, day); dalagang-bukid (fusilier) in the amihan season, salay-salay (yellowstripe scad) in the habagat |

Crab pots here bring up **alimango** (mud crab), **curacha** (spanner crab) or **sugpo** (tiger prawn).

- **Salt and daing:** rake Manang Rosa's salt beds once a day for 3 salt, but only if it hasn't rained since the
  morning (Tomas or Pip can tell you tomorrow's weather). Build a **drying rack** (4 wood, 2 stone; key 0 in the build
  bar) and press E to lay out up to three raw fish with a salt each. They need six hours of clear daylight (between
  6 AM and 6 PM) to become **daing**; tamban and sapsap dry whole into **tuyo**. Rain stops them drying. F takes them back.
- **Manang Rosa's provisions:** once you've met her, she wants two or three preserved fish a day for the boats, and pays
  25 coins each (plus 10) and 2 salt. In the amihan it's dried fish (daing or tuyo); in the habagat, when drying is
  chancy, it's tinapa or paksiw. When you have them, E hands them over.
- **Food:** Habagat's bushes give **calamansi**. At a stove: **kinilaw** (fish, calamansi, salt), **sinigang na isda**
  (2 fish, 2 calamansi), **ginataang isda** (fish, coconut) and **paksiw na isda** (fish and suka, vinegar from Pip).
  On a smoking rack, 2 galunggong, tamban, sapsap, matang-baka, alumahan, salay-salay, bangus or tilapia and a salt
  make 2 **tinapa**.
- **Gleaning:** at low tide walk the wet sand by the water on Habagat's beaches for cowries, sea urchins and sea
  grapes. Each tide has only so much, and the water coming back takes the rest. The tide follows the moon (see
  **The Sea school** below): the clock's tooltip here says when the next low tide is and how big.
- **Sungka** with Lola Pacing: seven shells in each little house; sow them round, never into her head. End in your own
  head and go again; end where there are shells and pick them up and keep going; end in an empty house of yours and take
  hers across from it. A win pays 40 coins once a day.
- **The regatta:** sail close by every islet to chart all twelve, then Dado races you round his buoys. The next gate
  flashes gold. Beat his lolo's record for the old agong; there are medals and a daily prize too. Your first gold
  wins Dado's **painted paraw sail** (red, gold, blue and green panels), which your boat wears from then on.
- **The lighthouse:** bring Tatay Celso what he needs, three times, and Parola burns again: its beam sweeps the sea at
  night, and squid and tarpon come to the pier.
- **The vanishing moon (spoilers):** the Case board gets a second case. Four clues on three islands lead to a full-moon
  night at Parola, when the old story of **Bakunawa**, the sea serpent that swallows moons, turns out to be true. Nobody
  fights it: you beat the agong on the beat while everyone bangs their pots, until it gives the moon back.
- **The marine sanctuary** (on the Amihan chart, south of Bakawan and Baga): yellow buoys mark it. Nothing is fished or
  trapped inside, but casts just outside bite quicker, with better luck, as fish spill out. Sea turtles, a dugong,
  a banded sea krait (*walo-walo*), a giant clam (*taklobo*) in the east seagrass and, by day, a whale shark live there. You can **watch** them (E, from
  the boat or the water) for the Fish log's Sightings card; Bantay Joy, the sea warden, rewards you for seeing all five. They are protected in the Philippines,
  so they're never catches.

Knocked out anywhere in Habagat, you wake at Manang Rosa's, your boat at the Asinan landing. The region has its own
gong music.

The people and places are fictional, inspired by Philippine traditions: Pangasinan's salt farms, the Hundred Islands,
Iloilo's paraw regatta, the Visayan story of Bakunawa, and community-run marine sanctuaries with their *bantay dagat*.
The Moonscale charm and Haring Buan-buan are inventions for the game.

## The bubo
Talk to **Tala** on Bakawan and she gives you a **bubo**, a long basket trap woven from split bamboo, and shows you how
to weave more at a workbench (4 wood, Tools tab). Her kind is for fresh water: set it (B, then click **Bubo**, the last
piece in the build bar, or step to it with the bumpers; it has no number key) at the edge of the Saltmere lagoon, the
oasis, the Mirewood swamp, the Amihan village pond or Bakawan's mangrove pool. The karst lagoon, the atoll lagoon and
the sea are salt; use a crab pot there. Face it the next morning and press E to lift it: one or two of that water's
small, everyday fish (nothing rare, nothing over 3 kg). It's marked on your chart, and taking it down gives it back.

## Maya's guso farm
New in **1.15.0**. **Maya** on Luntian is starting a seaweed farm in the karst lagoon: a living for the village that
takes no fish out of it. **Guso** is a seaweed farmed all over the Philippines. Talk to her and she gives you two
cuttings and the two lines at the lagoon's west end (the stakes and ropes in the water).
- **Plant:** stand on the sand beside a line, face it and press E to tie a cutting on.
- **Harvest:** on the second morning the line is a row of fat, golden-green bunches. E cuts four and ties one straight
  back on, so the line keeps growing. A **storm** while it grows tears half of it away (two instead of four), so watch
  the forecast; the habagat is hard on the lines.
- **Dry it:** guso needs no salt. At a drying rack, E (or F, if you've salted fish to lay out instead) spreads up to six
  bunches. After six hours of clear daylight they come off as **dried guso**, which Pip buys for 14 coins (the traders
  make carrageenan from it), and which rolls **sushi** and **maki** like any seaweed.
- **Eat it:** fresh guso is a crunchy snack (5), or, with a splash of suka at a stove, **ensaladang guso** (30).
- **The co-op:** bring Maya 4 dried guso for the village co-op's first sale. She pays 80 coins and opens the two east
  lines for you as well. If you run out of cuttings with a line empty, she has more.

The chart marks the farm (and when a line is ready). Lines and what's on them are saved.

## Bakawan at night
New in **1.15.0**. Once Tala has given you the bubo, ask her again: she keeps a list of what visitors see on Bakawan.
- **Alitaptap:** on the island's west shore stand three **pagatpat** mangroves. After dark, unless it's raining,
  thousands of fireflies gather in them and flash together, the whole tree at once. Press E nearby (on foot, or from
  your boat just offshore) to watch.
- **Glowing water:** on nights when the moon is small (not the nights around the full moon, and not in a storm), the
  water around Bakawan glows blue-green wherever it's stirred: sail through it and your wake lights up. Tidemane's
  wake and a wader's footsteps glow too, and so does the surf on its shore.
- **Tala's list:** the alitaptap, the glowing water, the **tarsiers** and the **hornbills** (press E to observe them).
  Each counts on the Fish log's Sightings card ("Legends & more"), next to the sanctuary's animals. Show her all four
  for a reward. They're only ever watched.

Firefly displays in mangroves, like those along the Iwahig, Donsol and Abatan rivers, and plankton that glow when
disturbed are both real; the trees and the list are the game's own.

## The restless sea (story 3, Amihan Village)
Once you've met Ma'am Isay, a day later the ground shakes while you're on Amihan Village's island. It's a small one:
get into the open, away from palms and houses, then **duck, cover your head and hold on** (hold E). Afterwards the
bangus pond has drained, and a new case opens on the Case board (its third tab, *The restless sea*):

1. **Ma'am Isay** explains earthquakes: rock slipping along a fault, the Pacific Ring of Fire, PHIVOLCS. Lira's lola
   had a Tagalog story for them, about Bernardo Carpio, trapped in the mountains of Montalban.
2. **Niko** tells his lola's version of a northern Luzon story: the **berberoka** held back the water so the fish
   lay stranded, then let it go. The pond itself drained through a crack the shaking made in its bank: bring Niko
   6 stone and 2 wood and he mends it; it refills on the tide by the next day.
3. **Lira** sends you to the old post by the landing, with a notch cut high up: when her lola was a girl, after a
   great shaking, the sea ran out past the reef and came back up that far.
4. **Ma'am Isay** puts it together: those remembered signs fit a tsunami (nobody knows how the berberoka story began).
   PHIVOLCS's signs are **Shake, Drop, Roar**, and any one of them means go uphill at once. Put up the three
   evacuation-route signs from the landing to **School Rise**, then run a drill with Mia, Jun and Bea: walk the route
   and call to them on your way (E near them; one call reaches everyone in earshot).

On a later day the ground shakes hard. Duck, cover and hold in the open; then go straight up to School Rise along the
signs, calling to anyone you pass. Lira and Niko head up straight away; the children go when you call, or when Ma'am
Isay rings the school bell. From the rise you watch the sea run out past the reef, roar, and come back over the low
ground, twice. Nobody gets hurt: anyone caught down on the beach is pulled up the slope. Once everyone's counted, an
hour passes and the town's disaster office gives the all-clear on the radio. The case closes with a card, 150 coins
and a short report on what you did; Niko has a crab pot for you the next day.

While it's happening the clock, hunger and the derby clock wait, and you can't board a boat, ride, build or fish. A
save made in the middle just starts it again the next time you're on the island by day.

## Beneath the clouds (story 4, Baga Island)
Baga, the volcanic island in Amihan, now has two people on its north shore. **Manay Mila** grows abaca and pili in
the rich volcanic soil; help her tie the abaca fibre to dry and plant a pili seedling, and she tells you her lola's
version of the Bicol story of **Daragang Magayon**: Magayon and Panganoron (whose name means cloud) died in the fighting
Pagtuga (eruption) began, their grave grew into Mayon, and the clouds round its summit are the two of them together.
**Ben** runs Baga's volcano station. His board shows the alert level, using the levels PHIVOLCS gives Mayon (0 No
Alert to 5 Hazardous Eruption; the alert card explains each). Baga and its station are made up for the game. Nobody
goes past the marker stones round the crater, the permanent danger zone, at any level.

On a later day Baga gets restless (Level 1), and Ben needs help with the readings, simplified examples of what a
volcano station measures: click each volcanic earthquake on the seismograph (not the surf's wiggles), read the GPS
marks (is the ground swelling?), sweep the gas scanner across the plume (sulfur dioxide, in tonnes a day), and check the
summit camera. Each one has to be read right before the next; Ben explains anything you get wrong. The first round
takes Baga to Level 2: pack a go-bag at his crate while it's calm. The next day's readings are the twist: fewer quakes,
but a glow and rockfalls at the crater. Signs don't all rise together. Level 3, and the disaster office orders
everyone off Baga, early, while the sea is calm.

Call to Manay Mila, sign the evacuation list at the jetty and board Niko's banca to Amihan Village (nothing has to be
finished first: if you dawdle, Ben and Niko see you aboard). The school is the shelter: before the ash comes, cover the
water jars, take the downpipe off the rain barrel and close the shutters; when the ash falls, stay inside, hand out
sealed water and register everyone. Then wait for rain: rain on fresh ash makes **lahars**. When it has rained, use the
display in the school to close the channel and the river mouth on the map and radio the warning, and watch the lahar
rush down the channel and spill past its banks. The case closes with a card, 150 coins and a report on what you did;
Manay Mila has an **abaca line** for you the next day. Finishing doesn't make Baga safe: an advisory reopens its north
shore and reef four days after the order, and the danger zone and the channel stay closed.

## The Sea school
New in **1.17.0**: three ways to learn about the sea that also make you a better fisher. Each has a badge in the
journal's **Sea school** tab, bronze, silver and gold, with a prize at every step; gold in all three is a diploma.
None of it is needed for anything else.

**Ma'am Isay's class.** Ma'am Isay teaches the children at the little school on the north side of Amihan Village
(07:00-16:00 they're on the bench out front). Once you've caught four kinds of fish she gives lessons about them:
eight questions with three hearts, a quick-answer bonus, a streak, and one **Ask the class** where the children vote
(they know the islands' own fish best). Name a fish from its picture or its shadow, say where it lives, when it bites,
how it fights, which grows heavier, which bait it likes, whether it's a real animal, or which fish a true fact or a
scientific name belongs to. Every answer, right or wrong, shows you the right one. Click an answer or press 1 to 4.
The first lesson of the day pays from the school fund (6 coins a right answer, 30 more for all eight); practise as much
as you like after. Fish you got wrong come up again first next time. Three right answers about a fish and you've **learned** it: a gold star in the Fish log. Learn 5
for her **field guide** (a fish you've learned is named the moment it bites), 15 for 150 coins, 30 for a **gold star
pin** (rare luck +1).

**Let it go.** For a few seconds after you land a fish, F lets it go. Little ones (well under the usual size) say
*Undersized*: they haven't spawned yet. **Galunggong and tamban** are in their closed season in the amihan, when Pip
won't buy them. Letting those go counts for the badge and a little experience, and any fish you let go may be back
at the same spot a day or more later, grown ("It's the one you let go!"). Crabs and lobsters from a pot go on a
**sorting tray**: measure the shell against the keep line (for alimasag it's the real Philippine minimum, 10.2 cm),
turn it over to check for eggs (a male has a narrow, pointed apron, a female a broad round one, and a female carrying
eggs has a spongy orange or dark mass under it), then keep it or let it go (keys 1, 2, and 3 to turn it). Lobsters
and crayfish are measured by the length of the carapace, the head shell. Eight right
in a row and you sort by eye from then on. Prizes: a **dehooker** (fish you let go come back grown far more often),
150 coins, and a **steward's badge** (the fish you catch run a tenth heavier).

**The tides.** The tide now follows the moon: a low tide every 12 hours 25 minutes, so each day's comes about 50
minutes later. Around the full and new moon the tides are big (**spring tides**): more to glean, for hours, and at the
lowest the shallows beside Habagat's beaches dry right out, so you can walk out onto the **reef flat** (where an
octopus sometimes turns up in a pool). At a half moon they're small (**neap tides**). Get back to the beach when
the tide turns. Talk to **Lola Pacing** a second time and she explains it and gives you a **tide table** (the next three
days as a graph, with the moon each day; hover to read it). Once a day she asks a riddle (F by her sungka board):
when's the next low tide, the lowest, the next one in daylight, the smallest day. Click the answer on the graph.
Find her the lowest tide still to come and she'll meet you on the flats below her board then, with two of her
grandchildren, for a **reef walk** that lasts until the tide comes back. Prizes: 60 coins, a **gleaner's basket** (half as many finds again), and a **tide watch** (the tide in
the clock's tooltip anywhere, and octopus more often).

The real facts: closed seasons protect spawning fish in parts of the Philippines (galunggong off northern Palawan from
November to January, sardines in the Visayan Sea and off Zamboanga from mid-November to mid-February, roughly the
amihan). Philippine rules ban keeping egg-carrying blue swimming crabs or ones under 10.2 cm. Spring and neap tides
come from the sun and moon pulling together or at right angles; on Earth that's about every two weeks, here every
four days, as the game's moon goes round in eight. Pip's closed season and the other crabs' keep lines are the game's
own.

## The island guide and the fish album
New in **1.18.0**, both for learning about the real places and animals behind the game.

**The island guide** has a page for every island and sea you've found (the rest stay "Uncharted"): what kind of
place it is (a coral atoll, limestone karst, a volcanic island, mangroves, salt flats...), what the land is like, its
bodies of water marked fresh, brackish or salt, the climate, what lives there, the fish you can catch there, and an
"In the real world" box on the real landforms and places behind it (how atolls form, what karst is, the Pacific Ring of
Fire, mangroves, salt-making, the monsoons, marine sanctuaries). Each page has a cross-section drawing of the island
(heights stretched to show the shape), the ocean's layers for the open seas, or a cut-away of the caverns. Open it with
**Island guide** on the map, by clicking an island's name on the map, from the Fesh-dex, or from the journal.

**The fish album** is a scrapbook like the ones made for school: a page for every fish you've caught, taped in with its
local, English and scientific names, its family, the water it lives in (fresh, brackish or salt) and what it eats.
The chapters follow how scientists group animals (sharks and rays, ancient lines, carp and catfish, tunas and billfish,
reef fish...) and end with the crustaceans and the squid and octopus, which aren't fish at all. Its first page explains
scientific names and families. Empty frames show only the water a missing fish lives in, and a full chapter pays five
coins a fish. Open it from the Fesh-dex, a fish's card (**Album page**), the journal's Sea school tab or the island
guide; turn the pages with the arrow keys.

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
Tidemane turns to face all four directions, with front and back views while galloping or swimming. Its mane and
tail move with it, and the rider, fishing rod and held catches share its bounce on land and on the water.

## Platejaw (spoilers)
The Ancient pool has a guardian. Land the **Ancient coelacanth** and, as you close its card, the black water heaves:
**Platejaw**, an armoured fish as long as two people, rises out of the pool. The coelacanth is already in your bag,
so it's yours however the fight goes. Platejaw never leaves the water for long:

- It circles under the surface, following you round the pool. Linger right at the edge and it **snaps** at you
  (12 damage).
- It surfaces at the edge nearest you and **lines up a lunge**: red arrows mark its lane, and brighten once it has
  picked its line. Step out of it. A lunge that catches you does 18 damage.
- After a lunge it lies **stranded** on the stone for a moment. That's the only time a blow gets past its armour, so
  strike it then; blows on its armoured head in the water just clang off.
- Lunging into a pillar **dazes** it for longer, and blows land harder.
- Once it's tired it **rams the side of the pool** and rocks fall from the roof. Orange rings show where (10 damage
  each): step out of them.
- Near the end it **throws a wave** out of the pool (14 damage). Get right behind a pillar, where green marks show
  the shelter: the wave breaks on it.

Wear it out and it sinks back, leaving a piece of its armour on the stone. A workbench makes that into **plate
armour** (with 2 iron bars), which halves what monsters and fighting fish do to you.

If you black out you wake at the cave mouth as usual, and walking off down the passage ends the fight too. Either
way, the **carved stone** south of the pool calls it back: knock on it. A save from before 1.19 that already has the
coelacanth meets Platejaw the same way.

Platejaw is made up, but it's based on a real fish: **Dunkleosteus**, an armoured placoderm that lived about 380 to
360 million years ago. Its card on the Fish log's last page tells you about it.

## Music
Every island has its own looping tune, and so do Frostfang Caverns, the insides of houses and the fights with
Tidemane and Platejaw. Rain adds its own patter. Like the sound effects, the music is composed by code at startup, so there are
no audio files. Turn it on or off from the pause menu.

## Health
The heart bar (top left, under the clock) is your health. Cave monsters, Tidemane, Platejaw and some Amihan fish can hurt you. Health comes
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
| Tuyo | 16 | Tamban or sapsap salted and dried on a rack |
| Tinapa | 22 | Small fish, bangus or tilapia and salt (smoking rack) |
| Daing | 28 | Any other fish salted and dried on a rack |
| Paksiw na isda | 40 | 1 raw fish and 1 suka (stove) |
| Guso | 5 | Maya's lines in the Luntian lagoon |
| Ensaladang guso | 30 | 2 guso and 1 suka (stove) |

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
blade 10 damage); without one you swing your pickaxe (2) or your fists (1). Shell armour cuts damage by a third, and
plate armour (made from Platejaw's armour) by half.

**The Ancient Floor** (floor 12) is always the same: a flooded ruin of pillars and glowing runes around the
Ancient pool, where the legendary **Ancient coelacanth** lives. Shades and rock crabs guard it, and it's the only
place with abyssite, which makes the Ancient rod. A carved stone south of the pool shows what else lives in it
(see Platejaw above).

## Crafting
Tomas's hut (walk to his door and press E) has a workbench, a stove and a bed you can use from the start.
Your own shacks start empty: go inside and press B to place a workbench, furnace, stove, bed and decorations.

| Station | Makes |
|---------|-------|
| Workbench: Tools | Stone axe (3 wood, 2 stone), pickaxes: stone (3 wood, 3 stone), copper (2 wood, 2 copper bar), iron (2 wood, 3 iron bar), gold (2 wood, 3 gold bar), crystal (2 gold bar, 3 crystal, 1 shadow essence); sailboat (20 wood, 4 iron bar, 1 sailcloth); fishing spear (2 wood, 1 copper bar); crab pot (4 wood, 1 wool); bubo (4 wood, once Tala has shown you) |
| Workbench: Rods | Copper rod (2 wood, 3 copper bar), iron rod (copper rod, 3 iron bar), crystal rod (iron rod, 1 iron bar, 3 crystal), Ancient rod (crystal rod, 2 gold bar, 3 abyssite) |
| Workbench: Tackle | Reels: copper (1 wood, 2 copper bar), iron (copper reel, 2 iron bar), gold (iron reel, 2 gold bar). Lines: silk (3 wool), crystal (silk line, 1 crystal, 1 slime gel). Hooks: barbed (1 copper bar), big-game (barbed hook, 2 iron bar). Bobbers: cork (2 wood), glow (cork bobber, 2 slime gel). Sinkers: stone (3 stone), iron (stone sinker, 1 iron bar). Lures: spinner (1 copper bar), fly (1 wool, 1 bat wing) |
| Workbench: Gear | Big sail (2 sailcloth, 4 wood, 1 iron bar), echo sounder (2 copper bar, 1 gold bar, 1 crystal), sunglasses (1 copper bar, 1 crystal), fish finder (2 copper bar, 1 iron bar, 1 crystal), waders (3 slime gel, 2 wool), abyssite charm (2 abyssite, 1 gold bar), headlamp (2 copper bar, 1 crystal), cooler (6 wood, 1 iron bar, 1 wool) |
| Workbench: Combat | Swords: copper (1 wood, 2 copper bar), iron (1 wood, 3 iron bar), gold (1 wood, 3 gold bar); crystal blade (2 gold bar, 3 crystal, 2 shadow essence); shell armour (4 crab shell, 2 iron bar); plate armour (Platejaw's plate, 2 iron bar), once you've beaten Platejaw |
| Workbench: Bait | Glow bait ×3 (2 bait, 1 slime gel), cut bait ×4 (1 raw fish), chum ×2 (1 raw fish, 1 berries) |
| Furnace | Copper bar (2 copper ore, 1 wood), iron bar (2 iron ore, 1 wood), gold bar (2 gold ore, 1 wood) |
| Smoking rack | Smoked fish (1 raw fish; fills 30, sells for 20), fish jerky ×2 (3 cut bait), tinapa ×2 (2 galunggong, tamban, sapsap, matang-baka, alumahan, salay-salay, bangus or tilapia, and 1 salt; fills 22) |
| Campfire (F) | Grilled fish, fried egg |
| Cooking stove | Everything the campfire makes, plus fish stew, sushi rolls (1 raw fish, 1 seaweed or dried guso) and maki platters (3 raw fish, 2 seaweed or dried guso, 1 berries; fills 80), the Habagat dishes, paksiw and ensaladang guso (2 guso, 1 suka) |

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
| Drying rack | 4 wood, 2 stone | Salt fish and dry them in the sun, or dry guso (key 0) |
| Bubo | 1 bubo (workbench, once Tala has shown you) | Goes in fresh water; lift it each morning (no number key: click it or use the bumpers) |
| Bahay kubo | 10 wood, 2 stone (once you've been to Amihan) | A nipa hut on stilts with woven bamboo walls; go inside and furnish it like a shack (no number key) |

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
- `src/Guardian.cs`: Platejaw, the Ancient pool's guardian: its fight, the carved stone, and its card
- `src/RestlessSea.cs`: the restless sea (story 3): the tremor, the clues, the pond, the signs and the drill, the evacuation, and the sea going out and coming back
- `src/Magayon.cs` and `src/MagayonUi.cs`: beneath the clouds (story 4): Baga's volcano, Manay Mila and Ben, the readings, the go-bag, leaving Baga, the school as the shelter, the lahar watch
- `src/Archipelago.cs`: Amihan's four islands, village NPCs, landmarks and regional scenery
- `src/Seaweed.cs`: Maya's guso farm in the Luntian lagoon
- `src/Bakawan.cs`: Bakawan's firefly trees, the glowing water, sightings and Tala's list
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
- `src/Ui.cs`: HUD, prompts, dialogue box, build bar, end screen, catch cards, Fesh-dex and Fish log, case board
- `src/Dex.cs`: the Fish log's fish cards, biting now, seen fish, filters and each island's reward
- `src/FishArt.cs`: every fish's own picture (a body shape, colours and a pattern each) and the line about it
- `src/Chart.cs`: the map: charting as you explore, label placement, hover details, the pin and its compass
- `src/UiMenu.cs`: the title screen, the save slots, and the menu (Game, Settings and Controls tabs)
- `src/Settings.cs`: settings (`settings.json`) and the rebindable controls
- `src/UiPanels.cs`: the bag, crafting stations and the character creator
- `src/Gfx.cs`: UI scaling, fonts, text wrapping, buttons, input
- `src/Art.cs`: a small vector renderer and the smooth creature portraits
- `src/Sfx.cs`: sound effects, synthesized at startup
- `src/State.cs`: the save file and the three save slots
- `src/AutoTest.cs`: debug-only scripted play-through (see below)
- `src/AmihanTests.cs`: navigation, village, wildlife, map and fish-attack checks
- `src/GusoTests.cs`: the guso farm, drying and cooking guso, the co-op, the fireflies, the glowing water and Tala's list
- `installer/Fesh.wxs`: the installer definition

## Automated check
Debug builds include a scripted play-through that exercises character creation, building, fishing (power casts,
depth, bait, every fight style, perfect hooks, sizes, records, chests, the ice hole, spearfishing, crab pots, chum,
worms and crickets, the tackle box, the derby, legends, the moon and storms), all five
islands, odd catches, chopping, mining, crafting, cooking, eating, houses, the cave (ore tiers, monsters,
health, fainting, the Ancient Floor and the lift), animals, the shop, requests, bait, weather, planters, the
aquarium, the boat, the Starwell and the whole fight with Tidemane, the fight with Platejaw, the restless sea from the tremor to the all-clear, Baga's volcano from the first reading to the lahar warning, riding and swimming, music, save slots, settings,
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
sheets next to it (`-a`, `-b` and `-c`), touches no saves, and quits. The `-boat-directions` and `-mount-directions`
sheets show all four headings while moving, fishing and holding a catch (plus the big sail and trolling).
Set `FESH_DIRECTION_TEST=1` alongside `FESH_AUTOTEST` and `FESH_SAVE` to run only the direction checks, or
`FESH_GUSO_TEST=1` for only the guso farm and Bakawan's night lights, `FESH_GUIDE_TEST=1` for only the guide, the
journal, the aquarium's layout and the real-fish notes, `FESH_EDU_TEST=1` for only the Sea school (tides, letting
fish go, sorting crabs and Ma'am Isay's class), or `FESH_ATLAS_TEST=1` for only the island guide and the fish album;
the full play-through includes them too.

Fonts: Pixelify Sans, Special Elite and Atkinson Hyperlegible (the Clear font, © Braille Institute of America; its
licence is in `Assets/Fonts/AtkinsonHyperlegible-OFL.txt`), all under the SIL Open Font License.
