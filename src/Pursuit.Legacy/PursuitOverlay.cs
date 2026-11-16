using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using GTA;
using GTA.Math;
using GTA.UI;
using Pursuit.Core;

namespace Pursuit.Legacy
{
    public sealed class PursuitOverlay
    {
        public int Page { get; set; }
        private static string Clip(string text, int length = 88)
        {
            var safe = (text ?? "").Replace("~", "").Replace("\r", " ").Replace("\n", " ");
            return safe.Length <= length ? safe : safe.Substring(0, length - 3) + "...";
        }
        private static void Text(string text, float x, float y, Color color, float scale = .30f)
            => new TextElement(Clip(text), new PointF(x, y), scale, color, GTA.UI.Font.ChaletLondon).Draw();

        public void Draw(OverlayState state, Snapshot snapshot, DecisionTrace trace, UnitState[] units, Assignment[] accepted, Dictionary<string, Pursuit.Core.Point> destinations, Dictionary<string, Vehicle> vehicles, long nowMs, bool controlEnabled, float markerRange, string toggleKey)
        {
            if (!state.Visible) return; // This single guard owns both panel and all markers.
            var lines = new List<string> { "LAYA PURSUIT COMMAND | " + (controlEnabled ? "EXPERIMENTAL CONTROL" : "OBSERVATION ONLY"), toggleKey + ": hide | PageUp/PageDown: units" };
            lines.Add(snapshot == null ? "Waiting for a police sighting / active vehicle pursuit" : "Wanted " + snapshot.WantedLevel + " | " + (snapshot.VisualContact ? "VISUAL CONTACT" : "CONTACT LOST") + " | report " + ((nowMs - snapshot.KnownSuspect.ObservedMs) / 1000.0).ToString("0.0") + "s old");
            lines.Add("Decision: " + trace.Status + (trace.Pending ? " | inference pending" : ""));
            lines.Add("Execution: " + trace.ExecutionStatus);
            if (trace.HasAcceptedPlan) lines.Add("Accepted plan age: " + ((nowMs - trace.AcceptedMs) / 1000.0).ToString("0.0") + "s");
            var pages = Math.Max(1, (units.Length + 5) / 6);
            Page = ((Page % pages) + pages) % pages;
            lines.Add("UNITS " + units.Length + " | page " + (Page + 1) + "/" + pages);
            foreach (var unit in units.Skip(Page * 6).Take(6))
            {
                var role = accepted.FirstOrDefault(a => a.UnitId == unit.Id);
                var destination = destinations.TryGetValue(unit.Id, out var actual) ? actual.X.ToString("0") + "," + actual.Y.ToString("0") + "," + actual.Z.ToString("0") : "not issued";
                lines.Add(unit.Id + " | " + (role == null ? "vanilla / unassigned" : role.Role + " -> " + destination));
            }
            if (trace.LastSubmittedSnapshot != null)
            {
                var attempt = trace.LastAttemptResponse;
                lines.Add(attempt == null ? "Submitted alternatives | " + (trace.Pending ? "pending" : "no usable response") : "LAYA RETURNED " + attempt.CandidateId + " | " + attempt.InferenceMs.ToString("0") + " ms");
                lines.Add("Model probabilities (not tactical success rates):");
                foreach (var plan in trace.LastSubmittedSnapshot.Candidates)
                {
                    var probability = 0.0;
                    var available = attempt?.Probabilities != null && attempt.Probabilities.TryGetValue(plan.Id, out probability) && Pursuit.Core.Point.Finite(probability);
                    lines.Add(plan.Id + " " + (available ? probability.ToString("P0") : "--") + " | " + plan.Description);
                }
            }
            lines.Add("Police reports and predicted roads only; no hidden target marker.");
            var height = Math.Min(640, 18 + lines.Count * 23);
            new GTA.UI.ContainerElement(new PointF(12, 18), new SizeF(690, height), Color.FromArgb(195, 10, 15, 22)).Draw();
            for (var i = 0; i < lines.Count; i++) Text(lines[i], 24, 26 + i * 23, i == 0 ? Color.LightSkyBlue : Color.White);

            var roles = state.Markers(units, accepted).ToDictionary(a => a.UnitId, a => a.Role);
            foreach (var pair in vehicles)
            {
                var vehicle = pair.Value;
                if (!vehicle.Exists() || !vehicle.IsOnScreen || vehicle.Position.DistanceTo(Game.Player.Character.Position) > markerRange) continue;
                var position = vehicle.Position + new Vector3(0, 0, 2.2f);
                var screen = GTA.UI.Screen.WorldToScreen(position);
                if (screen.X <= 0 || screen.Y <= 0 || screen.X >= GTA.UI.Screen.Width || screen.Y >= GTA.UI.Screen.Height) continue;
                var role = roles.TryGetValue(pair.Key, out var acceptedRole) ? acceptedRole : "vanilla";
                Text(pair.Key + " | " + role.ToUpperInvariant(), screen.X, screen.Y, Color.LightSkyBlue, .27f);
            }
        }
    }
}
