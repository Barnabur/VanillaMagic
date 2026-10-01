using System.Linq;
using Jotunn.Managers;
using UnityEngine;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Przenosi zrodlo ektoplazmy na bagna, zeby GhostShake dalo sie zrobic w swoim biomie.
    /// Waniliowo ektoplazma leci wylacznie z Ghosta (1-5 szt., 100%), a Ghost nie ma wpisu
    /// w spawnerach biomow - siedzi w dungeonach i eventach. Upior (Wraith) lata po bagnach
    /// noca, wiec to on dostaje ektoplazme (zawsze 1 szt.), a Ghost dropi ja juz tylko z 10% szansa.
    /// Nie odpinamy sie od eventu (waniliowe prefaby moga byc przeladowane miedzy sesjami);
    /// obie operacje sa idempotentne - wpis Upiora dokladamy tylko, gdy go jeszcze nie ma.
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
            var ectoplasm = PrefabManager.Instance.GetPrefab(EctoplasmName);
            if (!ectoplasm)
            {
                // przy zmianie configu w menu glownym prefaby moga jeszcze nie istniec - nalozy sie przy wejsciu do swiata
                Jotunn.Logger.LogDebug($"EctoplasmDrops: brak waniliowego prefabu {EctoplasmName}");
                return;
            }

            NerfGhost(ectoplasm);
            AddToWraith(ectoplasm);
        }

        private static void NerfGhost(GameObject ectoplasm)
        {
            var drops = GetDrops("Ghost");
            var drop = Find(drops);
            if (drop == null)
            {
                Jotunn.Logger.LogWarning("EctoplasmDrops: Ghost nie ma wpisu z ektoplazma - nic nie tne");
                return;
            }

            var amount = ModConfig.GhostEctoplasmAmount.Value;
            drop.m_amountMin = amount;
            drop.m_amountMax = amount;
            drop.m_chance = ModConfig.GhostEctoplasmChance.Value / 100f;
            Jotunn.Logger.LogDebug($"EctoplasmDrops: Ghost dropi {amount} ektoplazmy z szansa {drop.m_chance:P0}");
        }

        private static void AddToWraith(GameObject ectoplasm)
        {
            var drops = GetDrops("Wraith");
            if (drops == null) return;
            if (Find(drops) != null) return; // juz dolozone w tej sesji

            drops.m_drops.Add(new CharacterDrop.Drop
            {
                m_prefab = ectoplasm,
                m_amountMin = WraithAmount,
                m_amountMax = WraithAmount,
                m_chance = 1f,
                m_levelMultiplier = false,
            });
            Jotunn.Logger.LogDebug($"EctoplasmDrops: Wraith dropi zawsze {WraithAmount} ektoplazmy");
        }

        private static CharacterDrop GetDrops(string creature)
        {
            var prefab = PrefabManager.Instance.GetPrefab(creature);
            var drops = prefab ? prefab.GetComponent<CharacterDrop>() : null;
            if (!drops) Jotunn.Logger.LogWarning($"EctoplasmDrops: brak CharacterDrop na prefabie {creature}");
            return drops;
        }

        private static CharacterDrop.Drop Find(CharacterDrop drops)
        {
            return drops?.m_drops?.FirstOrDefault(d => d.m_prefab && d.m_prefab.name == EctoplasmName);
        }
    }
}
