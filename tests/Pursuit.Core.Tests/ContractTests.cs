using System;
using System.IO;
using Pursuit.Core;
using Xunit;

public class ContractTests
{
    private static string Read(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "examples", name + ".json"));
    [Fact] public void WireContainsOnlyContractPointFields()
    {
        var json = Newtonsoft.Json.Linq.JObject.Parse(Wire.Serialize(new Point { X = 1, Y = 2, Z = 3 }));
        Assert.Equal(3, json.Count);
    }
    [Theory] [InlineData("visible")] [InlineData("lost-contact")]
    public void SharedValidExamples(string name) { Assert.True(PlanValidator.ValidateSnapshot(Wire.Deserialize<Snapshot>(Read(name))).Valid); }
    [Fact] public void InvalidPlanIsRejected()
    {
        var snapshot = Wire.Deserialize<Snapshot>(Read("visible"));
        snapshot.Candidates[0].Assignments[0].UnitId = "missing";
        Assert.False(PlanValidator.ValidateSnapshot(snapshot).Valid);
    }
    [Fact] public void RejectsUnknownFields()
    { Assert.ThrowsAny<Exception>(() => Wire.Deserialize<Snapshot>(Read("visible").Replace("\"schema_version\"", "\"hidden_player_position\""))); }
    [Fact] public void DuplicateAndUnreachableAssignmentsFail()
    {
        var snapshot = Wire.Deserialize<Snapshot>(Read("visible"));
        snapshot.Roads[0].ReachableUnitIds = Array.Empty<string>();
        Assert.False(PlanValidator.ValidateSnapshot(snapshot).Valid);
        snapshot = Wire.Deserialize<Snapshot>(Read("visible"));
        snapshot.Candidates[0].Assignments = new[] { snapshot.Candidates[0].Assignments[0], snapshot.Candidates[0].Assignments[0] };
        Assert.False(PlanValidator.ValidateSnapshot(snapshot).Valid);
    }
}
