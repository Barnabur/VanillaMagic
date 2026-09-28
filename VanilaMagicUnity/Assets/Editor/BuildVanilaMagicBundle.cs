using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Buduje bundle "vanilamagic" z wlasnych modeli/tekstur i kopiuje go do VanilaMagic/Assets,
/// skad idzie do DLL-a jako EmbeddedResource (Jotunn AssetUtils.LoadAssetBundleFromResources).
///
/// Do bundla ida TYLKO meshe i tekstury. Prefaby (ShocklateSmoothie/VikingCupcake) sa w Unity
/// wylacznie podgladem: ich materialy stoja na shaderze Standard z ripu, a referencje do efektow
/// wanilii sa zerwane. W grze mod klonuje waniliowy item przez Jotunn i podmienia w nim
/// mesh + albedo (Items/ModAssets.cs), wiec shader i efekty zostaja waniliowe.
/// </summary>
public static class BuildVanilaMagicBundle
{
    public const string BundleName = "vanilamagic";

    static readonly string[] AssetPaths =
    {
        "Assets/GhostShake/GhostShake_mesh.asset",
        "Assets/GhostShake/GhostShake_d.png",
        "Assets/GhostShake/VikingCupcake_body_mesh.asset",
        "Assets/GhostShake/VikingCupcake_body_D.png",
        "Assets/GhostShake/VikingCupcake_frosted_D.png",
        "Assets/WraithArmor/WraithHood_mesh.asset", // kaptur Fenrisa z kolnierzem odsunietym nad peleryne (CapeLab w ripie)
        "Assets/WraithArmor/WraithCapeStrip_mesh.asset", // gorny pas peleryny z wagami kaptura (CapeLab.BuildCapeStrip) - bez fizyki (nieuzywany)
        "Assets/WraithArmor/WraithCape_mesh.asset", // cape2 z wagami kaptura w gornej czesci (CapeLab.ReweightCapeToHood), READABLE - MagicaCloth buduje w runtime
    };

    [MenuItem("VanilaMagic/Build asset bundle")]
    public static void Build()
    {
        foreach (var path in AssetPaths)
        {
            var importer = AssetImporter.GetAtPath(path);
            if (importer == null) throw new FileNotFoundException("Brak assetu: " + path);
            importer.assetBundleName = BundleName;
        }
        AssetDatabase.SaveAssets();

        var outDir = Path.Combine(Application.dataPath, "..", "AssetBundles");
        Directory.CreateDirectory(outDir);
        var manifest = BuildPipeline.BuildAssetBundles(outDir,
            BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode,
            BuildTarget.StandaloneWindows64);
        if (manifest == null) throw new System.Exception("BuildAssetBundles zwrocil null");

        var built = Path.Combine(outDir, BundleName);
        var target = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "VanilaMagic", "Assets", BundleName));
        File.Copy(built, target, true);
        Debug.Log($"Bundle {BundleName}: {new FileInfo(target).Length / 1024} KB -> {target}\n" +
                  string.Join("\n", manifest.GetAllAssetBundles()));
    }
}
