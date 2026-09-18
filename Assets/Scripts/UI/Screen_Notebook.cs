using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TrueDetective.Data;

namespace TrueDetective.UI
{
    /// <summary>The evidence notebook: everything filed, with what each piece carries.</summary>
    public partial class Game
    {
        private RectTransform _nbBody;

        private void BuildNotebook()
        {
            var s = NewScreen("notebook");
            _nbBody = UIKit.Node("body", s);
        }

        private void RefreshNotebook()
        {
            Clear(_nbBody);
            TopBar(_nbBody, "دفتر الأدلة", Back);
            BottomBar(_nbBody);
            var body = Body(_nbBody);

            ScrollRect sc;
            var content = UIKit.ScrollView("sv", body, out sc, 18f,
                new RectOffset((int)Theme.Gutter, (int)Theme.Gutter, 24, 30));

            if (Session.EvidenceCount == 0)
            {
                var empty = UIKit.Label("empty", content,
                    "الدفتر فارغ.\n\nافحص ما في المكان، واسأل من كان فيه.",
                    Theme.SizeBody, Theme.TextMuted, TextAlignmentOptions.Center);
                empty.textWrappingMode = TextWrappingModes.Normal;
                UIKit.FitHeight(empty.rectTransform);
                return;
            }

            foreach (var e in Session.CollectedEvidence)
                AddEvidenceRow(content, e);

            UIKit.Spacer(content, 10f);

            var note = UIKit.Label("note", content,
                "ما يُثبته الدليل، وما لا يُثبته، مكتوبان على كل ورقة. اقرأ الاثنين.",
                Theme.SizeTiny, Theme.TextMuted, TextAlignmentOptions.Center);
            note.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(note.rectTransform);
        }

        /// <summary>
        /// One row per piece of evidence. The kind badge matters: a statement and a
        /// recording are not the same weight of proof, and the list should say so before
        /// the player opens anything.
        /// </summary>
        private void AddEvidenceRow(RectTransform parent, Evidence e)
        {
            var card = UIKit.Rect("ev_" + e.id, parent, Theme.RoomLight);

            var btn = card.gameObject.AddComponent<Button>();
            btn.targetGraphic = card;
            btn.onClick.AddListener(() => OpenEvidenceDocument(e, false));

            var col = UIKit.Node("c", card.transform);
            UIKit.VStack(col, 8f, new RectOffset(28, 28, 22, 22));
            UIKit.FitHeight(col);

            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childControlHeight = true; v.childControlWidth = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var f = card.gameObject.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var head = UIKit.Label("h", col, e.id + " — " + e.name, Theme.SizeHeading,
                                   Theme.TextOnDark, TextAlignmentOptions.TopRight, false, true);
            head.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(head.rectTransform);
            UIKit.NoRaycast(head);

            var badge = UIKit.Label("k", col, KindLabel(e.kind), Theme.SizeTiny,
                                    KindColor(e.kind), TextAlignmentOptions.TopRight);
            UIKit.FitHeight(badge.rectTransform);
            UIKit.NoRaycast(badge);

            var sum = UIKit.Label("s", col, e.summary, Theme.SizeSmall, Theme.TextMuted);
            sum.textWrappingMode = TextWrappingModes.Normal;
            sum.overflowMode = TextOverflowModes.Ellipsis;
            UIKit.FitHeight(sum.rectTransform);
            UIKit.NoRaycast(sum);
        }

        private static string KindLabel(string kind)
        {
            switch (kind)
            {
                case "record":    return "مستند رسمي · يوثّق واقعة";
                case "statement": return "إفادة · ادّعاء يحتاج إثباتًا";
                case "footage":   return "تسجيل · يوثّق ما ظهر أمام العدسة";
                case "physical":  return "دليل مادي";
                default:          return "دليل";
            }
        }

        private static Color KindColor(string kind)
        {
            switch (kind)
            {
                case "record":    return Theme.Verified;
                case "statement": return Theme.Alert;
                case "footage":   return Theme.Amber;
                default:          return Theme.TextMuted;
            }
        }
    }
}
