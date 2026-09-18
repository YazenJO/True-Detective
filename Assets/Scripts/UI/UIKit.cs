using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using TrueDetective.Core;

namespace TrueDetective.UI
{
    /// <summary>
    /// Builders for the whole interface. Every label goes through ArabicText.Fix, and
    /// every alignment defaults to right, so a caller cannot accidentally produce
    /// unshaped or left-aligned Arabic.
    ///
    /// Sprites are generated in code: this prototype ships no art, and a rounded
    /// 1x1-derived sliced sprite looks the same as an imported one at this scale.
    /// </summary>
    public static class UIKit
    {
        private static Sprite _solid;
        private static Sprite _rounded;
        private static Sprite _roundedSmall;

        /// <summary>Flat white sprite, tinted by Image.color.</summary>
        public static Sprite Solid
        {
            get
            {
                if (_solid == null)
                {
                    var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
                    var px = new Color[16];
                    for (int i = 0; i < px.Length; i++) px[i] = Color.white;
                    t.SetPixels(px); t.Apply();
                    t.name = "td_solid";
                    _solid = Sprite.Create(t, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
                }
                return _solid;
            }
        }

        public static Sprite Rounded      { get { if (_rounded == null)      _rounded      = MakeRounded(48, 14); return _rounded; } }
        public static Sprite RoundedSmall { get { if (_roundedSmall == null) _roundedSmall = MakeRounded(32, 8);  return _roundedSmall; } }

        /// <summary>
        /// A white rounded-corner square as a 9-sliced sprite, so one texture stretches
        /// to any panel size without distorting its corners.
        /// </summary>
        private static Sprite MakeRounded(int size, int radius)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.name = "td_rounded_" + size;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = 1f;
                    // distance into the nearest corner circle, anti-aliased over one pixel
                    float cx = x < radius ? radius : (x >= size - radius ? size - radius - 1 : x);
                    float cy = y < radius ? radius : (y >= size - radius ? size - radius - 1 : y);
                    bool inCornerX = x < radius || x >= size - radius;
                    bool inCornerY = y < radius || y >= size - radius;
                    if (inCornerX && inCornerY)
                    {
                        float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        a = Mathf.Clamp01(radius - d + 0.5f);
                    }
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            t.Apply();
            int b = radius + 1;
            return Sprite.Create(t, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                                 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        // ------------------------------------------------------------------
        // structure
        // ------------------------------------------------------------------

        /// <summary>An empty stretched RectTransform, the base of every panel.</summary>
        public static RectTransform Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            Stretch(rt);
            return rt;
        }

        public static void Stretch(RectTransform rt, float pad = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(pad, pad);
            rt.offsetMax = new Vector2(-pad, -pad);
        }

        /// <summary>A coloured rectangle. Rounded unless radius is None.</summary>
        public static Image Rect(string name, Transform parent, Color color, bool rounded = true)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = rounded ? Rounded : Solid;
            if (rounded) img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        /// <summary>Full-screen panel, hidden by default so a screen can opt in.</summary>
        public static RectTransform Panel(string name, Transform parent, Color bg, bool active = false)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Solid;
            img.color = bg;
            rt.gameObject.SetActive(active);
            return rt;
        }

        // ------------------------------------------------------------------
        // text
        // ------------------------------------------------------------------

        /// <summary>
        /// An Arabic-safe label. Pass logical Arabic; shaping and direction are handled
        /// here. isRightToLeftText stays off on purpose - ArabicText already reordered.
        /// </summary>
        public static TextMeshProUGUI Label(
            string name, Transform parent, string text,
            float size = Theme.SizeBody, Color? color = null,
            TextAlignmentOptions align = TextAlignmentOptions.TopRight,
            bool serif = false, bool bold = false)
        {
            var rt = Node(name, parent);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font = serif ? (bold ? Theme.SerifBoldFont : Theme.SerifFont) : Theme.UIFont;
            tmp.fontSize = size;
            tmp.color = color ?? Theme.TextOnDark;
            tmp.alignment = align;
            tmp.lineSpacing = Theme.LineSpacing;
            tmp.richText = true;
            tmp.isRightToLeftText = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            if (bold && !serif) tmp.fontStyle = FontStyles.Bold;
            SetText(tmp, text);
            return tmp;
        }

        /// <summary>The only place text should ever be assigned to a label.</summary>
        public static void SetText(TMP_Text label, string logical)
        {
            if (label == null) return;
            label.text = ArabicText.Fix(logical ?? "");
        }

        // ------------------------------------------------------------------
        // buttons
        // ------------------------------------------------------------------

        /// <summary>
        /// A tappable row. Height defaults to Theme.TouchMin so nothing on screen is
        /// smaller than a thumb.
        /// </summary>
        public static Button Btn(
            string name, Transform parent, string text, Action onClick,
            Color? bg = null, Color? fg = null,
            float fontSize = Theme.SizeBody, float height = Theme.TouchMin,
            bool serif = false)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.color = bg ?? Theme.RoomLight;

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var c = img.color;
            var colors = btn.colors;
            colors.normalColor      = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor     = new Color(0.82f, 0.82f, 0.82f, 1f);
            colors.selectedColor    = Color.white;
            colors.disabledColor    = new Color(1f, 1f, 1f, 0.35f);
            colors.fadeDuration     = 0.08f;
            btn.colors = colors;

            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            le.flexibleWidth = 1f;

            var label = Label(name + "_text", rt, text, fontSize,
                              fg ?? Theme.TextOnDark, TextAlignmentOptions.Midline, serif);
            Stretch(label.rectTransform, 24f);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;

            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        /// <summary>Primary call to action: amber fill, dark text.</summary>
        public static Button BtnPrimary(string name, Transform parent, string text, Action onClick,
                                        float height = 130f, float fontSize = Theme.SizeHeading)
        {
            return Btn(name, parent, text, onClick, Theme.Amber, Theme.Ink, fontSize, height);
        }

        /// <summary>Quiet action: outline only.</summary>
        public static Button BtnGhost(string name, Transform parent, string text, Action onClick,
                                      float height = 96f, float fontSize = Theme.SizeSmall)
        {
            var b = Btn(name, parent, text, onClick, new Color(1f, 1f, 1f, 0.06f),
                        Theme.TextMuted, fontSize, height);
            var o = b.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(1f, 1f, 1f, 0.14f);
            o.effectDistance = new Vector2(2f, -2f);
            return b;
        }

        // ------------------------------------------------------------------
        // layout
        // ------------------------------------------------------------------

        public static VerticalLayoutGroup VStack(RectTransform rt, float spacing = 20f,
                                                 RectOffset padding = null,
                                                 TextAnchor align = TextAnchor.UpperCenter)
        {
            var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = padding ?? new RectOffset(0, 0, 0, 0);
            v.childAlignment = align;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childScaleWidth = false;
            v.childScaleHeight = false;
            return v;
        }

        public static HorizontalLayoutGroup HStack(RectTransform rt, float spacing = 16f,
                                                   RectOffset padding = null,
                                                   TextAnchor align = TextAnchor.MiddleCenter)
        {
            var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = padding ?? new RectOffset(0, 0, 0, 0);
            h.childAlignment = align;
            h.childForceExpandWidth = true;
            h.childForceExpandHeight = false;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childScaleWidth = false;
            h.childScaleHeight = false;
            // Arabic reads right to left, so the first child belongs on the right
            h.reverseArrangement = true;
            return h;
        }

        public static ContentSizeFitter FitHeight(RectTransform rt)
        {
            var f = rt.gameObject.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            f.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            return f;
        }

        /// <summary>
        /// A vertical scroll view. Returns the content transform to fill; it already has
        /// a VerticalLayoutGroup and grows with its children.
        /// </summary>
        public static RectTransform ScrollView(string name, Transform parent,
                                               out ScrollRect scroll, float spacing = 20f,
                                               RectOffset padding = null)
        {
            var viewport = Node(name, parent);
            scroll = viewport.gameObject.AddComponent<ScrollRect>();
            var mask = viewport.gameObject.AddComponent<RectMask2D>();
            mask.padding = Vector4.zero;

            var content = Node(name + "_content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            VStack(content, spacing, padding);
            FitHeight(content);

            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 40f;
            return content;
        }

        /// <summary>A thin divider line.</summary>
        public static Image Divider(Transform parent, Color? color = null, float height = 2f)
        {
            var img = Rect("divider", parent, color ?? new Color(1f, 1f, 1f, 0.10f), false);
            var le = img.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            return img;
        }

        /// <summary>Fixed vertical gap inside a VStack.</summary>
        public static RectTransform Spacer(Transform parent, float height)
        {
            var rt = Node("spacer", parent);
            var le = rt.gameObject.AddComponent<LayoutElement>();
            le.minHeight = height;
            le.preferredHeight = height;
            return rt;
        }

        /// <summary>
        /// A paper-coloured card: the visual signal for "this is a document, read it".
        /// Returns the inner transform, already a VStack with comfortable padding.
        /// </summary>
        public static RectTransform PaperCard(string name, Transform parent, float padding = 32f)
        {
            var card = Rect(name, parent, Theme.Paper);
            var shadow = card.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
            shadow.effectDistance = new Vector2(0f, -6f);

            var inner = Node(name + "_inner", card.transform);
            VStack(inner, 16f, new RectOffset((int)padding, (int)padding, (int)padding, (int)padding));
            FitHeight(inner);

            var fit = card.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childControlHeight = true; v.childControlWidth = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            return inner;
        }

        /// <summary>Screen-wide dim layer for a modal. Tapping it runs onTapOutside.</summary>
        public static Button Veil(string name, Transform parent, Action onTapOutside)
        {
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Solid;
            img.color = Theme.Shade;
            var b = rt.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            if (onTapOutside != null) b.onClick.AddListener(() => onTapOutside());
            return b;
        }

        /// <summary>Makes a graphic ignore taps so it never blocks what is beneath it.</summary>
        public static void NoRaycast(Graphic g)
        {
            if (g != null) g.raycastTarget = false;
        }

        // ------------------------------------------------------------------
        // artwork
        // ------------------------------------------------------------------

        /// <summary>
        /// A non-interactive picture. Returns null when the sprite is missing, so a
        /// caller can fall back rather than showing an empty white box.
        /// </summary>
        public static Image Picture(string name, Transform parent, Sprite sprite,
                                    bool preserveAspect = true)
        {
            if (sprite == null) return null;
            var rt = Node(name, parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = preserveAspect;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>
        /// A dark gradient laid over a photographic background. Text on a painted scene
        /// is unreadable without it, and a flat dim would kill the artwork - this keeps
        /// the middle of the image bright and sinks only the edges where the UI sits.
        /// </summary>
        public static Image Scrim(Transform parent, float topAlpha = 0.80f, float bottomAlpha = 0.92f)
        {
            var rt = Node("scrim", parent);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = VerticalFade(topAlpha, bottomAlpha);
            img.type = Image.Type.Simple;
            img.color = Color.white;
            img.raycastTarget = false;
            return img;
        }

        private static readonly Dictionary<int, Sprite> _fades = new Dictionary<int, Sprite>();

        /// <summary>Black, opaque at both ends, clear across the middle third.</summary>
        private static Sprite VerticalFade(float topAlpha, float bottomAlpha)
        {
            int key = Mathf.RoundToInt(topAlpha * 100f) * 1000 + Mathf.RoundToInt(bottomAlpha * 100f);
            Sprite cached;
            if (_fades.TryGetValue(key, out cached)) return cached;

            const int H = 256;
            var t = new Texture2D(1, H, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            t.name = "scrim_" + key;

            for (int y = 0; y < H; y++)
            {
                float v = (float)y / (H - 1);          // 0 = bottom, 1 = top
                // smoothstep in from each edge, clear between 0.32 and 0.72
                float top = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.72f, 1f, v)) * topAlpha;
                float bot = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.32f, 0f, v)) * bottomAlpha;
                t.SetPixel(0, y, new Color(0f, 0f, 0f, Mathf.Max(top, bot)));
            }
            t.Apply();

            var s = Sprite.Create(t, new Rect(0, 0, 1, H), new Vector2(0.5f, 0.5f), 100f);
            _fades[key] = s;
            return s;
        }

        /// <summary>Adds a CanvasGroup so a whole subtree can be faded as one.</summary>
        public static CanvasGroup Fader(RectTransform rt, float alpha = 1f)
        {
            var cg = rt.GetComponent<CanvasGroup>();
            if (cg == null) cg = rt.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = alpha;
            return cg;
        }
    }
}
