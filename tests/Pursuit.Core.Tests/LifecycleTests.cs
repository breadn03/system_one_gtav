using System;
using System.Linq;
using Pursuit.Core;
using Xunit;

public class LifecycleTests
{
    private static Snapshot Sample() => Wire.Deserialize<Snapshot>(System.IO.File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "examples/visible.json")));
    [Fact] public void RoutineCapturesDoNotInvalidateUsablePlan()
    {
        var version = new SituationVersion(); var s = Sample(); var original = version.Observe(s);
        s.CapturedMs += 500; s.KnownSuspect.ObservedMs += 500; s.KnownSuspect.Position.Y += 10;
        Assert.Equal(original, version.Observe(s));
        s.VisualContact = false; Assert.True(version.Observe(s) > original);
    }
    [Fact] public void AccumulatedMovementAndRouteTurnInvalidate()
    {
        var version = new SituationVersion(); var s = Sample(); var original = version.Observe(s);
        s.KnownSuspect.Position.Y += 70; Assert.True(version.Observe(s) > original);
        original = version.Observe(s); s.KnownSuspect.Heading += 45; Assert.True(version.Observe(s) > original);
    }
    [Fact] public void DriverSwitchingCarsChangesIdentity()
    {
        var ids = new UnitIdentities(); var old = ids.Get(42, 500);
        Assert.True(ids.Matches(old, 42, 500));
        Assert.NotEqual(old, ids.Get(42, 501));
        Assert.False(ids.Matches(old, 42, 500));
    }
    [Fact] public void ContactLossIssuesContainmentEvenDuringThrottle()
    {
        var state = new OrderState(); int issued = 0;
        Assert.True(state.Issue(new Point(), "lead", 1000, () => issued++));
        Assert.True(state.Issue(new Point(), "contain", 1400, () => issued++));
        Assert.Equal(2, issued);
        Assert.False(state.Issue(new Point(), "contain", 1500, () => issued++));
    }
    [Fact] public void FailedTaskDoesNotChangeAcceptedExecutionState()
    {
        var state = new OrderState(); state.Issue(new Point(), "lead", 1000, () => { });
        Assert.Throws<InvalidOperationException>(() => { state.Issue(new Point(), "contain", 1400, () => throw new InvalidOperationException()); });
        Assert.Equal("lead", state.Role);
    }
    [Fact] public void InvalidRouteAndMissingLeadRequireFallback()
    {
        var s = Sample(); var accepted = s.Candidates[0].Assignments;
        Assert.False(AssignmentPolicy.NeedsFallback(s, accepted));
        s.Roads[0].ReachableUnitIds = Array.Empty<string>(); Assert.True(AssignmentPolicy.NeedsFallback(s, accepted));
        s = Sample(); accepted = s.Candidates[0].Assignments; accepted[0].Role = "contain"; Assert.True(AssignmentPolicy.NeedsFallback(s, accepted));
        Assert.True(AssignmentPolicy.NeedsFallback(s, Array.Empty<Assignment>()));
    }
    [Fact] public void PoliceReachabilityDoesNotProveSuspectCanReachIntercept()
    {
        Assert.True(AssignmentPolicy.RouteIsFeasible(new Point { X = 100 }, new Point { X = 120, Z = 12 }, 150));
        Assert.False(AssignmentPolicy.SuspectRouteIsPlausible(new Point(), new Point { X = 120, Z = 12 }, 100000, 90, 90));
        Assert.False(AssignmentPolicy.SuspectRouteIsPlausible(new Point(), new Point { Y = 120 }, 150, 0, 180));
    }
    [Fact] public void MissingRequiredWireFieldsAreRejected()
    {
        var json = Newtonsoft.Json.Linq.JObject.Parse(Wire.Serialize(Sample()));
        json.Remove("visual_contact");
        Assert.ThrowsAny<Exception>(() => Wire.Deserialize<Snapshot>(json.ToString()));
    }
}
