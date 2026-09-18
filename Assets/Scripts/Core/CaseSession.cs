using System;
using System.Collections.Generic;
using System.Linq;
using TrueDetective.Data;

namespace TrueDetective.Core
{
    public enum Stage { Investigate, Connect, Confront, Report, Closed }

    public enum ConnectResult { Wrong, Correct, AlreadyKnown }

    /// <summary>Outcome of submitting the final report, with nothing leaked about which slots are wrong.</summary>
    public class ReportResult
    {
        public bool Accepted;
        public int CorrectSlots;
        public int TotalSlots;
        public string[] MissingEvidence = new string[0];
        public string Message;

        /// <summary>True when the player named a culprit off the back of the lie alone.</summary>
        public bool PrematureAccusation;
    }

    /// <summary>
    /// All progress for one playthrough, and every rule that decides what the player
    /// may do next. Holds no reference to any UI type, so the rules can be reasoned
    /// about - and reset - on their own.
    /// </summary>
    public class CaseSession
    {
        public readonly CaseData Case;

        private readonly HashSet<string> _evidence      = new HashSet<string>();
        private readonly HashSet<string> _asked         = new HashSet<string>();
        private readonly HashSet<string> _contradictions= new HashSet<string>();
        private readonly HashSet<string> _confrontations= new HashSet<string>();
        private readonly HashSet<string> _actions       = new HashSet<string>();
        private readonly HashSet<string> _visited       = new HashSet<string>();
        private readonly HashSet<string> _seenHotspots  = new HashSet<string>();

        private readonly Dictionary<Stage, int> _hintsUsed = new Dictionary<Stage, int>();

        public string CurrentLocationId { get; private set; }
        public bool Solved { get; private set; }
        public int FailedReportAttempts { get; private set; }

        /// <summary>Fires with the evidence id whenever a new piece is filed.</summary>
        public event Action<string> EvidenceCollected;
        /// <summary>Fires with a short line worth showing as a toast.</summary>
        public event Action<string> Notice;

        public CaseSession(CaseData data)
        {
            if (data == null) throw new ArgumentNullException("data");
            Case = data;
            Case.BuildIndex();
            CurrentLocationId = !string.IsNullOrEmpty(data.startLocationId)
                ? data.startLocationId
                : (data.locations != null && data.locations.Length > 0 ? data.locations[0].id : "");
            _visited.Add(CurrentLocationId);
        }

        // ------------------------------------------------------------------
        // queries
        // ------------------------------------------------------------------

        public bool HasEvidence(string id) { return !string.IsNullOrEmpty(id) && _evidence.Contains(id); }
        public bool HasAsked(string id) { return _asked.Contains(id); }
        public bool HasProven(string contradictionId) { return _contradictions.Contains(contradictionId); }
        public bool HasConfronted(string confrontationId) { return _confrontations.Contains(confrontationId); }
        public bool HasDoneAction(string actionId) { return _actions.Contains(actionId); }
        public bool HasSeenHotspot(string id) { return _seenHotspots.Contains(id); }
        public bool HasVisited(string locId) { return _visited.Contains(locId); }

        public int EvidenceCount { get { return _evidence.Count; } }
        public int EvidenceTotal { get { return Case.evidence != null ? Case.evidence.Length : 0; } }

        public IEnumerable<Evidence> CollectedEvidence
        {
            get
            {
                if (Case.evidence == null) yield break;
                // keep the case file's order so the notebook is stable between visits
                foreach (var e in Case.evidence)
                    if (e != null && _evidence.Contains(e.id)) yield return e;
            }
        }

        /// <summary>
        /// A requirement string is satisfied when it names evidence already filed, an
        /// action already taken, or a confrontation already held. Empty means no gate.
        /// </summary>
        public bool RequirementMet(string requires)
        {
            if (string.IsNullOrEmpty(requires)) return true;
            return _evidence.Contains(requires)
                || _actions.Contains(requires)
                || _confrontations.Contains(requires)
                || _contradictions.Contains(requires);
        }

        public bool HasConfrontedCharacter(string characterId)
        {
            if (Case.confrontations == null) return false;
            return Case.confrontations.Any(c => c != null
                && c.characterId == characterId && _confrontations.Contains(c.id));
        }

        // ------------------------------------------------------------------
        // locations and hotspots
        // ------------------------------------------------------------------

        public Location CurrentLocation { get { return Case.GetLocation(CurrentLocationId); } }

        public bool TravelTo(string locationId)
        {
            var loc = Case.GetLocation(locationId);
            if (loc == null) return false;
            if (!RequirementMet(loc.unlockedBy)) return false;
            CurrentLocationId = locationId;
            _visited.Add(locationId);
            return true;
        }

        /// <summary>Hotspots currently visible here, in case-file order.</summary>
        public IEnumerable<Hotspot> VisibleHotspots()
        {
            var loc = CurrentLocation;
            if (loc == null || loc.hotspots == null) yield break;
            foreach (var h in loc.hotspots)
            {
                if (h == null) continue;
                if (!RequirementMet(h.requires)) continue;
                // an evidence hotspot disappears once its evidence is filed
                if (!string.IsNullOrEmpty(h.givesEvidence) && _evidence.Contains(h.givesEvidence)) continue;
                yield return h;
            }
        }

        public void MarkHotspotSeen(string hotspotId)
        {
            if (!string.IsNullOrEmpty(hotspotId)) _seenHotspots.Add(hotspotId);
        }

        // ------------------------------------------------------------------
        // evidence
        // ------------------------------------------------------------------

        /// <summary>
        /// Files a piece of evidence. Refuses duplicates and anything still gated, so
        /// no path can hand the player E5 early.
        /// </summary>
        public bool CollectEvidence(string id)
        {
            var e = Case.GetEvidence(id);
            if (e == null) return false;
            if (_evidence.Contains(id)) return false;
            if (!RequirementMet(e.unlockedBy)) return false;

            _evidence.Add(id);
            if (EvidenceCollected != null) EvidenceCollected(id);
            if (Notice != null) Notice("أُضيف إلى دفتر الأدلة: " + e.name);
            return true;
        }

        // ------------------------------------------------------------------
        // interrogation
        // ------------------------------------------------------------------

        /// <summary>Questions the detective may put to this character right now.</summary>
        public IEnumerable<Question> AvailableQuestions(string characterId)
        {
            var c = Case.GetCharacter(characterId);
            if (c == null || c.questions == null) yield break;

            bool confronted = HasConfrontedCharacter(characterId);
            foreach (var q in c.questions)
            {
                if (q == null) continue;
                if (q.onlyAfterConfront && !confronted) continue;
                if (!RequirementMet(q.requires)) continue;
                yield return q;
            }
        }

        /// <summary>The answer text for a question, accounting for the confrontation.</summary>
        public string AnswerFor(string characterId, Question q)
        {
            if (q == null) return "";
            bool confronted = HasConfrontedCharacter(characterId);
            if (confronted && !string.IsNullOrEmpty(q.answerAfterConfront)) return q.answerAfterConfront;
            return q.answer;
        }

        public string GreetingFor(string characterId)
        {
            var c = Case.GetCharacter(characterId);
            if (c == null) return "";
            if (HasConfrontedCharacter(characterId) && !string.IsNullOrEmpty(c.greetingAfterConfront))
                return c.greetingAfterConfront;
            return c.greeting;
        }

        /// <summary>Records the question as asked and files any evidence it yields.</summary>
        public void AskQuestion(string characterId, Question q)
        {
            if (q == null) return;
            _asked.Add(q.id);
            if (!string.IsNullOrEmpty(q.givesEvidence)) CollectEvidence(q.givesEvidence);
        }

        // ------------------------------------------------------------------
        // connecting evidence
        // ------------------------------------------------------------------

        /// <summary>
        /// Tests an asserted relation between two pieces of evidence. Order does not
        /// matter, and a relation the case file does not list is simply wrong - there is
        /// no partial credit, because a guess should not read as progress.
        /// </summary>
        public ConnectResult TryConnect(string evidenceA, string evidenceB, string relation,
                                        out Contradiction matched)
        {
            matched = null;
            if (Case.contradictions == null) return ConnectResult.Wrong;
            if (string.IsNullOrEmpty(evidenceA) || string.IsNullOrEmpty(evidenceB)) return ConnectResult.Wrong;
            if (evidenceA == evidenceB) return ConnectResult.Wrong;
            if (!_evidence.Contains(evidenceA) || !_evidence.Contains(evidenceB)) return ConnectResult.Wrong;

            foreach (var c in Case.contradictions)
            {
                if (c == null) continue;
                bool samePair = (c.evidenceA == evidenceA && c.evidenceB == evidenceB)
                             || (c.evidenceA == evidenceB && c.evidenceB == evidenceA);
                if (!samePair) continue;
                if (c.relation != relation) continue;

                matched = c;
                if (_contradictions.Contains(c.id)) return ConnectResult.AlreadyKnown;

                _contradictions.Add(c.id);
                if (Notice != null) Notice("ثبت: " + c.claim);
                return ConnectResult.Correct;
            }
            return ConnectResult.Wrong;
        }

        // ------------------------------------------------------------------
        // confrontation
        // ------------------------------------------------------------------

        /// <summary>The confrontation this character is now open to, or null.</summary>
        public Confrontation PendingConfrontation(string characterId)
        {
            if (Case.confrontations == null) return null;
            foreach (var cf in Case.confrontations)
            {
                if (cf == null || cf.characterId != characterId) continue;
                if (_confrontations.Contains(cf.id)) continue;
                if (!_contradictions.Contains(cf.requiresContradiction)) continue;
                return cf;
            }
            return null;
        }

        /// <summary>
        /// Closes a confrontation and opens whatever investigative step it earns. The
        /// action is only unlocked - the evidence behind it still has to be requested,
        /// which is what stops E5 from feeling handed over.
        /// </summary>
        public void CompleteConfrontation(Confrontation cf)
        {
            if (cf == null || _confrontations.Contains(cf.id)) return;
            _confrontations.Add(cf.id);

            if (!string.IsNullOrEmpty(cf.unlocksAction))
            {
                var act = Case.GetAction(cf.unlocksAction);
                if (act != null && Notice != null) Notice("إجراء جديد متاح: " + act.label);
            }
        }

        // ------------------------------------------------------------------
        // actions
        // ------------------------------------------------------------------

        public IEnumerable<CaseAction> AvailableActions()
        {
            if (Case.actions == null) yield break;
            foreach (var a in Case.actions)
            {
                if (a == null || _actions.Contains(a.id)) continue;
                if (!RequirementMet(a.requires)) continue;
                yield return a;
            }
        }

        public bool PerformAction(string actionId)
        {
            var a = Case.GetAction(actionId);
            if (a == null || _actions.Contains(actionId)) return false;
            if (!RequirementMet(a.requires)) return false;

            _actions.Add(actionId);
            if (!string.IsNullOrEmpty(a.grantsEvidence)) CollectEvidence(a.grantsEvidence);
            return true;
        }

        // ------------------------------------------------------------------
        // the report
        // ------------------------------------------------------------------

        /// <summary>
        /// Grades the report. Reports the number of correct slots but never which ones,
        /// so a wrong answer sends the player back to the evidence rather than to a
        /// process of elimination.
        /// </summary>
        public ReportResult SubmitReport(Dictionary<string, string> answers, IEnumerable<string> citedEvidence)
        {
            var r = new ReportResult();
            var rep = Case.report;
            if (rep == null || rep.slots == null)
            {
                r.Message = "لا يوجد تقرير لهذه القضية.";
                return r;
            }

            r.TotalSlots = rep.slots.Length;
            foreach (var s in rep.slots)
            {
                if (s == null) continue;
                string given;
                if (answers != null && answers.TryGetValue(s.key, out given) && given == s.correct)
                    r.CorrectSlots++;
            }

            var cited = new HashSet<string>(citedEvidence ?? Enumerable.Empty<string>());
            var required = rep.requiredEvidence ?? new string[0];
            r.MissingEvidence = required.Where(id => !cited.Contains(id) || !_evidence.Contains(id)).ToArray();

            bool slotsPerfect = r.CorrectSlots == r.TotalSlots;
            bool evidenceComplete = r.MissingEvidence.Length == 0;

            // Naming a culprit on the strength of the lie alone is the mistake the case
            // is built to teach against, so it gets its own answer rather than a score.
            bool provenLie = _contradictions.Count > 0;
            bool missingTheProof = required.Any(id => !_evidence.Contains(id));
            if (!evidenceComplete && provenLie && missingTheProof)
            {
                r.PrematureAccusation = true;
                r.Message = "التناقض يستحق التحقيق، لكن الأدلة الحالية لا تثبت من أخرج المخطوطة.";
                FailedReportAttempts++;
                return r;
            }

            if (slotsPerfect && evidenceComplete)
            {
                r.Accepted = true;
                Solved = true;
                r.Message = rep.successText;
                return r;
            }

            FailedReportAttempts++;
            r.Message = rep.partialText;
            return r;
        }

        // ------------------------------------------------------------------
        // hints
        // ------------------------------------------------------------------

        /// <summary>
        /// Where the player actually is, derived from progress rather than tracked, so
        /// collecting evidence in an unusual order cannot strand the hint system.
        /// </summary>
        public Stage CurrentStage
        {
            get
            {
                if (Solved) return Stage.Closed;

                var required = Case.report != null && Case.report.requiredEvidence != null
                    ? Case.report.requiredEvidence : new string[0];
                if (required.Length > 0 && required.All(id => _evidence.Contains(id)))
                    return Stage.Report;

                if (_contradictions.Count > 0) return Stage.Confront;

                // enough on file to spot the clash, but the clash is not yet asserted
                bool canConnect = Case.contradictions != null && Case.contradictions.Any(c =>
                    c != null && _evidence.Contains(c.evidenceA) && _evidence.Contains(c.evidenceB));
                if (canConnect) return Stage.Connect;

                return Stage.Investigate;
            }
        }

        private static string StageKey(Stage s)
        {
            switch (s)
            {
                case Stage.Investigate: return "investigate";
                case Stage.Connect:     return "connect";
                case Stage.Confront:    return "confront";
                default:                return "report";
            }
        }

        public int HintsUsed(Stage s)
        {
            int n; return _hintsUsed.TryGetValue(s, out n) ? n : 0;
        }

        public int HintsAvailable(Stage s)
        {
            if (Case.hints == null) return 0;
            string key = StageKey(s);
            return Case.hints.Count(h => h != null && h.forStage == key);
        }

        /// <summary>
        /// Next hint for the current stage, escalating from a nudge to naming the pair.
        /// Costs nothing: no lives, no score, no scenes replayed.
        /// </summary>
        public string NextHint()
        {
            var stage = CurrentStage;
            if (Case.hints == null) return "لا توجد تلميحات لهذه القضية.";

            string key = StageKey(stage);
            var forStage = Case.hints.Where(h => h != null && h.forStage == key)
                                     .OrderBy(h => h.level).ToList();
            if (forStage.Count == 0) return "لا توجد تلميحات في هذه المرحلة.";

            int used = HintsUsed(stage);
            int idx = Math.Min(used, forStage.Count - 1);
            _hintsUsed[stage] = Math.Min(used + 1, forStage.Count);
            return forStage[idx].text;
        }

        // ------------------------------------------------------------------
        // replay
        // ------------------------------------------------------------------

        /// <summary>
        /// Clears every flag. Anything added to this class must be cleared here too, or
        /// a replay starts with the previous run's knowledge.
        /// </summary>
        public void Reset()
        {
            _evidence.Clear();
            _asked.Clear();
            _contradictions.Clear();
            _confrontations.Clear();
            _actions.Clear();
            _visited.Clear();
            _seenHotspots.Clear();
            _hintsUsed.Clear();

            Solved = false;
            FailedReportAttempts = 0;
            CurrentLocationId = !string.IsNullOrEmpty(Case.startLocationId)
                ? Case.startLocationId
                : (Case.locations != null && Case.locations.Length > 0 ? Case.locations[0].id : "");
            _visited.Add(CurrentLocationId);
        }
    }
}
