# VanillaMagic

**Vanilla-friendly early-game magic for Valheim.**

In vanilla, magic only becomes available in the Mistlands. VanillaMagic adds a small set of
basic magic tools that you can use from the **Black Forest through the Plains**, so a mage
playthrough can start much earlier.

The goal is to fit in, not to replace anything. Everything is built from the game's own
models, materials and effects, so it looks and feels like vanilla Valheim. The numbers are
tuned to sit next to the bows and weapons of each biome rather than outclass them. Your
Mistlands staves stay an upgrade.

## Features

### Wands and staves

Crafted at the **Forge**, one per biome, each a step up from the last:

| Item | Biome | Forge lvl | Recipe | What it does |
|---|---|---|---|---|
| **Flame Wand** | Black Forest | 1 | 7 Bronze, 1 Surtling Core | Rapid stream of embers. 20 blunt + 20 fire per bolt, 7 eitr |
| **Healing Staff** | Swamp | 2 | 10 Withered Bone, 2 Ancient Seed, 10 Ectoplasm | Heals you and everyone standing close for 10/15/20/25 s (by quality), 20 eitr |
| **Frost Wand** | Mountains | 3 | 28 Silver, 1 Crystal, 2 Freeze Gland | Single frost bolt that bursts on impact. 90 frost, 35 eitr |
| **Stone Wand** | Plains | 4 | 18 Black Metal, 4 Obsidian, 5 Fine Wood | Heavy rock that shatters and knocks enemies back. 240 blunt, 50 eitr |

All of them can be upgraded at the Forge and use the vanilla magic skills.

### Eitr food

You need eitr to cast, so the mod adds a few early sources of it:

- **Wildberries**: a glowing purple bush found in the **Black Forest** (more rarely in the Swamp and Plains). Eat the berries raw for a little eitr.
- **Ghostshake** (Cauldron lvl 2): 4 Wildberries + 1 Ectoplasm. A light eitr meal (5 HP / 20 stamina / 40 eitr).
- **Blue Mushroom**: grows in **Frost Caves** in the Mountains, using the game's own unused blue mushroom (15 HP / 10 stamina / 25 eitr).
- **Crystal Sugar**: grind Crystal in the **Windmill**.
  - **Eyescream with Sprinkles** (Cauldron lvl 3): the Eyescream recipe + 1 Crystal Sugar, trading a little health and stamina for 10 eitr.
  - **Frosted Sweetbread**: Viking Cupcake dough + 1 Crystal Sugar at the Prep Table, then bake it. The frosted Viking Cupcake now gives 20 eitr (33 / 33 / 20). Baking plain dough gives the new unfrosted **Sweetbread** with vanilla stats.

### Wraith armor

A 4-piece mage set for the Swamp-to-Mountains stretch (hood, robe, leggings, cape), made from
wraith chains and Fenris hair, crafted and upgraded at the **Workbench**:

- Light armor (8, +3 per level) with eitr regeneration on every piece.
- **Full set bonus**: +10 Elemental Magic and +10 Blood Magic.
- The cape is frost resistant.
- Upgrade costs grow with the biomes (Wolf Hair Bundle, Ectoplasm, Leather Scraps, Chain).

### Drop changes

These make the ingredients reachable in their own biome:

- **Wraiths** (Swamp, at night) always drop 1 **Ectoplasm**. **Ghosts** now drop it only 10% of the time.
- **Fenrings** have a 25% chance to drop a **Wolf Hair Bundle**.

The Ghost and Fenring drops can be changed in the [config](#configuration).

## Installation

### With a mod manager (recommended)

Install with **r2modman** or **Thunderstore Mod Manager**. BepInEx and Jötunn are installed automatically.

### Manual

1. Install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).
2. Install [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/).
3. Copy `VanillaMagic.dll` into `BepInEx/plugins/VanillaMagic/`.

### Multiplayer

**Everyone must have the mod**, including the server, with the same minor version. Jötunn checks this when you connect.

## Configuration

Settings are in `BepInEx/config/com.barnabur.vanillamagic.cfg`, created on first launch. You can also edit them in game with [Configuration Manager](https://thunderstore.io/c/valheim/p/Azumatt/Official_BepInEx_ConfigurationManager/) (F1).

| Setting | Default | Description |
|---|---|---|
| `Drops.FenringHairChance` | 25 | Chance (%) that a Fenring drops a Wolf Hair Bundle |
| `Drops.FenringHairAmount` | 1 | How many Wolf Hair Bundles a Fenring drops |
| `Drops.GhostEctoplasmChance` | 10 | Chance (%) that a Ghost drops Ectoplasm (vanilla: 100) |
| `Drops.GhostEctoplasmAmount` | 1 | How much Ectoplasm a Ghost drops (vanilla: 1-5) |

On a server, the server's values are synced to all players. Changes apply right away, also to creatures already in the world.

## Compatibility

- Wands, armor and foods are new items. Vanilla items are only touched where listed above (Viking Cupcake stats, the oven conversion, Ectoplasm/Fenring drops, the unused blue mushroom).
- Frost Caves get variants of their rooms with mushroom spots. Only newly generated caves have mushrooms, so explore new areas or use a new world.
- Mods that heavily rework the same drops or the Viking Cupcake may override each other's changes.

## Planned

- **More config options**: damage, eitr costs, recipes and the remaining drops (the first drop settings arrived in 0.0.4).
- **Balance pass**: further tuning of wand damage and eitr costs, Wraith armor stats and drop rates, based on playtesting and feedback.

## Changelog

### 0.1.1
- Flame Wand: bolts now leave from the wand head at chest height instead of appearing far in front of you (most visible when aiming down).
- Flame Wand: the first bolt fires about 1.5 s after you start the attack, then 1 bolt every 1.25 s (was 1.5 s).
- Healing Staff: the heal lasts 10/15/20/25 s depending on the staff's quality (shown in the tooltip).
- Recipes: Healing Staff 10 Ectoplasm (was 5), Frost Wand 28 Silver (was 5), Stone Wand 18 Black Metal (was 5); upgrades cost more Bronze/Silver/Black Metal accordingly.
- Wraith armor is now crafted and repaired at the Workbench (like the Fenris set) instead of the Forge.
- Fix: Ghosts spawned in dungeons dropped the vanilla 1-4 Ectoplasm instead of the configured amount; the Ghost, Wraith and Fenring drop changes now apply to every spawned creature, and config changes apply to creatures already in the world.

### 0.1.0
- Flame Wand: the first bolt now fires after a short delay, so holding the button is no longer slower than re-clicking. Durability is used only when a bolt is actually fired.

### 0.0.4
- Wands and the Healing Staff lose 1 durability per shot (the Flame Wand per bolt in its stream, not once per attack).
- New config file with server sync: Fenring Wolf Hair Bundle drop and Ghost Ectoplasm drop (chance and amount).
- Plugin file renamed to `VanillaMagic.dll` to match the mod name. Mod managers handle this automatically. If you installed manually, delete the old `BepInEx/plugins/VanilaMagic` folder.

### 0.0.3
- Mod name corrected to **VanillaMagic**.
- Surtling Wand renamed to **Flame Wand** and rebalanced: 20 blunt + 20 fire damage (was 22 + 15), 7 eitr per attack (was 10).
- Flame Wand now fires where you aim (its fireballs used to drop well below the crosshair).
- All wands and the Healing Staff: 100 durability, +25 per upgrade level.
- Healing Staff now uses Withered Bone instead of Bone Fragments.
- Ectoplasm: Wraiths always drop exactly 1, Ghosts now 10% for 1.

### 0.0.2
- Wraith armor set and cape, item icons, network compatibility check.

## Known issues

- Balance is still being tuned. Feedback is very welcome.

Source code and bug reports: https://github.com/Barnabur/VanillaMagic

## License

Code and original artwork: MIT, (c) 2026 Barnabur. See [LICENSE](https://github.com/Barnabur/VanillaMagic/blob/main/LICENSE).
Valheim and its assets are (c) Iron Gate AB. Files derived from game assets are not covered by the license.
