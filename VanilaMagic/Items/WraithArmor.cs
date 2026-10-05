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
        private const int SetSize = 4; // kaptur, szata, nogawice, peleryna
        private const string SetEffectName = "SetEffect_WraithArmor";

        // Kolor futra: piksel -> luminancja * Gain * Tint (alfa bez zmian). Strojenie na zywo: `wraithtint`.
        public static Color Tint = new Color(0.30f, 0.38f, 0.65f);
        public static float Gain = 1.5f;
        // Polysk calego setu = matowa peleryna (roughness od grafika ~0.99). Futro Fenrisa ma 0.06, cialo gracza 0.2
        // (nakladka torsu/nog swiecila sie obok peleryny) - WraithMatteBody ustawia to samo na ciele.
        public const float Gloss = 0.03f;
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

        // Lancuchy 3D (rece; opcjonalnie peleryna): ogniwa proceduralne (DanglingChain.BuildLinkChainMesh) o wymiarach
        // 1:1 z ogniwami namalowanymi na pelerynie (make_cape_tex.py CHAINS_C: skok 24 px, szerokosc 15 px, ~1.88 mm/px):
        // ogniwo 5.6 x 2.8 cm, rurka 3 mm, skok 4.5 cm, kajdan R=3.1 cm. Segment verletu = kilka ogniw; dlugosci celowo rozne.
        public const float ChainLinkLength = 0.056f;
        public const float ChainLinkWidth = 0.028f;
        public const float ChainTubeRadius = 0.006f;   // 2x grubsze niz malowane (Kamil: fizyczne lancuchy maja byc grubsze)
        public const float ChainPitch = 0.045f;
        public const float CuffRadius = 0.031f;
        private const int LeftHandSegments = 2;
        private const int LeftHandLinksPerSegment = 3;   // 2 x 14.6 cm
        private const int RightHandSegments = 2;
        private const int RightHandLinksPerSegment = 4;  // 2 x 19.1 cm
        private const int CapeLeftSegments = 3;
        private const int CapeLeftLinksPerSegment = 3;
        private const int CapeRightSegments = 2;
        private const int CapeRightLinksPerSegment = 3;

        private static readonly List<(Texture source, Texture2D target, Func<Color, Color> op)> RecoloredTextures = new List<(Texture, Texture2D, Func<Color, Color>)>();
        private static Texture2D _cuffTex; // jednolity kolor kajdanow = Colorize(szary), odswiezany w Retint
        private const float ChainGain = 1.7f; // tekstura ogniw Wraitha jest ciemna - podbicie, zeby zgrac z malowanymi lancuchami peleryny
        private static Material _furMat;
        private static Material _chestBodyMat;
        private static Material _legsBodyMat;
        private static Material _capeMat;
        private static Texture2D _capeAlbedo;      // skladane albedo w ukladzie UV (RGBA, alfa = strzepy)
        private static Texture2D _customCapeAlbedo; // dev: PNG z dysku (uklad UV cape2) zamiast zasobu
        private static Texture2D _embeddedCapeAlbedo; // Assets/WraithCape.png (EmbeddedResource) - kolory Fenrisa, Colorize w runtime
        private static Texture2D _embeddedCapeNormal; // Assets/WraithCape_n.png - normalna z albedo (zamiast pomarszczonej skory trolla)
        private static Texture _trollCapeNormal;       // oryginalna troll_n do porownania (`wraithtint capemat _BumpMap troll`)
        private static bool _embeddedCapeChecked;
        public static bool CapeCustomRaw;               // PNG z dysku bez Colorize (gotowe kolory)
        private const bool CapeEmbeddedRaw = false;     // WraithCape.png w DLL: baza grafika (ksztalt/alfa) sciemniona + lancuchy, kolory przez Colorize jak reszta setu
        public static bool CapeChains3D;                // lancuchy ClothChain na pelerynie (domyslnie off: lancuchy sa namalowane w WraithCape.png)
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
                BuildCape(setEffect, linkMesh, linkMat, cuffMat);
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
            _furMat.SetFloat("_Glossiness", Gloss);

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
            _trollCapeNormal = _capeMat.GetTexture("_BumpMap");
            var normal = LoadEmbeddedPng("WraithCape_n", "wraithcape_n");
            if (normal)
            {
                _embeddedCapeNormal = normal;
                _capeMat.SetTexture("_BumpMap", normal);
                Jotunn.Logger.LogDebug($"WraithArmor: normalna peleryny z WraithCape_n.png ({normal.width}x{normal.height}), _BumpScale={_capeMat.GetFloat("_BumpScale"):0.00}");
            }
            LogCapeMaterial();
        }

        /// <summary>
        /// Cutout na shaderze Custom/Creature: steruje nim wylacznie keyword _ALPHATEST_ON (shader nie ma
        /// _Mode - proba SetFloat("_Mode") sypie bledem w logu). Keywordy trolla zostaja (_TWOSIDEDNORMALS_ON,
        /// _NORMALMAP, _ADDRAIN_ON) - kopiowanie zestawu z "wraith" wlaczalo _METALLICGLOSSMAP bez mapy
        /// (= metallic 1, gloss 1 -> plastik) i gubilo dwustronnosc. Matowy material jak troll.
        /// </summary>
        private static void SetupCapeCutout(Material trollMat, Material reference)
        {
            Jotunn.Logger.LogDebug($"WraithArmor: shader peleryny={trollMat.shader.name} keywords=[{string.Join(" ", trollMat.shaderKeywords)}]; " +
                                  $"wraith={(reference ? reference.shader.name : "null")} keywords=[{(reference ? string.Join(" ", reference.shaderKeywords) : "")}]");
            _capeMat.shaderKeywords = trollMat.shaderKeywords;
            _capeMat.EnableKeyword("_ALPHATEST_ON");
            // Troll ma efekt deszczu (_ADDRAIN_ON: mokry polysk i "iskry" w deszczu), futro Fenrisa reszty setu nie -
            // mokra peleryna blyszczala obok matowej szaty. Wylaczone jak w FenringArmor_mat.
            _capeMat.DisableKeyword("_ADDRAIN_ON");
            if (_capeMat.HasProperty("_AddRain")) _capeMat.SetFloat("_AddRain", 0f);
            _capeMat.SetFloat("_Cutoff", 0.5f);
            _capeMat.SetFloat("_Metallic", 0f);
            // jak FenringArmor_mat: matowe futro (0.06, troll mial 0.2 - ciemna baza z polyskiem = czarny placek), relief pelny
            _capeMat.SetFloat("_Glossiness", Gloss);
            _capeMat.SetFloat("_MetalGloss", 0f);
            _capeMat.SetFloat("_BumpScale", 1f);
            _capeMat.renderQueue = 2450; // AlphaTest
            Jotunn.Logger.LogDebug($"WraithArmor: material peleryny keywords=[{string.Join(" ", _capeMat.shaderKeywords)}]");
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
            var ready = _customCapeAlbedo ? _customCapeAlbedo : EmbeddedCapeAlbedo();
            if (ready)
            {
                // Gotowe albedo w ukladzie UV cape2 (u 0..0.4 = peleryna, v=1 kolnierz, alfa = strzepy):
                // z dysku (`wraithtint capetex plik.png [raw]`) albo z zasobu DLL. Zasob ma kolory Fenrisa i przechodzi
                // przez Colorize jak reszta setu, zeby `wraithtint r g b` tintowal cala zbroje spojnie.
                var raw = _customCapeAlbedo ? CapeCustomRaw : CapeEmbeddedRaw;
                if (!_capeAlbedo || _capeAlbedo.width != ready.width || _capeAlbedo.height != ready.height)
                {
                    if (_capeAlbedo) UnityEngine.Object.Destroy(_capeAlbedo);
                    _capeAlbedo = new Texture2D(ready.width, ready.height, TextureFormat.RGBA32, true) { name = "wraithcape_d", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point }; // jak FenringArmor_d - piksele splotu 1:1 z szata
                }
                var px = ready.GetPixels();
                if (!raw) for (var i = 0; i < px.Length; i++) px[i] = Colorize(px[i]);
                _capeAlbedo.SetPixels(px);
                _capeAlbedo.Apply(true, false);
                _capeMat.SetTexture("_MainTex", _capeAlbedo);
                ResetCapeUv();
                return;
            }

            // Fallback (brak zasobu): kafelek futra Fenrisa + proceduralne strzepy przy krawedziach siatki.
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
            ResetCapeUv();
            if (tile) UnityEngine.Object.Destroy(tile);
        }

        /// <summary>
        /// Waniliowy material CapeTrollHide ma _MainTex_ST = (1, 1, -0.99, 0.38): troll_diffuse to wspoldzielony atlas
        /// i peleryna czyta z niego przesuniety wycinek. Nasza tekstura jest w czystym ukladzie UV cape2, wiec skala 1
        /// i offset 0 - inaczej (przy Clamp) cala peleryna dostaje jedna rozciagnieta kolumne pikseli.
        /// </summary>
        private static void ResetCapeUv()
        {
            foreach (var prop in new[] { "_MainTex", "_BumpMap" })
            {
                if (!_capeMat.HasProperty(prop)) continue;
                _capeMat.SetTextureScale(prop, Vector2.one);
                _capeMat.SetTextureOffset(prop, Vector2.zero);
            }
            SyncHoodedCape();
        }

        // Wariant peleryny noszonej z Kapturem Widma: gora (wiersze 0..HoodedCutRow z 128, od kolnierza) przezroczysta -
        // lezy pod kolnierzem kaptura i w ruchu przebijala nad niego. Bez kaptura peleryna pelna. Podmiana materialu
        // per postac w WraithHoodedCape (patch VisEquipment), bo _capeMat jest wspolny dla wszystkich graczy.
        public static int HoodedCutRow = 25;          // dobrane w grze (2026-09-27, gora przypieta do kaptura); dev: `wraithtint capecut wiersz`
        // Pas peleryny przypiety do kaptura (WraithCapeStrip_mesh z bundla: gorna czesc cape2 z wagami kolnierza kaptura,
        // bez fizyki) - widoczne wiersze StripTopRow..StripBottomRow (+-2 px postrzepienia). Gora pod kolnierzem schowana,
        // bo po odsunieciu pas wychodzil nad kolnierz. Dev: `wraithtint capestrip gora dol`.
        public static int StripTopRow = 34;
        public static int StripBottomRow = 46;
        private static Material _capeMatStrip;
        private static Texture2D _capeAlbedoStrip;
        public static Material CapeMaterialStrip => _capeMatStrip;
        private static Material _capeMatHooded;
        private static Texture2D _capeAlbedoHooded;
        public static Material CapeMaterial => _capeMat;
        public static Material CapeMaterialHooded => _capeMatHooded;

        /// <summary>Kopia materialu peleryny z albedo bez gornego pasa (ta sama tekstura, alfa 0 nad kolnierzem).</summary>
        internal static void SyncHoodedCape()
        {
            if (!_capeMat || !_capeAlbedo) return;
            if (!_capeMatHooded) _capeMatHooded = new Material(_capeMat) { name = "WraithCape_hooded_mat" };
            else _capeMatHooded.CopyPropertiesFromMaterial(_capeMat);
            _capeMatHooded.shaderKeywords = _capeMat.shaderKeywords;
            _capeMatHooded.renderQueue = _capeMat.renderQueue;
            int w = _capeAlbedo.width, h = _capeAlbedo.height;
            if (!_capeAlbedoHooded || _capeAlbedoHooded.width != w || _capeAlbedoHooded.height != h)
            {
                if (_capeAlbedoHooded) UnityEngine.Object.Destroy(_capeAlbedoHooded);
                _capeAlbedoHooded = new Texture2D(w, h, TextureFormat.RGBA32, true)
                    { name = "wraithcape_hooded_d", wrapMode = _capeAlbedo.wrapMode, filterMode = _capeAlbedo.filterMode };
            }
            var px = _capeAlbedo.GetPixels();
            var firstHidden = Mathf.RoundToInt(h * (1f - HoodedCutRow / 128f)); // Unity: y od dolu, kolnierz = v 1
            for (var y = firstHidden; y < h; y++)
                for (var x = 0; x < w; x++) px[y * w + x].a = 0f;
            _capeAlbedoHooded.SetPixels(px);
            _capeAlbedoHooded.Apply(true, false);
            _capeMatHooded.SetTexture("_MainTex", _capeAlbedoHooded);

            // pas: tylko wiersze StripTopRow..StripBottomRow, dol poszarpany (kolumny w pikselach 128)
            if (!_capeMatStrip) _capeMatStrip = new Material(_capeMat) { name = "WraithCape_strip_mat" };
            else _capeMatStrip.CopyPropertiesFromMaterial(_capeMat);
            _capeMatStrip.shaderKeywords = _capeMat.shaderKeywords;
            _capeMatStrip.renderQueue = _capeMat.renderQueue;
            if (!_capeAlbedoStrip || _capeAlbedoStrip.width != w || _capeAlbedoStrip.height != h)
            {
                if (_capeAlbedoStrip) UnityEngine.Object.Destroy(_capeAlbedoStrip);
                _capeAlbedoStrip = new Texture2D(w, h, TextureFormat.RGBA32, true)
                    { name = "wraithcape_strip_d", wrapMode = _capeAlbedo.wrapMode, filterMode = _capeAlbedo.filterMode };
            }
            var sp = _capeAlbedo.GetPixels();
            for (var y = 0; y < h; y++)
            {
                var row = (h - 1 - y) * 128f / h; // wiersz od kolnierza (0) w skali 128
                for (var x = 0; x < w; x++)
                {
                    var col = (int)(x * 128f / w);
                    var bottom = StripBottomRow + ((col * 7 + 3) % 5) - 2;
                    if (row < StripTopRow || row > bottom + 1) sp[y * w + x].a = 0f;
                }
            }
            _capeAlbedoStrip.SetPixels(sp);
            _capeAlbedoStrip.Apply(true, false);
            _capeMatStrip.SetTexture("_MainTex", _capeAlbedoStrip);
        }

        /// <summary>
        /// Dev: laduje PNG z dysku jako albedo peleryny (podglad tekstury malowanej w GIMP-ie bez Unity).
        /// Kafelkowanie zostaje z CapeTiling. Sciezka pusta lub "reset" przywraca kafelek z Fenrisa.
        /// </summary>
        public static string LoadCapeAlbedo(string path, bool raw = false)
        {
            if (!_capeMat) return "peleryna jeszcze nie zbudowana";
            if (string.IsNullOrEmpty(path) || path == "reset")
            {
                _customCapeAlbedo = null;
                CapeCustomRaw = false;
                RebuildCapeTextures();
                if (_embeddedCapeNormal) _capeMat.SetTexture("_BumpMap", _embeddedCapeNormal);
                return _embeddedCapeAlbedo ? "przywrocono WraithCape.png z DLL" : "przywrocono kafelek z Fenrisa";
            }
            if (!System.IO.File.Exists(path)) return $"brak pliku: {path}";
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "wraithcape_custom_d" };
            if (!LoadPng(tex, System.IO.File.ReadAllBytes(path))) return "nie udalo sie zdekodowac PNG";
            _customCapeAlbedo = tex; // uklad UV cape2 (u 0..0.4, v 0..1), alfa = strzepy - uzywane 1:1
            CapeCustomRaw = raw;
            RebuildCapeTextures();
            // normalna obok pliku: <nazwa>_n.png (uklad UV) - jesli jest, podmieniamy razem z albedo
            var normalPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path) ?? "", System.IO.Path.GetFileNameWithoutExtension(path) + "_n.png");
            var normalInfo = "";
            if (System.IO.File.Exists(normalPath))
            {
                var ntex = new Texture2D(2, 2, TextureFormat.RGBA32, true, true) { name = "wraithcape_custom_n", wrapMode = TextureWrapMode.Clamp };
                if (LoadPng(ntex, System.IO.File.ReadAllBytes(normalPath))) { _capeMat.SetTexture("_BumpMap", ntex); normalInfo = $" + normalna {System.IO.Path.GetFileName(normalPath)}"; }
            }
            else if (_embeddedCapeNormal) _capeMat.SetTexture("_BumpMap", _embeddedCapeNormal);
            return $"zaladowano {tex.width}x{tex.height} z {path} ({(raw ? "kolory 1:1" : "przez Colorize/tint")}){normalInfo}";
        }

        /// <summary>Assets/WraithCape.png z DLL (uklad UV cape2, kolory Fenrisa). null = brak zasobu, wtedy fallback proceduralny.</summary>
        private static Texture2D EmbeddedCapeAlbedo()
        {
            if (_embeddedCapeChecked) return _embeddedCapeAlbedo;
            _embeddedCapeChecked = true;
            _embeddedCapeAlbedo = LoadEmbeddedPng("WraithCape", "wraithcape_src");
            if (_embeddedCapeAlbedo) Jotunn.Logger.LogDebug($"WraithArmor: albedo peleryny z WraithCape.png ({_embeddedCapeAlbedo.width}x{_embeddedCapeAlbedo.height})");
            else Jotunn.Logger.LogWarning("WraithArmor: brak/zly zasob WraithCape.png - peleryna z kafelka Fenrisa");
            return _embeddedCapeAlbedo;
        }

        /// <summary>PNG z EmbeddedResource Assets/&lt;name&gt;.png jako czytelna Texture2D (null = brak zasobu / zly plik).</summary>
        private static Texture2D LoadEmbeddedPng(string name, string texName)
        {
            var resource = "VanilaMagic.Assets." + name + ".png";
            using (var stream = typeof(WraithArmor).Assembly.GetManifestResourceStream(resource))
            {
                if (stream == null) return null;
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = texName, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Point };
                return LoadPng(tex, bytes) ? tex : null;
            }
        }

        /// <summary>Wypisuje do logu wszystkie wlasciwosci materialu peleryny (shader, keywordy, floaty, kolory, tekstury).</summary>
        public static string LogCapeMaterial()
        {
            if (!_capeMat) return "peleryna jeszcze nie zbudowana";
            var sb = new System.Text.StringBuilder();
            sb.Append($"WraithCape_mat shader={_capeMat.shader.name} queue={_capeMat.renderQueue} keywords=[{string.Join(" ", _capeMat.shaderKeywords)}]");
            var shader = _capeMat.shader;
            var count = shader.GetPropertyCount();
            for (var i = 0; i < count; i++)
            {
                var prop = shader.GetPropertyName(i);
                switch (shader.GetPropertyType(i))
                {
                    case UnityEngine.Rendering.ShaderPropertyType.Float:
                    case UnityEngine.Rendering.ShaderPropertyType.Range:
                        sb.Append($"\n  {prop} = {_capeMat.GetFloat(prop):0.###}");
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Color:
                        sb.Append($"\n  {prop} = {_capeMat.GetColor(prop)}");
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Vector:
                        sb.Append($"\n  {prop} = {_capeMat.GetVector(prop)}");
                        break;
                    case UnityEngine.Rendering.ShaderPropertyType.Texture:
                        var t = _capeMat.GetTexture(prop);
                        sb.Append($"\n  {prop} = {(t ? t.name + " " + t.width + "x" + t.height : "null")}");
                        break;
                }
            }
            var text = sb.ToString();
            Jotunn.Logger.LogDebug("WraithArmor: " + text);
            return text;
        }

        /// <summary>
        /// Dev: ustawia wlasciwosc materialu peleryny na zywo. Wartosci: 1 liczba = float; 3-4 liczby = kolor;
        /// dla tekstur: "none" (null), "troll" (oryginalna normalna trolla), "own" (nasza z zasobu);
        /// "on"/"off" = keyword shadera. Zwraca komunikat do konsoli.
        /// </summary>
        public static string SetCapeMaterial(string prop, string[] values)
        {
            if (!_capeMat) return "peleryna jeszcze nie zbudowana";
            if (values.Length == 1 && (values[0] == "on" || values[0] == "off"))
            {
                if (values[0] == "on") _capeMat.EnableKeyword(prop); else _capeMat.DisableKeyword(prop);
                return $"keyword {prop} {values[0]} -> [{string.Join(" ", _capeMat.shaderKeywords)}]";
            }
            if (values.Length == 1 && (values[0] == "none" || values[0] == "troll" || values[0] == "own"))
            {
                Texture tex = values[0] == "none" ? null : values[0] == "troll" ? _trollCapeNormal : (Texture)_embeddedCapeNormal;
                _capeMat.SetTexture(prop, tex);
                return $"{prop} = {(tex ? tex.name : "null")}";
            }
            var nums = new List<float>();
            foreach (var v in values)
            {
                if (!float.TryParse(v, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var f)) return $"zla wartosc: {v}";
                nums.Add(f);
            }
            if (!_capeMat.HasProperty(prop)) return $"material nie ma wlasciwosci {prop}";
            if (nums.Count == 4 && prop.EndsWith("_ST"))
            {
                var texProp = prop.Substring(0, prop.Length - 3);
                _capeMat.SetTextureScale(texProp, new Vector2(nums[0], nums[1]));
                _capeMat.SetTextureOffset(texProp, new Vector2(nums[2], nums[3]));
                return $"{texProp} scale=({nums[0]},{nums[1]}) offset=({nums[2]},{nums[3]})";
            }
            if (nums.Count == 1) { _capeMat.SetFloat(prop, nums[0]); return $"{prop} = {nums[0]}"; }
            if (nums.Count >= 3)
            {
                var c = new Color(nums[0], nums[1], nums[2], nums.Count > 3 ? nums[3] : 1f);
                _capeMat.SetColor(prop, c);
                return $"{prop} = {c}";
            }
            return "uzycie: wraithtint capemat <wlasciwosc> <float | r g b [a] | none|troll|own | on|off>";
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
            var cc = CuffColor();
            tex.SetPixels(new[] { cc, cc, cc, cc });
            tex.Apply(false, false);
            _cuffTex = tex;
            var mat = new Material(linkMat) { name = "WraithCuff_mat" };
            mat.SetTexture("_MainTex", tex);
            mat.SetTexture("_BumpMap", null);
            mat.SetTexture("_MetallicGlossMap", null);
            mat.DisableKeyword("_METALLICGLOSSMAP");
            mat.SetFloat("_Metallic", 0.5f);
            mat.SetFloat("_Glossiness", 0.5f);
            mat.SetFloat("_MetalGloss", 0.5f);
            mat.SetColor("_Color", Color.white);
            return mat;
        }

        private static Texture2D Recolor(Texture source, string name, Func<Color, Color> op = null)
        {
            if (!source)
            {
                Jotunn.Logger.LogWarning($"WraithArmor: brak tekstury zrodlowej dla {name}");
                return null;
            }
            op = op ?? Colorize;
            var target = CopyReadable(source, name);
            ApplyPixelOp(target, op);
            RecoloredTextures.Add((source, target, op));
            return target;
        }

        /// <summary>Ogniwa lancuchow (rece, opcjonalnie peleryna): kopia materialu "wraith" z albedo przez Colorize + ChainGain.</summary>
        private static Material BuildChainLinkMaterial(Material linkMat)
        {
            if (!linkMat) return null;
            var mat = new Material(linkMat) { name = "WraithChain_mat" };
            var albedo = Recolor(linkMat.GetTexture("_MainTex"), "wraithchain_d", ColorizeChain);
            if (albedo) mat.SetTexture("_MainTex", albedo);
            return mat;
        }

        private static Color ColorizeChain(Color c)
        {
            var lum = (0.299f * c.r + 0.587f * c.g + 0.114f * c.b) * Gain * ChainGain;
            return new Color(Mathf.Clamp01(Tint.r * lum), Mathf.Clamp01(Tint.g * lum), Mathf.Clamp01(Tint.b * lum), c.a);
        }

        /// <summary>Kolor kajdanow: srednioszary metal przez Colorize (ten sam odcien co ogniwa i malowane lancuchy).</summary>
        private static Color CuffColor() => Colorize(new Color(0.62f, 0.62f, 0.62f, 1f)); // ~srodek jasnosci malowanych ogniw

        /// <summary>Przemalowuje ponownie wszystkie tekstury setu (po zmianie parametrow tintu).</summary>
        public static void Retint()
        {
            foreach (var (source, target, op) in RecoloredTextures)
            {
                var tmp = CopyReadable(source, "tmp");
                ApplyPixelOp(tmp, op);
                target.SetPixels(tmp.GetPixels());
                target.Apply(true, false);
                UnityEngine.Object.Destroy(tmp);
            }
            if (_cuffTex)
            {
                var cc = CuffColor();
                _cuffTex.SetPixels(new[] { cc, cc, cc, cc });
                _cuffTex.Apply(false, false);
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
            Jotunn.Logger.LogDebug($"WraithArmor: ogniwa z Chain - mesh={filter.sharedMesh.name} bounds={filter.sharedMesh.bounds} mat={(renderer ? renderer.sharedMaterial.name : "null")}");
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
            var hood = PrefabManager.Instance.GetPrefab("HelmetFenring"); // tymczasowo; BuildHood podmienia na ikone Kaptura Widma
            if (hood) se.m_icon = hood.GetComponent<ItemDrop>().m_itemData.m_shared.m_icons.FirstOrDefault();
            ItemManager.Instance.AddStatusEffect(new CustomStatusEffect(se, false));
            return se;
        }

        // ------------------------------------------------------------------ itemy

        private static void BuildHood(StatusEffect setEffect)
        {
            var item = BuildPiece(HoodName, "HelmetFenring", "item_wraithhood", Colorize, new Dictionary<string, int[]>
            {
                // koszty 1:1 z HelmetFenring: skora wilka -> ektoplazma, trofeum kultysty -> trofeum upiora
                //                           L1  L2  L3  L4
                { "TrophyWraith",   new[] {  1,  0,  0,  0 } },
                { "WolfHairBundle", new[] { 20,  5, 10, 15 } },
                { "Ectoplasm",      new[] {  2,  4,  8, 12 } },
            });
            var shared = item.ItemDrop.m_itemData.m_shared;
            ApplyArmorStats(shared, 1f, 0.1f, setEffect);
            // ikona efektu setu = przemalowana ikona Kaptura Widma (BuildSetEffect idzie przed kapturem i bral waniliowego Fenrisa)
            var hoodIcon = shared.m_icons.FirstOrDefault();
            if (setEffect && hoodIcon) setEffect.m_icon = hoodIcon;
            ReplaceMaterials(item.ItemPrefab, "FenringArmor_mat", _furMat);

            // Kolnierz kaptura odsuniety na zewnatrz peleryny (2-6 cm, liczone w ripie: CapeLab.PushHoodOverCape),
            // zeby peleryna wychodzila spod niego, a nie przykrywala go. Ta sama topologia, bindposes i wagi co FenringHood.
            var hoodMesh = ModAssets.Load<Mesh>("WraithHood_mesh");
            SkinnedMeshRenderer hoodSmr = null;
            foreach (var smr in item.ItemPrefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!smr.sharedMesh || smr.sharedMesh.name != "FenringHood") continue;
                hoodSmr = smr;
                if (hoodMesh && smr.sharedMesh.vertexCount == hoodMesh.vertexCount) smr.sharedMesh = hoodMesh;
            }

            // Gorny pas Peleryny Widma jako czesc kaptura: bez fizyki, wagi kolnierza, wiec w biegu nie wychodzi nad kolnierz
            // (peleryna pod kapturem ukryta do HoodedCutRow). AttachItem daje kosci ciala wszystkim SMR pod attach_skin.
            // Widoczny tylko z Peleryna Widma - przelacza WraithHoodedCape.
            // WYLACZONE (2026-09-27): osobny pas odjezdzal od peleryny ("dwie czesci") - zamiast tego sama peleryna ma gore
            // przypieta do kaptura (WraithCapeCloth.PinTopToHood). Kod zostaje na wypadek powrotu.
            var stripMesh = UseCapeStrip ? ModAssets.Load<Mesh>("WraithCapeStrip_mesh") : null;
            if (UseCapeStrip && hoodSmr && stripMesh && _capeMatStrip)
            {
                var strip = new GameObject(CapeStripName);
                strip.transform.SetParent(hoodSmr.transform.parent, false);
                strip.transform.localPosition = hoodSmr.transform.localPosition;
                strip.transform.localRotation = hoodSmr.transform.localRotation;
                strip.transform.localScale = hoodSmr.transform.localScale;
                var stripSmr = strip.AddComponent<SkinnedMeshRenderer>();
                stripSmr.sharedMesh = stripMesh;
                stripSmr.sharedMaterial = _capeMatStrip;
                stripSmr.bones = hoodSmr.bones;
                stripSmr.rootBone = hoodSmr.rootBone;
                stripSmr.updateWhenOffscreen = hoodSmr.updateWhenOffscreen;
                stripSmr.enabled = false;
                Jotunn.Logger.LogDebug($"WraithArmor: pas peleryny w kapturze ({stripMesh.vertexCount} wierzch.)");
            }
            else if (UseCapeStrip) Jotunn.Logger.LogWarning($"WraithArmor: brak pasa peleryny w kapturze (hood={(bool)hoodSmr} mesh={(bool)stripMesh} mat={(bool)_capeMatStrip})");
        }

        public const string CapeStripName = "WraithCapeStrip";
        private static readonly bool UseCapeStrip = false;

        private static void BuildRobe(StatusEffect setEffect, Mesh linkMesh, Material linkMat, Material cuffMat)
        {
            var item = BuildPiece(RobeName, "ArmorFenringChest", "item_wraithrobe", Colorize, new Dictionary<string, int[]>
            {
                // koszty 1:1 z ArmorFenringChest (skora wilka -> ektoplazma) + lancuchy na kajdany
                //                           L1  L2  L3  L4
                { "WolfHairBundle", new[] { 20,  5, 10, 15 } },
                { "Ectoplasm",      new[] {  5,  3,  6,  9 } },
                { "LeatherScraps",  new[] { 10,  4,  8, 12 } },
                { "Chain",          new[] {  5,  3,  3,  3 } },
            });
            var shared = item.ItemDrop.m_itemData.m_shared;
            ApplyArmorStats(shared, 5f, 0.2f, setEffect);
            ReplaceMaterials(item.ItemPrefab, "FenringArmor_mat", _furMat);
            shared.m_armorMaterial = _chestBodyMat;

            // lancuchy z kajdanami z nadgarstkow (kosci LeftHand / RightHand)
            AttachChain(item.ItemPrefab, "attach_LeftHand", cuffMat, LeftHandSegments, LeftHandLinksPerSegment);
            AttachChain(item.ItemPrefab, "attach_RightHand", cuffMat, RightHandSegments, RightHandLinksPerSegment);
        }

        private static void BuildLegs(StatusEffect setEffect)
        {
            var item = BuildPiece(LegsName, "ArmorFenringLegs", "item_wraithlegs", Colorize, new Dictionary<string, int[]>
            {
                // koszty 1:1 z ArmorFenringLegs (skora wilka -> ektoplazma)
                //                           L1  L2  L3  L4
                { "WolfHairBundle", new[] { 20,  5, 10, 15 } },
                { "Ectoplasm",      new[] {  5,  3,  6,  9 } },
                { "LeatherScraps",  new[] { 10,  4,  8, 12 } },
            });
            var shared = item.ItemDrop.m_itemData.m_shared;
            ApplyArmorStats(shared, 5f, 0.2f, setEffect);
            ReplaceMaterials(item.ItemPrefab, "FenringArmor_mat", _furMat);
            shared.m_armorMaterial = _legsBodyMat;
        }

        private static void BuildCape(StatusEffect setEffect, Mesh linkMesh, Material linkMat, Material cuffMat)
        {
            var item = BuildPiece(CapeName, "CapeTrollHide", "item_wraithcape", Colorize, new Dictionary<string, int[]>
            {
                // wzor CapeWolf (skora wilka 6 +4/poz., srebro 4 +2/poz., trofeum wilka):
                // skora -> siersc Fenrisa, srebro -> ektoplazma, trofeum -> trofeum upiora, + lancuchy
                //                           L1  L2  L3  L4
                { "WolfHairBundle", new[] {  6,  4,  8, 12 } },
                { "Ectoplasm",      new[] {  4,  2,  4,  6 } },
                { "TrophyWraith",   new[] {  1,  0,  0,  0 } },
                { "Chain",          new[] {  2,  2,  2,  2 } },
            });
            var shared = item.ItemDrop.m_itemData.m_shared;
            shared.m_armor = 1f;
            shared.m_armorPerLevel = 1f;
            shared.m_maxQuality = 4;
            shared.m_weight = 4f;
            shared.m_eitrRegenModifier = 0.1f;
            // baza nalezy do setu trolla - przepinamy na set Szat Widma (4. czesc), zeby nie liczyla sie do SetEffect_TrollArmor
            shared.m_setName = SetName;
            shared.m_setSize = SetSize;
            shared.m_setStatusEffect = setEffect;
            shared.m_damageModifiers = new List<HitData.DamageModPair>
            {
                new HitData.DamageModPair { m_type = HitData.DamageType.Frost, m_modifier = HitData.DamageModifier.Resistant },
            };
            ReplaceMaterials(item.ItemPrefab, "CapeTrollHide", _capeMat);
            WraithCapeCloth.ApplyToPrefab(item.ItemPrefab); // blizej ciala niz troll (promien czastek 0.085 -> 0.03)
            WraithCapeCloth.PinTopToHood(item.ItemPrefab);  // gora bez fizyki, z wagami kolnierza kaptura

            // dwa lancuchy "naszyte" na pelerynie (ClothChain): sledza wierzcholki tkaniny cape2 (troll)
            // w kolumnach x = -0.12 / +0.12 (przestrzen postaci), wiec zawsze leza na plaszczu.
            // Wczesniejsze podejscia (stala sila do tylu, kapsula tulowia, pochylona kapsula) przegrywaly
            // z symulacja tkaniny - lancuchy ladowaly pod spodem. Root "attach_Spine2" tylko po to,
            // zeby AttachArmor zainstancjonowal i aktywowal obiekt razem z peleryna.
            // Domyslnie WYLACZONE (CapeChains3D=false): lancuchy sa namalowane w WraithCape.png (jak pasy na CapeDeepNorthMage),
            // bo ogniwa przyklejone do 96 wierzcholkow tkaniny skakaly miedzy nimi. `wraithtint capechains on` wlacza je na zywo.
            var root = new GameObject(CapeChains3D ? "attach_Spine2" : "chains3d_off");
            root.transform.SetParent(item.ItemPrefab.transform, false);
            root.SetActive(false);
            var capeLeftMesh = DanglingChain.BuildLinkChainMesh(CapeLeftLinksPerSegment, ChainLinkLength, ChainLinkWidth, ChainTubeRadius, ChainPitch);
            var capeRightMesh = DanglingChain.BuildLinkChainMesh(CapeRightLinksPerSegment, ChainLinkLength, ChainLinkWidth, ChainTubeRadius, ChainPitch);
            ClothChain.Build("chain_left", capeLeftMesh, cuffMat, CapeLeftSegments, 1f, capeLeftMesh.bounds.size.y, -0.12f, cuffMat)
                .transform.SetParent(root.transform, false);
            ClothChain.Build("chain_right", capeRightMesh, cuffMat, CapeRightSegments, 1f, capeRightMesh.bounds.size.y, 0.12f, cuffMat)
                .transform.SetParent(root.transform, false);
            _capeChainsRoot = root;
        }

        private static GameObject _capeChainsRoot;

        /// <summary>
        /// Dev: wlacza/wylacza lancuchy 3D na pelerynie. Na prefabie zmienia nazwe roota (AttachArmor instancjonuje tylko
        /// dzieci "attach_*"), na zalozonych juz pelerynach gasi/zapala instancje ClothChain w scenie. Nowe zalozenie peleryny
        /// (zdjac/zalozyc) bierze stan z prefabu.
        /// </summary>
        public static string SetCapeChains3D(bool on)
        {
            CapeChains3D = on;
            if (_capeChainsRoot) _capeChainsRoot.name = on ? "attach_Spine2" : "chains3d_off";
            var live = 0;
            foreach (var chain in UnityEngine.Object.FindObjectsOfType<ClothChain>(true))
            {
                if (chain.transform.parent && chain.transform.parent.gameObject.scene.IsValid())
                {
                    chain.transform.parent.gameObject.SetActive(on);
                    live++;
                }
            }
            return $"lancuchy 3D na pelerynie: {(on ? "wlaczone" : "wylaczone")} (prefab), {live} instancji w scenie {(on ? "zapalonych" : "zgaszonych")}; zdejmij i zaloz peleryne, zeby odswiezyc";
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
                // jak waniliowy set Fenrisa, z ktorego klonujemy - warsztat, nie kuznia
                CraftingStation = "piece_workbench",
                RepairStation = "piece_workbench",
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
            shared.m_setSize = SetSize;
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

        private static void AttachChain(GameObject prefab, string attachName, Material cuffMat, int segments, int linksPerSegment)
        {
            var mesh = DanglingChain.BuildLinkChainMesh(linksPerSegment, ChainLinkLength, ChainLinkWidth, ChainTubeRadius, ChainPitch);
            var chain = DanglingChain.Build(attachName, mesh, cuffMat, segments, 1f, 1f, Vector3.zero, Vector3.zero, cuffMat);
            chain.transform.SetParent(prefab.transform, false);
            // jak waniliowe attach_skin: nieaktywne w prefabie, AttachArmor aktywuje instancje
            chain.SetActive(false);
        }
    }
}
