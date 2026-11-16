using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Pursuit.Core
{
    public sealed class Point
    {
        [JsonProperty(Required = Required.Always)] public double X { get; set; }
        [JsonProperty(Required = Required.Always)] public double Y { get; set; }
        [JsonProperty(Required = Required.Always)] public double Z { get; set; }
        [JsonIgnore] public bool IsFinite => Finite(X) && Finite(Y) && Finite(Z);
        public static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public Point Copy() => new Point { X = X, Y = Y, Z = Z };
        public double Distance(Point other) => Math.Sqrt(Math.Pow(X - other.X, 2) + Math.Pow(Y - other.Y, 2) + Math.Pow(Z - other.Z, 2));
    }
    public class KnownSuspect
    {
        [JsonProperty(Required = Required.Always)] public Point Position { get; set; } = new Point();
        [JsonProperty(Required = Required.Always)] public double Heading { get; set; }
        [JsonProperty(Required = Required.Always)] public double Speed { get; set; }
        [JsonProperty(Required = Required.Always)] public long ObservedMs { get; set; }
        [JsonProperty(Required = Required.Always)] public string VehicleDescription { get; set; } = "unknown";
        [JsonProperty(Required = Required.Always)] public string ObserverId { get; set; } = "unknown";
    }
    public sealed class Observation : KnownSuspect { }
    public sealed class UnitState
    {
        [JsonProperty(Required = Required.Always)] public string Id { get; set; }
        [JsonProperty(Required = Required.Always)] public Point Position { get; set; } = new Point();
        [JsonProperty(Required = Required.Always)] public bool Available { get; set; } = true;
    }
    public sealed class RoadCandidate
    {
        [JsonProperty(Required = Required.Always)] public string Id { get; set; }
        [JsonProperty(Required = Required.Always)] public Point Position { get; set; } = new Point();
        [JsonProperty(Required = Required.Always)] public string[] ReachableUnitIds { get; set; } = Array.Empty<string>();
        [JsonProperty(Required = Required.Always)] public string Description { get; set; } = "observed road";
        [JsonProperty(Required = Required.Always)] public bool SuspectRouteVerified { get; set; }
    }
    public sealed class Assignment
    {
        [JsonProperty(Required = Required.Always)] public string UnitId { get; set; }
        [JsonProperty(Required = Required.Always)] public string Role { get; set; }
        [JsonProperty(Required = Required.Always)] public string DestinationNodeId { get; set; }
    }
    public sealed class CandidatePlan
    {
        [JsonProperty(Required = Required.Always)] public string Id { get; set; }
        [JsonProperty(Required = Required.Always)] public string Description { get; set; }
        [JsonProperty(Required = Required.Always)] public Assignment[] Assignments { get; set; } = Array.Empty<Assignment>();
    }
    public sealed class Snapshot
    {
        [JsonProperty(Required = Required.Always)] public int SchemaVersion { get; set; } = 1;
        [JsonProperty(Required = Required.Always)] public string SessionId { get; set; }
        [JsonProperty(Required = Required.Always)] public long Revision { get; set; }
        [JsonProperty(Required = Required.Always)] public long CapturedMs { get; set; }
        [JsonProperty(Required = Required.Always)] public int WantedLevel { get; set; }
        [JsonProperty(Required = Required.Always)] public KnownSuspect KnownSuspect { get; set; }
        [JsonProperty(Required = Required.Always)] public bool VisualContact { get; set; }
        [JsonProperty(Required = Required.Always)] public UnitState[] Units { get; set; } = Array.Empty<UnitState>();
        [JsonProperty(Required = Required.Always)] public RoadCandidate[] Roads { get; set; } = Array.Empty<RoadCandidate>();
        [JsonProperty(Required = Required.Always)] public CandidatePlan[] Candidates { get; set; } = Array.Empty<CandidatePlan>();
    }
    public sealed class DecisionResponse
    {
        [JsonProperty(Required = Required.Always)] public string SessionId { get; set; }
        [JsonProperty(Required = Required.Always)] public long Revision { get; set; }
        [JsonProperty(Required = Required.Always)] public string CandidateId { get; set; }
        [JsonProperty(Required = Required.Always)] public Dictionary<string, double> Probabilities { get; set; } = new Dictionary<string, double>();
        [JsonProperty(Required = Required.Always)] public double InferenceMs { get; set; }
        [JsonProperty(Required = Required.Always)] public string Status { get; set; } = "selected";
    }
    public static class Wire
    {
        private static JsonSerializerSettings Settings => new JsonSerializerSettings {
            ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() },
            MissingMemberHandling = MissingMemberHandling.Error,
            MaxDepth = 20,
            Culture = System.Globalization.CultureInfo.InvariantCulture
        };
        public static string Serialize(object value) => JsonConvert.SerializeObject(value, Settings);
        public static T Deserialize<T>(string json) => JsonConvert.DeserializeObject<T>(json, Settings);
    }
}
