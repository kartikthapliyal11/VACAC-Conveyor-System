using System.Collections.Generic;
using UnityEngine;

namespace ConveyorSystem
{
    /// <summary>
    /// Runs the simulation: spawns products at the start of every conveyor line,
    /// holds belt speed / spawn rate settings, and keeps simple statistics.
    /// </summary>
    public class SimulationManager : MonoBehaviour
    {
        public static SimulationManager Instance { get; private set; }

        [Header("Products (filled by the Setup Wizard)")]
        public List<GameObject> productPrefabs = new List<GameObject>();
        [Tooltip("-1 = random mix, otherwise index into Product Prefabs")]
        public int selectedProduct = -1;

        [Header("Simulation")]
        public bool isRunning = true;
        [Tooltip("Belt speed in metres per second")]
        public float beltSpeed = 0.75f;
        [Tooltip("Seconds between new products at each line start")]
        public float spawnInterval = 2f;
        [Tooltip("Minimum space between queued products (metres)")]
        public float productGap = 0.1f;

        [Header("Visuals")]
        public Material pathMaterial;
        [SerializeField] bool showPaths = true;

        public int SpawnedCount { get; private set; }
        public int DeliveredCount { get; private set; }

        public bool ShowPaths
        {
            get => showPaths;
            set
            {
                showPaths = value;
                foreach (ConveyorPiece p in ConveyorPiece.Placed) p.SetPathVisible(value, pathMaterial);
            }
        }

        public int ProductsOnBelts
        {
            get
            {
                int count = 0;
                foreach (Product p in Product.All) if (p != null && p.OnBelt) count++;
                return count;
            }
        }

        float spawnTimer;

        void Awake() { Instance = this; }

        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            if (InputHelper.KeyDown(KeyCode.Space)) isRunning = !isRunning;
            if (!isRunning || productPrefabs.Count == 0) return;

            spawnTimer += Time.deltaTime;
            if (spawnTimer < spawnInterval) return;
            spawnTimer = 0f;

            // Every piece with nothing feeding into it is the start of a line.
            for (int i = 0; i < ConveyorPiece.Placed.Count; i++)
            {
                ConveyorPiece piece = ConveyorPiece.Placed[i];
                if (piece.Previous == null && IsEntryClear(piece)) Spawn(piece);
            }
        }

        bool IsEntryClear(ConveyorPiece piece)
        {
            if (piece.Products.Count == 0) return true;
            Product first = piece.Products[0];
            return first.Distance > first.Length + productGap;
        }

        public Product Spawn(ConveyorPiece piece)
        {
            GameObject prefab = PickPrefab();
            if (prefab == null) return null;

            piece.Sample(0f, out Vector3 position, out _);
            GameObject go = Instantiate(prefab, position, Quaternion.identity);
            go.name = prefab.name;

            Product product = go.GetComponent<Product>();
            if (product == null) product = go.AddComponent<Product>();
            product.Initialise(piece);
            SpawnedCount++;
            return product;
        }

        GameObject PickPrefab()
        {
            if (productPrefabs.Count == 0) return null;
            if (selectedProduct >= 0 && selectedProduct < productPrefabs.Count) return productPrefabs[selectedProduct];
            return productPrefabs[Random.Range(0, productPrefabs.Count)];
        }

        public void RegisterDelivered(Product product) { DeliveredCount++; }

        public void ClearProducts()
        {
            for (int i = Product.All.Count - 1; i >= 0; i--)
                if (Product.All[i] != null) Destroy(Product.All[i].gameObject);
            SpawnedCount = 0;
            DeliveredCount = 0;
        }
    }
}
