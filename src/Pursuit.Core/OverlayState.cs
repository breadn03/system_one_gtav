using System.Collections.Generic;
using System.Linq;

namespace Pursuit.Core
{
    public sealed class OverlayState
    {
        public bool Visible { get; private set; }
        public void Toggle() => Visible = !Visible;
        public Assignment[] Markers(UnitState[] units, Assignment[] acceptedAssignments)
        {
            if (!Visible) return System.Array.Empty<Assignment>();
            var existing = new HashSet<string>(units.Where(u => u.Available).Select(u => u.Id));
            return acceptedAssignments.Where(a => existing.Contains(a.UnitId)).ToArray();
        }
    }
}
