using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ConveyorSystem.EditorTools
{
    /// <summary>
    /// Tools > Conveyor System > Setup Wizard.
    /// Turns the imported conveyor / product models into ready-to-use prefabs and
    /// builds a complete, playable scene (camera, light, floor, managers, UI).
    /// </summary>
    public class ConveyorSetupWizard : EditorWindow
    {
        enum ModelKind { Straight, Curve90, Custom }

        [Serializable]
        class ModelEntry
        {
            public GameObject model;
            public ModelKind kind;
            public string displayName;
            public Vector3 size;
            public bool autoOrient = true;
            public Vector3 extraRotation;
        }

        const string OutputRoot = "Assets/ConveyorSystem/Generated";
        const string ScenePath = OutputRoot + "/ConveyorScene.unity";

        [SerializeField] List<ModelEntry> conveyors = new List<ModelEntry>();
        [SerializeField] List<ModelEntry> products = new List<ModelEntry>();
        [SerializeField] bool overwritePrefabs;
        [SerializeField] bool generateCurves = true;
        Vector2 scroll;

        [MenuItem("Tools/Conveyor System/Setup Wizard", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<ConveyorSetupWizard>("Conveyor Setup");
            window.minSize = new Vector2(460f, 520f);
        }

        [MenuItem("Tools/Conveyor System/Open Conveyor Scene", priority = 1)]
        public static void OpenScene()
        {
            if (!File.Exists(ScenePath)) { EditorUtility.DisplayDialog("Conveyor System", "Run the Setup Wizard first.", "OK"); return; }
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }

        // ---------------------------------------------------------------- GUI

        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);

            EditorGUILayout.LabelField("Conveyor System Setup", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "1) In the Project window, select the conveyor MODELS from the imported package (Ctrl-click to select several).\n" +
                "2) Click the matching 'Add selected' button below.\n" +
                "3) Select the product models and click 'Add selected products'.\n" +
                "4) Click 'Generate', then press Play.", MessageType.Info);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Conveyors", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add selected\nas Straight", GUILayout.Height(36))) AddSelected(conveyors, ModelKind.Straight);
            if (GUILayout.Button("Add selected\nas 90° Curve", GUILayout.Height(36))) AddSelected(conveyors, ModelKind.Curve90);
            if (GUILayout.Button("Add selected\nas Custom", GUILayout.Height(36))) AddSelected(conveyors, ModelKind.Custom);
            EditorGUILayout.EndHorizontal();
            DrawList(conveyors, true);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Products", EditorStyles.boldLabel);
            if (GUILayout.Button("Add selected products", GUILayout.Height(26))) AddSelected(products, ModelKind.Custom);
            DrawList(products, false);

            EditorGUILayout.Space();
            generateCurves = EditorGUILayout.ToggleLeft(
                new GUIContent("Also generate 90° curve conveyors (Left + Right)",
                    "The package has no corner piece, so build curves that match the straight belt's size and materials."),
                generateCurves);
            overwritePrefabs = EditorGUILayout.ToggleLeft(
                new GUIContent("Overwrite prefabs that already exist",
                    "Leave OFF if you hand-adjusted Entry/Exit points and only want to rebuild the scene."),
                overwritePrefabs);

            using (new EditorGUI.DisabledScope(conveyors.Count == 0))
            {
                // Run after this GUI pass finishes (creating scenes mid-GUI upsets the layout system).
                if (GUILayout.Button("Generate Prefabs + Build Scene", GUILayout.Height(40))) EditorApplication.delayCall += Generate;
            }

            EditorGUILayout.HelpBox(
                "Straight: a straight belt of any length (slopes work too).\n" +
                "90° Curve: one model becomes BOTH a left-turn and a right-turn piece.\n" +
                "Custom: anything else. Open its prefab and drag the green ENTRY and red EXIT points onto the belt ends yourself.",
                MessageType.None);

            EditorGUILayout.EndScrollView();
        }

        void DrawList(List<ModelEntry> list, bool showKind)
        {
            int remove = -1;
            for (int i = 0; i < list.Count; i++)
            {
                ModelEntry e = list[i];
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);

                EditorGUILayout.BeginHorizontal();
                GameObject newModel = (GameObject)EditorGUILayout.ObjectField(e.model, typeof(GameObject), false);
                if (newModel != e.model) { e.model = newModel; e.size = newModel != null ? MeasureAsset(newModel) : Vector3.zero; }
                if (showKind) e.kind = (ModelKind)EditorGUILayout.EnumPopup(e.kind, GUILayout.Width(80));
                if (GUILayout.Button("X", GUILayout.Width(22))) remove = i;
                EditorGUILayout.EndHorizontal();

                e.displayName = EditorGUILayout.TextField("Name", e.displayName);
                if (showKind)
                {
                    e.autoOrient = EditorGUILayout.ToggleLeft(
                        new GUIContent("Auto-orient (lay it flat if it is standing on its end)"), e.autoOrient);
                    e.extraRotation = EditorGUILayout.Vector3Field(
                        new GUIContent("Extra rotation (°)", "Only if it still comes out on its side / upside down, e.g. X = 90"),
                        e.extraRotation);
                }
                EditorGUILayout.LabelField(
                    string.Format("Size: {0:0.###} x {1:0.###} x {2:0.###} m", e.size.x, e.size.y, e.size.z),
                    EditorStyles.miniLabel);

                float biggest = Mathf.Max(e.size.x, Mathf.Max(e.size.y, e.size.z));
                if (biggest > 25f)
                    EditorGUILayout.HelpBox("This is very big. The model is probably in centimetres or millimetres. " +
                        "Select it in the Project window, lower 'Scale Factor' in the Inspector (Model tab), click Apply, then remove and re-add it here.",
                        MessageType.Warning);
                else if (biggest > 0f && biggest < 0.05f)
                    EditorGUILayout.HelpBox("This is very small. Increase 'Scale Factor' in the model's import settings.", MessageType.Warning);

                EditorGUILayout.EndVertical();
            }
            if (remove >= 0) list.RemoveAt(remove);
        }

        void AddSelected(List<ModelEntry> list, ModelKind kind)
        {
            int added = 0;
            foreach (UnityEngine.Object obj in Selection.objects)
            {
                var go = obj as GameObject;
                if (go == null || !EditorUtility.IsPersistent(go)) continue; // only assets from the Project window
                if (list.Exists(e => e.model == go && e.kind == kind)) continue;
                list.Add(new ModelEntry
                {
                    model = go,
                    kind = kind,
                    displayName = ObjectNames.NicifyVariableName(go.name),
                    size = MeasureAsset(go)
                });
                added++;
            }
            if (added == 0) ShowNotification(new GUIContent("Select model(s) in the Project window first"));
        }

        /// <summary>Approximate size of a model asset without putting it in the scene.</summary>
        static Vector3 MeasureAsset(GameObject asset) => MeasureAssetBounds(asset).size;

        static Bounds MeasureAssetBounds(GameObject asset)
        {
            Matrix4x4 toRoot = Matrix4x4.TRS(-asset.transform.position, Quaternion.identity, Vector3.one);
            bool found = false;
            Bounds total = new Bounds();
            foreach (MeshFilter mf in asset.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Bounds mb = mf.sharedMesh.bounds;
                Matrix4x4 m = toRoot * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = mb.center + Vector3.Scale(mb.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!found) { total = new Bounds(p, Vector3.zero); found = true; }
                    else total.Encapsulate(p);
                }
            }
            return found ? total : new Bounds(Vector3.zero, Vector3.zero);
        }

        // ---------------------------------------------------------------- generation

        void Generate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            EnsureFolder(OutputRoot);
            EnsureFolder(OutputRoot + "/Conveyors");
            EnsureFolder(OutputRoot + "/Products");

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var pieces = new List<ConveyorPiece>();
            var productPrefabs = new List<GameObject>();

            try
            {
                int step = 0;
                int total = Mathf.Max(1, conveyors.Count + products.Count);

                foreach (ModelEntry e in conveyors)
                {
                    EditorUtility.DisplayProgressBar("Conveyor Setup", "Creating " + e.displayName, (float)step++ / total);
                    if (e.model == null) continue;
                    switch (e.kind)
                    {
                        case ModelKind.Straight:
                            pieces.Add(CreateConveyorPrefab(e, ConveyorShape.Straight, e.displayName));
                            break;
                        case ModelKind.Curve90:
                            pieces.Add(CreateConveyorPrefab(e, ConveyorShape.CurveLeft, e.displayName + " (Left)"));
                            pieces.Add(CreateConveyorPrefab(e, ConveyorShape.CurveRight, e.displayName + " (Right)"));
                            break;
                        default:
                            pieces.Add(CreateConveyorPrefab(e, ConveyorShape.Custom, e.displayName));
                            break;
                    }
                }

                foreach (ModelEntry e in products)
                {
                    EditorUtility.DisplayProgressBar("Conveyor Setup", "Creating " + e.displayName, (float)step++ / total);
                    if (e.model == null) continue;
                    GameObject prefab = CreateProductPrefab(e.model, e.displayName);
                    if (prefab != null) productPrefabs.Add(prefab);
                }

                pieces.RemoveAll(p => p == null);
                if (generateCurves && !conveyors.Exists(c => c.kind == ModelKind.Curve90))
                    pieces.AddRange(GenerateCurves(pieces));
                pieces.RemoveAll(p => p == null);
                BuildScene(scene, pieces, productPrefabs);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorUtility.DisplayDialog("Conveyor System",
                "Done! Created " + pieces.Count + " conveyor prefabs and " + productPrefabs.Count + " product prefabs.\n\n" +
                "The scene was saved to " + ScenePath + " and added to Build Settings.\n\nPress Play to try it.",
                "Great");
        }

        ConveyorPiece CreateConveyorPrefab(ModelEntry entry, ConveyorShape shape, string displayName)
        {
            GameObject model = entry.model;
            string path = OutputRoot + "/Conveyors/" + MakeSafe(displayName) + ".prefab";
            if (!overwritePrefabs && File.Exists(path))
            {
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (existing != null && existing.GetComponent<ConveyorPiece>() != null) return existing.GetComponent<ConveyorPiece>();
            }

            var root = new GameObject("Conveyor_" + MakeSafe(displayName));
            GameObject instance = InstantiateModel(model);
            instance.transform.SetParent(root.transform, false);

            // Lay the model flat if it was exported standing on its end.
            if (entry.autoOrient)
                instance.transform.localRotation = ConveyorAutoFit.FindUprightRotation(root.transform) * instance.transform.localRotation;
            if (entry.extraRotation != Vector3.zero)
                instance.transform.localRotation = Quaternion.Euler(entry.extraRotation) * instance.transform.localRotation;

            // Colliders let the mouse pick conveyors and let fallen products land on them.
            foreach (MeshFilter mf in instance.GetComponentsInChildren<MeshFilter>(true))
                if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                    mf.gameObject.AddComponent<MeshCollider>();

            ConveyorPiece piece = root.AddComponent<ConveyorPiece>();
            piece.displayName = displayName;
            piece.shape = shape;
            ConveyorAutoFit.Fit(piece, shape);

            GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, path);
            DestroyImmediate(root);
            return asset != null ? asset.GetComponent<ConveyorPiece>() : null;
        }

        GameObject CreateProductPrefab(GameObject model, string displayName)
        {
            string path = OutputRoot + "/Products/" + MakeSafe(displayName) + ".prefab";
            if (!overwritePrefabs && File.Exists(path))
            {
                var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (existing != null) return existing;
            }

            var root = new GameObject("Product_" + MakeSafe(displayName));
            GameObject instance = InstantiateModel(model);
            instance.transform.SetParent(root.transform, false);

            Bounds b = ConveyorUtils.CalculateBounds(root);
            if (root.GetComponentInChildren<Collider>() == null)
            {
                var box = root.AddComponent<BoxCollider>();
                box.center = b.center;
                box.size = b.size;
            }

            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;

            var product = root.AddComponent<Product>();
            product.productName = displayName;

            GameObject asset = PrefabUtility.SaveAsPrefabAsset(root, path);
            DestroyImmediate(root);
            return asset;
        }

        /// <summary>Builds left/right curves sized from the flattest straight conveyor.</summary>
        List<ConveyorPiece> GenerateCurves(List<ConveyorPiece> pieces)
        {
            ConveyorPiece straight = null;
            float bestSlope = float.MaxValue;
            foreach (ConveyorPiece p in pieces)
            {
                if (p.shape != ConveyorShape.Straight || p.entry == null || p.exit == null) continue;
                float slope = Mathf.Abs(p.entry.localPosition.y - p.exit.localPosition.y);
                if (slope < bestSlope) { bestSlope = slope; straight = p; }
            }
            if (straight == null) return new List<ConveyorPiece>();

            Bounds b = MeasureAssetBounds(straight.gameObject);
            Vector3 forward = straight.entry.localRotation * Vector3.forward;
            float width = Mathf.Abs(forward.z) >= Mathf.Abs(forward.x) ? b.size.x : b.size.z;
            float height = straight.entry.localPosition.y - b.min.y;
            if (width <= 0.001f) width = 1f;
            if (height <= 0.001f) height = width * 0.5f;

            // Darkest material for the belt, lightest for rails and posts.
            var materials = new List<Material>();
            foreach (Renderer r in straight.GetComponentsInChildren<Renderer>(true))
                foreach (Material m in r.sharedMaterials)
                    if (m != null && !materials.Contains(m)) materials.Add(m);
            Material belt = null, frame = null;
            float darkest = float.MaxValue, lightest = float.MinValue;
            foreach (Material m in materials)
            {
                float lum = Brightness(m);
                if (lum < darkest) { darkest = lum; belt = m; }
                if (lum > lightest) { lightest = lum; frame = m; }
            }

            return CurveGenerator.Create(width, height, belt, frame, OutputRoot + "/Conveyors", overwritePrefabs);
        }

        static float Brightness(Material m)
        {
            Color c = Color.white;
            if (m.HasProperty("_BaseColor")) c = m.GetColor("_BaseColor");
            else if (m.HasProperty("_Color")) c = m.GetColor("_Color");
            return c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
        }

        void BuildScene(Scene scene, List<ConveyorPiece> pieces, List<GameObject> productPrefabs)
        {
            float typical = TypicalSize(pieces);

            // Camera (created by NewSceneSetup.DefaultGameObjects)
            Camera cam = null;
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                cam = go.GetComponent<Camera>();
                if (cam != null) break;
            }
            if (cam == null)
            {
                cam = new GameObject("Main Camera").AddComponent<Camera>();
                cam.tag = "MainCamera";
            }
            cam.nearClipPlane = Mathf.Max(0.01f, typical * 0.01f);
            cam.farClipPlane = Mathf.Max(1000f, typical * 300f);
            var camController = cam.gameObject.AddComponent<CameraController>();
            camController.distance = typical * 6f;
            camController.minDistance = typical * 0.4f;
            camController.maxDistance = typical * 60f;

            // Floor
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            float groundScale = Mathf.Max(10f, typical * 10f);
            ground.transform.localScale = new Vector3(groundScale, 1f, groundScale);
            Renderer groundRenderer = ground.GetComponent<Renderer>();
            Material groundMat = CreateGroundMaterial(groundRenderer.sharedMaterial);
            if (groundMat != null) groundRenderer.sharedMaterial = groundMat;

            // Managers + UI
            var system = new GameObject("Conveyor System");
            var sim = system.AddComponent<SimulationManager>();
            sim.productPrefabs = productPrefabs;
            sim.pathMaterial = CreatePathMaterial();
            sim.beltSpeed = Mathf.Clamp(typical * 0.4f, 0.2f, 3f);
            sim.productGap = typical * 0.05f;

            var builder = system.AddComponent<ConveyorBuilder>();
            builder.conveyorPrefabs = pieces;
            builder.cam = cam;
            builder.gridSize = Mathf.Max(0.05f, typical / 4f);
            builder.snapRadius = typical * 0.75f;

            system.AddComponent<BuilderUI>();

            EditorSceneManager.SaveScene(scene, ScenePath);

            // Make it the first scene in the build.
            var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                if (s.path != ScenePath) scenes.Add(new EditorBuildSettingsScene(s.path, false));
            EditorBuildSettings.scenes = scenes.ToArray();

            Selection.activeGameObject = system;
        }

        static float TypicalSize(List<ConveyorPiece> pieces)
        {
            ConveyorPiece reference = pieces.Find(p => p.shape == ConveyorShape.Straight);
            if (reference == null && pieces.Count > 0) reference = pieces[0];
            if (reference == null) return 1f;
            Vector3 size = MeasureAsset(reference.gameObject);
            float horizontal = Mathf.Max(size.x, size.z);
            return horizontal > 0.01f ? horizontal : 1f;
        }

        static Material CreatePathMaterial()
        {
            string path = OutputRoot + "/FlowPath.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) return null;

            var mat = new Material(shader) { name = "FlowPath" };
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static Material CreateGroundMaterial(Material source)
        {
            if (source == null) return null;
            string path = OutputRoot + "/Ground.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            var mat = new Material(source) { name = "Ground" };
            Color grey = new Color(0.32f, 0.34f, 0.37f);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", grey);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", grey);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static GameObject InstantiateModel(GameObject model)
        {
            var instance = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (instance == null) instance = Instantiate(model);
            return instance;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static string MakeSafe(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "Unnamed";
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Replace(' ', '_');
        }
    }
}
