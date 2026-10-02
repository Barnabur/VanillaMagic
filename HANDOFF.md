# VanilaMagic — handoff

Stan na 2026-09-27 (peleryna/zbroja), reszta 2026-09-22. Dokument dla nowej sesji: co to jest, jak to zbudować, co jest zrobione,
co zostało i na czym można się przejechać.

---

## 1. Gdzie co leży

| co | ścieżka |
|---|---|
| **aktywny projekt** | `C:\Users\kamil\source\2\VanilaMagic` |
| stub Unity (bundle własnych modeli) | `VanilaMagicUnity` |
| zamrożony snapshot starego moda | `C:\Users\kamil\source\2\test2` — **nie rozwijać** |
| waniliowe źródła do malowania | `C:\Users\kamil\source\2\vanilla_source` — **celowo poza repo** |
| rip assetów gry | `E:\ValheimRIP_2026-09` (AssetRipper, Unity 6) |
| gra | `C:\Program Files (x86)\Steam\steamapps\common\Valheim` |

Mod: Jotunn **2.30.2**, Unity **6000.0.75f1**, BepInEx 5.4.22.
`PluginGUID = "com.barnabur.vanillamagic"`, deploy do `BepInEx/plugins/VanilaMagic/`.
Stary `JotunnModStub` i `Jotunn_2.24.3.dll` siedzą w `BepInEx/plugins_disabled/`.

## 2. Build i test

```bash
dotnet build VanilaMagic.sln -c Debug
```

Post-build sam kopiuje DLL do `BepInEx/plugins/VanilaMagic`. **Buduj solucję, nie .csproj** —
przy samym projekcie skrypt publikujący dostaje pusty `$(SolutionDir)` i wywala się.
Ostrzeżenie `MSB3245 UnityEngine.ProfilerModule` jest stałe i nieszkodliwe.

Po zmianie itemów potrzebny **pełny restart gry** — składają się raz, przy wejściu do świata
(`PrefabManager.OnVanillaPrefabsAvailable`, handler odpina się po pierwszym przebiegu).

W grze: `devcommands`, potem `spawn <PrefabName> 1`.

Log: `BepInEx/LogOutput.log`
```bash
grep -E "RenderedIcons|CrystalSugar|SugarFoods|ClothChain|WraithArmor|LeveledRequirements" LogOutput.log
```

## 3. Zawartość moda

Rejestracja w `VanilaMagic.cs → Awake()`. **Kolejność ma znaczenie**: `WildBerry` przed
`GhostShake` (składnik receptury) i przed `HealingStaff` (SE leczenia bierze stamtąd ikonę).

### Różdżki — kitbash z żywych waniliowych assetów
| item | baza | obrażenia | eitr | receptura |
|---|---|---|---|---|
| `SurtlingWand` | StaffIceShards | fire 10 (+6/lvl) | — | Bronze 7 + SurtlingCore 1, kuźnia |
| `FrostWand` | StaffFireball | frost 30 (+5/lvl) ⚠ | 25 ⚠ | Silver 5 + Crystal 1 ⚠ |
| `StoneWand` | StaffFireball | blunt 45 ⚠ | 25 ⚠ | BlackMetal 5 + Obsidian 4 + FineWood 5 ⚠ |
| `HealingStaff` | StaffShield | — (AoE heal) | 20 | Bronze 7 + SurtlingCore 1, kuźnia lvl 4 |

⚠ = placeholder do ustalenia z Kamilem.

### Jedzenie i surowce
| item | jak się zdobywa | sytość / stamina / eitr |
|---|---|---|
| `Wildberry` | krzak `WildberryBush`, wegetacja Czarnego Lasu | 5 / 5 / 15 |
| `MushroomBlue` (waniliowy, przerobiony) | pokoje frost cave | 15 / 10 / 25 |
| `GhostShake` | kocioł lvl 2: Wildberry 4 + Ectoplasm 1 | 16 / 20 / 40 |
| `CrystalSugar` | **wiatrak**: Crystal → CrystalSugar 1:1 | surowiec |
| `EyescreamSprinkles` | kocioł lvl 3: GreydwarfEye 3 + FreezeGland 1 + cukier 1 | 16 / 60 / 10 |
| `Sweetbread` | piec z waniliowego Unbaked Sweetbread | 43 / 43 / 0 |
| `FrostedSweetbreadUncooked` | stół kuchenny: Unbaked Sweetbread 1 + cukier 1 | jak waniliowy półprodukt |
| `VikingCupcake` (waniliowy, przerobiony) | piec z powyższego | 33 / 33 / **20** |

Ektoplazma: `EctoplasmDrops` przenosi ją na bagna — Wraith dostaje 1–2 @100%,
Ghostowi ścięte z 1–5 na 1–2.

### Zbroja
`WraithArmor` + `ClothChain` + `DanglingChain` + `LeveledRequirements` (patch Harmony na
`Piece.Requirement.GetAmount`, koszty ulepszeń rozłożone na biomy). Pancerz 8 +3/lvl ⚠.

### Pokoje
`MushroomCaveRooms` — warianty pokoi frost cave z niebieskimi grzybkami.
Pełna dokumentacja w `VanilaMagic/DungeonRooms/README.md`.

### Komendy dev
`wandicon` (kadr ikon różdżek), `wraithtint` (kolory zbroi), `dumpstaff`,
`mushroomroom`, `whichroom`, `nearspawns`, `blinkspawn`, `mushroomonly`.

## 4. Co zostało

1. **Zbroja i peleryna** — stan 2026-09-27: ZAAKCEPTOWANE przez Kamila („jest perfekcyjnie").
   - Tekstura peleryny = piksele Fenrisa 1:1 (128 px, NEAREST x8, `filterMode Point` jak FenringArmor_d): generator
     `vanilla_source/peleryny/fenris_uv/make_fenris_cape.py`, w DLL wariant **I2** (jaśniej x1.2, niebieskie barki).
   - Połysk: ciało gracza ma `_Glossiness` 0.2 -> `WraithMatteBody.cs` ustawia `WraithArmor.Gloss`=0.03 przy szacie/nogawicach.
   - Kaptur: `WraithHood_mesh` z bundla = FenringHood z kołnierzem odsuniętym nad pelerynę (CapeLab.PushHoodOverCape).
     Rozchylenie kołnierza (flare) wycofane – psuło krańce.
   - Peleryna: `WraithCape_mesh` z bundla = cape2 z wagami kołnierza kaptura w górnej części + `WraithCapeCloth.PinTopToHood`
     (wiersze do 45 stałe, bez fizyki; prebuild MagicaCloth wyłączony, tkanina budowana w runtime). Parametry tkaniny w
     `WraithCapeCloth` (radius 0.03->0.08, angle 0.05, gravity 7, damping 0.2, maxdist 1), na żywo `wraithtint capecloth ...`.
   - Z Kapturem Widma góra peleryny (wiersze 0..25) niewidzialna – `WraithHoodedCape.cs`, `wraithtint capecut N`.
   - Podgląd offline: `E:\ValheimRIP_2026-09\ValheimRIP\Assets\Editor\CapeLab\CapeLab.cs` (MCP unity-rip), zrzut z gry
     `wraithtint capedump` -> `CapeLab.LoadDump`. Szczegóły i pułapki: pamięć `valheim-cape-magicacloth-and-rip-render`.
   - Bundle: `Unity.exe -batchmode -quit -projectPath VanilaMagicUnity -executeMethod BuildVanilaMagicBundle.Build`.
2. **Balans** — wartości oznaczone ⚠ powyżej. Kamil chciał tabelę wszystkiego naraz
   (obrażenia, eitr, receptury, koszty ulepszeń per poziom, jedzenie) z odniesieniem do
   waniliowych odpowiedników z danego etapu gry, żeby wpisać docelowe liczby w jednym miejscu.
3. Drobne: nazwa wyświetlana `Unbaked Frosted Sweetbread`, opisy „Lulz" w kilku itemach,
   `.gitignore`, potwierdzenie czy `WildBerry.png` to własna grafika przed publikacją.

## 5. Pułapki — rzeczy, które już raz kosztowały iterację

**Nazwy prefabów ≠ nazwy w grze.** „Frosted Sweetbread" to prefab `VikingCupcake`,
„Muckshake" to `ShocklateSmoothie` (przez **L**, ale jego model i materiał przez **O**:
`ShockolateSmoothie`). Eyescream: item `Eyescream`, model `EyesCream`. Zanim uznasz, że
czegoś w wanilii nie ma — **szukaj po nazwie wyświetlanej w CSV lokalizacji**, nie po nazwie
prefabu. CSV siedzi w `resources.assets` jako TextAsset `localization`.

**Assety z ripa nie trafiają do moda.** Zripowane shadery są martwe w runtime i **dublują
nazwy** prawdziwych shaderów gry, więc lookup po nazwie potrafi trafić w trupa. Z bundla
własnego bierzemy **tylko siatki i albedo**; materiał zawsze jest klonem żywego waniliowego.

**Bundle własnych modeli** (`VanilaMagic/Assets/vanilamagic`, budowany z menu
`VanilaMagic > Build asset bundle` w `VanilaMagicUnity`) ładuje się przez wczytanie całego
zasobu do pamięci — **nie** przez `AssetUtils.LoadAssetBundleFromResources`, bo tamto zamyka
strumień i gra crashuje przy kolejnym `LoadAsset`. API: `ModAssets.ApplyModel` / `ApplyAlbedo`.

**Ikony renderowane z modelu** (`RenderedIcons`): render trzeba wołać **po `BuildVisual`**,
nie po `AddItem` — inaczej łapie gołego klona bazy. Modele różdżek leżą **wzdłuż osi Z**, więc
obrót o 180° po Z kręci je wokół własnej długości i nie zmienia kierunku; do przerzucenia
głowicy trzeba osi prostopadłej. `UseCache` zostaje **false** — Jotunn trzyma w kluczu cache'a
globalną rewizję, a nie ustawienia kadru, więc z cache'em `wandicon` cicho oddaje stare obrazki.
`EnqueueRender` jest w 2.30.2 przestarzałe, używać `Render`.

**Tint materiału zapala cały obiekt.** Worek cukru i jego zawartość to jedna siatka, jedna
podsiatka, jeden materiał — rozdziela się je **w teksturze**, przemalowując tylko prostokąt UV
danej wysepki atlasu (`GrainUv` w `CrystalSugar`). Punktówka musi być **nad** obiektem, nie
w środku, inaczej świeci go od wewnątrz.

**Jedzenie wymaga `m_food > 0`** — `Player.ConsumeItem` woła `EatFood` tylko wtedy; przy zerze
przedmiot znika bez żadnego efektu, również bez staminy i eitru.

**Piec bierze jeden item.** `CookingStation.ItemConversion` to `m_from` / `m_to` / `m_cookTime`
— „ciasto + cukier" wymaga osobnego półproduktu. Jotunn umie konwersje tylko **dodawać**;
podmiana waniliowej to ręczna edycja `m_conversion` na prefabie `piece_oven`.

**Patche wanilii reaplikować co wejście do świata** (prefaby bywają przeładowane) i pisać je
idempotentnie. Itemy odwrotnie — tworzyć raz i odpiąć handler.

## 6. Narzędzia

- Render assetów z ripu bez gry: MCP `unity-rip` + `Unity_RunCommand` (materiały ripu mają martwy shader,
  tekstury czytać z `SerializedObject(mat)...m_TexEnvs`; siatki zbroi ×100, gracz z `Player.prefab`,
  `smr.bones = body.bones`). Przykłady w pamięci sesji (`valheim-cape-magicacloth-and-rip-render`).
- `tools/dump_prefab.py <Nazwa>` — hierarchia i pola waniliowego prefabu wprost z bundli
  (UnityPy, bez odpalania gry). Bundle itemów: `c4210710`, ikony: `6a33a62`,
  zawartość Bog Witch: `b8689a71`. Który bundle trzyma dany asset — `SoftRef/manifest_extended`.
- Ikony waniliowe: Sprite 64×64 w `6a33a62`, eksport przez `obj.read().image.save()`.
  **Unity liczy piksele od lewego dolnego rogu, PIL od górnego** — przy progach zależnych
  od `y` trzeba odwrócić obraz przed obróbką i po niej.
- Dekompilacja IL (np. żeby sprawdzić API Jotunna): Mono.Cecil w jednym wywołaniu PowerShell,
  DLL w `VanilaMagic/bin/Debug/net48`.
- Własne ikony: PNG 64×64 RGBA w `VanilaMagic/Assets/`, wpięte jako `EmbeddedResource`
  w csproj, ładowane przez `RuntimeTextures.LoadIcon(nazwa)`.
  **Konwencja: nazwa pliku = nazwa prefabu** (stąd `VikingCupcake.png` dla ikony,
  którą podmieniamy waniliowemu itemowi).

## 7. Jak pracować z Kamilem

Iteruje na wyglądzie bardzo szybko i ocenia ze screenów. Opłaca się:
- pokazywać warianty **wyrenderowane offline** (UnityPy + PIL, arkusz porównawczy,
  powiększenie NEAREST ×5) zamiast kazać odpalać grę,
- dodawać komendy konsoli do strojenia na żywo (`wandicon`, `wraithtint`) — inaczej każda
  próba to przebudowa i restart,
- sprawdzać fakty w danych gry zamiast zgadywać; kilka razy myliłem się właśnie tam, gdzie
  nie sprawdziłem (nazwy prefabów, istnienie itemu w wanilii).

Grafikę robi sam — od algorytmicznych przemalowań odszedł („wygląda tragicznie"). Rola
sesji: wyciągnąć waniliowe źródła do malowania i podpiąć gotowe pliki.
