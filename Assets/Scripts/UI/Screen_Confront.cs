using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TrueDetective.Core;
using TrueDetective.Data;

namespace TrueDetective.UI
{
    /// <summary>
    /// The confrontation: the beat where the player's work changes someone's story.
    /// Lines advance on tap, never on a timer, and the whole exchange can be skipped
    /// to the end for a replay.
    /// </summary>
    public partial class Game
    {
        private RectTransform _cfBody;
        private Confrontation _activeConfrontation;
        private int _cfLine;

        private void BuildConfrontation()
        {
            var s = NewScreen("confront");
            _cfBody = UIKit.Node("body", s);
        }

        private void RefreshConfrontation()
        {
            Clear(_cfBody);
            var cf = _activeConfrontation;
            if (cf == null || cf.dialogue == null || cf.dialogue.Length == 0)
            {
                Show("location", false);
                return;
            }

            var who = Case.GetCharacter(cf.characterId);
            TopBar(_cfBody, "مواجهة — " + (who != null ? who.name : ""), null, false);

            var body = Body(_cfBody, 130f, 200f);

            ScrollRect sc;
            var content = UIKit.ScrollView("sv", body, out sc, 14f,
                new RectOffset((int)Theme.Gutter, (int)Theme.Gutter, 20, 20));

            // the evidence being put to them, kept visible throughout
            if (cf.evidenceToPresent != null && cf.evidenceToPresent.Length > 0)
            {
                string cited = "";
                foreach (var id in cf.evidenceToPresent)
                {
                    var e = Case.GetEvidence(id);
                    if (e == null) continue;
                    if (cited.Length > 0) cited += "  ·  ";
                    cited += e.id + " " + e.name;
                }
                var tag = UIKit.Label("cited", content, "تواجهه بـ:  " + cited,
                                      Theme.SizeTiny, Theme.Amber, TextAlignmentOptions.Center);
                tag.textWrappingMode = TextWrappingModes.Normal;
                UIKit.FitHeight(tag.rectTransform);
                UIKit.Divider(content);
            }

            int shown = Mathf.Min(_cfLine + 1, cf.dialogue.Length);
            for (int i = 0; i < shown; i++)
                AddDialogueLine(content, cf.dialogue[i], who);

            // ---- controls ----
            var bar = UIKit.Node("cfbar", _cfBody);
            bar.anchorMin = new Vector2(0f, 0f);
            bar.anchorMax = new Vector2(1f, 0f);
            bar.pivot = new Vector2(0.5f, 0f);
            bar.sizeDelta = new Vector2(0f, 200f);
            bar.anchoredPosition = Vector2.zero;
            var bg = bar.gameObject.AddComponent<Image>();
            bg.sprite = UIKit.Solid;
            bg.color = new Color(0f, 0f, 0f, 0.6f);

            var col = UIKit.Node("col", bar);
            col.offsetMin = new Vector2(Theme.Gutter, 20f);
            col.offsetMax = new Vector2(-Theme.Gutter, -20f);
            UIKit.VStack(col, 12f);

            bool finished = _cfLine >= cf.dialogue.Length - 1;

            if (!finished)
            {
                UIKit.BtnPrimary("next", col, "تابع", () =>
                {
                    _cfLine++;
                    RefreshConfrontation();
                    sc.verticalNormalizedPosition = 0f;
                }, 110f, Theme.SizeBody);

                UIKit.BtnGhost("skip", col, "تجاوز الحوار", () =>
                {
                    _cfLine = cf.dialogue.Length - 1;
                    RefreshConfrontation();
                }, 74f, Theme.SizeTiny);
            }
            else
            {
                UIKit.BtnPrimary("end", col, "أنهِ المواجهة", () =>
                {
                    Session.CompleteConfrontation(cf);
                    _activeConfrontation = null;
                    _cfLine = 0;
                    OpenNote("ما تغيّر", cf.outcomeNote);
                    Show("location", false);
                }, 120f, Theme.SizeBody);
            }
        }

        private void AddDialogueLine(RectTransform parent, DialogueLine line, Character other)
        {
            bool isDetective = line.speaker == "detective";
            string speaker = isDetective ? "المحقق" : (other != null ? other.name : line.speaker);

            Color fill = isDetective
                ? new Color(1f, 1f, 1f, 0.06f)
                : MoodColor(line.mood);

            var card = UIKit.Rect("dl", parent, fill);
            var o = card.gameObject.AddComponent<Outline>();
            o.effectColor = isDetective
                ? new Color(1f, 1f, 1f, 0.16f)
                : new Color(MoodEdge(line.mood).r, MoodEdge(line.mood).g, MoodEdge(line.mood).b, 0.5f);
            o.effectDistance = new Vector2(2f, -2f);

            var col = UIKit.Node("c", card.transform);
            UIKit.VStack(col, 8f, new RectOffset(28, 28, 20, 20));
            UIKit.FitHeight(col);
            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childControlHeight = true; v.childControlWidth = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var f = card.gameObject.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // speaker row: a small face beside the name, so a long exchange stays readable
            var head = UIKit.Node("head", col);
            var hle = head.gameObject.AddComponent<LayoutElement>();
            hle.minHeight = 76f; hle.preferredHeight = 76f;
            UIKit.HStack(head, 12f);

            // once he breaks, Samer's portrait changes with him
            string face = isDetective ? "pt_detective"
                : (other != null
                    ? (line.mood == "breaking" && Art.Portrait(other.portrait + "_broken") != null
                        ? other.portrait + "_broken" : other.portrait)
                    : null);

            var pic = UIKit.Picture("face", head, Art.Portrait(face));
            if (pic != null)
            {
                var ple = pic.gameObject.AddComponent<LayoutElement>();
                ple.minWidth = 76f; ple.preferredWidth = 76f; ple.flexibleWidth = 0f;
                ple.minHeight = 76f; ple.preferredHeight = 76f;
            }

            var w = UIKit.Label("w", head, speaker, Theme.SizeSmall,
                                isDetective ? Theme.TextMuted : MoodEdge(line.mood),
                                TextAlignmentOptions.MidlineRight);
            UIKit.NoRaycast(w);

            var t = UIKit.Label("t", col, line.text, Theme.SizeBody, Theme.TextOnDark);
            t.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(t.rectTransform);
        }

        private static Color MoodColor(string mood)
        {
            switch (mood)
            {
                case "tense":    return new Color(0.79f, 0.29f, 0.25f, 0.12f);
                case "breaking": return new Color(0.79f, 0.29f, 0.25f, 0.22f);
                default:         return new Color(0.35f, 0.62f, 0.44f, 0.10f);
            }
        }

        private static Color MoodEdge(string mood)
        {
            switch (mood)
            {
                case "tense":    return Theme.Alert;
                case "breaking": return new Color(1f, 0.45f, 0.38f, 1f);
                default:         return Theme.Verified;
            }
        }
    }
}
