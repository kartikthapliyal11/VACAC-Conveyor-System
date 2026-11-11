using UnityEngine;

namespace ConveyorSystem
{
    /// <summary>
    /// Orbit camera. WASD / arrows to pan, Q / E to rotate, mouse wheel to zoom,
    /// middle mouse (or Alt + left mouse) to orbit, Shift + middle mouse to pan, F to reset.
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        public static CameraController Instance { get; private set; }

        public Vector3 focus = Vector3.zero;
        public float distance = 12f;
        public float yaw = 35f;
        public float pitch = 50f;
        public float minDistance = 1.5f;
        public float maxDistance = 120f;
        [Tooltip("Pan speed, relative to the zoom distance")]
        public float panSpeed = 0.9f;
        public float keyRotateSpeed = 90f;
        public float mouseRotateSpeed = 0.3f;

        Vector2 lastMouse;
        Vector3 homeFocus;
        float homeDistance, homeYaw, homePitch;

        void Awake()
        {
            Instance = this;
            SaveHome();
        }

        void Start()
        {
            lastMouse = InputHelper.MousePosition;
            Apply();
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            Vector2 mouse = InputHelper.MousePosition;
            Vector2 delta = mouse - lastMouse;
            lastMouse = mouse;

            // Keyboard pan
            Vector2 move = Vector2.zero;
            if (InputHelper.KeyHeld(KeyCode.W) || InputHelper.KeyHeld(KeyCode.UpArrow)) move.y += 1f;
            if (InputHelper.KeyHeld(KeyCode.S) || InputHelper.KeyHeld(KeyCode.DownArrow)) move.y -= 1f;
            if (InputHelper.KeyHeld(KeyCode.D) || InputHelper.KeyHeld(KeyCode.RightArrow)) move.x += 1f;
            if (InputHelper.KeyHeld(KeyCode.A) || InputHelper.KeyHeld(KeyCode.LeftArrow)) move.x -= 1f;
            Quaternion yawRotation = Quaternion.Euler(0f, yaw, 0f);
            focus += yawRotation * new Vector3(move.x, 0f, move.y) * (panSpeed * distance * dt);

            // Keyboard rotate
            if (InputHelper.KeyHeld(KeyCode.Q)) yaw += keyRotateSpeed * dt;
            if (InputHelper.KeyHeld(KeyCode.E)) yaw -= keyRotateSpeed * dt;

            // Mouse orbit / pan
            bool middle = InputHelper.MiddleHeld;
            bool orbit = (middle && !InputHelper.Shift) || (InputHelper.Alt && InputHelper.LeftHeld);
            bool pan = middle && InputHelper.Shift;
            if (orbit)
            {
                yaw += delta.x * mouseRotateSpeed;
                pitch -= delta.y * mouseRotateSpeed;
            }
            else if (pan)
            {
                float k = distance * 0.0015f;
                focus -= (transform.right * delta.x + yawRotation * Vector3.forward * delta.y) * k;
            }

            // Zoom
            if (!BuilderUI.IsPointerOverUI)
            {
                float scroll = InputHelper.Scroll;
                if (Mathf.Abs(scroll) > 0.001f) distance *= Mathf.Pow(0.88f, scroll);
            }

            if (InputHelper.KeyDown(KeyCode.F)) ResetView();

            distance = Mathf.Clamp(distance, minDistance, maxDistance);
            pitch = Mathf.Clamp(pitch, 10f, 89f);
            Apply();
        }

        void Apply()
        {
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.position = focus - transform.forward * distance;
        }

        /// <summary>Points the camera at <paramref name="bounds"/> and remembers it as the reset view.</summary>
        public void Frame(Bounds bounds)
        {
            focus = new Vector3(bounds.center.x, 0f, bounds.center.z);
            distance = Mathf.Clamp(bounds.extents.magnitude * 2.2f, minDistance, maxDistance);
            SaveHome();
            Apply();
        }

        public void ResetView()
        {
            focus = homeFocus;
            distance = homeDistance;
            yaw = homeYaw;
            pitch = homePitch;
        }

        void SaveHome()
        {
            homeFocus = focus;
            homeDistance = distance;
            homeYaw = yaw;
            homePitch = pitch;
        }
    }
}
