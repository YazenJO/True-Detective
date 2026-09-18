using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace TrueDetective.UI
{
    /// <summary>
    /// The newspaper and the briefing: the two beats between the menu and the first
    /// hotspot. The title screen lives in Screen_Menu.cs now.
    /// </summary>
    public partial class Game
    {
        private RectTransform _newspaperBody;
        private RectTransform _briefingBody;

        // ------------------------------------------------------------------
        // newspaper
        // ------------------------------------------------------------------

        private void BuildNewspaper()
        {
            var s = NewScreen("newspaper");
            _newspaperBody = UIKit.Node("body", s);
        }

        private void RefreshNewspaper()
        {
            Clear(_newspaperBody);
            var n = Case.newspaper;

            ScrollRect sc;
            var content = UIKit.ScrollView("sv", _newspaperBody, out sc, 0f,
                new RectOffset((int)Theme.Gutter, (int)Theme.Gutter, 60, 60));

            var inner = UIKit.PaperCard("paper", content, 40f);

            // masthead
            var mast = UIKit.Label("mast", inner, n.masthead, 58f, Theme.TextOnPaper,
                                   TextAlignmentOptions.Center, true, true);
            UIKit.FitHeight(mast.rectTransform);

            var date = UIKit.Label("date", inner, n.date, Theme.SizeTiny,
                                   new Color(0.4f, 0.37f, 0.31f, 1f), TextAlignmentOptions.Center);
            UIKit.FitHeight(date.rectTransform);

            UIKit.Divider(inner, Theme.PaperEdge, 4f);
            UIKit.Spacer(inner, 12f);

            // headline
            var head = UIKit.Label("head", inner, n.headline, Theme.SizeDisplay,
                                   Theme.TextOnPaper, TextAlignmentOptions.TopRight, true, true);
            head.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(head.rectTransform);

            UIKit.Spacer(inner, 10f);
            UIKit.Divider(inner, Theme.PaperEdge, 2f);
            UIKit.Spacer(inner, 10f);

            // a grey block standing in for the photo, with its caption
            var photo = UIKit.Rect("photo", inner, new Color(0.72f, 0.69f, 0.62f, 1f), false);
            var ple = photo.gameObject.AddComponent<LayoutElement>();
            ple.minHeight = 300f; ple.preferredHeight = 300f;

            var cap = UIKit.Label("cap", inner, n.caption, Theme.SizeTiny,
                                  new Color(0.42f, 0.39f, 0.33f, 1f), TextAlignmentOptions.TopRight, true);
            cap.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(cap.rectTransform);

            UIKit.Spacer(inner, 14f);

            var body = UIKit.Label("body", inner, n.body, Theme.SizeBody,
                                   Theme.TextOnPaper, TextAlignmentOptions.TopRight, true);
            body.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(body.rectTransform);

            UIKit.Spacer(content, 30f);

            // the hook: name the question before handing over any control
            var hook = UIKit.Label("hook", content,
                "لا نافذة مكسورة. لا قفل مخلوع.\nإذًا من دخل، دخل كما يدخل كل يوم.",
                Theme.SizeHeading, Theme.Amber, TextAlignmentOptions.Center);
            hook.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(hook.rectTransform);

            UIKit.Spacer(content, 30f);

            UIKit.BtnPrimary("next", content, "وصلك تكليف", () => Show("briefing"), 130f);
            UIKit.Spacer(content, 20f);
        }

        // ------------------------------------------------------------------
        // briefing
        // ------------------------------------------------------------------

        private void BuildBriefing()
        {
            var s = NewScreen("briefing");
            _briefingBody = UIKit.Node("body", s);
        }

        private void RefreshBriefing()
        {
            Clear(_briefingBody);

            ScrollRect sc;
            var content = UIKit.ScrollView("sv", _briefingBody, out sc, 0f,
                new RectOffset((int)Theme.Gutter, (int)Theme.Gutter, 80, 60));

            var header = UIKit.Label("h", content, "التكليف", Theme.SizeTitle,
                                     Theme.Amber, TextAlignmentOptions.Center);
            UIKit.FitHeight(header.rectTransform);
            UIKit.Spacer(content, 24f);

            var inner = UIKit.PaperCard("card", content, 40f);
            var body = UIKit.Label("t", inner, Case.briefing, Theme.SizeBody,
                                   Theme.TextOnPaper, TextAlignmentOptions.TopRight, true);
            body.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(body.rectTransform);

            UIKit.Spacer(content, 34f);

            var howTo = UIKit.Label("how", content,
                "افحص ما في المكان، واسأل من كان فيه.\nثم اربط ما يتعارض، وواجه به صاحبه.",
                Theme.SizeSmall, Theme.TextMuted, TextAlignmentOptions.Center);
            howTo.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(howTo.rectTransform);

            UIKit.Spacer(content, 30f);
            UIKit.BtnPrimary("go", content, "اذهب إلى دار الأرشيف", () =>
            {
                Session.TravelTo("archive_hall");
                Show("location");
            }, 140f);
            UIKit.Spacer(content, 20f);
        }
    }
}
