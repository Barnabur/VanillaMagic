using System.Collections.Generic;
using System.Linq;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace VanilaMagic.DungeonRooms
{
    internal static class MushroomCaveRooms
    {
        private sealed class MushroomSpot
        {
            public Vector3 Pos;
            public string Parent;
        }

        // Pozycja grzybka w lokalnych koordynatach pokoju. Opcjonalny 'parent' to nazwa
        // obiektu w pokoju (np. półki z własnym RandomSpawn), pod który grzybek zostanie
        // podpięty - pojawi się wtedy tylko razem z nim. Nazwę znajdziesz komendą nearspawns
        // stojąc obok obiektu (najlepiej w pokoju zespawnowanym przez mushroomroom,
        // bo tam istnieją wszystkie obiekty, także te wylosowane "na nie").
        private static MushroomSpot Spot(float x, float y, float z, string parent = null)
        {
            return new MushroomSpot { Pos = new Vector3(x, y, z), Parent = parent };
        }

        private static readonly Dictionary<string, MushroomSpot[]> RoomMushroomSpots = new Dictionary<string, MushroomSpot[]>
        {
            ["cave_dome_bottom_lake"] = new[]
            {
                Spot(14.6f, -3.2f, 13.4f),
                Spot(14.6f, -3.5f, 14.9f),
                Spot(12.515f, -3.407f, 7.94f),
                Spot(-7.017f, -3.574f, 10.43f),
                Spot(7.014f, -3.546f, -2.16f),
                Spot(14.76f, -3.635f, -20.726f),
                Spot(-0.07f, -2.634f, -17.86f),
                Spot(-14.667f, -3.576f, -1.044f),
                Spot(-12.32f, -3.451f, 6.285f),
            },
            ["cave_new_corridor01"] = new[] { Spot(-2.015f, -3.3f, -3.04f) },
            ["cave_new_corridor03"] = new[] { Spot(-3.81f, -0.504f, -4.435f) },
            ["cave_new_corridor05"] = new[] { Spot(1.75f, -3.65f, 1.07f) },
            ["cave_new_corridor06"] = new[]
            {
                Spot(0.56f, -3.4f, 0.096f),
                Spot(-0.62f, -3.38f, -0.75f),
            },
            ["cave_new_corridor08"] = new[] { Spot(-0.75f, -3f, -0.25f, "caverock_rockbun (1)") },
            ["cave_new_corridor09"] = new[]
            {
                Spot(-8.88f, -3.27f, 1.04f),
                Spot(8.18f, -3.32f, 2.09f),
            },
            ["cave_new_crossroads01"] = new[]
            {
                Spot(1.68f, -2.65f, -1.06f, "caverock_stairs"),
                Spot(-0.36f, -2.1f, 2.26f, "caverock_stairs"),
                Spot(-2.63f, -2.34f, 0.61f),
            },
            ["cave_new_crossroads01_hole"] = new[] { Spot(-1.91f, -11.64f, 2.46f) },
            ["cave_new_crossroads01_hole_ice"] = new[] { Spot(-0.103f, -10.8f, 2.15f, "caverock_rockbun (1)") },
            ["cave_new_crossroads01_hole_long"] = new[]
            {
                Spot(3.98f, 5.76f, 3.188f),
                Spot(0.406f, -1.7f, -2.508f, "GameObject[0]"),
            },
            ["cave_new_crossroads01_hole_shrine"] = new[] { Spot(-3.512f, 5.667f, 3.437f) },
            ["cave_new_crossroads01_hole_to_deeproom"] = new[] { Spot(3.58f, -16.67f, -4.95f) },
            ["cave_new_crossroads01_ice"] = new[]
            {
                Spot(-0.64f, -2.35f, 2.488f, "caverock_stairs"),
                Spot(-0.55f, -2.32f, -1.056f, "caverock_stairs"),
            },
            ["cave_new_crossroads02"] = new[] { Spot(3.43f, -3.2f, 1.31f, "caverock_cornerwall (4)") },
            ["cave_new_crossroads03"] = new[]
            {
                Spot(3.188095f, -3.268894f, -0.4582596f, "caverock_cornerwall (6)"),
                Spot(2.887878f, -3.352459f, 0.9809723f, "caverock_cornerwall (4)"),
            },
            ["cave_new_deeproom_bottom"] = new[]
            {
                Spot(7.90453f, -2.216988f, -5.601007f),
                Spot(-7.904791f, -2.337124f, 5.846292f),
            },
            ["cave_new_deeproom_bottom_ice"] = new[]
            {
                Spot(7.90453f, -2.216988f, -5.601007f),
                Spot(-7.904791f, -2.337124f, 5.846292f),
            },
            ["cave_new_deeproom_bottom_lake"] = new[] { Spot(8.247894f, 3.166863f, 8.71106f, "caverock_rockbun (2)") },
            ["cave_new_deeproom_top"] = new[]
            {
                Spot(-6.902685f, 2.219505f, 3.483905f),
                Spot(2.879924f, 2.431755f, 3.316665f),
            },
            ["cave_new_icecorridor04"] = new[] { Spot(-2.047768f, -2.444617f, 1.13559f) },
            ["cave_new_icecorridor06"] = new[]
            {
                Spot(2.212841f, -2.298157f, -1.106262f, "caverock_stairs"),
                Spot(-0.5860672f, -2.364056f, 1.136902f, "caverock_stairs"),
            },
            ["cave_new_icecorridor07"] = new[] { Spot(0.986763f, -2.206398f, -0.5788574f, "caverock_rockbun (1)") },
            ["cave_new_sloperoom01"] = new[]
            {
                Spot(-1.920021f, -12.34991f, -10.22765f),
                Spot(8.075127f, -12.1036f, 3.989426f),
                Spot(-6.700142f, -0.58446f, -13.90933f),
            },
            ["cave_new_sloperoom02"] = new[]
            {
                Spot(-8.543877f, -1.156097f, 10.27493f),
                Spot(2.066998f, -12.13457f, -9.897949f),
                Spot(-6.9221f, -12.58266f, 10.16925f),
            },
            ["cave_new_sloperoom03"] = new[]
            {
                Spot(-7.685143f, -0.9111785f, -12.12992f),
                Spot(7.319664f, -12.54028f, 9.617445f),
            },
            ["cave_new_sloperoom04"] = new[]
            {
                Spot(-6.542713f, -11.25193f, -13.52265f),
                Spot(-7.143684f, -0.5564294f, 14.04569f),
            },
            ["cave_new_sloperoom05"] = new[]
            {
                Spot(-6.548431f, -1.309874f, -0.4031525f),
                Spot(9.577744f, -12.24919f, 21.01906f),
                Spot(4.832031f, -12.47983f, -21.93217f),
            },
            ["cave_new_sloperoom_w_hole"] = new[]
            {
                Spot(-8.421457f, -9.077934f, 11.27274f),
                Spot(8.315807f, -12.22492f, 2.263329f),
            },
            ["cave_shrine_start02"] = new[] { Spot(-2.223389f, -3.404904f, -3.837372f) },
            ["cave_shrine_start02_corridor"] = new[] { Spot(1.602539f, -3.521391f, -4.902924f) },
            ["cave_shrine_start02_ice"] = new[] { Spot(-2.140106f, -3.310226f, -7.95816f) },
            ["cave_shrine_start02_ice_corridor"] = new[] { Spot(1.096588f, -3.297759f, -9.565338f) },
        };

        // Szansa w PROCENTACH (0-100), że pojedynczy grzybek wyrośnie w wygenerowanym pokoju.
        // Grzybek podpięty pod obiekt z własnym RandomSpawn losuje się PODWÓJNIE
        // (najpierw rodzic, potem grzybek), więc efektywna szansa jest niższa.
        private const float MushroomSpawnChance = 75f;

        internal static IEnumerable<string> RoomNames => RoomMushroomSpots.Keys;

        // Nazwa prefabu grzybka - ustawiana przy rejestracji, używana przez komendy testowe
        internal static string MushroomName { get; private set; }

        private static bool roomsAdded;

        public static void AddMushroomRooms(GameObject mushroomPrefab)
        {
            if (!mushroomPrefab)
            {
                Jotunn.Logger.LogWarning("Brak prefabu grzybka - pomijam dodawanie pokoi z grzybkami");
                return;
            }

            MushroomName = mushroomPrefab.name;

            // Rejestracja wariantów tylko raz na proces gry - Jotunn sam wpina
            // już zarejestrowane pokoje w kolejnych sesjach
            if (!roomsAdded)
            {
                // Lista wszystkich pokoi lodowych jaskiń - z niej wybierz nazwy do RoomMushroomSpots.
                // Uwaga: m_theme to flagi - część pokoi jest współdzielona z jaskinią Hildir (Cave|CaveHildir)
                foreach (var r in DungeonDB.instance.m_rooms.Where(r => (r.m_theme & Room.Theme.Cave) != 0))
                {
                    Jotunn.Logger.LogInfo($"Cave room: {r.m_prefab.Name} (theme: {r.m_theme})");
                }

                EnsureInZNetScene(mushroomPrefab);

                foreach (var pair in RoomMushroomSpots)
                {
                    AddMushroomVariant(pair.Key, pair.Value, mushroomPrefab);
                }

                roomsAdded = true;
            }

            DisableReplacedVanillaRooms();
        }

        private static void EnsureInZNetScene(GameObject prefab)
        {
            // Nieużywane waniliowe assety bywają niezarejestrowane - bez wpisu w ZNetScene
            // grzybki nie wczytałyby się po powrocie w okolicę ani u innych graczy
            int hash = prefab.name.GetStableHashCode();
            if (ZNetScene.instance && !ZNetScene.instance.GetPrefab(hash))
            {
                // do bieżącej sesji wprost, do kolejnych przez Jotunna
                ZNetScene.instance.m_prefabs.Add(prefab);
                ZNetScene.instance.m_namedPrefabs.Add(hash, prefab);
                PrefabManager.Instance.AddPrefab(new CustomPrefab(prefab, false));
                Jotunn.Logger.LogInfo($"Zarejestrowano {prefab.name} w ZNetScene");
            }
        }

        private static void DisableReplacedVanillaRooms()
        {
            // Wariant grzybkowy ZASTĘPUJE oryginał, a rzadkość grzybków steruje RandomSpawn.
            // Wyłączamy oryginały z puli generatora przy każdym wejściu do świata,
            // bo DungeonDB może odtworzyć listę pokoi między sesjami.
            int disabled = 0;
            foreach (var roomData in DungeonDB.instance.m_rooms)
            {
                if ((roomData.m_theme & Room.Theme.Cave) != 0
                    && roomData.m_enabled
                    && RoomMushroomSpots.ContainsKey(roomData.m_prefab.Name)
                    && DungeonManager.Instance.GetRoom(roomData.m_prefab.Name + "_mushroom") != null)
                {
                    roomData.m_enabled = false;
                    disabled++;
                }
            }

            if (disabled > 0)
            {
                Jotunn.Logger.LogInfo($"Wyłączono {disabled} oryginalnych pokoi zastąpionych wariantami z grzybkami");
            }
        }

        private static void AddMushroomVariant(string roomName, MushroomSpot[] spots, GameObject mushroomPrefab)
        {
            var vanillaData = DungeonDB.instance.m_rooms.FirstOrDefault(r =>
                (r.m_theme & Room.Theme.Cave) != 0 && r.m_prefab.Name == roomName);
            if (vanillaData == null)
            {
                Jotunn.Logger.LogWarning($"Nie znaleziono pokoju {roomName} - pomijam");
                return;
            }

            // Pokoje to soft-referencje - trzeba załadować i przytrzymać referencję,
            // żeby gra nie wyładowała meshy/materiałów, z których korzysta klon
            vanillaData.m_prefab.Load();
            vanillaData.m_prefab.HoldReference();

            var clone = PrefabManager.Instance.CreateClonedPrefab(roomName + "_mushroom", vanillaData.m_prefab.Asset);

            foreach (var spot in spots)
            {
                // Klon mieszka w NIEAKTYWNYM kontenerze Jotunna, więc Awake na ZNetView
                // grzybka się nie odpala - nie wolno wstawiać dzieci z ZNetView do aktywnych
                // assetów w trakcie gry, bo tworzą sieciowe obiekty-widmo i psują ZNetScene
                var mushroom = Object.Instantiate(mushroomPrefab, clone.transform);
                mushroom.transform.localPosition = spot.Pos;
                mushroom.name = mushroomPrefab.name; // bez "(Clone)", inaczej ZNetView nie dopasuje prefabu

                if (!string.IsNullOrEmpty(spot.Parent))
                {
                    AttachToParent(mushroom, spot.Parent, clone, roomName);
                }

                var randomSpawn = mushroom.AddComponent<RandomSpawn>();
                randomSpawn.m_chanceToSpawn = MushroomSpawnChance;
            }

            // Nieustawione pola RoomConfig zostają jak w klonowanym pokoju (waga, endcap itd.),
            // więc wariant wchodzi do generatora dokładnie w miejsce wyłączonego oryginału
            var config = new RoomConfig(Room.Theme.Cave.ToString());
            var customRoom = new CustomRoom(clone, false, config);
            DungeonManager.Instance.AddCustomRoom(customRoom);

            // RoomConfig przyjmuje tylko pojedynczy motyw, a część pokoi jest współdzielona
            // z jaskinią Hildir (flagi Cave|CaveHildir) - przywracamy pełne flagi oryginału,
            // żeby wariant zastąpił go we wszystkich rodzajach jaskiń
            customRoom.Room.m_theme = vanillaData.m_theme;
            if (customRoom.RoomData != null)
            {
                customRoom.RoomData.m_theme = vanillaData.m_theme;
            }

            Jotunn.Logger.LogInfo($"Dodano wariant pokoju {roomName} z grzybkami (theme: {vanillaData.m_theme})");
        }

        private static void AttachToParent(GameObject mushroom, string parentName, GameObject roomClone, string roomName)
        {
            Transform bestParent = null;

            if (parentName.Contains("/"))
            {
                // Pełna ścieżka w hierarchii pokoju - jednoznaczna; polecana, gdy obiekt ma
                // generyczną nazwę jak "GameObject". Ścieżkę odczytasz z hierarchii prefabu
                // w zripowanych assetach (bez węzła pokoju). Duplikaty nazw wśród rodzeństwa
                // rozróżnia indeks liczony od zera, np. "GameObject[1]" = drugi z kolei.
                bestParent = ResolvePath(roomClone.transform, parentName);
            }
            else if (parentName.Contains("["))
            {
                // "nazwa[n]" bez ścieżki: n-ty (od zera) obiekt o tej nazwie w całym pokoju,
                // licząc w kolejności hierarchii - tej samej, którą widać w zripowanym prefabie
                bestParent = FindDescendantByIndex(roomClone.transform, parentName);
            }
            else
            {
                // Sama nazwa: bierzemy obiekt najbliższy pozycji grzybka. Uwaga: pivot obiektu
                // bywa daleko od jego widocznej geometrii - przy duplikatach nazw pewniejsza
                // jest pełna ścieżka.
                float bestDistance = float.MaxValue;
                foreach (var t in roomClone.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != parentName || t == mushroom.transform)
                    {
                        continue;
                    }

                    float distance = Vector3.Distance(t.position, mushroom.transform.position);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestParent = t;
                    }
                }
            }

            if (bestParent == null)
            {
                Jotunn.Logger.LogWarning($"{roomName}: nie znaleziono rodzica '{parentName}' - grzybek zostaje pod pokojem");
                return;
            }

            // Rodzic z ZNetView spawnuje się jako osobny obiekt świata - zagnieżdżenie grzybka
            // w nim zdublowałoby spawn i zepsuło księgowość sieciową. Podpinać można tylko
            // pod dekoracje bez ZNetView (RandomSpawn gasi wtedy grzybka razem z rodzicem).
            if (bestParent.GetComponentInParent<ZNetView>() != null)
            {
                Jotunn.Logger.LogWarning($"{roomName}: rodzic '{parentName}' ma ZNetView - grzybek zostaje pod pokojem");
                return;
            }

            // worldPositionStays: pozycja grzybka względem pokoju zostaje bez zmian
            mushroom.transform.SetParent(bestParent, true);
        }

        private static Transform FindDescendantByIndex(Transform root, string nameWithIndex)
        {
            int bracket = nameWithIndex.IndexOf('[');
            if (bracket < 0 || !nameWithIndex.EndsWith("]"))
            {
                return null;
            }

            string name = nameWithIndex.Substring(0, bracket);
            if (!int.TryParse(nameWithIndex.Substring(bracket + 1, nameWithIndex.Length - bracket - 2), out int index))
            {
                return null;
            }

            int seen = 0;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != name)
                {
                    continue;
                }

                if (seen == index)
                {
                    return t;
                }
                seen++;
            }

            return null;
        }

        private static Transform ResolvePath(Transform root, string path)
        {
            Transform current = root;
            foreach (var rawSegment in path.Split('/'))
            {
                string segment = rawSegment;
                int index = 0;

                // składnia "nazwa[n]" - n-ty (od zera) obiekt o tej nazwie wśród rodzeństwa
                int bracket = segment.IndexOf('[');
                if (bracket >= 0 && segment.EndsWith("]"))
                {
                    int.TryParse(segment.Substring(bracket + 1, segment.Length - bracket - 2), out index);
                    segment = segment.Substring(0, bracket);
                }

                Transform next = null;
                int seen = 0;
                for (int i = 0; i < current.childCount; i++)
                {
                    var child = current.GetChild(i);
                    if (child.name != segment)
                    {
                        continue;
                    }

                    if (seen == index)
                    {
                        next = child;
                        break;
                    }
                    seen++;
                }

                if (next == null)
                {
                    return null;
                }
                current = next;
            }

            return current;
        }
    }
}
