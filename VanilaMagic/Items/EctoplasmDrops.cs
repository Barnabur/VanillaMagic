using System.Linq;
using HarmonyLib;
using Jotunn.Managers;
using UnityEngine;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Przenosi zrodlo ektoplazmy na bagna, zeby GhostShake dalo sie zrobic w swoim biomie.
    /// Waniliowo ektoplazma leci wylacznie z Ghosta (1-5 szt., 100%), a Ghost nie ma wpisu
    /// w spawnerach biomow - siedzi w dungeonach i eventach. Upior (Wraith) lata po bagnach
    /// noca, wiec to on dostaje ektoplazme (zawsze 1 szt.), a Ghost dropi ja juz tylko z 10% szansa.
    ///
    /// Sama zmiana prefabu nie wystarcza: Ghosty ze spawnerow w kryptach dropily waniliowe 1-4
    /// (Random.Range(1, 5) - max jest wylaczny), wiec spawner nie instancjonuje prefabu z ZNetScene
    /// (najpewniej wlasna kopia w bundlu pokoju). Dlatego dropy kazdej instancji poprawiamy tez
    /// tuz przed losowaniem (GenerateDropList), po nazwie prefabu - to dziala niezaleznie od zrodla
    /// i od razu lapie zmiany configu u stworow juz stojacych w swiecie.
    /// </summary>
    internal static class EctoplasmDrops
    {
        public const string EctoplasmName = "Ectoplasm";

        // Wraith: zawsze dokladnie 1 szt. (bez mnoznika za gwiazdki)
        private const int WraithAmount = 1;

        // Ghost: waniliowo 1-5 na 100%; szansa i ilosc z configu (domyslnie 1 szt. z 10% szansa)

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += Apply;
            ModConfig.DropsChanged += Apply;
        }

        private static void Apply()
        {
            ApplyTo(GetDrops("Ghost"), "Ghost");
            ApplyTo(GetDrops("Wraith"), "Wraith");
        }

        /// <summary>Nakłada zmiany na CharacterDrop prefabu albo instancji. Idempotentne.</summary>
        internal static void ApplyTo(CharacterDrop drops, string creature)
        {
            if (!drops) return;
            if (creature == "Ghost") NerfGhost(drops);
            else if (creature == "Wraith") AddToWraith(drops);
        }

        private static void NerfGhost(CharacterDrop drops)
        {
            var drop = Find(drops);
            if (drop == null) return;

            var amount = ModConfig.GhostEctoplasmAmount.Value;
            drop.m_amountMin = amount;
            // GenerateDropList losuje Random.Range(min, max) z wylacznym max (przy min == max daje min)
            drop.m_amountMax = amount;
            drop.m_chance = ModConfig.GhostEctoplasmChance.Value / 100f;
            drop.m_levelMultiplier = false;
        }

        private static void AddToWraith(CharacterDrop drops)
        {
            if (Find(drops) != null) return; // juz dolozone

            var ectoplasm = PrefabManager.Instance.GetPrefab(EctoplasmName);
            if (!ectoplasm) return;

            drops.m_drops.Add(new CharacterDrop.Drop
            {
                m_prefab = ectoplasm,
                m_amountMin = WraithAmount,
                m_amountMax = WraithAmount,
                m_chance = 1f,
                m_levelMultiplier = false,
            });
        }

        private static CharacterDrop GetDrops(string creature)
        {
            var prefab = PrefabManager.Instance.GetPrefab(creature);
            return prefab ? prefab.GetComponent<CharacterDrop>() : null;
        }

        private static CharacterDrop.Drop Find(CharacterDrop drops)
        {
            return drops?.m_drops?.FirstOrDefault(d => d.m_prefab && d.m_prefab.name == EctoplasmName);
        }
    }

    /// <summary>
    /// Wspolny punkt dla zmian dropow: tuz przed losowaniem dropu poprawia CharacterDrop
    /// zabijanej instancji (patrz komentarz w <see cref="EctoplasmDrops"/>).
    /// </summary>
    [HarmonyPatch(typeof(CharacterDrop), nameof(CharacterDrop.GenerateDropList))]
    internal static class DropListPatch
    {
        private static void Prefix(CharacterDrop __instance)
        {
            var creature = Utils.GetPrefabName(__instance.gameObject);
            EctoplasmDrops.ApplyTo(__instance, creature);
            FenringHairDrop.ApplyTo(__instance, creature);
        }
    }
}
