using UnityEngine;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Zwisajacy lancuch z segmentow: prosty verlet (punkty, grawitacja, tlumienie, sztywna
    /// dlugosc miedzy punktami), pierwszy punkt przyklejony do kosci, pod ktora gra podpiela
    /// obiekt (VisEquipment.AttachArmor: dziecko prefabu "attach_[Kosc]" laduje pod ta koscia).
    /// Bez fizyki Unity - zero kolizji z graczem i drgan. Segmenty ("link*") to dzieci tego obiektu,
    /// kazdy z siatka lancucha ustawiona wzdluz lokalnego -Y (gora segmentu w punkcie i,
    /// dol w punkcie i+1); opcjonalne dziecko "cuff" (kajdan) wisi na wolnym koncu.
    /// Offset zaczepu i dodatkowa sila podawane w przestrzeni postaci (prawo/gora/przod),
    /// bo lokalne osie kosci nie sa znane; sila do tylu pozwala polozyc lancuch NA pelerynie.
    /// </summary>
    public class DanglingChain : MonoBehaviour
    {
        public float m_segmentLength = 0.17f;
        public float m_segmentTop = 0.08f; // odleglosc od pivota segmentu do jego gornego konca (wzdluz +Y)
        public float m_cuffRadius = WraithArmor.CuffRadius;
        public float m_gravity = 14f;      // wiecej niz 9.81 = "ciezki" metal: szybciej opada i sie uspokaja
        public float m_damping = 0.96f; // bezwladnosc w biegu zostaje, ale bez lekkiego "fruwania"
        public float m_maxSpeed = 12f; // m/s - tylko ochrona przed biczowaniem przy bardzo szybkich animacjach
        public int m_iterations = 3;
        public Vector3 m_characterOffset = Vector3.zero; // (prawo, gora, przod) wzgledem postaci
        public Vector3 m_characterForce = Vector3.zero;  // przyspieszenie w przestrzeni postaci (m/s^2)
        public float m_windScale = 0.6f;   // ile wiatru z EnvMan trafia w lancuch
        public float m_gustScale = 1.2f;   // losowe podmuchy (Perlin), zeby kolysal sie tez przy slabym wietrze
        public float m_maxStep = 0.05f; // ochrona przed eksplozja po lagu

        // kolizja z kapsula tulowia (os pionowa postaci przez kosc zaczepu + offset), zeby lancuch
        // ukladal sie po plecach / pelerynie zamiast wisiec w linii prostej
        public bool m_collide;
        public float m_colliderRadius = 0.18f;
        public Vector3 m_colliderOffset = Vector3.zero; // przestrzen postaci, wzgledem kosci zaczepu
        public float m_colliderTop = 0.25f;    // zakres osi wzgledem srodka (m)
        public float m_colliderBottom = -0.8f;
        public float m_colliderTilt = 0f;      // pochylenie osi: skladowa "przod" na jednostke "gora" - os idzie w dol DO TYLU,
                                               // jak peleryna, ktora odstaje od plecow coraz bardziej ku dolowi

        private float _gustSeed;

        private Transform[] _segments;
        private Transform _cuff;
        private Vector3[] _pos;
        private Vector3[] _prev;
        private Transform _character;
        private bool _ready;

        private void Start()
        {
            var links = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in transform)
            {
                if (child.name == "cuff") _cuff = child;
                else links.Add(child);
            }
            _segments = links.ToArray();
            if (_segments.Length == 0)
            {
                enabled = false;
                return;
            }
            var character = GetComponentInParent<Character>();
            _character = character ? character.transform : transform.root;
            _gustSeed = Random.value * 100f;

            var count = _segments.Length + 1;
            _pos = new Vector3[count];
            _prev = new Vector3[count];
            Reset(Anchor());
            _ready = true;
            Place();
        }

        private void Reset(Vector3 anchor)
        {
            for (var i = 0; i < _pos.Length; i++)
            {
                _pos[i] = anchor + Vector3.down * (m_segmentLength * i);
                _prev[i] = _pos[i];
            }
        }

        private Vector3 Anchor()
        {
            return transform.position + _character.TransformVector(m_characterOffset);
        }

        private void LateUpdate()
        {
            if (!_ready) return;
            var dt = Mathf.Min(Time.deltaTime, m_maxStep);
            var anchor = Anchor();

            // teleport / respawn: zresetuj zamiast rozciagac lancuch przez pol mapy
            if ((anchor - _pos[0]).sqrMagnitude > 4f) Reset(anchor);

            _pos[0] = anchor;
            var accel = (Vector3.down * m_gravity + _character.TransformVector(m_characterForce) + Wind()) * (dt * dt);
            var maxStep = m_maxSpeed * dt;
            for (var i = 1; i < _pos.Length; i++)
            {
                var velocity = (_pos[i] - _prev[i]) * m_damping;
                if (velocity.sqrMagnitude > maxStep * maxStep) velocity = velocity.normalized * maxStep;
                _prev[i] = _pos[i];
                _pos[i] += velocity + accel;
            }

            var colliderBase = transform.position + _character.TransformVector(m_colliderOffset);
            var up = (_character.up + _character.forward * m_colliderTilt).normalized;
            for (var it = 0; it < m_iterations; it++)
            {
                _pos[0] = anchor;
                for (var i = 1; i < _pos.Length; i++)
                {
                    var delta = _pos[i] - _pos[i - 1];
                    var dist = delta.magnitude;
                    if (dist < 1e-5f) continue;
                    var correction = delta * ((dist - m_segmentLength) / dist);
                    if (i == 1)
                    {
                        _pos[i] -= correction;
                    }
                    else
                    {
                        _pos[i - 1] += correction * 0.5f;
                        _pos[i] -= correction * 0.5f;
                    }
                    if (m_collide) PushOutOfBody(ref _pos[i], colliderBase, up);
                }
            }
            Place();
        }

        private void PushOutOfBody(ref Vector3 p, Vector3 axisBase, Vector3 up)
        {
            var t = Mathf.Clamp(Vector3.Dot(p - axisBase, up), m_colliderBottom, m_colliderTop);
            var closest = axisBase + up * t;
            var radial = p - closest;
            var dist = radial.magnitude;
            if (dist >= m_colliderRadius) return;
            var dir = dist > 1e-4f ? radial / dist : -_character.forward;
            p = closest + dir * m_colliderRadius;
        }

        /// <summary>Wiatr z gry (EnvMan) + losowe podmuchy: lekkie kolysanie w spoczynku.</summary>
        private Vector3 Wind()
        {
            var wind = Vector3.zero;
            if (EnvMan.instance) wind = EnvMan.instance.GetWindForce() * m_windScale;
            var t = Time.time * 0.35f;
            var gust = new Vector3(Mathf.PerlinNoise(t, _gustSeed) - 0.5f, 0f, Mathf.PerlinNoise(_gustSeed, t) - 0.5f) * m_gustScale;
            return wind + gust;
        }

        private void Place()
        {
            var rot = Quaternion.identity;
            for (var i = 0; i < _segments.Length; i++)
            {
                var dir = _pos[i + 1] - _pos[i];
                if (dir.sqrMagnitude < 1e-8f) dir = Vector3.down;
                rot = Quaternion.FromToRotation(Vector3.down, dir.normalized);
                var seg = _segments[i];
                seg.rotation = rot;
                // gora segmentu (lokalne +Y * m_segmentTop) ma lezec w punkcie i
                seg.position = _pos[i] - rot * (Vector3.up * m_segmentTop);
            }
            if (_cuff)
            {
                // kajdan: pierscien w plaszczyznie zawierajacej lancuch, gorna krawedz w ostatnim punkcie
                var end = _pos[_pos.Length - 1];
                _cuff.rotation = rot;
                _cuff.position = end + rot * (Vector3.down * m_cuffRadius);
            }
        }

        /// <summary>
        /// Buduje obiekt zaczepu z N segmentami z podanej siatki lancucha. Siatka Chain.model
        /// (chain.002_Torus.002) biegnie wzdluz Y (~0.96 m, srednica ~0.17 m); radialScale ustala
        /// grubosc, lengthScale dlugosc segmentu. cuffMaterial != null dodaje kajdan (torus) na koncu.
        /// Zwracany root trzeba nazwac "attach_[Kosc]" (lub wpiac pod taki) w prefabie itemu.
        /// </summary>
        public static GameObject Build(string name, Mesh linkMesh, Material linkMaterial, int segments,
            float radialScale, float lengthScale, Vector3 characterOffset, Vector3 characterForce, Material cuffMaterial,
            bool collideWithBody = false, Vector3 colliderOffset = default, float colliderTilt = 0f)
        {
            var root = new GameObject(name);
            var bounds = linkMesh.bounds;
            var top = (bounds.center.y + bounds.extents.y) * lengthScale;
            var length = bounds.size.y * lengthScale;
            for (var i = 0; i < segments; i++)
            {
                var seg = new GameObject($"link{i}");
                seg.transform.SetParent(root.transform, false);
                seg.transform.localScale = new Vector3(radialScale, lengthScale, radialScale);
                AddMesh(seg, linkMesh, linkMaterial);
            }

            var cuffRadius = WraithArmor.CuffRadius;
            if (cuffMaterial)
            {
                var cuff = new GameObject("cuff");
                cuff.transform.SetParent(root.transform, false);
                AddMesh(cuff, CuffMesh, cuffMaterial);
            }

            var chain = root.AddComponent<DanglingChain>();
            chain.m_segmentLength = length;
            chain.m_segmentTop = top;
            chain.m_cuffRadius = cuffRadius;
            chain.m_characterOffset = characterOffset;
            chain.m_characterForce = characterForce;
            chain.m_collide = collideWithBody;
            chain.m_colliderOffset = colliderOffset;
            chain.m_colliderTilt = colliderTilt;
            return root;
        }

        private static void AddMesh(GameObject go, Mesh mesh, Material material)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        private static Mesh _cuffMesh;

        /// <summary>Wspolna siatka kajdana - promien i grubosc jak kajdan namalowany na pelerynie (WraithCape.png).</summary>
        public static Mesh CuffMesh => _cuffMesh ? _cuffMesh : BuildCuffMesh(WraithArmor.CuffRadius, WraithArmor.ChainTubeRadius);

        /// <summary>
        /// Proceduralny odcinek lancucha: `links` owalnych ogniw wzdluz osi Y (srodek odcinka w 0), naprzemiennie
        /// w plaszczyznach XY i ZY, o wymiarach 1:1 z ogniwami namalowanymi na pelerynie. Siatka w metrach -
        /// DanglingChain.Build / ClothChain.Build wolac ze skala 1. UV z TubeBuilder nie pasuja do atlasu,
        /// wiec material = jednolity kolor (WraithCuff_mat).
        /// </summary>
        public static Mesh BuildLinkChainMesh(int links, float linkLength, float linkWidth, float tubeRadius, float pitch)
        {
            var mb = new TubeBuilder();
            var total = (links - 1) * pitch + linkLength;
            var a = linkLength * 0.5f - tubeRadius; // polos wzdluz lancucha (os rurki)
            var b = linkWidth * 0.5f - tubeRadius;  // polos poprzeczna
            for (var i = 0; i < links; i++)
            {
                var cy = -total * 0.5f + linkLength * 0.5f + i * pitch;
                var flat = i % 2 == 0;
                mb.AddTube(t =>
                {
                    var ang = t * 2f * Mathf.PI;
                    var y = cy + Mathf.Sin(ang) * a;
                    var w = Mathf.Cos(ang) * b;
                    return flat ? new Vector3(w, y, 0f) : new Vector3(0f, y, w);
                }, t => tubeRadius, 28, 8);
            }
            return mb.ToMesh($"wraith_chain_{links}");
        }

        /// <summary>Kajdan = torus w plaszczyznie XY (os Z), zeby po obrocie segmentu wisial pionowo w linii lancucha.</summary>
        private static Mesh BuildCuffMesh(float ringRadius, float tubeRadius)
        {
            if (_cuffMesh) return _cuffMesh;
            var mb = new TubeBuilder();
            mb.AddTube(
                t => new Vector3(Mathf.Cos(t * 2f * Mathf.PI) * ringRadius, Mathf.Sin(t * 2f * Mathf.PI) * ringRadius, 0f),
                t => tubeRadius, 32, 10);
            _cuffMesh = mb.ToMesh("wraith_cuff");
            return _cuffMesh;
        }
    }
}
