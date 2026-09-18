using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TrueDetective.Core;
using TrueDetective.Data;

namespace TrueDetective.UI
{
    /// <summary>
    /// The investigation hub: a painted room, its hotspots, and the document modal that
    /// opens when one of them yields something.
    /// </summary>
    public partial class Game
    {
        private RectTransform _locBody;
        private Image _locBg;

        private void BuildLocation()
        {
            var s = NewScreen("location");
            _locBody = UIKit.Node("body", s);
        }

        private void RefreshLocation()
        {
            Clear(_locBody);
            var loc = Session.CurrentLocation;
            if (loc == null) return;

            // ---- background ----
            var bg = UIKit.Node("bg", _locBody);
            _locBg = bg.gameObject.AddComponent<Image>();
            _locBg.sprite = BackgroundFor(loc.background);
            _locBg.type = Image.Type.Simple;
            _locBg.preserveAspect = false;
            UIKit.NoRaycast(_locBg);

            // painted scenes need the edges sunk or the bars and labels vanish into them
            UIKit.Scrim(_locBody, 0.82f, 0.90f);

            TopBar(_locBody, loc.name, Session.CurrentLocationId == Case.startLocationId ? (System.Action)null : Back);
            BottomBar(_locBody);

            var body = Body(_locBody);

            // ---- ambience line ----
            var amb = UIKit.Label("amb", body, loc.ambience, Theme.SizeSmall,
                                  Theme.TextMuted, TextAlignmentOptions.TopRight);
            amb.rectTransform.anchorMin = new Vector2(0f, 1f);
            amb.rectTransform.anchorMax = new Vector2(1f, 1f);
            amb.rectTransform.pivot = new Vector2(0.5f, 1f);
            amb.rectTransform.sizeDelta = new Vector2(-Theme.Gutter * 2f, 100f);
            amb.rectTransform.anchoredPosition = new Vector2(0f, -16f);
            amb.textWrappingMode = TextWrappingModes.Normal;
            UIKit.NoRaycast(amb);

            // ---- hotspots, positioned over the background ----
            var spots = UIKit.Node("spots", body);
            spots.offsetMin = new Vector2(0f, 0f);
            spots.offsetMax = new Vector2(0f, -120f);

            foreach (var h in Session.VisibleHotspots())
                MakeHotspot(spots, h);

            // ---- unlocked investigative steps, listed plainly so none can be missed ----
            var actionsCol = UIKit.Node("actions", body);
            actionsCol.anchorMin = new Vector2(0f, 0f);
            actionsCol.anchorMax = new Vector2(1f, 0f);
            actionsCol.pivot = new Vector2(0.5f, 0f);
            actionsCol.offsetMin = new Vector2(Theme.Gutter, 12f);
            actionsCol.offsetMax = new Vector2(-Theme.Gutter, 12f);
            UIKit.VStack(actionsCol, 12f);
            UIKit.FitHeight(actionsCol);

            foreach (var a in Session.AvailableActions())
            {
                var act = a;
                var card = UIKit.Btn("act_" + act.id, actionsCol, "إجراء متاح: " + act.label,
                                     () => ShowActionPrompt(act), Theme.AmberDim, Theme.Amber,
                                     Theme.SizeTiny, 118f);
                var o = card.gameObject.AddComponent<Outline>();
                o.effectColor = Theme.Amber;
                o.effectDistance = new Vector2(2f, -2f);
            }
        }

        /// <summary>
        /// One tappable region. The rect comes from the case file in normalised
        /// coordinates; a faint outline and a label make it findable without turning
        /// the screen into a pixel hunt.
        /// </summary>
        private void MakeHotspot(RectTransform parent, Hotspot h)
        {
            var rt = UIKit.Node("hs_" + h.id, parent);
            // case files use a top-left origin; anchors here are bottom-left
            rt.anchorMin = new Vector2(h.area.x, 1f - (h.area.y + h.area.h));
            rt.anchorMax = new Vector2(h.area.x + h.area.w, 1f - h.area.y);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            bool isTravel = !string.IsNullOrEmpty(h.travelTo);
            bool isPerson = !string.IsNullOrEmpty(h.opensCharacter);
            bool isClue   = !string.IsNullOrEmpty(h.givesEvidence);

            Color fill =
                isClue   ? new Color(0.88f, 0.63f, 0.24f, 0.16f) :
                isPerson ? new Color(0.35f, 0.62f, 0.44f, 0.16f) :
                isTravel ? new Color(0.55f, 0.60f, 0.70f, 0.14f) :
                           new Color(1f, 1f, 1f, 0.06f);

            Color edge =
                isClue   ? new Color(0.88f, 0.63f, 0.24f, 0.85f) :
                isPerson ? new Color(0.35f, 0.62f, 0.44f, 0.85f) :
                isTravel ? new Color(0.62f, 0.68f, 0.78f, 0.7f) :
                           new Color(1f, 1f, 1f, 0.28f);

            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = UIKit.RoundedSmall;
            img.type = Image.Type.Sliced;
            img.color = fill;

            var outline = rt.gameObject.AddComponent<Outline>();
            outline.effectColor = edge;
            outline.effectDistance = new Vector2(2.5f, -2.5f);

            var btn = rt.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(() => OnHotspot(h));

            string prefix = isPerson ? "استجواب: " : isTravel ? "" : "";
            var label = UIKit.Label("l", rt, prefix + h.label, Theme.SizeTiny,
                                    edge, TextAlignmentOptions.Midline);
            UIKit.Stretch(label.rectTransform, 10f);
            label.textWrappingMode = TextWrappingModes.Normal;
            label.overflowMode = TextOverflowModes.Ellipsis;
            UIKit.NoRaycast(label);
        }

        private void OnHotspot(Hotspot h)
        {
            Session.MarkHotspotSeen(h.id);

            if (!string.IsNullOrEmpty(h.travelTo))
            {
                if (Session.TravelTo(h.travelTo)) Show("location", false);
                return;
            }

            if (!string.IsNullOrEmpty(h.opensCharacter))
            {
                _interrogateCharacterId = h.opensCharacter;
                Show("interrogate");
                return;
            }

            if (!string.IsNullOrEmpty(h.givesEvidence))
            {
                var e = Case.GetEvidence(h.givesEvidence);
                if (Session.CollectEvidence(h.givesEvidence))
                {
                    OpenEvidenceDocument(e, true);
                    RefreshLocation();   // the hotspot is spent, take it off the wall
                }
                else if (e != null)
                {
                    OpenEvidenceDocument(e, false);
                }
                return;
            }

            OpenNote(h.label, h.flavourText);
        }

        private void ShowActionPrompt(CaseAction a)
        {
            OpenModal(a.label, a.description, "نفّذ الإجراء", () =>
            {
                if (Session.PerformAction(a.id))
                {
                    Sfx.Play(Sfx.Cue.Unlock);
                    CloseModal();
                    var granted = Case.GetEvidence(a.grantsEvidence);
                    OpenNote("تم", a.resultText);
                    if (granted != null) _pendingEvidenceAfterNote = granted;
                    RefreshLocation();
                }
            });
        }

        // ------------------------------------------------------------------
        // document modal - every piece of reading in the game comes through here
        // ------------------------------------------------------------------

        private RectTransform _docModal;
        private RectTransform _docColumn;
        private Evidence _pendingEvidenceAfterNote;

        private void BuildDocumentModal()
        {
            _docModal = UIKit.Node("docModal", _root);
            UIKit.Veil("doc_veil", _docModal, CloseModal);

            var frame = UIKit.Node("doc_frame", _docModal);
            frame.offsetMin = new Vector2(Theme.Gutter * 0.6f, 150f);
            frame.offsetMax = new Vector2(-Theme.Gutter * 0.6f, -150f);

            ScrollRect sc;
            _docColumn = UIKit.ScrollView("doc_sv", frame, out sc, 18f, new RectOffset(0, 0, 0, 0));

            _docModal.gameObject.SetActive(false);
        }

        public void CloseModal()
        {
            _docModal.gameObject.SetActive(false);
            Sfx.Play(Sfx.Cue.PaperClose, 0.7f);

            // an action that granted evidence shows the note first, then the document
            if (_pendingEvidenceAfterNote != null)
            {
                var e = _pendingEvidenceAfterNote;
                _pendingEvidenceAfterNote = null;
                OpenEvidenceDocument(e, true);
            }
        }

        private void OpenModalShell()
        {
            Clear(_docColumn);
            Sfx.Play(Sfx.Cue.PaperOpen);
            _docModal.gameObject.SetActive(true);
            _docModal.SetAsLastSibling();
            if (_toast != null) _toast.SetAsLastSibling();
        }

        /// <summary>A short note on paper: flavour text, results, anything not filed.</summary>
        public void OpenNote(string title, string text)
        {
            OpenModalShell();
            var inner = UIKit.PaperCard("note", _docColumn, 36f);

            if (!string.IsNullOrEmpty(title))
            {
                var t = UIKit.Label("t", inner, title, Theme.SizeTitle, Theme.TextOnPaper,
                                    TextAlignmentOptions.TopRight, true, true);
                UIKit.FitHeight(t.rectTransform);
                UIKit.Divider(inner, Theme.PaperEdge, 3f);
            }

            var b = UIKit.Label("b", inner, text, Theme.SizeBody, Theme.TextOnPaper,
                                TextAlignmentOptions.TopRight, true);
            b.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(b.rectTransform);

            UIKit.BtnPrimary("close", _docColumn, "إغلاق", CloseModal, 110f, Theme.SizeBody);
        }

        private void OpenModal(string title, string text, string confirmLabel, System.Action onConfirm)
        {
            OpenModalShell();
            var inner = UIKit.PaperCard("m", _docColumn, 36f);

            var t = UIKit.Label("t", inner, title, Theme.SizeHeading, Theme.TextOnPaper,
                                TextAlignmentOptions.TopRight, true, true);
            t.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(t.rectTransform);
            UIKit.Divider(inner, Theme.PaperEdge, 3f);

            var b = UIKit.Label("b", inner, text, Theme.SizeBody, Theme.TextOnPaper,
                                TextAlignmentOptions.TopRight, true);
            b.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(b.rectTransform);

            UIKit.BtnPrimary("ok", _docColumn, confirmLabel, onConfirm, 118f, Theme.SizeBody);
            UIKit.BtnGhost("cancel", _docColumn, "ليس الآن", CloseModal);
        }

        /// <summary>
        /// The evidence sheet. The proves / does-not-prove split is printed on every
        /// piece, because telling those apart is the skill the case is teaching.
        /// </summary>
        public void OpenEvidenceDocument(Evidence e, bool justCollected)
        {
            if (e == null) return;
            OpenModalShell();

            if (justCollected)
            {
                var tag = UIKit.Label("new", _docColumn, "أُضيف إلى دفتر الأدلة",
                                      Theme.SizeSmall, Theme.Amber, TextAlignmentOptions.Center);
                UIKit.FitHeight(tag.rectTransform);
            }

            // the exhibit itself, before any of the words about it
            var shot = UIKit.Picture("shot", _docColumn, Art.EvidenceIcon(e.icon));
            if (shot != null)
            {
                var sle = shot.gameObject.AddComponent<LayoutElement>();
                sle.minHeight = 560f; sle.preferredHeight = 560f;
                var o = shot.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0f, 0f, 0f, 0.6f);
                o.effectDistance = new Vector2(4f, -4f);
            }

            var inner = UIKit.PaperCard("ev", _docColumn, 36f);

            var name = UIKit.Label("n", inner, e.id + " — " + e.name, Theme.SizeTitle,
                                   Theme.TextOnPaper, TextAlignmentOptions.TopRight, true, true);
            name.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(name.rectTransform);

            var src = UIKit.Label("s", inner, "المصدر: " + e.source, Theme.SizeTiny,
                                  new Color(0.42f, 0.39f, 0.33f, 1f), TextAlignmentOptions.TopRight);
            src.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(src.rectTransform);

            UIKit.Divider(inner, Theme.PaperEdge, 3f);

            var sum = UIKit.Label("sum", inner, e.summary, Theme.SizeBody, Theme.TextOnPaper,
                                  TextAlignmentOptions.TopRight, true);
            sum.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(sum.rectTransform);

            if (!string.IsNullOrEmpty(e.detail))
            {
                UIKit.Spacer(inner, 8f);
                var det = UIKit.Label("det", inner, e.detail, Theme.SizeSmall,
                                      new Color(0.25f, 0.23f, 0.19f, 1f),
                                      TextAlignmentOptions.TopRight, true);
                det.textWrappingMode = TextWrappingModes.Normal;
                UIKit.FitHeight(det.rectTransform);
            }

            AddProofList(_docColumn, "ما يُثبته هذا الدليل", e.proves, Theme.Verified);
            AddProofList(_docColumn, "ما لا يُثبته", e.notProves, Theme.Alert);

            UIKit.BtnPrimary("close", _docColumn, "إغلاق", CloseModal, 110f, Theme.SizeBody);
        }

        private void AddProofList(RectTransform parent, string heading, string[] items, Color accent)
        {
            if (items == null || items.Length == 0) return;

            var card = UIKit.Rect("pl", parent, new Color(accent.r, accent.g, accent.b, 0.13f));
            var o = card.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(accent.r, accent.g, accent.b, 0.55f);
            o.effectDistance = new Vector2(2f, -2f);

            var col = UIKit.Node("c", card.transform);
            UIKit.VStack(col, 10f, new RectOffset(28, 28, 22, 22));
            UIKit.FitHeight(col);

            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childControlHeight = true; v.childControlWidth = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var f = card.gameObject.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var h = UIKit.Label("h", col, heading, Theme.SizeSmall, accent,
                                TextAlignmentOptions.TopRight, false, true);
            UIKit.FitHeight(h.rectTransform);

            foreach (var line in items)
            {
                var l = UIKit.Label("i", col, "•  " + line, Theme.SizeSmall, Theme.TextOnDark);
                l.textWrappingMode = TextWrappingModes.Normal;
                UIKit.FitHeight(l.rectTransform);
            }
        }
    }
}
