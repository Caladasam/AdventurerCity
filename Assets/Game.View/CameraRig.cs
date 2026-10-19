using UnityEngine;

namespace AdventurerCity.View
{
    public sealed class CameraRig : MonoBehaviour
    {
        const float RotateSpeed = 90f;
        const float MinDistance = 10f;
        const float MaxDistance = 120f;

        Camera cam;
        Vector3 pivot;
        Vector2 bounds;
        Vector3 lastMouse;
        float yaw = 45f;
        float pitch = 55f;
        float distance = 45f;

        public void Init(Vector3 focus, Vector2 mapSize)
        {
            pivot = focus;
            bounds = mapSize;
            cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Sky;
            cam.farClipPlane = 500f;
            Apply();
        }

        public bool TryGetGroundCell(out Vector2Int cell)
        {
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out var hit))
            {
                cell = GridMath.WorldToCell(ray.GetPoint(hit));
                return true;
            }
            cell = default;
            return false;
        }

        void Update()
        {
            var dt = Time.unscaledDeltaTime;
            var input = Vector3.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) input.z += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) input.z -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input.x += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input.x -= 1f;
            if (Input.GetKey(KeyCode.Q)) yaw += RotateSpeed * dt;
            if (Input.GetKey(KeyCode.E)) yaw -= RotateSpeed * dt;

            var heading = Quaternion.Euler(0f, yaw, 0f);
            pivot += heading * input.normalized * (distance * 0.8f * dt);

            if (Input.GetMouseButtonDown(2)) lastMouse = Input.mousePosition;
            if (Input.GetMouseButton(2))
            {
                var delta = Input.mousePosition - lastMouse;
                pivot -= heading * new Vector3(delta.x, 0f, delta.y) * (distance * 0.002f);
                lastMouse = Input.mousePosition;
            }

            distance = Mathf.Clamp(distance * (1f - Input.mouseScrollDelta.y * 0.1f), MinDistance, MaxDistance);
            pivot.x = Mathf.Clamp(pivot.x, 0f, bounds.x);
            pivot.z = Mathf.Clamp(pivot.z, 0f, bounds.y);
            Apply();
        }

        void Apply()
        {
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            cam.transform.SetPositionAndRotation(pivot + rotation * new Vector3(0f, 0f, -distance), rotation);
        }
    }
}
