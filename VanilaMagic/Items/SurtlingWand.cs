using System;
using System.Linq;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace VanilaMagic.Items
{
    /// <summary>
    /// SurtlingWand skladany w calosci w kodzie z waniliowych czesci (kitbash):
    /// - baza (animacja staff_rapidfire, atak, dzwieki): StaffIceShards
    /// - pocisk: klon staff_fireball_projectile (do pozniejszego przemalowania)
    /// - rekojesc: mesh "stand" + material "corestand" (tint na braz) z Pickable_SurtlingCoreStand
    /// - core: mesh i material z itemu SurtlingCore
    /// - efekty flare/embers: z itemu StaffFireball
    /// Nic nie jest shipowane w bundlu - wszystkie assety pochodza z zaladowanej gry.
    /// </summary>
    internal static class SurtlingWand
    {
        public const string PrefabName = "SurtlingWand";
        private const string ProjectileName = "wand_fireball_projectile";
        private const string ExplosionName = "wand_fireball_explosion";

        // Wartosci przeniesione 1:1 z recznego prefabu z bundla (wand.mat + transformy dzieci)
        private static readonly Color BronzeTint = new Color(1f, 0.8994f, 0.5802f);

        // ~27 DPS na q1 / ~33 na q4 (20 obuchu + 20 ognia co 1.5 s)
        private const float BluntDamage = 20f;
        private const float FireDamage = 20f;
        private const float FireDamagePerLevel = 3f;
        private const float BurstInterval = 1.5f;
        private const float AttackEitr = 7f;
        private const float LaunchAngleUp = 3f;

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
                BuildWand(PrefabName, "$item_wandfireball", "$item_wandfireball_description", projectile);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"SurtlingWand: skladanie itemu nie powiodlo sie: {ex}");
            }
        }

        /// <summary>
        /// Klon staff_fireball_projectile przestylowany 1:1 na wzor recznego prefabu z bundla
        /// (wartosci z diffu wand_fireball_projectile vs vanilla): mniejsza kula, mniejszy
        /// i mniej dymny wybuch, zelazna kula z czerwona emisja zamiast fioletowej.
        /// </summary>
        private static GameObject BuildProjectile()
        {
            var explosion = PrefabManager.Instance.CreateClonedPrefab(ExplosionName, "fx_fireball_staff_explosion");
            RestyleExplosion(explosion);
            PrefabManager.Instance.AddPrefab(new CustomPrefab(explosion, false));

            var projectile = PrefabManager.Instance.CreateClonedPrefab(ProjectileName, "staff_fireball_projectile");
            RestyleProjectile(projectile, explosion);
            PrefabManager.Instance.AddPrefab(new CustomPrefab(projectile, false));
            return projectile;
        }

        private static void RestyleProjectile(GameObject projectile, GameObject explosion)
        {
            projectile.transform.localScale = Vector3.one * 0.5f;
            SetChildScale(projectile, "flames", 0.4f);
            SetChildScale(projectile, "flames_world", 0.4f);

            var proj = projectile.GetComponent<Projectile>();
            proj.m_aoe = 1f;
            proj.m_hitNoise = 30f;
            ReplaceEffect(proj.m_hitEffects, "fx_fireball_staff_explosion", explosion);
            if (proj.m_spawnOnHit && proj.m_spawnOnHit.name == "fx_fireball_staff_explosion")
            {
                proj.m_spawnOnHit = explosion;
            }

            var lightChild = projectile.transform.Find("Point light");
            if (lightChild) lightChild.GetComponent<Light>().range = 2f;

            var sphere = projectile.transform.Find("Sphere");
            if (sphere)
            {
                var renderer = sphere.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = BuildFireballMaterial(renderer.sharedMaterial);
            }
            else Jotunn.Logger.LogWarning("SurtlingWand: brak dziecka 'Sphere' w klonie pocisku");
        }

        private static void RestyleExplosion(GameObject explosion)
        {
            explosion.transform.localScale = Vector3.one * 0.5f;
            SetChildScale(explosion, "Chunks", 0.35f);
            SetChildScale(explosion, "Distortion Shockwave", 0.5f);
            SetChildScale(explosion, "Point light", 0.5f);
            SetChildScale(explosion, "Sparks", 0.5f);
            SetChildScale(explosion, "fire", 0.3f);
            SetChildScale(explosion, "shockwave", 0.3f);
            SetChildScale(explosion, "smoke", 0.5f);

            // przyciete moduly shape emiterow (root: radius 0.49 -> 0.19)
            var rootShape = explosion.GetComponent<ParticleSystem>().shape;
            rootShape.radius = 0.19f;
            rootShape.scale = new Vector3(0.5f, 1f, 1f);

            var smoke = explosion.transform.Find("smoke");
            if (smoke)
            {
                var smokeShape = smoke.GetComponent<ParticleSystem>().shape;
                smokeShape.scale = new Vector3(0.5f, 0.5f, 0.5f);
            }
        }

        /// <summary>
        /// Odtworzony wand_fire.mat: Standard shader, waniliowe tekstury (iron/iron_n/flint)
        /// i czerwona emisja HDR - to ona robi kolor kuli.
        /// </summary>
        private static Material BuildFireballMaterial(Material baseMat)
        {
            // Shader.Find("Standard") w player buildzie zwraca null (shader nie jest
            // "always included") - bierzemy zywa instancje z materialu, ktory podmieniamy
            // (vanilla shaman_prupleball uzywa Standard), z globalnym fallbackiem.
            var shader = baseMat && baseMat.shader && baseMat.shader.name == "Standard"
                ? baseMat.shader
                : Resources.FindObjectsOfTypeAll<Shader>().FirstOrDefault(s => s.name == "Standard");
            if (!shader)
            {
                Jotunn.Logger.LogWarning("SurtlingWand: brak shadera Standard - kula zostaje z materialem vanilla");
                return baseMat;
            }

            var mat = new Material(shader) { name = "wand_fire" };
            mat.SetTexture("_MainTex", FindLoadedTexture("iron"));
            mat.SetTexture("_BumpMap", FindLoadedTexture("iron_n"));
            mat.SetTexture("_EmissionMap", FindLoadedTexture("flint"));
            mat.EnableKeyword("_NORMALMAP");
            mat.EnableKeyword("_EMISSION");
            mat.SetFloat("_BumpScale", 2.88f);
            mat.SetFloat("_Metallic", 1f);
            mat.SetFloat("_Glossiness", 0.812f);
            mat.color = new Color(1f, 0.897f, 0.897f);
            mat.SetColor("_EmissionColor", new Color(2.832f, 0.1895f, 0f, 1f));
            return mat;
        }

        private static void ReplaceEffect(EffectList list, string prefabName, GameObject replacement)
        {
            if (list?.m_effectPrefabs == null) return;
            var found = false;
            foreach (var effect in list.m_effectPrefabs)
            {
                if (effect.m_prefab && effect.m_prefab.name == prefabName)
                {
                    effect.m_prefab = replacement;
                    found = true;
                }
            }
            if (!found)
            {
                Jotunn.Logger.LogWarning($"SurtlingWand: nie znaleziono efektu '{prefabName}' do podmiany");
            }
        }

        private static void SetChildScale(GameObject root, string childName, float scale)
        {
            var child = root.transform.Find(childName);
            if (child) child.localScale = Vector3.one * scale;
            else Jotunn.Logger.LogWarning($"SurtlingWand: brak dziecka '{childName}' w {root.name}");
        }

        private static Texture2D FindLoadedTexture(string name)
        {
            var tex = Resources.FindObjectsOfTypeAll<Texture2D>().FirstOrDefault(t => t.name == name);
            if (!tex) Jotunn.Logger.LogWarning($"SurtlingWand: nie znaleziono tekstury '{name}'");
            return tex;
        }

        private static void BuildWand(string prefabName, string name, string description, GameObject projectile)
        {
            var fireStaffShared = PrefabManager.Instance.GetPrefab("StaffFireball")
                .GetComponent<ItemDrop>().m_itemData.m_shared;

            var config = new ItemConfig
            {
                Name = name,
                Description = description,
                Icons = new[] { fireStaffShared.m_icons[0] },
                CraftingStation = "forge",
                RepairStation = "forge",
                // jak waniliowa bron z brazu: kuznia 1, q4 przy kuzni 4 (era zelaza)
                MinStationLevel = 1,
                Requirements = new[]
                {
                    new RequirementConfig("Bronze", 7, 2),
                    new RequirementConfig("SurtlingCore", 1, 1),
                },
            };

            var item = new CustomItem(prefabName, "StaffIceShards", config);
            ItemManager.Instance.AddItem(item);

            var shared = item.ItemDrop.m_itemData.m_shared;
            // wytrzymalosc wspolna dla wszystkich rozdzek: 100 +25 na poziom, -1 za kazdy strzal
            shared.m_maxDurability = 100f;
            shared.m_durabilityPerLevel = 25f;
            shared.m_useDurability = true;
            shared.m_useDurabilityDrain = 1f;
            BurstDurability.Register(shared.m_name);
            // Stan animacji ZOSTAJE "Staves": seria "staff_rapidfire" w warstwie bazowej animatora
            // wchodzi tylko ze stanu ruchu kostura; z OneHanded InAttack() jest false i strzaly
            // leca w losowe strony. Jednoreczny blok robi OneHandedBlock (statei tylko na czas bloku).
            OneHandedBlock.Register(shared.m_name);
            // balans: jeden strzal ~ Crude bow + ognista strzala (20 obuchu, 20 ognia),
            // tu przebicie -> obuch (zelazna kula); z poziomem rosnie tylko ogien, jak w Staff of Embers
            shared.m_damages.m_frost = 0f;
            shared.m_damages.m_blunt = BluntDamage;
            shared.m_damages.m_fire = FireDamage;
            shared.m_damagesPerLevel.m_frost = 0f;
            shared.m_damagesPerLevel.m_fire = FireDamagePerLevel;
            shared.m_attack.m_attackProjectile = projectile;
            // StaffIceShards ma m_hitVariant = -1: przy ujemnym wariancie StatusEffect.TriggerStartEffects
            // odpala WSZYSTKIE warianty vfx_Burning naraz (pomaranczowy + niebieski + zielony = bialy
            // przeswietlony blysk). Ognista strzala i StaffFireball maja 0 = zwykly pomaranczowy ogien.
            shared.m_hitVariant = 0;

            // Tempo strzalow to dana ataku, nie animacja: FireProjectileBurst odpala sie
            // co m_burstInterval (baza 0.2 s). Eitr idzie per strzal (m_perBurstResourceUsage).
            var attack = shared.m_attack;
            Jotunn.Logger.LogDebug(
                $"{prefabName}: bazowy atak StaffIceShards - bursts={attack.m_projectileBursts}, " +
                $"interval={attack.m_burstInterval}s, pociski/burst={attack.m_projectiles}, " +
                $"eitr={attack.m_attackEitr}, rozrzut={attack.m_projectileAccuracy}, " +
                $"wysokosc={attack.m_attackHeight}, launchAngle={attack.m_launchAngle}");
            attack.m_burstInterval = BurstInterval;
            attack.m_attackEitr = AttackEitr;

            // punkt startu pocisku = pozycja postaci + up * m_attackHeight (+ forward/right);
            // ~1/4 wzrostu postaci nizej, zeby kula wylatywala z rozdzki, a nie znad glowy
            attack.m_attackHeight -= 0.45f;

            // kula z StaffFireball ma grawitacje, a atak StaffIceShards nie kompensuje opadania
            // (pocisk lodu leci prosto) - strzaly ladowaly wyraznie ponizej celownika.
            // Staff of Embers ma na to launchAngle -5 (ujemny = wyzej); dajemy 3 stopnie w gore (6 przestrzeliwalo).
            attack.m_launchAngle -= LaunchAngleUp;

            // celnosc: nizsza wartosc = mniejszy rozrzut (frost staff celowo strzela na oslep);
            // Min obowiazuje przy skillu 0, docelowa przy wysokim skillu
            attack.m_projectileAccuracyMin = 4f;
            attack.m_projectileAccuracy = 1f;

            // dzwiek strzalu: w rapidfire gra m_burstEffect (per strzal) - bazowo lodowy sfx;
            // m_startEffect wyciszamy, zeby nie zostal lodowy "whoosh" na starcie ataku
            var launchSfx = PrefabManager.Instance.GetPrefab("sfx_firestaff_launch");
            if (launchSfx)
            {
                attack.m_burstEffect = new EffectList
                {
                    m_effectPrefabs = new[] { new EffectList.EffectData { m_prefab = launchSfx } }
                };
            }
            attack.m_startEffect = new EffectList();

            BuildVisual(item.ItemPrefab);
            // Render ikony DOPIERO po zlozeniu modelu - inaczej lapie golego klona bazy
            RenderedIcons.Register(prefabName);
        }

        private static void BuildVisual(GameObject prefab)
        {
            var (standMesh, corestandMat) = FindModel("Pickable_SurtlingCoreStand", "stand");
            standMesh = standMesh ? standMesh : FindLoadedMesh("stand");
            corestandMat = corestandMat ? corestandMat : FindLoadedMaterial("corestand");

            var (coreMesh, coreMat) = FindModel("SurtlingCore", "core");
            coreMesh = coreMesh ? coreMesh : FindLoadedMesh("core");
            coreMat = coreMat ? coreMat : FindLoadedMaterial("surtlingcore");

            if (!standMesh || !corestandMat || !coreMesh || !coreMat)
            {
                Jotunn.Logger.LogWarning(
                    $"SurtlingWand: brak czesci modelu (stand={(bool)standMesh}, corestand={(bool)corestandMat}, " +
                    $"core={(bool)coreMesh}, surtlingcore={(bool)coreMat}) - zostaje model kostura lodu");
                return;
            }

            // kopia materialu - nigdy nie mutujemy wspoldzielonego waniliowego assetu
            var bronzeMat = new Material(corestandMat) { name = "wand_bronze" };
            if (bronzeMat.HasProperty("_Color")) bronzeMat.color = BronzeTint;
            if (bronzeMat.HasProperty("_Metallic")) bronzeMat.SetFloat("_Metallic", 1f);
            if (bronzeMat.HasProperty("_Glossiness")) bronzeMat.SetFloat("_Glossiness", 0.721f);

            var fireStaff = PrefabManager.Instance.GetPrefab("StaffFireball");

            // Wizuale ida bezposrednio pod "attach" - to jego zawartosc VisEquipment wpina
            // do reki i to on renderuje sie na ziemi. Wezla "equiped" (podrzedny attach,
            // przelaczany przez VisEquipment.EnableEquippedEffects) nie tworzymy - w recznym
            // prefabie byl pusty i wylaczony.
            var holder = prefab.transform.Find("attach");
            if (!holder)
            {
                Jotunn.Logger.LogWarning("SurtlingWand: brak wezla 'attach' w klonie StaffIceShards");
                return;
            }

            foreach (var child in holder.Cast<Transform>().ToArray())
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            holder.localRotation = Quaternion.Euler(0f, 0f, 180f);

            var wand = new GameObject("wand");
            // auto-pickup gracza skanuje collidery wylacznie na warstwie "item"
            wand.layer = LayerMask.NameToLayer("item");
            wand.transform.SetParent(holder, false);
            wand.transform.localPosition = Vector3.zero;
            wand.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            wand.transform.localScale = new Vector3(1f, 0.6f, 1f);
            wand.AddComponent<MeshFilter>().sharedMesh = standMesh;
            wand.AddComponent<MeshRenderer>().sharedMaterial = bronzeMat;
            // collidery sa niezbedne: bez nich item przelatuje przez podloge
            // i nie da sie go podniesc (wartosci z recznego prefabu)
            var wandCollider = wand.AddComponent<BoxCollider>();
            wandCollider.size = new Vector3(0.179f, 0.9067f, 0.1804f);
            wandCollider.center = new Vector3(0f, 0.4221f, 0f);

            var effects = new GameObject("effects");
            effects.layer = LayerMask.NameToLayer("item");
            effects.transform.SetParent(wand.transform, false);
            effects.transform.localPosition = new Vector3(0f, 0.82f, 0f);
            CloneFx(fireStaff, "flare", effects.transform);
            CloneFx(fireStaff, "embers", effects.transform);

            var core = new GameObject("core");
            core.transform.SetParent(holder, false);
            core.transform.localPosition = new Vector3(0f, 0f, 0.5256f);
            core.transform.localRotation = Quaternion.Euler(-35.341f, 0f, -135f);
            core.transform.localScale = Vector3.one * 0.055f;
            core.AddComponent<MeshFilter>().sharedMesh = coreMesh;
            core.AddComponent<MeshRenderer>().sharedMaterial = coreMat;
            var coreCollider = core.AddComponent<BoxCollider>();
            coreCollider.size = new Vector3(2.5763f, 2.6028f, 2.5501f);
            coreCollider.center = new Vector3(0f, 0f, -0.0264f);

            // poswiata rdzenia - wartosci z recznego prefabu
            var glow = new GameObject("Point light");
            glow.transform.SetParent(core.transform, false);
            var light = glow.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(0.9779f, 0.3015f, 0.1870f);
            light.intensity = 2f;
            light.range = 2.54f;
            light.shadows = LightShadows.None;
        }

        private static (Mesh mesh, Material mat) FindModel(string prefabName, string meshName)
        {
            var go = PrefabManager.Instance.GetPrefab(prefabName);
            if (!go)
            {
                Jotunn.Logger.LogWarning($"SurtlingWand: nie znaleziono prefabu '{prefabName}'");
                return (null, null);
            }
            foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh && filter.sharedMesh.name == meshName)
                {
                    var renderer = filter.GetComponent<MeshRenderer>();
                    return (filter.sharedMesh, renderer ? renderer.sharedMaterial : null);
                }
            }
            return (null, null);
        }

        private static void CloneFx(GameObject source, string childName, Transform parent)
        {
            var src = source.GetComponentsInChildren<Transform>(true).FirstOrDefault(
                t => t.name == childName || t.name.StartsWith(childName + " "));
            if (!src)
            {
                Jotunn.Logger.LogWarning($"SurtlingWand: nie znaleziono fx '{childName}' w {source.name}");
                return;
            }
            var clone = UnityEngine.Object.Instantiate(src.gameObject, parent, false);
            clone.name = childName;
            clone.transform.localPosition = Vector3.zero;
            clone.transform.localRotation = Quaternion.identity;
        }

        private static Mesh FindLoadedMesh(string name)
        {
            return Resources.FindObjectsOfTypeAll<Mesh>().FirstOrDefault(m => m.name == name);
        }

        private static Material FindLoadedMaterial(string name)
        {
            return Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m.name == name);
        }
    }
}
