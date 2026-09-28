using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Ikony rozdzek renderowane z ICH WLASNYCH modeli, zamiast pozyczania ikon waniliowych
    /// kosturow (te nie pasowaly - pokazywaly zupelnie inny przedmiot niz ten w rece).
    /// Waniliowe ikony Valheima to po prostu rendery modeli, wiec Jotunn ma na to gotowca:
    /// RenderManager z <see cref="RenderManager.IsometricRotation"/> - tym samym kadrem,
    /// ktorego uzywa gra.
    ///
    /// Render jest synchroniczny (EnqueueRender jest w 2.30.2 oznaczone jako przestarzale).
    /// Gdyby sie nie udal, item zostaje przy ikonie bazowej z ItemConfig - nigdy nie konczymy
    /// z pustym kwadratem w ekwipunku.
    ///
    /// Kadr kazdej rozdzki stroi sie na zywo komenda <c>wandicon</c> - bez przebudowy moda.
    /// </summary>
    internal static class RenderedIcons
    {
        private const int Size = 64;

        /// <summary>Kadr renderu: obrot modelu, ciasnosc obiektywu i odsuniecie kamery.</summary>
        internal struct Shot
        {
            public Vector3 Euler;
            public float FieldOfView;
            public float Distance;

            // Skret wokol WLASNEJ osi rozdzki (modele lezą wzdluz Z, czubkiem w +Z).
            // Decyduje tylko o tym, ktora strona glowicy patrzy w kamere - kierunku rozdzki
            // NIE zmienia, bo to jest wlasnie jej os. Stad wczesniejsza probe odwrocenia
            // przez "+180 na Z" widac bylo jako obrot glowicy, ale nie jako zmiane kierunku.
            private const float RollZ = 35f + 180f;

            /// <summary>Kadr izometryczny gry (Euler 23/51/25.8) plus skret wokol osi rozdzki.</summary>
            private static Quaternion Framing =>
                Quaternion.Euler(RenderManager.IsometricRotation.eulerAngles + new Vector3(0f, 0f, RollZ));

            public static Shot Default => new Shot
            {
                // Glowica ma celowac w gore, a nie w dol. Obrot o 180 wokol LOKALNEGO Y modelu
                // (czyli w poprzek rozdzki) przerzuca +Z na -Z, wiec konce zamieniaja sie
                // miejscami; kadr nalozony po nim zostaje ten sam co wczesniej.
                Euler = (Framing * Quaternion.AngleAxis(180f, Vector3.up)).eulerAngles,
                FieldOfView = 0.5f,
                Distance = 1f,
            };
        }

        private static readonly Dictionary<string, Shot> Shots = new Dictionary<string, Shot>();

        /// <summary>Zglasza item do renderu. Wolane po zbudowaniu prefabu, przy rejestracji.</summary>
        public static void Register(string prefabName)
        {
            if (!Shots.ContainsKey(prefabName)) Shots[prefabName] = Shot.Default;
            Render(prefabName);
        }

        public static IEnumerable<string> Registered => Shots.Keys;

        public static bool TryGetShot(string prefabName, out Shot shot) => Shots.TryGetValue(prefabName, out shot);

        /// <summary>Podmienia kadr i renderuje od nowa (uzywane przez komende wandicon).</summary>
        public static bool Retake(string prefabName, Shot shot)
        {
            if (!Shots.ContainsKey(prefabName)) return false;
            Shots[prefabName] = shot;
            return Render(prefabName);
        }

        private static bool Render(string prefabName)
        {
            var prefab = PrefabManager.Instance.GetPrefab(prefabName);
            var drop = prefab ? prefab.GetComponent<ItemDrop>() : null;
            if (!drop)
            {
                Jotunn.Logger.LogWarning($"RenderedIcons: brak itemu {prefabName}");
                return false;
            }

            var shot = Shots[prefabName];
            var request = new RenderManager.RenderRequest(prefab)
            {
                Width = Size,
                Height = Size,
                Rotation = Quaternion.Euler(shot.Euler),
                FieldOfView = shot.FieldOfView,
                DistanceMultiplier = shot.Distance,
                // Bez cache - inaczej strojenie komenda wandicon oddawaloby stary render
                UseCache = false,
            };

            var sprite = RenderManager.Instance.Render(request);
            if (!sprite)
            {
                Jotunn.Logger.LogWarning($"RenderedIcons: render {prefabName} nie wyszedl - zostaje ikona bazowa z ItemConfig");
                return false;
            }

            sprite.name = prefabName + "_rendered";
            drop.m_itemData.m_shared.m_icons = new[] { sprite };
            Jotunn.Logger.LogInfo($"RenderedIcons: {prefabName} ikona z modelu, kadr {shot.Euler} fov={shot.FieldOfView} dist={shot.Distance}");
            return true;
        }
    }
}
