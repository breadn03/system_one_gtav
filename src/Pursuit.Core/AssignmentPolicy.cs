using System;
using System.Collections.Generic;
using System.Linq;

namespace Pursuit.Core
{
    public static class AssignmentPolicy
    {
        public static bool NeedsFallback(Snapshot snapshot, Assignment[] assignments)
        {
            var plan = new CandidatePlan { Id = "retained", Description = "Retained assignments", Assignments = assignments };
            return !PlanValidator.Validate(snapshot, plan).Valid
                || (snapshot.VisualContact && !assignments.Any(a => a.Role == "lead"))
                || (!snapshot.VisualContact && assignments.Any(a => a.Role == "lead" || a.Role == "support"));
        }
        public static bool SuspectRouteIsPlausible(Point start, Point destination, double roadDistance, double suspectHeading, double roadHeading)
        {
            if (!RouteIsFeasible(start, destination, roadDistance)) return false;
            if (roadDistance > start.Distance(destination) * 2.5 + 80) return false;
            var angle = suspectHeading * Math.PI / 180;
            var forwardDistance = -(destination.X - start.X) * Math.Sin(angle) + (destination.Y - start.Y) * Math.Cos(angle);
            var headingDifference = Math.Abs(((roadHeading - suspectHeading) % 360 + 540) % 360 - 180);
            return forwardDistance >= -40 && headingDifference <= 100;
        }
        public static bool RouteIsFeasible(Point start, Point destination, double roadDistance)
        {
            if (start == null || destination == null || !start.IsFinite || !destination.IsFinite || !Point.Finite(roadDistance)) return false;
            var direct = start.Distance(destination);
            return roadDistance >= 0 && roadDistance < 10000 && roadDistance <= direct * 4 + 200 && roadDistance + 25 >= direct;
        }
        public static CandidatePlan[] Build(KnownSuspect suspect, UnitState[] units, RoadCandidate[] roads, Assignment[] previous, bool visualContact)
        {
            if (suspect == null || units.Length == 0 || roads.Length == 0 || units.Any(u => !roads.Any(r => r.ReachableUnitIds.Contains(u.Id)))) return Array.Empty<CandidatePlan>();
            var plans = new List<CandidatePlan>();
            var shell = new Snapshot { Units = units, Roads = roads };
            var continuation = new CandidatePlan { Id = "A", Description = "Continue the current valid assignments.", Assignments = previous };
            if (PlanValidator.Validate(shell, continuation).Valid && (visualContact ? previous.Count(a => a.Role == "lead") == 1 : previous.All(a => a.Role != "lead" && a.Role != "support"))) plans.Add(continuation);
            var ordered = units.OrderBy(u => u.Id == suspect.ObserverId ? 0 : 1).ThenBy(u => u.Position.Distance(suspect.Position)).ThenBy(u => u.Id, StringComparer.Ordinal).ToArray();
            for (int variation = 0; variation < 3; variation++)
            {
                var assignments = new List<Assignment>();
                var useCount = new Dictionary<string, int>();
                var leader = ordered[variation == 1 && ordered.Length > 1 ? 1 : 0];
                var remaining = ordered.Where(u => u.Id != leader.Id).ToArray();
                foreach (var unit in new[] { leader }.Concat(remaining))
                {
                    var isLead = unit.Id == leader.Id;
                    var isSupport = visualContact && variation != 2 && remaining.Length > 0 && unit.Id == remaining[0].Id;
                    var role = visualContact && isLead ? "lead" : isSupport ? "support" : isLead ? "contain" : "intercept";
                    var choices = roads.Where(r => r.ReachableUnitIds.Contains(unit.Id));
                    if (role == "intercept")
                    {
                        var verified = choices.Where(r => r.SuspectRouteVerified).ToArray();
                        if (verified.Length > 0) choices = verified;
                        else role = "contain";
                    }
                    RoadCandidate road;
                    if (role == "lead" || role == "contain") road = choices.OrderBy(r => r.Position.Distance(suspect.Position)).First();
                    else road = choices.OrderBy(r => useCount.TryGetValue(r.Id, out var used) ? used : 0)
                        .ThenBy(r => variation == 1 ? -r.Position.Distance(suspect.Position) : unit.Position.Distance(r.Position)).First();
                    useCount[road.Id] = useCount.TryGetValue(road.Id, out var count) ? count + 1 : 1;
                    assignments.Add(new Assignment { UnitId = unit.Id, Role = role, DestinationNodeId = road.Id });
                }
                if (plans.Any(p => Same(p.Assignments, assignments.ToArray()))) continue;
                plans.Add(new CandidatePlan {
                    Id = ((char)('A' + plans.Count)).ToString(),
                    Description = visualContact
                        ? variation == 1 ? "Use an alternative lead and cover farther reachable routes." : variation == 2 ? "One unit maintains contact; spare units cover reachable routes." : "Closest reporting unit leads, one supports, spare units cover separate reachable routes."
                        : variation == 1 ? "Search farther predicted roads from the last sighting; no confirmed current target." : "Contain the last sighting and search other reachable predicted roads.",
                    Assignments = assignments.ToArray()
                });
            }
            return plans.ToArray();
        }
        private static bool Same(Assignment[] a, Assignment[] b) => a.Length == b.Length && a.All(x => b.Any(y => x.UnitId == y.UnitId && x.Role == y.Role && x.DestinationNodeId == y.DestinationNodeId));
    }
}
