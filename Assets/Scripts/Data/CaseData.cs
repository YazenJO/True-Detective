using System;
using System.Collections.Generic;

namespace TrueDetective.Data
{
    /// <summary>
    /// A whole case, loaded from one JSON file. Nothing here knows about UI or Unity
    /// scenes, so adding a case means adding a JSON file and its background sprites,
    /// with no code change. Fields are public and the shapes are arrays-of-classes
    /// only, because Unity's JsonUtility cannot handle properties or dictionaries.
    /// </summary>
    [Serializable]
    public class CaseData
    {
        public string id;
        public string title;
        public string subtitle;

        public Newspaper newspaper;
        public string briefing;              // what the client asks the detective to do
        public string startLocationId;

        public Location[] locations;
        public Evidence[] evidence;
        public Character[] characters;
        public Contradiction[] contradictions;
        public Confrontation[] confrontations;
        public CaseAction[] actions;
        public Report report;
        public Hint[] hints;
        public string epilogue;              // teaser line for a future case

        // ---- lookup tables, built once after load ----
        [NonSerialized] private Dictionary<string, Location> _locations;
        [NonSerialized] private Dictionary<string, Evidence> _evidence;
        [NonSerialized] private Dictionary<string, Character> _characters;
        [NonSerialized] private Dictionary<string, CaseAction> _actions;

        /// <summary>Called once by the loader. Safe to call again; it rebuilds.</summary>
        public void BuildIndex()
        {
            _locations = new Dictionary<string, Location>();
            _evidence = new Dictionary<string, Evidence>();
            _characters = new Dictionary<string, Character>();
            _actions = new Dictionary<string, CaseAction>();

            if (locations != null)
                foreach (var l in locations) if (l != null && !string.IsNullOrEmpty(l.id)) _locations[l.id] = l;
            if (evidence != null)
                foreach (var e in evidence) if (e != null && !string.IsNullOrEmpty(e.id)) _evidence[e.id] = e;
            if (characters != null)
                foreach (var c in characters) if (c != null && !string.IsNullOrEmpty(c.id)) _characters[c.id] = c;
            if (actions != null)
                foreach (var a in actions) if (a != null && !string.IsNullOrEmpty(a.id)) _actions[a.id] = a;
        }

        public Location GetLocation(string lid)
        {
            Location v; return _locations != null && _locations.TryGetValue(lid ?? "", out v) ? v : null;
        }

        public Evidence GetEvidence(string eid)
        {
            Evidence v; return _evidence != null && _evidence.TryGetValue(eid ?? "", out v) ? v : null;
        }

        public Character GetCharacter(string cid)
        {
            Character v; return _characters != null && _characters.TryGetValue(cid ?? "", out v) ? v : null;
        }

        public CaseAction GetAction(string aid)
        {
            CaseAction v; return _actions != null && _actions.TryGetValue(aid ?? "", out v) ? v : null;
        }
    }

    [Serializable]
    public class Newspaper
    {
        public string masthead;   // paper name
        public string date;
        public string headline;
        public string body;
        public string caption;
    }

    /// <summary>
    /// One screen the detective can stand in. The background is a sprite name resolved
    /// from Resources, so a case can use whatever environment it likes.
    /// </summary>
    [Serializable]
    public class Location
    {
        public string id;
        public string name;
        public string background;        // sprite in Resources/Backgrounds
        public string ambience;          // one line shown on arrival
        public string unlockedBy;        // action id, or empty for available from the start
        public Hotspot[] hotspots;
    }

    /// <summary>
    /// A rectangle in normalised 0..1 coordinates over the background, origin top-left.
    /// Deliberately not UnityEngine.Rect: plain floats read better in the JSON and keep
    /// JsonUtility out of struct-serialisation territory.
    /// </summary>
    [Serializable]
    public class Area
    {
        public float x, y, w, h;
    }

    /// <summary>A tappable region. Exactly one of the give* fields should be set.</summary>
    [Serializable]
    public class Hotspot
    {
        public string id;
        public string label;
        public Area area;
        public string requires;          // evidence/action id needed before it appears

        public string givesEvidence;     // evidence id to collect
        public string opensCharacter;    // character id to interrogate
        public string travelTo;          // location id to move to
        public string flavourText;       // shown when it grants nothing
    }

    /// <summary>
    /// A piece of evidence. The proves/notProves split is the teaching core of the game:
    /// a witness saying he saw a box does not prove what was inside it.
    /// </summary>
    [Serializable]
    public class Evidence
    {
        public string id;
        public string name;
        public string source;            // where it came from - chain of custody
        public string kind;              // record | statement | footage | physical
        public string summary;           // the line the player reads
        public string detail;            // longer text in the notebook
        public string icon;              // sprite in Resources/Evidence
        public string[] proves;
        public string[] notProves;
        public string unlockedBy;        // action id, empty if collectable directly
    }

    [Serializable]
    public class Character
    {
        public string id;
        public string name;
        public string role;
        public string portrait;          // sprite in Resources/Portraits
        public string greeting;
        public string greetingAfterConfront;
        public Question[] questions;
    }

    [Serializable]
    public class Question
    {
        public string id;
        public string text;
        public string answer;
        public string answerAfterConfront;   // empty means the answer does not change
        public string givesEvidence;
        public string requires;              // evidence/action id needed to ask this
        public bool onlyAfterConfront;
    }

    /// <summary>
    /// Two pieces of evidence the player must notice clash. Proving one does not solve
    /// the case - it only earns the right to confront someone.
    /// </summary>
    [Serializable]
    public class Contradiction
    {
        public string id;
        public string evidenceA;
        public string evidenceB;
        public string relation;              // contradicts | supports | explains
        public string claim;                 // what the player is asserting
        public string successText;
        public string unlocksConfrontation;  // confrontation id
    }

    [Serializable]
    public class Confrontation
    {
        public string id;
        public string characterId;
        public string requiresContradiction;
        public string[] evidenceToPresent;
        public DialogueLine[] dialogue;
        public string unlocksAction;         // the new investigative step it opens
        public string outcomeNote;
    }

    [Serializable]
    public class DialogueLine
    {
        public string speaker;               // character id, or "detective"
        public string text;
        public string mood;                  // calm | tense | breaking
    }

    /// <summary>
    /// An explicit investigative step, e.g. asking the guard to pull room footage for a
    /// stated window. Gating E5 behind this is what keeps the new evidence from feeling
    /// as though it simply appeared.
    /// </summary>
    [Serializable]
    public class CaseAction
    {
        public string id;
        public string label;
        public string description;
        public string requires;              // confrontation or evidence id
        public string grantsEvidence;
        public string unlocksLocation;
        public string resultText;
    }

    /// <summary>
    /// The final report. The player fills every slot from its own word bank, so the
    /// solution cannot be guessed and the player writes the conclusion themselves.
    /// </summary>
    [Serializable]
    public class Report
    {
        public string intro;
        public string template;              // uses {slotKey} placeholders
        public ReportSlot[] slots;
        public string[] requiredEvidence;    // must be cited for the report to stand
        public string evidencePrompt;
        public string successText;
        public string partialText;
        public string reconstruction;        // shown on the verdict screen
    }

    [Serializable]
    public class ReportSlot
    {
        public string key;                   // matches {key} in the template
        public string correct;
        public string[] options;             // includes the correct one; shuffled at runtime
    }

    [Serializable]
    public class Hint
    {
        public int level;                    // 1 = nudge, 2 = narrow, 3 = name the pair
        public string text;
        public string forStage;              // investigate | connect | confront | report
    }
}
