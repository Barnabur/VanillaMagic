using System;
using System.Linq;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using VanilaMagic.StatusEffects;
using UnityEngine;

namespace VanilaMagic.Items
{
    /// <summary>
    /// HealingStaff skladany w calosci w kodzie z waniliowych czesci (kitbash):
    /// - baza (animacja staff_shield, atak, dzwieki): StaffShield
    /// - AoE leczenia: klon DvergerStaffHeal_aoe (reczny heal_aoe byl jego kopia)
    ///   z podpietym wlasnym Heal_SE
    /// - rekojesc: kosc (mesh "Bone"; w recepcie WitheredBone z bagna), glowica: AncientSeed,
    ///   zielone plomienie: nieaktywne dziecko "flames" z DvergerStaffHeal_aoe
    /// Wartosci transformow i Aoe przeniesione 1:1 z recznych prefabow z bundla.
    /// </summary>
    internal static class HealingStaff
    {
        public const string PrefabName = "HealingStaff";
        private const string AoeName = "heal_aoe";
        private const string HealSEName = "HealStatusEffect";

        // material fireworks z klonu heal_aoe - pewne zrodlo DZIALAJACEGO shadera
        // Particles/Standard Unlit2 (bundle laduje zepsuty rip o tej samej nazwie:
        // "Failed to find expected binary shader data"); renderer plomieni trzymamy
        // statycznie do ewentualnego dokonczenia materialu po wejsciu do swiata
        private static Material _fireworksMat;
        private static ParticleSystemRenderer _flamesRenderer;

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
                var healSE = CreateStatusEffect();
                ItemManager.Instance.AddStatusEffect(healSE);

                var healAoe = BuildHealAoe();

                var seedShared = PrefabManager.Instance.GetPrefab("AncientSeed")
                    .GetComponent<ItemDrop>().m_itemData.m_shared;

                var config = new ItemConfig
                {
                    Name = "$item_staffheal",
                    Description = "$item_staffheal_description",
                    Icons = new[] { seedShared.m_icons[0] },
                    CraftingStation = "forge",
                    RepairStation = "forge",
                    // jak waniliowa bron z zelaza: kuznia 2, q4 przy kuzni 5
                    MinStationLevel = 2,
                    Requirements = new[]
                    {
                        new RequirementConfig("WitheredBone", 10, 5),
                        new RequirementConfig("AncientSeed", 2, 1),
                        new RequirementConfig("Ectoplasm", 10, 5),
                    },
                };

                var item = new CustomItem(PrefabName, "StaffShield", config);
                ItemManager.Instance.AddItem(item);

                var shared = item.ItemDrop.m_itemData.m_shared;
                // wytrzymalosc wspolna dla wszystkich rozdzek: 100 +25 na poziom, -1 za kazdy strzal
                shared.m_maxDurability = 100f;
                shared.m_durabilityPerLevel = 25f;
                shared.m_useDurability = true;
                shared.m_useDurabilityDrain = 1f;
                shared.m_attack.m_attackProjectile = healAoe;
                shared.m_attack.m_attackEitr = 20f;
                // heal_aoe ma m_useAttackSettings, wiec Aoe.Setup nadpisuje jego SE hashem
                // z broni - bez tego przypisania nie-gracze (tamy) nie dostana leczenia;
                // przy okazji item pokazuje tooltip efektu (GetStatusEffectTooltip)
                shared.m_attackStatusEffect = healSE.StatusEffect;

                // Efekty 1:1 z recznego prefabu: puste startEffect/triggerEffect wycinaja
                // waniliowa kule castu kostura tarczy; zostaje dzwiek rzutu przy wystrzale
                // i iskry przy trafieniu. Zielona poswiata pochodzi z samego heal_aoe.
                var attack = shared.m_attack;
                attack.m_startEffect = new EffectList();
                attack.m_triggerEffect = new EffectList();
                attack.m_trailStartEffect = new EffectList();
                attack.m_burstEffect = new EffectList();
                attack.m_hitTerrainEffect = new EffectList();
                attack.m_hitEffect = MakeEffectList("vfx_HitSparks");
                shared.m_startEffect = new EffectList();
                shared.m_triggerEffect = new EffectList();
                shared.m_holdStartEffect = new EffectList();
                shared.m_equipEffect = new EffectList();
                shared.m_hitEffect = new EffectList();
                shared.m_hitTerrainEffect = new EffectList();
                shared.m_trailStartEffect = MakeEffectList("sfx_bomb_throw");

                BuildVisual(item.ItemPrefab);
                // Render ikony DOPIERO po zlozeniu modelu - inaczej lapie golego klona bazy
                RenderedIcons.Register(PrefabName);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"HealingStaff: skladanie itemu nie powiodlo sie: {ex}");
            }
        }

        private static CustomStatusEffect CreateStatusEffect()
        {
            var healEffect = ScriptableObject.CreateInstance<Heal_SE>();
            healEffect.name = HealSEName;
            healEffect.m_name = "$se_heal";
            healEffect.m_ttl = 10f;
            healEffect.m_tickInterval = 1f;
            // m_healthPerTick ustawia Heal_SE.SetLevel przy nalozeniu (quality + skill);
            // pola m_healthOverTime* pominiete - dzialaja tylko przy m_healthOverTime > 0

            // ikona: zielone serce (Assets/HealIcon.png - serce z waniliowego sprite'a "Healthy"
            // bez ramki, przemalowane offline; sprite'a nie laduje zaden prefab, wiec nie da sie
            // go wziac w runtime), awaryjnie waniliowa malina
            healEffect.m_icon = RuntimeTextures.LoadIcon("HealIcon");
            if (!healEffect.m_icon)
            {
                var raspberry = PrefabManager.Instance.GetPrefab("Raspberry");
                if (raspberry) healEffect.m_icon = raspberry.GetComponent<ItemDrop>().m_itemData.m_shared.m_icons.FirstOrDefault();
                else Jotunn.Logger.LogWarning("HealingStaff: brak zrodla ikony dla status effectu");
            }
            return new CustomStatusEffect(healEffect, false);
        }

        /// <summary>
        /// Klon DvergerStaffHeal_aoe z wartosciami z recznego heal_aoe. Roznice vs vanilla:
        /// SE -> HealStatusEffect, radius 5 (4.32), ttl 3.5 (7), hitEffects (iskry + sfx),
        /// ignorePVP, zielensze i krotsze swiatlo. m_hitEnemy zostaje vanilla=false
        /// (w recznym prefabie bylo true przez pomylke - leczyl wrogow).
        /// </summary>
        private static GameObject BuildHealAoe()
        {
            var aoeGo = PrefabManager.Instance.CreateClonedPrefab(AoeName, "DvergerStaffHeal_aoe");

            // vanilla ma 4 warianty plomieni - w recznym prefabie zostal tylko "flames_world"
            DestroyChild(aoeGo, "flames");
            DestroyChild(aoeGo, "flames_world (1)");
            DestroyChild(aoeGo, "flames_world (2)");

            var aoe = aoeGo.GetComponent<Aoe>();
            aoe.m_statusEffect = HealSEName;
            aoe.m_statusEffectIfPlayer = HealSEName;
            aoe.m_hitEnemy = false;
            aoe.m_hitProps = false;
            aoe.m_ignorePVP = true;
            aoe.m_radius = 5f;
            aoe.m_ttl = 3.5f;

            var hitSparks = PrefabManager.Instance.GetPrefab("vfx_HitSparks");
            var hitSfx = PrefabManager.Instance.GetPrefab("sfx_greydwarf_attack_hit");
            aoe.m_hitEffects = new EffectList
            {
                m_effectPrefabs = new[] { hitSparks, hitSfx }
                    .Where(p => p)
                    .Select(p => new EffectList.EffectData { m_prefab = p })
                    .ToArray()
            };

            var flamesWorld = aoeGo.transform.Find("flames_world");
            if (flamesWorld)
            {
                var fwRenderer = flamesWorld.GetComponent<ParticleSystemRenderer>();
                if (fwRenderer) _fireworksMat = fwRenderer.sharedMaterial;
            }

            var light = aoeGo.GetComponentInChildren<Light>(true);
            if (light) light.color = new Color(0.8797f, 1f, 0.4549f);
            var flicker = aoeGo.GetComponentInChildren<LightFlicker>(true);
            if (flicker)
            {
                flicker.m_ttl = 2.5f;
                flicker.m_fadeDuration = 1f;
                flicker.m_fadeInDuration = 0f;
            }

            PrefabManager.Instance.AddPrefab(new CustomPrefab(aoeGo, false));
            return aoeGo;
        }

        private static void BuildVisual(GameObject prefab)
        {
            var attach = prefab.transform.Find("attach");
            if (!attach)
            {
                Jotunn.Logger.LogWarning("HealingStaff: brak wezla 'attach' w klonie StaffShield");
                return;
            }
            attach.localPosition = new Vector3(0f, 0.025f, 0f);
            attach.localRotation = Quaternion.Euler(0f, 0f, 180f);

            // czyscimy CALY attach (model kostura tarczy jest rodzenstwem "equiped",
            // nie jego dzieckiem) i odtwarzamy wezel equiped jak w recznym prefabie
            foreach (var child in attach.Cast<Transform>().ToArray())
            {
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            }
            var equiped = new GameObject("equiped");
            equiped.transform.SetParent(attach, false);
            var holder = equiped.transform;

            var itemLayer = LayerMask.NameToLayer("item");

            // mesh "Bone"/material "bone" szukamy globalnie po nazwie - dokladnie tak
            // rozwiazywal je Jotunn dla JVLmocka w bundlu (BoneFragments ma inny mesh - sterte)
            var boneMesh = FindLoadedMesh("Bone");
            var boneMat = FindLoadedMaterial("bone");
            var (seedMesh, seedMat) = FindModel("AncientSeed", "ancientseed");
            if (!boneMesh || !boneMat || !seedMesh || !seedMat)
            {
                Jotunn.Logger.LogWarning(
                    $"HealingStaff: brak czesci modelu (bone={(bool)boneMesh}/{(bool)boneMat}, " +
                    $"seed={(bool)seedMesh}/{(bool)seedMat}) - zostaje model kostura tarczy");
                return;
            }

            var wand = new GameObject("Wand");
            wand.layer = itemLayer;
            wand.transform.SetParent(holder, false);
            wand.transform.localPosition = new Vector3(0.009f, 0f, 0.016f);
            wand.transform.localScale = new Vector3(0.6f, 0.6f, 0.8f);
            wand.AddComponent<MeshFilter>().sharedMesh = boneMesh;
            wand.AddComponent<MeshRenderer>().sharedMaterial = boneMat;
            wand.AddComponent<BoxCollider>().size = new Vector3(0.2f, 0.2f, 2f);

            var model = new GameObject("model");
            model.layer = itemLayer;
            model.transform.SetParent(holder, false);
            model.transform.localPosition = new Vector3(0.001f, 0.006f, 0.698f);
            model.transform.localRotation = Quaternion.Euler(-34.489f, -74.284f, 186.532f);
            model.transform.localScale = Vector3.one * 0.16f;
            model.AddComponent<MeshFilter>().sharedMesh = seedMesh;
            model.AddComponent<MeshRenderer>().sharedMaterial = seedMat;
            var modelCollider = model.AddComponent<BoxCollider>();
            modelCollider.size = new Vector3(1.73f, 1.61f, 1.65f);
            modelCollider.center = new Vector3(-0.02f, 0f, 0.149f);

            // zielone plomienie na glowicy: nieaktywny wariant "flames" z DvergerStaffHeal_aoe
            var dvergerAoe = PrefabManager.Instance.GetPrefab("DvergerStaffHeal_aoe");
            var flamesSrc = dvergerAoe ? dvergerAoe.transform.Find("flames") : null;
            if (flamesSrc)
            {
                var flames = UnityEngine.Object.Instantiate(flamesSrc.gameObject, model.transform, false);
                flames.name = "flames";
                flames.SetActive(true);
                // warstwe zostawiamy odziedziczona ze zrodla (22, jak efekty dvergrow) -
                // reczny prefab mial 10, ale ta warstwa nie renderuje sie w grze
                flames.transform.localPosition = Vector3.zero;
                flames.transform.localRotation = Quaternion.Euler(91.6782f, 0f, 180f);
                flames.transform.localScale = Vector3.one * 1.8f;
                RestyleFlames(flames);
            }
            else
            {
                Jotunn.Logger.LogWarning("HealingStaff: nie znaleziono 'flames' w DvergerStaffHeal_aoe");
            }

            // "Poswiata" jak na SurtlingWand: zielone swiatlo punktowe + miekki flare.
            // Reczny prefab ich nie mial - i wlasnie dlatego glowica nie swiecila w grze.
            var glow = new GameObject("Point light");
            glow.transform.SetParent(model.transform, false);
            var glowLight = glow.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = new Color(0.8797f, 1f, 0.4549f);
            glowLight.intensity = 2f;
            glowLight.range = 2.54f;
            glowLight.shadows = LightShadows.None;

            var fireStaff = PrefabManager.Instance.GetPrefab("StaffFireball");
            var flareSrc = fireStaff
                ? fireStaff.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "flare")
                : null;
            if (flareSrc)
            {
                var flare = UnityEngine.Object.Instantiate(flareSrc.gameObject, model.transform, false);
                flare.name = "flare";
                flare.transform.localPosition = Vector3.zero;
                flare.transform.localRotation = Quaternion.identity;
                flare.transform.localScale = Vector3.one * 0.45f;
                var flarePs = flare.GetComponent<ParticleSystem>();
                if (flarePs)
                {
                    var flareMain = flarePs.main;
                    flareMain.startColor = new Color(0.35f, 1f, 0.7f, 0.55f);
                }
            }
            else
            {
                Jotunn.Logger.LogWarning("HealingStaff: nie znaleziono 'flare' w StaffFireball");
            }
        }

        /// <summary>
        /// Dostrojenie sklonowanych plomieni do recznej wersji (diff PS vs vanilla):
        /// wolna zielona mgielka zamiast pikselowego ognia dvergrow.
        /// </summary>
        private static void RestyleFlames(GameObject flames)
        {
            var ps = flames.GetComponent<ParticleSystem>();
            if (!ps)
            {
                Jotunn.Logger.LogWarning("HealingStaff: klon flames bez ParticleSystem");
                return;
            }

            var main = ps.main;
            main.simulationSpeed = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 1.5f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.6f);
            // alpha podbita ~3x wzgledem recznego prefabu (0.12/0.07) - tamte wartosci
            // byly strojone w edytorze i w grze mgielka byla praktycznie niewidoczna
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0f, 0.43712f, 0.8f, 0.35f),
                new Color(0f, 0.62403f, 0.65882f, 0.22f));

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.angle = 0f;
            shape.radius = 0.0001f;

            var emission = ps.emission;
            emission.rateOverTime = 10f;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f,
                new AnimationCurve(new Keyframe(0f, 0.4615f, 1f, 1f), new Keyframe(1f, 1f, 1f, 1f)));

            // gradient zycia: odbarwiony na biel (kolor daje startColor + material),
            // czasy i kanal alfa zostaja z vanilla
            var colorOverLifetime = ps.colorOverLifetime;
            var gradient = colorOverLifetime.color.gradient;
            if (gradient != null)
            {
                var keys = gradient.colorKeys;
                for (var i = 0; i < keys.Length; i++) keys[i].color = Color.white;
                gradient.colorKeys = keys;
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
            }

            _flamesRenderer = flames.GetComponent<ParticleSystemRenderer>();
            ApplyFlamesMaterial();
        }

        /// <summary>
        /// Probuje zbudowac i podpiac material mgielki. W menu glownym tekstura waterfog
        /// bywa niezaladowana - wtedy zostawiamy material zrodlowy i ponawiamy przy
        /// kolejnym OnVanillaPrefabsAvailable (wejscie do swiata).
        /// </summary>
        private static void ApplyFlamesMaterial()
        {
            if (!_flamesRenderer)
            {
                PrefabManager.OnVanillaPrefabsAvailable -= ApplyFlamesMaterial;
                return;
            }
            var mat = BuildHealingFlameMaterial();
            if (mat)
            {
                _flamesRenderer.sharedMaterial = mat;
                PrefabManager.OnVanillaPrefabsAvailable -= ApplyFlamesMaterial;
            }
            else
            {
                Jotunn.Logger.LogDebug("HealingStaff: healing_flame niekompletny (menu?) - ponowie przy wejsciu do swiata");
                PrefabManager.OnVanillaPrefabsAvailable -= ApplyFlamesMaterial;
                PrefabManager.OnVanillaPrefabsAvailable += ApplyFlamesMaterial;
            }
        }

        /// <summary>
        /// Odtworzony healing_flame.mat z bundla: Particles/Standard Unlit2 + waterfog
        /// + zielony tint. To pelna podmiana materialu, nie przemalowanie pixel-artowego
        /// materialu dvergrow. Zwraca null, gdy brakuje jeszcze podstaw.
        /// </summary>
        private static Material BuildHealingFlameMaterial()
        {
            // w pamieci moga byc DWA shadery "Particles/Standard Unlit2": prawdziwy z gry
            // i martwy rip z bundla - bierzemy tylko taki, ktory ma skompilowane warianty
            var candidates = Resources.FindObjectsOfTypeAll<Shader>()
                .Where(s => s.name == "Particles/Standard Unlit2")
                .ToList();
            if (_fireworksMat && _fireworksMat.shader) candidates.Insert(0, _fireworksMat.shader);
            var shader = candidates.FirstOrDefault(s => s.isSupported);
            var waterfog = Resources.FindObjectsOfTypeAll<Texture2D>()
                .FirstOrDefault(t => t.name == "waterfog");
            Jotunn.Logger.LogDebug(
                "HealingStaff: shadery-kandydaci mgielki: " +
                string.Join("; ", candidates.Select(s => $"id={s.GetInstanceID()} supported={s.isSupported}")) +
                $", waterfog={(bool)waterfog}");

            if (!shader || !waterfog)
            {
                // awaryjnie: kopia dzialajacego materialu fireworks przemalowana na zielono
                if (_fireworksMat)
                {
                    Jotunn.Logger.LogWarning("HealingStaff: brak wspieranego shadera/tekstury - mgielka na bazie fireworks");
                    var fallback = new Material(_fireworksMat) { name = "healing_flame_fallback" };
                    if (fallback.HasProperty("_Color")) fallback.SetColor("_Color", new Color(0f, 1f, 0.8649f, 0.35f));
                    return fallback;
                }
                Jotunn.Logger.LogWarning(
                    $"HealingStaff: brak podstaw healing_flame (shader={(bool)shader}, waterfog={(bool)waterfog})");
                return null;
            }

            // wartosci 1:1 z recznego healing_flame.mat
            var mat = new Material(shader) { name = "healing_flame", renderQueue = 3000 };
            mat.SetTexture("_MainTex", waterfog);
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.EnableKeyword("_FADING_ON");
            mat.SetFloat("_Mode", 2f);
            mat.SetFloat("_SrcBlend", 5f);
            mat.SetFloat("_DstBlend", 10f);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_Cull", 2f);
            mat.SetFloat("_InvFade", 3f);
            mat.SetFloat("_SoftParticlesEnabled", 1f);
            mat.SetFloat("_SoftParticlesFarFadeDistance", 0.3f);
            mat.SetFloat("_SoftParticlesNearFadeDistance", 0f);
            mat.SetFloat("_CameraFadingEnabled", 0f);
            mat.SetFloat("_DistortionEnabled", 0f);
            mat.SetFloat("_EmissionEnabled", 0f);
            mat.SetFloat("_LightingEnabled", 0f);
            mat.SetFloat("_FlipbookMode", 0f);
            mat.SetFloat("_ColorMode", 0f);
            mat.SetFloat("_Cutoff", 0.2f);
            mat.SetColor("_Color", new Color(0f, 1f, 0.8649f, 1f));
            mat.SetColor("_TintColor", new Color(0.4338f, 0.4338f, 0.4338f, 1f));
            return mat;
        }

        private static Mesh FindLoadedMesh(string name)
        {
            var mesh = Resources.FindObjectsOfTypeAll<Mesh>().FirstOrDefault(m => m.name == name);
            if (!mesh) Jotunn.Logger.LogWarning($"HealingStaff: nie znaleziono mesha '{name}'");
            return mesh;
        }

        private static Material FindLoadedMaterial(string name)
        {
            var mat = Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m.name == name);
            if (!mat) Jotunn.Logger.LogWarning($"HealingStaff: nie znaleziono materialu '{name}'");
            return mat;
        }

        private static (Mesh mesh, Material mat) FindModel(string prefabName, string meshName)
        {
            var go = PrefabManager.Instance.GetPrefab(prefabName);
            if (!go)
            {
                Jotunn.Logger.LogWarning($"HealingStaff: nie znaleziono prefabu '{prefabName}'");
                return (null, null);
            }
            MeshFilter first = null;
            foreach (var filter in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh) continue;
                if (!first) first = filter;
                if (filter.sharedMesh.name == meshName)
                {
                    first = filter;
                    break;
                }
            }
            if (!first) return (null, null);
            var renderer = first.GetComponent<MeshRenderer>();
            return (first.sharedMesh, renderer ? renderer.sharedMaterial : null);
        }

        private static EffectList MakeEffectList(params string[] prefabNames)
        {
            var prefabs = prefabNames
                .Select(n =>
                {
                    var p = PrefabManager.Instance.GetPrefab(n);
                    if (!p) Jotunn.Logger.LogWarning($"HealingStaff: brak prefabu efektu '{n}'");
                    return p;
                })
                .Where(p => p)
                .Select(p => new EffectList.EffectData { m_prefab = p })
                .ToArray();
            return new EffectList { m_effectPrefabs = prefabs };
        }

        private static void DestroyChild(GameObject root, string childName)
        {
            var child = root.transform.Find(childName);
            if (child) UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
    }
}
