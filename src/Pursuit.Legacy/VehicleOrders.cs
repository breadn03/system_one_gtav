using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Native;
using Pursuit.Core;

namespace Pursuit.Legacy
{
    public sealed class ExecutionResult
    {
        public bool Applied { get; set; }
        public string Status { get; set; }
    }
    public sealed class VehicleOrders
    {
        private sealed class Owned
        {
            public Vehicle Vehicle;
            public Ped Driver;
            public bool Blocked;
            public OrderState Order = new OrderState();
        }
        private readonly Dictionary<string, Owned> owned = new Dictionary<string, Owned>();
        public Assignment[] Accepted { get; private set; } = Array.Empty<Assignment>();

        public ExecutionResult Apply(Snapshot snapshot, CandidatePlan plan, Dictionary<string, Vehicle> vehicles, Func<string, Vehicle, bool> isCurrent, long nowMs)
        {
            var validation = PlanValidator.Validate(snapshot, plan);
            if (!validation.Valid) return new ExecutionResult { Status = "rejected: " + validation.Reason };
            if (plan.Assignments.Any(a => !vehicles.TryGetValue(a.UnitId, out var v) || !isCurrent(a.UnitId, v) || !v.Driver.IsAlive)) return new ExecutionResult { Status = "rejected: unavailable or changed unit" };
            var previous = Accepted;
            Accepted = plan.Assignments.Select(a => new Assignment { UnitId = a.UnitId, Role = a.Role, DestinationNodeId = a.DestinationNodeId }).ToArray();
            foreach (var a in Accepted)
            {
                var vehicle = vehicles[a.UnitId];
                if (!owned.ContainsKey(a.UnitId)) owned[a.UnitId] = new Owned { Vehicle = vehicle, Driver = vehicle.Driver, Blocked = vehicle.Driver.BlockPermanentEvents };
                var driver = owned[a.UnitId].Driver;
                driver.BlockPermanentEvents = true;
                var road = snapshot.Roads.First(r => r.Id == a.DestinationNodeId);
                var changed = !previous.Any(old => old.UnitId == a.UnitId && old.Role == a.Role && old.DestinationNodeId == a.DestinationNodeId);
                Drive(a.UnitId, road.Position, a.Role, nowMs, changed);
            }
            return new ExecutionResult { Applied = true, Status = "orders applied" };
        }
        private bool Drive(string id, Point destination, string role, long nowMs, bool force)
        {
            var unit = owned[id];
            if (!Bound(unit)) return false;
            var speed = role == "support" ? 25f : role == "contain" ? 28f : 36f;
            return unit.Order.Issue(destination, role, nowMs,
                () => unit.Driver.Task.DriveTo(unit.Vehicle, PoliceObserver.ToVector(destination), speed, (VehicleDrivingFlags)786603, role == "lead" ? 8f : 15f), force);
        }
        public void Maintain(KnownSuspect suspect, bool visualContact, Dictionary<string, Vehicle> active, long nowMs)
        {
            foreach (var id in owned.Keys.Where(id => !active.TryGetValue(id, out var car) || car.Handle != owned[id].Vehicle.Handle || !Bound(owned[id])).ToArray()) Release(id);
            if (suspect == null) return;
            foreach (var assignment in Accepted.Where(a => owned.ContainsKey(a.UnitId)))
            {
                if (assignment.Role != "lead" && assignment.Role != "support") continue;
                // Coordinate tasks carry no live player entity handle, including after loss of sight.
                var destination = suspect.Position.Copy();
                if (assignment.Role == "support")
                {
                    var angle = suspect.Heading * Math.PI / 180;
                    destination.X += Math.Sin(angle) * 35; destination.Y -= Math.Cos(angle) * 35;
                }
                var issued = Drive(assignment.UnitId, destination, visualContact ? assignment.Role : "contain", nowMs, !visualContact);
                if (!visualContact && issued) assignment.Role = "contain";
            }
            Accepted = Accepted.Where(a => owned.ContainsKey(a.UnitId)).ToArray();
        }
        private void Release(string id)
        {
            var unit = owned[id];
            if (unit.Driver.Exists())
            {
                if (Bound(unit)) unit.Driver.Task.ClearAll();
                unit.Driver.BlockPermanentEvents = unit.Blocked;
            }
            owned.Remove(id);
        }
        public void ReleaseAll() { foreach (var id in owned.Keys.ToArray()) Release(id); Accepted = Array.Empty<Assignment>(); }
        public Dictionary<string, Point> Destinations => owned.Where(p => p.Value.Order.Destination != null).ToDictionary(p => p.Key, p => p.Value.Order.Destination.Copy());
        private static bool Bound(Owned unit) => unit.Vehicle.Exists() && unit.Driver.Exists() && unit.Driver.IsAlive && unit.Vehicle.Driver != null && unit.Vehicle.Driver.Exists() && unit.Vehicle.Driver.Handle == unit.Driver.Handle;
    }
}
