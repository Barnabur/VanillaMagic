using HarmonyLib;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Jedzenie bez zdrowia (m_food == 0), ale ze stamina/eitr - np. Ghostshake.
    ///
    /// Wanilia gate'uje jedzenie wylacznie po m_food: Player.CanConsumeItem sprawdza CanEat tylko
    /// gdy m_food > 0, a Player.ConsumeItem wola EatFood tylko gdy m_food > 0. Item z m_food == 0
    /// da sie wiec "zjesc" (znika z ekwipunku), ale nie trafia na liste jedzenia. Samo EatFood
    /// z m_food == 0 dziala poprawnie (Food.m_health = 0, reszta normalnie), wiec wystarczy
    /// przejac oba gate'y dla takich itemow. Zaden waniliowy pokarm nie ma m_food == 0.
    /// </summary>
    internal static class HealthlessFood
    {
        private static bool IsHealthless(ItemDrop.ItemData item)
        {
            var shared = item?.m_shared;
            return shared != null
                && shared.m_itemType == ItemDrop.ItemData.ItemType.Consumable
                && shared.m_food <= 0f
                && (shared.m_foodStamina > 0f || shared.m_foodEitr > 0f);
        }

        /// <summary>Bez tego item bez hp dalby sie jesc w kolko (brak sprawdzenia CanEat / "$msg_nomore").</summary>
        [HarmonyPatch(typeof(Player), nameof(Player.CanConsumeItem))]
        private static class CanConsumePatch
        {
            private static void Postfix(Player __instance, ItemDrop.ItemData item, ref bool __result)
            {
                if (!__result || !IsHealthless(item)) return;
                __result = __instance.CanEat(item, showMessages: true);
            }
        }

        /// <summary>Wanilia pomija EatFood przy m_food == 0 - robimy to samo, co ona dla zwyklego jedzenia.</summary>
        [HarmonyPatch(typeof(Player), nameof(Player.ConsumeItem))]
        private static class ConsumePatch
        {
            private static bool Prefix(Player __instance, Inventory inventory, ItemDrop.ItemData item, bool checkWorldLevel, ref bool __result)
            {
                if (!IsHealthless(item)) return true;

                __result = false;
                if (!__instance.CanConsumeItem(item, checkWorldLevel)) return false;
                if (item.m_shared.m_consumeStatusEffect)
                {
                    __instance.GetSEMan().AddStatusEffect(item.m_shared.m_consumeStatusEffect, resetTime: true);
                }
                __instance.EatFood(item);
                inventory.RemoveOneItem(item);
                __result = true;
                return false;
            }
        }

        /// <summary>
        /// Jedzenie prosto z ziemi (ItemDrop.Eat) ma ten sam gate m_food > 0 - kopia waniliowej
        /// metody bez niego. m_wnt to prywatne pole ustawiane w Awake z GetComponent&lt;WearNTear&gt;.
        /// </summary>
        [HarmonyPatch(typeof(ItemDrop), nameof(ItemDrop.Eat))]
        private static class GroundEatPatch
        {
            private static bool Prefix(ItemDrop __instance, ref bool __result)
            {
                var item = __instance.m_itemData;
                if (!IsHealthless(item)) return true;

                __result = false;
                var player = Player.m_localPlayer;
                if (!player || !player.CanConsumeItem(item)) return false;
                if (!__instance.CanEat())
                {
                    __instance.CancelInvoke("EatUpdate");
                    __instance.InvokeRepeating("EatUpdate", 0.05f, 0.05f);
                    __instance.RequestOwn();
                    return false;
                }
                if (item.m_shared.m_consumeStatusEffect)
                {
                    player.GetSEMan().AddStatusEffect(item.m_shared.m_consumeStatusEffect, resetTime: true);
                }
                player.EatFood(item);
                var wnt = __instance.GetComponent<WearNTear>();
                if (wnt) wnt.Remove(blockDrop: true);
                else __instance.GetComponent<ZNetView>()?.Destroy();
                __result = true;
                return false;
            }
        }
    }
}
