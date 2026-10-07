using System;
using Jotunn.Managers;
using UnityEngine;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Nasze jedzenie do postawienia Taca (waniliowy item Feaster, "Serving Tray").
    ///
    /// Wanilia robi to bez osobnych prefabow: item jedzenia ma na sobie Piece + WearNTear,
    /// a jego Piece.m_resources to on sam (1 szt.). Taca ma PieceTable "_FeasterPieceTable"
    /// z ~120 takimi itemami. Postawiony item traci Rigidbody (ItemDrop.MakePiece), staje sie
    /// elementem, a Use na nim je go (ItemDrop.Interact -> Eat). Zdjac go mozna tylko Taca
    /// (m_canRemoveFeasts) - mlotek feastow nie rusza.
    ///
    /// Nasze klony (CustomItem z waniliowej bazy) dziedzicza Piece bazy: Unity przepina
    /// odwolanie m_resItem na wlasne ItemDrop klona, ale nazwa/opis/ikona elementu zostaja
    /// waniliowe - stad SyncPiece. MushroomBlue to nieuzywany waniliowy item bez Piece i
    /// WearNTear - dostaje je skopiowane z czerwonego grzyba (AddPieceComponents).
    /// Podmienione ikony waniliowego Eyescreama i Frosted Sweetbreada tez trzeba przepisac
    /// na Piece, inaczej w menu Tacy wisialy stare.
    /// </summary>
    internal static class ServingTray
    {
        private const string TrayName = "Feaster";
        // Wzor komponentow elementu dla niebieskiego grzyba - ten sam model (Boletus_edulis01)
        private const string PieceTemplate = "Mushroom";

        private static readonly string[] OurFoods =
        {
            WildBerry.ItemName,
            "MushroomBlue",
            GhostShake.ItemName,
            SugarFoods.SprinkledEyescreamName,
            SugarFoods.SweetbreadName,
            MushroomSoup.ItemName,
        };

        // Waniliowe itemy z podmieniona ikona (SugarFoods.ReplaceVanillaIcon) - sa juz na Tacy
        private static readonly string[] RetouchedVanilla = { "Eyescream", "VikingCupcake" };

        public static void Register()
        {
            // Bez odpinania: patch waniliowej tablicy i prefabu MushroomBlue - prefaby moga byc
            // przeladowane miedzy sesjami, wszystko ponizej jest idempotentne.
            // Rejestrowac PO itemach - ich Create musi sie wykonac wczesniej w tym samym evencie.
            PrefabManager.OnVanillaPrefabsAvailable += Patch;
        }

        private static void Patch()
        {
            try
            {
                // Tablica wprost z itemu Tacy (_FeasterPieceTable) - dokladnie ta, z ktorej gra buduje menu
                var tray = PrefabManager.Instance.GetPrefab(TrayName)?.GetComponent<ItemDrop>();
                var table = tray ? tray.m_itemData.m_shared.m_buildPieces : null;
                if (!table)
                {
                    Jotunn.Logger.LogWarning($"ServingTray: brak tablicy elementow itemu {TrayName} - jedzenie nie trafi na Tace");
                    return;
                }

                foreach (var name in OurFoods)
                {
                    var prefab = PrefabManager.Instance.GetPrefab(name);
                    if (!prefab)
                    {
                        Jotunn.Logger.LogWarning($"ServingTray: brak prefabu {name}");
                        continue;
                    }
                    if (!prefab.GetComponent<Piece>() && !AddPieceComponents(prefab)) continue;

                    SyncPiece(prefab);
                    // Koszt postawienia = 1 sztuka tego itemu (zwracana przy zdjeciu, jesli nie zjedzona)
                    var drop = prefab.GetComponent<ItemDrop>();
                    prefab.GetComponent<Piece>().m_resources = new[]
                    {
                        new Piece.Requirement { m_resItem = drop, m_amount = 1, m_amountPerLevel = 1, m_recover = true },
                    };
                    if (!table.m_pieces.Contains(prefab)) table.m_pieces.Add(prefab);
                }

                foreach (var name in RetouchedVanilla)
                {
                    var prefab = PrefabManager.Instance.GetPrefab(name);
                    if (prefab && prefab.GetComponent<Piece>()) SyncPiece(prefab);
                }
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"ServingTray: podpiecie jedzenia pod Tace nie powiodlo sie: {ex}");
            }
        }

        /// <summary>Nazwa, opis i ikona elementu (menu Tacy) = te z itemu.</summary>
        private static void SyncPiece(GameObject prefab)
        {
            var piece = prefab.GetComponent<Piece>();
            var shared = prefab.GetComponent<ItemDrop>().m_itemData.m_shared;

            piece.m_name = shared.m_name;
            piece.m_description = shared.m_description;
            if (shared.m_icons != null && shared.m_icons.Length > 0) piece.m_icon = shared.m_icons[0];
        }

        /// <summary>
        /// Kopiuje Piece i WearNTear z waniliowego grzyba (JsonUtility zachowuje referencje
        /// w obrebie sesji), po czym przepina to, co wskazywalo na tamten prefab.
        /// </summary>
        private static bool AddPieceComponents(GameObject prefab)
        {
            var template = PrefabManager.Instance.GetPrefab(PieceTemplate);
            var templatePiece = template ? template.GetComponent<Piece>() : null;
            var templateWnt = template ? template.GetComponent<WearNTear>() : null;
            if (!templatePiece || !templateWnt)
            {
                Jotunn.Logger.LogWarning($"ServingTray: {PieceTemplate} nie ma Piece/WearNTear - {prefab.name} nie trafi na Tace");
                return false;
            }

            // Stany zuzycia wskazuja dziecko "attach" (caly model) - jak u wzoru
            var attach = prefab.transform.Find("attach")?.gameObject;
            if (!attach)
            {
                Jotunn.Logger.LogWarning($"ServingTray: {prefab.name} nie ma dziecka 'attach'");
                return false;
            }

            var wnt = prefab.GetComponent<WearNTear>();
            if (!wnt) wnt = prefab.AddComponent<WearNTear>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(templateWnt), wnt);
            wnt.m_new = attach;
            wnt.m_worn = attach;
            wnt.m_broken = attach;

            var piece = prefab.AddComponent<Piece>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(templatePiece), piece);

            Jotunn.Logger.LogInfo($"ServingTray: {prefab.name} dostal Piece + WearNTear z {PieceTemplate}");
            return true;
        }
    }
}
