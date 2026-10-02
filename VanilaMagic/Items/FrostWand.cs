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
    /// FrostWand skladany w calosci w kodzie z waniliowych czesci (kitbash):
    /// - baza (animacja staff_fireball, pojedynczy strzal, dzwieki): StaffFireball
    /// - pocisk: klon DvergerStaffIce_projectile (lodowa kula maga dvergrow z dymnym
    ///   ogonem; wybuch fx_DvergerMage_Ice_hit = iskry, odlamki lodu, mgla, dzwiek)
    /// - trzonek: siatka generowana w kodzie (uchwyt + podwojna spirala oplatajaca
    ///   krysztal + szpic), material pochodny od "silverbar" ze sztabki srebra (rozjasniony)
    /// - glowica: pojedynczy krysztal - dziecko "Cube" itemu Crystal (crystal_exterior),
    ///   przeskalowany po bounds siatki do CrystalLength (siatka ma ~7 jednostek, nie 1)
    /// - efekty: niebieski flare i sniezynki z efektow StaffIceShards
    /// Nic nie jest shipowane w bundlu - wszystkie assety pochodza z zaladowanej gry.
    /// </summary>
    internal static class FrostWand
    {
        public const string PrefabName = "FrostWand";
        private const string ProjectileName = "wand_frostbolt_projectile";

        // balans: tempo i eitr jak Staff of Embers (baza StaffFireball, 35 eitr), obrazenia
        // ~40% jego 240 - AoE, wiec celowo ponizej srebrnej broni bialej na jednym celu
        private const float FrostDamage = 90f;
        private const float FrostDamagePerLevel = 6f;
        private const float AttackEitr = 35f;
        private const float ExplosionRadius = 1.3f;

        public static void Register()
        {
            PrefabManager.OnVanillaPrefabsAvailable += Create;
        }

        private static void Create()
        {
            // Item tworzymy tylko raz (event odpala sie przy kazdym wejsciu do main scene)
            PrefabManager.OnVanillaPrefabsAvailable -= Create;
            try
            {
                var projectile = BuildProjectile();
                BuildWand(projectile);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"FrostWand: skladanie itemu nie powiodlo sie: {ex}");
            }
        }

        /// <summary>
        /// Klon DvergerStaffIce_projectile. Vanilla: aoe 3, gravity 5, ttl 4, rayRadius 0.2,
        /// hitEffects = fx_DvergerMage_Ice_hit (zawiera sfx). Zmiany: mniejszy obszar,
        /// grawitacja jak w fireballu (atak StaffFireball ma launchAngle -5 strojony pod 4),
        /// raytest wlasciciela jak w pocisku gracza.
        /// </summary>
        private static GameObject BuildProjectile()
        {
            var projectile = PrefabManager.Instance.CreateClonedPrefab(ProjectileName, "DvergerStaffIce_projectile");
            var proj = projectile.GetComponent<Projectile>();
            proj.m_aoe = ExplosionRadius;
            proj.m_gravity = 4f;
            proj.m_ttl = 10f;
            proj.m_doOwnerRaytest = true;
            proj.m_hitNoise = 30f;
            Jotunn.Logger.LogDebug(
                $"FrostWand: pocisk bazowy - hitEffects=" +
                string.Join(",", proj.m_hitEffects?.m_effectPrefabs?.Select(e => e.m_prefab ? e.m_prefab.name : "null") ?? new string[0]) +
                $", spawnOnHit={(proj.m_spawnOnHit ? proj.m_spawnOnHit.name : "null")}, rayRadius={proj.m_rayRadius}");

            PrefabManager.Instance.AddPrefab(new CustomPrefab(projectile, false));
            return projectile;
        }

        private static void BuildWand(GameObject projectile)
        {
            var iceStaffShared = PrefabManager.Instance.GetPrefab("StaffIceShards")
                .GetComponent<ItemDrop>().m_itemData.m_shared;

            var config = new ItemConfig
            {
                Name = "$item_wandfrost",
                Description = "$item_wandfrost_description",
                Icons = new[] { iceStaffShared.m_icons[0] },
                CraftingStation = "forge",
                RepairStation = "forge",
                // jak waniliowa bron ze srebra: kuznia 3, q4 przy kuzni 6
                MinStationLevel = 3,
                Requirements = new[]
                {
                    new RequirementConfig("Silver", 5, 2),
                    new RequirementConfig("Crystal", 1, 1),
                    // polowa gruczolow z Staff of Frost (4 +2/poziom)
                    new RequirementConfig("FreezeGland", 2, 1),
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
            // StaffFireball ma blunt 120 + fire 120 (+6 fire/lvl) - zerujemy i dajemy frost
            shared.m_damages = new HitData.DamageTypes { m_frost = FrostDamage };
            shared.m_damagesPerLevel = new HitData.DamageTypes { m_frost = FrostDamagePerLevel };
            shared.m_attackForce = 40f;
            // Krotka rozdzka, nie kostur: idle/blok z warstwy broni jednorecznych (jak MaceSilver),
            // a nie "Staves" po StaffFireball. Trigger ataku "staff_fireball" jest globalny w animatorze.
            shared.m_animationState = ItemDrop.ItemData.AnimationState.OneHanded;

            var attack = shared.m_attack;
            attack.m_attackProjectile = projectile;
            attack.m_attackEitr = AttackEitr;
            Jotunn.Logger.LogDebug(
                $"{PrefabName}: bazowy atak StaffFireball - vel={attack.m_projectileVel}, " +
                $"launchAngle={attack.m_launchAngle}, wysokosc={attack.m_attackHeight}, offset={attack.m_attackOffset}");
            // rozdzka jest krotka (glowica ~0.55 m od dloni) - fireball vanilla startuje
            // z wysokosci 1.2 (czubek dlugiego kostura); obnizamy jak w SurtlingWand
            attack.m_attackHeight -= 0.3f;

            // dzwiek castu: lodowy cast maga dvergrow zamiast ognistego "whoosh";
            // trailStartEffect (sfx_bomb_throw) zostaje
            var castSfx = PrefabManager.Instance.GetPrefab("sfx_dverger_ice_projectile_start");
            if (castSfx)
            {
                shared.m_startEffect = new EffectList
                {
                    m_effectPrefabs = new[] { new EffectList.EffectData { m_prefab = castSfx } }
                };
            }
            else
            {
                Jotunn.Logger.LogWarning("FrostWand: brak sfx_dverger_ice_projectile_start - zostaje dzwiek fire staffa");
            }

            BuildVisual(item.ItemPrefab);
            // Render ikony DOPIERO po zlozeniu modelu - inaczej lapie golego klona bazy
            RenderedIcons.Register(PrefabName);
        }

        private static void BuildVisual(GameObject prefab)
        {
            var silverMat = FindModelMaterial("Silver", "bar") ?? FindLoadedMaterial("silverbar");
            var crystalItem = PrefabManager.Instance.GetPrefab("Crystal");
            var crystalSrc = crystalItem ? crystalItem.transform.Find("Cube") : null;
            if (!silverMat || !crystalSrc)
            {
                Jotunn.Logger.LogWarning(
                    $"FrostWand: brak czesci modelu (silverbar={(bool)silverMat}, Crystal/Cube={(bool)crystalSrc}) " +
                    "- zostaje model kostura ognia");
                return;
            }

            // Wizuale ida bezposrednio pod "attach" (to jego zawartosc VisEquipment wpina do reki
            // i to on renderuje sie na ziemi). W StaffFireball attach ma juz rotacje 180 wokol Z;
            // czyscimy go calego (model kostura "default (1)" + nieaktywny "equiped").
            var holder = prefab.transform.Find("attach");
            if (!holder)
            {
                Jotunn.Logger.LogWarning("FrostWand: brak wezla 'attach' w klonie StaffFireball");
                return;
            }
            foreach (var child in holder.Cast<Transform>().ToArray())
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            holder.localRotation = Quaternion.Euler(0f, 0f, 180f);

            var itemLayer = LayerMask.NameToLayer("item");

            // Trzonek generowany proceduralnie (os rozdzki = lokalne +Y siatki; obrot 90 wokol X
            // klade ja wzdluz +Z attach - czubek w strone +Z, jak w SurtlingWand).
            var wand = new GameObject("wand");
            // auto-pickup gracza skanuje collidery wylacznie na warstwie "item"
            wand.layer = itemLayer;
            wand.transform.SetParent(holder, false);
            wand.transform.localPosition = Vector3.zero;
            wand.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            wand.AddComponent<MeshFilter>().sharedMesh = BuildShaftMesh();
            wand.AddComponent<MeshRenderer>().sharedMaterial = BuildShaftMaterial(silverMat);
            // collidery sa niezbedne: bez nich item przelatuje przez podloge i nie da sie go podniesc
            var wandCollider = wand.AddComponent<BoxCollider>();
            wandCollider.size = new Vector3(0.14f, TipEnd + 0.02f, 0.14f);
            wandCollider.center = new Vector3(0f, TipEnd * 0.5f, 0f);

            // Glowica: pojedynczy krysztal - dziecko "Cube" itemu Crystal (material crystal_exterior).
            // UWAGA: mimo nazwy to nie kostka jednostkowa - siatka ma ~7 jednostek dlugosci i nie jest
            // wycentrowana, dlatego skalujemy po realnych wymiarach do CrystalLength i centrujemy po bounds.
            var crystal = UnityEngine.Object.Instantiate(crystalSrc.gameObject, holder, false);
            crystal.name = "crystal";
            crystal.layer = itemLayer;
            var crystalCollider = crystal.GetComponent<Collider>();
            if (crystalCollider) UnityEngine.Object.DestroyImmediate(crystalCollider);
            var crystalFilter = crystal.GetComponent<MeshFilter>();
            var crystalMesh = crystalFilter ? crystalFilter.sharedMesh : null;
            var bounds = crystalMesh ? crystalMesh.bounds : new Bounds(Vector3.zero, Vector3.one);
            var extents = Vector3.Scale(bounds.size, crystalSrc.localScale);
            var longAxis = extents.x >= extents.y && extents.x >= extents.z ? 0 : extents.y >= extents.z ? 1 : 2;
            var longest = Mathf.Max(extents.x, extents.y, extents.z);
            var scale = crystalSrc.localScale * (CrystalLength / Mathf.Max(longest, 0.001f));
            // grubosc: skalujemy tylko dwie osie poprzeczne wzgledem dlugiej
            if (longAxis != 0) scale.x *= CrystalWidth;
            if (longAxis != 1) scale.y *= CrystalWidth;
            if (longAxis != 2) scale.z *= CrystalWidth;
            // dluga os siatki -> os rozdzki (+Z attach), plus lekki skret wokol osi
            var align = longAxis == 0 ? Quaternion.Euler(0f, -90f, 0f)
                : longAxis == 1 ? Quaternion.Euler(90f, 0f, 0f)
                : Quaternion.identity;
            var rotation = Quaternion.Euler(0f, 0f, 35f) * align;
            crystal.transform.localScale = scale;
            crystal.transform.localRotation = rotation;
            crystal.transform.localPosition = new Vector3(0f, 0f, CageCenter) - rotation * Vector3.Scale(bounds.center, scale);
            Jotunn.Logger.LogDebug(
                $"FrostWand: krysztal mesh={(crystalMesh ? crystalMesh.name : "null")} bounds.size={bounds.size} " +
                $"center={bounds.center} srcScale={crystalSrc.localScale} -> wymiary vanilla={extents}, dluga os={longAxis}, skala={scale}");

            // niebieska poswiata krysztalu - wartosci swiatla z itemu Crystal (0.549,0.914,1 / 1 / 3)
            var glow = new GameObject("Point light");
            glow.transform.SetParent(holder, false);
            glow.transform.localPosition = new Vector3(0f, 0f, CageCenter);
            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.549f, 0.914f, 1f);
            light.intensity = 1.5f;
            light.range = 2.5f;
            light.shadows = LightShadows.None;

            // poswiata i sniezynki z efektow kostura lodu
            var iceStaff = PrefabManager.Instance.GetPrefab("StaffIceShards");
            var effects = new GameObject("effects");
            effects.layer = itemLayer;
            effects.transform.SetParent(holder, false);
            effects.transform.localPosition = new Vector3(0f, 0f, CageCenter);
            CloneFx(iceStaff, "flare", effects.transform);
            CloneFx(iceStaff, "embers", effects.transform);
        }

        // Wymiary trzonka (metry, wzdluz osi Y siatki): uchwyt -> klatka ze spiral -> szpic
        private const float HandleEnd = 0.30f;
        private const float CageEnd = 0.60f;
        private const float TipEnd = 0.70f;
        private const float CageCenter = (HandleEnd + CageEnd) * 0.5f;
        private const float CageRadius = 0.055f;
        // Wymiary krysztalu: dlugosc wzdluz rozdzki (metry) i mnoznik grubosci
        // (1 = proporcje oryginalnego krysztalu, 0.8 = smuklejszy, 1.2 = grubszy)
        private const float CrystalLength = 0.275f;
        private const float CrystalWidth = 1f;

        /// <summary>
        /// Siatka trzonka: prosty uchwyt (lekko zwezany, zamkniety od dolu), dwie spirale
        /// (podwojna helisa, 1.5 obrotu, rozchylone w srodku i schodzace do osi na koncach,
        /// zeby zlaly sie z uchwytem i szpicem) oraz stozkowy szpic. Wszystko to rurki
        /// zbudowane przez przesuwanie wieloboku po krzywej (ramka transportowana rownolegle).
        /// Low-poly: normalne sa radialne (gladkie), wiec nawet 4-boczna rurka cieniuje sie
        /// jak okragla.
        /// </summary>
        private static Mesh BuildShaftMesh()
        {
            // Budzet: <= 500 trojkatow. Rurka = segmenty * boki * 2 trojkatow.
            var mb = new TubeBuilder();

            // uchwyt (szesciokatny): denko-stozek 0 -> 0.018 (12 tris) + walec 0.018 -> 0.013 (12 tris)
            mb.AddTube(
                t => new Vector3(0f, Mathf.Lerp(-0.012f, 0f, t), 0f),
                t => Mathf.Lerp(0f, 0.018f, t), 1, 6);
            mb.AddTube(
                t => new Vector3(0f, Mathf.Lerp(0f, HandleEnd, t), 0f),
                t => Mathf.Lerp(0.018f, 0.013f, t), 1, 6);

            // pierscien u nasady klatki: 8 segmentow x 3 boki = 48 tris
            mb.AddTube(
                t => new Vector3(Mathf.Cos(t * 2f * Mathf.PI) * 0.014f, HandleEnd, Mathf.Sin(t * 2f * Mathf.PI) * 0.014f),
                t => 0.009f, 8, 3);

            // podwojna helisa: 24 segmenty (16 na obrot) x 4 boki = 192 tris na nitke
            for (var strand = 0; strand < 2; strand++)
            {
                var phase = strand * Mathf.PI;
                mb.AddTube(
                    t =>
                    {
                        var angle = phase + t * 2f * Mathf.PI * 1.5f;
                        var radial = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI)), 0.75f) * CageRadius + 0.006f;
                        return new Vector3(Mathf.Cos(angle) * radial, Mathf.Lerp(HandleEnd, CageEnd, t), Mathf.Sin(angle) * radial);
                    },
                    t => 0.0105f, 24, 4);
            }

            // szpic: szesciokatny stozek 0.013 -> 0 (12 tris)
            mb.AddTube(
                t => new Vector3(0f, Mathf.Lerp(CageEnd - 0.01f, TipEnd, t), 0f),
                t => Mathf.Lerp(0.013f, 0f, t), 1, 6);

            // razem: 24 + 48 + 384 + 12 = 468 trojkatow
            return mb.ToMesh("frostwand_shaft");
        }

        /// <summary>
        /// "silverbar" to Standard bez albedo z Metallic=1 i Glossiness=0.82 - czysty metal, ktory
        /// odbija wylacznie otoczenie i na cienkim, zaokraglonym trzonku wychodzi niemal czarny.
        /// Bulawa srebrna (SilverHammer_mat) jest jasna, bo ma Metallic=0 i jasny albedo. Robimy
        /// wlasny wariant w tym duchu: jasnoszary, lekko chlodny kolor bazowy i umiarkowany metal.
        /// </summary>
        private static Material BuildShaftMaterial(Material source)
        {
            var mat = new Material(source) { name = source.name + "_frostwand" };
            // 0.86/0.9/0.96 przy Metallic 0.35 bylo za jasne (swiecilo jak plastik) - ciemniejszy
            // szary i wiecej metalu, zeby zostal polysk, ale bez rozswietlonego diffuse'u
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", new Color(0.5f, 0.53f, 0.58f));
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.55f);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.72f);
            return mat;
        }

        private static Material FindModelMaterial(string prefabName, string meshName)
        {
            var go = PrefabManager.Instance.GetPrefab(prefabName);
            if (!go)
            {
                Jotunn.Logger.LogWarning($"FrostWand: nie znaleziono prefabu '{prefabName}'");
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

        private static void CloneFx(GameObject source, string childName, Transform parent)
        {
            var src = source
                ? source.GetComponentsInChildren<Transform>(true).FirstOrDefault(
                    t => t.name == childName || t.name.StartsWith(childName + " "))
                : null;
            if (!src)
            {
                Jotunn.Logger.LogWarning($"FrostWand: nie znaleziono fx '{childName}' w {(source ? source.name : "null")}");
                return;
            }
            var clone = UnityEngine.Object.Instantiate(src.gameObject, parent, false);
            clone.name = childName;
            clone.layer = parent.gameObject.layer;
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
        }

        private static Material FindLoadedMaterial(string name)
        {
            return Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m.name == name);
        }
    }
}
