using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace COMP2165.Clicker
{
    public class MapZoom : MonoBehaviour
    {
        public RectTransform map;     // the big Map object
        public ScrollRect scroll;     // the Scroll Rect on this object
        public float minZoom = 1f;
        public float maxZoom = 2.5f;
        public float wheelStep = 0.1f;
        public float buttonStep = 0.25f;

        RectTransform window;
        Canvas canvas;
        float zoom = 1f;

        void Awake()
        {
            window = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>().rootCanvas;
            if (scroll != null) scroll.scrollSensitivity = 0f;   // the wheel zooms instead of scrolling
        }

        void Start()
        {
            // Never zoom out far enough to show past the edge of the map
            float fit = Mathf.Max(window.rect.width / map.rect.width, window.rect.height / map.rect.height);
            minZoom = fit;
            SetZoom(Mathf.Max(1f, minZoom), CenterPoint());
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            float wheel = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(wheel) < 0.01f) return;
            Vector2 pos = mouse.position.ReadValue();
            if (!RectTransformUtility.RectangleContainsScreenPoint(window, pos, Cam())) return;
            SetZoom(zoom + Mathf.Sign(wheel) * wheelStep, pos);
        }

        // For + and - buttons (On Click)
        public void ZoomIn()  { SetZoom(zoom + buttonStep, CenterPoint()); }
        public void ZoomOut() { SetZoom(zoom - buttonStep, CenterPoint()); }

        Camera Cam()
        {
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        Vector2 CenterPoint()
        {
            return RectTransformUtility.WorldToScreenPoint(Cam(), window.TransformPoint(window.rect.center));
        }

        void SetZoom(float target, Vector2 screenPoint)
        {
            target = Mathf.Clamp(target, minZoom, maxZoom);
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(map, screenPoint, Cam(), out local);
            map.anchoredPosition += local * (zoom - target);   // keeps the point under the cursor in place
            zoom = target;
            map.localScale = new Vector3(zoom, zoom, 1f);

            if (scroll != null)
            {
                scroll.StopMovement();
                Vector2 n = scroll.normalizedPosition;   // pull back inside the edges if zooming out overshot
                scroll.normalizedPosition = new Vector2(Mathf.Clamp01(n.x), Mathf.Clamp01(n.y));
            }
        }
    }
}