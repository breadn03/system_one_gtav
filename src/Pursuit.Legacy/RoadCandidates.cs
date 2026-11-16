using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GTA.Math;
using GTA.Native;
using Pursuit.Core;

namespace Pursuit.Legacy
{
    public sealed class RoadCandidates
    {
        public RoadCandidate[] Find(KnownSuspect suspect, UnitState[] units, long nowMs, RoadCandidate[] previous)
        {
            var result = new List<RoadCandidate>();
            var angle = suspect.Heading * Math.PI / 180;
            var forward = new Point { X = -Math.Sin(angle), Y = Math.Cos(angle) };
            var ageSeconds = Math.Max(0, nowMs - suspect.ObservedMs) / 1000.0;
            var predictedDistance = Math.Min(350, suspect.Speed * Math.Min(ageSeconds, 10) + 100);
            var probes = new List<Point> { suspect.Position.Copy() };
            foreach (var distance in new[] { -35.0, 80.0, predictedDistance, predictedDistance + 90 })
                probes.Add(new Point { X = suspect.Position.X + forward.X * distance, Y = suspect.Position.Y + forward.Y * distance, Z = suspect.Position.Z });
            foreach (var side in new[] { -100.0, 100.0 })
                probes.Add(new Point { X = suspect.Position.X + forward.X * 100 + forward.Y * side, Y = suspect.Position.Y + forward.Y * 100 - forward.X * side, Z = suspect.Position.Z });
            probes.AddRange(units.Select(u => u.Position));
            probes.AddRange(previous.Select(r => r.Position));
            foreach (var probe in probes)
            {
                if (result.Count >= 64) break;
                using (var position = new OutputArgument())
                using (var heading = new OutputArgument())
                {
                    if (!Function.Call<bool>(Hash.GET_CLOSEST_VEHICLE_NODE_WITH_HEADING, (float)probe.X, (float)probe.Y, (float)probe.Z, position, heading, 1, 3.0f, 0)) continue;
                    var node = position.GetResult<Vector3>(); var point = PoliceObserver.ToPoint(node);
                    if (result.Any(r => r.Position.Distance(point) < 12)) continue;
                    var reachable = new List<string>();
                    var distances = new List<string>();
                    foreach (var unit in units)
                    {
                        var distance = Function.Call<float>(Hash.CALCULATE_TRAVEL_DISTANCE_BETWEEN_POINTS,
                            (float)unit.Position.X, (float)unit.Position.Y, (float)unit.Position.Z, node.X, node.Y, node.Z);
                        if (!AssignmentPolicy.RouteIsFeasible(unit.Position, point, distance)) continue;
                        reachable.Add(unit.Id); distances.Add(unit.Id + "=" + Math.Round(distance).ToString(CultureInfo.InvariantCulture) + "m");
                    }
                    if (reachable.Count == 0) continue;
                    var nodeHeading = heading.GetResult<float>();
                    var suspectDistance = Function.Call<float>(Hash.CALCULATE_TRAVEL_DISTANCE_BETWEEN_POINTS,
                        (float)suspect.Position.X, (float)suspect.Position.Y, (float)suspect.Position.Z, node.X, node.Y, node.Z);
                    var suspectRouteVerified = AssignmentPolicy.SuspectRouteIsPlausible(suspect.Position, point, suspectDistance, suspect.Heading, nodeHeading);
                    result.Add(new RoadCandidate { Id = NodeId(point), Position = point, ReachableUnitIds = reachable.ToArray(),
                        SuspectRouteVerified = suspectRouteVerified,
                        Description = (point.Distance(suspect.Position) < 45 ? "near last sighting" : "predicted road, not a confirmed sighting")
                        + "; heading " + Math.Round(nodeHeading).ToString(CultureInfo.InvariantCulture) + "; route " + string.Join(",", distances).Substring(0, Math.Min(130, string.Join(",", distances).Length)) });
                }
            }
            return result.ToArray();
        }
        private static string NodeId(Point p) => string.Format(CultureInfo.InvariantCulture, "road-{0:0}-{1:0}-{2:0}", p.X, p.Y, p.Z);
    }
}
