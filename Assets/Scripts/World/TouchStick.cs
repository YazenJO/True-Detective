using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace TrueDetective.World
{
    /// <summary>
    /// A floating virtual stick. It has no fixed home: it appears wherever the thumb
    /// lands in the lower half of the screen and follows from there, which is the only
    /// arrangement that works when nobody can see where their thumb is.
    ///
    /// The keyboard is read too, so the game is playable in the editor without a touch
    /// device, and WASD or the arrows override the stick while held.
    /// </summary>
    public class TouchStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Tooltip("Distance in reference pixels for full deflection.")]
        public float Range = 150f;

        [Tooltip("Deflection below this fraction reads as no input, killing thumb jitter.")]
        public float DeadZone = 0.12f;

        private RectTransform _self;
        private RectTransform _ring;
        private RectTransform _knob;
        private CanvasGroup _group;
        private Canvas _canvas;

        private Vector2 _origin;
        private Vector2 _value;
        private int _finger = -1;

        /// <summary>Current direction, magnitude 0 to 1.</summary>
        public Vector2 Value
        {
            get
            {
                var keys = KeyboardVector();
                return keys.sqrMagnitude > 0.01f ? keys : _value;
            }
        }

        private static Vector2 KeyboardVector()
        {
            float x = 0f, y = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))  x -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) x += 1f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))    y += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))  y -= 1f;
            var v = new Vector2(x, y);
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }

        /// <summary>
        /// Builds the stick into a parent, covering the lower part of the screen so a
        /// thumb anywhere down there starts a drag. Returns the component.
        /// </summary>
        public static TouchStick Create(RectTransform parent, Canvas canvas,
                                        Sprite ring, Sprite knob)
        {
            var go = new GameObject("touchstick", typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0.55f);     // lower half only: the top is for reading
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            // an invisible but raycastable surface to catch the touch
            var catcher = go.AddComponent<Image>();
            catcher.color = new Color(0f, 0f, 0f, 0f);
            catcher.raycastTarget = true;

            var stick = go.AddComponent<TouchStick>();
            stick._self = rt;
            stick._canvas = canvas;

            var visual = new GameObject("visual", typeof(RectTransform));
            var vrt = (RectTransform)visual.transform;
            vrt.SetParent(rt, false);
            vrt.sizeDelta = new Vector2(280f, 280f);
            stick._ring = vrt;

            var ringImg = visual.AddComponent<Image>();
            ringImg.sprite = ring;
            ringImg.color = new Color(1f, 1f, 1f, 0.16f);
            ringImg.raycastTarget = false;

            var knobGo = new GameObject("knob", typeof(RectTransform));
            var krt = (RectTransform)knobGo.transform;
            krt.SetParent(vrt, false);
            krt.sizeDelta = new Vector2(120f, 120f);
            stick._knob = krt;

            var knobImg = knobGo.AddComponent<Image>();
            knobImg.sprite = knob;
            knobImg.color = new Color(1f, 1f, 1f, 0.34f);
            knobImg.raycastTarget = false;

            stick._group = visual.AddComponent<CanvasGroup>();
            stick._group.alpha = 0f;
            stick._group.blocksRaycasts = false;

            return stick;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (_finger != -1) return;
            _finger = e.pointerId;
            _origin = LocalPoint(e);
            _ring.anchoredPosition = _origin;
            _knob.anchoredPosition = Vector2.zero;
            _value = Vector2.zero;
            _group.alpha = 1f;
        }

        public void OnDrag(PointerEventData e)
        {
            if (e.pointerId != _finger) return;

            Vector2 delta = LocalPoint(e) - _origin;
            float mag = delta.magnitude;

            if (mag > Range)
            {
                // past full deflection the origin follows the thumb, so a long drag
                // never runs out of stick
                _origin += delta.normalized * (mag - Range);
                _ring.anchoredPosition = _origin;
                delta = delta.normalized * Range;
                mag = Range;
            }

            _knob.anchoredPosition = delta;

            float amount = mag / Range;
            _value = amount < DeadZone ? Vector2.zero : delta.normalized * Mathf.InverseLerp(DeadZone, 1f, amount);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.pointerId != _finger) return;
            _finger = -1;
            _value = Vector2.zero;
            _knob.anchoredPosition = Vector2.zero;
            _group.alpha = 0f;
        }

        private Vector2 LocalPoint(PointerEventData e)
        {
            Vector2 p;
            var cam = _canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null : e.pressEventCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_self, e.position, cam, out p);
            return p;
        }

        /// <summary>Drops any in-progress touch. Used when a panel opens over the world.</summary>
        public void Release()
        {
            _finger = -1;
            _value = Vector2.zero;
            if (_knob != null) _knob.anchoredPosition = Vector2.zero;
            if (_group != null) _group.alpha = 0f;
        }
    }
}
