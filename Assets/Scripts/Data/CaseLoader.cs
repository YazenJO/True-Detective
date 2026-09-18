using UnityEngine;

namespace TrueDetective.Data
{
    /// <summary>
    /// Reads a case from Resources/Cases. Uses JsonUtility, which is why CaseData is
    /// all public fields and arrays - no dictionaries, no properties, no polymorphism.
    /// </summary>
    public static class CaseLoader
    {
        public const string CasesFolder = "Cases/";

        /// <summary>
        /// Loads and validates a case by id, e.g. "case01". Returns null and logs the
        /// reason on failure, so a broken case file never fails silently at runtime.
        /// </summary>
        public static CaseData Load(string caseId)
        {
            var text = Resources.Load<TextAsset>(CasesFolder + caseId);
            if (text == null)
            {
                Debug.LogError("[CaseLoader] no case file at Resources/" + CasesFolder + caseId + ".json");
                return null;
            }

            CaseData data;
            try
            {
                data = JsonUtility.FromJson<CaseData>(text.text);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[CaseLoader] " + caseId + " is not valid JSON: " + e.Message);
                return null;
            }

            if (data == null)
            {
                Debug.LogError("[CaseLoader] " + caseId + " parsed to null");
                return null;
            }

            data.BuildIndex();
            if (!Validate(data)) return null;
            return data;
        }

        /// <summary>
        /// Catches the case-authoring mistakes that would otherwise show up as a dead
        /// end mid-playthrough: ids pointing at nothing, or a report that cannot be
        /// completed from the evidence the case actually contains.
        /// </summary>
        public static bool Validate(CaseData d)
        {
            bool ok = true;

            if (string.IsNullOrEmpty(d.id)) { Err("case has no id"); ok = false; }
            if (d.locations == null || d.locations.Length == 0) { Err("case has no locations"); ok = false; }
            if (d.evidence == null || d.evidence.Length == 0) { Err("case has no evidence"); ok = false; }

            if (d.GetLocation(d.startLocationId) == null)
            { Err("startLocationId '" + d.startLocationId + "' is not a location"); ok = false; }

            // hotspots must point at something that exists
            if (d.locations != null)
            {
                foreach (var loc in d.locations)
                {
                    if (loc == null || loc.hotspots == null) continue;
                    foreach (var h in loc.hotspots)
                    {
                        if (h == null) continue;
                        string where = loc.id + "/" + h.id;
                        if (!string.IsNullOrEmpty(h.givesEvidence) && d.GetEvidence(h.givesEvidence) == null)
                        { Err(where + " gives unknown evidence '" + h.givesEvidence + "'"); ok = false; }
                        if (!string.IsNullOrEmpty(h.opensCharacter) && d.GetCharacter(h.opensCharacter) == null)
                        { Err(where + " opens unknown character '" + h.opensCharacter + "'"); ok = false; }
                        if (!string.IsNullOrEmpty(h.travelTo) && d.GetLocation(h.travelTo) == null)
                        { Err(where + " travels to unknown location '" + h.travelTo + "'"); ok = false; }
                        if (h.area == null)
                        { Err(where + " has no area"); ok = false; }
                    }
                }
            }

            // questions and actions must point at real evidence
            if (d.characters != null)
            {
                foreach (var c in d.characters)
                {
                    if (c == null || c.questions == null) continue;
                    foreach (var q in c.questions)
                    {
                        if (q == null) continue;
                        if (!string.IsNullOrEmpty(q.givesEvidence) && d.GetEvidence(q.givesEvidence) == null)
                        { Err(c.id + "/" + q.id + " gives unknown evidence '" + q.givesEvidence + "'"); ok = false; }
                        if (string.IsNullOrEmpty(q.answer) && string.IsNullOrEmpty(q.answerAfterConfront))
                        { Err(c.id + "/" + q.id + " has no answer in either state"); ok = false; }
                    }
                }
            }

            if (d.contradictions != null)
            {
                foreach (var c in d.contradictions)
                {
                    if (c == null) continue;
                    if (d.GetEvidence(c.evidenceA) == null || d.GetEvidence(c.evidenceB) == null)
                    { Err("contradiction " + c.id + " references unknown evidence"); ok = false; }
                }
            }

            if (d.actions != null)
            {
                foreach (var a in d.actions)
                {
                    if (a == null) continue;
                    if (!string.IsNullOrEmpty(a.grantsEvidence) && d.GetEvidence(a.grantsEvidence) == null)
                    { Err("action " + a.id + " grants unknown evidence '" + a.grantsEvidence + "'"); ok = false; }
                }
            }

            // the report has to be winnable
            if (d.report != null)
            {
                if (d.report.slots == null || d.report.slots.Length == 0)
                { Err("report has no slots"); ok = false; }
                else
                {
                    foreach (var s in d.report.slots)
                    {
                        if (s == null) continue;
                        if (s.options == null || s.options.Length < 2)
                        { Err("slot " + s.key + " needs at least two options"); ok = false; continue; }
                        bool hasCorrect = false;
                        foreach (var o in s.options) if (o == s.correct) hasCorrect = true;
                        if (!hasCorrect)
                        { Err("slot " + s.key + " has no option matching its correct value"); ok = false; }
                        if (!d.report.template.Contains("{" + s.key + "}"))
                        { Err("slot " + s.key + " has no placeholder in the template"); ok = false; }
                    }
                }

                if (d.report.requiredEvidence != null)
                    foreach (var id in d.report.requiredEvidence)
                        if (d.GetEvidence(id) == null)
                        { Err("report requires unknown evidence '" + id + "'"); ok = false; }
            }

            return ok;
        }

        private static void Err(string m) { Debug.LogError("[CaseLoader] " + m); }
    }
}
