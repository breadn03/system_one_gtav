using System;
using System.Linq;
using Pursuit.Core;
using Xunit;

public class AssignmentPolicyTests
{
    private static UnitState[] Units => new[] { new UnitState { Id = "a", Position = new Point() }, new UnitState { Id = "b", Position = new Point { X = 100 } }, new UnitState { Id = "c", Position = new Point { X = 200 } } };
    private static RoadCandidate[] Roads => new[] { new RoadCandidate { SuspectRouteVerified = true, Id = "north", Position = new Point { Y = 100 }, ReachableUnitIds = new[] { "a", "b", "c" } }, new RoadCandidate { SuspectRouteVerified = true, Id = "east", Position = new Point { X = 200 }, ReachableUnitIds = new[] { "a", "b", "c" } } };
    [Fact] public void CandidatePlansCoverUnitsWithoutDogpile()
    {
        var plans = AssignmentPolicy.Build(new KnownSuspect { ObserverId = "a" }, Units, Roads, Array.Empty<Assignment>(), true);
        Assert.NotEmpty(plans);
        Assert.All(plans, p => { Assert.Equal(3, p.Assignments.Select(a => a.UnitId).Distinct().Count()); Assert.Single(p.Assignments, a => a.Role == "lead"); Assert.True(p.Assignments.Count(a => a.Role == "support") <= 1); });
        Assert.Contains(plans, p => p.Assignments.Any(a => a.Role == "intercept"));
    }
    [Fact] public void LeadReplacementUsesRemainingUnits()
    {
        var plans = AssignmentPolicy.Build(new KnownSuspect { ObserverId = "gone" }, Units.Skip(1).ToArray(), Roads, Array.Empty<Assignment>(), true);
        Assert.All(plans, p => Assert.DoesNotContain(p.Assignments, a => a.UnitId == "gone"));
        Assert.All(plans, p => Assert.Single(p.Assignments, a => a.Role == "lead"));
    }
    [Fact] public void PreserveValidAssignments()
    {
        var previous = new[] { new Assignment { UnitId = "a", Role = "lead", DestinationNodeId = "north" }, new Assignment { UnitId = "b", Role = "intercept", DestinationNodeId = "east" }, new Assignment { UnitId = "c", Role = "contain", DestinationNodeId = "north" } };
        var plans = AssignmentPolicy.Build(new KnownSuspect { ObserverId = "a" }, Units, Roads, previous, true);
        Assert.Equal("Continue the current valid assignments.", plans[0].Description);
        Assert.Equal("east", plans[0].Assignments.Single(a => a.UnitId == "b").DestinationNodeId);
    }
    [Fact] public void DisconnectedNearbyRoadIsNotReachable()
    {
        Assert.False(AssignmentPolicy.RouteIsFeasible(new Point(), new Point { X = 2, Z = 12 }, 100000));
        Assert.False(AssignmentPolicy.RouteIsFeasible(new Point(), new Point { X = 2, Z = 12 }, double.NaN));
        Assert.True(AssignmentPolicy.RouteIsFeasible(new Point(), new Point { X = 100 }, 130));
    }
    [Fact] public void LostContactHasNoLiveLeadOrSupport()
    {
        var plans = AssignmentPolicy.Build(new KnownSuspect(), Units, Roads, Array.Empty<Assignment>(), false);
        Assert.All(plans.SelectMany(p => p.Assignments), a => Assert.Contains(a.Role, new[] { "contain", "intercept" }));
    }
    [Fact] public void RegainingVisualMustNotRetainALeaderlessPlan()
    {
        var contained = Units.Select(u => new Assignment { UnitId = u.Id, Role = "contain", DestinationNodeId = "north" }).ToArray();
        var plans = AssignmentPolicy.Build(new KnownSuspect { ObserverId = "a" }, Units, Roads, contained, true);
        Assert.All(plans, p => Assert.Single(p.Assignments, a => a.Role == "lead"));
    }
}
