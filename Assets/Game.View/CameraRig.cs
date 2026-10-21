using UnityEngine;

namespace AdventurerCity.View
{
    public sealed class CameraRig : MonoBehaviour
    {
        const float MinSize = 4f;
        const float MaxSize = 30f;

        Camera cam;
        Vector3 pivot;
        Vector2 bounds;
        Vector3 lastMouse;
        float size = 12f;

        public Camera Camera => cam;

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
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Background;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 100f;
            cam.transform.rotation = Quaternion.identity;
            Apply();
        }

        public bool TryGetGroundCell(out Vector2Int cell)
        {
            if (!cam.enabled)
            {
                cell = default;
                return false;
            }
            var point = cam.ScreenToWorldPoint(Input.mousePosition);
            cell = GridMath.WorldToCell(point);
            return true;
        }

        void Update()
        {
            if (!cam.enabled) return;
            var dt = Time.unscaledDeltaTime;
            var input = Vector3.zero;
            if (GameSettings.Held(GameKey.CameraUp) || Input.GetKey(KeyCode.UpArrow)) input.y += 1f;
            if (GameSettings.Held(GameKey.CameraDown) || Input.GetKey(KeyCode.DownArrow)) input.y -= 1f;
            if (GameSettings.Held(GameKey.CameraRight) || Input.GetKey(KeyCode.RightArrow)) input.x += 1f;
            if (GameSettings.Held(GameKey.CameraLeft) || Input.GetKey(KeyCode.LeftArrow)) input.x -= 1f;
            pivot += input.normalized * (size * 1.2f * dt);

            if (Input.GetMouseButtonDown(2)) lastMouse = Input.mousePosition;
            if (Input.GetMouseButton(2))
            {
                var delta = Input.mousePosition - lastMouse;
                pivot -= delta * (2f * size / Screen.height);
                lastMouse = Input.mousePosition;
            }

            size = Mathf.Clamp(size * (1f - Input.mouseScrollDelta.y * 0.1f), MinSize, MaxSize);
            pivot.x = Mathf.Clamp(pivot.x, 0f, bounds.x);
            pivot.y = Mathf.Clamp(pivot.y, 0f, bounds.y);
            Apply();
        }

        void Apply()
        {
            cam.orthographicSize = size;
            cam.transform.position = new Vector3(pivot.x, pivot.y, -10f);
        }
    }
}
