using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ConveyorSystem.EditorTools
{
    /// <summary>
    /// The provided package has straight and incline conveyors but no corner piece.
    /// This builds 90 degree left and right curve conveyors procedurally (belt, side
    /// rails and support posts) that match the straight belt's width, height and
    /// materials, so lines can turn corners.
    /// </summary>
    public static class CurveGenerator
    {
        const int Segments = 20;

        /// <param name="width">Full width of the straight conveyor (frame included).</param>
        /// <param name="height">Height of the belt surface above the conveyor's feet.</param>
        public static List<ConveyorPiece> Create(float width, float height, Material beltMat, Material frameMat,
            string folder, bool overwrite)
        {
            return new List<ConveyorPiece>
            {
                CreateOne(true, width, height, beltMat, frameMat, folder, overwrite),
                CreateOne(false, width, height, beltMat, frameMat, folder, overwrite)
            };
        }

        static ConveyorPiece CreateOne(bool left, float width, float height, Material beltMat, Material frameMat,
            string folder, bool overwrite)
        {
            string side = left ? "Left" : "Right";
            string prefabPath = folder + "/Curve_90_" + side + ".prefab";
            string meshPath = folder + "/Curve_90_" + side + "_Mesh.asset";

            if (!overwrite && File.Exists(prefabPath))
            {
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (existing != null && existing.GetComponent<ConveyorPiece>() != null) return existing.GetComponent<ConveyorPiece>();
            }

            float s = left ? 1f : -1f;          // mirror for right-hand curves
            float rc = width;                    // centre-line radius
            float rIn = rc - width * 0.5f;
            float rOut = rc + width * 0.5f;
            float rail = width * 0.07f;
            float thickness = Mathf.Clamp(height * 0.08f, 0.01f, height * 0.5f);
            float railHeight = width * 0.08f;

            var mb = new MeshBuilder();
            // Belt (sub-mesh 0) and side rails (sub-mesh 1)
            Slab(mb, 0, s, rc, rIn + rail, rOut - rail, height - thickness, height);
            Slab(mb, 1, s, rc, rIn, rIn + rail, height - thickness * 1.5f, height + railHeight);
            Slab(mb, 1, s, rc, rOut - rail, rOut, height - thickness * 1.5f, height + railHeight);

            // Support posts
            float postTop = height - thickness;
            if (postTop > 0.001f)
            {
                float half = width * 0.05f;
                foreach (float deg in new[] { 22.5f, 67.5f })
                {
                    Vector3 c = Point(s, rc, rc, deg * Mathf.Deg2Rad, postTop * 0.5f);
                    Box(mb, 1, c, new Vector3(half, postTop * 0.5f, half));
                }
            }

            Mesh mesh = mb.ToMesh("Curve_90_" + side);
            if (File.Exists(meshPath)) AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            // Prefab
            var root = new GameObject("Conveyor_Curve_90_" + side);
            var model = new GameObject("Model");
            model.transform.SetParent(root.transform, false);
            model.AddComponent<MeshFilter>().sharedMesh = mesh;
            model.AddComponent<MeshRenderer>().sharedMaterials = new[] { beltMat, frameMat != null ? frameMat : beltMat };
            model.AddComponent<MeshCollider>().sharedMesh = mesh;

            ConveyorPiece piece = root.AddComponent<ConveyorPiece>();
            piece.displayName = "Curve 90° (" + side + ")";
            piece.shape = left ? ConveyorShape.CurveLeft : ConveyorShape.CurveRight;

            var entry = new GameObject("Entry").transform;
            entry.SetParent(root.transform, false);
            entry.localPosition = new Vector3(0f, height, 0f);
            entry.localRotation = Quaternion.identity;

            var exit = new GameObject("Exit").transform;
            exit.SetParent(root.transform, false);
            float end = 90f * Mathf.Deg2Rad;
            exit.localPosition = Point(s, rc, rc, end, height);
            exit.localRotation = Quaternion.LookRotation(Tangent(s, end), Vector3.up);

            piece.entry = entry;
            piece.exit = exit;

            GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            Object.DestroyImmediate(root);
            return asset != null ? asset.GetComponent<ConveyorPiece>() : null;
        }

        // Entry is at the origin heading +Z; a left curve turns towards -X (s = 1), a right curve towards +X (s = -1).
        static Vector3 Point(float s, float rc, float r, float angle, float y) =>
            new Vector3(s * (-rc + r * Mathf.Cos(angle)), y, r * Mathf.Sin(angle));

        static Vector3 Radial(float s, float angle) => new Vector3(s * Mathf.Cos(angle), 0f, Mathf.Sin(angle));

        static Vector3 Tangent(float s, float angle) => new Vector3(-s * Mathf.Sin(angle), 0f, Mathf.Cos(angle));

        /// <summary>A rectangular cross-section swept along the quarter circle.</summary>
        static void Slab(MeshBuilder mb, int sub, float s, float rc, float rIn, float rOut, float y0, float y1)
        {
            for (int i = 0; i < Segments; i++)
            {
                float a0 = 0.5f * Mathf.PI * i / Segments;
                float a1 = 0.5f * Mathf.PI * (i + 1) / Segments;
                Vector3 radial = Radial(s, (a0 + a1) * 0.5f);

                mb.Quad(sub, Point(s, rc, rIn, a0, y1), Point(s, rc, rIn, a1, y1), Point(s, rc, rOut, a1, y1), Point(s, rc, rOut, a0, y1), Vector3.up);
                mb.Quad(sub, Point(s, rc, rIn, a0, y0), Point(s, rc, rIn, a1, y0), Point(s, rc, rOut, a1, y0), Point(s, rc, rOut, a0, y0), Vector3.down);
                mb.Quad(sub, Point(s, rc, rIn, a0, y0), Point(s, rc, rIn, a1, y0), Point(s, rc, rIn, a1, y1), Point(s, rc, rIn, a0, y1), -radial);
                mb.Quad(sub, Point(s, rc, rOut, a0, y0), Point(s, rc, rOut, a1, y0), Point(s, rc, rOut, a1, y1), Point(s, rc, rOut, a0, y1), radial);
            }

            float end = 0.5f * Mathf.PI;
            mb.Quad(sub, Point(s, rc, rIn, 0f, y0), Point(s, rc, rOut, 0f, y0), Point(s, rc, rOut, 0f, y1), Point(s, rc, rIn, 0f, y1), -Tangent(s, 0f));
            mb.Quad(sub, Point(s, rc, rIn, end, y0), Point(s, rc, rOut, end, y0), Point(s, rc, rOut, end, y1), Point(s, rc, rIn, end, y1), Tangent(s, end));
        }

        static void Box(MeshBuilder mb, int sub, Vector3 c, Vector3 h)
        {
            Vector3 P(float x, float y, float z) => c + new Vector3(x * h.x, y * h.y, z * h.z);
            mb.Quad(sub, P(-1, 1, -1), P(-1, 1, 1), P(1, 1, 1), P(1, 1, -1), Vector3.up);
            mb.Quad(sub, P(-1, -1, -1), P(-1, -1, 1), P(1, -1, 1), P(1, -1, -1), Vector3.down);
            mb.Quad(sub, P(1, -1, -1), P(1, -1, 1), P(1, 1, 1), P(1, 1, -1), Vector3.right);
            mb.Quad(sub, P(-1, -1, -1), P(-1, -1, 1), P(-1, 1, 1), P(-1, 1, -1), Vector3.left);
            mb.Quad(sub, P(-1, -1, 1), P(1, -1, 1), P(1, 1, 1), P(-1, 1, 1), Vector3.forward);
            mb.Quad(sub, P(-1, -1, -1), P(1, -1, -1), P(1, 1, -1), P(-1, 1, -1), Vector3.back);
        }

        class MeshBuilder
        {
            readonly List<Vector3> vertices = new List<Vector3>();
            readonly List<Vector3> normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<int>[] triangles = { new List<int>(), new List<int>() };

            /// <summary>Adds a flat quad, fixing the winding so it faces <paramref name="normal"/>.</summary>
            public void Quad(int sub, Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), normal) < 0f)
                {
                    Vector3 t = b; b = d; d = t;
                }
                int i = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                for (int k = 0; k < 4; k++) normals.Add(normal);
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(1f, 0f));
                triangles[sub].Add(i); triangles[sub].Add(i + 1); triangles[sub].Add(i + 2);
                triangles[sub].Add(i); triangles[sub].Add(i + 2); triangles[sub].Add(i + 3);
            }

            public Mesh ToMesh(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uvs);
                mesh.subMeshCount = 2;
                mesh.SetTriangles(triangles[0], 0);
                mesh.SetTriangles(triangles[1], 1);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }
        }
    }
}
