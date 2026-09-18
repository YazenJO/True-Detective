using System.Collections.Generic;
using System.Text;

namespace TrueDetective.Core
{
    /// <summary>
    /// Arabic shaping + bidi for TextMeshPro. TMP draws glyphs left-to-right and does
    /// no letter joining, so we do both ourselves: map each Arabic letter to its correct
    /// presentation form (U+FE70..U+FEFF) based on its neighbours, then emit the string
    /// already reversed so a plain LTR pass renders it right-to-left.
    ///
    /// Leave TMP_Text.isRightToLeftText OFF on any label fed from here, or it reverses twice.
    ///
    /// Latin/digit runs keep their reading order inside the reversed output, so
    /// "الساعة 20:12" and "المخطوطة A-47" come out correct.
    /// </summary>
    public static class ArabicText
    {
        // isolated, final, initial, medial. 0 = that form does not exist for this letter.
        private struct Forms
        {
            public char Iso, Fin, Ini, Med;
            public Forms(int iso, int fin, int ini, int med)
            { Iso = (char)iso; Fin = (char)fin; Ini = (char)ini; Med = (char)med; }

            /// <summary>Letter can connect to whatever follows it (to its left).</summary>
            public bool CanJoinForward { get { return Ini != 0; } }
        }

        private static readonly Dictionary<char, Forms> Map = new Dictionary<char, Forms>
        {
            // ---- Arabic ----
            { 'ء', new Forms(0xFE80, 0,      0,      0      ) }, // hamza
            { 'آ', new Forms(0xFE81, 0xFE82, 0,      0      ) }, // alef madda
            { 'أ', new Forms(0xFE83, 0xFE84, 0,      0      ) }, // alef hamza above
            { 'ؤ', new Forms(0xFE85, 0xFE86, 0,      0      ) }, // waw hamza
            { 'إ', new Forms(0xFE87, 0xFE88, 0,      0      ) }, // alef hamza below
            { 'ئ', new Forms(0xFE89, 0xFE8A, 0xFE8B, 0xFE8C ) }, // yeh hamza
            { 'ا', new Forms(0xFE8D, 0xFE8E, 0,      0      ) }, // alef
            { 'ب', new Forms(0xFE8F, 0xFE90, 0xFE91, 0xFE92 ) }, // beh
            { 'ة', new Forms(0xFE93, 0xFE94, 0,      0      ) }, // teh marbuta
            { 'ت', new Forms(0xFE95, 0xFE96, 0xFE97, 0xFE98 ) }, // teh
            { 'ث', new Forms(0xFE99, 0xFE9A, 0xFE9B, 0xFE9C ) }, // theh
            { 'ج', new Forms(0xFE9D, 0xFE9E, 0xFE9F, 0xFEA0 ) }, // jeem
            { 'ح', new Forms(0xFEA1, 0xFEA2, 0xFEA3, 0xFEA4 ) }, // hah
            { 'خ', new Forms(0xFEA5, 0xFEA6, 0xFEA7, 0xFEA8 ) }, // khah
            { 'د', new Forms(0xFEA9, 0xFEAA, 0,      0      ) }, // dal
            { 'ذ', new Forms(0xFEAB, 0xFEAC, 0,      0      ) }, // thal
            { 'ر', new Forms(0xFEAD, 0xFEAE, 0,      0      ) }, // reh
            { 'ز', new Forms(0xFEAF, 0xFEB0, 0,      0      ) }, // zain
            { 'س', new Forms(0xFEB1, 0xFEB2, 0xFEB3, 0xFEB4 ) }, // seen
            { 'ش', new Forms(0xFEB5, 0xFEB6, 0xFEB7, 0xFEB8 ) }, // sheen
            { 'ص', new Forms(0xFEB9, 0xFEBA, 0xFEBB, 0xFEBC ) }, // sad
            { 'ض', new Forms(0xFEBD, 0xFEBE, 0xFEBF, 0xFEC0 ) }, // dad
            { 'ط', new Forms(0xFEC1, 0xFEC2, 0xFEC3, 0xFEC4 ) }, // tah
            { 'ظ', new Forms(0xFEC5, 0xFEC6, 0xFEC7, 0xFEC8 ) }, // zah
            { 'ع', new Forms(0xFEC9, 0xFECA, 0xFECB, 0xFECC ) }, // ain
            { 'غ', new Forms(0xFECD, 0xFECE, 0xFECF, 0xFED0 ) }, // ghain
            { 'ف', new Forms(0xFED1, 0xFED2, 0xFED3, 0xFED4 ) }, // feh
            { 'ق', new Forms(0xFED5, 0xFED6, 0xFED7, 0xFED8 ) }, // qaf
            { 'ك', new Forms(0xFED9, 0xFEDA, 0xFEDB, 0xFEDC ) }, // kaf
            { 'ل', new Forms(0xFEDD, 0xFEDE, 0xFEDF, 0xFEE0 ) }, // lam
            { 'م', new Forms(0xFEE1, 0xFEE2, 0xFEE3, 0xFEE4 ) }, // meem
            { 'ن', new Forms(0xFEE5, 0xFEE6, 0xFEE7, 0xFEE8 ) }, // noon
            { 'ه', new Forms(0xFEE9, 0xFEEA, 0xFEEB, 0xFEEC ) }, // heh
            { 'و', new Forms(0xFEED, 0xFEEE, 0,      0      ) }, // waw
            { 'ى', new Forms(0xFEEF, 0xFEF0, 0,      0      ) }, // alef maksura
            { 'ي', new Forms(0xFEF1, 0xFEF2, 0xFEF3, 0xFEF4 ) }, // yeh
            // ---- Persian / Urdu extras, harmless to keep ----
            { 'پ', new Forms(0xFB56, 0xFB57, 0xFB58, 0xFB59 ) }, // peh
            { 'چ', new Forms(0xFB7A, 0xFB7B, 0xFB7C, 0xFB7D ) }, // tcheh
            { 'ژ', new Forms(0xFB8A, 0xFB8B, 0,      0      ) }, // jeh
            { 'ک', new Forms(0xFB8E, 0xFB8F, 0xFB90, 0xFB91 ) }, // keheh
            { 'گ', new Forms(0xFB92, 0xFB93, 0xFB94, 0xFB95 ) }, // gaf
            { 'ی', new Forms(0xFBFC, 0xFBFD, 0xFBFE, 0xFBFF ) }, // farsi yeh
        };

        // lam + alef collapse into one mandatory ligature. key = the alef that follows lam.
        private static readonly Dictionary<char, char[]> LamAlef = new Dictionary<char, char[]>
        {
            { 'آ', new[] { 'ﻵ', 'ﻶ' } }, // lam + alef madda
            { 'أ', new[] { 'ﻷ', 'ﻸ' } }, // lam + alef hamza above
            { 'إ', new[] { 'ﻹ', 'ﻺ' } }, // lam + alef hamza below
            { 'ا', new[] { 'ﻻ', 'ﻼ' } }, // lam + alef
        };

        private const char Tatweel = 'ـ';

        /// <summary>Combining mark: sits on a letter and must not break its joining.</summary>
        private static bool IsMark(char c)
        {
            return (c >= 'ً' && c <= 'ٟ') || c == 'ٰ'
                || (c >= 'ۖ' && c <= 'ۜ') || (c >= '۟' && c <= 'ۨ')
                || (c >= '۪' && c <= 'ۭ');
        }

        /// <summary>Letter can connect to whatever precedes it (to its right).</summary>
        private static bool CanJoinBackward(char c)
        {
            return c == Tatweel || Map.ContainsKey(c);
        }

        private static bool CanJoinForward(char c)
        {
            if (c == Tatweel) return true;
            Forms f;
            return Map.TryGetValue(c, out f) && f.CanJoinForward;
        }

        /// <summary>Characters that form a left-to-right run: digits, Latin letters.</summary>
        private static bool IsLtr(char c)
        {
            return (c >= '0' && c <= '9')
                || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')
                || (c >= 'À' && c <= 'ɏ');
        }

        /// <summary>Separator that may live inside an LTR run: "20:12", "A-47", "3.5".</summary>
        private static bool IsLtrSeparator(char c)
        {
            return c == ':' || c == '-' || c == '.' || c == '/'
                || c == ',' || c == '%' || c == '+';
        }

        /// <summary>
        /// Shape and reorder <paramref name="logical"/> for display in a plain LTR renderer.
        /// Null, empty and pure-Latin input pass through safely.
        /// </summary>
        public static string Fix(string logical)
        {
            if (string.IsNullOrEmpty(logical)) return logical;

            string[] lines = logical.Replace("\r\n", "\n").Split('\n');
            var sb = new StringBuilder(logical.Length + 8);
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(Reorder(Shape(lines[i])));
            }
            return sb.ToString();
        }

        // ---- pass 1: presentation forms + lam-alef ligatures ----
        private static List<char> Shape(string s)
        {
            var result = new List<char>(s.Length);

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];

                // marks and every non-Arabic char (incl. tatweel) pass through untouched
                if (IsMark(c) || !Map.ContainsKey(c)) { result.Add(c); continue; }

                // find neighbours, skipping combining marks
                int p = i - 1; while (p >= 0 && IsMark(s[p])) p--;
                int n = i + 1; while (n < s.Length && IsMark(s[n])) n++;

                char prev = p >= 0 ? s[p] : '\0';
                char next = n < s.Length ? s[n] : '\0';

                bool linkPrev = prev != '\0' && CanJoinForward(prev);
                bool linkNext = next != '\0' && CanJoinBackward(next);

                // lam followed by an alef is a mandatory ligature; it consumes both letters
                char[] lig;
                if (c == 'ل' && next != '\0' && LamAlef.TryGetValue(next, out lig))
                {
                    result.Add(linkPrev ? lig[1] : lig[0]);
                    i = n;
                    continue;
                }

                Forms f = Map[c];
                char glyph;
                if (linkPrev && linkNext && f.Med != 0) glyph = f.Med;
                else if (linkPrev && f.Fin != 0)        glyph = f.Fin;
                else if (linkNext && f.Ini != 0)        glyph = f.Ini;
                else                                    glyph = f.Iso;

                result.Add(glyph);
            }

            return result;
        }

        // ---- pass 2: reverse, keeping LTR runs in reading order ----
        private static string Reorder(List<char> shaped)
        {
            var sb = new StringBuilder(shaped.Count);
            int i = shaped.Count - 1;

            while (i >= 0)
            {
                if (!IsLtr(shaped[i]))
                {
                    sb.Append(Mirror(shaped[i]));
                    i--;
                    continue;
                }

                // walk left to the start of this LTR run; a separator only stays inside
                // the run when a real LTR char sits on its left ("20:12" keeps the colon,
                // a trailing "20:" does not).
                int end = i;
                int start = i;
                while (start > 0)
                {
                    char left = shaped[start - 1];
                    if (IsLtr(left)) { start--; continue; }
                    if (IsLtrSeparator(left) && start - 2 >= 0 && IsLtr(shaped[start - 2]))
                    { start -= 2; continue; }
                    break;
                }

                for (int k = start; k <= end; k++) sb.Append(shaped[k]);
                i = start - 1;
            }

            return sb.ToString();
        }

        /// <summary>Paired glyphs must flip when the surrounding run is reversed.</summary>
        private static char Mirror(char c)
        {
            switch (c)
            {
                case '(': return ')';
                case ')': return '(';
                case '[': return ']';
                case ']': return '[';
                case '{': return '}';
                case '}': return '{';
                case '<': return '>';
                case '>': return '<';
                case '«': return '»';
                case '»': return '«';
                default: return c;
            }
        }
    }
}
