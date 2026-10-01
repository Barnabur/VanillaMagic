# VanilaMagic

**Vanilla-friendly early-game magic for Valheim.**

In vanilla, magic only becomes available in the Mistlands. VanilaMagic adds a small set of
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
| **Healing Staff** | Swamp | 2 | 10 Withered Bone, 2 Ancient Seed, 5 Ectoplasm | Heals you and everyone standing close, 20 eitr |
| **Frost Wand** | Mountains | 3 | 5 Silver, 1 Crystal, 2 Freeze Gland | Single frost bolt that bursts on impact. 90 frost, 35 eitr |
| **Stone Wand** | Plains | 4 | 5 Black Metal, 4 Obsidian, 5 Fine Wood | Heavy rock that shatters and knocks enemies back. 240 blunt, 50 eitr |

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
wraith chains and Fenris hair:

- Light armor (8, +3 per level) with eitr regeneration on every piece.
- **Full set bonus**: +10 Elemental Magic and +10 Blood Magic.
- The cape is frost resistant.
- Upgrade costs grow with the biomes (Wolf Hair Bundle, Ectoplasm, Leather Scraps, Chain).

### Drop changes

These make the ingredients reachable in their own biome:

- **Wraiths** (Swamp, at night) always drop 1 **Ectoplasm**. **Ghosts** now drop it only 10% of the time.
- **Fenrings** have a 25% chance to drop a **Wolf Hair Bundle**.

## Installation

### With a mod manager (recommended)

Install with **r2modman** or **Thunderstore Mod Manager**. BepInEx and Jötunn are installed automatically.

### Manual

1. Install [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/).
2. Install [Jötunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/).
3. Copy `VanilaMagic.dll` into `BepInEx/plugins/VanilaMagic/`.

### Multiplayer

**Everyone must have the mod**, including the server, with the same minor version. Jötunn checks this when you connect.

## Compatibility

- Wands, armor and foods are new items. Vanilla items are only touched where listed above (Viking Cupcake stats, the oven conversion, Ectoplasm/Fenring drops, the unused blue mushroom).
- Frost Caves get variants of their rooms with mushroom spots. Only newly generated caves have mushrooms, so explore new areas or use a new world.
- Mods that heavily rework the same drops or the Viking Cupcake may override each other's changes.

## Changelog

### 0.0.3
- Surtling Wand renamed to **Flame Wand** and rebalanced: 20 blunt + 20 fire damage (was 22 + 15), 7 eitr per attack (was 10).
- Flame Wand now fires where you aim (its fireballs used to drop well below the crosshair).
- All wands and the Healing Staff: 100 durability, +25 per upgrade level.
- Healing Staff now uses Withered Bone instead of Bone Fragments.
- Ectoplasm: Wraiths always drop exactly 1, Ghosts now 10% for 1.

### 0.0.2
- Wraith armor set and cape, item icons, network compatibility check.

## Known issues

- Balance is still being tuned. Feedback is very welcome.

Source code and bug reports: https://github.com/Barnabarz/VanilaMagic
