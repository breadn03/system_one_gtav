using System;
using System.Collections.Generic;
using System.Linq;

namespace Pursuit.Core
{
    public sealed class Knowledge
    {
        public KnownSuspect Current { get; private set; }
        public KnownSuspect Update(Observation[] reports, long nowMs)
        {
            var latest = reports.Where(r => r != null && r.Position != null && r.Position.IsFinite && r.ObservedMs >= 0 && r.ObservedMs <= nowMs && Point.Finite(r.Heading) && Point.Finite(r.Speed) && r.Speed >= 0)
                .OrderByDescending(r => r.ObservedMs).ThenBy(r => r.ObserverId, StringComparer.Ordinal).FirstOrDefault();
            if (latest != null && (Current == null || latest.ObservedMs > Current.ObservedMs))
                Current = new KnownSuspect { Position = latest.Position.Copy(), Heading = latest.Heading, Speed = latest.Speed, ObservedMs = latest.ObservedMs, VehicleDescription = latest.VehicleDescription, ObserverId = latest.ObserverId };
            return Current;
        }
    }
    public sealed class UnitIdentities
    {
        private readonly Dictionary<int, string> ids = new Dictionary<int, string>();
        private readonly Dictionary<int, int> vehicles = new Dictionary<int, int>();
        private int generation;
        public string Get(int handle)
        {
            if (!ids.TryGetValue(handle, out var id)) ids[handle] = id = handle + "-" + ++generation;
            return id;
        }
        public string Get(int driverHandle, int vehicleHandle)
        {
            if (vehicles.TryGetValue(driverHandle, out var old) && old != vehicleHandle) ids.Remove(driverHandle);
            vehicles[driverHandle] = vehicleHandle;
            return Get(driverHandle);
        }
        public bool Matches(string id, int driverHandle, int vehicleHandle) => ids.TryGetValue(driverHandle, out var current) && current == id && vehicles.TryGetValue(driverHandle, out var bound) && bound == vehicleHandle;
        public void Retain(int[] handles)
        {
            var active = new HashSet<int>(handles);
            foreach (var handle in ids.Keys.Where(h => !active.Contains(h)).ToArray()) { ids.Remove(handle); vehicles.Remove(handle); }
        }
    }
}
