using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Przemalowywanie waniliowych tekstur i ikon w runtime - wspolne dla itemow skladanych
    /// z waniliowych czesci (GhostShake, CrystalSugar). Waniliowe tekstury nie sa czytelne
    /// z CPU, wiec kopia zawsze idzie przez RenderTexture (Blit + ReadPixels).
    /// WildBerry i WraithArmor maja wlasne warianty tych metod - tam operacja na pikselach
    /// jest wpisana w konkretny model przemalowania.
    /// </summary>
    internal static class RuntimeTextures
    {
        private static readonly Dictionary<string, Sprite> IconCache = new Dictionary<string, Sprite>();

        /// <summary>
        /// Wlasna ikona z EmbeddedResource <c>Assets/&lt;name&gt;.png</c> (64x64 RGBA).
        /// Wynik jest cache'owany - patche waniliowych prefabow nakladaja sie co wejscie
        /// do swiata i nie ma po co dekodowac tego samego PNG-a w kolko.
        /// </summary>
        public static Sprite LoadIcon(string name)
        {
            if (IconCache.TryGetValue(name, out var cached)) return cached;

            var sprite = DecodeIcon(name);
            IconCache[name] = sprite;
            return sprite;
        }

        private static Sprite DecodeIcon(string name)
        {
            var resource = $"VanilaMagic.Assets.{name}.png";
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                if (stream == null)
                {
                    Jotunn.Logger.LogWarning($"RuntimeTextures: brak zasobu ikony '{resource}' - zostaje ikona bazowa");
                    return null;
                }

                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);

                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = name };
                if (!LoadPng(tex, bytes))
                {
                    Jotunn.Logger.LogWarning($"RuntimeTextures: nie udalo sie zdekodowac ikony '{name}' - zostaje ikona bazowa");
                    return null;
                }
                return Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
            }
        }

        /// <summary>
        /// ImageConversion.LoadImage przez refleksje: UnityEngine.ImageConversionModule z gry jest
        /// zbudowany pod netstandard 2.1 i bezposrednia referencja nie kompiluje sie pod net48.
        /// </summary>
        private static bool LoadPng(Texture2D target, byte[] png)
        {
            var imageConversion = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var loadImage = imageConversion?.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });
            if (loadImage == null)
            {
                Jotunn.Logger.LogWarning("RuntimeTextures: brak ImageConversion.LoadImage w runtime");
                return false;
            }
            return (bool)loadImage.Invoke(null, new object[] { target, png });
        }

        public static Texture2D CopyReadable(Texture source, string name)
        {
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, source.mipmapCount > 1)
                {
                    name = name,
                    filterMode = source.filterMode,
                    wrapMode = source.wrapMode,
                    anisoLevel = source.anisoLevel,
                };
                copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                return copy;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        public static Texture2D Recolor(Texture source, Func<Color, Color> op, string name)
        {
            return Recolor(source, (x, y, c) => op(c), name);
        }

        /// <summary>Wariant ze wspolrzednymi piksela - dla wzorow (np. posypki), nie samego koloru.</summary>
        public static Texture2D Recolor(Texture source, Func<int, int, Color, Color> op, string name)
        {
            var readable = CopyReadable(source, name);
            var pixels = readable.GetPixels();
            var width = readable.width;
            for (var i = 0; i < pixels.Length; i++) pixels[i] = op(i % width, i / width, pixels[i]);
            readable.SetPixels(pixels);
            readable.Apply(true, true);
            return readable;
        }

        /// <summary>Ikona = wycinek atlasu ikon przemalowany ta sama operacja co tekstury.</summary>
        public static Sprite RecolorIcon(Sprite sprite, Func<Color, Color> op, string name)
        {
            return RecolorIcon(sprite, (x, y, c) => op(c), name);
        }

        /// <summary>Wariant ze wspolrzednymi piksela (liczonymi od lewego dolnego rogu wycinka).</summary>
        public static Sprite RecolorIcon(Sprite sprite, Func<int, int, Color, Color> op, string name)
        {
            if (!sprite) return null;
            Texture2D atlas = null;
            try
            {
                atlas = CopyReadable(sprite.texture, "tmp_atlas");
                var r = sprite.textureRect;
                var x = Mathf.RoundToInt(r.x);
                var y = Mathf.RoundToInt(r.y);
                var w = Mathf.RoundToInt(r.width);
                var h = Mathf.RoundToInt(r.height);
                var pixels = atlas.GetPixels(x, y, w, h);
                for (var i = 0; i < pixels.Length; i++) pixels[i] = op(i % w, i / w, pixels[i]);
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, filterMode = sprite.texture.filterMode };
                tex.SetPixels(pixels);
                tex.Apply(false, true);
                return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"RuntimeTextures: przemalowanie ikony {name} nie powiodlo sie ({ex.Message}) - zostaje oryginal");
                return sprite;
            }
            finally
            {
                if (atlas) UnityEngine.Object.Destroy(atlas);
            }
        }
    }
}
