using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VanilaMagic.Items
{
    /// <summary>
    /// Lancuch "naszyty" na pelerynie: zamiast wlasnej symulacji sledzi wierzcholki peleryny gracza,
    /// wiec zawsze lezy NA plaszczu i rusza sie razem z nim. Pozycje bierze z
    /// SkinnedMeshRenderer.BakeMesh (aktualna, zdeformowana siatka) - niezaleznie od tego, czy
    /// tkanine liczy Unity Cloth, MagicaCloth (nowsze Valheim podmienia Cloth w runtime, siatka
    /// staje sie "cape2(Clone)" i komponent Cloth znika) czy nic.
    /// Przy bindowaniu startuje od wierzcholka tylnej czesci peleryny (z ujemne w przestrzeni postaci)
    /// najblizszego x = m_sideX u gory i schodzi zachlannie w dol po siatce (kolejny wierzcholek
    /// ~m_spacing nizej, jak najmniej w bok) - siatka cape2 ma tylko 96 wierzcholkow i rozszerza
    /// sie ku dolowi. Co klatke ustawia segmenty ("link*") miedzy kolejnymi wierzcholkami,
    /// skalujac ich dlugosc do aktualnej odleglosci; "cuff" (kajdan) wisi na ostatnim.
    /// Wierzcholki sa odsuwane od osi postaci o m_surfaceOffset, zeby ogniwa nie tonely w materiale.
    /// </summary>
    public class ClothChain : MonoBehaviour
    {
        public string m_rendererName = "cape2"; // obiekt SkinnedMeshRenderer peleryny (CapeTrollHide: attach_skin/cape2)
        public float m_sideX = -0.12f;      // przestrzen postaci: prawo(+)/lewo(-)
        public float m_topDrop = 0.12f;     // start: wierzcholki nie nizej niz tyle od najwyzszego z tylu
        public float m_backZ = -0.04f;      // tylko tyl peleryny: z (przestrzen postaci) ponizej tej wartosci
        public float m_spacing = 0.15f;     // odstep celow wzdluz peleryny (dlugosc segmentu w spoczynku)
        public float m_surfaceOffset = 0.03f;
        public float m_cuffRadius = 0.055f;
        public float m_radialScale = 0.32f;
        public float m_meshLengthY = 0.96f; // dlugosc siatki ogniw (bounds.size.y) bez skali
        public float m_meshTopY = 0.49f;    // bounds.center.y + extents.y bez skali

        private Transform[] _segments;
        private Transform _cuff;
        private Transform _character;
        private SkinnedMeshRenderer _renderer;
        private Mesh _baked;
        private int[] _indices;
        private float _retryAt;
        private string _lastFail;

        private void Start()
        {
            var links = new List<Transform>();
            foreach (Transform child in transform)
            {
                if (child.name == "cuff") _cuff = child;
                else links.Add(child);
            }
            _segments = links.ToArray();
            var character = GetComponentInParent<Character>();
            _character = character ? character.transform : transform.root;
            _baked = new Mesh { name = "clothchain_bake" };
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (_baked) Destroy(_baked);
        }

        private void LateUpdate()
        {
            if (!_renderer || _indices == null)
            {
                if (Time.time < _retryAt) return;
                _retryAt = Time.time + 0.5f;
                if (!Bind())
                {
                    SetVisible(false);
                    return;
                }
                SetVisible(true);
            }

            var verts = BakeWorld();
            if (verts == null) return;
            var count = _indices.Length;
            var pts = new Vector3[count];
            var up = _character.up;
            for (var i = 0; i < count; i++)
            {
                var idx = _indices[i];
                if (idx >= verts.Length)
                {
                    _renderer = null; // siatka sie zmienila - zbinduj ponownie
                    return;
                }
                var w = verts[idx];
                // odsun od osi postaci (poziomo), zeby lancuch lezal na materiale, nie w nim
                var radial = w - _character.position;
                radial -= up * Vector3.Dot(radial, up);
                if (radial.sqrMagnitude > 1e-6f) w += radial.normalized * m_surfaceOffset;
                pts[i] = w;
            }
            Place(pts);
        }

        /// <summary>Aktualne pozycje wierzcholkow peleryny w swiecie (BakeMesh ze skala + obrot/pozycja renderera).</summary>
        private Vector3[] BakeWorld()
        {
            if (!_renderer || !_renderer.sharedMesh) return null;
            _renderer.BakeMesh(_baked, true);
            var local = _baked.vertices;
            if (local == null || local.Length == 0) return null;
            var t = _renderer.transform;
            var pos = t.position;
            var rot = t.rotation;
            var world = new Vector3[local.Length];
            for (var i = 0; i < local.Length; i++) world[i] = pos + rot * local[i];
            return world;
        }

        private bool Fail(string reason)
        {
            if (reason != _lastFail)
            {
                _lastFail = reason;
                Jotunn.Logger.LogWarning($"ClothChain {name}: {reason}");
            }
            return false;
        }

        private bool Bind()
        {
            var root = _character;
            var renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(false);
            _renderer = renderers.FirstOrDefault(r => r.name == m_rendererName && r.sharedMesh)
                        ?? renderers.FirstOrDefault(r => r.sharedMesh && r.sharedMesh.name.StartsWith(m_rendererName));
            if (!_renderer)
            {
                var names = renderers.Select(r => $"{r.name}/{(r.sharedMesh ? r.sharedMesh.name : "-")}");
                return Fail($"brak peleryny '{m_rendererName}' pod {root.name}; skinned: {string.Join(", ", names)}");
            }
            var verts = BakeWorld();
            if (verts == null || verts.Length < 4) return Fail($"BakeMesh {_renderer.name} dal {(verts == null ? 0 : verts.Length)} wierzcholkow");

            // wierzcholki w przestrzeni postaci
            var local = new Vector3[verts.Length];
            for (var i = 0; i < verts.Length; i++) local[i] = _character.InverseTransformPoint(verts[i]);

            var back = Enumerable.Range(0, local.Length).Where(i => local[i].z < m_backZ).ToList();
            if (back.Count < 2) return Fail($"za malo wierzcholkow z tylu (z < {m_backZ}): {back.Count}; z w zakresie {local.Min(p => p.z):0.00}..{local.Max(p => p.z):0.00}");

            // start: u gory tylu peleryny, najblizej m_sideX
            var topY = back.Max(i => local[i].y);
            var start = back.Where(i => local[i].y >= topY - m_topDrop)
                .OrderBy(i => Mathf.Abs(local[i].x - m_sideX))
                .First();
            var picked = new List<int> { start };
            var current = start;
            for (var k = 0; k < _segments.Length; k++)
            {
                var best = -1;
                var bestScore = float.MaxValue;
                foreach (var i in back)
                {
                    if (picked.Contains(i)) continue;
                    var dy = local[current].y - local[i].y;
                    if (dy < m_spacing * 0.5f) continue; // tylko wyraznie nizej
                    var dx = Mathf.Abs(local[i].x - local[current].x);
                    var dz = Mathf.Abs(local[i].z - local[current].z);
                    var score = Mathf.Abs(dy - m_spacing) + dx * 1.5f + dz * 0.5f;
                    if (score < bestScore)
                    {
                        bestScore = score;
                        best = i;
                    }
                }
                if (best < 0) break;
                picked.Add(best);
                current = best;
            }
            if (picked.Count < 2) return Fail("nie udalo sie zejsc po siatce ponizej startu");

            _lastFail = null;
            _indices = picked.ToArray();
            Jotunn.Logger.LogInfo($"ClothChain {name}: peleryna {_renderer.name} (mesh {_renderer.sharedMesh.name}, {verts.Length} wierzch.), " +
                                  $"sciezka od x={m_sideX}: " + string.Join(", ", _indices.Select(i => $"#{i}({local[i].x:0.00},{local[i].y:0.00},{local[i].z:0.00})")));
            return true;
        }

        private void Place(Vector3[] pts)
        {
            var rot = Quaternion.identity;
            var lastEnd = pts[pts.Length - 1];
            for (var i = 0; i < _segments.Length; i++)
            {
                var seg = _segments[i];
                if (i + 1 >= pts.Length)
                {
                    seg.gameObject.SetActive(false);
                    continue;
                }
                seg.gameObject.SetActive(true);
                var dir = pts[i + 1] - pts[i];
                var dist = dir.magnitude;
                if (dist < 1e-4f) dir = Vector3.down * 0.01f;
                rot = Quaternion.FromToRotation(Vector3.down, dir.normalized);
                var scaleY = Mathf.Max(0.05f, dist / m_meshLengthY);
                seg.localScale = new Vector3(m_radialScale, scaleY, m_radialScale);
                seg.rotation = rot;
                seg.position = pts[i] - rot * (Vector3.up * (m_meshTopY * scaleY));
            }
            if (_cuff)
            {
                _cuff.rotation = rot;
                _cuff.position = lastEnd + rot * (Vector3.down * m_cuffRadius);
            }
        }

        private void SetVisible(bool visible)
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = visible;
        }

        /// <summary>Buduje lancuch naszyty na pelerynie: N segmentow + kajdan, jak DanglingChain.Build.</summary>
        public static GameObject Build(string name, Mesh linkMesh, Material linkMaterial, int segments, float radialScale,
            float spacing, float sideX, Material cuffMaterial)
        {
            var root = new GameObject(name);
            for (var i = 0; i < segments; i++)
            {
                var seg = new GameObject($"link{i}");
                seg.transform.SetParent(root.transform, false);
                seg.transform.localScale = new Vector3(radialScale, spacing / linkMesh.bounds.size.y, radialScale);
                AddMesh(seg, linkMesh, linkMaterial);
            }
            if (cuffMaterial)
            {
                var cuff = new GameObject("cuff");
                cuff.transform.SetParent(root.transform, false);
                AddMesh(cuff, DanglingChain.CuffMesh, cuffMaterial);
            }
            var chain = root.AddComponent<ClothChain>();
            chain.m_sideX = sideX;
            chain.m_spacing = spacing;
            chain.m_radialScale = radialScale;
            chain.m_meshLengthY = linkMesh.bounds.size.y;
            chain.m_meshTopY = linkMesh.bounds.center.y + linkMesh.bounds.extents.y;
            return root;
        }

        private static void AddMesh(GameObject go, Mesh mesh, Material material)
        {
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
    }
}
