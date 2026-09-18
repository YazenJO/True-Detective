using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TrueDetective.Core;

namespace TrueDetective.UI
{
    /// <summary>
    /// The lobby: main menu, case select, settings and the case archive. All four share
    /// the same painted backdrop and header so moving between them feels like one place
    /// rather than four screens.
    /// </summary>
    public partial class Game
    {
        private RectTransform _menuBody, _casesBody, _setBody, _codexBody;

        // ------------------------------------------------------------------
        // shared lobby chrome
        // ------------------------------------------------------------------

        /// <summary>The menu backdrop with its scrim. Returns the content column.</summary>
        private RectTransform LobbyFrame(RectTransform parent, out ScrollRect scroll,
                                         string title, Action onBack)
        {
            var bg = UIKit.Picture("bg", parent, Art.Background("bg_menu"), false);
            if (bg != null) UIKit.Stretch(bg.rectTransform);
            UIKit.Scrim(parent, 0.88f, 0.94f);

            if (!string.IsNullOrEmpty(title)) TopBar(parent, title, onBack, false);

            var body = Body(parent, string.IsNullOrEmpty(title) ? 0f : 130f, 0f);
            return UIKit.ScrollView("sv", body, out scroll, 16f,
                new RectOffset((int)Theme.Gutter, (int)Theme.Gutter, 30, 40));
        }

        // ------------------------------------------------------------------
        // main menu
        // ------------------------------------------------------------------

        private void BuildMenu()
        {
            var s = NewScreen("menu");
            _menuBody = UIKit.Node("body", s);
        }

        private void RefreshMenu()
        {
            Clear(_menuBody);
            ScrollRect sc;
            var col = LobbyFrame(_menuBody, out sc, null, null);

            UIKit.Spacer(col, 40f);

            var logo = UIKit.Picture("logo", col, Art.BrandArt("logo"));
            if (logo != null)
            {
                var le = logo.gameObject.AddComponent<LayoutElement>();
                le.minHeight = 420f; le.preferredHeight = 420f;
            }
            else
            {
                var t = UIKit.Label("t", col, "DETECT", 96f, Theme.Paper,
                                    TextAlignmentOptions.Center, true, true);
                UIKit.FitHeight(t.rectTransform);
            }

            var tag = UIKit.Label("tag", col, "كل استنتاج يحتاج دليلًا يحمله",
                                  Theme.SizeSmall, Theme.Amber, TextAlignmentOptions.Center);
            UIKit.FitHeight(tag.rectTransform);

            UIKit.Spacer(col, 40f);

            bool solved = GameSettings.IsSolved(caseId);
            MenuButton(col, solved ? "العب مرة أخرى" : "ابدأ التحقيق", true, () =>
            {
                Sfx.Play(Sfx.Cue.Tap);
                StartCase(caseId);
            });

            MenuButton(col, "القضايا", false, () => { Sfx.Play(Sfx.Cue.Tap); Show("cases"); });
            MenuButton(col, "الأرشيف", false, () => { Sfx.Play(Sfx.Cue.Tap); Show("codex"); });
            MenuButton(col, "الإعدادات", false, () => { Sfx.Play(Sfx.Cue.Tap); Show("settings"); });

            UIKit.Spacer(col, 26f);
            var credit = UIKit.Label("c", col,
                "بروتوتايب · قضية واحدة\nكل الأسماء والوقائع من نسج الخيال",
                Theme.SizeTiny, new Color(1f, 1f, 1f, 0.32f), TextAlignmentOptions.Center);
            credit.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(credit.rectTransform);
            UIKit.Spacer(col, 30f);
        }

        private void MenuButton(RectTransform parent, string label, bool primary, Action onClick)
        {
            if (primary) UIKit.BtnPrimary("mb_" + label, parent, label, onClick, 136f, Theme.SizeHeading);
            else UIKit.Btn("mb_" + label, parent, label, onClick,
                           new Color(1f, 1f, 1f, 0.07f), Theme.TextOnDark, Theme.SizeBody, 112f);
        }

        /// <summary>Clears any previous run and drops the player into the newspaper.</summary>
        private void StartCase(string id)
        {
            ResetRun();
            Show("newspaper", false);
        }

        // ------------------------------------------------------------------
        // case select
        // ------------------------------------------------------------------

        private void BuildCases()
        {
            var s = NewScreen("cases");
            _casesBody = UIKit.Node("body", s);
        }

        private void RefreshCases()
        {
            Clear(_casesBody);
            ScrollRect sc;
            var col = LobbyFrame(_casesBody, out sc, "القضايا", () => Show("menu", false));

            CaseCard(col, "01", Case.title,
                     "دار الأرشيف · اختفاء مخطوطة دون آثار اقتحام",
                     GameSettings.IsSolved(caseId), true,
                     () => StartCase(caseId));

            // Locked entries are the roadmap, stated plainly rather than teased. Each one
            // names the single new mechanic it would introduce.
            CaseCard(col, "02", "الشاهد الذي لم يكن هناك",
                     "ثلاث إفادات، وخط زمني واحد لا يتّسع لها كلها", false, false, null);
            CaseCard(col, "03", "ما تركته اليد",
                     "المختبر يقول شيئًا، والشهود يقولون غيره", false, false, null);
            CaseCard(col, "04", "أربعة في غرفة واحدة",
                     "كل واحد يغطّي الآخر. من يسقط أولًا؟", false, false, null);
            CaseCard(col, "05", "القضية المغلقة",
                     "قضية حُلّت من قبل. الحل كان خاطئًا.", false, false, null);

            UIKit.Spacer(col, 20f);
            var note = UIKit.Label("n", col,
                "القضايا المقفلة ليست مبنية بعد. المحرّك يشيلها، والقضية ملف نصي.",
                Theme.SizeTiny, Theme.TextMuted, TextAlignmentOptions.Center);
            note.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(note.rectTransform);
        }

        private void CaseCard(RectTransform parent, string number, string title,
                              string blurb, bool solved, bool unlocked, Action onPlay)
        {
            var card = UIKit.Rect("case_" + number, parent,
                unlocked ? new Color(1f, 1f, 1f, 0.07f) : new Color(1f, 1f, 1f, 0.025f));

            var o = card.gameObject.AddComponent<Outline>();
            o.effectColor = solved
                ? new Color(Theme.Verified.r, Theme.Verified.g, Theme.Verified.b, 0.7f)
                : (unlocked ? new Color(Theme.Amber.r, Theme.Amber.g, Theme.Amber.b, 0.55f)
                            : new Color(1f, 1f, 1f, 0.07f));
            o.effectDistance = new Vector2(2f, -2f);

            if (unlocked && onPlay != null)
            {
                var b = card.gameObject.AddComponent<Button>();
                b.targetGraphic = card;
                b.onClick.AddListener(() => { Sfx.Play(Sfx.Cue.Tap); onPlay(); });
            }

            var col = UIKit.Node("c", card.transform);
            UIKit.VStack(col, 8f, new RectOffset(30, 30, 26, 26));
            UIKit.FitHeight(col);
            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childControlHeight = true; v.childControlWidth = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var f = card.gameObject.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            string status = solved ? "✓ مغلقة" : (unlocked ? "متاحة" : "مقفلة");
            Color statusColor = solved ? Theme.Verified : (unlocked ? Theme.Amber : Theme.TextMuted);

            var head = UIKit.Label("h", col, "القضية " + number + "  ·  " + status,
                                   Theme.SizeTiny, statusColor, TextAlignmentOptions.TopRight);
            UIKit.FitHeight(head.rectTransform);
            UIKit.NoRaycast(head);

            var t = UIKit.Label("t", col, unlocked ? title : "؟؟؟", Theme.SizeHeading,
                                unlocked ? Theme.TextOnDark : Theme.TextMuted,
                                TextAlignmentOptions.TopRight, true, true);
            t.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(t.rectTransform);
            UIKit.NoRaycast(t);

            var b2 = UIKit.Label("b", col, blurb, Theme.SizeSmall, Theme.TextMuted);
            b2.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(b2.rectTransform);
            UIKit.NoRaycast(b2);

            if (solved)
            {
                int best = GameSettings.BestAttempts(caseId);
                if (best > 0)
                {
                    var s = UIKit.Label("s", col, "أفضل محاولة: " + best +
                        (best == 1 ? " (من أول مرة)" : ""), Theme.SizeTiny, Theme.Verified);
                    UIKit.FitHeight(s.rectTransform);
                    UIKit.NoRaycast(s);
                }
            }
        }

        // ------------------------------------------------------------------
        // settings
        // ------------------------------------------------------------------

        private void BuildSettings()
        {
            var s = NewScreen("settings");
            _setBody = UIKit.Node("body", s);
        }

        private void RefreshSettings()
        {
            Clear(_setBody);
            ScrollRect sc;
            var col = LobbyFrame(_setBody, out sc, "الإعدادات", () => Show("menu", false));

            Section(col, "الصوت");

            UIKit.Btn("mute", col, GameSettings.Muted ? "الصوت: مكتوم" : "الصوت: مفعّل", () =>
            {
                GameSettings.Muted = !GameSettings.Muted;
                Sfx.Play(Sfx.Cue.Tap);
                RefreshSettings();
            }, GameSettings.Muted ? new Color(1f, 1f, 1f, 0.05f) : Theme.RoomLight,
               GameSettings.Muted ? Theme.TextMuted : Theme.TextOnDark, Theme.SizeBody, 112f);

            Slider(col, "مؤثرات", GameSettings.SfxVolume, v =>
            {
                GameSettings.SfxVolume = v;
                Sfx.Play(Sfx.Cue.Tap);
                RefreshSettings();
            });

            Slider(col, "أجواء", GameSettings.AmbienceVolume, v =>
            {
                GameSettings.AmbienceVolume = v;
                RefreshSettings();
            });

            Section(col, "النص");

            UIKit.Btn("speed", col, "سرعة الحوار: " + GameSettings.SpeedName, () =>
            {
                GameSettings.CycleSpeed();
                Sfx.Play(Sfx.Cue.Tap);
                RefreshSettings();
            }, Theme.RoomLight, Theme.TextOnDark, Theme.SizeBody, 112f);

            Section(col, "التقدّم");

            UIKit.Btn("replayintro", col, "شاهد المقدمة مرة أخرى", () =>
            {
                Sfx.Play(Sfx.Cue.Tap);
                Show("intro", false);
            }, new Color(1f, 1f, 1f, 0.07f), Theme.TextOnDark, Theme.SizeSmall, 104f);

            UIKit.Btn("reset", col, "امسح كل التقدّم", () =>
            {
                OpenModal("مسح التقدّم",
                    "سيُمسح كل ما سجّلته اللعبة: القضايا المغلقة وإعدادات الصوت.\n\nلا يمكن التراجع.",
                    "امسح", () =>
                    {
                        GameSettings.ResetAll();
                        CloseModal();
                        ShowToast("مُسح كل التقدّم.");
                        RefreshSettings();
                    });
            }, new Color(Theme.Alert.r, Theme.Alert.g, Theme.Alert.b, 0.18f),
               Theme.Alert, Theme.SizeSmall, 104f);

            UIKit.Spacer(col, 20f);
            var v2 = UIKit.Label("v", col, "DETECT · بروتوتايب 0.1",
                                 Theme.SizeTiny, new Color(1f, 1f, 1f, 0.3f),
                                 TextAlignmentOptions.Center);
            UIKit.FitHeight(v2.rectTransform);
        }

        private void Section(RectTransform parent, string title)
        {
            UIKit.Spacer(parent, 14f);
            var t = UIKit.Label("sec", parent, title, Theme.SizeSmall, Theme.Amber,
                                TextAlignmentOptions.TopRight, false, true);
            UIKit.FitHeight(t.rectTransform);
            UIKit.Divider(parent, new Color(Theme.Amber.r, Theme.Amber.g, Theme.Amber.b, 0.3f), 2f);
        }

        /// <summary>
        /// A five-step level control rather than a drag slider: on a phone, five big
        /// targets beat a thin track, and volume does not need finer resolution.
        /// </summary>
        private void Slider(RectTransform parent, string label, float value, Action<float> onSet)
        {
            var row = UIKit.Node("slider_" + label, parent);
            var le = row.gameObject.AddComponent<LayoutElement>();
            le.minHeight = 104f; le.preferredHeight = 104f;
            UIKit.HStack(row, 8f);

            var l = UIKit.Label("l", row, label, Theme.SizeSmall, Theme.TextOnDark,
                                TextAlignmentOptions.MidlineRight);
            var lle = l.gameObject.AddComponent<LayoutElement>();
            lle.minWidth = 150f; lle.preferredWidth = 150f; lle.flexibleWidth = 0f;
            UIKit.NoRaycast(l);

            int level = Mathf.RoundToInt(Mathf.Clamp01(value) * 4f);
            for (int i = 0; i <= 4; i++)
            {
                int step = i;
                bool on = step <= level;
                UIKit.Btn("s" + i, row, step == 0 ? "٠" : new string('▍', 1),
                    () => onSet(step / 4f),
                    on ? Theme.Amber : new Color(1f, 1f, 1f, 0.07f),
                    on ? Theme.Ink : Theme.TextMuted,
                    Theme.SizeSmall, 88f);
            }
        }

        // ------------------------------------------------------------------
        // archive / codex
        // ------------------------------------------------------------------

        private void BuildCodex()
        {
            var s = NewScreen("codex");
            _codexBody = UIKit.Node("body", s);
        }

        private void RefreshCodex()
        {
            Clear(_codexBody);
            ScrollRect sc;
            var col = LobbyFrame(_codexBody, out sc, "الأرشيف", () => Show("menu", false));

            Section(col, "كيف تُحقّق");
            Entry(col, "١ · افحص",
                  "كل ما في المكان قابل للفحص. الدليل الذي تجمعه يُحفظ في دفترك، ولا يضيع.");
            Entry(col, "٢ · اسأل",
                  "لكل شخصية أسئلة محدودة. بعض الأسئلة لا تُفتح إلا بعد أن تعرف ما يكفي لتسألها.");
            Entry(col, "٣ · اربط",
                  "عندما تحمل معلومتان الشيء نفسه وتقولان عكسه، فذلك تناقض. أثبته في لوحة الربط.");
            Entry(col, "٤ · واجه",
                  "التناقض يفتح مواجهة. ما يقوله الشخص بعدها يفتح طريقًا لم يكن مفتوحًا قبلها.");
            Entry(col, "٥ · أغلق",
                  "التقرير لا يُقبل إلا مدعومًا. اكتب الخلاصة، واستشهد بما يحملها فعلًا.");

            Section(col, "ما يُثبت وما لا يُثبت");
            Entry(col, "الإفادة",
                  "ما يقوله شخص عن نفسه ادّعاء، لا واقعة. الإفادة تُثبت أنه قال، لا أنه صدق.");
            Entry(col, "الشهادة المنقولة",
                  "أن يرى شاهد صندوقًا ويُقال له ما بداخله لا يُثبت ما بداخله. يُثبت أن صندوقًا خرج فقط.");
            Entry(col, "التسجيل",
                  "يُثبت ما ظهر أمام العدسة في ذلك الوقت، بشرط أن يكون توقيته مضبوطًا ومقطعه متصلًا.");
            Entry(col, "المستند الرسمي",
                  "يُثبت ما قُيّد فيه — وأحيانًا يُثبت الأهم: ما لم يُقيّد. غياب تصريح دليل بذاته.");
            Entry(col, "الكذب",
                  "ثبوت أن رواية شخص غير صحيحة يستحق سؤالًا. لا يستحق حكمًا. الكذب ليس الجريمة.");

            Section(col, "عن اللعبة");
            Entry(col, "DETECT",
                  "لعبة تحقيق عربية. كل الأسماء والجهات والوقائع من نسج الخيال، وأي تشابه غير مقصود.");

            UIKit.Spacer(col, 24f);
        }

        private void Entry(RectTransform parent, string title, string body)
        {
            var card = UIKit.Rect("e", parent, new Color(1f, 1f, 1f, 0.05f));
            var col = UIKit.Node("c", card.transform);
            UIKit.VStack(col, 8f, new RectOffset(28, 28, 22, 22));
            UIKit.FitHeight(col);
            var v = card.gameObject.AddComponent<VerticalLayoutGroup>();
            v.childControlHeight = true; v.childControlWidth = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            var f = card.gameObject.AddComponent<ContentSizeFitter>();
            f.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var t = UIKit.Label("t", col, title, Theme.SizeBody, Theme.Amber,
                                TextAlignmentOptions.TopRight, false, true);
            UIKit.FitHeight(t.rectTransform);

            var b = UIKit.Label("b", col, body, Theme.SizeSmall, Theme.TextOnDark);
            b.textWrappingMode = TextWrappingModes.Normal;
            UIKit.FitHeight(b.rectTransform);
        }
    }
}
