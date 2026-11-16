using System;
using Pursuit.Core;
using Xunit;

public class KnowledgeTests
{
    [Fact] public void HiddenMovementDoesNotChangeKnowledge()
    {
        var memory = new Knowledge();
        memory.Update(new[] { new Observation { ObserverId = "u1", ObservedMs = 100, Position = new Point { X = 12 }, VehicleDescription = "red sedan" } }, 100);
        var before = Wire.Serialize(memory.Current);
        memory.Update(Array.Empty<Observation>(), 500);
        Assert.Equal(before, Wire.Serialize(memory.Current));
        Assert.Equal(100, memory.Current.ObservedMs);
    }
    [Fact] public void OlderReportCannotReplaceNewer()
    {
        var memory = new Knowledge();
        memory.Update(new[] { new Observation { ObservedMs = 200, Position = new Point { X = 20 } } }, 200);
        memory.Update(new[] { new Observation { ObservedMs = 100, Position = new Point { X = 99 } } }, 300);
        Assert.Equal(20, memory.Current.Position.X);
    }
    [Fact] public void FutureAndNonfiniteReportsAreIgnored()
    {
        var memory = new Knowledge();
        memory.Update(new[] { new Observation { ObservedMs = 201 }, new Observation { ObservedMs = 100, Position = new Point { X = double.NaN } } }, 200);
        Assert.Null(memory.Current);
    }
    [Fact] public void HandleReuseChangesIdentity()
    {
        var ids = new UnitIdentities();
        var first = ids.Get(42);
        ids.Retain(Array.Empty<int>());
        Assert.NotEqual(first, ids.Get(42));
    }
}
