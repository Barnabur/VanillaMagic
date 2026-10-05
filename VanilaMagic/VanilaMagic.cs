using System.IO;
using BepInEx;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using HarmonyLib;
using UnityEngine;
using VanilaMagic.StatusEffects;
using VanilaMagic.DungeonRooms;
using Jotunn.Configs;
using System.Collections.Generic;
using TMPro;
using System;
using System.Linq;
using System.Collections;
using static DungeonDB;

namespace VanilaMagic
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    internal class VanilaMagic : BaseUnityPlugin
    {
        public const string PluginGUID = "com.barnabur.vanillamagic";
        public const string PluginName = "VanillaMagic";
        public const string PluginVersion = "0.1.1";
        private GameObject BlueMushroomPrefab;
        // Use this class to add your own localization to the game
        // https://valheim-modding.github.io/Jotunn/tutorials/localization.html
        private CustomLocalization Localization;
        private void AddLocalizations()
        {
            // Get your mod translation instance
            Localization = LocalizationManager.Instance.GetLocalization();

            // Add translations for the surtlingwand
            Localization.AddTranslation("English", new Dictionary<string, string>
            {
                { "item_wandfireball", "Flame Wand" },
                { "item_staffheal", "Healing Staff" },
                { "item_wandfireball_description", "A bronze wand crowned with a surtling core that never stops smouldering. Spits a stream of embers that burst into flame on impact." },
                { "item_wandfrost", "Frost Wand" },
                { "item_wandfrost_description", "A crystal-tipped wand that hurls a bolt of biting cold. Bursts on impact." },
                { "item_wandstone", "Stone Wand" },
                { "item_wandstone_description", "A black metal claw gripping a shard of obsidian. Hurls a heavy rock that shatters on impact and knocks foes back." },
                { "item_staffheal_description", "Withered bone and ancient seeds bound together with ectoplasm. The greydwarves' stolen life seeps from it, mending the wounds of everyone who stands close." },
                { "se_heal", "Healing" },
                { "se_heal_desc", "Health per second:\n<color=#ff8000>{0}</color> <color=#ffff00>({1})</color>\nDuration: <color=#ff8000>{2}s</color>" },
                { "item_bluemushroom", "Blue Mushroom" },
                { "item_bluemushroom_description", "A strange mushroom pulsing with magical energy." },
                { "item_wildberries", "Wildberries" },
                { "item_wildberries_description", "Glowing purple berries. Sweet, and humming with eitr." },
                { "item_ghostshake", "Ghostshake" },
                { "item_ghostshake_description", "Wildberries churned with ectoplasm until the whole thing glows. Cold all the way down, and the mist it leaves behind tastes of magic." },
                { "item_crystalsugar", "Crystal Sugar" },
                { "item_crystalsugar_description", "Mountain crystal run through the millstones until it is fine as frost. It never melts, and it never quite stops glittering." },
                { "item_eyescreamsprinkles", "Eyescream with Sprinkles" },
                { "item_eyescreamsprinkles_description", "Frozen greydwarf eyes under a scatter of crystal sugar. The sprinkles crackle with eitr on the way down." },
                { "item_sweetbread", "Sweetbread" },
                { "item_sweetbread_description", "An unbaked sweetbread taken out of the oven plain, with no frosting on it. Sweet enough on its own." },
                { "item_frostedsweetbread_uncooked", "Unbaked Frosted Sweetbread" },
                { "item_frostedsweetbread_uncooked_description", "Sweetbread dough rolled in crystal sugar. The frosting sets in the oven." },
                { "item_wraithhood", "Wraith Hood" },
                { "item_wraithhood_description", "A hood torn from a wraith's shroud, held together with chain. The cold never quite leaves it." },
                { "item_wraithrobe", "Wraith Robe" },
                { "item_wraithrobe_description", "Tattered robes bound in wraith-chain. Light as mist, and it hums with eitr." },
                { "item_wraithlegs", "Wraith Leggings" },
                { "item_wraithlegs_description", "Leggings stitched from shroud and chain. They do not slow the wearer." },
                { "item_wraithcape", "Wraith Cape" },
                { "item_wraithcape_description", "A shroud-cape trailing chains. Keeps the mountain cold at bay." },
                { "se_wraithset", "Wraith's Shroud" },
                { "se_wraithset_tooltip", "Bound in the wraith's chains, the dead lend you their craft." },

            });

            Localization.AddTranslation("Polish", new Dictionary<string, string>
            {
                { "item_wandfireball", "Różdżka żaru" },
                { "item_staffheal", "Kostur leczenia" },
                { "item_wandfireball_description", "Brązowa różdżka zwieńczona rdzeniem pomiotu Surtra, który nigdy nie przestaje się tlić. Pluje strumieniem żaru, który przy uderzeniu wybucha płomieniem." },
                { "item_wandfrost", "Różdżka mrozu" },
                { "item_wandfrost_description", "Różdżka zakończona kryształem, która ciska pocisk przeszywającego zimna. Rozpryskuje się przy uderzeniu." },
                { "item_wandstone", "Różdżka kamienia" },
                { "item_wandstone_description", "Szpon z czarnego metalu ściskający odłamek obsydianu. Ciska ciężkim kamieniem, który roztrzaskuje się przy uderzeniu i odrzuca wrogów." },
                { "item_staffheal_description", "Wysuszone kości i starożytne nasiona spojone ektoplazmą. Skradzione Szarłom życie sączy się z niego, zasklepiając rany każdego, kto stoi w pobliżu." },
                { "se_heal", "Leczenie" },
                { "se_heal_desc", "Zdrowie na sekundę:\n<color=#ff8000>{0}</color> <color=#ffff00>({1})</color>\nCzas trwania: <color=#ff8000>{2}s</color>" },
                { "item_bluemushroom", "Niebieski grzyb" },
                { "item_bluemushroom_description", "Dziwny grzyb pulsujący magiczną energią." },
                { "item_wildberries", "Dzikie jagody" },
                { "item_wildberries_description", "Świecące fioletowe jagody. Słodkie i brzęczące od eitru." },
                { "item_ghostshake", "Upiorny koktajl" },
                { "item_ghostshake_description", "Dzikie jagody ubite z ektoplazmą, aż całość zaczyna świecić. Zimny do samego dna, a mgiełka, którą zostawia, smakuje magią." },
                { "item_crystalsugar", "Kryształowy cukier" },
                { "item_crystalsugar_description", "Górski kryształ zmielony w żarnach na proch drobny jak szron. Nigdy się nie topi i nigdy do końca nie przestaje lśnić." },
                { "item_eyescreamsprinkles", "Lody z oczu z posypką" },
                { "item_eyescreamsprinkles_description", "Zamrożone oczy Szarłów pod warstwą kryształowego cukru. Posypka trzaska od eitru przy przełykaniu." },
                { "item_sweetbread", "Słodka bułka" },
                { "item_sweetbread_description", "Słodka bułka wyjęta z pieca bez lukru. Wystarczająco słodka sama w sobie." },
                { "item_frostedsweetbread_uncooked", "Nieupieczona lukrowana słodka bułka" },
                { "item_frostedsweetbread_uncooked_description", "Ciasto na słodką bułkę obtoczone w kryształowym cukrze. Lukier zastyga w piecu." },
                { "item_wraithhood", "Kaptur upiora" },
                { "item_wraithhood_description", "Kaptur wydarty z całunu upiora, spięty łańcuchem. Chłód nigdy go do końca nie opuszcza." },
                { "item_wraithrobe", "Szata upiora" },
                { "item_wraithrobe_description", "Postrzępione szaty spięte upiornym łańcuchem. Lekkie jak mgła i brzęczące od eitru." },
                { "item_wraithlegs", "Nogawice upiora" },
                { "item_wraithlegs_description", "Nogawice zszyte z całunu i łańcucha. Nie spowalniają noszącego." },
                { "item_wraithcape", "Peleryna upiora" },
                { "item_wraithcape_description", "Całunowa peleryna ciągnąca za sobą łańcuchy. Chroni przed górskim chłodem." },
                { "se_wraithset", "Całun upiora" },
                { "se_wraithset_tooltip", "Spętany łańcuchami upiora – umarli użyczają ci swojego kunsztu." },
            });
        }
        private void Awake()
        {
            // Jotunn comes with its own Logger class to provide a consistent Log style for all mods using it
            Jotunn.Logger.LogInfo("VanillaMagic has landed");
            AddLocalizations();
            // config przed rejestracja - dropy czytaja z niego wartosci
            ModConfig.Bind(Config);
            // Bundle wylaczony: rozdzki i krzak sa w calosci kodowe (kitbash z waniliowych assetow).
            // WildBerry przed HealingStaff - status effect leczenia bierze ikone z itemu Wildberry.
            Items.SurtlingWand.Register();
            Items.FrostWand.Register();
            Items.StoneWand.Register();
            Items.WildBerry.Register();
            // GhostShake po WildBerry - receptura bierze Wildberry jako skladnik
            Items.GhostShake.Register();
            Items.EctoplasmDrops.Register();
            Items.FenringHairDrop.Register();
            Items.CrystalSugar.Register();
            Items.SugarFoods.Register();
            Items.HealingStaff.Register();
            Items.WraithArmor.Register();
            // patche Harmony (LeveledRequirements: koszty ulepszen per poziom)
            new Harmony(PluginGUID).PatchAll(typeof(VanilaMagic).Assembly);
            PrefabManager.OnVanillaPrefabsAvailable += SetupBlueMushroom;
            DungeonManager.OnVanillaRoomsAvailable += () => MushroomCaveRooms.AddMushroomRooms(BlueMushroomPrefab);
#if DEBUG
            // komendy deweloperskie - tylko w buildzie Debug, nie trafiaja do wydania
            CommandManager.Instance.AddConsoleCommand(new SpawnMushroomRoomCommand());
            CommandManager.Instance.AddConsoleCommand(new WhichRoomCommand());
            CommandManager.Instance.AddConsoleCommand(new NearSpawnsCommand());
            CommandManager.Instance.AddConsoleCommand(new BlinkSpawnCommand());
            CommandManager.Instance.AddConsoleCommand(new MushroomOnlyCommand());
            CommandManager.Instance.AddConsoleCommand(new Items.DumpStaffCommand());
            CommandManager.Instance.AddConsoleCommand(new Items.WraithTintCommand());
            CommandManager.Instance.AddConsoleCommand(new Items.WandIconCommand());
#endif
            // To learn more about Jotunn's features, go to
            // https://valheim-modding.github.io/Jotunn/tutorials/overview.html
        }
        private void SetupBlueMushroom()
        {
            // Gra ma nieużywane assety niebieskiego grzybka (MushroomBlue, Pickable_Mushroom_blue
            // i ikonkę mushroomblue) - używamy ich zamiast własnego modelu.
            // Bez odpinania od eventu: waniliowe prefaby mogą być przeładowane między sesjami,
            // a ponowne nałożenie tych samych wartości jest nieszkodliwe.
            GameObject mushroomItem = PrefabManager.Instance.GetPrefab("MushroomBlue");
            BlueMushroomPrefab = PrefabManager.Instance.GetPrefab("Pickable_Mushroom_blue");
            if (!mushroomItem || !BlueMushroomPrefab)
            {
                Jotunn.Logger.LogWarning("Nie znaleziono waniliowych prefabów niebieskiego grzybka (MushroomBlue / Pickable_Mushroom_blue)");
                return;
            }

            // Nieużywany item nie ma sensownych statystyk - robimy z niego jedzenie dające eitr
            var shared = mushroomItem.GetComponent<ItemDrop>().m_itemData.m_shared;
            shared.m_itemType = ItemDrop.ItemData.ItemType.Consumable;
            shared.m_name = "$item_bluemushroom";
            shared.m_description = "$item_bluemushroom_description";
            shared.m_food = 15f;
            shared.m_foodStamina = 10f;
            shared.m_foodEitr = 25f;
            shared.m_foodRegen = 1f;
            shared.m_foodBurnTime = 1200f;

            BlueMushroomPrefab.GetComponent<Pickable>().m_itemPrefab = mushroomItem;
        }

    }
}