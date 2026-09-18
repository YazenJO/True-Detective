using System;
using System.Collections.Generic;
using UnityEngine;

namespace TrueDetective.Core
{
    /// <summary>
    /// Every sound in the game, synthesised into AudioClips at startup. Nothing is
    /// downloaded or imported, so there is no licence attached to any of it and the
    /// build carries no audio files.
    ///
    /// The palette is deliberately small and dry: a detective's room, not a score.
    /// Paper, switches, a struck string for a discovery, a low room tone underneath.
    /// Nothing here ever carries information the player needs - muting loses nothing.
    /// </summary>
    public static class Sfx
    {
        public const int SampleRate = 44100;

        public enum Cue
        {
            Tap,          // any button
            Back,         // leaving a screen
            PaperOpen,    // a document appears
            PaperClose,
            EvidenceFiled,// something entered the notebook
            Reveal,       // a contradiction proved, the good moment
            Wrong,        // an assertion that does not hold
            Confront,     // the confrontation begins
            Unlock,       // a new action opened
            Close         // the case closes
        }

        private static readonly Dictionary<Cue, AudioClip> _clips = new Dictionary<Cue, AudioClip>();
        private static AudioClip _roomTone;
        private static AudioSource _oneShot;
        private static AudioSource _ambience;

        private static float _sfxVolume = 0.7f;
        private static float _ambienceVolume = 0.35f;
        private static bool _muted;

        public static float SfxVolume
        {
            get { return _sfxVolume; }
            set { _sfxVolume = Mathf.Clamp01(value); }
        }

        public static float AmbienceVolume
        {
            get { return _ambienceVolume; }
            set
            {
                _ambienceVolume = Mathf.Clamp01(value);
                if (_ambience != null) _ambience.volume = _muted ? 0f : _ambienceVolume;
            }
        }

        public static bool Muted
        {
            get { return _muted; }
            set
            {
                _muted = value;
                if (_ambience != null) _ambience.volume = _muted ? 0f : _ambienceVolume;
            }
        }

        /// <summary>Builds every clip and attaches the two sources. Safe to call twice.</summary>
        public static void Init(GameObject host)
        {
            if (_oneShot != null) return;

            _oneShot = host.AddComponent<AudioSource>();
            _oneShot.playOnAwake = false;
            _oneShot.spatialBlend = 0f;

            _ambience = host.AddComponent<AudioSource>();
            _ambience.playOnAwake = false;
            _ambience.loop = true;
            _ambience.spatialBlend = 0f;
            _ambience.volume = _muted ? 0f : _ambienceVolume;

            BuildAll();

            _ambience.clip = _roomTone;
            _ambience.Play();
        }

        public static void Play(Cue cue, float volumeScale = 1f)
        {
            if (_muted || _oneShot == null) return;
            AudioClip c;
            if (!_clips.TryGetValue(cue, out c) || c == null) return;
            _oneShot.PlayOneShot(c, Mathf.Clamp01(_sfxVolume * volumeScale));
        }

        // ------------------------------------------------------------------
        // synthesis
        // ------------------------------------------------------------------

        private static void BuildAll()
        {
            _clips[Cue.Tap]           = Click(0.045f, 1500f, 0.30f);
            _clips[Cue.Back]          = Click(0.055f, 820f, 0.24f);

            _clips[Cue.PaperOpen]     = Paper(0.30f, 0.55f);
            _clips[Cue.PaperClose]    = Paper(0.22f, 0.40f);

            // a filed clue gets a small two-note rise: something was gained
            _clips[Cue.EvidenceFiled] = Notes(new[] { 587.33f, 880.00f }, 0.085f, 0.13f, 0.30f);

            // the discovery. a minor-third fall reads as "oh" rather than "well done"
            _clips[Cue.Reveal]        = Notes(new[] { 622.25f, 523.25f, 392.00f }, 0.13f, 0.34f, 0.34f);

            // wrong is a soft low thud, never a buzzer - a mistake here is not a failure
            _clips[Cue.Wrong]         = Notes(new[] { 174.61f, 164.81f }, 0.10f, 0.22f, 0.26f);

            _clips[Cue.Confront]      = Swell(0.9f, 98f, 0.32f);
            _clips[Cue.Unlock]        = Notes(new[] { 392.00f, 523.25f, 659.25f }, 0.075f, 0.16f, 0.26f);
            _clips[Cue.Close]         = Notes(new[] { 261.63f, 329.63f, 392.00f, 523.25f }, 0.16f, 0.60f, 0.30f);

            _roomTone = RoomTone(6f);
        }

        /// <summary>A UI click: a short filtered noise burst with a fast decay.</summary>
        private static AudioClip Click(float seconds, float centreHz, float amp)
        {
            int n = Mathf.RoundToInt(SampleRate * seconds);
            var data = new float[n];
            var rng = new System.Random(unchecked((int)(centreHz * 31)));
            float last = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float env = Mathf.Exp(-14f * t);                 // sharp percussive decay
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                // one-pole low-pass, tuned by centreHz, to take the hiss off
                float a = Mathf.Clamp01(centreHz / (SampleRate * 0.5f));
                last += a * (noise - last);
                // a faint tone under the noise gives the click a body
                float tone = Mathf.Sin(2f * Mathf.PI * centreHz * i / SampleRate) * 0.35f;
                data[i] = (last + tone) * env * amp;
            }
            return Make("click_" + (int)centreHz, data);
        }

        /// <summary>Paper: shaped noise with a slow attack, several small rustles layered.</summary>
        private static AudioClip Paper(float seconds, float amp)
        {
            int n = Mathf.RoundToInt(SampleRate * seconds);
            var data = new float[n];
            var rng = new System.Random(4242);
            float hp = 0f, lp = 0f;

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                // two overlapping rustles rather than one flat hiss
                float env = Mathf.Exp(-5f * t) * (0.6f + 0.4f * Mathf.Sin(t * 22f));
                env *= Mathf.Clamp01(t * 30f);                    // brief fade-in, no click
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);

                lp += 0.35f * (noise - lp);                       // keep the low mids
                hp = noise - lp;                                  // and the crisp top
                data[i] = (hp * 0.8f + lp * 0.2f) * env * amp;
            }
            return Make("paper_" + n, data);
        }

        /// <summary>A short sequence of plucked tones, each a decaying sine with a little body.</summary>
        private static AudioClip Notes(float[] hz, float noteLen, float total, float amp)
        {
            int n = Mathf.RoundToInt(SampleRate * total);
            var data = new float[n];

            for (int k = 0; k < hz.Length; k++)
            {
                int start = Mathf.RoundToInt(SampleRate * noteLen * k);
                int len = Mathf.RoundToInt(SampleRate * (total - noteLen * k));
                if (start >= n) break;

                for (int i = 0; i < len && start + i < n; i++)
                {
                    float t = (float)i / SampleRate;
                    float env = Mathf.Exp(-6.5f * t);
                    float f = hz[k];
                    float s = Mathf.Sin(2f * Mathf.PI * f * t)
                            + 0.28f * Mathf.Sin(2f * Mathf.PI * f * 2f * t)   // octave
                            + 0.12f * Mathf.Sin(2f * Mathf.PI * f * 3f * t);  // fifth above
                    data[start + i] += s * env * amp * 0.55f;
                }
            }
            SoftClip(data);
            return Make("notes_" + hz.Length + "_" + (int)hz[0], data);
        }

        /// <summary>A low swell for the confrontation: a rising detuned drone.</summary>
        private static AudioClip Swell(float seconds, float baseHz, float amp)
        {
            int n = Mathf.RoundToInt(SampleRate * seconds);
            var data = new float[n];

            for (int i = 0; i < n; i++)
            {
                float t = (float)i / SampleRate;
                float p = (float)i / n;
                float env = Mathf.Sin(p * Mathf.PI);              // in and out, no hard edge
                env *= env;
                float rise = 1f + p * 0.12f;                      // pitch creeps up: unease
                float s = Mathf.Sin(2f * Mathf.PI * baseHz * rise * t)
                        + 0.6f * Mathf.Sin(2f * Mathf.PI * baseHz * rise * 1.007f * t)  // detune beat
                        + 0.3f * Mathf.Sin(2f * Mathf.PI * baseHz * rise * 2f * t);
                data[i] = s * env * amp * 0.4f;
            }
            SoftClip(data);
            return Make("swell", data);
        }

        /// <summary>
        /// Looping room tone: filtered noise with a slow drift, low enough to read as
        /// "a building at night" rather than as music. Loop ends are cross-faded.
        /// </summary>
        private static AudioClip RoomTone(float seconds)
        {
            int n = Mathf.RoundToInt(SampleRate * seconds);
            var data = new float[n];
            var rng = new System.Random(90210);
            float lp1 = 0f, lp2 = 0f;

            for (int i = 0; i < n; i++)
            {
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp1 += 0.010f * (noise - lp1);                    // two poles: deep rumble only
                lp2 += 0.010f * (lp1 - lp2);
                float t = (float)i / SampleRate;
                float drift = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.07f * t);
                data[i] = lp2 * 9f * drift;
            }

            // cross-fade the tail over the head so the loop point is inaudible
            int fade = Mathf.Min(n / 8, SampleRate / 2);
            for (int i = 0; i < fade; i++)
            {
                float k = (float)i / fade;
                data[i] = Mathf.Lerp(data[n - fade + i], data[i], k);
            }

            SoftClip(data);
            return Make("roomtone", data);
        }

        /// <summary>Keeps summed layers inside range without the crunch of hard clipping.</summary>
        private static void SoftClip(float[] data)
        {
            for (int i = 0; i < data.Length; i++)
                data[i] = (float)Math.Tanh(data[i]);
        }

        private static AudioClip Make(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
