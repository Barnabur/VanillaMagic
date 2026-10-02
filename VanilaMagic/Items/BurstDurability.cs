using System.Collections.Generic;
using HarmonyLib;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Wytrzymalosc -1 za KAZDY pocisk serii, nie tylko za caly atak.
    /// Wanilia odejmuje m_useDurabilityDrain raz, w Attack.ProjectileAttackTriggered (start ataku);
    /// kolejne pociski rapidfire leca z Attack.Update -> FireProjectileBurst bez zuzycia,
    /// wiec seria Flame Wand przy trzymanym przycisku bylaby prawie darmowa.
    /// Pierwszy pocisk serii (m_projectileBurstsFired == 0) pomijamy - za niego zaplacil juz start ataku.
    /// Postfix na FireProjectileBurst odpala sie tez, gdy seria urywa sie na braku eitru
    /// (Stop() przed strzalem) - wtedy zabieramy najwyzej 1 punkt za niewystrzelony pocisk.
    /// </summary>
    [HarmonyPatch(typeof(Attack), nameof(Attack.FireProjectileBurst))]
    internal static class BurstDurability
    {
        private static readonly HashSet<string> Items = new HashSet<string>();

        /// <summary>Wlacza zuzycie per pocisk dla itemu o danym m_shared.m_name.</summary>
        public static void Register(string sharedName) => Items.Add(sharedName);

        private static void Postfix(Attack __instance)
        {
            var weapon = __instance.m_weapon;
            if (weapon == null || !Items.Contains(weapon.m_shared.m_name)) return;
            if (__instance.m_projectileBursts <= 1 || __instance.m_projectileBurstsFired == 0) return;
            if (!weapon.m_shared.m_useDurability || !__instance.m_character.IsPlayer()) return;

            weapon.m_durability -= weapon.m_shared.m_useDurabilityDrain * Game.m_durabilityRate;
        }
    }
}
