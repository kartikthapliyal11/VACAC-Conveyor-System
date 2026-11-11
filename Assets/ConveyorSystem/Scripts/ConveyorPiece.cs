using System.Collections.Generic;
using UnityEngine;

namespace ConveyorSystem
{
    public enum ConveyorShape { Straight, CurveLeft, CurveRight, Custom }

    /// <summary>
    /// One conveyor section. Products travel from <see cref="entry"/> to <see cref="exit"/>
    /// along a smooth path. Two pieces are connected when one piece's exit sits exactly
    /// on another piece's entry and both point the same way.
    /// </summary>
    [DisallowMultipleComponent]
    public class ConveyorPiece : MonoBehaviour
    {
        /// <summary>Every conveyor currently placed in the world (ghost previews are not included).</summary>
        public static readonly List<ConveyorPiece> Placed = new List<ConveyorPiece>();

        const float ConnectTolerance = 0.02f;

        // Clears static state when entering Play Mode with "domain reload" turned off.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { Placed.Clear(); }

        [Header("Info")]
        public string displayName = "Conveyor";
        public ConveyorShape shape = ConveyorShape.Straight;

        [Header("Snap points: blue (Z) arrow = direction products travel")]
        [Tooltip("Where products get on. Sits on the belt surface at the start of the piece.")]
        public Transform entry;
        [Tooltip("Where products get off. Sits on the belt surface at the end of the piece.")]
        public Transform exit;

        [Header("Path")]
        [Range(2, 64)] public int pathResolution = 24;

        [HideInInspector] public int catalogIndex = -1;
        [HideInInspector] public bool isGhost;

        public ConveyorPiece Next { get; private set; }
        public ConveyorPiece Previous { get; private set; }
        public float Length { get; private set; }
        public bool IsPlaced { get; private set; }

        /// <summary>Products on this piece, ordered from the entry (index 0) towards the exit.</summary>
        public readonly List<Product> Products = new List<Product>();

        Vector3[] points;
        float[] cumulative;
        LineRenderer pathLine;

        // ------------------------------------------------------------------ lifecycle

        public void MarkPlaced(int index)
        {
            catalogIndex = index;
            isGhost = false;
            IsPlaced = true;
            if (!Placed.Contains(this)) Placed.Add(this);
            RebuildPath();
        }

        /// <summary>Removes this piece from the world list. Products riding on it fall off.</summary>
        public void Unregister(bool dropProducts)
        {
            IsPlaced = false;
            Placed.Remove(this);
            if (dropProducts)
            {
                for (int i = Products.Count - 1; i >= 0; i--)
                    if (Products[i] != null) Products[i].Drop(false);
            }
            Products.Clear();
        }

        void OnDestroy()
        {
            IsPlaced = false;
            Placed.Remove(this);
        }

        // ------------------------------------------------------------------ path

        /// <summary>
        /// Builds a cubic Bezier from entry to exit. Straight pieces become a straight line,
        /// 90 degree pieces become (almost exactly) a circular arc, ramps become slopes.
        /// </summary>
        public void RebuildPath()
        {
            if (entry == null || exit == null) { Length = 0f; points = null; return; }

            Vector3 p0 = entry.position, p3 = exit.position;
            Vector3 d0 = entry.forward, d3 = exit.forward;
            float chord = Vector3.Distance(p0, p3);
            float angle = Vector3.Angle(d0, d3) * Mathf.Deg2Rad;

            // Handle length that makes a Bezier match a circular arc of the given angle.
            float handle = angle < 0.01f
                ? chord / 3f
                : 4f / 3f * Mathf.Tan(angle / 4f) * chord / (2f * Mathf.Sin(angle / 2f));

            Vector3 p1 = p0 + d0 * handle;
            Vector3 p2 = p3 - d3 * handle;

            int n = Mathf.Max(2, pathResolution);
            if (points == null || points.Length != n + 1)
            {
                points = new Vector3[n + 1];
                cumulative = new float[n + 1];
            }

            for (int i = 0; i <= n; i++)
            {
                float t = (float)i / n, u = 1f - t;
                points[i] = u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
                cumulative[i] = i == 0 ? 0f : cumulative[i - 1] + Vector3.Distance(points[i - 1], points[i]);
            }

            Length = cumulative[n];
            if (pathLine != null) UpdatePathLine();
        }

        /// <summary>Position and travel direction at <paramref name="distance"/> metres along the belt.</summary>
        public void Sample(float distance, out Vector3 position, out Vector3 forward)
        {
            if (points == null) RebuildPath();
            if (points == null)
            {
                position = transform.position;
                forward = transform.forward;
                return;
            }

            distance = Mathf.Clamp(distance, 0f, Length);
            int lo = 0, hi = cumulative.Length - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) / 2;
                if (cumulative[mid] < distance) lo = mid; else hi = mid;
            }

            float segment = cumulative[hi] - cumulative[lo];
            float t = segment > 1e-6f ? (distance - cumulative[lo]) / segment : 0f;
            position = Vector3.Lerp(points[lo], points[hi], t);
            forward = points[hi] - points[lo];
            if (forward.sqrMagnitude < 1e-8f) forward = exit.forward;
            forward.Normalize();
        }

        // ------------------------------------------------------------------ snapping

        public static float Yaw(Vector3 direction) => Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        /// <summary>
        /// Works out where this piece must go so that its <paramref name="ownPoint"/>
        /// (entry or exit) lands exactly on <paramref name="target"/>, facing the same way.
        /// </summary>
        public void GetSnapPose(Transform ownPoint, Transform target, out Vector3 position, out Quaternion rotation)
        {
            Vector3 local = transform.InverseTransformPoint(ownPoint.position);
            float localYaw = Yaw(transform.InverseTransformDirection(ownPoint.forward));
            rotation = Quaternion.Euler(0f, Yaw(target.forward) - localYaw, 0f);
            position = target.position - rotation * Vector3.Scale(local, transform.lossyScale);
        }

        /// <summary>Re-links every placed piece to its neighbours. Cheap enough for hundreds of pieces.</summary>
        public static void RefreshConnections()
        {
            foreach (ConveyorPiece p in Placed) { p.Next = null; p.Previous = null; }

            foreach (ConveyorPiece a in Placed)
            {
                if (a.exit == null) continue;
                foreach (ConveyorPiece b in Placed)
                {
                    if (a == b || a.Next != null || b.Previous != null || b.entry == null) continue;
                    bool samePlace = (a.exit.position - b.entry.position).sqrMagnitude <= ConnectTolerance * ConnectTolerance;
                    bool sameWay = Vector3.Dot(a.exit.forward, b.entry.forward) > 0.9f;
                    if (samePlace && sameWay)
                    {
                        a.Next = b;
                        b.Previous = a;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ visuals

        /// <summary>Shows a coloured line along the belt (green = start, red = end).</summary>
        public void SetPathVisible(bool visible, Material material)
        {
            if (visible && pathLine == null && material != null)
            {
                var go = new GameObject("FlowPath");
                go.transform.SetParent(transform, false);
                pathLine = go.AddComponent<LineRenderer>();
                pathLine.sharedMaterial = material;
                pathLine.useWorldSpace = true;
                pathLine.startColor = new Color(0.2f, 1f, 0.4f);
                pathLine.endColor = new Color(1f, 0.3f, 0.2f);
                pathLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pathLine.receiveShadows = false;
                UpdatePathLine();
            }
            if (pathLine != null) pathLine.enabled = visible;
        }

        void UpdatePathLine()
        {
            if (points == null) { pathLine.positionCount = 0; return; }
            pathLine.widthMultiplier = Mathf.Clamp(Length * 0.025f, 0.02f, 0.2f);
            pathLine.positionCount = points.Length;
            Vector3 lift = Vector3.up * pathLine.widthMultiplier;
            for (int i = 0; i < points.Length; i++) pathLine.SetPosition(i, points[i] + lift);
        }

        [ContextMenu("Reverse Direction (swap Entry and Exit)")]
        void ReverseDirection()
        {
            if (entry == null || exit == null) return;
#if UNITY_EDITOR
            UnityEditor.Undo.RecordObjects(new Object[] { this, entry, exit, entry.gameObject, exit.gameObject }, "Reverse conveyor");
#endif
            Transform oldEntry = entry;
            entry = exit;
            exit = oldEntry;
            entry.Rotate(0f, 180f, 0f, Space.World);
            exit.Rotate(0f, 180f, 0f, Space.World);
            entry.name = "Entry";
            exit.name = "Exit";
        }

        void OnDrawGizmos()
        {
            if (entry == null || exit == null) return;
            if (!Application.isPlaying) RebuildPath();
            float s = Mathf.Clamp(Length * 0.04f, 0.03f, 0.3f);

            Gizmos.color = Color.green;
            Gizmos.DrawSphere(entry.position, s);
            Gizmos.DrawLine(entry.position, entry.position + entry.forward * s * 4f);

            Gizmos.color = Color.red;
            Gizmos.DrawSphere(exit.position, s);
            Gizmos.DrawLine(exit.position, exit.position + exit.forward * s * 4f);

            if (points != null)
            {
                Gizmos.color = Color.yellow;
                for (int i = 1; i < points.Length; i++) Gizmos.DrawLine(points[i - 1], points[i]);
            }
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.Handles.Label(entry.position + Vector3.up * s * 2f, "ENTRY");
                UnityEditor.Handles.Label(exit.position + Vector3.up * s * 2f, "EXIT");
            }
#endif
        }
    }
}
