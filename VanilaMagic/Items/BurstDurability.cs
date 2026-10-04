using System.Collections.Generic;
using HarmonyLib;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Seria rapidfire dla rozdzek: wytrzymalosc -1 za KAZDY pocisk i opoznienie pierwszego strzalu.
    /// Wanilia odejmuje m_useDurabilityDrain raz, w Attack.ProjectileAttackTriggered (start ataku);
    /// kolejne pociski leca z Attack.Update -> UpdateProjectile -> FireProjectileBurst bez zuzycia,
    /// wiec seria Flame Wand przy trzymanym przycisku bylaby prawie darmowa.
    /// Pierwszy pocisk wanilia odpala od razu (m_projectileFireTimer startuje od -1), dopiero
    /// kolejne czekaja m_burstInterval - klikanie od nowa omijalo odstep i strzelalo szybciej niz
    /// trzymanie przycisku. Ustawiamy timer na starcie, a zuzycie ze startu ataku zwracamy
    /// i liczymy dopiero przy faktycznym pocisku (puszczenie przed strzalem nic nie kosztuje).
    /// Postfix na FireProjectileBurst odpala sie tez, gdy seria urywa sie na braku eitru
    /// (Stop() przed strzalem) - wtedy zabieramy najwyzej 1 punkt za niewystrzelony pocisk.
    /// </summary>
    [HarmonyPatch]
    internal static class BurstDurability
    {
        private static readonly Dictionary<string, float> Items = new Dictionary<string, float>();

        /// <summary>
        /// Wlacza zuzycie per pocisk dla itemu o danym m_shared.m_name;
        /// firstShotDelay = czas od zdarzenia ataku w animacji do pierwszego pocisku.
        /// </summary>
        public static void Register(string sharedName, float firstShotDelay) => Items[sharedName] = firstShotDelay;

        private static bool IsTracked(Attack attack, out float firstShotDelay)
        {
            firstShotDelay = 0f;
            var weapon = attack.m_weapon;
            return weapon != null && attack.m_projectileBursts > 1
                && Items.TryGetValue(weapon.m_shared.m_name, out firstShotDelay);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Attack), nameof(Attack.ProjectileAttackTriggered))]
        private static void DelayFirstShot(Attack __instance)
        {
            if (!IsTracked(__instance, out var delay)) return;
            __instance.m_projectileFireTimer = delay;

            var weapon = __instance.m_weapon;
            if (weapon.m_shared.m_useDurability && __instance.m_character.IsPlayer())
            {
                weapon.m_durability += weapon.m_shared.m_useDurabilityDrain * Game.m_durabilityRate;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(Attack), nameof(Attack.FireProjectileBurst))]
        private static void DrainPerShot(Attack __instance)
        {
            if (!IsTracked(__instance, out _)) return;
            var weapon = __instance.m_weapon;
            if (!weapon.m_shared.m_useDurability || !__instance.m_character.IsPlayer()) return;

            weapon.m_durability -= weapon.m_shared.m_useDurabilityDrain * Game.m_durabilityRate;
        }
    }
}
