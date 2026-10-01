using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Koszty ulepszen rozlozone na rozne materialy per poziom jakosci.
    /// Vanilla liczy koszt zawsze jako m_amountPerLevel * (poziom - 1) z tego samego materialu,
    /// wiec nie da sie natywnie powiedziec "srebro dopiero od poziomu 3". Patch na
    /// Piece.Requirement.GetAmount podmienia wynik dla zarejestrowanych wymagan na tabele
    /// [lvl1, lvl2, lvl3, lvl4]. UI (InventoryGui.SetupRequirementList) samo pomija wymagania
    /// z GetAmount(poziom) mniejszym lub rownym 0, a odkrywanie receptury (Player.HaveRequirementItems)
    /// patrzy na m_amount, czyli na koszt poziomu 1 - materialy z wyzszych poziomow nie blokuja odkrycia.
    /// </summary>
    internal static class LeveledRequirements
    {
        private static readonly Dictionary<Piece.Requirement, int[]> Table = new Dictionary<Piece.Requirement, int[]>();

        /// <summary>Tabele per item: nazwa prefabu itemu -> (nazwa prefabu materialu -> ilosci per poziom).</summary>
        private static readonly Dictionary<string, Dictionary<string, int[]>> Pending = new Dictionary<string, Dictionary<string, int[]>>();

        private static bool _hooked;

        public static void Register(string itemPrefab, Dictionary<string, int[]> perLevel)
        {
            Pending[itemPrefab] = perLevel;
            if (_hooked) return;
            _hooked = true;
            // ObjectDB jest przeladowywane przy kazdym wejsciu do swiata - Jotunn odpala event kazdorazowo
            Jotunn.Managers.ItemManager.OnItemsRegistered += Apply;
        }

        private static void Apply()
        {
            if (!ObjectDB.instance) return;
            var applied = 0;
            foreach (var recipe in ObjectDB.instance.m_recipes)
            {
                if (!recipe || !recipe.m_item) continue;
                if (!Pending.TryGetValue(recipe.m_item.gameObject.name, out var perLevel)) continue;
                foreach (var req in recipe.m_resources)
                {
                    if (req?.m_resItem == null) continue;
                    if (!perLevel.TryGetValue(req.m_resItem.gameObject.name, out var amounts))
                    {
                        Jotunn.Logger.LogWarning($"LeveledRequirements: {recipe.m_item.gameObject.name} ma skladnik {req.m_resItem.gameObject.name} bez tabeli poziomow");
                        continue;
                    }
                    Table[req] = amounts;
                    applied++;
                }
            }
            Jotunn.Logger.LogDebug($"LeveledRequirements: podpiete {applied} wymagan dla {Pending.Count} itemow");
        }

        [HarmonyPatch(typeof(Piece.Requirement), nameof(Piece.Requirement.GetAmount))]
        private static class GetAmountPatch
        {
            private static void Postfix(Piece.Requirement __instance, int qualityLevel, ref int __result)
            {
                if (!Table.TryGetValue(__instance, out var amounts)) return;
                var index = Mathf.Clamp(qualityLevel, 1, amounts.Length) - 1;
                __result = amounts[index];
            }
        }
    }
}
