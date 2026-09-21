# Grzybki w lodowych jaskiniach — dokumentacja

Niebieskie grzybki dające eitr, spawnujące się w pokojach lodowych jaskiń (Frost Caves
i jaskinia Hildir). Wykorzystują **nieużywane waniliowe assety** gry: item `MushroomBlue`,
pickable `Pickable_Mushroom_blue` i ikonkę `mushroomblue` — dzięki temu mod nie
redystrybuuje żadnych assetów (EULA/Thunderstore). Przed publikacją można je podmienić
na własny model z bundla.

## Jak to działa

1. **`SetupBlueMushroom`** ([VanilaMagic.cs](../VanilaMagic.cs)) — na evencie
   `PrefabManager.OnVanillaPrefabsAvailable` (każda sesja, bez odpinania) nadaje
   waniliowemu itemowi statystyki jedzenia (25 eitr / 15 HP / 10 staminy / 20 min)
   i podpina tłumaczenia `$item_bluemushroom`.
2. **`MushroomCaveRooms`** — na evencie `DungeonManager.OnVanillaRoomsAvailable`:
   - **raz na proces gry**: dla każdego pokoju z `RoomMushroomSpots` klonuje waniliowy
     pokój (`PrefabManager.CreateClonedPrefab`), wstawia grzybki z komponentem
     `RandomSpawn` i rejestruje klon jako `CustomRoom` (`DungeonManager.AddCustomRoom`),
   - **każdą sesję**: wyłącza oryginały z puli generatora (`RoomData.m_enabled = false`)
     — wariant grzybkowy **zastępuje** oryginał, a rzadkość grzybków steruje wyłącznie
     `MushroomSpawnChance` (procenty 0–100, obecnie 75).
3. **`EnsureInZNetScene`** — nieużywane assety bywają niezarejestrowane; bez wpisu
   w ZNetScene grzybki nie wczytałyby się po powrocie w okolicę ani u innych graczy.

## Konfiguracja pozycji (`RoomMushroomSpots`)

```csharp
["nazwa_pokoju"] = new[]
{
    Spot(x, y, z),                       // grzybek stojący na geometrii pokoju
    Spot(x, y, z, "caverock_stairs"),    // grzybek podpięty pod obiekt z RandomSpawn
},
```

- Pozycje są **lokalne względem środka pokoju** (origin w centrum, Y = wysokość,
  `pos` w grze zwraca pozycję stóp gracza).
- **Parametr `parent`** podpina grzybka jako dziecko wskazanego obiektu — grzybek
  pojawia się wtedy tylko razem z nim (mechanika `RandomSpawn.m_childNetViews`).
  Konieczny dla grzybków stojących na półkach/skałkach, które same mają `RandomSpawn`
  (inaczej grzybek lewituje, gdy półka się nie wylosuje). Formy zapisu:
  - `"nazwa"` — obiekt o tej nazwie **najbliższy grzybkowi** (uwaga: liczy się odległość
    do pivota, a pivoty bywają daleko od geometrii; pewne przy nazwach unikalnych),
  - `"nazwa[n]"` — n-ty (od zera) obiekt o tej nazwie w całym pokoju, w kolejności
    hierarchii (tej samej, którą widać w zripowanym prefabie),
  - `"sciezka/do/obiektu[n]"` — pełna ścieżka od dzieci roota pokoju, `[n]` rozróżnia
    duplikaty wśród rodzeństwa.
  - Nazwy muszą być dokładne, łącznie z sufiksami typu ` (1)`.
- Zabezpieczenia: zła nazwa/ścieżka → ostrzeżenie w logu, grzybek zostaje pod pokojem.
  Rodzic z ZNetView jest odrzucany (zagnieżdżanie netview'ów dublowałoby spawny).
- Grzybek z rodzicem losuje się **podwójnie** (szansa rodzica × szansa grzybka).

## Komendy dev (wymagają `devcommands`)

| Komenda | Do czego |
|---|---|
| `mushroomroom [pokój]` | Spawnuje wariant pokoju w miejscu gracza (bez rotacji, wypisuje origin) — oględziny rozmieszczenia |
| `mushroomonly [pokój]` | Jak wyżej, ale wszystkie obiekty `RandomSpawn` ukryte — **lewitujący grzybek = brakuje mu rodzica**; podpięty znika razem z półką |
| `whichroom` | Nazwa pokoju, w którym stoisz, + Twoja lokalna pozycja w nim (uwzględnia rotację — gotowe koordynaty do słownika) |
| `nearspawns [zasięg]` | Lista obiektów z `RandomSpawn` w pobliżu + czy można pod nie podpinać |
| `blinkspawn [nazwa]` | Miga wskazanym/najbliższym obiektem z `RandomSpawn` — identyfikacja wzrokowa |

Workflow dodania grzybka: `mushroomroom pokój` → stań w wybranym miejscu → `whichroom`
→ przepisz lokalną pozycję do słownika. Jeśli miejsce jest na półce: `nearspawns` /
`blinkspawn` / zripowany prefab → dopisz `parent`. Weryfikacja: `mushroomonly pokój`.

## Pułapki (nie ruszać bez zrozumienia)

- **Nie instancjonować prefabów z ZNetView do załadowanych waniliowych assetów w trakcie
  gry** — `ZNetView.Awake` tworzy wtedy sieciowe obiekty-widmo i psuje `ZNetScene`
  (spam NRE w `RemoveObjects`). Dlatego pokoje modyfikujemy wyłącznie na klonach,
  które mieszkają w **nieaktywnym** kontenerze Jotunna (tam `Awake` się nie odpala).
- **`Room.Theme` to flagi bitowe** — większość pokoi frost cave jest współdzielona
  z jaskinią Hildir (`Cave|CaveHildir`). Porównywać przez `(theme & Cave) != 0`;
  wariant musi odziedziczyć pełne flagi oryginału (`customRoom.Room.m_theme = ...`),
  inaczej jaskinia Hildir straci wyłączone pokoje.
- **Pokoje to soft-referencje** (`DungeonDB.RoomData.m_prefab`) — przed klonowaniem
  `Load()` + `HoldReference()`, inaczej gra wyładuje meshe używane przez klon.
- **Dzieci pokoju muszą nazywać się dokładnie jak ich prefab** (bez `(Clone)`) —
  po tej nazwie ZNetView liczy hash do synchronizacji sieciowej.
- `RandomSpawn.m_chanceToSpawn` jest w **procentach 0–100** (nie 0–1).
- Zmiana puli pokoi wpływa na układ **istniejących** jaskiń przy ponownym wczytaniu
  (struktura regeneruje się z seeda w trybie Client) — testować na świeżych światach;
  docelowy świat zakładać z finalną konfiguracją moda.
- Nowe pokoje pojawiają się tylko w **nowo generowanych strefach**.

## Stan / TODO

- 33 pokoje, 61 pozycji grzybków; szansa 75% na grzybek.
- Rodzice podpięci m.in. w: crossroads01/02/03, crossroads01_ice, crossroads01_hole_ice,
  crossroads01_hole_long, deeproom_bottom_lake, icecorridor06/07.
- Przed publikacją: balans szansy, decyzja o komendach dev, ewentualnie własny model
  grzybka w bundlu (zamiast waniliowych assetów).
