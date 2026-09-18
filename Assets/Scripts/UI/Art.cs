using System.Collections.Generic;
using UnityEngine;

namespace TrueDetective.UI
{
    /// <summary>
    /// Sprite lookup for everything the case files name. A texture imported into
    /// Resources is loaded as a Texture2D rather than a Sprite unless its importer was
    /// set to Sprite mode, so both are tried and a Texture2D is wrapped on the fly.
    /// That means artwork can be dropped into the folders with no import settings at
    /// all and still appear.
    /// </summary>
    public static class Art
    {
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        public const string Backgrounds = "Backgrounds/";
        public const string Portraits   = "Portraits/";
        public const string Evidence    = "Evidence/";
        public const string Brand       = "Brand/";

        /// <summary>Loads folder + name, or null if there is no such image.</summary>
        public static Sprite Load(string folder, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string key = folder + name;

            Sprite cached;
            if (_cache.TryGetValue(key, out cached)) return cached;

            var sprite = Resources.Load<Sprite>(key);
            if (sprite == null)
            {
                var tex = Resources.Load<Texture2D>(key);
                if (tex != null)
                {
                    sprite = Sprite.Create(tex,
                        new Rect(0f, 0f, tex.width, tex.height),
                        new Vector2(0.5f, 0.5f), 100f);
                    sprite.name = key;
                }
            }

            _cache[key] = sprite;   // cache misses too, so a missing file is looked up once
            return sprite;
        }

        public static Sprite Background(string name) { return Load(Backgrounds, name); }
        public static Sprite Portrait(string name)   { return Load(Portraits, name); }
        public static Sprite EvidenceIcon(string n)  { return Load(Evidence, n); }
        public static Sprite BrandArt(string name)   { return Load(Brand, name); }

        public static void ClearCache() { _cache.Clear(); }
    }
}
