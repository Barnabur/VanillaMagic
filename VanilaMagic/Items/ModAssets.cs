using System;
using System.Linq;
using UnityEngine;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Wlasne modele i tekstury z bundla "vanilamagic" (EmbeddedResource Assets/vanilamagic,
    /// budowany w VanilaMagicUnity menu VanilaMagic > Build asset bundle).
    ///
    /// Bundle niesie TYLKO meshe i albedo. Item w grze to nadal klon waniliowego prefabu
    /// (Jotunn CustomItem z baseName) - dzieki temu ItemDrop, efekty, iskierki i shader
    /// materialu zostaja waniliowe, a my podmieniamy tylko geometrie i teksture.
    /// Materialy z Unity NIE nadaja sie do gry: stoja na shaderze Standard z ripu.
    /// </summary>
    internal static class ModAssets
    {
        private const string BundleName = "vanilamagic";
        private static AssetBundle _bundle;
        private static bool _loadAttempted;

        private static AssetBundle Bundle
        {
            get
            {
                if (_loadAttempted) return _bundle;
                _loadAttempted = true;
                try
                {
                    // UWAGA: nie AssetUtils.LoadAssetBundleFromResources - ten laduje przez LoadFromStream
                    // i zamyka strumien zasobu, a AssetBundle.LoadAsset seekuje po nim przy kazdym
                    // pozniejszym wywolaniu ("ManagedStream object must be readable" -> crash gry).
                    // Bundle jest maly (~100 KB), wiec wczytujemy go w calosci do pamieci.
                    var assembly = typeof(VanilaMagic).Assembly;
                    var resource = assembly.GetManifestResourceNames()
                        .FirstOrDefault(n => n.EndsWith("." + BundleName, StringComparison.OrdinalIgnoreCase));
                    if (resource == null)
                    {
                        Jotunn.Logger.LogWarning($"ModAssets: brak bundla {BundleName} w zasobach DLL");
                        return null;
                    }
                    byte[] bytes;
                    using (var stream = assembly.GetManifestResourceStream(resource))
                    using (var memory = new System.IO.MemoryStream())
                    {
                        stream.CopyTo(memory);
                        bytes = memory.ToArray();
                    }
                    _bundle = AssetBundle.LoadFromMemory(bytes);
                    if (_bundle) Jotunn.Logger.LogDebug($"ModAssets: bundle {BundleName} zaladowany ({bytes.Length / 1024} KB, {_bundle.GetAllAssetNames().Length} assetow)");
                    else Jotunn.Logger.LogError($"ModAssets: AssetBundle.LoadFromMemory zwrocil null dla {BundleName} ({bytes.Length} B) - bundle z innej wersji Unity?");
                }
                catch (Exception ex)
                {
                    Jotunn.Logger.LogError($"ModAssets: ladowanie bundla {BundleName} nie powiodlo sie: {ex}");
                }
                return _bundle;
            }
        }

        public static T Load<T>(string name) where T : UnityEngine.Object
        {
            var bundle = Bundle;
            if (!bundle) return null;
            var asset = bundle.LoadAsset<T>(name);
            if (!asset)
            {
                // Bundle trzyma assety pod pelna sciezka (assets/ghostshake/ghostshake_mesh.asset) - szukamy po nazwie pliku
                var path = bundle.GetAllAssetNames().FirstOrDefault(p =>
                    string.Equals(System.IO.Path.GetFileNameWithoutExtension(p), name, StringComparison.OrdinalIgnoreCase));
                if (path != null) asset = bundle.LoadAsset<T>(path);
            }
            if (!asset) Jotunn.Logger.LogWarning($"ModAssets: brak {typeof(T).Name} '{name}' w bundlu {BundleName}");
            return asset;
        }

        /// <summary>
        /// Podmienia w klonie waniliowego itemu mesh (wszystkie MeshFiltery - wanilia trzyma ten
        /// sam model pod "attach" i "equipoffset") i albedo (kopia waniliowego materialu, wiec
        /// shader i mapa normalnych zostaja). Zwraca false, gdy bundla/assetow brak - wolajacy
        /// moze wtedy zostac przy dotychczasowym runtime'owym przemalowaniu.
        /// </summary>
        public static bool ApplyModel(GameObject prefab, string meshName, string albedoName)
        {
            var mesh = Load<Mesh>(meshName);
            var albedo = Load<Texture2D>(albedoName);
            if (!mesh || !albedo) return false;

            var filters = 0;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                filter.sharedMesh = mesh;
                filters++;
            }

            SwapAlbedo(prefab, albedo, albedoName);
            Jotunn.Logger.LogDebug($"ModAssets: {prefab.name} <- {meshName} + {albedoName} ({filters} MeshFilter)");
            return true;
        }

        /// <summary>
        /// Sama tekstura, mesh bez zmian - do patchowania WANILIOWYCH prefabow (np. niebieskie
        /// krysztalki cukru na Frosted Sweetbread). Idempotentne: material juz podmieniony
        /// (po sufiksie w nazwie) jest pomijany, bo patche wanilii nakladaja sie co wejscie do swiata.
        /// </summary>
        public static bool ApplyAlbedo(GameObject prefab, string albedoName)
        {
            var albedo = Load<Texture2D>(albedoName);
            if (!albedo) return false;
            var swapped = SwapAlbedo(prefab, albedo, albedoName);
            if (swapped > 0) Jotunn.Logger.LogDebug($"ModAssets: {prefab.name} <- {albedoName} ({swapped} materialow)");
            return true;
        }

        private static int SwapAlbedo(GameObject prefab, Texture2D albedo, string albedoName)
        {
            var suffix = "_" + albedoName;
            var swapped = 0;
            foreach (var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.sharedMaterials = renderer.sharedMaterials
                    .Select(mat =>
                    {
                        if (!mat || mat.name.EndsWith(suffix, StringComparison.Ordinal)) return mat;
                        var copy = new Material(mat) { name = mat.name + suffix };
                        copy.mainTexture = albedo;
                        swapped++;
                        return copy;
                    })
                    .ToArray();
            }
            return swapped;
        }
    }
}
