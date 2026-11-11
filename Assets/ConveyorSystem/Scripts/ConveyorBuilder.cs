using System.Collections.Generic;
using UnityEngine;

namespace ConveyorSystem
{
    /// <summary>
    /// Build mode: pick a conveyor, preview it under the mouse ("ghost"), snap it to
    /// open ends of existing conveyors, place, move and delete pieces.
    /// </summary>
    public class ConveyorBuilder : MonoBehaviour
    {
        public static ConveyorBuilder Instance { get; private set; }

        [Header("Catalog (filled by the Setup Wizard)")]
        public List<ConveyorPiece> conveyorPrefabs = new List<ConveyorPiece>();
        public Camera cam;

        [Header("Placement")]
        public bool useGrid = true;
        public float gridSize = 0.25f;
        [Tooltip("How close (metres) the mouse must be to snap to an open conveyor end")]
        public float snapRadius = 1f;
        public bool buildDemoOnStart = true;

        [Header("Colours")]
        public Color validColor = new Color(0.45f, 1f, 0.55f);
        public Color invalidColor = new Color(1f, 0.35f, 0.35f);
        public Color hoverColor = new Color(1f, 0.85f, 0.35f);

        public int SelectedIndex { get; private set; } = -1;
        public bool IsSnapped => snapTarget != null;
        public string Status => Time.time < statusUntil ? status : "";
        public bool StatusIsError { get; private set; }

        static readonly Plane Ground = new Plane(Vector3.up, Vector3.zero);

        ConveyorPiece ghost, snapTarget, hovered;
        MeshRenderer[] ghostRenderers;
        Bounds ghostLocalBounds;
        float ghostYaw;
        bool ghostValid;

        string status = "";
        float statusUntil;

        MaterialPropertyBlock block;
        int baseColorId, colorId;
        readonly Collider[] overlapBuffer = new Collider[64];
        readonly RaycastHit[] rayBuffer = new RaycastHit[32];

        void Awake()
        {
            Instance = this;
            block = new MaterialPropertyBlock();
            baseColorId = Shader.PropertyToID("_BaseColor"); // URP / HDRP
            colorId = Shader.PropertyToID("_Color");         // Built-in pipeline
            if (cam == null) cam = Camera.main;
        }

        void Start()
        {
            if (buildDemoOnStart) BuildDemoLine();
            else SetStatus("Pick a conveyor on the left (or press 1-9) to start building.");
        }

        void Update()
        {
            if (cam == null) { cam = Camera.main; if (cam == null) return; }

            HandleKeys();
            bool overUI = BuilderUI.IsPointerOverUI;
            Ray ray = cam.ScreenPointToRay(InputHelper.MousePosition);

            if (ghost != null) UpdateGhost(ray, overUI);
            else UpdateHover(ray, overUI);
        }

        void HandleKeys()
        {
            int count = Mathf.Min(9, conveyorPrefabs.Count);
            for (int i = 0; i < count; i++)
                if (InputHelper.KeyDown(KeyCode.Alpha1 + i)) SelectConveyor(i);

            if (InputHelper.KeyDown(KeyCode.R)) RotateGhost(InputHelper.Shift ? -90f : 90f);
            if (InputHelper.KeyDown(KeyCode.G)) useGrid = !useGrid;
            if (InputHelper.KeyDown(KeyCode.Escape)) CancelPlacement();
        }

        // ------------------------------------------------------------------ selecting

        public void SelectConveyor(int index)
        {
            if (index < 0 || index >= conveyorPrefabs.Count || conveyorPrefabs[index] == null) return;
            if (index == SelectedIndex && ghost != null) { CancelPlacement(); return; }
            CreateGhost(index);
        }

        public void RotateGhost(float degrees)
        {
            ghostYaw = Mathf.Repeat(ghostYaw + degrees, 360f);
        }

        public void CancelPlacement()
        {
            DestroyGhost();
            SelectedIndex = -1;
            snapTarget = null;
        }

        void CreateGhost(int index)
        {
            DestroyGhost();
            ClearHover();
            SelectedIndex = index;

            ghost = Instantiate(conveyorPrefabs[index], Vector3.zero, Quaternion.identity);
            ghost.name = "Ghost - " + conveyorPrefabs[index].displayName;
            ghost.isGhost = true;
            foreach (Collider c in ghost.GetComponentsInChildren<Collider>()) c.enabled = false;

            // The ghost sits at the origin unrotated, so world bounds == local bounds.
            ghostLocalBounds = ConveyorUtils.CalculateBounds(ghost.gameObject);
            ghostRenderers = ghost.GetComponentsInChildren<MeshRenderer>();
            SetStatus("Move the mouse to an open conveyor end to snap. Click to place, R to rotate, right-click to stop.");
        }

        void DestroyGhost()
        {
            if (ghost != null) Destroy(ghost.gameObject);
            ghost = null;
            ghostRenderers = null;
        }

        // ------------------------------------------------------------------ ghost / placing

        void UpdateGhost(Ray ray, bool overUI)
        {
            if (!Ground.Raycast(ray, out float enter)) return;
            Vector3 mouse = ray.GetPoint(enter);

            // Free placement on the floor, centred on the mouse.
            Quaternion rot = Quaternion.Euler(0f, ghostYaw, 0f);
            Vector3 centre = ghostLocalBounds.center;
            centre.y = 0f;
            Vector3 pos = mouse - rot * centre;
            if (useGrid && gridSize > 0f)
            {
                pos.x = Mathf.Round(pos.x / gridSize) * gridSize;
                pos.z = Mathf.Round(pos.z / gridSize) * gridSize;
            }
            pos.y = -ghostLocalBounds.min.y;

            // Snap to the nearest open end (hold Shift to place freely).
            snapTarget = null;
            if (!InputHelper.Shift) FindSnap(mouse, ref pos, ref rot);

            ghost.transform.SetPositionAndRotation(pos, rot);
            ghostValid = !OverlapsPlacedPiece(pos, rot);
            Tint(ghostRenderers, ghostValid ? validColor : invalidColor);

            if (overUI || InputHelper.Alt) return;

            if (InputHelper.LeftDown)
            {
                if (ghostValid) PlaceGhost();
                else SetStatus("Can't place here: it overlaps another conveyor.", true);
            }
            else if (InputHelper.RightDown)
            {
                CancelPlacement();
            }
        }

        void FindSnap(Vector3 mouse, ref Vector3 pos, ref Quaternion rot)
        {
            float reach = Mathf.Max(snapRadius, Mathf.Max(ghostLocalBounds.extents.x, ghostLocalBounds.extents.z));
            float best = reach * reach;
            Vector3 centre = ghostLocalBounds.center;
            bool found = false;
            Vector3 bestPos = pos;
            Quaternion bestRot = rot;

            foreach (ConveyorPiece other in ConveyorPiece.Placed)
            {
                for (int k = 0; k < 2; k++)
                {
                    Transform own, target;
                    if (k == 0)
                    {
                        if (other.Next != null) continue;      // other's exit is already used
                        own = ghost.entry; target = other.exit; // ghost continues the line
                    }
                    else
                    {
                        if (other.Previous != null) continue;  // other's entry is already used
                        own = ghost.exit; target = other.entry; // ghost feeds into the line
                    }
                    if (own == null || target == null) continue;

                    ghost.GetSnapPose(own, target, out Vector3 p, out Quaternion r);
                    Vector3 c = p + r * centre;
                    float d = new Vector2(c.x - mouse.x, c.z - mouse.z).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        bestPos = p;
                        bestRot = r;
                        snapTarget = other;
                        found = true;
                    }
                }
            }

            if (found)
            {
                pos = bestPos;
                rot = bestRot;
            }
        }

        bool OverlapsPlacedPiece(Vector3 pos, Quaternion rot)
        {
            Vector3 centre = pos + rot * ghostLocalBounds.center;
            Vector3 half = ghostLocalBounds.extents * 0.8f; // shrink so touching neighbours are fine
            half.y = Mathf.Max(0.01f, half.y);

            int count = Physics.OverlapBoxNonAlloc(centre, half, overlapBuffer, rot, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                ConveyorPiece piece = overlapBuffer[i].GetComponentInParent<ConveyorPiece>();
                if (piece != null && piece.IsPlaced && piece != ghost && piece != snapTarget) return true;
            }
            return false;
        }

        void PlaceGhost()
        {
            bool snapped = snapTarget != null;
            ConveyorPiece piece = SpawnPiece(SelectedIndex, ghost.transform.position, ghost.transform.rotation);
            ghostYaw = ghost.transform.eulerAngles.y; // keep building in the same direction
            snapTarget = null;
            SetStatus("Placed " + piece.displayName + (snapped ? " (snapped)" : ""));
        }

        /// <summary>Creates a real conveyor in the world.</summary>
        public ConveyorPiece SpawnPiece(int index, Vector3 position, Quaternion rotation, bool refresh = true)
        {
            ConveyorPiece piece = Instantiate(conveyorPrefabs[index], position, rotation);
            piece.name = conveyorPrefabs[index].displayName;
            piece.MarkPlaced(index);

            SimulationManager sim = SimulationManager.Instance;
            if (sim != null) piece.SetPathVisible(sim.ShowPaths, sim.pathMaterial);
            if (refresh) ConveyorPiece.RefreshConnections();
            return piece;
        }

        // ------------------------------------------------------------------ hover / edit

        void UpdateHover(Ray ray, bool overUI)
        {
            ConveyorPiece target = overUI ? null : RaycastPiece(ray);
            if (target != hovered)
            {
                ClearHover();
                hovered = target;
                if (hovered != null) Tint(hovered.GetComponentsInChildren<MeshRenderer>(), hoverColor);
            }

            if (hovered == null || InputHelper.Alt) return;

            if (InputHelper.LeftDown)
                PickUp(hovered);
            else if (InputHelper.RightDown || InputHelper.KeyDown(KeyCode.Delete) ||
                     InputHelper.KeyDown(KeyCode.X) || InputHelper.KeyDown(KeyCode.Backspace))
                DeletePiece(hovered);
        }

        ConveyorPiece RaycastPiece(Ray ray)
        {
            int count = Physics.RaycastNonAlloc(ray, rayBuffer, 2000f, ~0, QueryTriggerInteraction.Ignore);
            ConveyorPiece best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                ConveyorPiece piece = rayBuffer[i].collider.GetComponentInParent<ConveyorPiece>();
                if (piece != null && piece.IsPlaced && rayBuffer[i].distance < bestDistance)
                {
                    best = piece;
                    bestDistance = rayBuffer[i].distance;
                }
            }
            return best;
        }

        void ClearHover()
        {
            if (hovered != null) Tint(hovered.GetComponentsInChildren<MeshRenderer>(), null);
            hovered = null;
        }

        void PickUp(ConveyorPiece piece)
        {
            int index = piece.catalogIndex;
            float yaw = piece.transform.eulerAngles.y;
            DeletePiece(piece);
            if (index < 0 || index >= conveyorPrefabs.Count) return;
            ghostYaw = yaw;
            CreateGhost(index);
            SetStatus("Moving " + piece.displayName + ". Click to drop it somewhere else.");
        }

        public void DeletePiece(ConveyorPiece piece)
        {
            if (piece == hovered) hovered = null;
            piece.Unregister(true);
            Destroy(piece.gameObject);
            ConveyorPiece.RefreshConnections();
        }

        public void ClearAll()
        {
            CancelPlacement();
            ClearHover();
            for (int i = ConveyorPiece.Placed.Count - 1; i >= 0; i--)
            {
                ConveyorPiece p = ConveyorPiece.Placed[i];
                p.Unregister(false);
                Destroy(p.gameObject);
            }
            if (SimulationManager.Instance != null) SimulationManager.Instance.ClearProducts();
        }

        // ------------------------------------------------------------------ save / load / demo

        public void SaveLayout()
        {
            if (LayoutStorage.Save()) SetStatus("Layout saved to " + LayoutStorage.FilePath);
            else SetStatus("Could not save layout.", true);
        }

        public void LoadLayout()
        {
            LayoutRecord data = LayoutStorage.Load();
            if (data == null) { SetStatus("No saved layout found yet. Press Save first.", true); return; }

            ClearAll();
            foreach (PieceRecord record in data.pieces)
            {
                if (record.type < 0 || record.type >= conveyorPrefabs.Count || conveyorPrefabs[record.type] == null) continue;
                SpawnPiece(record.type, record.position, record.rotation, false);
            }
            Physics.SyncTransforms();
            ConveyorPiece.RefreshConnections();
            FrameAll();
            SetStatus("Loaded " + data.pieces.Count + " conveyors.");
        }

        /// <summary>Builds an example line using snapping, so there is something to look at straight away.</summary>
        public void BuildDemoLine()
        {
            int s = FindShape(ConveyorShape.Straight);
            if (s < 0) s = conveyorPrefabs.FindIndex(p => p != null);
            if (s < 0) { SetStatus("No conveyors in the catalog. Run Tools > Conveyor System > Setup Wizard.", true); return; }
            int right = FindShape(ConveyorShape.CurveRight);
            int left = FindShape(ConveyorShape.CurveLeft);

            var sequence = new List<int> { s, s, s };
            if (right >= 0) sequence.AddRange(new[] { right, s, s, right, s, s, s });
            if (left >= 0) sequence.AddRange(new[] { left, s, left, s, s });
            if (right < 0 && left < 0) sequence.AddRange(new[] { s, s, s });

            ClearAll();

            ConveyorPiece previous = SpawnPiece(sequence[0], Vector3.zero, Quaternion.identity, false);
            Bounds b = ConveyorUtils.CalculateBounds(previous.gameObject);
            previous.transform.position += Vector3.up * -b.min.y; // stand it on the floor
            previous.RebuildPath();

            for (int i = 1; i < sequence.Count; i++)
            {
                if (previous.exit == null) break;
                ConveyorPiece piece = SpawnPiece(sequence[i], Vector3.zero, Quaternion.identity, false);
                if (piece.entry == null) break;
                piece.GetSnapPose(piece.entry, previous.exit, out Vector3 p, out Quaternion r);
                piece.transform.SetPositionAndRotation(p, r);
                piece.RebuildPath();
                previous = piece;
            }

            Physics.SyncTransforms();
            ConveyorPiece.RefreshConnections();
            FrameAll();
            SetStatus("Demo line built. Pick a conveyor on the left to extend it!");
        }

        int FindShape(ConveyorShape shape)
        {
            if (shape != ConveyorShape.Straight)
                return conveyorPrefabs.FindIndex(p => p != null && p.shape == shape);

            // For straights prefer the flattest one, so the demo doesn't use an incline.
            int best = -1;
            float bestSlope = float.MaxValue;
            for (int i = 0; i < conveyorPrefabs.Count; i++)
            {
                ConveyorPiece p = conveyorPrefabs[i];
                if (p == null || p.shape != ConveyorShape.Straight || p.entry == null || p.exit == null) continue;
                float slope = Mathf.Abs(p.entry.position.y - p.exit.position.y);
                if (slope < bestSlope) { bestSlope = slope; best = i; }
            }
            return best;
        }

        void FrameAll()
        {
            if (CameraController.Instance == null || ConveyorPiece.Placed.Count == 0) return;
            Bounds all = ConveyorUtils.CalculateBounds(ConveyorPiece.Placed[0].gameObject);
            foreach (ConveyorPiece p in ConveyorPiece.Placed) all.Encapsulate(ConveyorUtils.CalculateBounds(p.gameObject));
            CameraController.Instance.Frame(all);
        }

        // ------------------------------------------------------------------ helpers

        public void SetStatus(string message, bool isError = false)
        {
            status = message;
            StatusIsError = isError;
            statusUntil = Time.time + 5f;
        }

        void Tint(MeshRenderer[] renderers, Color? color)
        {
            if (renderers == null) return;
            foreach (MeshRenderer r in renderers)
            {
                if (r == null) continue;
                if (color.HasValue)
                {
                    block.Clear();
                    block.SetColor(baseColorId, color.Value);
                    block.SetColor(colorId, color.Value);
                    r.SetPropertyBlock(block);
                }
                else
                {
                    r.SetPropertyBlock(null);
                }
            }
        }
    }
}
