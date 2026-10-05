using System.Linq;
using Jotunn.Managers;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Fenring dostaje szanse na siersc Fenrisa (WolfHairBundle) - potrzebna do zbroi Widma.
    /// Szansa i ilosc z configu (domyslnie 25%, 1 szt.).
    /// Waniliowo Fenring dropi tylko WolfFang 1-2 (100%) i trofeum (10%), a siersc lezy wylacznie
    /// jako pickable w jaskiniach lodowych (Pickable_Hairstrands*, hanging_hairstrands, fenrirhide_hanging).
    /// Bez m_levelMultiplier - gwiazdki nie zwiekszaja ilosci. Wpis dokladamy raz, a przy zmianie
    /// configu tylko aktualizujemy jego wartosci. Instancje poprawia tez DropListPatch
    /// (prefab z ZNetScene nie zawsze jest tym, ktory spawner instancjonuje).
    /// </summary>
    internal static class FenringHairDrop
    {
        private const string CreatureName = "Fenring";
        private const string HairName = "WolfHairBundle";

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += Apply;
            ModConfig.DropsChanged += Apply;
        }

        private static void Apply()
        {
            var fenring = PrefabManager.Instance.GetPrefab(CreatureName);
            ApplyTo(fenring ? fenring.GetComponent<CharacterDrop>() : null, CreatureName);
        }

        /// <summary>Nakłada zmiany na CharacterDrop prefabu albo instancji. Idempotentne.</summary>
        internal static void ApplyTo(CharacterDrop drops, string creature)
        {
            if (!drops || creature != CreatureName) return;

            var drop = drops.m_drops.FirstOrDefault(d => d.m_prefab && d.m_prefab.name == HairName);
            if (drop == null)
            {
                // przy zmianie configu w menu glownym prefaby moga jeszcze nie istniec - nalozy sie przy wejsciu do swiata
                var hair = PrefabManager.Instance.GetPrefab(HairName);
                if (!hair) return;
                drop = new CharacterDrop.Drop { m_prefab = hair, m_levelMultiplier = false };
                drops.m_drops.Add(drop);
            }

            var amount = ModConfig.FenringHairAmount.Value;
            drop.m_amountMin = amount;
            drop.m_amountMax = amount;
            drop.m_chance = ModConfig.FenringHairChance.Value / 100f;
        }
    }
}
