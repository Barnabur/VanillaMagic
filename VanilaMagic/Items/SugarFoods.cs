using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Jedzenie na krysztalowym cukrze (patrz [CrystalSugar]).
    ///
    /// Eyescream with Sprinkles: receptura Eyescreama + 1 cukier (21/65/0 -> 16/60/10).
    /// Modele obu wersji rozroznia obecnosc bryl oczu: z posypka = waniliowy model (oczy sa),
    /// zwykly = waniliowy prefab z wygaszonymi oczami.
    ///
    /// Sweetbread: wanilia piecze Unbaked Sweetbread od razu na lukrowana bulke, my wpinamy
    /// sie w ten lancuch. UWAGA na nazwy: waniliowe prefaby to VikingCupcake (w grze
    /// "Frosted Sweetbread") i VikingCupcakeUncooked ("Unbaked Sweetbread") - stad szukanie
    /// po "sweetbread" w nazwach prefabow nic nie daje.
    ///   Unbaked Sweetbread  --piec-->  Sweetbread              (nowy item, staty waniliowej bulki)
    ///   Unbaked Sweetbread + cukier  --piec-->  Frosted Sweetbread  (waniliowy VikingCupcake, +20 eitr / -10 reszta)
    /// Waniliowa konwersja pieca jest przepinana na nowy Sweetbread, a lukrowana wersja
    /// dostaje wlasny polprodukt (FrostedSweetbreadUncooked).
    ///
    /// Ikony sa wlasne (Assets/*.png), lacznie z podmienionymi ikonami DWOCH waniliowych itemow:
    /// Eyescreama (zeby odroznic go od wersji z posypka) i Frosted Sweetbreada (lukier to teraz
    /// krysztalowy cukier). Waniliowy SUROWY sweetbread zostaje nietkniety - wlasna ikone ma
    /// tylko nasz polprodukt z cukrem. Model zwyklego Sweetbreada: klon lukrowanego z meshem
    /// VikingCupcake_body_mesh (zdjety lukier i orzechy, wierzch z naciecien X jak chleb) i albedo
    /// VikingCupcake_body_D z bundla "vanilamagic" (ModAssets). Bez bundla zostaje fallback:
    /// waniliowy model z tekstura przemalowana w runtime (Unfrost).
    /// </summary>
    internal static class SugarFoods
    {
        public const string SprinkledEyescreamName = "EyescreamSprinkles";
        public const string SweetbreadName = "Sweetbread";
        public const string FrostedUncookedName = "FrostedSweetbreadUncooked";

        private const string VanillaEyescream = "Eyescream";
        private const string VanillaFrosted = "VikingCupcake";
        private const string VanillaUncooked = "VikingCupcakeUncooked";

        private const string PrepTable = "piece_preptable";
        private const string Cauldron = "piece_cauldron";
        private const float OvenCookTime = 50f;

        // Bryly oczu greydwarfa w modelu Eyescreama (osobne dziecko pod "attach"
        // i pod "equipoffset", wlasny material greydwarfeyemat)
        private const string EyesObject = "eyes";
        private const string CreamObject = "cream";

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += Create;
            // Patche waniliowych prefabow zostaja podpiete - prefaby moga byc przeladowane miedzy sesjami
            PrefabManager.OnVanillaPrefabsAvailable += PatchVanilla;

            // Lukrowana wersja piecze sie z wlasnego polproduktu; zwykly Sweetbread bierze
            // przepiete waniliowe wejscie (patrz RewireOven).
            ItemManager.Instance.AddItemConversion(new CustomItemConversion(new CookingConversionConfig
            {
                Station = CookingStations.StoneOven,
                FromItem = FrostedUncookedName,
                ToItem = VanillaFrosted,
                CookTime = OvenCookTime,
            }));
        }

        private static void Create()
        {
            // Itemy tworzymy tylko raz (event odpala sie przy kazdym wejsciu do main scene)
            PrefabManager.OnVanillaPrefabsAvailable -= Create;
            try
            {
                BuildSprinkledEyescream();
                BuildSweetbread();
                BuildFrostedUncooked();
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"SugarFoods: skladanie itemow nie powiodlo sie: {ex}");
            }
        }

        private static void PatchVanilla()
        {
            try
            {
                ReplaceVanillaIcon(VanillaEyescream);
                RemoveVanillaEyes();
                ReplaceVanillaIcon(VanillaFrosted);
                NerfVanillaFrosted();
                RecolorVanillaFrostedSprinkles();
                RewireOven();
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"SugarFoods: przepiecie waniliowych prefabow nie powiodlo sie: {ex}");
            }
        }

        // ------------------------------------------------------------------ itemy

        /// <summary>
        /// Eyescream + posypka: ta sama receptura co waniliowa, doszedl 1 cukier.
        /// Model i tak juz wyglada jak lody z posypka - zostaje bez zmian, a to waniliowy
        /// traci oczy (RemoveVanillaEyes), zeby te dwa dalo sie odroznic.
        /// </summary>
        private static void BuildSprinkledEyescream()
        {
            var config = new ItemConfig
            {
                Name = "$item_eyescreamsprinkles",
                Description = "$item_eyescreamsprinkles_description",
                CraftingStation = Cauldron,
                MinStationLevel = 3,
                Requirements = new[]
                {
                    new RequirementConfig("GreydwarfEye", 3),
                    new RequirementConfig("FreezeGland", 1),
                    new RequirementConfig(CrystalSugar.ItemName, 1),
                },
            };

            var item = new CustomItem(SprinkledEyescreamName, VanillaEyescream, config);
            SetFood(item, 16f, 60f, 10f); // Eyescream: 21 / 65 / 0

            SetIcon(item, RuntimeTextures.LoadIcon(SprinkledEyescreamName));
            // Model zostaje waniliowy - to wlasnie bryly oczu graja tu role posypki.
            // Wlaczamy je jawnie, bo waniliowy prefab (z ktorego klonujemy) je traci.
            SetEyesActive(item.ItemPrefab, true);

            ItemManager.Instance.AddItem(item);
        }

        /// <summary>
        /// Zwykla bulka: staty waniliowej lukrowanej (43/43/0) zostaja bez zmian - to ten sam
        /// wypiek, tylko bez lukru. Receptury nie ma, wychodzi wylacznie z pieca (RewireOven).
        /// </summary>
        private static void BuildSweetbread()
        {
            var item = new CustomItem(SweetbreadName, VanillaFrosted);
            var shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_name = "$item_sweetbread";
            shared.m_description = "$item_sweetbread_description";

            SetIcon(item, RuntimeTextures.LoadIcon(SweetbreadName));
            if (!ModAssets.ApplyModel(item.ItemPrefab, "VikingCupcake_body_mesh", "VikingCupcake_body_D"))
            {
                Jotunn.Logger.LogWarning("SugarFoods: brak modelu Sweetbreada w bundlu - zostaje przemalowana wanilia");
                RecolorModel(item.ItemPrefab, Unfrost, "_plain");
            }

            ItemManager.Instance.AddItem(item);
        }

        /// <summary>
        /// Polprodukt pod lukier. Istnieje TYLKO dlatego, ze CookingStation.ItemConversion to
        /// m_from -> m_to (jedno wejscie, jedno wyjscie) - piec nie umie wziac ciasta i cukru
        /// naraz, wiec cukier trzeba wmieszac wczesniej i rozroznic wsad osobnym itemem.
        /// </summary>
        private static void BuildFrostedUncooked()
        {
            var config = new ItemConfig
            {
                Name = "$item_frostedsweetbread_uncooked",
                Description = "$item_frostedsweetbread_uncooked_description",
                CraftingStation = PrepTable,
                Requirements = new[]
                {
                    new RequirementConfig(VanillaUncooked, 1),
                    new RequirementConfig(CrystalSugar.ItemName, 1),
                },
            };

            var item = new CustomItem(FrostedUncookedName, VanillaUncooked, config);
            // Staty i model zostaja waniliowe; wlasna jest tylko ikona (ciasto obsypane cukrem).
            // WANILIOWY surowy sweetbread nie jest tu ruszany - to osobny, nietkniety item.
            SetIcon(item, RuntimeTextures.LoadIcon(FrostedUncookedName));

            ItemManager.Instance.AddItem(item);
        }

        // ------------------------------------------------------------------ patche wanilii

        /// <summary>
        /// Podmienia ikone waniliowego itemu na wlasna z Assets/&lt;nazwa prefabu&gt;.png.
        /// Eyescream - zeby dalo sie go odroznic od wersji z posypka; Frosted Sweetbread -
        /// bo lukier to teraz krysztalowy cukier, a nie waniliowe pomaranczowe okruchy.
        /// </summary>
        private static void ReplaceVanillaIcon(string prefabName)
        {
            var icon = RuntimeTextures.LoadIcon(prefabName);
            if (!icon) return;

            var shared = VanillaShared(prefabName);
            if (shared != null) shared.m_icons = new[] { icon };
        }

        /// <summary>
        /// Zwykly Eyescream traci bryly oczu - zostaje sam rozek ze smietana, tak jak na
        /// nowej ikonie. Oczy to osobne dzieci prefabu z wlasnym materialem, wiec wystarczy
        /// je wygasic; wersja z posypka (klon) trzyma swoje wlaczone.
        /// </summary>
        private static void RemoveVanillaEyes()
        {
            var prefab = PrefabManager.Instance.GetPrefab(VanillaEyescream);
            if (!prefab)
            {
                Jotunn.Logger.LogWarning($"SugarFoods: brak waniliowego prefabu {VanillaEyescream}");
                return;
            }

            if (SetEyesActive(prefab, false) == 0)
            {
                Jotunn.Logger.LogWarning($"SugarFoods: nie znalazlem dzieci \"{EyesObject}\" w {VanillaEyescream}");
            }
        }

        /// <summary>Wlacza/wygasza wszystkie bryly oczu w prefabie; zwraca ile ich bylo.</summary>
        private static int SetEyesActive(GameObject prefab, bool active)
        {
            var found = 0;
            foreach (var child in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (!child.name.StartsWith(EyesObject, StringComparison.Ordinal)) continue;
                found++;

                // Prefab ma dwa komplety bryl: aktywny pod "attach" i zgaszony dubel pod
                // "equipoffset". Poznajemy je po rodzenstwie - obok zgaszonych oczu lezy
                // zgaszona smietana. Tego dubla nie budzimy, bo wanilia go nie budzi.
                var cream = child.parent ? child.parent.Find(CreamObject) : null;
                if (active && cream && !cream.gameObject.activeSelf) continue;

                child.gameObject.SetActive(active);
            }
            return found;
        }

        /// <summary>
        /// Trzy "orzechy" na lukrze waniliowego Frosted Sweetbreada staja sie niebieskimi krysztalkami
        /// cukru - jak na podmienionej ikonie. Albedo VikingCupcake_frosted_D z bundla (wanilia z
        /// przemalowanym regionem UV orzechow), mesh i shader zostaja waniliowe.
        /// </summary>
        private static void RecolorVanillaFrostedSprinkles()
        {
            var prefab = PrefabManager.Instance.GetPrefab(VanillaFrosted);
            if (!prefab)
            {
                Jotunn.Logger.LogWarning($"SugarFoods: brak waniliowego prefabu {VanillaFrosted}");
                return;
            }
            if (!ModAssets.ApplyAlbedo(prefab, "VikingCupcake_frosted_D"))
            {
                Jotunn.Logger.LogWarning("SugarFoods: brak tekstury krysztalkow w bundlu - orzechy zostaja waniliowe");
            }
        }

        /// <summary>Lukier to teraz cukier krysztalowy: +20 eitr kosztem 10 pozostalych statow.</summary>
        private static void NerfVanillaFrosted()
        {
            var shared = VanillaShared(VanillaFrosted);
            if (shared == null) return;

            shared.m_food = 33f;        // wanilia 43
            shared.m_foodStamina = 33f; // wanilia 43
            shared.m_foodEitr = 20f;    // wanilia 0
        }

        /// <summary>
        /// Waniliowo piec robi z Unbaked Sweetbread od razu lukrowana bulke - przepinamy to
        /// wyjscie na zwykly Sweetbread. Lukrowana idzie teraz z wlasnego polproduktu
        /// (konwersja dodana w Register). Idempotentne: wpis szukany po wsadzie.
        /// </summary>
        private static void RewireOven()
        {
            var oven = PrefabManager.Instance.GetPrefab("piece_oven");
            var station = oven ? oven.GetComponent<CookingStation>() : null;
            if (!station)
            {
                Jotunn.Logger.LogWarning("SugarFoods: brak CookingStation na prefabie piece_oven");
                return;
            }

            var sweetbread = PrefabManager.Instance.GetPrefab(SweetbreadName);
            var drop = sweetbread ? sweetbread.GetComponent<ItemDrop>() : null;
            if (!drop)
            {
                Jotunn.Logger.LogWarning($"SugarFoods: brak itemu {SweetbreadName} - waniliowa konwersja pieca zostaje");
                return;
            }

            var conversion = station.m_conversion
                .FirstOrDefault(c => c.m_from && c.m_from.name == VanillaUncooked);
            if (conversion == null)
            {
                Jotunn.Logger.LogWarning($"SugarFoods: piec nie ma konwersji z {VanillaUncooked}");
                return;
            }

            conversion.m_to = drop;
            Jotunn.Logger.LogInfo($"SugarFoods: piec {VanillaUncooked} -> {SweetbreadName}");
        }

        // ------------------------------------------------------------------ narzedzia

        private static ItemDrop.ItemData.SharedData VanillaShared(string prefabName)
        {
            var prefab = PrefabManager.Instance.GetPrefab(prefabName);
            var shared = prefab ? prefab.GetComponent<ItemDrop>()?.m_itemData?.m_shared : null;
            if (shared == null) Jotunn.Logger.LogWarning($"SugarFoods: brak waniliowego itemu {prefabName}");
            return shared;
        }

        /// <summary>Regen, czas trawienia i wielkosc stosu zostaja z waniliowego pierwowzoru.</summary>
        private static void SetFood(CustomItem item, float food, float stamina, float eitr)
        {
            var shared = item.ItemDrop.m_itemData.m_shared;
            // UWAGA: m_food MUSI byc > 0 - Player.ConsumeItem wola EatFood tylko wtedy.
            shared.m_food = food;
            shared.m_foodStamina = stamina;
            shared.m_foodEitr = eitr;
        }

        private static void SetIcon(CustomItem item, Sprite icon)
        {
            if (icon) item.ItemDrop.m_itemData.m_shared.m_icons = new[] { icon };
        }

        /// <summary>Klonuje materialy prefabu i podmienia w nich albedo na przemalowane.</summary>
        private static void RecolorModel(GameObject prefab, Func<Color, Color> op, string suffix)
        {
            var cache = new Dictionary<Texture, Texture2D>();
            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials
                    .Select(mat => RecolorMaterial(mat, (x, y, c) => op(c), suffix, cache))
                    .ToArray();
            }
        }

        private static Material RecolorMaterial(Material source, Func<int, int, Color, Color> op, string suffix, Dictionary<Texture, Texture2D> cache)
        {
            if (!source) return null;

            var mat = new Material(source) { name = source.name + suffix };
            var tex = source.mainTexture;
            if (!tex) return mat;

            if (!cache.TryGetValue(tex, out var recolored))
            {
                recolored = RuntimeTextures.Recolor(tex, op, tex.name + suffix);
                cache[tex] = recolored;
            }
            if (recolored) mat.mainTexture = recolored;
            return mat;
        }

        /// <summary>Zdjety lukier: bialy wierzch schodzi w cieple zloto wypieku.</summary>
        private static Color Unfrost(Color c)
        {
            Color.RGBToHSV(c, out _, out var s, out var v);
            var plain = Color.HSVToRGB(0.08f, Mathf.Clamp01(Mathf.Max(0.35f, s)), Mathf.Clamp01(v * 0.75f + 0.05f));
            plain.a = c.a;
            return plain;
        }

    }
}
