using System;
using System.Linq;

namespace Pursuit.Core
{
    public sealed class SituationVersion
    {
        private Snapshot baseline;
        private long revision;
        public long Observe(Snapshot snapshot)
        {
            bool changed = baseline == null || baseline.SessionId != snapshot.SessionId || baseline.VisualContact != snapshot.VisualContact
                || baseline.WantedLevel != snapshot.WantedLevel
                || !baseline.Units.Select(u => u.Id).OrderBy(id => id).SequenceEqual(snapshot.Units.Select(u => u.Id).OrderBy(id => id))
                || baseline.KnownSuspect.VehicleDescription != snapshot.KnownSuspect.VehicleDescription
                || baseline.KnownSuspect.Position.Distance(snapshot.KnownSuspect.Position) >= 60
                || HeadingDifference(baseline.KnownSuspect.Heading, snapshot.KnownSuspect.Heading) >= 30;
            if (changed) { revision++; baseline = Wire.Deserialize<Snapshot>(Wire.Serialize(snapshot)); }
            return revision;
        }
        private static double HeadingDifference(double a, double b) => Math.Abs(((a - b) % 360 + 540) % 360 - 180);
    }
}
