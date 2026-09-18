using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TrueDetective.Core;
using TrueDetective.Data;

namespace TrueDetective.UI
{
    /// <summary>
    /// Interrogation. Few, specific questions; answers stay on screen once read, so a
    /// player can re-read a statement without asking again and without a transition.
    /// </summary>
    public partial class Game
    {
        private RectTransform _intBody;
        private string _interrogateCharacterId;

        /// <summary>Answers already heard this session, so returning shows them again.</summary>
        private readonly Dictionary<string, string> _heard = new Dictionary<string, string>();

        private void BuildInterrogation()
        {
            var s = NewScreen("interrogate");
            _intBody = UIKit.Node("body", s);
        }

        private void RefreshInterrogation()
        {
            Clear(_intBody);
            var c = Case.GetCharacter(_interrogateCharacterId);
            if (c == null) { Show(InvestigationScreen, false); return; }

            TopBar(_intBody, c.name + " — " + c.role, Back);
            BottomBar(_intBody);
            var body = Body(_intBody);

            ScrollRect sc;
            var content = UIKit.ScrollView("sv", body, out sc, 16f,
                new RectOffset((int)Theme.Gutter, (int)Theme.Gutter, 22, 30));

            // ---- portrait ----
            // A confronted Samer gets his second portrait: the change of face is the
            // clearest possible signal that the player's work landed.
            string portraitName = c.portrait;
            if (Session.HasConfrontedCharacter(c.id))
            {
                var alt = Art.Portrait(c.portrait + "_broken");
                if (alt != null) portraitName = c.portrait + "_broken";
            }

            var portrait = UIKit.Picture("portrait", content, Art.Portrait(portraitName));
            if (portrait != null)
            {
                var ple = portrait.gameObject.AddComponent<LayoutElement>();
                ple.minHeight = 520f; ple.preferredHeight = 520f;

                var frame = portrait.gameObject.AddComponent<Outline>();
                frame.effectColor = new Color(Theme.Amber.r, Theme.Amber.g, Theme.Amber.b, 0.35f);
                frame.effectDistance = new Vector2(3f, -3f);
            }

            // ---- greeting ----
            var top = UIKit.Rect("greet", content, Theme.RoomLight);
            var gcol = UIKit.Node("gc", top.transform);
            UIKit.VStack(gcol, 12f, new RectOffset(28, 28, 24, 24));
            UIKit.FitHeight(gcol);
            var gv = top.gameObject.AddComponent<VerticalLayoutGroup>();
            gv.childControlHeight = true; gv.childControlWidth = true;
            gv.childForceExpandWidth = true; gv.childForceExpandHeight = false;
            var gf = top.gameObject.AddComponent<ContentSizeFitter>();
            gf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            bool confronted = Session.HasConfrontedCharacter(c.id);
            if (confronted)
            {
                var state = UIKit.Label("st", gcol, "بعد المواجهة", Theme.SizeTiny,
                                        Theme.Alert, TextAlignmentOptions.TopRight);
                UIKit.FitHeight(state.rectTransform);
            }

            var greet = UIKit.Label("g", gcol, "«" + Session.GreetingFor(c.id) + "»",
                                    Theme.SizeBody, Theme.TextOnDark);
            greet.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(greet.rectTransform);

            // ---- the confrontation, when it has been earned ----
            var pending = Session.PendingConfrontation(c.id);
            if (pending != null)
            {
                UIKit.Spacer(content, 6f);
                var cf = UIKit.Btn("confront", content, "واجهه بما ثبت", () =>
                {
                    Sfx.Play(Sfx.Cue.Confront);
                    _activeConfrontation = pending;
                    Show("confront");
                }, Theme.Alert, Color.white, Theme.SizeHeading, 132f);
                var o = cf.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(1f, 1f, 1f, 0.5f);
                o.effectDistance = new Vector2(2f, -2f);
                UIKit.Spacer(content, 6f);
            }

            // ---- questions ----
            var qs = new List<Question>(Session.AvailableQuestions(c.id));
            if (qs.Count == 0)
            {
                var none = UIKit.Label("none", content,
                    "لا شيء آخر تسأله عنه الآن.",
                    Theme.SizeSmall, Theme.TextMuted, TextAlignmentOptions.Center);
                UIKit.FitHeight(none.rectTransform);
            }

            foreach (var q in qs)
            {
                var question = q;
                bool asked = Session.HasAsked(question.id);

                var btn = UIKit.Btn("q_" + question.id, content, question.text, () =>
                {
                    Session.AskQuestion(c.id, question);
                    _heard[question.id] = Session.AnswerFor(c.id, question);
                    RefreshInterrogation();
                },
                asked ? new Color(1f, 1f, 1f, 0.05f) : Theme.RoomLight,
                asked ? Theme.TextMuted : Theme.TextOnDark,
                Theme.SizeSmall, 118f);

                if (!asked)
                {
                    var o = btn.gameObject.AddComponent<Outline>();
                    o.effectColor = new Color(Theme.Amber.r, Theme.Amber.g, Theme.Amber.b, 0.45f);
                    o.effectDistance = new Vector2(2f, -2f);
                }

                // the answer stays under its question once heard
                string heard;
                if (asked && _heard.TryGetValue(question.id, out heard) && !string.IsNullOrEmpty(heard))
                    AddAnswerBubble(content, c.name, heard);
            }
        }

        private void AddAnswerBubble(RectTransform parent, string speaker, string text)
        {
            var card = UIKit.Rect("ans", parent, new Color(0.88f, 0.63f, 0.24f, 0.10f));
            var o = card.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.88f, 0.63f, 0.24f, 0.35f);
            o.effectDistance = new Vector2(2f, -2f);

            var col = UIKit.Node("c", card.transform);
            UIKit.VStack(col, 8f, new RectOffset(28, 28, 20, 20));
            UIKit.FitHeight(col);
            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childControlHeight = true; v.childControlWidth = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var f = card.gameObject.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var who = UIKit.Label("w", col, speaker, Theme.SizeTiny, Theme.Amber);
            UIKit.FitHeight(who.rectTransform);

            var t = UIKit.Label("t", col, "«" + text + "»", Theme.SizeBody, Theme.TextOnDark);
            t.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(t.rectTransform);
        }
    }
}
