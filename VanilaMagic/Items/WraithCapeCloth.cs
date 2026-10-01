using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Parametry symulacji tkaniny peleryny (MagicaCloth 2 na cape2). Waniliowa peleryna trolla ma promien czastek
    /// 0.085 m przy kolizjach z kapsulami ciala, wiec kazdy punkt tkaniny trzyma sie ~8.5 cm od ciala, a przywracanie
    /// kata (sztywnosc ksztaltu) dokleja do tego skos - na Szatach Widma peleryna "lewitowala" za postacia
    /// (zrzut z gry: 7 cm od plecow na 1.28 m, 20 cm przy biodrach, 40-60 cm przy udach).
    /// MagicaClothV2.dll nie jest referencjonowany przez mod (JotunnLib go nie publikuje), wiec dostep refleksja.
    /// Parametry sa czescia serializeData (nie prebuildu), wiec zmiana nie psuje waniliowego MagicaPreBuild.
    /// </summary>
    internal static class WraithCapeCloth
    {
        // Promien czastek rosnie od kolnierza do rabka (krzywa po glebokosci): gora przy plecach, dol odstaje od nog.
        // Staly 0.03 przyklejal rabek do posladkow i owijal go na nogach.
        public static float Radius = 0.03f;          // przy kolnierzu (wanilia 0.085 na calej dlugosci, domyslna MagicaCloth 0.02)
        public static float RadiusBottom = 0.08f;    // na rabku
        public static float AngleStiffness = 0.05f;  // dobrane w grze (wanilia 0.15, krzywa 1 -> 0.1 od kolnierza w dol); <0 = bez zmian
        public static float Gravity = 7f;            // jak wanilia (dobrane w grze po przypieciu gory do kaptura); <0 = bez zmian
        // Limit odleglosci od pozy animacji (motionConstraint.maxDistance) tylko na GORNEJ polowie peleryny (glebokosc
        // 0 = kolnierz, 1 = rabek): od kolnierza 0 do MaxDistance w polowie, ponizej polowy FreeDistance = w praktyce bez limitu.
        // W biegu gora podskakiwala ponad kolnierz kaptura, a limit na calej dlugosci usztywnial dol. <0 = wylaczone (wanilia).
        public static float MaxDistance = -1f;       // WYLACZONE: gore trzyma przypiecie do kaptura (PinTopToHood); limit ciagnal dol za udami (kucanie -> peleryna do przodu)
        private const float FreeDistance = 1f;
        public static float LimitedDepth = 0.1f;     // punkt odciecia limitu (0 = kolnierz, 1 = rabek); dziala tylko przy maxdist >= 0
        public static float Damping = 0.2f;          // <0 = bez zmian (wanilia 0.1)

        private const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        private static Component[] FindCloths(GameObject root) =>
            root.GetComponentsInChildren<Component>(true).Where(c => c && c.GetType().FullName == "MagicaCloth2.MagicaCloth").ToArray();

        private static object Data(Component cloth) => cloth.GetType().GetProperty("SerializeData", Any)?.GetValue(cloth);

        private static float GetCurve(object owner, string field)
        {
            var curve = owner?.GetType().GetField(field, Any)?.GetValue(owner);
            return curve == null ? float.NaN : (float)curve.GetType().GetField("value", Any).GetValue(curve);
        }

        private static void SetCurve(object owner, string field, float value)
        {
            var curve = owner?.GetType().GetField(field, Any)?.GetValue(owner);
            curve?.GetType().GetField("value", Any).SetValue(curve, value);
        }

        private static string Apply(Component cloth)
        {
            var data = Data(cloth);
            if (data == null) return "brak SerializeData";
            var bottom = Mathf.Max(RadiusBottom, 0.001f);
            SetCurve(data, "radius", bottom);
            var radius = data.GetType().GetField("radius", Any)?.GetValue(data);
            radius?.GetType().GetField("useCurve", Any)?.SetValue(radius, true);
            radius?.GetType().GetField("curve", Any)?.SetValue(radius, AnimationCurve.Linear(0f, Mathf.Clamp01(Radius / bottom), 1f, 1f));
            var angle = data.GetType().GetField("angleRestorationConstraint", Any)?.GetValue(data);
            if (AngleStiffness >= 0f) SetCurve(angle, "stiffness", AngleStiffness);
            if (Gravity >= 0f) data.GetType().GetField("gravity", Any)?.SetValue(data, Gravity);
            if (Damping >= 0f) SetCurve(data, "damping", Damping);
            var motion = data.GetType().GetField("motionConstraint", Any)?.GetValue(data);
            if (motion != null)
            {
                motion.GetType().GetField("useMaxDistance", Any)?.SetValue(motion, MaxDistance >= 0f);
                if (MaxDistance >= 0f)
                {
                    SetCurve(motion, "maxDistance", FreeDistance);
                    var curve = motion.GetType().GetField("maxDistance", Any)?.GetValue(motion);
                    curve?.GetType().GetField("useCurve", Any)?.SetValue(curve, true);
                    // klucze: 0 przy kolnierzu -> MaxDistance w punkcie odciecia -> swobodnie 0.1 nizej; ostatni klucz (1,1)
                    // tylko gdy nie pokrywa sie z poprzednim (przy cutoff 0.9 wychodzily dwa klucze w t=1)
                    var cut = Mathf.Clamp(LimitedDepth, 0.01f, 0.9f);
                    var keys = new System.Collections.Generic.List<Keyframe> { new Keyframe(0f, 0f), new Keyframe(cut, MaxDistance / FreeDistance) };
                    var free = Mathf.Min(cut + 0.1f, 1f);
                    keys.Add(new Keyframe(free, 1f));
                    if (free < 0.999f) keys.Add(new Keyframe(1f, 1f));
                    curve?.GetType().GetField("curve", Any)?.SetValue(curve, new AnimationCurve(keys.ToArray()));
                }
            }
            return Describe(data);
        }

        private static string Describe(object data)
        {
            var angle = data.GetType().GetField("angleRestorationConstraint", Any)?.GetValue(data);
            var gravity = data.GetType().GetField("gravity", Any)?.GetValue(data);
            var motion = data.GetType().GetField("motionConstraint", Any)?.GetValue(data);
            var useMax = motion?.GetType().GetField("useMaxDistance", Any)?.GetValue(motion);
            var rc = data.GetType().GetField("radius", Any)?.GetValue(data);
            var top = rc?.GetType().GetField("curve", Any)?.GetValue(rc) is AnimationCurve c && c.length > 0 ? c.keys[0].value : 1f;
            return string.Format(CultureInfo.InvariantCulture, "radius={0:0.###}->" + (GetCurve(data, "radius") * top).ToString("0.###", CultureInfo.InvariantCulture) + "(gora) angle={1:0.###} gravity={2} damping={3:0.###} maxdist={4}",
                GetCurve(data, "radius"), GetCurve(angle, "stiffness"), gravity, GetCurve(data, "damping"),
                useMax is bool on && on ? MaxDistance.ToString("0.###", CultureInfo.InvariantCulture) + " do " + LimitedDepth.ToString("0.##", CultureInfo.InvariantCulture) : "off");
        }

        // Gora peleryny przypieta do kaptura (pomysl Kamila, 2026-09-27): wierzcholki do FixedRow (z 128, od kolnierza,
        // 45 = ok. 1.30 m) sa STALE (bez fizyki) i maja wagi kolnierza kaptura (WraithCape_mesh z bundla: cape2 z wagami
        // przepietymi w CapeLab.ReweightCapeToHood). Symulacja zaczyna sie ponizej, wiec w biegu gora nie wyskakuje nad kolnierz
        // (w labie sam skinning nie przebija - przebijala symulowana gora). Wymaga zbudowania tkaniny w runtime: waniliowy
        // MagicaPreBuild ma zapisane zaznaczenie i wagi, wiec prebuild off + initData.Clear().
        public static int FixedRow = 45;

        /// <summary>Podmienia siatke peleryny na przepieta i oznacza gorne wiersze jako stale. Prefab, przed pierwszym zalozeniem.</summary>
        public static void PinTopToHood(GameObject prefab)
        {
            try
            {
                var mesh = ModAssets.Load<Mesh>("WraithCape_mesh");
                var smr = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(s => s.sharedMesh && s.sharedMesh.name.StartsWith("cape2"));
                if (!mesh || !smr || !mesh.isReadable || mesh.vertexCount != smr.sharedMesh.vertexCount)
                {
                    Jotunn.Logger.LogWarning($"WraithCapeCloth: bez przypiecia do kaptura (mesh={(bool)mesh} readable={(mesh ? mesh.isReadable : false)} smr={(bool)smr})");
                    return;
                }
                var cloth = FindCloths(prefab).FirstOrDefault();
                var sd2 = cloth?.GetType().GetMethod("GetSerializeData2", Any)?.Invoke(cloth, null);
                if (sd2 == null) { Jotunn.Logger.LogWarning("WraithCapeCloth: brak serializeData2"); return; }

                smr.sharedMesh = mesh;
                var pre = sd2.GetType().GetField("preBuildData", Any)?.GetValue(sd2);
                pre?.GetType().GetField("enabled", Any)?.SetValue(pre, false);
                var init = sd2.GetType().GetField("initData", Any)?.GetValue(sd2);
                init?.GetType().GetMethod("Clear", Any)?.Invoke(init, null);

                var sel = sd2.GetType().GetField("selectionData", Any)?.GetValue(sd2);
                var positions = sel?.GetType().GetField("positions", Any)?.GetValue(sel) as Array;
                var attributes = sel?.GetType().GetField("attributes", Any)?.GetValue(sel) as Array;
                if (positions == null || attributes == null || positions.Length != attributes.Length)
                {
                    Jotunn.Logger.LogWarning("WraithCapeCloth: brak selectionData - tylko wagi przepiete");
                    return;
                }
                var verts = mesh.vertices; var uv = mesh.uv;
                var attrType = attributes.GetType().GetElementType();
                var valueField = attrType.GetField("Value", Any);
                int fixedNow = 0, madeFixed = 0;
                for (var i = 0; i < positions.Length; i++)
                {
                    var p = positions.GetValue(i);
                    var pt = p.GetType();
                    var pos = new Vector3((float)pt.GetField("x").GetValue(p), (float)pt.GetField("y").GetValue(p), (float)pt.GetField("z").GetValue(p));
                    var best = 0; var bd = float.MaxValue;
                    for (var j = 0; j < verts.Length; j++) { var d = (verts[j] - pos).sqrMagnitude; if (d < bd) { bd = d; best = j; } }
                    var row = (1f - uv[best].y) * 128f;
                    var attr = attributes.GetValue(i);
                    var value = (byte)valueField.GetValue(attr);
                    if ((value & 1) != 0) fixedNow++;
                    if (row <= FixedRow && (value & 1) == 0)
                    {
                        value = (byte)((value & ~3) | 1);
                        valueField.SetValue(attr, value); // attr to boxed struct - zapis w kopii, potem SetValue do tablicy
                        attributes.SetValue(attr, i);
                        madeFixed++;
                    }
                }
                Jotunn.Logger.LogDebug($"WraithCapeCloth: gora przypieta do kaptura - stale {fixedNow}+{madeFixed}/{positions.Length} punktow (do wiersza {FixedRow}), prebuild off");
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"WraithCapeCloth: przypiecie do kaptura nie powiodlo sie: {ex}");
            }
        }

        /// <summary>Na prefabie itemu (przed pierwszym zalozeniem - VisEquipment.SetupCloth buduje z tych danych).</summary>
        public static void ApplyToPrefab(GameObject prefab)
        {
            try
            {
                var cloths = FindCloths(prefab);
                foreach (var cloth in cloths) Jotunn.Logger.LogDebug($"WraithCapeCloth: {cloth.name} {Apply(cloth)}");
                if (cloths.Length == 0) Jotunn.Logger.LogWarning("WraithCapeCloth: brak MagicaCloth na prefabie peleryny");
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"WraithCapeCloth: {ex}");
            }
        }

        /// <summary>Dev: na zalozonej pelerynie lokalnego gracza (SetParameterChange przelicza parametry w locie).</summary>
        public static string ApplyLive()
        {
            var player = Player.m_localPlayer;
            if (!player) return "brak gracza";
            var cloths = FindCloths(player.gameObject).Where(c => c.name.StartsWith("cape2")).ToArray();
            if (cloths.Length == 0) return "brak zalozonej peleryny";
            var result = "";
            foreach (var cloth in cloths)
            {
                result = Apply(cloth);
                cloth.GetType().GetMethod("SetParameterChange", Any, null, Type.EmptyTypes, null)?.Invoke(cloth, null);
            }
            return result;
        }
    }
}
