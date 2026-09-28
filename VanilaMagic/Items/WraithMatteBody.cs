using HarmonyLib;
using UnityEngine;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Matowe cialo pod Szatami Widma. Tors i rekawy szaty (oraz nogawice) to nie siatka, tylko nakladka
    /// na material ciala gracza (Custom/Player) - VisEquipment kopiuje z m_armorMaterial wylacznie tekstury
    /// (_ChestTex/_ChestBumpMap/_ChestMetal), a polysk bierze z _Glossiness ciala (0.2). Peleryna i futro
    /// maja WraithArmor.Gloss, wiec obok nich szata sie swiecila. Tu: po zmianie torsu/nog ustawiamy
    /// _Glossiness ciala na polysk setu, gdy nosimy szate albo nogawice, a po zdjeciu przywracamy oryginal.
    /// Skutek uboczny: skora (dlonie) tez jest matowa, poki nosimy set.
    /// </summary>
    internal static class WraithMatteBody
    {
        private static readonly int GlossId = Shader.PropertyToID("_Glossiness");
        private static readonly int RobeHash = WraithArmor.RobeName.GetStableHashCode();
        private static readonly int LegsHash = WraithArmor.LegsName.GetStableHashCode();
        private static readonly AccessTools.FieldRef<VisEquipment, int> ChestHash = AccessTools.FieldRefAccess<VisEquipment, int>("m_currentChestItemHash");
        private static readonly AccessTools.FieldRef<VisEquipment, int> LegHash = AccessTools.FieldRefAccess<VisEquipment, int>("m_currentLegItemHash");
        private const float VanillaBodyGloss = 0.2f; // PlayerMaterial._Glossiness

        private static void Apply(VisEquipment vis)
        {
            if (!vis || !vis.m_bodyModel) return;
            var mat = vis.m_bodyModel.material;
            if (!mat || !mat.HasProperty(GlossId)) return;
            var wearing = ChestHash(vis) == RobeHash || LegHash(vis) == LegsHash;
            mat.SetFloat(GlossId, wearing ? WraithArmor.Gloss : VanillaBodyGloss);
        }

        [HarmonyPatch(typeof(VisEquipment), "SetChestEquipped")]
        private static class ChestPatch
        {
            private static void Postfix(VisEquipment __instance, bool __result)
            {
                if (__result) Apply(__instance);
            }
        }

        [HarmonyPatch(typeof(VisEquipment), "SetLegEquipped")]
        private static class LegsPatch
        {
            private static void Postfix(VisEquipment __instance, bool __result)
            {
                if (__result) Apply(__instance);
            }
        }
    }
}
