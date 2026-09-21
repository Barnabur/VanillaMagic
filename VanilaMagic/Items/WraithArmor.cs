using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Szaty Widma - zbroja maga na etap Bagna, kitbash z waniliowych assetow (bez modelowania):
    /// - kaptur/szata/nogawice: klony HelmetFenring / ArmorFenringChest / ArmorFenringLegs (set Fenris,
    ///   futrzany, bez zlotych klamer - set maga byl "za endgamowy"). Siatki sa juz zriggowane do
    ///   szkieletu gracza (dzieci attach_skin), zmieniamy tylko materialy: tekstury futra
    ///   (FenringArmor_d, FenringArmorChest_d, FenringArmorLegs_d) kolorowane w runtime na granat
    ///   Wraitha (luminancja * tint, patrz Colorize); nakladka na cialo (m_armorMaterial) tez,
    ///   bo VisEquipment kopiuje z niej tylko tekstury, nie kolory. Staty i set effect sa nasze
    ///   (bonus Fenrisa do piesci NIE przechodzi - siedzi w SetEffect_FenringArmor, ktory podmieniamy).
    /// - peleryna: klon CapeTrollHide (Cloth + GlobalWind zostaja; CapeWolf odpadl - "tragicznie pasowal").
    ///   Albedo skladane w runtime w ukladzie UV siatki cape2 (czytelnej z CPU, 96 wierzch.): w srodku
    ///   kafelek z tekstury nogawic Fenrisa (ciemny splot z jasnymi pasami, pokolorowany, zmirrorowany),
    ///   a przy krawedziach siatki (poza kolnierzem, v > 0.8) alfa wycina postrzepione pasy - jak Fenris,
    ///   ktorego "strzepy" tez sa alfa, nie geometria. Material przelaczony na cutout (_Mode 1 + keywordy
    ///   z materialu "wraith"). Strojenie: `wraithtint capetile sx sy`, `wraithtint tatter szerokosc_m`.
    /// - lancuchy: dzieci "attach_LeftHand"/"attach_RightHand" (szata) i "attach_Spine2" (peleryna).
    ///   VisEquipment.AttachArmor podpina kazde dziecko "attach_[Kosc]" pod kosc gracza o tej nazwie.
    ///   Ogniwa = siatka itemu Chain (chain.002_Torus.002, material "wraith"), kajdan = torus z TubeBuilder,
    ///   kolysanie = DanglingChain (verlet, wiatr z EnvMan) na rekach; na pelerynie ClothChain (przyklejone
    ///   do wierzcholkow tkaniny Cloth, wiec zawsze na wierzchu plaszcza), rozne dlugosci.
    /// - ulepszenia rozlozone na biomy przez LeveledRequirements (Bagno 1-2 + siersc Fenrisa z jaskin lodowych, Gory 3, Rowniny 4;
    ///   peleryna Gory 1-2, Rowniny 3-4).
    /// Balans (pancerz, regen eitr, koszty) to PLACEHOLDERY do strojenia w grze.
    /// </summary>
    internal static class WraithArmor
    {
        public const string HoodName = "WraithHood";
        public const string RobeName = "WraithRobe";
        public const string LegsName = "WraithLegs";
        public const string CapeName = "WraithCape";
        private const string SetName = "wraith";
        private const string SetEffectName = "SetEffect_WraithArmor";

        // Kolor futra: piksel -> luminancja * Gain * Tint (alfa bez zmian). Strojenie na zywo: `wraithtint`.
        public static Color Tint = new Color(0.30f, 0.38f, 0.65f);
        public static float Gain = 1.5f;
        public static Color CapeTint = Color.white; // mnoznik _Color na juz pokolorowanym kafelku peleryny
        public static Vector2 CapeTiling = new Vector2(5f, 3f); // kafelki na jednostke UV (u: 0.4 UV ~ 0.6 m, v: 1 UV ~ 0.8 m)
        public static float CapeTatterWidth = 0.05f;               // pas strzepow przy krawedzi (m); ogony peleryny maja ~0.2 m szerokosci, 0.12 wycinalo je cale
        private const int CapeTexSize = 512;
        private const float CapeUvToMetersU = 1.5f;                // skala UV->m (przyblizona, do liczenia odleglosci od krawedzi)
        private const float CapeUvToMetersV = 0.8f;

        // Krawedzie brzegowe siatki cape2 w UV (u1,v1,u2,v2), bez kolnierza (v > 0.8) - zbakowane offline
        // z bundla (scratchpad export_mesh.py), bo w grze siatka NIE jest czytelna z CPU (mimo flagi w bundlu).
        // Uklad UV: u 0.01-0.14 i 0.31-0.40 = dwa ogony (lewy dluzszy, szpic v=0.01), srodek konczy sie
        // lukiem v 0.30-0.43 (krotszy tyl), v ~1 = kolnierz.
        private static readonly float[] CapeEdgesUv =
        {
            0.1750f, 0.3311f, 0.1382f, 0.2949f,
            0.1945f, 0.3822f, 0.1750f, 0.3311f,
            0.1945f, 0.3822f, 0.2398f, 0.4281f,
            0.2398f, 0.4281f, 0.3051f, 0.3902f,
            0.1382f, 0.2949f, 0.1363f, 0.2393f,
            0.0756f, 0.2398f, 0.1053f, 0.1658f,
            0.1363f, 0.2393f, 0.1336f, 0.1685f,
            0.1053f, 0.1658f, 0.1322f, 0.0957f,
            0.1336f, 0.1685f, 0.1322f, 0.0957f,
            0.0121f, 0.2374f, 0.0114f, 0.3145f,
            0.0121f, 0.2374f, 0.0119f, 0.1579f,
            0.0756f, 0.2398f, 0.0647f, 0.1581f,
            0.0119f, 0.1579f, 0.0124f, 0.0754f,
            0.0647f, 0.1581f, 0.0464f, 0.0831f,
            0.0124f, 0.0754f, 0.0195f, 0.0114f,
            0.0464f, 0.0831f, 0.0195f, 0.0114f,
            0.0114f, 0.3145f, 0.0133f, 0.3873f,
            0.0133f, 0.3873f, 0.0163f, 0.4837f,
            0.0163f, 0.4837f, 0.0193f, 0.5592f,
            0.0193f, 0.5592f, 0.0235f, 0.6430f,
            0.0235f, 0.6430f, 0.0233f, 0.7228f,
            0.0233f, 0.7228f, 0.0228f, 0.7772f,
            0.0228f, 0.7772f, 0.0156f, 0.8083f,
            0.3111f, 0.8145f, 0.3134f, 0.7886f,
            0.3134f, 0.7886f, 0.3227f, 0.7376f,
            0.3227f, 0.7376f, 0.3302f, 0.6551f,
            0.3302f, 0.6551f, 0.3435f, 0.5730f,
            0.3435f, 0.5730f, 0.3548f, 0.4986f,
            0.3548f, 0.4986f, 0.3729f, 0.4033f,
            0.3729f, 0.4033f, 0.3841f, 0.3502f,
            0.3051f, 0.3902f, 0.3333f, 0.3420f,
            0.3333f, 0.3420f, 0.3556f, 0.3017f,
            0.3841f, 0.3502f, 0.3927f, 0.3062f,
            0.3556f, 0.3017f, 0.4002f, 0.2339f,
            0.3927f, 0.3062f, 0.4002f, 0.2339f,
        };

        // wycinek tekstury FenringArmor_d (128x128): ciemny splot nogawic z pojedynczymi jasnymi pasami
        // (lewy dolny rog atlasu; caly rog 0,0,64x56 mial za duzo bialych fredzli - peleryna wychodzila szara).
        // Wsp. Unity (y od dolu), w pikselach tekstury 128 - skalowane, gdyby gra podala inna rozdzielczosc.
        // Strojenie: `wraithtint capecrop x y w h`.
        public static int CapeCropX = 32;
        public static int CapeCropY = 24;
        public static int CapeCropW = 56;
        public static int CapeCropH = 32;

        // PLACEHOLDER balansu (Root set: 8 +2/lvl; zelazo: 14 +2/lvl)
        private const float Armor = 8f;
        private const float ArmorPerLevel = 3f;

        // lancuchy: grubosc (skala promieniowa siatki Chain) stala, dlugosci celowo rozne (asymetria):
        // segmentow x skala dlugosci (siatka ma ~0.96 m) -> lewa reka ~0.29 m, prawa ~0.35 m, peleryna ~0.40 / ~0.31 m
        private const float ChainRadialScale = 0.32f;
        private const int LeftHandSegments = 2;
        private const float LeftHandLength = 0.15f;
        private const int RightHandSegments = 2;
        private const float RightHandLength = 0.18f;
        private const int CapeLeftSegments = 3;
        private const float CapeLeftLength = 0.14f;
        private const int CapeRightSegments = 2;
        private const float CapeRightLength = 0.16f;

        private static readonly List<(Texture source, Texture2D target)> RecoloredTextures = new List<(Texture, Texture2D)>();
        private static Material _furMat;
        private static Material _chestBodyMat;
        private static Material _legsBodyMat;
        private static Material _capeMat;
        private static Texture2D _capeAlbedo;      // skladane albedo w ukladzie UV (RGBA, alfa = strzepy)
        private static Texture2D _customCapeAlbedo; // dev: PNG z dysku (uklad UV) zamiast kafelka
        private static float[] _capeEdgeDistance;  // odleglosc od krawedzi siatki (m) per piksel, null = brak siatki

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += Create;
        }

        private static void Create()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Create;
            try
            {
                var (linkMesh, linkMat) = FindChainLink();
                BuildMaterials(linkMat);
                var cuffMat = BuildCuffMaterial(linkMat);
                var setEffect = BuildSetEffect();
                BuildHood(setEffect);
                BuildRobe(setEffect, linkMesh, linkMat, cuffMat);
                BuildLegs(setEffect);
                BuildCape(linkMesh, linkMat, cuffMat);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"WraithArmor: skladanie setu nie powiodlo sie: {ex}");
            }
        }

        // ------------------------------------------------------------------ materialy

        private static void BuildMaterials(Material cutoutReference)
        {
            var fenChest = PrefabManager.Instance.GetPrefab("ArmorFenringChest");
            var fenLegs = PrefabManager.Instance.GetPrefab("ArmorFenringLegs");
            var trollCape = PrefabManager.Instance.GetPrefab("CapeTrollHide");
            if (!fenChest || !fenLegs || !trollCape)
            {
                throw new InvalidOperationException("brak prefabow bazowych (ArmorFenringChest/ArmorFenringLegs/CapeTrollHide)");
            }

            var furMat = fenChest.GetComponentInChildren<SkinnedMeshRenderer>(true).sharedMaterial;          // FenringArmor_mat
            var chestBody = fenChest.GetComponent<ItemDrop>().m_itemData.m_shared.m_armorMaterial;           // FenringArmorChest_mat
            var legsBody = fenLegs.GetComponent<ItemDrop>().m_itemData.m_shared.m_armorMaterial;             // FenringArmorLegs_mat
            var capeMat = FindMaterial(trollCape, "CapeTrollHide");   // attach_skin i model dropu

            _furMat = new Material(furMat) { name = "WraithFur_mat" };
            _furMat.SetTexture("_MainTex", Recolor(furMat.GetTexture("_MainTex"), "wraithfur_d"));

            _chestBodyMat = new Material(chestBody) { name = "WraithFurChest_mat" };
            _chestBodyMat.SetTexture("_ChestTex", Recolor(chestBody.GetTexture("_ChestTex"), "wraithfur_chest_d"));

            _legsBodyMat = new Material(legsBody) { name = "WraithFurLegs_mat" };
            _legsBodyMat.SetTexture("_LegsTex", Recolor(legsBody.GetTexture("_LegsTex"), "wraithfur_legs_d"));

            _capeMat = new Material(capeMat) { name = "WraithCape_mat" };
            // normalna zostaje trollowa (troll_n, w ukladzie UV peleryny) - kafelkowana normalna futra
            // po Blit miala zle kanaly i razem z _METALLICGLOSSMAP dawala "plastik"
            SetupCapeCutout(capeMat, cutoutReference);
            BuildCapeEdgeDistance();
            RebuildCapeTextures();
            _capeMat.SetColor("_Color", CapeTint);
        }

        /// <summary>
        /// Cutout na shaderze Custom/Creature: steruje nim wylacznie keyword _ALPHATEST_ON (shader nie ma
        /// _Mode - proba SetFloat("_Mode") sypie bledem w logu). Keywordy trolla zostaja (_TWOSIDEDNORMALS_ON,
        /// _NORMALMAP, _ADDRAIN_ON) - kopiowanie zestawu z "wraith" wlaczalo _METALLICGLOSSMAP bez mapy
        /// (= metallic 1, gloss 1 -> plastik) i gubilo dwustronnosc. Matowy material jak troll.
        /// </summary>
        private static void SetupCapeCutout(Material trollMat, Material reference)
        {
            Jotunn.Logger.LogInfo($"WraithArmor: shader peleryny={trollMat.shader.name} keywords=[{string.Join(" ", trollMat.shaderKeywords)}]; " +
                                  $"wraith={(reference ? reference.shader.name : "null")} keywords=[{(reference ? string.Join(" ", reference.shaderKeywords) : "")}]");
            _capeMat.shaderKeywords = trollMat.shaderKeywords;
            _capeMat.EnableKeyword("_ALPHATEST_ON");
            _capeMat.SetFloat("_Cutoff", 0.5f);
            _capeMat.SetFloat("_Metallic", 0f);
            _capeMat.SetFloat("_Glossiness", 0.2f);
            _capeMat.SetFloat("_MetalGloss", 0f);
            _capeMat.renderQueue = 2450; // AlphaTest
            Jotunn.Logger.LogInfo($"WraithArmor: material peleryny keywords=[{string.Join(" ", _capeMat.shaderKeywords)}]");
        }

        /// <summary>
        /// Mapa odleglosci od krawedzi siatki cape2 w ukladzie UV (per piksel tekstury, w metrach
        /// w przyblizeniu) ze zbakowanych krawedzi CapeEdgesUv.
        /// </summary>
        private static void BuildCapeEdgeDistance()
        {
            var edges = new List<(Vector2 a, Vector2 b)>();
            for (var i = 0; i + 3 < CapeEdgesUv.Length; i += 4)
            {
                edges.Add((new Vector2(CapeEdgesUv[i] * CapeUvToMetersU, CapeEdgesUv[i + 1] * CapeUvToMetersV),
                           new Vector2(CapeEdgesUv[i + 2] * CapeUvToMetersU, CapeEdgesUv[i + 3] * CapeUvToMetersV)));
            }

            var size = CapeTexSize;
            _capeEdgeDistance = new float[size * size];
            for (var y = 0; y < size; y++)
            {
                var pv = (y + 0.5f) / size * CapeUvToMetersV;
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f) / size * CapeUvToMetersU, pv);
                    var best = float.MaxValue;
                    foreach (var (a, b) in edges)
                    {
                        var ab = b - a;
                        var len2 = ab.sqrMagnitude;
                        var t = len2 > 1e-8f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
                        var d = (p - (a + ab * t)).sqrMagnitude;
                        if (d < best) best = d;
                    }
                    _capeEdgeDistance[y * size + x] = Mathf.Sqrt(best);
                }
            }
        }

        /// <summary>
        /// Sklada albedo (i normalna) peleryny w ukladzie UV: kafelek futra Fenrisa (lub PNG z dysku)
        /// powtarzany CapeTiling razy na jednostke UV, przy krawedziach siatki alfa wycina strzepy
        /// (prog zalezny od szumu, zeby wychodzily nieregularne pasy) i lekko przyciemnia brzeg.
        /// </summary>
        private static void RebuildCapeTextures()
        {
            if (!_capeMat) return;
            var fur = _furMat ? _furMat.GetTexture("_MainTex") as Texture2D : null;
            var tile = fur ? BuildMirroredTile(fur, "wraithcape_tile_d") : null;

            var size = CapeTexSize;
            if (!_capeAlbedo)
            {
                _capeAlbedo = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "wraithcape_d", wrapMode = TextureWrapMode.Clamp };
            }
            var albedo = new Color[size * size];
            var seed = 17.3f;
            for (var y = 0; y < size; y++)
            {
                var v = (y + 0.5f) / size;
                for (var x = 0; x < size; x++)
                {
                    var u = (x + 0.5f) / size;
                    Color c;
                    if (_customCapeAlbedo) c = _customCapeAlbedo.GetPixelBilinear(u, v);
                    else if (tile) c = tile.GetPixelBilinear(u * CapeTiling.x, v * CapeTiling.y);
                    else c = new Color(0.15f, 0.18f, 0.3f, 1f);

                    var alpha = 1f;
                    if (_capeEdgeDistance != null && CapeTatterWidth > 0f)
                    {
                        var d = _capeEdgeDistance[y * size + x];
                        if (d < CapeTatterWidth)
                        {
                            // pasy: szum rozciagniety wzdluz v (strzepy zwisaja w dol) + drobny szum
                            var strips = Mathf.PerlinNoise(u * 48f + seed, v * 6f);
                            var fine = Mathf.PerlinNoise(u * 140f, v * 140f + seed);
                            var n = 0.65f * strips + 0.35f * fine;
                            var threshold = CapeTatterWidth * (0.25f + 0.75f * n);
                            alpha = d < threshold ? 0f : 1f;
                            c *= Mathf.Lerp(0.55f, 1f, d / CapeTatterWidth); // przybrudzony brzeg
                        }
                    }
                    c.a = alpha;
                    albedo[y * size + x] = c;
                }
            }
            _capeAlbedo.SetPixels(albedo);
            _capeAlbedo.Apply(true, false);
            _capeMat.SetTexture("_MainTex", _capeAlbedo);
            _capeMat.SetTextureScale("_MainTex", Vector2.one);
            if (tile) UnityEngine.Object.Destroy(tile);
        }

        /// <summary>
        /// Dev: laduje PNG z dysku jako albedo peleryny (podglad tekstury malowanej w GIMP-ie bez Unity).
        /// Kafelkowanie zostaje z CapeTiling. Sciezka pusta lub "reset" przywraca kafelek z Fenrisa.
        /// </summary>
        public static string LoadCapeAlbedo(string path)
        {
            if (!_capeMat) return "peleryna jeszcze nie zbudowana";
            if (string.IsNullOrEmpty(path) || path == "reset")
            {
                _customCapeAlbedo = null;
                RebuildCapeTextures();
                return "przywrocono kafelek z Fenrisa";
            }
            if (!System.IO.File.Exists(path)) return $"brak pliku: {path}";
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "wraithcape_custom_d" };
            if (!LoadPng(tex, System.IO.File.ReadAllBytes(path))) return "nie udalo sie zdekodowac PNG";
            _customCapeAlbedo = tex; // uklad UV peleryny (u 0..0.4, v 0..1); strzepy z alfy nakladane dalej
            RebuildCapeTextures();
            return $"zaladowano {tex.width}x{tex.height} z {path} (uklad UV, strzepy dolozone)";
        }

        /// <summary>ImageConversion.LoadImage przez refleksje (bezposrednia referencja nie kompiluje sie pod net48, patrz WildBerry).</summary>
        private static bool LoadPng(Texture2D target, byte[] png)
        {
            var imageConversion = Type.GetType("UnityEngine.ImageConversion, UnityEngine.ImageConversionModule");
            var loadImage = imageConversion?.GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });
            return loadImage != null && (bool)loadImage.Invoke(null, new object[] { target, png });
        }

        public static void ApplyCapeTiling()
        {
            RebuildCapeTextures();
        }

        /// <summary>
        /// Wycina CapeCrop* ze zrodla (czytelnego), odbija lustrzanie w obu osiach (2x2) - kafelek bez szwow -
        /// i zwraca teksture z wrapMode Repeat.
        /// </summary>
        private static Texture2D BuildMirroredTile(Texture2D source, string name)
        {
            var scale = source.width / 128f;
            var x0 = Mathf.RoundToInt(CapeCropX * scale);
            var y0 = Mathf.RoundToInt(CapeCropY * scale);
            var w = Mathf.Max(1, Mathf.RoundToInt(CapeCropW * scale));
            var h = Mathf.Max(1, Mathf.RoundToInt(CapeCropH * scale));
            w = Mathf.Min(w, source.width - x0);
            h = Mathf.Min(h, source.height - y0);
            var crop = source.GetPixels(x0, y0, w, h);
            var tile = new Color[w * 2 * h * 2];
            for (var y = 0; y < h * 2; y++)
            {
                var sy = y < h ? y : 2 * h - 1 - y;
                for (var x = 0; x < w * 2; x++)
                {
                    var sx = x < w ? x : 2 * w - 1 - x;
                    tile[y * w * 2 + x] = crop[sy * w + sx];
                }
            }
            var tex = new Texture2D(w * 2, h * 2, TextureFormat.RGBA32, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = source.filterMode,
                anisoLevel = source.anisoLevel,
            };
            tex.SetPixels(tile);
            tex.Apply(true, false);
            return tex;
        }

        private static Material FindMaterial(GameObject prefab, string name)
        {
            var mat = prefab.GetComponentsInChildren<Renderer>(true)
                .SelectMany(r => r.sharedMaterials)
                .FirstOrDefault(m => m && m.name == name);
            if (!mat) throw new InvalidOperationException($"brak materialu {name} w {prefab.name}");
            return mat;
        }

        /// <summary>Kajdan: kopia materialu lancucha z jednolita ciemnoszara tekstura (siatka z TubeBuilder nie ma UV pod atlas Wraitha).</summary>
        private static Material BuildCuffMaterial(Material linkMat)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "wraith_cuff_d" };
            var gray = new Color(0.32f, 0.33f, 0.37f, 1f);
            tex.SetPixels(new[] { gray, gray, gray, gray });
            tex.Apply(false, true);
            var mat = new Material(linkMat) { name = "WraithCuff_mat" };
            mat.SetTexture("_MainTex", tex);
            mat.SetTexture("_BumpMap", null);
            mat.SetTexture("_MetallicGlossMap", null);
            mat.SetFloat("_Metallic", 1f);
            mat.SetFloat("_Glossiness", 0.45f);
            mat.SetFloat("_MetalGloss", 0.45f);
            mat.SetColor("_Color", Color.white);
            return mat;
        }

        private static Texture2D Recolor(Texture source, string name)
        {
            if (!source)
            {
                Jotunn.Logger.LogWarning($"WraithArmor: brak tekstury zrodlowej dla {name}");
                return null;
            }
            var target = CopyReadable(source, name);
            ApplyPixelOp(target, Colorize);
            RecoloredTextures.Add((source, target));
            return target;
        }

        /// <summary>Przemalowuje ponownie wszystkie tekstury setu (po zmianie parametrow tintu).</summary>
        public static void Retint()
        {
            foreach (var (source, target) in RecoloredTextures)
            {
                var tmp = CopyReadable(source, "tmp");
                ApplyPixelOp(tmp, Colorize);
                target.SetPixels(tmp.GetPixels());
                target.Apply(true, false);
                UnityEngine.Object.Destroy(tmp);
            }
            if (_capeMat)
            {
                RebuildCapeTextures();
                _capeMat.SetColor("_Color", CapeTint);
            }
        }

        private static Color Colorize(Color c)
        {
            var lum = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) * Gain;
            return new Color(Mathf.Clamp01(Tint.r * lum), Mathf.Clamp01(Tint.g * lum), Mathf.Clamp01(Tint.b * lum), c.a);
        }

        private static void ApplyPixelOp(Texture2D tex, Func<Color, Color> op)
        {
            var pixels = tex.GetPixels();
            for (var i = 0; i < pixels.Length; i++) pixels[i] = op(pixels[i]);
            tex.SetPixels(pixels);
            tex.Apply(true, false);
        }

        /// <summary>Waniliowe tekstury nie sa czytelne z CPU - kopia przez RenderTexture (jak w WildBerry).</summary>
        private static Texture2D CopyReadable(Texture source, string name)
        {
            var rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true)
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

        /// <summary>Ikona = wycinek atlasu ikon bazowego itemu przemalowany ta sama operacja co tekstury.</summary>
        private static Sprite RecolorSprite(Sprite sprite, Func<Color, Color> op, string name)
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
                for (var i = 0; i < pixels.Length; i++) pixels[i] = op(pixels[i]);
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, filterMode = sprite.texture.filterMode };
                tex.SetPixels(pixels);
                tex.Apply(false, true);
                return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning($"WraithArmor: przemalowanie ikony {name} nie powiodlo sie ({ex.Message}) - zostaje oryginal");
                return sprite;
            }
            finally
            {
                if (atlas) UnityEngine.Object.Destroy(atlas);
            }
        }

        private static (Mesh mesh, Material material) FindChainLink()
        {
            var chain = PrefabManager.Instance.GetPrefab("Chain");
            var filter = chain ? chain.GetComponentsInChildren<MeshFilter>(true).FirstOrDefault(f => f.sharedMesh) : null;
            if (!filter)
            {
                throw new InvalidOperationException("brak siatki w prefabie Chain");
            }
            var renderer = filter.GetComponent<MeshRenderer>();
            Jotunn.Logger.LogInfo($"WraithArmor: ogniwa z Chain - mesh={filter.sharedMesh.name} bounds={filter.sharedMesh.bounds} mat={(renderer ? renderer.sharedMaterial.name : "null")}");
            return (filter.sharedMesh, renderer ? renderer.sharedMaterial : null);
        }

        // ------------------------------------------------------------------ set effect

        private static StatusEffect BuildSetEffect()
        {
            var se = ScriptableObject.CreateInstance<SE_Stats>();
            se.name = SetEffectName;
            se.m_name = "$se_wraithset";
            se.m_tooltip = "$se_wraithset_tooltip";
            se.m_skillLevel = Skills.SkillType.ElementalMagic;
            se.m_skillLevelModifier = 10f;
            se.m_skillLevel2 = Skills.SkillType.BloodMagic;
            se.m_skillLevelModifier2 = 10f;
            var hood = PrefabManager.Instance.GetPrefab("HelmetFenring");
            if (hood) se.m_icon = hood.GetComponent<ItemDrop>().m_itemData.m_shared.m_icons.FirstOrDefault();
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, false));
            return se;
        }

        // ------------------------------------------------------------------ itemy

        private static void BuildHood(StatusEffect setEffect)
        {
            var item = BuildPiece(HoodName, "HelmetFenring", "item_wraithhood", Colorize, new Dictionary<string, int[]>
            {
                //                         L1 L2 L3 L4   (Bagno, Bagno, Gory, Rowniny)
                { "TrophyWraith", new[] { 1, 0, 0, 0 } },
                { "Chain",        new[] { 2, 2, 0, 0 } },
                { "WolfHairBundle", new[] { 20, 0, 0, 0 } }, // siersc Fenrisa, tyle co w recepturze Fenrisa
                { "Iron",         new[] { 0, 3, 0, 0 } },
                { "Silver",       new[] { 0, 0, 4, 0 } },
                { "Crystal",      new[] { 0, 0, 2, 0 } },
                { "BlackMetal",   new[] { 0, 0, 0, 3 } },
                { "LinenThread",  new[] { 0, 0, 0, 6 } },
            });
            var shared = item.ItemDrop.m_itemData.m_shared;
            ApplyArmorStats(shared, 1f, 0.1f, setEffect);
            ReplaceMaterials(item.ItemPrefab, "FenringArmor_mat", _furMat);
        }

        private static void BuildRobe(StatusEffect setEffect, Mesh linkMesh, Material linkMat, Material cuffMat)
        {
            var item = BuildPiece(RobeName, "ArmorFenringChest", "item_wraithrobe", Colorize, new Dictionary<string, int[]>
            {
                { "Chain",        new[] { 5, 3, 0, 0 } },
                { "Iron",         new[] { 4, 6, 0, 0 } },
                { "WolfHairBundle", new[] { 20, 0, 0, 0 } }, // siersc Fenrisa, tyle co w recepturze Fenrisa
                { "Silver",       new[] { 0, 0, 8, 0 } },
                { "Crystal",      new[] { 0, 0, 3, 0 } },
                { "BlackMetal",   new[] { 0, 0, 0, 6 } },
                { "LinenThread",  new[] { 0, 0, 0, 10 } },
            });
            var shared = item.ItemDrop.m_itemData.m_shared;
            ApplyArmorStats(shared, 5f, 0.2f, setEffect);
            ReplaceMaterials(item.ItemPrefab, "FenringArmor_mat", _furMat);
            shared.m_armorMaterial = _chestBodyMat;

            // lancuchy z kajdanami z nadgarstkow (kosci LeftHand / RightHand)
            AttachChain(item.ItemPrefab, "attach_LeftHand", linkMesh, linkMat, cuffMat, LeftHandSegments, LeftHandLength);
            AttachChain(item.ItemPrefab, "attach_RightHand", linkMesh, linkMat, cuffMat, RightHandSegments, RightHandLength);
        }

        private static void BuildLegs(StatusEffect setEffect)
        {
            var item = BuildPiece(LegsName, "ArmorFenringLegs", "item_wraithlegs", Colorize, new Dictionary<string, int[]>
            {
                { "Chain",        new[] { 3, 2, 0, 0 } },
                { "Iron",         new[] { 3, 5, 0, 0 } },
                { "WolfHairBundle", new[] { 20, 0, 0, 0 } }, // siersc Fenrisa, tyle co w recepturze Fenrisa
                { "Silver",       new[] { 0, 0, 6, 0 } },
                { "Crystal",      new[] { 0, 0, 2, 0 } },
                { "BlackMetal",   new[] { 0, 0, 0, 5 } },
                { "LinenThread",  new[] { 0, 0, 0, 8 } },
            });
            var shared = item.ItemDrop.m_itemData.m_shared;
            ApplyArmorStats(shared, 5f, 0.2f, setEffect);
            ReplaceMaterials(item.ItemPrefab, "FenringArmor_mat", _furMat);
            shared.m_armorMaterial = _legsBodyMat;
        }

        private static void BuildCape(Mesh linkMesh, Material linkMat, Material cuffMat)
        {
            var item = BuildPiece(CapeName, "CapeTrollHide", "item_wraithcape", Colorize, new Dictionary<string, int[]>
            {
                //                         L1 L2 L3 L4   (Gory, Gory, Rowniny, Rowniny)
                { "Chain",        new[] { 4, 0, 0, 0 } },
                { "Silver",       new[] { 6, 6, 0, 0 } },
                { "Crystal",      new[] { 2, 3, 0, 0 } },
                { "BlackMetal",   new[] { 0, 0, 4, 6 } },
                { "LinenThread",  new[] { 0, 0, 8, 10 } },
            });
            var shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_armor = 1f;
            shared.m_armorPerLevel = 1f;
            shared.m_maxQuality = 4;
            shared.m_weight = 4f;
            shared.m_eitrRegenModifier = 0.1f;
            // baza nalezy do setu trolla - odpinamy, zeby peleryna nie liczyla sie do SetEffect_TrollArmor
            shared.m_setName = string.Empty;
            shared.m_setSize = 0;
            shared.m_setStatusEffect = null;
            shared.m_damageModifiers = new List<HitData.DamageModPair>
            {
                new HitData.DamageModPair { m_type = HitData.DamageType.Frost, m_modifier = HitData.DamageModifier.Resistant },
            };
            ReplaceMaterials(item.ItemPrefab, "CapeTrollHide", _capeMat);

            // dwa lancuchy "naszyte" na pelerynie (ClothChain): sledza wierzcholki tkaniny cape2 (troll)
            // w kolumnach x = -0.12 / +0.12 (przestrzen postaci), wiec zawsze leza na plaszczu.
            // Wczesniejsze podejscia (stala sila do tylu, kapsula tulowia, pochylona kapsula) przegrywaly
            // z symulacja tkaniny - lancuchy ladowaly pod spodem. Root "attach_Spine2" tylko po to,
            // zeby AttachArmor zainstancjonowal i aktywowal obiekt razem z peleryna.
            var root = new GameObject("attach_Spine2");
            root.transform.SetParent(item.ItemPrefab.transform, false);
            root.SetActive(false);
            ClothChain.Build("chain_left", linkMesh, linkMat, CapeLeftSegments, ChainRadialScale, CapeLeftLength * 0.96f, -0.12f, cuffMat)
                .transform.SetParent(root.transform, false);
            ClothChain.Build("chain_right", linkMesh, linkMat, CapeRightSegments, ChainRadialScale, CapeRightLength * 0.96f, 0.12f, cuffMat)
                .transform.SetParent(root.transform, false);
        }

        private static CustomItem BuildPiece(string prefabName, string baseName, string token, Func<Color, Color> iconOp, Dictionary<string, int[]> perLevel)
        {
            var baseShared = PrefabManager.Instance.GetPrefab(baseName).GetComponent<ItemDrop>().m_itemData.m_shared;
            var icon = RecolorSprite(baseShared.m_icons.FirstOrDefault(), iconOp, prefabName + "_icon");

            var config = new ItemConfig
            {
                Name = "$" + token,
                Description = "$" + token + "_description",
                Icons = new[] { icon },
                CraftingStation = "forge",
                RepairStation = "forge",
                MinStationLevel = 1,
                // m_amount = koszt poziomu 1 (steruje tez odkrywaniem receptury); wyzsze poziomy z LeveledRequirements.
                // Jotunn (RequirementConfig.IsValid) wycina wymagania z amount=0 i amountPerLevel=0, wiec skladniki
                // pojawiajace sie dopiero przy ulepszaniu dostaja amountPerLevel=1 - patch GetAmount i tak je nadpisuje.
                Requirements = perLevel.Select(kv => new RequirementConfig(kv.Key, kv.Value[0], kv.Value[0] > 0 ? 0 : 1)).ToArray(),
            };
            var item = new CustomItem(prefabName, baseName, config);
            ItemManager.Instance.AddItem(item);
            LeveledRequirements.Register(prefabName, perLevel);
            return item;
        }

        private static void ApplyArmorStats(ItemDrop.ItemData.SharedData shared, float weight, float eitrRegen, StatusEffect setEffect)
        {
            shared.m_armor = Armor;
            shared.m_armorPerLevel = ArmorPerLevel;
            shared.m_maxQuality = 4;
            shared.m_weight = weight;
            shared.m_maxDurability = 800f;
            shared.m_durabilityPerLevel = 100f;
            shared.m_movementModifier = 0f; // Fenris ma +0.03 - to jego bonus, nie nasz
            shared.m_eitrRegenModifier = eitrRegen;
            shared.m_setName = SetName;
            shared.m_setSize = 3;
            shared.m_setStatusEffect = setEffect;
        }

        /// <summary>Podmienia material o danej nazwie na wszystkich rendererach prefabu (model dropu + attach_skin).</summary>
        private static void ReplaceMaterials(GameObject prefab, string materialName, Material replacement)
        {
            var count = 0;
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
            {
                var mats = renderer.sharedMaterials;
                var changed = false;
                for (var i = 0; i < mats.Length; i++)
                {
                    if (mats[i] && mats[i].name == materialName)
                    {
                        mats[i] = replacement;
                        changed = true;
                    }
                }
                if (!changed) continue;
                renderer.sharedMaterials = mats;
                count++;
            }
            if (count == 0)
            {
                Jotunn.Logger.LogWarning($"WraithArmor: {prefab.name} - nie znaleziono materialu {materialName} do podmiany");
            }
        }

        private static void AttachChain(GameObject prefab, string attachName, Mesh linkMesh, Material linkMat, Material cuffMat,
            int segments, float lengthScale)
        {
            var chain = DanglingChain.Build(attachName, linkMesh, linkMat, segments, ChainRadialScale, lengthScale,
                Vector3.zero, Vector3.zero, cuffMat);
            chain.transform.SetParent(prefab.transform, false);
            // jak waniliowe attach_skin: nieaktywne w prefabie, AttachArmor aktywuje instancje
            chain.SetActive(false);
        }
    }
}
