using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TrueDetective.Core;

namespace TrueDetective.UI
{
    /// <summary>
    /// The close. Replays the case as a sequence, naming the evidence behind each step,
    /// so the player leaves able to explain the solution rather than just having reached it.
    /// </summary>
    public partial class Game
    {
        private RectTransform _vdBody;

        private void BuildVerdict()
        {
            var s = NewScreen("verdict");
            _vdBody = UIKit.Node("body", s);
        }

        private void RefreshVerdict()
        {
            Clear(_vdBody);
            var rep = Case.report;

            ScrollRect sc;
            var content = UIKit.ScrollView("sv", _vdBody, out sc, 18f,
                new RectOffset((int)Theme.Gutter, (int)Theme.Gutter, 80, 50));

            var closed = UIKit.Label("c", content, "القضية مغلقة", 72f, Theme.Amber,
                                     TextAlignmentOptions.Center, true, true);
            UIKit.FitHeight(closed.rectTransform);

            var title = UIKit.Label("t", content, "«" + Case.title + "»", Theme.SizeHeading,
                                    Theme.TextMuted, TextAlignmentOptions.Center);
            UIKit.FitHeight(title.rectTransform);

            UIKit.Spacer(content, 10f);
            UIKit.Divider(content, new Color(1f, 1f, 1f, 0.18f), 3f);
            UIKit.Spacer(content, 10f);

            var ok = UIKit.Label("ok", content, rep.successText, Theme.SizeBody,
                                 Theme.Verified, TextAlignmentOptions.Center);
            ok.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(ok.rectTransform);

            UIKit.Spacer(content, 16f);

            var h = UIKit.Label("h", content, "إعادة بناء الأحداث", Theme.SizeTitle,
                                Theme.TextOnDark, TextAlignmentOptions.TopRight, true, true);
            UIKit.FitHeight(h.rectTransform);

            // the reconstruction, with its **evidence** markers turned into TMP bold
            var inner = UIKit.PaperCard("recon", content, 36f);
            var body = UIKit.Label("r", inner, MarkupBold(rep.reconstruction), Theme.SizeBody,
                                   Theme.TextOnPaper, TextAlignmentOptions.TopRight, true);
            body.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(body.rectTransform);

            // ---- what the player did ----
            UIKit.Spacer(content, 16f);
            var statsCard = UIKit.Rect("stats", content, Theme.RoomLight);
            var scol = UIKit.Node("sc", statsCard.transform);
            UIKit.VStack(scol, 8f, new RectOffset(28, 28, 22, 22));
            UIKit.FitHeight(scol);
            var sv = statsCard.gameObject.AddComponent<VerticalLayoutGroup>();
            sv.childControlHeight = true; sv.childControlWidth = true;
            sv.childForceExpandWidth = true; sv.childForceExpandHeight = false;
            var sf = statsCard.gameObject.AddComponent<ContentSizeFitter>();
            sf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            AddStat(scol, "الأدلة المجموعة", Session.EvidenceCount + " من " + Session.EvidenceTotal);
            AddStat(scol, "محاولات التقرير", (Session.FailedReportAttempts + 1).ToString());
            int hints = Session.HintsUsed(Stage.Investigate) + Session.HintsUsed(Stage.Connect)
                      + Session.HintsUsed(Stage.Confront) + Session.HintsUsed(Stage.Report);
            AddStat(scol, "التلميحات المستخدمة", hints.ToString());

            // ---- teaser, without building the next case ----
            if (!string.IsNullOrEmpty(Case.epilogue))
            {
                UIKit.Spacer(content, 20f);
                var ep = UIKit.Label("ep", content, Case.epilogue, Theme.SizeBody,
                                     Theme.Amber, TextAlignmentOptions.Center, true);
                ep.textWrappingMode = TextWrappingModes.Normal;
                UIKit.FitHeight(ep.rectTransform);
            }

            UIKit.Spacer(content, 26f);
            UIKit.BtnPrimary("again", content, "أعد اللعب من البداية", () =>
            {
                Sfx.Play(Sfx.Cue.Tap);
                StartCase(caseId);
            }, 130f);
            UIKit.Spacer(content, 12f);
            UIKit.BtnGhost("menu", content, "العودة إلى القائمة", () =>
            {
                Sfx.Play(Sfx.Cue.Back);
                ResetRun();
                Show("menu", false);
            });
            UIKit.Spacer(content, 12f);
            UIKit.BtnGhost("nb", content, "راجع دفتر الأدلة", () => Show("notebook"));
            UIKit.Spacer(content, 20f);
        }

        private void AddStat(RectTransform parent, string label, string value)
        {
            var row = UIKit.Node("row", parent);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 58f; le.preferredHeight = 58f;
            UIKit.HStack(row, 10f);

            var l = UIKit.Label("l", row, label, Theme.SizeSmall, Theme.TextMuted,
                                TextAlignmentOptions.MidlineRight);
            var v = UIKit.Label("v", row, value, Theme.SizeSmall, Theme.Amber,
                                TextAlignmentOptions.MidlineLeft);
        }

        /// <summary>Turns the case file's **markers** into TMP bold tags.</summary>
        private static string MarkupBold(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            var parts = text.Split(new[] { "**" }, System.StringSplitOptions.None);
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < parts.Length; i++)
            {
                if (i % 2 == 1) sb.Append("<b>").Append(parts[i]).Append("</b>");
                else sb.Append(parts[i]);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Clears one playthrough back to nothing, without deciding where to go next.
        ///
        /// CaseSession.Reset alone is not enough: the report answers, the interrogation
        /// transcript and the board selection live in the UI, so anything added there
        /// has to be cleared here too or a replay starts knowing the previous run.
        /// </summary>
        private void ResetRun()
        {
            Session.Reset();
            ResetReportInputs();
            _heard.Clear();
            _pickA = _pickB = null;
            _activeConfrontation = null;
            _cfLine = 0;
            _interrogateCharacterId = null;
            _pendingEvidenceAfterNote = null;
            _history.Clear();

            CloseModal();
            if (_hintModal != null) _hintModal.gameObject.SetActive(false);
        }
    }
}
