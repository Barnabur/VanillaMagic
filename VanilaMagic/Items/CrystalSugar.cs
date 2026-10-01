using System;
using System.Linq;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Crystal sugar - krysztal zmielony w wiatraku, dokladnie tak jak Barley -> BarleyFlour
    /// (Smelter na prefabie "windmill": 1:1, 10 s na sztuke, wsad do 50).
    /// Ikona jest wlasna (Assets/CrystalSugar.png), model to klon BarleyFlour: samo ZIARNO
    /// w worku jest przemalowane na blekit i swieci (emisja), plotno worka zostaje waniliowe.
    /// UWAGA na bramki progresji: wiatrak stoi na Stole Rzemieslnika (Lza Smoka -> Moder),
    /// wiec cukier jest de facto za Gorami/Rownina, mimo ze sam Crystal jest z Gor.
    /// </summary>
    internal static class CrystalSugar
    {
        public const string ItemName = "CrystalSugar";
        private const string BaseName = "BarleyFlour";
        private const string FromItem = "Crystal";

        private const string SackMaterial = "fi_village_containers";

        // Ziarno w worku ma wlasna wysepke na atlasie fi_village_containers_hd - tylko ja
        // przemalowujemy, zeby samo plotno zostalo waniliowe. Prostokat policzony offline
        // z UV trojkatow czapy worka (siatka "Mesh" z fi_vil_container_sack03_grain,
        // 6 trojkatow, srodek wysepki w v < 0.2 - reszta gornych trojkatow to kolnierz).
        private static readonly Rect GrainUv = Rect.MinMaxRect(0.6087f, 0.0078f, 0.7573f, 0.1931f);

        private static readonly Color SugarTint = new Color(0.30f, 0.62f, 1f);    // mnoznik na ziarnie
        private static readonly Color EmissionTint = new Color(0.25f, 0.60f, 1f); // kolor swiecenia ziarna
        private const float EmissionBoost = 1.5f;

        // Punktowka tuz nad wlotem worka - ma dac odrobine rozlewu na ziemie, a nie
        // rozswietlic calego worka od srodka (dlatego jest NAD nim, nie w nim)
        private static readonly Color LightColor = new Color(0.45f, 0.75f, 1f);
        private const float LightHeight = 0.7f;
        private const float LightIntensity = 0.5f;
        private const float LightRange = 1f;

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += Create;

            // Konwersje Jotunn trzyma po nazwach i podpina przy rejestracji ZNetScene,
            // wiec zglaszamy ja od razu przy ladowaniu moda - prefaby nie sa tu potrzebne.
            ItemManager.Instance.AddItemConversion(new CustomItemConversion(new SmelterConversionConfig
            {
                Station = Smelters.Windmill,
                FromItem = FromItem,
                ToItem = ItemName,
            }));
        }

        private static void Create()
        {
            // Tworzymy tylko raz (event odpala sie przy kazdym wejsciu do main scene)
            PrefabManager.OnVanillaPrefabsAvailable -= Create;
            try
            {
                Build();
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"CrystalSugar: skladanie itemu nie powiodlo sie: {ex}");
            }
        }

        private static void Build()
        {
            var config = new ItemConfig
            {
                Name = "$item_crystalsugar",
                Description = "$item_crystalsugar_description",
            };

            var item = new CustomItem(ItemName, BaseName, config);
            var shared = item.ItemDrop.m_itemData.m_shared;

            // 1 krysztal -> 1 cukier, wiec i waga jak u krysztalu (maka wazy 0.2)
            shared.m_weight = 1f;
            shared.m_maxStackSize = 50;

            Sugarize(item.ItemPrefab);

            var icon = RuntimeTextures.LoadIcon(ItemName);
            if (icon) shared.m_icons = new[] { icon };

            AddGlow(item.ItemPrefab.transform);

            ItemManager.Instance.AddItem(item);
        }

        /// <summary>
        /// Worek i jego zawartosc to JEDNA siatka z jedna podsiatka i jednym materialem, wiec
        /// nie da sie ich rozdzielic przypisaniem drugiego materialu. Rozdzielamy je w teksturze:
        /// kopia atlasu jest przemalowywana wylacznie w prostokacie UV ziarna (GrainUv), a druga
        /// kopia - z ziarnem na czarnym tle - idzie jako _EmissionMap, zeby swiecila sama
        /// zawartosc. Plotno worka zostaje waniliowe.
        /// </summary>
        private static void Sugarize(GameObject prefab)
        {
            var done = false;
            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mats = renderer.sharedMaterials;
                var touched = false;
                for (var i = 0; i < mats.Length; i++)
                {
                    if (!mats[i] || !mats[i].name.StartsWith(SackMaterial, StringComparison.Ordinal)) continue;
                    mats[i] = SugarizeMaterial(mats[i], ref done);
                    touched = true;
                }
                if (touched) renderer.sharedMaterials = mats;
            }

            if (!done) Jotunn.Logger.LogWarning($"CrystalSugar: nie znalazlem materialu {SackMaterial} na klonie worka");
        }

        private static Material SugarizeMaterial(Material source, ref bool logged)
        {
            var mat = new Material(source) { name = source.name + "_crystalsugar" };
            var tex = source.mainTexture;
            if (!tex)
            {
                Jotunn.Logger.LogWarning("CrystalSugar: material worka nie ma albedo - zostaje waniliowy");
                return mat;
            }

            mat.mainTexture = RuntimeTextures.Recolor(tex, GrainOnly(tex, Blue), tex.name + "_crystalsugar");

            // Emisja: mapa czarna wszedzie poza ziarnem, kolor mnoznika ponad 1 dla mocniejszej lupy.
            var emissive = mat.HasProperty("_EmissionMap") && mat.HasProperty("_EmissionColor");
            if (emissive)
            {
                mat.SetTexture("_EmissionMap", RuntimeTextures.Recolor(tex, GrainOnly(tex, Glow, Color.black), tex.name + "_crystalsugar_e"));
                mat.SetColor("_EmissionColor", Color.white * EmissionBoost);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            if (!logged)
            {
                logged = true;
                Jotunn.Logger.LogDebug($"CrystalSugar: {source.name} shader={mat.shader.name} albedo={tex.name} {tex.width}x{tex.height} " +
                                      $"emisja={(emissive ? "tak" : "NIE - zostaje sam tint")}");
            }
            return mat;
        }

        /// <summary>
        /// Opakowuje operacje na pikselach tak, by dzialala tylko wewnatrz prostokata UV ziarna.
        /// Piksele poza nim dostaja <paramref name="outside"/> albo zostaja bez zmian.
        /// </summary>
        private static Func<int, int, Color, Color> GrainOnly(Texture tex, Func<Color, Color> op, Color? outside = null)
        {
            var x0 = Mathf.FloorToInt(GrainUv.xMin * tex.width);
            var x1 = Mathf.CeilToInt(GrainUv.xMax * tex.width);
            var y0 = Mathf.FloorToInt(GrainUv.yMin * tex.height);
            var y1 = Mathf.CeilToInt(GrainUv.yMax * tex.height);
            return (x, y, c) =>
            {
                if (x < x0 || x > x1 || y < y0 || y > y1) return outside ?? c;
                return op(c);
            };
        }

        /// <summary>Ziarno przemnozone przez blekit - czarne wypelnienie wysepki zostaje czarne.</summary>
        private static Color Blue(Color c)
        {
            return new Color(c.r * SugarTint.r, c.g * SugarTint.g, c.b * SugarTint.b, c.a);
        }

        /// <summary>Mapa emisji: jasnosc ziarna przelozona na blekit, reszta czarna.</summary>
        private static Color Glow(Color c)
        {
            var luma = c.grayscale;
            return new Color(EmissionTint.r * luma, EmissionTint.g * luma, EmissionTint.b * luma, 1f);
        }

        /// <summary>Niebieska poswiata nad workiem (jak swiatelka jagod w WildBerry).</summary>
        private static void AddGlow(Transform parent)
        {
            var go = new GameObject("Point light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, LightHeight, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = LightColor;
            light.intensity = LightIntensity;
            light.range = LightRange;
            light.shadows = LightShadows.None;

            var flicker = go.AddComponent<LightFlicker>();
            flicker.m_flickerIntensity = 0.1f;
            flicker.m_flickerSpeed = 4f;
            flicker.m_movement = 0.02f;
            flicker.m_ttl = 0f;
            flicker.m_fadeDuration = 0.2f;
            flicker.m_fadeInDuration = 0f;
        }
    }
}
