using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using TrueDetective.Core;

namespace TrueDetective.UI
{
    /// <summary>
    /// The opening. Full-bleed stills, one line of text at a time, a slow push on the
    /// image, then the logo lands on a beat of silence.
    ///
    /// Two rules borrowed from every good cold open: it is skippable from the first
    /// frame, and it is shown once. A cinematic the player cannot escape stops being
    /// atmosphere the second time they see it.
    /// </summary>
    public partial class Game
    {
        private RectTransform _cinBody;
        private Coroutine _cinRoutine;

        /// <summary>One beat: a still, a line, and how long it holds.</summary>
        private struct Beat
        {
            public string Art;      // sprite in Resources/Brand, empty for plain black
            public string Line;
            public float Hold;      // seconds the line stays after fading in
            public Sfx.Cue Cue;
            public bool HasCue;

            public static Beat Of(string art, string line, float hold)
            {
                return new Beat { Art = art, Line = line, Hold = hold, HasCue = false };
            }
            public static Beat Of(string art, string line, float hold, Sfx.Cue cue)
            {
                return new Beat { Art = art, Line = line, Hold = hold, Cue = cue, HasCue = true };
            }
        }

        private static readonly Beat[] IntroBeats =
        {
            Beat.Of("intro_light",  "كل جريمة تترك أثرًا.", 2.2f, Sfx.Cue.PaperOpen),
            Beat.Of("intro_light",  "ليس دائمًا في المكان.", 2.0f),
            Beat.Of("intro_folder", "أحيانًا تتركه في رواية شخص\nعن ليلة لم تكن كما قال.", 3.0f, Sfx.Cue.Reveal),
            Beat.Of("intro_folder", "مهمتك ليست أن تعرف من فعلها.", 2.4f),
            Beat.Of("",             "مهمتك أن تُثبت ذلك.", 2.6f, Sfx.Cue.Confront),
        };

        private void BuildCinematic()
        {
            var s = NewScreen("intro", Color.black);
            _cinBody = UIKit.Node("body", s);
        }

        private void RefreshCinematic()
        {
            Clear(_cinBody);
            if (_cinRoutine != null) { StopCoroutine(_cinRoutine); _cinRoutine = null; }
            _cinRoutine = StartCoroutine(PlayIntro());
        }

        private IEnumerator PlayIntro()
        {
            // ---- layers ----
            var artHolder = UIKit.Node("art", _cinBody);
            var artGroup = UIKit.Fader(artHolder, 0f);

            var textHolder = UIKit.Node("text", _cinBody);
            textHolder.offsetMin = new Vector2(Theme.Gutter * 1.4f, 0f);
            textHolder.offsetMax = new Vector2(-Theme.Gutter * 1.4f, 0f);
            var line = UIKit.Label("line", textHolder, "", Theme.SizeTitle,
                                   Theme.Paper, TextAlignmentOptions.Center, true);
            line.textWrappingMode = TextWrappingModes.Normal;
            var lineGroup = UIKit.Fader(line.rectTransform, 0f);

            // skip is available from the very first frame
            var skip = UIKit.Btn("skip", _cinBody, "تخطّي", () => FinishIntro(),
                                 new Color(1f, 1f, 1f, 0.06f), Theme.TextMuted, Theme.SizeTiny, 74f);
            var srt = skip.GetComponent<RectTransform>();
            srt.anchorMin = new Vector2(1f, 0f);
            srt.anchorMax = new Vector2(1f, 0f);
            srt.pivot = new Vector2(1f, 0f);
            srt.sizeDelta = new Vector2(170f, 74f);
            srt.anchoredPosition = new Vector2(-Theme.Gutter, Theme.Gutter);
            Destroy(skip.GetComponent<LayoutElement>());

            yield return new WaitForSeconds(0.6f);   // a beat of black before anything

            Image current = null;
            string shownArt = null;

            foreach (var beat in IntroBeats)
            {
                // swap the still only when it actually changes, so a held image keeps drifting
                if (beat.Art != shownArt)
                {
                    if (current != null) { Destroy(current.gameObject); current = null; }
                    shownArt = beat.Art;

                    if (!string.IsNullOrEmpty(beat.Art))
                    {
                        current = UIKit.Picture("still", artHolder, Art.BrandArt(beat.Art), false);
                        if (current != null)
                        {
                            UIKit.Stretch(current.rectTransform);
                            current.rectTransform.SetAsFirstSibling();
                        }
                    }
                    yield return Fade(artGroup, current != null ? 1f : 0f, 0.7f);
                }

                if (beat.HasCue) Sfx.Play(beat.Cue, 0.8f);

                UIKit.SetText(line, beat.Line);
                yield return Fade(lineGroup, 1f, 0.55f);

                // slow push on the still while the line is up: the frame stays alive
                float t = 0f;
                while (t < beat.Hold)
                {
                    t += Time.deltaTime;
                    if (current != null)
                    {
                        float k = 1f + 0.05f * (t / Mathf.Max(beat.Hold, 0.01f));
                        current.rectTransform.localScale = new Vector3(k, k, 1f);
                    }
                    yield return null;
                }

                yield return Fade(lineGroup, 0f, 0.45f);
            }

            // ---- the logo lands ----
            yield return Fade(artGroup, 0f, 0.6f);
            yield return new WaitForSeconds(0.35f);

            var logo = UIKit.Picture("logo", _cinBody, Art.BrandArt("logo"));
            if (logo != null)
            {
                var lrt = logo.rectTransform;
                lrt.anchorMin = new Vector2(0.5f, 0.5f);
                lrt.anchorMax = new Vector2(0.5f, 0.5f);
                lrt.sizeDelta = new Vector2(720f, 720f);
                lrt.anchoredPosition = Vector2.zero;

                var lg = UIKit.Fader(lrt, 0f);
                Sfx.Play(Sfx.Cue.Close, 0.9f);

                // overshoot then settle: the scale curve is the whole feel of the beat
                float t = 0f;
                const float dur = 0.85f;
                while (t < dur)
                {
                    t += Time.deltaTime;
                    float p = Mathf.Clamp01(t / dur);
                    lg.alpha = Mathf.Clamp01(p * 2.2f);
                    float k = 1.16f - 0.16f * Mathf.Sin(p * Mathf.PI * 0.5f);
                    lrt.localScale = new Vector3(k, k, 1f);
                    yield return null;
                }
                lrt.localScale = Vector3.one;
            }

            yield return new WaitForSeconds(1.1f);
            FinishIntro();
        }

        private void FinishIntro()
        {
            if (_cinRoutine != null) { StopCoroutine(_cinRoutine); _cinRoutine = null; }
            GameSettings.HasSeenIntro = true;
            Show("menu", false);
        }

        /// <summary>Linear fade of a CanvasGroup. Used everywhere a screen transitions.</summary>
        private static IEnumerator Fade(CanvasGroup cg, float to, float seconds)
        {
            if (cg == null) yield break;
            float from = cg.alpha;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                cg.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds));
                yield return null;
            }
            cg.alpha = to;
        }
    }
}
