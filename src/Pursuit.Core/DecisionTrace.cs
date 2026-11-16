namespace Pursuit.Core
{
    public sealed class DecisionTrace
    {
        public Snapshot CompletedSnapshot { get; internal set; }
        public DecisionResponse Response { get; internal set; }
        public long CompletedMs { get; internal set; }
        public string Status { get; internal set; } = "idle";
        public string ExecutionStatus { get; set; } = "no order applied";
        public long AcceptedMs { get; set; }
        public bool HasAcceptedPlan { get; set; }
        public Snapshot LastSubmittedSnapshot { get; internal set; }
        public DecisionResponse LastAttemptResponse { get; internal set; }
        public bool Pending { get; internal set; }
    }
}
