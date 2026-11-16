using System;
using System.Linq;

namespace Pursuit.Core
{
    public sealed class ValidationResult
    {
        public bool Valid { get; }
        public string Reason { get; }
        public ValidationResult(bool valid, string reason = "") { Valid = valid; Reason = reason; }
    }
    public static class PlanValidator
    {
        private static bool Id(string s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 96;
        public static ValidationResult ValidateSnapshot(Snapshot s)
        {
            if (s == null || s.SchemaVersion != 1 || !Id(s.SessionId) || s.Revision < 0 || s.CapturedMs < 0 || s.WantedLevel < 1 || s.WantedLevel > 5) return new ValidationResult(false, "invalid snapshot identity");
            var k = s.KnownSuspect;
            if (k == null || k.Position == null || !k.Position.IsFinite || !Point.Finite(k.Speed) || k.Speed < 0 || !Point.Finite(k.Heading) || k.ObservedMs < 0 || k.ObservedMs > s.CapturedMs || !Id(k.ObserverId) || k.VehicleDescription == null || k.VehicleDescription.Length > 256) return new ValidationResult(false, "invalid sighting");
            if (s.Units == null || s.Units.Length == 0 || s.Units.Length > 128 || s.Units.Any(u => u == null || !Id(u.Id) || !u.Available || u.Position == null || !u.Position.IsFinite) || s.Units.Select(u => u.Id).Distinct().Count() != s.Units.Length) return new ValidationResult(false, "invalid units");
            if (s.Roads == null || s.Roads.Length == 0 || s.Roads.Length > 256 || s.Roads.Any(r => r == null || !Id(r.Id) || r.Position == null || !r.Position.IsFinite || r.ReachableUnitIds == null || r.ReachableUnitIds.Any(id => !s.Units.Any(u => u.Id == id)) || r.Description == null || r.Description.Length > 256) || s.Roads.Select(r => r.Id).Distinct().Count() != s.Roads.Length) return new ValidationResult(false, "invalid roads");
            if (s.Candidates == null || s.Candidates.Length == 0 || s.Candidates.Length > 8 || s.Candidates.Any(p => p == null || !Id(p.Id)) || s.Candidates.Select(p => p.Id).Distinct().Count() != s.Candidates.Length) return new ValidationResult(false, "invalid candidate IDs");
            foreach (var p in s.Candidates) { var result = Validate(s, p); if (!result.Valid) return result; }
            return new ValidationResult(true);
        }
        public static ValidationResult Validate(Snapshot s, CandidatePlan p)
        {
            if (p == null || !Id(p.Id) || string.IsNullOrWhiteSpace(p.Description) || p.Description.Length > 2048 || p.Assignments == null || p.Assignments.Length != s.Units.Length) return new ValidationResult(false, "incomplete plan");
            if (p.Assignments.Any(a => a == null) || p.Assignments.Select(a => a.UnitId).Distinct().Count() != s.Units.Length || p.Assignments.Count(a => a.Role == "lead") > 1) return new ValidationResult(false, "duplicate assignments or lead");
            foreach (var a in p.Assignments)
                if (!s.Units.Any(u => u.Id == a.UnitId) || !new[] { "lead", "support", "intercept", "contain" }.Contains(a.Role) || !s.Roads.Any(r => r.Id == a.DestinationNodeId && r.ReachableUnitIds.Contains(a.UnitId) && (a.Role != "intercept" || r.SuspectRouteVerified))) return new ValidationResult(false, "invalid or unreachable assignment");
            return new ValidationResult(true);
        }
        public static bool ValidateResponse(Snapshot s, DecisionResponse r)
        {
            if (r == null || r.Status != "selected" || r.SessionId != s.SessionId || r.Revision != s.Revision || !Point.Finite(r.InferenceMs) || r.InferenceMs < 0 || r.Probabilities == null || r.Probabilities.Count != s.Candidates.Length || !s.Candidates.Any(p => p.Id == r.CandidateId)) return false;
            return s.Candidates.All(p => r.Probabilities.TryGetValue(p.Id, out var value) && Point.Finite(value) && value >= 0 && value <= 1) && Math.Abs(r.Probabilities.Values.Sum() - 1) <= 0.02;
        }
    }
}
