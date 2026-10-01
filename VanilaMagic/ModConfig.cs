using System;
using BepInEx.Configuration;

namespace VanilaMagic
{
    /// <summary>
    /// Ustawienia moda w BepInEx/config/com.barnabur.vanilamagic.cfg.
    /// Wszystkie wpisy sa IsAdminOnly: Jotunn synchronizuje je z serwera do klientow,
    /// wiec na serwerze obowiazuja wartosci serwera (dropy i tak liczy wlasciciel stwora).
    /// Zmiana w trakcie gry (edycja pliku, Configuration Manager, synchronizacja z serwera)
    /// odpala <see cref="DropsChanged"/> - prefaby dostaja nowe wartosci od razu, a stwory
    /// juz stojace w swiecie zachowuja stare do ponownego zaladowania strefy.
    /// </summary>
    internal static class ModConfig
    {
        public static ConfigEntry<float> FenringHairChance;
        public static ConfigEntry<int> FenringHairAmount;
        public static ConfigEntry<float> GhostEctoplasmChance;
        public static ConfigEntry<int> GhostEctoplasmAmount;

        /// <summary>Ktorys z wpisow sekcji Drops zmienil wartosc.</summary>
        public static event Action DropsChanged;

        public static void Bind(ConfigFile config)
        {
            const string drops = "Drops";

            FenringHairChance = BindChance(config, drops, "FenringHairChance", 25f,
                "Chance (%) that a Fenring drops Wolf Hair Bundle.");
            FenringHairAmount = BindAmount(config, drops, "FenringHairAmount", 1,
                "How many Wolf Hair Bundles a Fenring drops.");
            GhostEctoplasmChance = BindChance(config, drops, "GhostEctoplasmChance", 10f,
                "Chance (%) that a Ghost drops Ectoplasm (vanilla: 100).");
            GhostEctoplasmAmount = BindAmount(config, drops, "GhostEctoplasmAmount", 1,
                "How much Ectoplasm a Ghost drops (vanilla: 1-5).");
        }

        private static ConfigEntry<float> BindChance(ConfigFile config, string section, string key, float value, string description)
        {
            var entry = config.Bind(section, key, value, new ConfigDescription(description,
                new AcceptableValueRange<float>(0f, 100f), AdminOnly()));
            entry.SettingChanged += (_, __) => DropsChanged?.Invoke();
            return entry;
        }

        private static ConfigEntry<int> BindAmount(ConfigFile config, string section, string key, int value, string description)
        {
            var entry = config.Bind(section, key, value, new ConfigDescription(description,
                new AcceptableValueRange<int>(1, 20), AdminOnly()));
            entry.SettingChanged += (_, __) => DropsChanged?.Invoke();
            return entry;
        }

        private static ConfigurationManagerAttributes AdminOnly() => new ConfigurationManagerAttributes { IsAdminOnly = true };
    }
}
