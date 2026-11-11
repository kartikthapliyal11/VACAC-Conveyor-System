using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ConveyorSystem.EditorTools
{
    /// <summary>
    /// Guesses where a conveyor model's belt starts and ends by looking at its mesh,
    /// then places the Entry / Exit snap points there. You can always fine-tune
    /// them by hand afterwards in Prefab Mode.
    /// </summary>
    public static class ConveyorAutoFit
    {
        public static void Fit(ConveyorPiece piece, ConveyorShape shape)
        {
            Transform root = piece.transform;
            List<Vector3> verts = CollectVertices(root);

            Bounds b = new Bounds(Vector3.zero, Vector3.one);
            if (verts.Count > 0)
            {
                b = new Bounds(verts[0], Vector3.zero);
                foreach (Vector3 v in verts) b.Encapsulate(v);
            }
            else
            {
                Debug.LogWarning("[Conveyor] " + piece.name + " has no meshes; using a default 1m box for snap points.");
            }

            EnsurePoints(piece);

            Vector3 inPos, inDir, outPos, outDir;
            bool isCurve = shape == ConveyorShape.CurveLeft || shape == ConveyorShape.CurveRight;
            if (isCurve && verts.Count > 0)
                FitCurve(verts, b, shape == ConveyorShape.CurveLeft, out inPos, out inDir, out outPos, out outDir);
            else
                FitStraight(b, out inPos, out inDir, out outPos, out outDir);

            // Put the points on the belt surface (ray cast down onto the model, slightly inside the ends).
            float inset = Mathf.Max(b.size.x, b.size.z) * 0.04f;
            inPos.y = SurfaceHeight(root, inPos + inDir * inset, b);
            outPos.y = SurfaceHeight(root, outPos - outDir * inset, b);

            // Inclines: make products travel uphill (entry at the low end).
            if (!isCurve && inPos.y > outPos.y + 0.001f)
            {
                Vector3 tmp = inPos; inPos = outPos; outPos = tmp;
                inDir = -inDir; outDir = -outDir;
            }

            piece.entry.localPosition = inPos;
            piece.entry.localRotation = Quaternion.LookRotation(inDir, Vector3.up);
            piece.exit.localPosition = outPos;
            piece.exit.localRotation = Quaternion.LookRotation(outDir, Vector3.up);
        }

        /// <summary>
        /// Some models are exported "standing on their end" (their longest side points up).
        /// A conveyor's longest side should be horizontal, so in that case this returns the
        /// rotation that lays it down with its belt facing up. The belt side is found from
        /// the area-weighted centre of the mesh: belt, rollers and frame are on top and
        /// have far more surface than the thin legs underneath.
        /// Models whose longest side is already horizontal are left alone.
        /// </summary>
        public static Quaternion FindUprightRotation(Transform root)
        {
            bool any = false;
            Bounds b = new Bounds();
            Vector3 weighted = Vector3.zero;
            float totalArea = 0f;

            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null) continue;
                Vector3[] mv = mesh.vertices;
                var pts = new Vector3[mv.Length];
                for (int i = 0; i < mv.Length; i++)
                {
                    pts[i] = root.InverseTransformPoint(mf.transform.TransformPoint(mv[i]));
                    if (!any) { b = new Bounds(pts[i], Vector3.zero); any = true; }
                    else b.Encapsulate(pts[i]);
                }
                int[] tris = mesh.triangles;
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    Vector3 p0 = pts[tris[t]], p1 = pts[tris[t + 1]], p2 = pts[tris[t + 2]];
                    float area = Vector3.Cross(p1 - p0, p2 - p0).magnitude * 0.5f;
                    weighted += (p0 + p1 + p2) / 3f * area;
                    totalArea += area;
                }
            }
            if (!any || totalArea <= 0f) return Quaternion.identity;

            // Already lying down? Leave it.
            if (b.size.y < b.size.x || b.size.y < b.size.z) return Quaternion.identity;

            Vector3 centroid = weighted / totalArea;
            float offX = (centroid.x - b.center.x) / Mathf.Max(b.size.x, 1e-5f);
            float offZ = (centroid.z - b.center.z) / Mathf.Max(b.size.z, 1e-5f);

            Vector3 up;
            if (Mathf.Abs(offX) >= Mathf.Abs(offZ))
                up = new Vector3(Mathf.Sign(offX == 0f ? 1f : offX), 0f, 0f);
            else
                up = new Vector3(0f, 0f, Mathf.Sign(offZ));

            return Quaternion.FromToRotation(up, Vector3.up);
        }

        static void FitStraight(Bounds b, out Vector3 inPos, out Vector3 inDir, out Vector3 outPos, out Vector3 outDir)
        {
            // Products travel along the longest horizontal side.
            if (b.size.z >= b.size.x)
            {
                inPos = new Vector3(b.center.x, 0f, b.min.z);
                outPos = new Vector3(b.center.x, 0f, b.max.z);
                inDir = outDir = Vector3.forward;
            }
            else
            {
                inPos = new Vector3(b.min.x, 0f, b.center.z);
                outPos = new Vector3(b.max.x, 0f, b.center.z);
                inDir = outDir = Vector3.right;
            }
        }

        /// <summary>
        /// A 90 degree curve has its two belt ends on two neighbouring sides of its bounding box.
        /// Those sides are the ones where the mesh touches the box along a wide flat edge
        /// (the other two sides are only touched by the outer arc at a single point).
        /// </summary>
        static void FitCurve(List<Vector3> verts, Bounds b, bool left,
            out Vector3 inPos, out Vector3 inDir, out Vector3 outPos, out Vector3 outDir)
        {
            float tol = Mathf.Max(b.size.x, b.size.z) * 0.01f;
            Face zMin = MeasureFace(verts, b.min.z, 2, 0, tol);
            Face zMax = MeasureFace(verts, b.max.z, 2, 0, tol);
            Face xMin = MeasureFace(verts, b.min.x, 0, 2, tol);
            Face xMax = MeasureFace(verts, b.max.x, 0, 2, tol);

            bool useZMin = zMin.span >= zMax.span;
            bool useXMin = xMin.span >= xMax.span;

            Vector3 aPos = useZMin ? new Vector3(zMin.mid, 0f, b.min.z) : new Vector3(zMax.mid, 0f, b.max.z);
            Vector3 aInward = useZMin ? Vector3.forward : Vector3.back;
            Vector3 bPos = useXMin ? new Vector3(b.min.x, 0f, xMin.mid) : new Vector3(b.max.x, 0f, xMax.mid);
            Vector3 bInward = useXMin ? Vector3.right : Vector3.left;

            // Try travelling A -> B; if that turns the wrong way, travel B -> A instead.
            inPos = aPos; inDir = aInward;
            outPos = bPos; outDir = -bInward;
            bool turnsLeft = Vector3.Cross(inDir, outDir).y < 0f;
            if (turnsLeft != left)
            {
                inPos = bPos; inDir = bInward;
                outPos = aPos; outDir = -aInward;
            }
        }

        struct Face { public float span, mid; }

        static Face MeasureFace(List<Vector3> verts, float plane, int axis, int lateral, float tol)
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (Vector3 v in verts)
            {
                if (Mathf.Abs(v[axis] - plane) > tol) continue;
                if (v[lateral] < min) min = v[lateral];
                if (v[lateral] > max) max = v[lateral];
            }
            if (min > max) return new Face { span = 0f, mid = 0f };
            return new Face { span = max - min, mid = (min + max) * 0.5f };
        }

        static float SurfaceHeight(Transform root, Vector3 localPoint, Bounds b)
        {
            Physics.SyncTransforms();
            float margin = Mathf.Max(b.size.y, 0.1f);
            Vector3 origin = root.TransformPoint(new Vector3(localPoint.x, b.max.y + margin, localPoint.z));
            var ray = new Ray(origin, -root.up);
            float best = float.NegativeInfinity;
            foreach (Collider c in root.GetComponentsInChildren<Collider>())
            {
                if (c.Raycast(ray, out RaycastHit hit, b.size.y + margin * 2f))
                    best = Mathf.Max(best, root.InverseTransformPoint(hit.point).y);
            }
            return float.IsNegativeInfinity(best) ? b.max.y : best;
        }

        static List<Vector3> CollectVertices(Transform root)
        {
            var result = new List<Vector3>();
            foreach (MeshFilter mf in root.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null) continue;
                foreach (Vector3 v in mesh.vertices)
                    result.Add(root.InverseTransformPoint(mf.transform.TransformPoint(v)));
            }
            return result;
        }

        static void EnsurePoints(ConveyorPiece piece)
        {
            if (piece.entry == null) piece.entry = CreateChild(piece.transform, "Entry");
            if (piece.exit == null) piece.exit = CreateChild(piece.transform, "Exit");
        }

        static Transform CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create snap point");
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // ---------------------------------------------------------------- right-click menu on the component

        [MenuItem("CONTEXT/ConveyorPiece/Auto-fit Snap Points/Straight")]
        static void MenuStraight(MenuCommand command) => FitFromMenu(command, ConveyorShape.Straight);

        [MenuItem("CONTEXT/ConveyorPiece/Auto-fit Snap Points/Curve Left")]
        static void MenuCurveLeft(MenuCommand command) => FitFromMenu(command, ConveyorShape.CurveLeft);

        [MenuItem("CONTEXT/ConveyorPiece/Auto-fit Snap Points/Curve Right")]
        static void MenuCurveRight(MenuCommand command) => FitFromMenu(command, ConveyorShape.CurveRight);

        static void FitFromMenu(MenuCommand command, ConveyorShape shape)
        {
            var piece = (ConveyorPiece)command.context;
            Undo.RegisterFullObjectHierarchyUndo(piece.gameObject, "Auto-fit snap points");
            piece.shape = shape;
            Fit(piece, shape);
            EditorUtility.SetDirty(piece);
            if (piece.entry != null) EditorUtility.SetDirty(piece.entry);
            if (piece.exit != null) EditorUtility.SetDirty(piece.exit);
            if (piece.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(piece.gameObject.scene);
        }
    }
}
