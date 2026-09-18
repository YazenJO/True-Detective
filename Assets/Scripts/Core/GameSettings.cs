using UnityEngine;

namespace TrueDetective.Core
{
    /// <summary>
    /// Player preferences and which cases have been solved, kept in PlayerPrefs.
    ///
    /// Only the outcome of a case is stored, never mid-case progress: a case is short
    /// enough to finish in one sitting, and saving half an investigation would mean
    /// serialising the whole session and reasoning about what a half-restored state
    /// even means.
    /// </summary>
    public static class GameSettings
    {
        private const string KeySfx       = "td.vol.sfx";
        private const string KeyAmbience  = "td.vol.ambience";
        private const string KeyMute      = "td.mute";
        private const string KeyTextSpeed = "td.textspeed";
        private const string KeySolved    = "td.solved.";      // + case id
        private const string KeyBest      = "td.best.";        // + case id, attempts used
        private const string KeySeenIntro = "td.seenintro";

        /// <summary>How fast dialogue types itself out. 0 = instant.</summary>
        public enum TextSpeed { Instant = 0, Fast = 1, Normal = 2 }

        public static void Load()
        {
            Sfx.SfxVolume      = PlayerPrefs.GetFloat(KeySfx, 0.7f);
            Sfx.AmbienceVolume = PlayerPrefs.GetFloat(KeyAmbience, 0.35f);
            Sfx.Muted          = PlayerPrefs.GetInt(KeyMute, 0) == 1;
        }

        public static float SfxVolume
        {
            get { return Sfx.SfxVolume; }
            set { Sfx.SfxVolume = value; PlayerPrefs.SetFloat(KeySfx, value); PlayerPrefs.Save(); }
        }

        public static float AmbienceVolume
        {
            get { return Sfx.AmbienceVolume; }
            set { Sfx.AmbienceVolume = value; PlayerPrefs.SetFloat(KeyAmbience, value); PlayerPrefs.Save(); }
        }

        public static bool Muted
        {
            get { return Sfx.Muted; }
            set { Sfx.Muted = value; PlayerPrefs.SetInt(KeyMute, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static TextSpeed Speed
        {
            get { return (TextSpeed)PlayerPrefs.GetInt(KeyTextSpeed, (int)TextSpeed.Fast); }
            set { PlayerPrefs.SetInt(KeyTextSpeed, (int)value); PlayerPrefs.Save(); }
        }

        /// <summary>Seconds per character when typing dialogue out.</summary>
        public static float SecondsPerChar
        {
            get
            {
                switch (Speed)
                {
                    case TextSpeed.Instant: return 0f;
                    case TextSpeed.Normal:  return 0.030f;
                    default:                return 0.014f;
                }
            }
        }

        public static string SpeedName
        {
            get
            {
                switch (Speed)
                {
                    case TextSpeed.Instant: return "فوري";
                    case TextSpeed.Normal:  return "عادي";
                    default:                return "سريع";
                }
            }
        }

        public static void CycleSpeed()
        {
            Speed = (TextSpeed)(((int)Speed + 1) % 3);
        }

        // ---- case progress ----

        public static bool IsSolved(string caseId)
        {
            return PlayerPrefs.GetInt(KeySolved + caseId, 0) == 1;
        }

        /// <summary>Records a solve, keeping the fewest report attempts ever used.</summary>
        public static void MarkSolved(string caseId, int attempts)
        {
            PlayerPrefs.SetInt(KeySolved + caseId, 1);
            int best = PlayerPrefs.GetInt(KeyBest + caseId, int.MaxValue);
            if (attempts < best) PlayerPrefs.SetInt(KeyBest + caseId, attempts);
            PlayerPrefs.Save();
        }

        public static int BestAttempts(string caseId)
        {
            return PlayerPrefs.GetInt(KeyBest + caseId, 0);
        }

        public static bool HasSeenIntro
        {
            get { return PlayerPrefs.GetInt(KeySeenIntro, 0) == 1; }
            set { PlayerPrefs.SetInt(KeySeenIntro, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Wipes everything. Offered in settings, behind a confirmation.</summary>
        public static void ResetAll()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
            Load();
        }
    }
}
