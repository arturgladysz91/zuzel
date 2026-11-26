
namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record Balance215(State215 State, double Residual, int Iterations, double[] Balance);

    private static Balance215 SteadyCircle215(UnifiedModel215 engine, Geometry215 geometry, State215 initial,
        double speed, double yawRate, double time, double ratio, bool balanceRider = false)
    {
        var g = geometry.Base;
        var yaw = Vec215.Up * yawRate;
        // Variables are roll, pitch, COM height, steer, velocity heading, relative wheel and
        // crank rates, then Drive. Residuals are co-rotating Newton/Euler and shaft balance.
        double rim = speed / g.Radius;
        double bank = -Math.Atan(speed * yawRate / Gravity215);
        var supported = balanceRider
            ? geometry.Supported(initial with { Pose = Attitude215.Pose(0, initial.Pose.Pitch, bank) },
                Gravity215, UnifiedModel215.Stiffness)
            : initial;
        double[] x = [bank, supported.Pose.Pitch, balanceRider ? supported.Center.Z : .528,
            .050, -.015, rim * 1.015, rim * 1.015 * ratio, .15];
        if (balanceRider) x = [.. x, initial.Rider.X, initial.Rider.Y, initial.Rider.Z];
        int frontVariable = x.Length;
        var front = geometry.FrontContact;
        if (front.PolarInertiaKgM2 > 0) x = [.. x, rim];
        if (front.RelaxationMeters > 0) x = [.. x, 0d];
        double[] sample = Get(x).Balance;
        int i = 0;
        for (; i < 45 && Norm(sample) > 2e-4; i++)
        {
            int n = x.Length;
            var matrix = new double[n, n + 1];
            for (int j = 0; j < n; j++)
            {
                double h = j is 5 or 6 || (front.PolarInertiaKgM2 > 0 && j == frontVariable) ? .002 : .00002;
                var shifted = (double[])x.Clone();
                shifted[j] += h;
                var result = Get(shifted).Balance;
                for (int k = 0; k < n; k++) matrix[k, j] = (result[k] - sample[k]) / h;
            }
            for (int j = 0; j < n; j++) matrix[j, n] = -sample[j];
            if (!Solve(matrix)) break;
            bool advanced = false;
            for (double fraction = 1; fraction >= .0001; fraction /= 2)
            {
                var candidate = x.Select((value, j) => value + fraction * matrix[j, n]).ToArray();
                if (candidate[7] is < 0 or > 1 || Math.Abs(candidate[0]) > .65 || Math.Abs(candidate[1]) > .2 ||
                    candidate[2] is < .35 or > .65 || Math.Abs(candidate[3]) > .28 || candidate[5] < 1 || candidate[6] < 1)
                    continue;
                var compare = Get(candidate).Balance;
                if (Norm(compare) >= Norm(sample)) continue;
                (x, sample) = (candidate, compare);
                advanced = true;
                break;
            }
            if (!advanced) break;
        }
        return new(Get(x).State, Norm(sample), i, sample);

        (State215 State, double[] Balance) Get(double[] a)
        {
            var pose = Attitude215.Pose(0, a[1], a[0]);
            var w = pose.Body(yaw);
            var v = new Vec215(Math.Cos(a[4]) * speed, Math.Sin(a[4]) * speed, 0);
            var s = initial with
            {
                Time = time, Pose = pose, Center = new(0, 0, a[2]), Momentum = v * g.Mass,
                Steer = (float)a[3], Command = new(new((float)a[3], (float)a[7]), default),
                Crank = (a[6] + w.Y) * g.CrankInertia, Wheel = (a[5] + w.Y) * g.WheelInertia,
                Rider = balanceRider ? new(a[8], a[9], a[10]) : initial.Rider
            };
            if (front.PolarInertiaKgM2 > 0)
                s = s with { FrontWheel = a[frontVariable] * front.PolarInertiaKgM2 };
            if (front.RelaxationMeters > 0)
                s = s with { FrontShearMeters = a[frontVariable] };
            s = geometry.WithRates(s, w, default);
            try
            {
                const double h = .0001;
                var step = engine.Step(s, [], h);
                var f = step.Forces;
                var force = (f.Front.Force + f.Rear.Force + f.Drag) / g.Mass -
                    Vec215.Up * Gravity215 - yaw.Cross(v);
                var moment = (f.Front.Patch.Arm.Cross(f.Front.Force) + f.Rear.Patch.Arm.Cross(f.Rear.Force) -
                    yaw.Cross(s.Angular)) / (g.Mass * .55);
                double[] residual = [force.X, force.Y, force.Z, moment.X, moment.Y, moment.Z, f.WheelDerivative / 50, f.CrankDerivative / 10];
                if (balanceRider)
                {
                    var r = (step.After.RiderMomentum - s.RiderMomentum) / (h * g.RiderMass);
                    residual = [.. residual, r.X, r.Y, r.Z];
                }
                if (front.PolarInertiaKgM2 > 0) residual = [.. residual, f.FrontResponse.RotorTorqueNm / 50];
                if (front.RelaxationMeters > 0) residual = [.. residual, f.FrontResponse.StrainRateMetersPerSecond * 10];
                return (s, residual);
            }
            catch (InvalidOperationException)
            {
                return (s, Enumerable.Repeat(1000d, a.Length).ToArray());
            }
        }

        static double Norm(double[] v) => Math.Sqrt(v.Sum(x => x * x));

        static bool Solve(double[,] a)
        {
            int n = a.GetLength(0);
            for (int i = 0; i < n; i++)
            {
                int pivot = i;
                for (int k = i + 1; k < n; k++)
                    if (Math.Abs(a[k, i]) > Math.Abs(a[pivot, i])) pivot = k;
                if (Math.Abs(a[pivot, i]) < 1e-9) return false;
                double divisor = a[pivot, i];
                for (int j = i; j <= n; j++)
                    (a[i, j], a[pivot, j]) = (a[pivot, j], a[i, j]);
                for (int j = i; j <= n; j++) a[i, j] /= divisor;
                for (int k = 0; k < n; k++)
                {
                    if (k == i) continue;
                    double multiplier = a[k, i];
                    for (int j = i; j <= n; j++) a[k, j] -= multiplier * a[i, j];
                }
            }
            return true;
        }
    }
}
