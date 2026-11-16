using System;
using System.Collections.Generic;
using System.Linq;
using GTA;
using GTA.Native;
using Pursuit.Core;

namespace Pursuit.Legacy
{
    public sealed class PoliceObserver
    {
        private readonly UnitIdentities identities = new UnitIdentities();
        public Dictionary<string, Vehicle> Vehicles { get; } = new Dictionary<string, Vehicle>();
        public UnitState[] Units { get; private set; } = Array.Empty<UnitState>();
        public bool VisualContact { get; private set; }
        public static Point ToPoint(GTA.Math.Vector3 p) => new Point { X = p.X, Y = p.Y, Z = p.Z };
        public static GTA.Math.Vector3 ToVector(Point p) => new GTA.Math.Vector3((float)p.X, (float)p.Y, (float)p.Z);

        public Observation[] Capture(long nowMs)
        {
            var cars = World.GetAllVehicles().Where(v => v.Exists() && v.Model.IsCar && v.IsDriveable && v.Driver != null && v.Driver.Exists() && v.Driver.IsAlive
                && !v.Driver.IsPlayer && new[] { 6, 27, 29 }.Contains(Function.Call<int>(Hash.GET_PED_TYPE, v.Driver.Handle))
                && Function.Call<bool>(Hash.IS_PED_IN_ANY_POLICE_VEHICLE, v.Driver.Handle)
                && (v.IsSirenActive || Vehicles.Values.Any(old => old.Handle == v.Handle))).ToArray();
            identities.Retain(cars.Select(v => v.Driver.Handle).ToArray());
            Vehicles.Clear();
            foreach (var v in cars) Vehicles[identities.Get(v.Driver.Handle, v.Handle)] = v;
            Units = Vehicles.Select(pair => new UnitState { Id = pair.Key, Position = ToPoint(pair.Value.Position) }).ToArray();
            var player = Game.Player.Character;
            var reports = new List<Observation>();
            if (!player.Exists() || player.IsDead) { VisualContact = false; return reports.ToArray(); }
            Entity target = player.IsInVehicle() ? (Entity)player.CurrentVehicle : player;
            foreach (var pair in Vehicles)
            {
                var car = pair.Value;
                var delta = target.Position - car.Position;
                var range = delta.Length();
                var forward = car.ForwardVector;
                var facing = range < 15 || (forward.X * delta.X + forward.Y * delta.Y) / Math.Max(1, range) > -0.15f;
                if (range > 150 || !facing || !Function.Call<bool>(Hash.HAS_ENTITY_CLEAR_LOS_TO_ENTITY, car.Driver.Handle, target.Handle, 17)) continue;
                // Player data crosses into police memory only after the visibility gate above.
                reports.Add(new Observation { ObserverId = pair.Key, ObservedMs = nowMs,
                    Position = ToPoint(target.Position), Heading = target.Heading, Speed = target.Speed,
                    VehicleDescription = player.IsInVehicle() ? "vehicle model " + target.Model.Hash : "suspect on foot" });
            }
            VisualContact = reports.Count > 0;
            return reports.ToArray();
        }
        public void Reset() { Vehicles.Clear(); Units = Array.Empty<UnitState>(); identities.Retain(Array.Empty<int>()); VisualContact = false; }
        public bool Matches(string id, Vehicle vehicle) => vehicle != null && vehicle.Exists() && vehicle.Driver != null && vehicle.Driver.Exists() && identities.Matches(id, vehicle.Driver.Handle, vehicle.Handle);
    }
}
