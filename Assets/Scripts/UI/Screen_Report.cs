using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TrueDetective.Core;
using TrueDetective.Data;

namespace TrueDetective.UI
{
    /// <summary>
    /// The final report. The player fills every blank from its own word bank, so the
    /// conclusion is written rather than picked, and it cannot be reached by elimination:
    /// seven slots of four options is far too large a space to guess.
    /// </summary>
    public partial class Game
    {
        private RectTransform _rpBody;
        private readonly Dictionary<string, string> _answers = new Dictionary<string, string>();
        private readonly HashSet<string> _cited = new HashSet<string>();
        private string _openSlot;

        private void BuildReport()
        {
            var s = NewScreen("report");
            _rpBody = UIKit.Node("body", s);
        }

        private void RefreshReport()
        {
            Clear(_rpBody);
            TopBar(_rpBody, "تقرير التحقيق", Back);
            BottomBar(_rpBody);
            var body = Body(_rpBody);

            var rep = Case.report;
            ScrollRect sc;
            var content = UIKit.ScrollView("sv", body, out sc, 16f,
                new RectOffset((int)Theme.Gutter, (int)Theme.Gutter, 20, 30));

            var intro = UIKit.Label("i", content, rep.intro, Theme.SizeSmall,
                                    Theme.TextMuted, TextAlignmentOptions.TopRight);
            intro.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(intro.rectTransform);

            // ---- the sentence so far ----
            var inner = UIKit.PaperCard("sheet", content, 34f);
            var sentence = UIKit.Label("s", inner, BuildSentence(rep), Theme.SizeBody,
                                       Theme.TextOnPaper, TextAlignmentOptions.TopRight, true);
            sentence.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(sentence.rectTransform);

            UIKit.Spacer(content, 8f);

            // ---- one row per blank ----
            var h1 = UIKit.Label("h1", content, "املأ الفراغات", Theme.SizeSmall,
                                 Theme.Amber, TextAlignmentOptions.TopRight, false, true);
            UIKit.FitHeight(h1.rectTransform);

            foreach (var slot in rep.slots)
            {
                var s = slot;
                string chosen;
                bool filled = _answers.TryGetValue(s.key, out chosen) && !string.IsNullOrEmpty(chosen);

                UIKit.Btn("slot_" + s.key, content,
                    SlotLabel(s.key) + ":   " + (filled ? chosen : "— اختر —"),
                    () => { _openSlot = (_openSlot == s.key) ? null : s.key; RefreshReport(); },
                    filled ? new Color(0.35f, 0.62f, 0.44f, 0.18f) : Theme.RoomLight,
                    filled ? Theme.TextOnDark : Theme.TextMuted,
                    Theme.SizeSmall, 108f);

                if (_openSlot == s.key)
                {
                    foreach (var opt in s.options)
                    {
                        var option = opt;
                        UIKit.Btn("o_" + s.key + "_" + option, content, option, () =>
                        {
                            _answers[s.key] = option;
                            _openSlot = null;
                            RefreshReport();
                        }, new Color(1f, 1f, 1f, 0.05f), Theme.TextOnDark, Theme.SizeSmall, 96f);
                    }
                }
            }

            // ---- evidence citation ----
            UIKit.Spacer(content, 14f);
            UIKit.Divider(content);
            UIKit.Spacer(content, 6f);

            var h2 = UIKit.Label("h2", content, "الأدلة الداعمة", Theme.SizeSmall,
                                 Theme.Amber, TextAlignmentOptions.TopRight, false, true);
            UIKit.FitHeight(h2.rectTransform);

            var prompt = UIKit.Label("p", content, rep.evidencePrompt, Theme.SizeTiny,
                                     Theme.TextMuted, TextAlignmentOptions.TopRight);
            prompt.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(prompt.rectTransform);

            if (Session.EvidenceCount == 0)
            {
                var none = UIKit.Label("n", content, "لم تجمع أي دليل بعد.",
                                       Theme.SizeSmall, Theme.TextMuted, TextAlignmentOptions.Center);
                UIKit.FitHeight(none.rectTransform);
            }

            foreach (var e in Session.CollectedEvidence)
            {
                var ev = e;
                bool on = _cited.Contains(ev.id);
                UIKit.Btn("cite_" + ev.id, content,
                    (on ? "✓  " : "◯  ") + ev.id + " — " + ev.name,
                    () =>
                    {
                        if (!_cited.Remove(ev.id)) _cited.Add(ev.id);
                        RefreshReport();
                    },
                    on ? new Color(0.35f, 0.62f, 0.44f, 0.20f) : Theme.RoomLight,
                    on ? Theme.TextOnDark : Theme.TextMuted,
                    Theme.SizeSmall, 104f);
            }

            // ---- submit ----
            UIKit.Spacer(content, 18f);
            int filledCount = 0;
            foreach (var s in rep.slots)
            {
                string v;
                if (_answers.TryGetValue(s.key, out v) && !string.IsNullOrEmpty(v)) filledCount++;
            }

            bool ready = filledCount == rep.slots.Length && _cited.Count > 0;
            var submit = UIKit.BtnPrimary("submit", content,
                ready ? "أغلق القضية" : "أكمل الفراغات واختر الأدلة (" + filledCount + "/" + rep.slots.Length + ")",
                ready ? (System.Action)Submit : null, 132f, Theme.SizeBody);
            submit.interactable = ready;

            UIKit.Spacer(content, 10f);
            var safety = UIKit.Label("sf", content,
                "الخطأ هنا لا يُنهي القضية ولا يُلغي تقدمك.",
                Theme.SizeTiny, Theme.TextMuted, TextAlignmentOptions.Center);
            UIKit.FitHeight(safety.rectTransform);
        }

        /// <summary>Renders the template, showing filled values and leaving blanks visible.</summary>
        private string BuildSentence(Report rep)
        {
            var sb = new StringBuilder(rep.template);
            foreach (var s in rep.slots)
            {
                string v;
                bool has = _answers.TryGetValue(s.key, out v) && !string.IsNullOrEmpty(v);
                sb.Replace("{" + s.key + "}", has ? "«" + v + "»" : "(......)");
            }
            return sb.ToString();
        }

        private static string SlotLabel(string key)
        {
            switch (key)
            {
                case "t_place":   return "وقت الوضع في الصندوق";
                case "who":       return "من";
                case "code":      return "رمز المخطوطة";
                case "container": return "أين أُخفيت";
                case "where":     return "المكان";
                case "t_exit":    return "وقت المغادرة";
                case "permit":    return "المخالفة";
                default:          return key;
            }
        }

        private void Submit()
        {
            var result = Session.SubmitReport(_answers, _cited);

            if (result.Accepted)
            {
                Sfx.Play(Sfx.Cue.Close);
                // attempts = the failures before this one, plus the one that landed
                GameSettings.MarkSolved(Case.id, Session.FailedReportAttempts + 1);
                Show("verdict");
                return;
            }

            Sfx.Play(Sfx.Cue.Wrong);

            if (result.PrematureAccusation)
            {
                OpenNote("ليس بعد", result.Message +
                    "\n\nثبوت أن رواية شخص غير صحيحة لا يثبت أنه ارتكب الفعل. ابحث عمّا يربطه بالمخطوطة نفسها.");
                return;
            }

            string detail = result.Message;
            detail += "\n\nصحيح " + result.CorrectSlots + " من " + result.TotalSlots + " من عناصر الخلاصة.";
            if (result.MissingEvidence.Length > 0)
                detail += "\n\nوالخلاصة تحتاج دليلًا لا تملكه أو لم تستشهد به بعد.";

            OpenNote("التقرير غير مكتمل", detail);
        }

        /// <summary>Clears the report so a replay does not start with the last run's answers.</summary>
        private void ResetReportInputs()
        {
            _answers.Clear();
            _cited.Clear();
            _openSlot = null;
        }
    }
}
