using System;
using System.Threading;
using System.Threading.Tasks;

namespace Pursuit.Core
{
    public sealed class DecisionCoordinator
    {
        private readonly IDecisionClient client;
        private readonly long refreshMs, expiryMs;
        private Task<DecisionResponse> active;
        private Snapshot pending, latest;
        private DecisionResponse ready;
        private string session;
        private long startedMs, nextRequestMs;
        public DecisionTrace Trace { get; private set; } = new DecisionTrace();
        public Snapshot PendingSnapshot => pending;

        public DecisionCoordinator(IDecisionClient client, long refreshMs = 1000, long expiryMs = 2000)
        {
            if (refreshMs <= 0 || expiryMs <= 0) throw new ArgumentOutOfRangeException();
            this.client = client; this.refreshMs = refreshMs; this.expiryMs = expiryMs;
        }
        public void Reset(string sessionId)
        {
            session = sessionId; latest = null; ready = null; nextRequestMs = 0;
            Trace = new DecisionTrace { Pending = active != null, Status = active == null ? "idle" : "waiting for old request" };
            // Keep the outstanding request until it finishes; HTTP cancellation cannot stop GPU work.
        }
        public void Update(Snapshot snapshot, long nowMs)
        {
            if (!PlanValidator.ValidateSnapshot(snapshot).Valid) { Trace.Status = "invalid snapshot; retain valid orders"; return; }
            if (session != snapshot.SessionId) Reset(snapshot.SessionId);
            latest = snapshot;
            if (ready != null && (ready.SessionId != snapshot.SessionId || ready.Revision != snapshot.Revision)) ready = null;
            if (active != null && active.IsCompleted)
            {
                Trace.Pending = false;
                if (!active.IsCanceled && !active.IsFaulted) Trace.LastAttemptResponse = active.Result;
                if (active.IsCanceled || active.IsFaulted)
                {
                    var observed = active.Exception;
                    Trace.Status = "request failed; retain valid orders";
                }
                else if (nowMs - startedMs > expiryMs) Trace.Status = "expired response; retain valid orders";
                else if (pending.SessionId != session || pending.Revision != snapshot.Revision) Trace.Status = "stale response discarded";
                else if (!PlanValidator.ValidateResponse(pending, active.Result)) Trace.Status = "invalid response discarded";
                else if (!PlanValidator.Validate(snapshot, System.Linq.Enumerable.First(pending.Candidates, p => p.Id == active.Result.CandidateId)).Valid) Trace.Status = "invalidated route or unit; response discarded";
                else
                {
                    ready = active.Result; Trace.Response = ready; Trace.CompletedSnapshot = pending;
                    Trace.CompletedMs = nowMs; Trace.Status = "selected; awaiting execution";
                }
                active = null; pending = null;
            }
            if (active != null) { if (nowMs - startedMs > expiryMs) Trace.Status = "expired request; waiting without blocking"; return; }
            if (nowMs < nextRequestMs || ready != null) return;
            pending = Wire.Deserialize<Snapshot>(Wire.Serialize(latest));
            Trace.LastSubmittedSnapshot = pending; Trace.LastAttemptResponse = null;
            startedMs = nowMs; nextRequestMs = nowMs + refreshMs; Trace.Pending = true;
            try { active = client.DecideAsync(pending, CancellationToken.None); }
            catch (Exception) { active = null; pending = null; Trace.Pending = false; Trace.Status = "request failed; retain valid orders"; }
        }
        public DecisionResponse TryTakeDecision(long nowMs)
        {
            var result = ready; ready = null;
            return result != null && nowMs - startedMs <= expiryMs ? result : null;
        }
    }
}
