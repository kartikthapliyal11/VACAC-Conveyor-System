using System.Collections.Generic;
using UnityEngine;

namespace ConveyorSystem
{
    /// <summary>
    /// A product riding the conveyors. It follows each piece's path, moves on to the
    /// next connected piece, queues behind the product in front, and falls off
    /// (using real physics) when it reaches the end of a line.
    /// </summary>
    public class Product : MonoBehaviour
    {
        /// <summary>Every product that exists (on a belt or falling).</summary>
        public static readonly List<Product> All = new List<Product>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }

        public string productName = "Product";

        public float Length { get; private set; }
        public float Distance => distance;
        public bool OnBelt => !dropped && current != null;

        ConveyorPiece current;
        float distance;
        float bottomOffset;
        bool dropped;
        Rigidbody body;

        void Awake() { All.Add(this); }

        void OnDestroy()
        {
            All.Remove(this);
            if (current != null) current.Products.Remove(this);
        }

        /// <summary>Puts the product on the start of <paramref name="start"/>.</summary>
        public void Initialise(ConveyorPiece start)
        {
            // Measure the product while it is unrotated.
            transform.rotation = Quaternion.identity;
            Bounds b = ConveyorUtils.CalculateBounds(gameObject);
            bottomOffset = transform.position.y - b.min.y;
            Length = Mathf.Max(0.01f, b.size.z);

            if (GetComponentInChildren<Collider>() == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                Vector3 scale = transform.lossyScale;
                box.center = Div(b.center - transform.position, scale);
                box.size = Div(b.size, scale);
            }

            body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;

            current = start;
            distance = 0f;
            dropped = false;
            start.Products.Insert(0, this);
            UpdateTransform();
        }

        void Update()
        {
            if (dropped) return;
            if (current == null) { Drop(false); return; } // its conveyor was removed

            SimulationManager sim = SimulationManager.Instance;
            if (sim != null && !sim.isRunning) return;

            float speed = sim != null ? sim.beltSpeed : 1f;
            float gap = sim != null ? sim.productGap : 0.1f;
            float move = speed * Time.deltaTime;

            // Queue behind the product in front instead of overlapping it.
            if (FindProductAhead(out Product ahead, out float spacing))
            {
                float required = (Length + ahead.Length) * 0.5f + gap;
                move = Mathf.Min(move, Mathf.Max(0f, spacing - required));
            }

            distance += move;
            while (distance >= current.Length)
            {
                if (current.Next == null)
                {
                    distance = current.Length;
                    UpdateTransform();
                    Drop(true);
                    return;
                }
                distance -= current.Length;
                current.Products.Remove(this);
                current = current.Next;
                current.Products.Insert(0, this);
            }
            UpdateTransform();
        }

        bool FindProductAhead(out Product ahead, out float spacing)
        {
            List<Product> list = current.Products;
            int index = list.IndexOf(this);
            if (index >= 0 && index + 1 < list.Count)
            {
                ahead = list[index + 1];
                spacing = ahead.distance - distance;
                return true;
            }

            // Look along the following pieces.
            float travelled = current.Length - distance;
            ConveyorPiece piece = current.Next;
            for (int step = 0; piece != null && step < 8 && travelled < 50f; step++)
            {
                if (piece.Products.Count > 0)
                {
                    ahead = piece.Products[0];
                    if (ahead == this) break; // closed loop with only this product
                    spacing = travelled + ahead.distance;
                    return true;
                }
                travelled += piece.Length;
                piece = piece.Next;
            }

            ahead = null;
            spacing = float.PositiveInfinity;
            return false;
        }

        void UpdateTransform()
        {
            current.Sample(distance, out Vector3 position, out Vector3 forward);
            transform.position = position + Vector3.up * bottomOffset;
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
        }

        /// <summary>Lets the product fall off with physics. Destroyed a few seconds later.</summary>
        public void Drop(bool reachedEndOfLine)
        {
            if (dropped) return;
            dropped = true;

            if (current != null) current.Products.Remove(this);
            current = null;

            SimulationManager sim = SimulationManager.Instance;
            float speed = sim != null ? sim.beltSpeed : 1f;

            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = false;
#if UNITY_6000_0_OR_NEWER
            body.linearVelocity = transform.forward * speed;
#else
            body.velocity = transform.forward * speed;
#endif
            if (reachedEndOfLine && sim != null) sim.RegisterDelivered(this);
            Destroy(gameObject, 4f);
        }

        static Vector3 Div(Vector3 a, Vector3 b)
        {
            return new Vector3(
                b.x != 0f ? a.x / b.x : a.x,
                b.y != 0f ? a.y / b.y : a.y,
                b.z != 0f ? a.z / b.z : a.z);
        }
    }
}
