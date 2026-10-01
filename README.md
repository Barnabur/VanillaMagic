# VanillaMagic

**Vanilla-friendly early-game magic for Valheim.**

VanillaMagic adds a small set of basic, not overpowered magic tools you can use from the
**Black Forest through the Plains**: four wands and staves, early eitr foods, and a wraith
mage armor set. Everything is built at runtime from the game's own models, materials and
effects, so it looks and feels like vanilla Valheim.

- **Mod page, features and changelog:** [VanilaMagic/README.md](VanilaMagic/README.md). This is the same text that appears on Thunderstore.
- **Download:** Thunderstore (install with r2modman or Thunderstore Mod Manager).
- **Configuration:** `BepInEx/config/com.barnabur.vanilamagic.cfg`, synced from the server to all players. The Ghost Ectoplasm and Fenring Wolf Hair Bundle drops (chance and amount) can be changed there. All settings are listed in [VanilaMagic/README.md](VanilaMagic/README.md#configuration).

## Building from source

Requirements:

- Valheim with [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)
- .NET SDK (the project targets `net48`)
- [Jötunn](https://github.com/Valheim-Modding/Jotunn) 2.30.2, pulled in via NuGet (`JotunnLib`)

Setup:

1. Edit `Environment.props` and set `VALHEIM_INSTALL` to your Valheim folder. `MOD_DEPLOYPATH` defaults to `<Valheim>\BepInEx\plugins`.
2. Build:

   ```bash
   dotnet build VanilaMagic.sln -c Debug
   ```

| Configuration | Result |
|---|---|
| **Debug** | DLL copied to `BepInEx/plugins/VanillaMagic/`. Developer console commands are registered (`wandicon`, `wraithtint`, `dumpstaff`, `mushroomroom`, `whichroom`, `nearspawns`, `blinkspawn`, `mushroomonly`). |
| **Release** | Thunderstore package `VanilaMagic/bin/Release/net48/VanillaMagic.zip`, built from `VanilaMagic/Package` (manifest, icon) and `VanilaMagic/README.md`. No developer commands. |

Diagnostic logging uses `LogDebug`. To see it, set `LogLevels = All` in `BepInEx/config/BepInEx.cfg`.

## Repository layout

| Path | Contents |
|---|---|
| `VanilaMagic/` | The plugin. `ModConfig.cs` holds the config entries, `Items/` the wands, foods and armor, `DungeonRooms/` the Frost Cave mushroom rooms (see its README), `StatusEffects/` the status effects. |
| `VanilaMagic/Package/` | Thunderstore manifest and icon. |
| `VanilaMagic/Assets/` | Embedded resources: item icons, cape textures and the `vanilamagic` asset bundle. |
| `VanilaMagicUnity/` | Unity 6 (6000.0.75f1) stub project used to build the asset bundle. |
| `tools/` | Offline helper scripts (prefab dump, glTF export). |
| `art/` | Source art (wand renders, mod icon). |

## License

Code and original artwork: MIT, (c) 2026 Barnabur. See [LICENSE](LICENSE).
Valheim and its assets are (c) Iron Gate AB. Files derived from game assets are not covered by the license.

## Credits

Based on [JotunnModStub](https://github.com/Valheim-Modding/JotunnModStub) by the Valheim Modding Community.
