using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Jednoreczna animacja bloku dla rozdzek, ktore MUSZA zostac w stanie animacji "Staves".
    ///
    /// Dlaczego nie po prostu m_animationState = OneHanded: w Player_animator stan
    /// "staff_rapidfire" (seria StaffIceShards) w warstwie bazowej nie ma przejscia z ANY -
    /// wchodzi sie do niego tylko ze stanu ruchu kostura (statei == 12). Z ANY odpala sie
    /// jedynie w warstwie "upperbody". Przy statei == OneHanded warstwa bazowa nigdy nie jest
    /// w stanie z tagiem "attack", wiec Humanoid.InAttack() == false i Player co klatke
    /// restartuje atak (Stop + nowy Attack.Start) - strzaly leca w losowe strony (m.in. pionowo).
    /// Pojedynczy "staff_fireball" ma przejscia z ANY w warstwie bazowej, wiec tam OneHanded dziala.
    ///
    /// Rozwiazanie: item zostaje "Staves", a tu podmieniamy parametr animatora "statei" na
    /// OneHanded wylacznie na czas trwania bloku (przejscie Movement -> "One handed Blocking"
    /// wymaga blocking && statei == 1). Po puszczeniu bloku wracamy do Staves.
    /// </summary>
    internal static class OneHandedBlock
    {
        private static readonly HashSet<string> Names = new HashSet<string>();
        private static readonly ConditionalWeakTable<Humanoid, State> States = new ConditionalWeakTable<Humanoid, State>();
        private static readonly int StateHash = ZSyncAnimation.GetHash("statei");

        private sealed class State
        {
            public ZSyncAnimation Anim;
            public int Applied = -1;
        }

        /// <summary>Rejestruje item (po m_shared.m_name), ktory ma dostac jednoreczny blok.</summary>
        public static void Register(string sharedName)
        {
            Names.Add(sharedName);
        }

        private static bool Wants(Humanoid humanoid)
        {
            var right = humanoid.GetCurrentWeapon();
            return right != null && Names.Contains(right.m_shared.m_name);
        }

        [HarmonyPatch(typeof(Humanoid), "UpdateBlock")]
        private static class UpdateBlockPatch
        {
            // UpdateBlock biegnie tylko u wlasciciela (CustomFixedUpdate), wiec SetInt idzie
            // przez ZSyncAnimation do ZDO i reszta klientow dostaje ten sam statei.
            private static void Postfix(Humanoid __instance)
            {
                if (!Wants(__instance)) return;
                var state = States.GetValue(__instance, h => new State { Anim = h.GetComponent<ZSyncAnimation>() });
                if (!state.Anim) return;

                // IsBlocking() jest false w trakcie ataku, wiec seria zawsze leci ze statei == Staves
                var desired = (int)(__instance.IsBlocking()
                    ? ItemDrop.ItemData.AnimationState.OneHanded
                    : ItemDrop.ItemData.AnimationState.Staves);
                if (state.Applied == desired) return;
                state.Applied = desired;
                state.Anim.SetInt(StateHash, desired);
            }
        }

        [HarmonyPatch(typeof(Humanoid), "SetupAnimationState")]
        private static class SetupAnimationStatePatch
        {
            // zmiana ekwipunku ustawia statei od nowa z itemu - zapominamy, co sami wpisalismy
            private static void Postfix(Humanoid __instance)
            {
                if (States.TryGetValue(__instance, out var state)) state.Applied = -1;
            }
        }
    }
}
