using UnityEngine;

namespace ConveyorSystem
{
    /// <summary>
    /// On-screen control panel drawn with Unity's immediate-mode GUI, so it needs no
    /// Canvas, EventSystem or TextMeshPro setup. Press H to hide it (for clean video).
    /// </summary>
    public class BuilderUI : MonoBehaviour
    {
        const float PanelWidth = 290f;
        const float Margin = 10f;

        const string HelpText =
            "BUILD\n" +
            "• 1-9 or buttons: choose conveyor\n" +
            "• Left click: place (or pick up a placed one)\n" +
            "• R / Shift+R: rotate\n" +
            "• Hold Shift: place without snapping\n" +
            "• G: toggle grid\n" +
            "• Right click: stop placing / delete hovered\n" +
            "• Delete or X: delete hovered\n" +
            "• Esc: stop placing\n\n" +
            "CAMERA\n" +
            "• WASD / arrows: pan\n" +
            "• Q / E: rotate\n" +
            "• Mouse wheel: zoom\n" +
            "• Middle drag (or Alt+Left drag): orbit\n" +
            "• Shift+Middle drag: pan\n" +
            "• F: reset view\n\n" +
            "SIMULATION\n" +
            "• Space: play / pause\n" +
            "• H: hide this panel";

        static BuilderUI instance;

        public bool visible = true;
        public bool showHelp = true;

        Vector2 scroll;
        GUIStyle titleStyle, headerStyle, statusStyle, smallStyle;
        string[] productNames;

        float Scale => Mathf.Clamp(Screen.height / 900f, 0.8f, 2f);

        /// <summary>True when the mouse is over the panel, so clicks don't place conveyors.</summary>
        public static bool IsPointerOverUI
        {
            get
            {
                if (instance == null || !instance.isActiveAndEnabled || !instance.visible) return false;
                return InputHelper.MousePosition.x <= (PanelWidth + Margin * 2f) * instance.Scale;
            }
        }

        void Awake() { instance = this; }

        void Update()
        {
            if (InputHelper.KeyDown(KeyCode.H)) visible = !visible;
        }

        void OnGUI()
        {
            if (!visible) return;
            ConveyorBuilder builder = ConveyorBuilder.Instance;
            SimulationManager sim = SimulationManager.Instance;
            if (builder == null || sim == null) return;

            EnsureStyles();
            float s = Scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
            float height = Screen.height / s - Margin * 2f;

            GUILayout.BeginArea(new Rect(Margin, Margin, PanelWidth, height), GUI.skin.box);
            scroll = GUILayout.BeginScrollView(scroll);

            GUILayout.Label("Conveyor Builder", titleStyle);

            // 1. Conveyors ------------------------------------------------------
            GUILayout.Label("1. Choose a conveyor", headerStyle);
            for (int i = 0; i < builder.conveyorPrefabs.Count; i++)
            {
                ConveyorPiece prefab = builder.conveyorPrefabs[i];
                if (prefab == null) continue;
                bool selected = builder.SelectedIndex == i;
                string label = (i < 9 ? "[" + (i + 1) + "]  " : "") + prefab.displayName;
                if (GUILayout.Toggle(selected, label, GUI.skin.button) != selected) builder.SelectConveyor(i);
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Rotate [R]")) builder.RotateGhost(90f);
            if (GUILayout.Button("Stop [Esc]")) builder.CancelPlacement();
            GUILayout.EndHorizontal();
            builder.useGrid = GUILayout.Toggle(builder.useGrid, " Snap to grid [G]");

            // 2. Products -------------------------------------------------------
            GUILayout.Space(6);
            GUILayout.Label("2. Products", headerStyle);
            if (sim.productPrefabs.Count == 0)
            {
                GUILayout.Label("No products set up. Add them in the Setup Wizard.", smallStyle);
            }
            else
            {
                RefreshProductNames(sim);
                int current = Mathf.Clamp(sim.selectedProduct + 1, 0, productNames.Length - 1);
                sim.selectedProduct = GUILayout.SelectionGrid(current, productNames, 2) - 1;
            }

            // 3. Simulation -----------------------------------------------------
            GUILayout.Space(6);
            GUILayout.Label("3. Simulation", headerStyle);
            if (GUILayout.Button(sim.isRunning ? "Pause  [Space]" : "Play  [Space]")) sim.isRunning = !sim.isRunning;
            GUILayout.Label("Belt speed: " + sim.beltSpeed.ToString("0.00") + " m/s");
            sim.beltSpeed = GUILayout.HorizontalSlider(sim.beltSpeed, 0.05f, 3f);
            GUILayout.Label("New product every: " + sim.spawnInterval.ToString("0.0") + " s");
            sim.spawnInterval = GUILayout.HorizontalSlider(sim.spawnInterval, 0.3f, 8f);
            bool paths = GUILayout.Toggle(sim.ShowPaths, " Show flow paths");
            if (paths != sim.ShowPaths) sim.ShowPaths = paths;
            if (GUILayout.Button("Clear products")) sim.ClearProducts();

            // 4. Layout ---------------------------------------------------------
            GUILayout.Space(6);
            GUILayout.Label("4. Layout", headerStyle);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Save")) builder.SaveLayout();
            if (GUILayout.Button("Load")) builder.LoadLayout();
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Demo line")) builder.BuildDemoLine();
            if (GUILayout.Button("Clear all")) builder.ClearAll();
            GUILayout.EndHorizontal();

            // Stats -------------------------------------------------------------
            GUILayout.Space(6);
            GUILayout.Label("Stats", headerStyle);
            int lineStarts = 0;
            foreach (ConveyorPiece p in ConveyorPiece.Placed) if (p.Previous == null) lineStarts++;
            GUILayout.Label(
                "Conveyors placed: " + ConveyorPiece.Placed.Count +
                "\nLine starts (spawn points): " + lineStarts +
                "\nProducts on belts: " + sim.ProductsOnBelts +
                "\nSpawned: " + sim.SpawnedCount + "    Delivered: " + sim.DeliveredCount, smallStyle);

            string status = builder.Status;
            if (!string.IsNullOrEmpty(status))
            {
                statusStyle.normal.textColor = builder.StatusIsError ? new Color(1f, 0.45f, 0.45f) : new Color(0.55f, 1f, 0.6f);
                GUILayout.Label(status, statusStyle);
            }

            GUILayout.Space(6);
            showHelp = GUILayout.Toggle(showHelp, " Show controls");
            if (showHelp) GUILayout.Label(HelpText, smallStyle);

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void RefreshProductNames(SimulationManager sim)
        {
            int needed = sim.productPrefabs.Count + 1;
            if (productNames != null && productNames.Length == needed) return;
            productNames = new string[needed];
            productNames[0] = "Random mix";
            for (int i = 0; i < sim.productPrefabs.Count; i++)
            {
                GameObject prefab = sim.productPrefabs[i];
                Product product = prefab != null ? prefab.GetComponent<Product>() : null;
                productNames[i + 1] = product != null ? product.productName : (prefab != null ? prefab.name : "Missing");
            }
        }

        void EnsureStyles()
        {
            if (titleStyle != null) return;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
            statusStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontStyle = FontStyle.Bold };
            smallStyle = new GUIStyle(GUI.skin.label) { wordWrap = true, fontSize = 12 };
        }
    }
}
