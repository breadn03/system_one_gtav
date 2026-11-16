using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using GTA;
using Pursuit.Core;

namespace Pursuit.Legacy
{
    public sealed class PursuitScript : Script
    {
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private readonly PoliceObserver observer = new PoliceObserver();
        private readonly RoadCandidates roadFinder = new RoadCandidates();
        private readonly VehicleOrders orders = new VehicleOrders();
        private readonly OverlayState overlay = new OverlayState();
        private readonly PursuitOverlay display = new PursuitOverlay();
        private readonly DecisionClient client;
        private readonly DecisionCoordinator coordinator;
        private readonly bool controlEnabled;
        private readonly Keys toggleKey;
        private readonly float markerRange;
        private readonly string logPath;
        private Knowledge memory = new Knowledge();
        private SituationVersion versions = new SituationVersion();
        private Snapshot snapshot;
        private RoadCandidate[] roads = Array.Empty<RoadCandidate>();
        private string session;
        private long nextCaptureMs;
        private string lastLoggedEvent;
        private bool keyHeld, faulted;

        public PursuitScript()
        {
            var settings = ScriptSettings.Load(Path.Combine("scripts", "Pursuit.ini"));
            controlEnabled = settings.GetValue("General", "ControlEnabled", false);
            toggleKey = settings.GetValue("Overlay", "ToggleKey", Keys.F9);
            markerRange = Math.Max(20, Math.Min(500, settings.GetValue("Overlay", "MarkerRange", 150f)));
            client = new DecisionClient();
            coordinator = new DecisionCoordinator(client,
                Math.Max(200, Math.Min(5000, settings.GetValue("Model", "RefreshMs", 1000))),
                Math.Max(200, Math.Min(10000, settings.GetValue("Model", "ExpiryMs", 2000))));
            logPath = Path.Combine("scripts", "Pursuit.decisions.jsonl");
            Interval = 0;
            Tick += OnTick;
            KeyDown += (s, e) => {
                if (e.KeyCode == toggleKey && !keyHeld) { overlay.Toggle(); keyHeld = true; }
                if (overlay.Visible && e.KeyCode == Keys.PageDown) display.Page++;
                if (overlay.Visible && e.KeyCode == Keys.PageUp) display.Page--;
            };
            KeyUp += (s, e) => { if (e.KeyCode == toggleKey) keyHeld = false; };
            Aborted += (s, e) => { orders.ReleaseAll(); client.Dispose(); };
        }
        private void OnTick(object sender, EventArgs args)
        {
            var nowMs = clock.ElapsedMilliseconds;
            try
            {
                if (!faulted && !Game.IsPaused)
                {
                    var player = Game.Player.Character;
                    var active = Game.Player.Wanted.WantedLevel > 0 && player.Exists() && !player.IsDead && player.IsInVehicle();
                    if (!active && session != null) EndSession();
                    if (active && nowMs >= nextCaptureMs)
                    {
                        nextCaptureMs = nowMs + 500;
                        if (session == null) { session = Guid.NewGuid().ToString("N"); versions = new SituationVersion(); memory = new Knowledge(); coordinator.Reset(session); }
                        var known = memory.Update(observer.Capture(nowMs), nowMs);
                        if (known != null && observer.Units.Length > 0)
                        {
                            roads = roadFinder.Find(known, observer.Units, nowMs, roads);
                            var candidates = AssignmentPolicy.Build(known, observer.Units, roads, orders.Accepted, observer.VisualContact);
                            snapshot = candidates.Length == 0 ? null : new Snapshot { SessionId = session,
                                CapturedMs = nowMs, WantedLevel = Game.Player.Wanted.WantedLevel, KnownSuspect = known, VisualContact = observer.VisualContact,
                                Units = observer.Units, Roads = roads, Candidates = candidates };
                            if (snapshot != null) snapshot.Revision = versions.Observe(snapshot);
                            if (controlEnabled)
                            {
                                orders.Maintain(known, observer.VisualContact, observer.Vehicles, nowMs);
                                if (snapshot != null && AssignmentPolicy.NeedsFallback(snapshot, orders.Accepted))
                                {
                                    var fallback = orders.Apply(snapshot, candidates[0], observer.Vehicles, observer.Matches, nowMs);
                                    coordinator.Trace.ExecutionStatus = "deterministic fallback: " + fallback.Status;
                                    if (fallback.Applied) { coordinator.Trace.AcceptedMs = nowMs; coordinator.Trace.HasAcceptedPlan = true; }
                                }
                                else if (snapshot == null) { orders.ReleaseAll(); coordinator.Trace.ExecutionStatus = "no feasible plan; units released to vanilla"; }
                            }
                        }
                        else { snapshot = null; if (controlEnabled) orders.Maintain(known, false, observer.Vehicles, nowMs); }
                    }
                    if (snapshot != null)
                    {
                        coordinator.Update(snapshot, nowMs);
                        var decision = coordinator.TryTakeDecision(nowMs);
                        if (decision != null)
                        {
                            var completed = coordinator.Trace.CompletedSnapshot;
                            var plan = completed.Candidates.First(p => p.Id == decision.CandidateId);
                            if (controlEnabled)
                            {
                                var applied = orders.Apply(snapshot, plan, observer.Vehicles, observer.Matches, nowMs);
                                coordinator.Trace.ExecutionStatus = applied.Status;
                                if (applied.Applied) { coordinator.Trace.AcceptedMs = nowMs; coordinator.Trace.HasAcceptedPlan = true; }
                            }
                            else coordinator.Trace.ExecutionStatus = "observation only; choice not applied";
                            Log(new { captured_ms = nowMs, request = completed, response = decision, execution = coordinator.Trace.ExecutionStatus });
                        }
                        var eventKey = coordinator.Trace.Status + "|" + coordinator.Trace.Pending + "|" + coordinator.Trace.ExecutionStatus;
                        if (lastLoggedEvent != eventKey)
                        {
                            lastLoggedEvent = eventKey;
                            Log(new { captured_ms = nowMs, status = coordinator.Trace.Status, pending = coordinator.Trace.Pending,
                                request = coordinator.Trace.LastSubmittedSnapshot, response = coordinator.Trace.LastAttemptResponse, execution = coordinator.Trace.ExecutionStatus });
                        }
                    }
                }
                display.Draw(overlay, snapshot, coordinator.Trace, observer.Units, orders.Accepted, orders.Destinations, observer.Vehicles, nowMs, controlEnabled, markerRange, toggleKey.ToString());
            }
            catch (Exception ex)
            {
                faulted = true;
                try { orders.ReleaseAll(); } catch { }
                coordinator.Trace.ExecutionStatus = "integration error; released tasks; reload script to resume";
                Log(new { error = ex.ToString() });
            }
        }
        private void EndSession()
        {
            orders.ReleaseAll(); observer.Reset(); memory = new Knowledge(); snapshot = null; roads = Array.Empty<RoadCandidate>(); session = null;
            coordinator.Reset(Guid.NewGuid().ToString("N"));
        }
        private void Log(object value)
        {
            // Small bounded log; a locked/missing file must never abort the pursuit.
            try
            {
                if (File.Exists(logPath) && new FileInfo(logPath).Length > 8 * 1024 * 1024) return;
                File.AppendAllText(logPath, Wire.Serialize(value) + Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
