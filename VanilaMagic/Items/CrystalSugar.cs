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
    /// Ikona jest wlasna (Assets/CrystalSugar.png), model to klon BarleyFlour (worek z ziarnem)
    /// z materialem tintowanym na lodowy blekit - do wymiany, gdy bedzie wlasna tekstura.
    /// UWAGA na bramki progresji: wiatrak stoi na Stole Rzemieslnika (Lza Smoka -> Moder),
    /// wiec cukier jest de facto za Gorami/Rownina, mimo ze sam Crystal jest z Gor.
    /// </summary>
    internal static class CrystalSugar
    {
        public const string ItemName = "CrystalSugar";
        private const string BaseName = "BarleyFlour";
        private const string FromItem = "Crystal";

        // Lodowy tint worka i poswiaty
        private static readonly Color GlowColor = new Color(0.72f, 0.88f, 1f);

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
        /// Worek siedzi na wspoldzielonym atlasie fi_village_containers (przemalowanie calej
        /// tekstury byloby marnotrawstwem), wiec tintujemy kopie materialu przez _Color.
        /// </summary>
        private static void Sugarize(GameObject prefab)
        {
            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials
                    .Select(mat => mat ? new Material(mat) { name = mat.name + "_crystalsugar", color = GlowColor } : null)
                    .ToArray();
            }
        }

        /// <summary>Ledwie widoczny chlod nad workiem (jak swiatelka jagod w WildBerry).</summary>
        private static void AddGlow(Transform parent)
        {
            var go = new GameObject("Point light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.2f, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = GlowColor;
            light.intensity = 0.2f;
            light.range = 0.4f;
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
