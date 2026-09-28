using System;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.Rendering;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Wildberry (item) + WildberryBush (krzak, wegetacja Czarnego Lasu) skladane w kodzie
    /// z waniliowych czesci (kitbash), 1:1 wg recznych prefabow z bundla
    /// (Unity: test/Wildberry.prefab, test/WildberryBush.prefab):
    /// - item: klon Raspberry, skala 0.35, jedzenie 5 hp / 5 st / 15 eitr, wlasna ikona (Assets/WildBerry.png),
    ///   fioletowe swiatelko z LightFlicker
    /// - krzak: klon RaspberryBush; liscie = Bush01_raspberry z tintem na zolto,
    ///   drewno = Bush01_wood z tintem na granatowo (tekstury w recznych materialach byly
    ///   identyczne z waniliowymi - roznil sie tylko _Color), 10 mniejszych jagod ze swiatelkami
    /// - jagoda: waniliowy material raspberry z tekstura przemalowana w runtime
    ///   (czerwien -> magenta) - jedyna prawdziwa przerobka tekstury w bundlu
    /// Nic nie jest shipowane z ripa - jedyny wlasny asset to ikona itemu.
    /// </summary>
    internal static class WildBerry
    {
        public const string ItemName = "Wildberry";
        public const string BushName = "WildberryBush";
        // Plik zasobu ma wielkie B, w odroznieniu od nazwy prefabu
        private const string IconName = "WildBerry";

        // Wartosci przeniesione 1:1 z recznych prefabow/materialow z bundla
        private static readonly Color LeafTint = new Color(1f, 0.8197487f, 0f);
        private static readonly Color WoodTint = new Color(0.098039225f, 0.16862746f, 0.2627451f);
        private static readonly Color BushLightColor = new Color(0.9764706f, 0.18823528f, 0.8254388f);
        private static readonly Color ItemLightColor = new Color(0.9764706f, 0.18823528f, 0.8846969f);
        private const float BerryScale = 0.035f; // vanilla 0.0655

        // Dodatkowe jagody wzgledem vanilla (7 -> 10); pozycje/rotacje lokalne w "Berrys"
        private static readonly (string name, Vector3 pos, Quaternion rot)[] ExtraBerries =
        {
            ("Sphere (7)", new Vector3(0.613f, 0.609f, -0.063f), new Quaternion(0f, 0f, 0.18506493f, 0.98272634f)),
            ("Sphere (8)", new Vector3(-0.252f, 0.627f, -0.667f), new Quaternion(0.019486872f, -0.25522366f, -0.07359431f, 0.96388024f)),
            ("Sphere (9)", new Vector3(-0.502f, 0.541f, 0.479f), new Quaternion(-0.041960347f, 0.54956305f, -0.06352328f, 0.83197635f)),
        };

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += Create;
        }

        private static void Create()
        {
            // Tworzymy tylko raz (event odpala sie przy kazdym wejsciu do main scene)
            PrefabManager.OnVanillaPrefabsAvailable -= Create;
            try
            {
                var berryMaterial = BuildBerryMaterial();
                var item = BuildItem(berryMaterial);
                var bush = BuildBush(item, berryMaterial);
                AddVegetation(bush);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"WildBerry: skladanie krzaka nie powiodlo sie: {ex}");
            }
        }

        /// <summary>
        /// Odtworzony wildberry.mat: kopia waniliowego "raspberry" (Standard) z przemalowana
        /// tekstura oraz smoothness z kanalu alpha albedo (GlossMapScale 0.186, Metallic 0.089).
        /// </summary>
        private static Material BuildBerryMaterial()
        {
            var raspberry = PrefabManager.Instance.GetPrefab("Raspberry");
            var source = raspberry.transform.Find("attach").GetComponent<MeshRenderer>().sharedMaterial;

            var mat = new Material(source) { name = "wildberry" };
            var recolored = RecolorBerryTexture(source.mainTexture as Texture2D);
            if (recolored) mat.mainTexture = recolored;
            mat.SetFloat("_GlossMapScale", 0.186f);
            mat.SetFloat("_Metallic", 0.089f);
            mat.SetFloat("_SmoothnessTextureChannel", 1f);
            mat.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            return mat;
        }

        /// <summary>
        /// Reczna tekstura wildberry_d to waniliowa raspberry_d z przemalowana jagoda
        /// (Hue/Saturation na czerwieniach). Dopasowany model (sredni blad ~6/255 na calej teksturze):
        /// piksele o odcieniu w +-25 stopni od czerwieni i nasyceniu > 0.4 -> odcien -42 stopnie,
        /// nasycenie x1.6, jasnosc x1.35. Reszta (niebieskie tlo, lodyga) bez zmian.
        /// Waniliowa tekstura nie jest czytelna z CPU, wiec kopiujemy ja przez RenderTexture.
        /// </summary>
        private static Texture2D RecolorBerryTexture(Texture2D source)
        {
            if (!source)
            {
                Jotunn.Logger.LogWarning("WildBerry: brak tekstury jagody do przemalowania - zostaje waniliowa");
                return null;
            }

            var readable = CopyReadable(source);
            var pixels = readable.GetPixels();
            for (var i = 0; i < pixels.Length; i++)
            {
                var c = pixels[i];
                Color.RGBToHSV(c, out var h, out var s, out var v);
                var hueFromRed = Mathf.Min(h, 1f - h) * 360f;
                if (hueFromRed >= 25f || s <= 0.4f) continue;

                var shifted = Color.HSVToRGB(Mathf.Repeat(h - 42f / 360f, 1f), Mathf.Min(1f, s * 1.6f), Mathf.Min(1f, v * 1.35f));
                shifted.a = c.a;
                pixels[i] = shifted;
            }
            readable.SetPixels(pixels);
            readable.Apply(true, true);
            return readable;
        }

        private static Texture2D CopyReadable(Texture2D source)
        {
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, source.mipmapCount > 1)
                {
                    name = "wildberry_d",
                    filterMode = source.filterMode,
                    wrapMode = source.wrapMode,
                };
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                return copy;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static CustomItem BuildItem(Material berryMaterial)
        {
            var item = new CustomItem(ItemName, "Raspberry");
            var prefab = item.ItemPrefab;
            prefab.transform.localScale = Vector3.one * 0.35f;

            var shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_name = "$item_wildberries";
            shared.m_description = "$item_wildberries_description";
            // UWAGA: m_food MUSI byc > 0 - Player.ConsumeItem wola EatFood tylko wtedy,
            // przy 0 item znika bez zadnego efektu (stamina/eitr tez nie wchodza).
            shared.m_food = 5f;
            shared.m_foodStamina = 5f;
            shared.m_foodEitr = 15f;
            // m_foodBurnTime 600 i m_foodRegen 1 zostaja z maliny

            var icon = RuntimeTextures.LoadIcon(IconName);
            if (icon) shared.m_icons = new[] { icon };

            var attach = prefab.transform.Find("attach");
            if (attach) attach.GetComponent<MeshRenderer>().sharedMaterial = berryMaterial;
            else Jotunn.Logger.LogWarning("WildBerry: brak dziecka 'attach' w klonie itemu");

            AddBerryLight(prefab.transform, new Vector3(0f, 0.0661f, 0f), 0.0042307694f, ItemLightColor, 0.2f, 0.2f);

            ItemManager.Instance.AddItem(item);
            return item;
        }

        private static GameObject BuildBush(CustomItem item, Material berryMaterial)
        {
            var bush = PrefabManager.Instance.CreateClonedPrefab(BushName, "RaspberryBush");

            var model = bush.transform.Find("model");
            var modelRenderer = model.GetComponent<MeshRenderer>();
            var vanillaMats = modelRenderer.sharedMaterials; // [Bush01_raspberry, Bush01_wood]
            var leaf = new Material(vanillaMats[0]) { name = "Bush01_wildberry", color = LeafTint };
            var wood = new Material(vanillaMats[1]) { name = "Bush03_wood", color = WoodTint };
            modelRenderer.sharedMaterials = new[] { leaf, wood };
            modelRenderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbesAndSkybox;

            var low = model.Find("low");
            if (low) low.GetComponent<MeshRenderer>().sharedMaterial = leaf;
            else Jotunn.Logger.LogWarning("WildBerry: brak LOD-a 'low' w klonie krzaka");

            var berrys = model.Find("Berrys");
            if (berrys)
            {
                var template = berrys.GetChild(0).gameObject;
                foreach (var (name, pos, rot) in ExtraBerries)
                {
                    var extra = UnityEngine.Object.Instantiate(template, berrys);
                    extra.name = name;
                    extra.transform.localPosition = pos;
                    extra.transform.localRotation = rot;
                }
                foreach (Transform berry in berrys)
                {
                    berry.localScale = Vector3.one * BerryScale;
                    berry.GetComponent<MeshRenderer>().sharedMaterial = berryMaterial;
                    AddBerryLight(berry, Vector3.zero, 0.07857143f, BushLightColor, 0.5f, 0.1f);
                }
            }
            else Jotunn.Logger.LogWarning("WildBerry: brak 'Berrys' w klonie krzaka");

            bush.GetComponent<Pickable>().m_itemPrefab = item.ItemPrefab;

            // Prefab rejestruje ZoneManager.AddCustomVegetation (sam wola PrefabManager.AddPrefab)
            return bush;
        }

        /// <summary>Punktowe swiatelko z LightFlicker (wartosci z recznych prefabow).</summary>
        private static void AddBerryLight(Transform parent, Vector3 localPos, float scale, Color color, float intensity, float range)
        {
            var go = new GameObject("Point light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * scale;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;

            var flicker = go.AddComponent<LightFlicker>();
            flicker.m_flickerIntensity = 0.1f;
            flicker.m_flickerSpeed = 10f;
            flicker.m_movement = 0.1f;
            flicker.m_ttl = 0f;
            flicker.m_fadeDuration = 0.2f;
            flicker.m_fadeInDuration = 0f;
        }

        // Dodatkowe biomy poza Czarnym Lasem: (biom, max, min wysokosc, max krzakow w grupie).
        // Max < 1 = szansa na jedna grupe w strefie (ZoneSystem.PlaceVegetation).
        // Na bagnie teren lezy tuz nad woda - 0.5 jak waniliowe firtree_small_dead_swamp.
        private static readonly (Heightmap.Biome biome, float max, float minAltitude, int groupSizeMax)[] ExtraBiomes =
        {
            (Heightmap.Biome.Swamp, 0.5f, 0.5f, 3),
            (Heightmap.Biome.Plains, 0.2f, 1f, 2),
        };

        private static CustomVegetation _blackForestVegetation;

        /// <summary>
        /// Wegetacja: Czarny Las przez Jotunna (60% szans na grupe w strefie, dla porownania waniliowe
        /// borowki maja 1-1 grup, maliny 1-2, moroszki 1-3). Jotunn trzyma jeden wpis na prefab,
        /// wiec bagno i rowniny to klony tego wpisu dokladane recznie do ZoneSystem.m_vegetation.
        /// </summary>
        private static void AddVegetation(GameObject bush)
        {
            var config = new VegetationConfig
            {
                Biome = Heightmap.Biome.BlackForest,
                BlockCheck = true,
                MinAltitude = 1f,
                MaxAltitude = 100f,
                Max = 0.6f,
                ScaleMin = 1f,
                ScaleMax = 1.5f,
                GroupSizeMin = 1,
                GroupSizeMax = 4,
                GroupRadius = 5f,
            };
            _blackForestVegetation = new CustomVegetation(bush, false, config);
            ZoneManager.Instance.AddCustomVegetation(_blackForestVegetation);
            ZoneManager.OnVegetationRegistered += AddExtraBiomeVegetation;
        }

        /// <summary>Odpala sie przy kazdym SetupLocations (kazdy swiat) - pomijamy wpisy, ktore juz sa.</summary>
        private static void AddExtraBiomeVegetation()
        {
            var vegetation = ZoneSystem.instance.m_vegetation;
            foreach (var (biome, max, minAltitude, groupSizeMax) in ExtraBiomes)
            {
                var name = $"{BushName}_{biome}";
                if (vegetation.Exists(v => v.m_name == name)) continue;

                var veg = _blackForestVegetation.Vegetation.Clone();
                veg.m_name = name;
                veg.m_biome = biome;
                veg.m_max = max;
                veg.m_minAltitude = minAltitude;
                veg.m_groupSizeMax = groupSizeMax;
                vegetation.Add(veg);
            }
        }
    }
}
