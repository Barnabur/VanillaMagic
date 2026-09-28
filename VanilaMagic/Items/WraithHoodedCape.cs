using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Peleryna Widma z Kapturem Widma: gorny pas peleryny (pod kolnierzem kaptura) niewidzialny - material
    /// WraithArmor.CapeMaterialHooded zamiast CapeMaterial na instancji peleryny danej postaci. Bez kaptura (albo z innym
    /// helmem) peleryna pelna. Przelaczane po zmianie helmu i po zalozeniu peleryny (kolejnosc w UpdateEquipmentVisuals
    /// jest dowolna, wiec oba patche). Do tego pas peleryny w kapturze (WraithArmor.CapeStripName) - wlaczony tylko
    /// z kapturem i peleryna naraz.
    /// </summary>
    internal static class WraithHoodedCape
    {
        private static readonly int HoodHash = WraithArmor.HoodName.GetStableHashCode();
        private static readonly AccessTools.FieldRef<VisEquipment, int> HelmetHash = AccessTools.FieldRefAccess<VisEquipment, int>("m_currentHelmetItemHash");
        private static readonly int CapeHash = WraithArmor.CapeName.GetStableHashCode();
        private static readonly AccessTools.FieldRef<VisEquipment, int> ShoulderHash = AccessTools.FieldRefAccess<VisEquipment, int>("m_currentShoulderItemHash");
        private static readonly AccessTools.FieldRef<VisEquipment, GameObject> HelmetInstance = AccessTools.FieldRefAccess<VisEquipment, GameObject>("m_helmetItemInstance");
        private static readonly AccessTools.FieldRef<VisEquipment, List<GameObject>> ShoulderInstances = AccessTools.FieldRefAccess<VisEquipment, List<GameObject>>("m_shoulderItemInstances");

        private static void Apply(VisEquipment vis)
        {
            var full = WraithArmor.CapeMaterial;
            var hooded = WraithArmor.CapeMaterialHooded;
            if (!vis || !full || !hooded) return;
            var hoodOn = HelmetHash(vis) == HoodHash;
            var capeOn = ShoulderHash(vis) == CapeHash;

            // pas peleryny w kapturze tylko razem z Peleryna Widma
            var helmet = HelmetInstance(vis);
            if (helmet)
                foreach (var smr in helmet.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (smr.name == WraithArmor.CapeStripName) smr.enabled = hoodOn && capeOn;

            var instances = ShoulderInstances(vis);
            if (instances == null) return;
            var wanted = hoodOn ? hooded : full;
            foreach (var instance in instances)
            {
                if (!instance) continue;
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = renderer.sharedMaterials;
                    var changed = false;
                    for (var i = 0; i < mats.Length; i++)
                    {
                        // po nazwie tez, gdyby cos (MaterialMan, mokrosc) zrobilo instancje materialu
                        var ours = mats[i] && (mats[i] == full || mats[i] == hooded
                            || mats[i].name.StartsWith(full.name) || mats[i].name.StartsWith(hooded.name));
                        if (ours && mats[i] != wanted) { mats[i] = wanted; changed = true; }
                    }
                    if (changed) renderer.sharedMaterials = mats;
                }
            }
        }

        [HarmonyPatch(typeof(VisEquipment), "SetHelmetEquipped")]
        private static class HelmetPatch
        {
            private static void Postfix(VisEquipment __instance, bool __result)
            {
                if (__result) Apply(__instance);
            }
        }

        [HarmonyPatch(typeof(VisEquipment), "SetShoulderEquipped")]
        private static class ShoulderPatch
        {
            private static void Postfix(VisEquipment __instance, bool __result)
            {
                if (__result) Apply(__instance);
            }
        }
    }
}
