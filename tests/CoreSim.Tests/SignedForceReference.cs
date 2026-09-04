namespace CoreSim.Tests;

// Independent test-only double-precision oracle. Deliberately does not call
// production force, kinematics or midpoint helpers. Never a runtime step option.
internal static class SignedForceReference
{
    internal readonly record struct Node(double Distance, double Start, double End, double StartTime, double Time)
    {
        internal double Acceleration => (End * End - Start * Start) / (2d * Distance);
    }

    internal static List<Node> Integrate(double initial, double distance, double force,
        double gearing = 0.5d, double step = 0.05d, double? target = null, double preparation = 2.6d)
    {
        double Acceleration(double speed)
        {
            var fade = 0.0175d + (0.005d - 0.0175d) * gearing;
            var envelope = speed <= 16d ? 1d : Math.Clamp(1d - fade * (speed - 16d), 0d, 1d);
            return (force * envelope - (40d + 0.20d * speed * speed)) / 142d;
        }
        double Advance(double speed, double acceleration, double ds)
            => Math.Sqrt(Math.Max(0d, speed * speed + 2d * acceleration * ds));
        var nodes = new List<Node>();
        var speed = initial;
        var time = 0d;
        for (var position = 0d; position < distance;)
        {
            var ds = Math.Min(step, distance - position);
            var predicted = Advance(speed, Acceleration(speed), ds);
            var full = Advance(speed, Acceleration((speed + predicted) / 2d), ds);
            var end = full;
            if (target is { } bound)
            {
                var allowed = Math.Sqrt(bound * bound + 2d * preparation * Math.Max(0d, distance - position - ds));
                var reachable = Advance(speed, -preparation, ds);
                end = Math.Min(full, Math.Max(allowed, reachable));
            }
            var dt = 2d * ds / (speed + end);
            nodes.Add(new Node(ds, speed, end, time, dt));
            speed = end;
            time += dt;
            position += ds;
        }
        return nodes;
    }
}
