using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using TrueDetective.Core;
using TrueDetective.Data;

namespace TrueDetective.UI
{
    /// <summary>
    /// The one MonoBehaviour in the project. Builds the canvas, every screen and the
    /// event system in code, so the scene needs a single empty GameObject with this
    /// component and nothing has to be wired in the Inspector.
    ///
    /// Screens are declared as partial-class methods in the Screen_*.cs files.
    /// </summary>
    [AddComponentMenu("True Detective/Game")]
    public partial class Game : MonoBehaviour
    {
        [Tooltip("Case file to load from Assets/Resources/Cases, without the .json")]
        public string caseId = "case01";

        public const float RefWidth  = 1080f;
        public const float RefHeight = 1920f;

        public CaseSession Session { get; private set; }
        public CaseData Case { get { return Session != null ? Session.Case : null; } }

        private RectTransform _root;
        private readonly Dictionary<string, RectTransform> _screens = new Dictionary<string, RectTransform>();
        private readonly List<string> _history = new List<string>();
        private string _current;

        private static readonly Dictionary<string, Sprite> _bgCache = new Dictionary<string, Sprite>();

        // ------------------------------------------------------------------
        // lifecycle
        // ------------------------------------------------------------------

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;

            var data = CaseLoader.Load(caseId);
            if (data == null)
            {
                BuildCanvas();
                UIKit.Label("loadError", _root,
                    "تعذّر تحميل ملف القضية «" + caseId + "».\n\nتأكد من وجوده في:\nAssets/Resources/Cases/" + caseId + ".json\nوراجع نافذة Console.",
                    Theme.SizeHeading, Theme.Alert, TextAlignmentOptions.Center);
                return;
            }

            Session = new CaseSession(data);
            Session.Notice += ShowToast;
            Session.EvidenceCollected += _ => Sfx.Play(Sfx.Cue.EvidenceFiled);

            GameSettings.Load();
            Sfx.Init(gameObject);

            BuildCanvas();
            BuildAllScreens();

            // the cold open plays once; after that the menu is the front door
            Show(GameSettings.HasSeenIntro ? "menu" : "intro", false);
        }

        private void BuildCanvas()
        {
            if (FindObjectOfType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem));
                es.AddComponent<StandaloneInputModule>();
                es.transform.SetParent(transform, false);
            }

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            // match width: a taller phone gets more room below, it never shrinks the text
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0f;

            _root = UIKit.Node("Root", canvasGo.transform);

            var bg = _root.gameObject.AddComponent<Image>();
            bg.sprite = UIKit.Solid;
            bg.color = Theme.Ink;
        }

        // ------------------------------------------------------------------
        // screen stack
        // ------------------------------------------------------------------

        /// <summary>Registers a screen panel under a name. Called by each Build* method.</summary>
        private RectTransform NewScreen(string key, Color? bg = null)
        {
            var panel = UIKit.Panel("screen_" + key, _root, bg ?? Theme.Ink);
            _screens[key] = panel;
            return panel;
        }

        /// <summary>
        /// Shows a screen and refreshes it. Screens rebuild their contents on show
        /// rather than trying to patch themselves, which is what keeps progress
        /// consistent no matter which order the player moves through the interface.
        /// </summary>
        public void Show(string key, bool recordHistory = true)
        {
            if (!_screens.ContainsKey(key))
            {
                Debug.LogError("[Game] unknown screen '" + key + "'");
                return;
            }

            if (recordHistory && !string.IsNullOrEmpty(_current) && _current != key)
            {
                _history.Add(_current);
                if (_history.Count > 24) _history.RemoveAt(0);
            }

            foreach (var kv in _screens) kv.Value.gameObject.SetActive(kv.Key == key);
            _current = key;
            RefreshScreen(key);
            BringOverlaysToFront();
        }

        /// <summary>Goes back one screen, falling back to the current location.</summary>
        public void Back()
        {
            if (_history.Count == 0) { Show("location", false); return; }
            string prev = _history[_history.Count - 1];
            _history.RemoveAt(_history.Count - 1);
            Show(prev, false);
        }

        private void RefreshScreen(string key)
        {
            switch (key)
            {
                case "intro":        RefreshCinematic();    break;
                case "menu":         RefreshMenu();         break;
                case "cases":        RefreshCases();        break;
                case "settings":     RefreshSettings();     break;
                case "codex":        RefreshCodex();        break;
                case "newspaper":    RefreshNewspaper();    break;
                case "briefing":     RefreshBriefing();     break;
                case "location":     RefreshLocation();     break;
                case "notebook":     RefreshNotebook();     break;
                case "interrogate":  RefreshInterrogation();break;
                case "connect":      RefreshConnect();      break;
                case "confront":     RefreshConfrontation();break;
                case "report":       RefreshReport();       break;
                case "verdict":      RefreshVerdict();      break;
            }
        }

        private void BuildAllScreens()
        {
            BuildCinematic();
            BuildMenu();
            BuildCases();
            BuildSettings();
            BuildCodex();
            BuildNewspaper();
            BuildBriefing();
            BuildLocation();
            BuildNotebook();
            BuildInterrogation();
            BuildConnect();
            BuildConfrontation();
            BuildReport();
            BuildVerdict();

            BuildDocumentModal();
            BuildToast();
            BuildHintModal();
        }

        /// <summary>Modals and the toast must sit above every screen.</summary>
        private void BringOverlaysToFront()
        {
            if (_docModal != null) _docModal.SetAsLastSibling();
            if (_hintModal != null) _hintModal.SetAsLastSibling();
            if (_toast != null) _toast.SetAsLastSibling();
        }

        // ------------------------------------------------------------------
        // shared chrome
        // ------------------------------------------------------------------

        /// <summary>
        /// The bar every investigation screen carries: a back action on the right where
        /// the thumb reaches in an RTL layout, the title, and the evidence count.
        /// </summary>
        private RectTransform TopBar(RectTransform parent, string title, Action onBack, bool showCount = true)
        {
            var bar = UIKit.Node("topbar", parent);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(0f, 130f);
            bar.anchoredPosition = Vector2.zero;

            var bg = bar.gameObject.AddComponent<Image>();
            bg.sprite = UIKit.Solid;
            bg.color = new Color(0f, 0f, 0f, 0.35f);

            if (onBack != null)
            {
                var back = UIKit.Btn("back", bar, "رجوع", onBack,
                                     new Color(1f, 1f, 1f, 0.08f), Theme.TextOnDark, Theme.SizeSmall, 82f);
                var brt = back.GetComponent<RectTransform>();
                brt.anchorMin = new Vector2(1f, 0.5f);
                brt.anchorMax = new Vector2(1f, 0.5f);
                brt.pivot = new Vector2(1f, 0.5f);
                brt.sizeDelta = new Vector2(180f, 82f);
                brt.anchoredPosition = new Vector2(-Theme.Gutter * 0.6f, 0f);
                Destroy(back.GetComponent<LayoutElement>());
            }

            var t = UIKit.Label("title", bar, title, Theme.SizeHeading,
                                Theme.TextOnDark, TextAlignmentOptions.Midline);
            t.rectTransform.anchorMin = new Vector2(0f, 0f);
            t.rectTransform.anchorMax = new Vector2(1f, 1f);
            t.rectTransform.offsetMin = new Vector2(220f, 0f);
            t.rectTransform.offsetMax = new Vector2(-220f, 0f);
            UIKit.NoRaycast(t);

            if (showCount)
            {
                var c = UIKit.Label("count", bar,
                    "الأدلة " + Session.EvidenceCount + "/" + Session.EvidenceTotal,
                    Theme.SizeTiny, Theme.Amber, TextAlignmentOptions.MidlineLeft);
                c.rectTransform.anchorMin = new Vector2(0f, 0.5f);
                c.rectTransform.anchorMax = new Vector2(0f, 0.5f);
                c.rectTransform.pivot = new Vector2(0f, 0.5f);
                c.rectTransform.sizeDelta = new Vector2(220f, 82f);
                c.rectTransform.anchoredPosition = new Vector2(Theme.Gutter * 0.6f, 0f);
                UIKit.NoRaycast(c);
            }

            return bar;
        }

        /// <summary>
        /// The persistent bottom bar: notebook, deduction board, report and hint. Always
        /// in the same place so a simple action never costs a screen transition.
        /// </summary>
        private RectTransform BottomBar(RectTransform parent)
        {
            var bar = UIKit.Node("bottombar", parent);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.sizeDelta = new Vector2(0f, 170f);
            bar.anchoredPosition = Vector2.zero;

            var bg = bar.gameObject.AddComponent<Image>();
            bg.sprite = UIKit.Solid;
            bg.color = new Color(0f, 0f, 0f, 0.55f);

            var row = UIKit.Node("row", bar);
            UIKit.Stretch(row, 0f);
            row.offsetMin = new Vector2(Theme.Gutter * 0.5f, 22f);
            row.offsetMax = new Vector2(-Theme.Gutter * 0.5f, -22f);
            UIKit.HStack(row, 14f);

            UIKit.Btn("nb", row, "دفتر الأدلة", () => Show("notebook"),
                      Theme.RoomLight, Theme.TextOnDark, Theme.SizeTiny, 126f);
            UIKit.Btn("cn", row, "لوحة الربط", () => Show("connect"),
                      Theme.RoomLight, Theme.TextOnDark, Theme.SizeTiny, 126f);
            UIKit.Btn("rp", row, "التقرير", () => Show("report"),
                      Theme.RoomLight, Theme.TextOnDark, Theme.SizeTiny, 126f);
            UIKit.Btn("hn", row, "تلميح", ShowHint,
                      Theme.AmberDim, Theme.Amber, Theme.SizeTiny, 126f);

            return bar;
        }

        /// <summary>Content area between the top and bottom bars.</summary>
        private RectTransform Body(RectTransform parent, float top = 130f, float bottom = 170f)
        {
            var rt = UIKit.Node("body", parent);
            rt.offsetMin = new Vector2(0f, bottom);
            rt.offsetMax = new Vector2(0f, -top);
            return rt;
        }

        private static void Clear(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject);
        }

        // ------------------------------------------------------------------
        // procedural backgrounds
        // ------------------------------------------------------------------

        /// <summary>
        /// Resolves a location background. A real sprite in Resources/Backgrounds wins;
        /// otherwise one is painted in code from the name, so the prototype has three
        /// distinguishable rooms without any art, and dropping art in later needs no
        /// code change.
        /// </summary>
        private static Sprite BackgroundFor(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) spriteName = "bg_default";
            Sprite cached;
            if (_bgCache.TryGetValue(spriteName, out cached)) return cached;

            var real = Resources.Load<Sprite>("Backgrounds/" + spriteName);
            if (real != null) { _bgCache[spriteName] = real; return real; }

            var s = PaintRoom(spriteName);
            _bgCache[spriteName] = s;
            return s;
        }

        private static Sprite PaintRoom(string key)
        {
            const int W = 270, H = 480;           // quarter-res, stretched; plenty for a backdrop
            var t = new Texture2D(W, H, TextureFormat.RGBA32, false);
            t.name = "painted_" + key;
            t.filterMode = FilterMode.Bilinear;

            Color top, bottom, accent;
            switch (key)
            {
                case "bg_office":
                    top = new Color32(0x24, 0x1F, 0x1A, 0xFF);
                    bottom = new Color32(0x12, 0x10, 0x0E, 0xFF);
                    accent = new Color32(0x3E, 0x33, 0x26, 0xFF);
                    break;
                case "bg_archive_hall":
                    top = new Color32(0x1A, 0x20, 0x26, 0xFF);
                    bottom = new Color32(0x0D, 0x10, 0x14, 0xFF);
                    accent = new Color32(0x2C, 0x34, 0x3E, 0xFF);
                    break;
                case "bg_guard_room":
                    top = new Color32(0x14, 0x1E, 0x20, 0xFF);
                    bottom = new Color32(0x0A, 0x11, 0x13, 0xFF);
                    accent = new Color32(0x20, 0x33, 0x36, 0xFF);
                    break;
                default:
                    top = Theme.Room; bottom = Theme.Ink; accent = Theme.RoomLight;
                    break;
            }

            for (int y = 0; y < H; y++)
            {
                float v = (float)y / (H - 1);
                // ease the gradient so the light pools near the top instead of banding
                Color row = Color.Lerp(bottom, top, v * v * (3f - 2f * v));
                for (int x = 0; x < W; x++)
                {
                    float u = (float)x / (W - 1);
                    // soft vignette keeps the eye on the middle of the frame
                    float dx = (u - 0.5f) * 2f, dy = (v - 0.55f) * 1.6f;
                    float vig = 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) - 0.35f) * 0.75f;
                    t.SetPixel(x, y, row * vig);
                }
            }

            // a few large blocks that read as furniture at a glance
            switch (key)
            {
                case "bg_office":
                    Band(t, 0, 0, W, (int)(H * 0.30f), accent, 0.55f);                 // desk
                    Band(t, (int)(W * 0.62f), (int)(H * 0.52f), (int)(W * 0.32f), (int)(H * 0.30f), accent, 0.7f); // board
                    break;
                case "bg_archive_hall":
                    for (int i = 0; i < 5; i++)                                        // shelving
                        Band(t, (int)(W * (0.03f + i * 0.20f)), (int)(H * 0.30f),
                             (int)(W * 0.14f), (int)(H * 0.52f), accent, 0.55f);
                    Band(t, (int)(W * 0.38f), (int)(H * 0.26f), (int)(W * 0.24f), (int)(H * 0.16f), accent, 0.85f); // pedestal
                    break;
                case "bg_guard_room":
                    for (int i = 0; i < 4; i++)                                        // monitor wall
                        Band(t, (int)(W * (0.48f + (i % 2) * 0.24f)),
                             (int)(H * (0.46f + (i / 2) * 0.22f)),
                             (int)(W * 0.20f), (int)(H * 0.18f), accent, 0.9f);
                    Band(t, 0, 0, W, (int)(H * 0.24f), accent, 0.5f);                  // console
                    break;
            }

            t.Apply();
            return Sprite.Create(t, new UnityEngine.Rect(0, 0, W, H), new Vector2(0.5f, 0.5f), 100f);
        }

        private static void Band(Texture2D t, int x0, int y0, int w, int h, Color c, float strength)
        {
            int x1 = Mathf.Min(t.width, x0 + w), y1 = Mathf.Min(t.height, y0 + h);
            for (int y = Mathf.Max(0, y0); y < y1; y++)
                for (int x = Mathf.Max(0, x0); x < x1; x++)
                    t.SetPixel(x, y, Color.Lerp(t.GetPixel(x, y), c, strength));
        }

        // ------------------------------------------------------------------
        // toast
        // ------------------------------------------------------------------

        private RectTransform _toast;
        private TextMeshProUGUI _toastLabel;
        private Coroutine _toastRoutine;

        private void BuildToast()
        {
            _toast = UIKit.Node("toast", _root);
            _toast.anchorMin = new Vector2(0f, 0f);
            _toast.anchorMax = new Vector2(1f, 0f);
            _toast.pivot = new Vector2(0.5f, 0f);
            _toast.sizeDelta = new Vector2(0f, 120f);
            _toast.anchoredPosition = new Vector2(0f, 190f);

            var card = UIKit.Rect("toast_card", _toast, Theme.Amber);
            UIKit.Stretch(card.rectTransform, 0f);
            card.rectTransform.offsetMin = new Vector2(Theme.Gutter, 0f);
            card.rectTransform.offsetMax = new Vector2(-Theme.Gutter, 0f);
            UIKit.NoRaycast(card);

            _toastLabel = UIKit.Label("toast_text", card.transform, "", Theme.SizeSmall,
                                      Theme.Ink, TextAlignmentOptions.Midline);
            UIKit.Stretch(_toastLabel.rectTransform, 22f);
            _toastLabel.textWrappingMode = TextWrappingModes.Normal;
            UIKit.NoRaycast(_toastLabel);

            _toast.gameObject.SetActive(false);
        }

        /// <summary>A brief, non-blocking confirmation. Never used for anything required.</summary>
        public void ShowToast(string message)
        {
            if (_toast == null) return;
            UIKit.SetText(_toastLabel, message);
            _toast.gameObject.SetActive(true);
            _toast.SetAsLastSibling();
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(HideToastAfter(2.6f));
        }

        private IEnumerator HideToastAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (_toast != null) _toast.gameObject.SetActive(false);
            _toastRoutine = null;
        }

        // ------------------------------------------------------------------
        // hint modal
        // ------------------------------------------------------------------

        private RectTransform _hintModal;
        private TextMeshProUGUI _hintText;
        private TextMeshProUGUI _hintMeta;

        private void BuildHintModal()
        {
            _hintModal = UIKit.Node("hintModal", _root);
            UIKit.Veil("hint_veil", _hintModal, () => _hintModal.gameObject.SetActive(false));

            var holder = UIKit.Node("hint_holder", _hintModal);
            holder.anchorMin = new Vector2(0f, 0.5f);
            holder.anchorMax = new Vector2(1f, 0.5f);
            holder.pivot = new Vector2(0.5f, 0.5f);
            holder.offsetMin = new Vector2(Theme.Gutter, -420f);
            holder.offsetMax = new Vector2(-Theme.Gutter, 420f);
            UIKit.VStack(holder, 22f, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);

            var inner = UIKit.PaperCard("hint_card", holder, 36f);
            UIKit.Label("hint_h", inner, "تلميح", Theme.SizeTitle, Theme.TextOnPaper,
                        TextAlignmentOptions.TopRight, true, true);
            UIKit.Divider(inner, Theme.PaperEdge, 3f);
            _hintText = UIKit.Label("hint_t", inner, "", Theme.SizeBody, Theme.TextOnPaper);
            _hintText.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(_hintText.rectTransform);
            _hintMeta = UIKit.Label("hint_m", inner, "", Theme.SizeTiny,
                                    new Color(0.35f, 0.32f, 0.27f, 1f));
            UIKit.FitHeight(_hintMeta.rectTransform);

            UIKit.BtnPrimary("hint_close", holder, "تابع التحقيق",
                             () => _hintModal.gameObject.SetActive(false), 110f, Theme.SizeBody);

            _hintModal.gameObject.SetActive(false);
        }

        /// <summary>
        /// Escalates a hint for whatever stage the player is actually in. Costs nothing:
        /// no lives, no score, no completed scene replayed.
        /// </summary>
        public void ShowHint()
        {
            var stage = Session.CurrentStage;
            int before = Session.HintsUsed(stage);
            string text = Session.NextHint();

            UIKit.SetText(_hintText, text);
            UIKit.SetText(_hintMeta, "تلميح " + Mathf.Min(before + 1, Session.HintsAvailable(stage)) +
                                     " من " + Session.HintsAvailable(stage) + " · " + StageName(stage));
            _hintModal.gameObject.SetActive(true);
            _hintModal.SetAsLastSibling();
        }

        private static string StageName(Stage s)
        {
            switch (s)
            {
                case Stage.Investigate: return "مرحلة جمع المعلومات";
                case Stage.Connect:     return "مرحلة الربط";
                case Stage.Confront:    return "مرحلة المواجهة";
                case Stage.Report:      return "مرحلة التقرير";
                default:                return "القضية مغلقة";
            }
        }
    }
}
