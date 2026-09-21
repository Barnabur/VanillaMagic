using System;
using System.Collections.Generic;
using UnityEngine;

namespace VanilaMagic.Items
{
    /// <summary>Skladacz siatki z rurek wokol krzywych parametrycznych.</summary>
    internal sealed class TubeBuilder
    {
        private readonly List<Vector3> _verts = new List<Vector3>();
        private readonly List<Vector3> _normals = new List<Vector3>();
        private readonly List<Vector2> _uvs = new List<Vector2>();
        private readonly List<int> _tris = new List<int>();

        public void AddTube(Func<float, Vector3> curve, Func<float, float> radius, int segments, int ring)
        {
            var baseIndex = _verts.Count;
            var prevNormal = Vector3.zero;
            var length = 0f;
            var prevPoint = curve(0f);
            var dt = 1f / segments;

            for (var i = 0; i <= segments; i++)
            {
                var t = i * dt;
                var p = curve(t);
                var tangent = (curve(Mathf.Min(1f, t + dt)) - curve(Mathf.Max(0f, t - dt))).normalized;
                if (tangent == Vector3.zero) tangent = Vector3.up;

                Vector3 n;
                if (i == 0)
                {
                    var reference = Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up;
                    n = Vector3.Cross(tangent, reference).normalized;
                }
                else
                {
                    // transport rownolegly: rzut poprzedniej normalnej na plaszczyzne prostopadla do tangensa
                    n = (prevNormal - tangent * Vector3.Dot(prevNormal, tangent)).normalized;
                    if (n == Vector3.zero) n = Vector3.Cross(tangent, Vector3.right).normalized;
                }
                prevNormal = n;
                var b = Vector3.Cross(tangent, n);
                length += Vector3.Distance(prevPoint, p);
                prevPoint = p;

                var r = radius(t);
                for (var j = 0; j <= ring; j++)
                {
                    var a = j / (float)ring * 2f * Mathf.PI;
                    var dir = n * Mathf.Cos(a) + b * Mathf.Sin(a);
                    _verts.Add(p + dir * r);
                    _normals.Add(dir);
                    _uvs.Add(new Vector2(j / (float)ring, length * 4f));
                }
            }

            var stride = ring + 1;
            for (var i = 0; i < segments; i++)
            {
                for (var j = 0; j < ring; j++)
                {
                    var a = baseIndex + i * stride + j;
                    var next = a + stride;
                    _tris.Add(a); _tris.Add(next); _tris.Add(a + 1);
                    _tris.Add(a + 1); _tris.Add(next); _tris.Add(next + 1);
                }
            }
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(_verts);
            mesh.SetNormals(_normals);
            mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_tris, 0);
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            return mesh;
        }
    }
}
