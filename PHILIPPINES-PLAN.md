# The Philippines map: plan and step 2

A working plan for the new-game map of the real Philippines, written so the next session can pick it up after the
conversation is compacted. Technical details of what is already built are in `CLAUDE.md` ("The Philippines map, step 1");
the user's decisions are also in memory (`philippines-map.md`).

## What the user decided (2026-11-20)

- New games choose their map in the creator: the original world (Pulo ng Asin) or the Philippines. Old saves stay on the
  original map.
- The Philippines are **true to scale** at the **large** size: about 1.1 km a tile, so Marinduque is ~30-40 tiles across.
- Islands: the ~12 main ones (Marinduque, Luzon, Mindoro, Palawan, Panay, Negros, Cebu, Bohol, Leyte, Samar, Mindanao),
  plus small islands where a boss needs one.
- **New stories** for the new map (the old stories stay on the old map).
- **Marinduque is the starting island.**
- Bosses move to homes chosen by real habitat (list approved by the user, below).
- 3D models in `C:\Users\growk\Desktop\Feshtry2` (a Rust/Bevy prototype) can be rendered to pixel sprites in Blender, used
  as reference, and new models made too, so things don't look copy-pasted.
- Order: **1** the map (done) → **2** towns, fishing spots, animals → **3** the bosses → **4** the new stories. An exe and
  APK after each step. Run only the targeted tests (`FESH_PH_TEST=1` and whatever else the step touches).
- The user doesn't use Codex any more: do design and review passes alone.

## Step 1 (done, uncommitted)

The country from Natural Earth's 1:10m coastlines (`tools/phmap/rasterize.js` → `PhMapData.cs`, 968 x 1684 tiles), the map
choice in the creator, the start at Boac with a banca, sailing anywhere, place names, a chart of the whole country, the
world as the country with its baked pixel layers in a 192-tile window that moves as you go, the original world's content
gated off with `PhWorld`, First steps skipped. `FESH_PH_TEST=1` (18 checks).

## Step 2: towns, fishing spots and animals

**Built (2026-11-21, uncommitted)**: see CLAUDE.md, "The Philippines map, step 2". Changes from the plan below: Marinduque has four settlements, not six (Boac with the only church, and the villages of Santa Cruz, Gasan and Torrijos), because at true scale it is about 30 x 33 tiles and six towns were crammed; only bigger towns have a church; the building colours were brightened from the dark Feshtry2 textures to muted, natural tones (the user found them gloomy, then too bright). Also added: island names by connected land (Sabah was called "Palawan"), and a zoomable chart with pins. Not done yet: a Fish log page for this map (catches count on each fish's own page), an inn that costs anything, named market vendors with stories.

Goal: the islands feel lived in and fishing has places and purpose, starting with Marinduque and then a town or two on
each main island.

### 2a. Places as data
- A new data file (e.g. `PhData.cs`) listing places by latitude and longitude (`PhPlace(lat, lon)` turns them into world
  pixels): towns, piers, fishing spots, landmarks. Snap each to the nearest suitable tile at load (a beach for a pier, a
  coast for a town, water for a spot) with a search like `PhStandNear`.
- Marinduque first, its six towns: **Boac** (the start, the capital, west coast), **Mogpog** (north-west), **Santa Cruz**
  (north-east, the biggest), **Torrijos** (east; Poctoy white beach), **Buenavista** (south; the hot springs of Malbog and
  Mount Malindig), **Gasan** (south-west, facing the Tres Reyes islands). Then one or two towns per main island:
  Manila / Batangas / Legazpi (Luzon), Calapan (Mindoro), Puerto Princesa / Coron (Palawan), Iloilo (Panay),
  Dumaguete / Bacolod (Negros), Cebu City, Tagbilaran (Bohol), Tacloban (Leyte), Catbalogan (Samar), Davao, Zamboanga,
  Cagayan de Oro (Mindanao), Masbate.
- Keep everything keyed to world (country) coordinates: builds, felled trees, picked bushes and wear already are.

### 2b. Towns
- A town is stamped onto the tiles round its point when the country's tiles are made (`PhInitTiles`): a plaza (path tiles),
  a church, 6-12 houses (bahay kubo and bahay na bato), a market shed, a pier/jetty into the sea, a few palms and gardens.
  Solids and targets for each building; stamp deterministically from the place's name so a town always looks the same.
- Sprites: render Feshtry2's models through the existing Blender method (`tools/blender/pixelrender.py`; import glTF with
  `bpy.ops.import_scene.gltf`) and make variants (roof colours, sizes, mirrored, extra props) so no two towns match.
  Candidates from Feshtry2: `bahay_0..7`, `church`, `market_shed`, `jetty`, `court`, `hoop`, `flagpole`, `water_pump`,
  `fence`, `garden_0/1`, `laundry_0/1`, `net_rack`, `net_spread`, `tuyo_rack`, `fish_table`, `fish_dryer`, `yard_goods`,
  `small_boat`, `banana_0/1`, `palm_0..7`, `tree_0..6`, `bush_0..3`, `fern_0/1`, `flowers_*`, `boulder*`, `limestone*`,
  `stump*`, `driftwood_*`. People: `fisher`, `fisher_b..f`, and named models (`ambo`, `corazon`, `crisanto`, `danny`,
  `domingo`, `felipe`, `goryo`, `iyo`, `jun`, `lorna`, `maricel`, `minda`, `nita`, `oyet`, `paolo`, `ramon`, `remedios`,
  `rosario`, `sela`, `tessie`) as references for villagers drawn with `LookData.DrawFigure` (people stayed hand-drawn:
  3D lost at that size).
- Villagers: a few strollers per town (reuse `Stroller` and `LookData.DrawFigure`, turning and idling already work), each
  with a name, a look and a few lines; they sleep at night like the original villagers (`FolkAsleep`).
- A market in each town buys fish and sells bait, tackle and food (reuse Pip's shop panel with the town's stock); a place to
  rest (a town inn, or your own shack: building works on this map).
- The chart marks towns you've visited; the status card names the town you're in.

### 2c. Fishing spots
- Spots of the new map live in their own list (not `Data.Spots`, whose coordinates are the original world's): each with a
  kind and a fish list by habitat: coral reef, mangrove and estuary, river mouth, lake (Laguna de Bay, Taal), open sea,
  deep channel. Use real Philippine species already in the game where they fit (galunggong, tamban, bangus, tilapia,
  dalag, tulingan, lapu-lapu, maya-maya, talakitok, etc.) and the Feshtry2 fish list as a guide
  (`fish_barakuda`, `fish_biya`, `fish_dalagangbukid`, `fish_dalupapa`, `fish_dapa`, `fish_dilis`, `fish_gulyasan`,
  `fish_hasahasa`, `fish_igat`, `fish_kalaso`, `fish_kanduli`, `fish_kugtong`, `fish_labahita`, `fish_lapulapu`,
  `fish_pakol`, `fish_pampano`, `fish_paruparo`, `fish_suno`, `fish_talakitok`, `fish_tambakol`, `fish_tulingan`).
- `FindTarget`, `SpotPos`, `RollCatch`/`CatchOdds`, the Fish log (a page per region of the new map), the chart and
  `WildSpot` (casting anywhere) need a `PhWorld` path that reads this list. Today wild fishing on the new map falls back to
  the original world's nearest spot lists: replace that with the new spots' habitats.
- The open sea: per-sea fish lists (`PhNames` seas: Sibuyan Sea, Sulu Sea, Philippine Sea...), feeding frenzies already
  work anywhere.

### 2d. Animals
- Spawn animals near you (not the whole country): carabao, goats, chickens, dogs and cats round towns and fields, a few
  on open land, chosen by tile hash so the same field has the same animals; despawn when far. `SpawnAnimals` is gated off
  on this map today (`Survival.cs`).

### 2e. Tests and delivery
- Extend `PhTests.cs`: every town stands on land beside water with its pier reachable by boat; walking into Boac shows its
  buildings and villagers; talking to one; selling a fish at a market by real clicks; each spot is fishable from land or
  the boat and gives its own fish; animals appear near you and not far away; the chart marks visited towns. Screenshots of
  each town.
- Publish the exe (`build-installer.ps1`) and the APK (bump `ApplicationVersion` in `fesh-android/FeshAndroid.csproj`,
  `dotnet publish -c Release -r android-arm64`, copy the signed APK to `fesh-android/Fesh-latest.apk`).

### Questions worth asking the user at the start of step 2
- How many towns per island to begin with, and whether the towns should be real towns by name (as planned) or fictional
  villages on real islands.
- Whether markets should be run by named characters (a Pip-like vendor per town).
- Whether the start should stay at Boac or be a house of your own in Boac.

## Step 3: the bosses (approved homes)

| Boss | Home on the new map |
|---|---|
| Tidemane (hippocamp, ridden after) | Tubbataha Reefs, Sulu Sea (a real atoll: carve it, it isn't in Natural Earth) |
| Ironbill (marlin) | The open Philippine Sea east of Samar |
| Starfall ray (manta) | Ticao Pass ("Manta Bowl"), between Masbate and Sorsogon |
| Bakunawa (moon-eating serpent) | Off Bohol, in the Visayas |
| Mire leviathan (arapaima) | Agusan Marsh, Mindanao (carve the marsh: not in the data) |
| Haring Buan-buan (tarpon) | Naujan Lake area, Mindoro (add the lake: not in the data) |
| Sunscale lungfish | Paoay Sand Dunes, Ilocos Norte (dunes + an oasis pool) |
| Old Whiskers (catfish) | Laguna de Bay, Luzon (in the data) |
| Platejaw (cave pool armoured fish) | Puerto Princesa Underground River, Palawan (a cave scene) |

Each boss's fight code reads its pool or sea from the original world (`MirePool`, `LungPool`, `WhiskPool`, `TarponWater`,
`RayWater`, the Starwell, the Ancient Floor...): give each a `PhWorld` home, and the legends' bite conditions a spot on the
new map.

## Step 4: new stories

To design with the user: new stories set on real islands, one teller's version of each local legend, like the original
stories (keep the same care: attributed tellings, conservative facts, nothing that misleads).

## Engine notes for whoever continues

- On the Philippines map the world is the country: `COLS`/`ROWS` = 968 x 1684; every position is in its coordinates. The
  baked pixel layers cover a 192-tile window (`Scenery.bufX0/bufY0/bufW/bufH`, `BI(wx, wy)`), moved by `PhMoveWindow`.
  Read baked pixels only through `ShapePx`/`BI`; never index `shape` or a `.Buf` with `wy * PW + wx`.
- New systems that belong only to the original world must check `PhWorld`; new Philippines systems must not read the
  original world's fixed coordinates (Saltmere, Amihan, Habagat, Marilaya constants).
- Whole-map loops are 1.6M tiles on this map: keep per-frame work to the window or near the player.
- `FESH_SPRITES` without `FESH_SAVE` reads the real settings (a zoomed-in setting breaks the sheets); pass `FESH_SAVE`.
- Some files have CRLF line endings: multi-line `perl` substitutions miss there silently; use the Edit tool or check the
  result with `grep`.
