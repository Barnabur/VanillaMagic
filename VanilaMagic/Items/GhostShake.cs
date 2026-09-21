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
    /// Ghostshake - bagienny odpowiednik Shocklate Smoothie (kociol lvl 2, 1 "maz" + 4 jagody),
    /// tylko pod magie: 1 Ektoplazma + 4 Wildberry zamiast Ooze + maliny/jagody.
    /// Statystyki tez sa przelozeniem smoothie: ta sama sytosc i czas trawienia, ale wiekszosc
    /// bonusu ze staminy idzie w eitr - to pierwsze prawdziwe jedzenie na eitr w progresji.
    /// Ikona jest wlasna (Assets/GhostShake.png), model to waniliowy smoothie z tekstura
    /// przemalowana w runtime na upiorna zielen - do wymiany, gdy bedzie wlasna tekstura.
    /// Zrodlo ektoplazmy przenosi EctoplasmDrops (Wraith zamiast Ghosta).
    /// </summary>
    internal static class GhostShake
    {
        public const string ItemName = "GhostShake";
        private const string BaseName = "ShocklateSmoothie";

        // Upiorna zielen: staly odcien, sciete nasycenie, podbita jasnosc
        // (wariant "D - zjawa zielona", wybrany z podgladu ikon)
        private const float GhostHue = 0.42f;
        private const float GhostSaturationMax = 0.4f;
        private static readonly Color GlowColor = new Color(0.62f, 1f, 0.78f);

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
                Build();
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"GhostShake: skladanie itemu nie powiodlo sie: {ex}");
            }
        }

        private static void Build()
        {
            var config = new ItemConfig
            {
                Name = "$item_ghostshake",
                Description = "$item_ghostshake_description",
                // Shocklate Smoothie idzie z kotla lvl 2 - ghostshake ma byc jego bagiennym bratem
                CraftingStation = "piece_cauldron",
                MinStationLevel = 2,
                Amount = 1,
                Requirements = new[]
                {
                    new RequirementConfig(WildBerry.ItemName, 4),
                    new RequirementConfig(EctoplasmDrops.EctoplasmName, 1),
                },
            };

            var item = new CustomItem(ItemName, BaseName, config);
            var shared = item.ItemDrop.m_itemData.m_shared;

            // Shocklate Smoothie: 16 hp / 50 stamina / 0 eitr, regen 1, 1200 s.
            // UWAGA: m_food MUSI byc > 0 - Player.ConsumeItem wola EatFood tylko wtedy.
            shared.m_food = 16f;
            shared.m_foodStamina = 20f;
            shared.m_foodEitr = 40f;
            shared.m_foodRegen = 1f;
            shared.m_foodBurnTime = 1200f;
            shared.m_maxStackSize = 10;

            Ghostify(item.ItemPrefab);

            var icon = RuntimeTextures.LoadIcon(ItemName);
            if (icon) shared.m_icons = new[] { icon };

            AddGlow(item.ItemPrefab.transform);

            ItemManager.Instance.AddItem(item);
        }

        /// <summary>Przemalowuje materialy klona (czekoladowy braz -> widmowa zielen).</summary>
        private static void Ghostify(GameObject prefab)
        {
            var recolored = new Dictionary<Texture, Texture2D>();
            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials
                    .Select(mat => GhostifyMaterial(mat, recolored))
                    .ToArray();
            }
        }

        private static Material GhostifyMaterial(Material source, Dictionary<Texture, Texture2D> cache)
        {
            if (!source) return null;

            var mat = new Material(source) { name = source.name + "_ghost" };
            var tex = source.mainTexture;
            if (!tex) return mat;

            if (!cache.TryGetValue(tex, out var ghost))
            {
                ghost = RuntimeTextures.Recolor(tex, Ghostify, tex.name + "_ghost");
                cache[tex] = ghost;
            }
            if (ghost) mat.mainTexture = ghost;
            return mat;
        }

        /// <summary>Odcien na stala zielen, nasycenie sciete, jasnosc podbita - wychodzi mleczna zjawa.</summary>
        private static Color Ghostify(Color c)
        {
            Color.RGBToHSV(c, out _, out var s, out var v);
            var ghost = Color.HSVToRGB(GhostHue, Mathf.Min(GhostSaturationMax, s * 0.6f), Mathf.Clamp01(v * 0.55f + 0.45f));
            ghost.a = c.a;
            return ghost;
        }

        /// <summary>Zimna poswiata nad kubkiem (jak swiatelka jagod w WildBerry).</summary>
        private static void AddGlow(Transform parent)
        {
            var go = new GameObject("Point light");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.18f, 0f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = GlowColor;
            light.intensity = 0.3f;
            light.range = 0.6f;
            light.shadows = LightShadows.None;

            var flicker = go.AddComponent<LightFlicker>();
            flicker.m_flickerIntensity = 0.15f;
            flicker.m_flickerSpeed = 6f;
            flicker.m_movement = 0.05f;
            flicker.m_ttl = 0f;
            flicker.m_fadeDuration = 0.2f;
            flicker.m_fadeInDuration = 0f;
        }
    }
}
