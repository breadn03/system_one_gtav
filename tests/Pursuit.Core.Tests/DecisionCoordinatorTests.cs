using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pursuit.Core;
using Xunit;

public class DecisionCoordinatorTests
{
    private sealed class Client : IDecisionClient
    {
        public readonly List<TaskCompletionSource<DecisionResponse>> Calls = new List<TaskCompletionSource<DecisionResponse>>();
        public Task<DecisionResponse> DecideAsync(Snapshot snapshot, CancellationToken token)
        { var call = new TaskCompletionSource<DecisionResponse>(); Calls.Add(call); return call.Task; }
    }
    private static Snapshot Sample() => Wire.Deserialize<Snapshot>(System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "examples/visible.json")));
    private static DecisionResponse Answer(Snapshot s) => new DecisionResponse { SessionId = s.SessionId, Revision = s.Revision, CandidateId = "A", Probabilities = new Dictionary<string, double> { ["A"] = .7, ["B"] = .3 } };

    [Fact] public void OnlyOneRequestAndLatestUpdateIsCoalesced()
    {
        var client = new Client(); var c = new DecisionCoordinator(client, 1000, 2000); var s = Sample();
        c.Update(s, 1000); s.Revision = 2; c.Update(s, 1100); s.Revision = 3; c.Update(s, 1200);
        Assert.Single(client.Calls);
        var original = Sample(); client.Calls[0].SetResult(Answer(original)); c.Update(s, 2000);
        Assert.Null(c.TryTakeDecision(2000));
        Assert.Equal(2, client.Calls.Count);
        var answer = Answer(s); client.Calls[1].SetResult(answer); c.Update(s, 2100);
        Assert.Equal(3, c.TryTakeDecision(2100).Revision);
    }
    [Fact] public void ExpiryDoesNotStartConcurrentInference()
    {
        var client = new Client(); var c = new DecisionCoordinator(client, 1000, 2000); var s = Sample();
        c.Update(s, 1000); c.Update(s, 5000); Assert.Single(client.Calls);
        client.Calls[0].SetResult(Answer(s)); c.Update(s, 5100); Assert.Null(c.TryTakeDecision(5100));
        Assert.Contains("expired", c.Trace.Status);
    }
    [Fact] public void PreviousSessionCannotControlReusedEntities()
    {
        var client = new Client(); var c = new DecisionCoordinator(client); var s = Sample();
        c.Update(s, 1000); c.Reset("new-session"); var old = Answer(s);
        s.SessionId = "new-session"; c.Update(s, 1100); Assert.Single(client.Calls);
        client.Calls[0].SetResult(old); c.Update(s, 1200); Assert.Null(c.TryTakeDecision(1200));
    }
    [Fact] public void InvalidModelResultIsRejected()
    {
        var client = new Client(); var c = new DecisionCoordinator(client); var s = Sample(); c.Update(s, 1000);
        var result = Answer(s); result.CandidateId = "missing"; client.Calls[0].SetResult(result);
        c.Update(s, 1100); Assert.Null(c.TryTakeDecision(1100)); Assert.Contains("invalid", c.Trace.Status);
    }
    [Fact] public void FailureRetainsLastCompletedTrace()
    {
        var client = new Client(); var c = new DecisionCoordinator(client); var s = Sample(); c.Update(s, 1000);
        client.Calls[0].SetResult(Answer(s)); c.Update(s, 1100); Assert.NotNull(c.TryTakeDecision(1100));
        c.Update(s, 2100); client.Calls[1].SetException(new Exception("offline")); c.Update(s, 2200);
        Assert.Null(c.TryTakeDecision(2200)); Assert.Equal("A", c.Trace.Response.CandidateId);
        Assert.Contains("failed", c.Trace.Status);
    }
    [Fact] public void CallerMutationCannotAlterInFlightSnapshot()
    {
        var client = new Client(); var c = new DecisionCoordinator(client); var s = Sample(); c.Update(s, 1000);
        s.Candidates[0].Assignments[0].Role = "contain";
        Assert.Equal("lead", c.PendingSnapshot.Candidates[0].Assignments[0].Role);
    }
    [Fact] public void RoutineCaptureDuringRequestDoesNotDiscardIt()
    {
        var client = new Client(); var c = new DecisionCoordinator(client); var version = new SituationVersion(); var s = Sample();
        s.Revision = version.Observe(s); c.Update(s, 1000);
        s.CapturedMs = 1500; s.KnownSuspect.ObservedMs = 1450; s.Revision = version.Observe(s); c.Update(s, 1500);
        client.Calls[0].SetResult(Answer(s)); c.Update(s, 1600);
        Assert.NotNull(c.TryTakeDecision(1600));
    }
}
