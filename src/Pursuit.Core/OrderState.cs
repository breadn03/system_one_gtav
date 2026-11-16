using System;

namespace Pursuit.Core
{
    public sealed class OrderState
    {
        public Point Destination { get; private set; }
        public string Role { get; private set; }
        public long IssuedMs { get; private set; }
        public bool Issue(Point destination, string role, long nowMs, Action issueTask, bool force = false)
        {
            var changedRole = Role != role;
            if (!force && !changedRole && nowMs - IssuedMs < 750) return false;
            if (!force && !changedRole && Destination != null && Destination.Distance(destination) < 15 && nowMs - IssuedMs < 5000) return false;
            issueTask();
            Destination = destination.Copy(); Role = role; IssuedMs = nowMs;
            return true;
        }
    }
}
