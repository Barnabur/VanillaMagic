using System;
using System.Linq;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace VanilaMagic.Items
{
    /// <summary>
    /// StoneWand (rozdzka z rownin) skladana w calosci w kodzie z waniliowych czesci:
    /// - baza (animacja staff_fireball, pojedynczy strzal): StaffFireball
    /// - pocisk: klon troll_throw_projectile (glaz rzucany przez trolla, material "stone")
    ///   przeskalowany w dol; na trafieniu vfx_stonegolem_attack_hit (odlamki skaly, pyl,
    ///   iskry) + dzwiek rozbicia trollowego glazu
    /// - rekojesc: zwykly walec generowany w kodzie; material = Standard z itemu FineWood z tekstura
    ///   sloi ze slupa wood_pole (jedyna kafelkowana tekstura drewna; atlasy itemow na walcu sie rozjezdzaja)
    /// - okucia: stopka, pierscien i cztery szpony z czarnego metalu (material "blackmetal"
    ///   ze sztabki BlackMetal)
    /// - glowica: bryla obsydianu - dziecko "model" itemu Obsidian (material obsidian_nosnow),
    ///   przeskalowana po bounds, trzymana w szponach
    /// Obrazenia obuchowe z duzym odrzutem - jedyna magia obuchowa na dystans.
    /// </summary>
    internal static class StoneWand
    {
        public const string PrefabName = "StoneWand";
        private const string ProjectileName = "wand_stonebolt_projectile";

        // balans: cios ponad bron jednoreczna z czarnego metalu (95-113), okupiony
        // ogromnym kosztem eitru - 2-3 strzaly z pelnego paska, potem trzeba czekac
        private const float BluntDamage = 240f;
        private const float BluntDamagePerLevel = 10f;
        private const float AttackForce = 100f;
        private const float AttackEitr = 50f;
        private const float ExplosionRadius = 2f;
        // skala wizualu glazu w pocisku (vanilla trollowy glaz ma 0.2293)
        private const float ProjectileRockScale = 0.08f;
        // dzwiek wystrzalu: klon sfx_troll_attack_hit (sam klip Hit_DeepHugeChestThump1 - gleboki
        // tapniecie w piers trolla), sciszony i lekko obnizony; vanilla ma vol 1, pitch 0.9-1.1
        private const string CastSfxName = "sfx_wandstone_cast";
        private const string CastSfxSource = "sfx_troll_attack_hit";
        private const float CastSfxVolume = 0.25f;
        private const float CastSfxMinPitch = 0.75f;
        private const float CastSfxMaxPitch = 0.85f;
        // opoznienie odtworzenia od momentu castu (sekundy); ZSFX losuje z min-max
        private const float CastSfxDelay = 0.4f;

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += Create;
        }

        private static void Create()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= Create;
            try
            {
                var projectile = BuildProjectile();
                BuildWand(projectile);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"StoneWand: skladanie itemu nie powiodlo sie: {ex}");
            }
        }

        /// <summary>
        /// Klon troll_throw_projectile. Vanilla: aoe 0, gravity 10, ttl 4, rayRadius 0.5,
        /// hitEffects = sfx/vfx_troll_rock_destroyed, brak raytestu wlasciciela.
        /// Zmiany: maly obszar, grawitacja jak w fireballu (atak StaffFireball strojony pod 4),
        /// cienszy promien trafienia, raytest wlasciciela, mniejszy glaz, golemowy efekt trafienia.
        /// </summary>
        private static GameObject BuildProjectile()
        {
            var projectile = PrefabManager.Instance.CreateClonedPrefab(ProjectileName, "troll_throw_projectile");
            var proj = projectile.GetComponent<Projectile>();
            proj.m_aoe = ExplosionRadius;
            proj.m_gravity = 4f;
            proj.m_ttl = 10f;
            proj.m_rayRadius = 0.2f;
            proj.m_doOwnerRaytest = true;
            proj.m_hitNoise = 30f;

            var rock = projectile.transform.Find("default");
            if (rock)
            {
                rock.localScale = Vector3.one * ProjectileRockScale;
            }
            else
            {
                Jotunn.Logger.LogWarning("StoneWand: brak wezla 'default' (glaz) w klonie troll_throw_projectile");
            }

            // trafienie: odlamki skaly + pyl + iskry golema; dzwiek rozbicia glazu zostaje z trolla
            var golemHit = PrefabManager.Instance.GetPrefab("vfx_stonegolem_attack_hit");
            var rockSfx = PrefabManager.Instance.GetPrefab("sfx_troll_rock_destroyed");
            var hitFx = new[] { golemHit, rockSfx }.Where(p => p).Select(p => new EffectList.EffectData { m_prefab = p }).ToArray();
            if (hitFx.Length > 0)
            {
                proj.m_hitEffects = new EffectList { m_effectPrefabs = hitFx };
            }
            else
            {
                Jotunn.Logger.LogWarning("StoneWand: brak vfx_stonegolem_attack_hit/sfx_troll_rock_destroyed - zostaje efekt trolla");
            }
            Jotunn.Logger.LogDebug(
                $"StoneWand: pocisk - hitEffects=" +
                string.Join(",", proj.m_hitEffects?.m_effectPrefabs?.Select(e => e.m_prefab ? e.m_prefab.name : "null") ?? new string[0]) +
                $", rayRadius={proj.m_rayRadius}, glaz={(rock ? rock.localScale.x.ToString("0.###") : "brak")}");

            PrefabManager.Instance.AddPrefab(new CustomPrefab(projectile, false));
            return projectile;
        }

        private static void BuildWand(GameObject projectile)
        {
            // ikona: kostur pekania (Ashlands) wyglada najbardziej "kamiennie"; fallback kostur ognia
            var iconSource = PrefabManager.Instance.GetPrefab("StaffClusterbomb") ?? PrefabManager.Instance.GetPrefab("StaffFireball");
            var iconShared = iconSource.GetComponent<ItemDrop>().m_itemData.m_shared;

            var config = new ItemConfig
            {
                Name = "$item_wandstone",
                Description = "$item_wandstone_description",
                Icons = new[] { iconShared.m_icons[0] },
                CraftingStation = "forge",
                RepairStation = "forge",
                // jak waniliowa bron z czarnego metalu: kuznia 4, q4 przy kuzni 7
                MinStationLevel = 4,
                Requirements = new[]
                {
                    new RequirementConfig("BlackMetal", 18, 7),
                    new RequirementConfig("Obsidian", 4, 2),
                    new RequirementConfig("FineWood", 5, 2),
                },
            };

            var item = new CustomItem(PrefabName, "StaffFireball", config);
            ItemManager.Instance.AddItem(item);

            var shared = item.ItemDrop.m_itemData.m_shared;
            // wytrzymalosc wspolna dla wszystkich rozdzek: 100 +25 na poziom, -1 za kazdy strzal
            shared.m_maxDurability = 100f;
            shared.m_durabilityPerLevel = 25f;
            shared.m_useDurability = true;
            shared.m_useDurabilityDrain = 1f;
            // StaffFireball ma blunt 120 + fire 120 (+6 fire/lvl) - zerujemy i dajemy czysty blunt
            shared.m_damages = new HitData.DamageTypes { m_blunt = BluntDamage };
            shared.m_damagesPerLevel = new HitData.DamageTypes { m_blunt = BluntDamagePerLevel };
            shared.m_attackForce = AttackForce;
            // Krotka rozdzka, nie kostur: idle/blok z warstwy broni jednorecznych (jak MaceSilver),
            // a nie "Staves" po bazowym kosturze. Triggery atakow sa globalne w animatorze.
            shared.m_animationState = ItemDrop.ItemData.AnimationState.OneHanded;

            var attack = shared.m_attack;
            attack.m_attackProjectile = projectile;
            attack.m_attackEitr = AttackEitr;
            // rozdzka jest krotka (glowica ~0.55 m od dloni) - obnizamy start pocisku jak w FrostWand
            attack.m_attackHeight -= 0.3f;
            // glaz jest ciezki - strzelamy ~5 stopni wyzej niz fireball (vanilla launchAngle -5,
            // w Valheim ujemna wartosc = wyzej), zeby lot mial wyrazniejszy luk
            attack.m_launchAngle -= 5f;
            Jotunn.Logger.LogDebug($"StoneWand: launchAngle={attack.m_launchAngle}, vel={attack.m_projectileVel}");

            // dzwiek wystrzalu: cichy, obnizony thump trolla zamiast ognistego "whoosh";
            // alternatywy: sfx_stonegolem_primary_start (zgrzyt skaly), sfx_greydwarf_throw (lekki rzut kamieniem)
            var castSfx = BuildCastSfx();
            if (castSfx)
            {
                shared.m_startEffect = new EffectList
                {
                    m_effectPrefabs = new[] { new EffectList.EffectData { m_prefab = castSfx } }
                };
            }
            else
            {
                Jotunn.Logger.LogWarning($"StoneWand: brak {CastSfxSource} - zostaje dzwiek fire staffa");
            }

            BuildVisual(item.ItemPrefab);
            // Render ikony DOPIERO po zlozeniu modelu - inaczej lapie golego klona bazy
            RenderedIcons.Register(PrefabName);
        }

        /// <summary>
        /// Klon sfx_troll_attack_hit (ZSFX + AudioSource + ZNetView + CamShaker) z jednym klipem
        /// Hit_DeepHugeChestThump1. Glosnosc na 25%, pitch obnizony, bez trzesienia kamery
        /// (to hit trolla w gracza, nie strzal). Klip bierzemy z wanilii, wiec nic nie wozimy w bundlu.
        /// </summary>
        private static GameObject BuildCastSfx()
        {
            if (!PrefabManager.Instance.GetPrefab(CastSfxSource)) return null;
            var sfx = PrefabManager.Instance.CreateClonedPrefab(CastSfxName, CastSfxSource);
            var zsfx = sfx.GetComponent<ZSFX>();
            if (!zsfx)
            {
                Jotunn.Logger.LogWarning($"StoneWand: klon {CastSfxSource} nie ma ZSFX");
                return null;
            }
            zsfx.m_minVol = CastSfxVolume;
            zsfx.m_maxVol = CastSfxVolume;
            zsfx.m_minPitch = CastSfxMinPitch;
            zsfx.m_maxPitch = CastSfxMaxPitch;
            zsfx.m_useVibration = false;
            zsfx.m_minDelay = CastSfxDelay;
            zsfx.m_maxDelay = CastSfxDelay;
            var shake = sfx.GetComponent<CamShaker>();
            if (shake) UnityEngine.Object.DestroyImmediate(shake, true);

            PrefabManager.Instance.AddPrefab(new CustomPrefab(sfx, false));
            Jotunn.Logger.LogDebug($"StoneWand: {CastSfxName} <- {CastSfxSource}, vol={CastSfxVolume}, pitch={CastSfxMinPitch}-{CastSfxMaxPitch}, delay={CastSfxDelay}s, " +
                $"klipy={string.Join(",", zsfx.m_audioClips.Select(c => c ? c.name : "null"))}");
            return sfx;
        }

        // Wymiary (metry, wzdluz osi Y siatki): uchwyt -> szpony z kamieniem -> koniec szponow
        private const float HandleEnd = 0.42f;
        private const float ClawEnd = 0.66f;
        private const float StoneCenter = (HandleEnd + ClawEnd) * 0.5f;
        private const float ClawRadius = 0.068f;
        // najdluzszy wymiar obsydianu w glowicy oraz minimalna grubosc jako ulamek dlugosci
        // (waniliowy obsydian to plaska plytka ~1.8:0.5:1.6 - najciensza os jest rozciagana)
        private const float StoneSize = 0.154f;
        private const float StoneMinThickness = 0.7f;
        // mnoznik osi poprzecznych (1 = proporcje po rozciagnieciu, mniej = smuklejsza bryla)
        private const float StoneWidth = 0.75f;

        private static void BuildVisual(GameObject prefab)
        {
            var metalMat = FindModelMaterial("BlackMetal", "bar") ?? FindLoadedMaterial("blackmetal");
            var stoneItem = PrefabManager.Instance.GetPrefab("Obsidian");
            var stoneSrc = stoneItem ? stoneItem.transform.Find("model") : null;
            var woodMat = BuildWoodMaterial();
            if (!metalMat || !stoneSrc || !woodMat)
            {
                Jotunn.Logger.LogWarning(
                    $"StoneWand: brak czesci modelu (blackmetal={(bool)metalMat}, Obsidian/model={(bool)stoneSrc}, drewno={(bool)woodMat}) " +
                    "- zostaje model kostura ognia");
                return;
            }

            // Wizuale ida pod "attach" (patrz FrostWand) - czyscimy model kostura ognia.
            var holder = prefab.transform.Find("attach");
            if (!holder)
            {
                Jotunn.Logger.LogWarning("StoneWand: brak wezla 'attach' w klonie StaffFireball");
                return;
            }
            foreach (var child in holder.Cast<Transform>().ToArray())
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            holder.localRotation = Quaternion.Euler(0f, 0f, 180f);

            var itemLayer = LayerMask.NameToLayer("item");

            // Dwie siatki (dwa materialy): drewniana rekojesc i czarnometalowe okucia.
            // Os rozdzki = lokalne +Y siatki; obrot 90 wokol X klade ja wzdluz +Z attach.
            var wand = AddMeshChild(holder, "wand", BuildHandleMesh(), woodMat, itemLayer);
            // collidery sa niezbedne: bez nich item przelatuje przez podloge i nie da sie go podniesc
            var wandCollider = wand.AddComponent<BoxCollider>();
            wandCollider.size = new Vector3(0.14f, ClawEnd + 0.04f, 0.14f);
            wandCollider.center = new Vector3(0f, ClawEnd * 0.5f, 0f);
            AddMeshChild(holder, "fittings", BuildFittingsMesh(), metalMat, itemLayer);

            // Glowica: obsydian z itemu Obsidian (material obsidian_nosnow, vanilla scale 0.3).
            // Waniliowy mesh to UKOSNY odlamek - jego os glowna jest odchylona ~15 stopni od osi bounding
            // boxa, wiec ustawiony "po bounds" siedzi w klatce po skosie i cala glowica wyglada na zgieta.
            // Dlatego: osie glowne z PCA po wierzcholkach -> dwa wezly: "stone" (skret + skala w ukladzie
            // wyrownanym) i pod nim model (obrot PCA -> osie, centrowanie). Fallback: osie bounds.
            var stoneRoot = new GameObject("stone");
            stoneRoot.layer = itemLayer;
            stoneRoot.transform.SetParent(holder, false);
            var stone = UnityEngine.Object.Instantiate(stoneSrc.gameObject, stoneRoot.transform, false);
            stone.name = "model";
            stone.layer = itemLayer;
            foreach (var c in stone.GetComponents<Collider>()) UnityEngine.Object.DestroyImmediate(c);
            var lod = stone.GetComponent<LODGroup>();
            if (lod) UnityEngine.Object.DestroyImmediate(lod);
            var stoneFilter = stone.GetComponent<MeshFilter>();
            var stoneMesh = stoneFilter ? stoneFilter.sharedMesh : null;

            Vector3 axisLong, axisMid, axisThin, frameCenter; // w przestrzeni siatki
            Vector3 extents; // zakresy wzdluz osi (long, mid, thin) w jednostkach siatki
            var usedPca = ComputePrincipalFrame(stoneMesh, out axisLong, out axisMid, out axisThin, out frameCenter, out extents);
            extents *= stoneSrc.localScale.x; // wymiary vanilla
            // model: os glowna -> +Z (os rozdzki), najciensza -> +Y; srodek ramki w origin wezla "stone"
            var toAxes = Quaternion.Inverse(Quaternion.LookRotation(axisLong, axisThin));
            stone.transform.localRotation = toAxes;
            stone.transform.localScale = stoneSrc.localScale;
            stone.transform.localPosition = -(toAxes * Vector3.Scale(frameCenter, stoneSrc.localScale));

            // skala w ukladzie wyrownanym: Z = dlugosc, Y = najciensza (rozciagana), X = srednia
            var longest = Mathf.Max(extents.x, 0.001f);
            var scale = Vector3.one * (StoneSize / longest);
            // plytka -> bryla: najciensza os rozciagnieta do StoneMinThickness dlugosci
            scale.y *= Mathf.Clamp(StoneMinThickness * longest / Mathf.Max(extents.z, 0.001f), 1f, 5f);
            // osie poprzeczne zwezone, zeby bryla miescila sie w szponach
            scale.x *= StoneWidth;
            scale.y *= StoneWidth;
            stoneRoot.transform.localScale = scale;
            stoneRoot.transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
            stoneRoot.transform.localPosition = new Vector3(0f, 0f, StoneCenter);
            Jotunn.Logger.LogDebug(
                $"StoneWand: obsydian mesh={(stoneMesh ? stoneMesh.name : "null")} pca={usedPca} osie long={axisLong} thin={axisThin} " +
                $"srodek={frameCenter} -> wymiary vanilla (long,mid,thin)={extents}, skala={scale}");
        }

        /// <summary>
        /// Osie glowne siatki (PCA po wierzcholkach, 3x3 Jacobi) posortowane malejaco po rozrzucie,
        /// srodek = polowa zakresu rzutow na kazda os, extents = zakresy rzutow (long, mid, thin).
        /// Gdy wierzcholki sa nieczytelne, zwraca osie i srodek bounds (false).
        /// </summary>
        private static bool ComputePrincipalFrame(Mesh mesh, out Vector3 axisLong, out Vector3 axisMid, out Vector3 axisThin, out Vector3 center, out Vector3 extents)
        {
            Vector3[] verts = null;
            try { if (mesh) verts = mesh.vertices; }
            catch (Exception ex) { Jotunn.Logger.LogWarning($"StoneWand: mesh obsydianu nieczytelny ({ex.Message}) - osie bounds"); }

            if (verts == null || verts.Length < 4)
            {
                var b = mesh ? mesh.bounds : new Bounds(Vector3.zero, Vector3.one);
                var size = b.size;
                var order = new[] { 0, 1, 2 }.OrderByDescending(i => size[i]).ToArray();
                Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;
                axisLong = Axis(order[0]); axisMid = Axis(order[1]); axisThin = Axis(order[2]);
                center = b.center;
                extents = new Vector3(size[order[0]], size[order[1]], size[order[2]]);
                return false;
            }

            var mean = Vector3.zero;
            foreach (var v in verts) mean += v;
            mean /= verts.Length;
            // kowariancja
            var c = new double[3, 3];
            foreach (var v in verts)
            {
                var d = v - mean;
                c[0, 0] += d.x * d.x; c[0, 1] += d.x * d.y; c[0, 2] += d.x * d.z;
                c[1, 1] += d.y * d.y; c[1, 2] += d.y * d.z; c[2, 2] += d.z * d.z;
            }
            c[1, 0] = c[0, 1]; c[2, 0] = c[0, 2]; c[2, 1] = c[1, 2];
            // Jacobi: diagonalizacja przez obroty, vec zbiera wektory wlasne w kolumnach
            var vec = new double[3, 3] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
            for (var sweep = 0; sweep < 50; sweep++)
            {
                var off = Math.Abs(c[0, 1]) + Math.Abs(c[0, 2]) + Math.Abs(c[1, 2]);
                if (off < 1e-12) break;
                for (var p = 0; p < 2; p++)
                for (var q = p + 1; q < 3; q++)
                {
                    if (Math.Abs(c[p, q]) < 1e-15) continue;
                    var theta = 0.5 * Math.Atan2(2 * c[p, q], c[q, q] - c[p, p]);
                    var cs = Math.Cos(theta); var sn = Math.Sin(theta);
                    for (var k = 0; k < 3; k++)
                    {
                        var ckp = c[k, p]; var ckq = c[k, q];
                        c[k, p] = cs * ckp - sn * ckq; c[k, q] = sn * ckp + cs * ckq;
                    }
                    for (var k = 0; k < 3; k++)
                    {
                        var cpk = c[p, k]; var cqk = c[q, k];
                        c[p, k] = cs * cpk - sn * cqk; c[q, k] = sn * cpk + cs * cqk;
                    }
                    for (var k = 0; k < 3; k++)
                    {
                        var vkp = vec[k, p]; var vkq = vec[k, q];
                        vec[k, p] = cs * vkp - sn * vkq; vec[k, q] = sn * vkp + cs * vkq;
                    }
                }
            }
            var eig = new[] { c[0, 0], c[1, 1], c[2, 2] };
            var idx = new[] { 0, 1, 2 }.OrderByDescending(i => eig[i]).ToArray();
            Vector3 Col(int j) => new Vector3((float)vec[0, j], (float)vec[1, j], (float)vec[2, j]).normalized;
            axisLong = Col(idx[0]); axisMid = Col(idx[1]); axisThin = Col(idx[2]);

            // zakresy rzutow na osie i srodek ramki (polowa zakresu, nie centroid)
            float minL = float.MaxValue, maxL = float.MinValue, minM = float.MaxValue, maxM = float.MinValue, minT = float.MaxValue, maxT = float.MinValue;
            foreach (var v in verts)
            {
                var d = v - mean;
                var l = Vector3.Dot(d, axisLong); var m = Vector3.Dot(d, axisMid); var t = Vector3.Dot(d, axisThin);
                if (l < minL) minL = l; if (l > maxL) maxL = l;
                if (m < minM) minM = m; if (m > maxM) maxM = m;
                if (t < minT) minT = t; if (t > maxT) maxT = t;
            }
            center = mean + axisLong * ((minL + maxL) * 0.5f) + axisMid * ((minM + maxM) * 0.5f) + axisThin * ((minT + maxT) * 0.5f);
            extents = new Vector3(maxL - minL, maxM - minM, maxT - minT);
            return true;
        }

        private static GameObject AddMeshChild(Transform holder, string name, Mesh mesh, Material material, int layer)
        {
            var go = new GameObject(name);
            // auto-pickup gracza skanuje collidery wylacznie na warstwie "item"
            go.layer = layer;
            go.transform.SetParent(holder, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        // Rekojesc: zwykly walec o stalym promieniu, zamkniety od dolu (od gory chowa sie pod kolnierz)
        private const float HandleRadius = 0.023f;

        private static Mesh BuildHandleMesh()
        {
            var mb = new TubeBuilder();
            mb.AddTube(
                t => new Vector3(0f, Mathf.Lerp(-0.005f, HandleEnd, t), 0f),
                t => t < 0.03f ? Mathf.Lerp(0f, HandleRadius, t / 0.03f) : HandleRadius,
                40, 16);
            return mb.ToMesh("stonewand_handle");
        }

        /// <summary>
        /// Material drewna: kopia materialu itemu FineWood (shader Standard, swieci sie jak inne itemy
        /// w rece) z podmieniona tekstura na kafelkowane sloje ze slupa wood_pole ("woodpole").
        /// Materialy itemow drewna to atlasy i na walcu daja plamy; material slupa ma z kolei shader
        /// budowli (Custom/Piece) i na trzymanym itemie wychodzi prawie czarny.
        /// </summary>
        private static Material BuildWoodMaterial()
        {
            var baseMat = FindModelMaterial("FineWood", "default") ?? FindLoadedMaterial("finewood_item");
            var grainMat = FindModelMaterial("wood_pole", "Cube_Cube_Material") ?? FindLoadedMaterial("woodpole");
            if (!baseMat || !grainMat)
            {
                Jotunn.Logger.LogWarning($"StoneWand: brak materialow drewna (finewood_item={(bool)baseMat}, woodpole={(bool)grainMat})");
                return baseMat ? baseMat : grainMat;
            }
            var mat = new Material(baseMat) { name = "stonewand_wood" };
            mat.mainTexture = grainMat.mainTexture;
            mat.mainTextureScale = Vector2.one;
            mat.mainTextureOffset = Vector2.zero;
            // mapa normalnych z atlasu fine wood nie pasuje do sloi - wylaczamy
            if (mat.HasProperty("_BumpMap")) mat.SetTexture("_BumpMap", null);
            if (mat.HasProperty("_MetallicGlossMap")) mat.SetTexture("_MetallicGlossMap", null);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.25f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            Jotunn.Logger.LogDebug($"StoneWand: material drewna - shader={mat.shader.name}, tekstura={(mat.mainTexture ? mat.mainTexture.name : "null")}");
            return mat;
        }

        /// <summary>
        /// Czarnometalowe okucia: stopka (zamknieta od dolu), pierscien u nasady glowicy
        /// oraz cztery szpony wychylajace sie na boki wokol kamienia i schodzace do siebie na koncu.
        /// </summary>
        private static Mesh BuildFittingsMesh()
        {
            var mb = new TubeBuilder();

            // stopka: stozek do zera -> walec o promieniu 0.027, konczy sie 0.05 nad dolem
            mb.AddTube(
                t => new Vector3(0f, Mathf.Lerp(-0.015f, 0.05f, t), 0f),
                t => t < 0.2f ? Mathf.Lerp(0f, 0.027f, t / 0.2f) : 0.027f,
                16, 12);

            // pierscien (kolnierz) u nasady szponow
            mb.AddTube(
                t => new Vector3(0f, Mathf.Lerp(HandleEnd - 0.03f, HandleEnd + 0.01f, t), 0f),
                t => 0.025f,
                8, 12);

            // cztery szpony
            for (var claw = 0; claw < 4; claw++)
            {
                var angle = claw * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
                mb.AddTube(
                    t =>
                    {
                        // wychylenie: 0.014 przy kolnierzu, ClawRadius w polowie, 0.012 na czubku
                        var radial = 0.012f + Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 0.8f) * (ClawRadius - 0.012f);
                        // lekki skret wokol osi, zeby szpony nie byly plaskie
                        var a = angle + t * 0.35f;
                        return new Vector3(Mathf.Cos(a) * radial, Mathf.Lerp(HandleEnd, ClawEnd, t), Mathf.Sin(a) * radial);
                    },
                    t => Mathf.Lerp(0.011f, 0.004f, Mathf.Pow(t, 1.5f)),
                    40, 8);
            }

            return mb.ToMesh("stonewand_fittings");
        }

        private static Material FindModelMaterial(string prefabName, string meshName)
        {
            var go = PrefabManager.Instance.GetPrefab(prefabName);
            if (!go)
            {
                Jotunn.Logger.LogWarning($"StoneWand: nie znaleziono prefabu '{prefabName}'");
                return null;
            }
            foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh && filter.sharedMesh.name == meshName)
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    return renderer ? renderer.sharedMaterial : null;
                }
            }
            return null;
        }

        private static Material FindLoadedMaterial(string name)
        {
            return Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m.name == name);
        }
    }
}
