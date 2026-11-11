using UnityEngine;

namespace ConveyorSystem
{
    public static class ConveyorUtils
    {
        /// <summary>World-space bounds of every mesh renderer under <paramref name="go"/>.</summary>
        public static Bounds CalculateBounds(GameObject go)
        {
            bool found = false;
            Bounds bounds = new Bounds(go.transform.position, Vector3.zero);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is LineRenderer || r is TrailRenderer) continue;
                if (!found) { bounds = r.bounds; found = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (!found) bounds = new Bounds(go.transform.position, Vector3.one * 0.25f);
            return bounds;
        }
    }
}
