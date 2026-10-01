using System.Linq;
using Jotunn.Managers;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Fenring dostaje 25% szans na 1 siersc Fenrisa (WolfHairBundle) - potrzebna do zbroi Widma.
    /// Waniliowo Fenring dropi tylko WolfFang 1-2 (100%) i trofeum (10%), a siersc lezy wylacznie
    /// jako pickable w jaskiniach lodowych (Pickable_Hairstrands*, hanging_hairstrands, fenrirhide_hanging).
    /// Bez m_levelMultiplier - gwiazdki nie zwiekszaja ilosci. Wpis dokladamy tylko, gdy go jeszcze nie ma.
    /// </summary>
    internal static class FenringHairDrop
    {
        private const string CreatureName = "Fenring";
        private const string HairName = "WolfHairBundle";
        private const float Chance = 0.25f;

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += Apply;
        }

        private static void Apply()
        {
            var hair = PrefabManager.Instance.GetPrefab(HairName);
            var fenring = PrefabManager.Instance.GetPrefab(CreatureName);
            var drops = fenring ? fenring.GetComponent<CharacterDrop>() : null;
            if (!hair || !drops)
            {
                Jotunn.Logger.LogWarning($"FenringHairDrop: brak prefabu {HairName} albo CharacterDrop na {CreatureName}");
                return;
            }
            if (drops.m_drops.Any(d => d.m_prefab && d.m_prefab.name == HairName)) return; // juz dolozone w tej sesji

            drops.m_drops.Add(new CharacterDrop.Drop
            {
                m_prefab = hair,
                m_amountMin = 1,
                m_amountMax = 1,
                m_chance = Chance,
                m_levelMultiplier = false,
            });
            Jotunn.Logger.LogDebug($"FenringHairDrop: {CreatureName} dropi 1 {HairName} z szansa {Chance:P0}");
        }
    }
}
