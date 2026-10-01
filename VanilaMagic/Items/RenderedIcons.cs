using System;
using System.Collections.Generic;
using System.IO;
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

        /// <summary>
        /// Renderuje ikone tym samym kadrem co w grze, ale w podanym rozmiarze, i zapisuje jako PNG
        /// (przezroczyste tlo) - material do grafik poza gra, np. ikony moda na Thunderstore.
        /// </summary>
        public static string Export(string prefabName, int size, string directory)
        {
            var prefab = PrefabManager.Instance.GetPrefab(prefabName);
            if (!prefab || !Shots.TryGetValue(prefabName, out var shot)) return null;

            var sprite = RenderSprite(prefab, shot, size);
            if (!sprite) return null;

            var encode = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule")
                ?.GetMethod("EncodeToPNG", new[] { typeof(Texture2D) });
            if (encode == null)
            {
                Jotunn.Logger.LogWarning("RenderedIcons: brak ImageConversion.EncodeToPNG w runtime");
                return null;
            }

            var png = (byte[])encode.Invoke(null, new object[] { sprite.texture });
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"{prefabName}_{size}.png");
            File.WriteAllBytes(path, png);
            return path;
        }

        private static Sprite RenderSprite(GameObject prefab, Shot shot, int size)
        {
            var request = new RenderManager.RenderRequest(prefab)
            {
                Width = size,
                Height = size,
                Rotation = Quaternion.Euler(shot.Euler),
                FieldOfView = shot.FieldOfView,
                DistanceMultiplier = shot.Distance,
                // Bez cache - inaczej strojenie komenda wandicon oddawaloby stary render
                UseCache = false,
            };
            return RenderManager.Instance.Render(request);
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
            var sprite = RenderSprite(prefab, shot, Size);
            if (!sprite)
            {
                Jotunn.Logger.LogWarning($"RenderedIcons: render {prefabName} nie wyszedl - zostaje ikona bazowa z ItemConfig");
                return false;
            }

            sprite.name = prefabName + "_rendered";
            drop.m_itemData.m_shared.m_icons = new[] { sprite };
            Jotunn.Logger.LogDebug($"RenderedIcons: {prefabName} ikona z modelu, kadr {shot.Euler} fov={shot.FieldOfView} dist={shot.Distance}");
            return true;
        }
    }
}
