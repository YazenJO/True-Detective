using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TrueDetective.Core;
using TrueDetective.Data;

namespace TrueDetective.UI
{
    /// <summary>
    /// The deduction board. Two taps pick the pair, a third picks the relation - no
    /// dragging, which on a phone is both fiddly and slower than it looks.
    /// </summary>
    public partial class Game
    {
        private RectTransform _cnBody;
        private string _pickA, _pickB;

        private void BuildConnect()
        {
            var s = NewScreen("connect");
            _cnBody = UIKit.Node("body", s);
        }

        private void RefreshConnect()
        {
            Clear(_cnBody);
            TopBar(_cnBody, "لوحة الربط", Back);
            BottomBar(_cnBody);
            var body = Body(_cnBody);

            ScrollRect sc;
            var content = UIKit.ScrollView("sv", body, out sc, 14f,
                new RectOffset((int)Theme.Gutter, (int)Theme.Gutter, 20, 30));

            if (Session.EvidenceCount < 2)
            {
                var msg = UIKit.Label("m", content,
                    "تحتاج دليلين على الأقل قبل أن تربط بينهما.",
                    Theme.SizeBody, Theme.TextMuted, TextAlignmentOptions.Center);
                msg.textWrappingMode = TextWrappingModes.Normal;
                UIKit.FitHeight(msg.rectTransform);
                return;
            }

            // ---- what has already been established ----
            var proven = new List<Contradiction>();
            if (Case.contradictions != null)
                foreach (var c in Case.contradictions)
                    if (c != null && Session.HasProven(c.id)) proven.Add(c);

            if (proven.Count > 0)
            {
                var h = UIKit.Label("ph", content, "ما ثبت", Theme.SizeSmall,
                                    Theme.Verified, TextAlignmentOptions.TopRight, false, true);
                UIKit.FitHeight(h.rectTransform);

                foreach (var c in proven)
                {
                    var card = UIKit.Rect("pc", content, new Color(Theme.Verified.r, Theme.Verified.g, Theme.Verified.b, 0.14f));
                    var o = card.gameObject.AddComponent<Outline>();
                    o.effectColor = new Color(Theme.Verified.r, Theme.Verified.g, Theme.Verified.b, 0.6f);
                    o.effectDistance = new Vector2(2f, -2f);
                    var l = UIKit.Label("l", card.transform, "✓  " + c.claim,
                                        Theme.SizeSmall, Theme.TextOnDark);
                    UIKit.Stretch(l.rectTransform, 24f);
                    l.textWrappingMode = TextWrappingModes.Normal;
                    var le = card.gameObject.AddComponent<LayoutElement>();
                    le.minHeight = 110f; le.preferredHeight = 110f;
                }
                UIKit.Spacer(content, 12f);
            }

            // ---- instruction ----
            string step = _pickA == null
                ? "اختر الدليل الأول."
                : (_pickB == null ? "اختر الدليل الثاني." : "حدّد العلاقة بينهما.");

            var instr = UIKit.Label("i", content, step, Theme.SizeBody,
                                    Theme.Amber, TextAlignmentOptions.Center);
            UIKit.FitHeight(instr.rectTransform);
            UIKit.Spacer(content, 6f);

            // ---- evidence chips ----
            foreach (var e in Session.CollectedEvidence)
            {
                var ev = e;
                bool selected = ev.id == _pickA || ev.id == _pickB;

                var btn = UIKit.Btn("c_" + ev.id, content, ev.id + " — " + ev.name, () =>
                {
                    TogglePick(ev.id);
                    RefreshConnect();
                },
                selected ? Theme.Amber : Theme.RoomLight,
                selected ? Theme.Ink : Theme.TextOnDark,
                Theme.SizeSmall, 112f);
            }

            // ---- relation buttons, only once a pair is chosen ----
            if (_pickA != null && _pickB != null)
            {
                UIKit.Spacer(content, 14f);
                UIKit.Divider(content);
                UIKit.Spacer(content, 8f);

                AddRelation(content, "contradicts", "يناقض");
                AddRelation(content, "supports",    "يدعم");
                AddRelation(content, "explains",    "يفسّر");

                UIKit.Spacer(content, 8f);
                UIKit.BtnGhost("clear", content, "ألغِ الاختيار", () =>
                {
                    _pickA = _pickB = null;
                    RefreshConnect();
                });
            }
        }

        private void TogglePick(string id)
        {
            if (_pickA == id) { _pickA = _pickB; _pickB = null; return; }
            if (_pickB == id) { _pickB = null; return; }
            if (_pickA == null) { _pickA = id; return; }
            if (_pickB == null) { _pickB = id; return; }
            // both taken: the newest pick replaces the older one
            _pickA = _pickB;
            _pickB = id;
        }

        private void AddRelation(RectTransform parent, string relation, string label)
        {
            UIKit.Btn("rel_" + relation, parent, label, () => Assert(relation),
                      Theme.RoomLight, Theme.TextOnDark, Theme.SizeHeading, 120f);
        }

        /// <summary>
        /// Tests the assertion. A wrong one costs nothing but is never dressed up as
        /// progress - it sends the player back to what each piece actually proves.
        /// </summary>
        private void Assert(string relation)
        {
            Contradiction matched;
            var result = Session.TryConnect(_pickA, _pickB, relation, out matched);

            switch (result)
            {
                case ConnectResult.Correct:
                    _pickA = _pickB = null;
                    Sfx.Play(Sfx.Cue.Reveal);
                    OpenNote("ثبت التناقض", matched.successText);
                    RefreshConnect();
                    break;

                case ConnectResult.AlreadyKnown:
                    _pickA = _pickB = null;
                    OpenNote("معروف", "هذه العلاقة مثبتة بالفعل. تابع من حيث تركت.");
                    RefreshConnect();
                    break;

                                default:
                    Sfx.Play(Sfx.Cue.Wrong);
                    OpenNote("لا تصح هذه العلاقة",
                        "هذه المعلومات لا تدعم العلاقة المختارة. راجع ما يثبته كل دليل.");
                    break;
            }
        }
    }
}
