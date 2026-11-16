using System;
using Pursuit.Core;
using Xunit;

public class OverlayStateTests
{
    [Fact] public void ToggleHidesPanelAndMarkersTogether()
    {
        var state = new OverlayState(); Assert.False(state.Visible); Assert.Empty(state.Markers(new[] { new UnitState { Id = "a" } }, new[] { new Assignment { UnitId = "a", Role = "lead" } }));
        state.Toggle(); Assert.True(state.Visible); Assert.Single(state.Markers(new[] { new UnitState { Id = "a" } }, new[] { new Assignment { UnitId = "a", Role = "lead" } }));
        state.Toggle(); Assert.False(state.Visible); Assert.Empty(state.Markers(new[] { new UnitState { Id = "a" } }, new[] { new Assignment { UnitId = "a", Role = "lead" } }));
    }
    [Fact] public void DespawnAndPendingSelectionCannotCreateAcceptedMarkers()
    {
        var state = new OverlayState(); state.Toggle();
        Assert.Empty(state.Markers(Array.Empty<UnitState>(), new[] { new Assignment { UnitId = "gone", Role = "lead" } }));
        Assert.Empty(state.Markers(new[] { new UnitState { Id = "a" } }, Array.Empty<Assignment>()));
    }
}
