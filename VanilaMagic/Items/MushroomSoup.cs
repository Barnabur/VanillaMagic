using System;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Zupa grzybowa: po jednym grzybie kazdego koloru (czerwony, zolty, nasz niebieski),
    /// z jednego craftu 2 porcje, trawi sie 25 min. Pierwotnie porcja dawala sume statystyk grzybow
    /// (40 / 55 / 25), Kamil scial na 20 / 23 / 25:
    ///   Mushroom        15 / 15 /  0, regen 1
    ///   MushroomYellow  10 / 30 /  0, regen 1
    ///   MushroomBlue    15 / 10 / 25, regen 1   (nasze staty - SetupBlueMushroom)
    ///   ------------------------------------
    ///   suma            40 / 55 / 25, regen 3
    ///   MushroomSoup    20 / 23 / 25, regen 3, 1500 s
    /// Model: waniliowy Turnip Stew z albedo MushroomSoup_d z bundla (VanilaMagicUnity/Assets/MushroomSoup). Ikona wlasna (Assets/MushroomSoup.png) - przemalowany Turnip Stew:
    /// brazowy wywar, kawalki rzepy jako czerwone/zolte/niebieskie grzybki.
    /// </summary>
    internal static class MushroomSoup
    {
        public const string ItemName = "MushroomSoup";
        private const string BaseName = "TurnipStew";

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
                Jotunn.Logger.LogError($"MushroomSoup: skladanie itemu nie powiodlo sie: {ex}");
            }
        }

        private static void Build()
        {
            var config = new ItemConfig
            {
                Name = "$item_mushroomsoup",
                Description = "$item_mushroomsoup_description",
                CraftingStation = "piece_cauldron",
                MinStationLevel = 3,
                Amount = 2,
                Requirements = new[]
                {
                    new RequirementConfig("Mushroom", 1),
                    new RequirementConfig("MushroomYellow", 1),
                    new RequirementConfig("MushroomBlue", 1),
                },
            };

            var item = new CustomItem(ItemName, BaseName, config);
            var shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_food = 20f;
            shared.m_foodStamina = 23f;
            shared.m_foodEitr = 25f;
            shared.m_foodRegen = 3f;
            shared.m_foodBurnTime = 1500f;
            shared.m_maxStackSize = 10;

            // Mesh zostaje waniliowy - podmieniamy tylko albedo (brazowy wywar, czerwone/zolte/niebieskie grzybki)
            if (!ModAssets.ApplyAlbedo(item.ItemPrefab, "MushroomSoup_d"))
            {
                Jotunn.Logger.LogWarning("MushroomSoup: brak tekstury w bundlu - zostaje model Turnip Stew");
            }

            var icon = RuntimeTextures.LoadIcon(ItemName);
            if (icon) shared.m_icons = new[] { icon };

            ItemManager.Instance.AddItem(item);
        }
    }
}
