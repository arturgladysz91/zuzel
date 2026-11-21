namespace SharedWorldSpeedway;

public enum ContactRootStatus
{
    Converged,
    NoPhysicallyAdmissibleRoot,
    NumericalDiscontinuity,
    IterationLimit,
    AmbiguousOrMultipleRoots,
    InvalidPhysicalState
}

public sealed class ContactRootException(ContactRootStatus status, string detail)
    : InvalidOperationException($"H27-N {status}: {detail}")
{
    public ContactRootStatus Status { get; } = status;
}

public readonly record struct ContactResidual64<T>(double Ax, double Residual, T Value);

public static class ContactLoadSolver64
{
    public const int IterationLimit = 48;
    private const int InterpolationIterations = 12;
    public const int VerificationIntervals = 4;

    public static ContactResidual64<T> Solve<T>(double lo, double hi, double tolerance,
        Func<double, ContactResidual64<T>> evaluate, Func<int, bool> excludesRoots, bool bisect = false)
    {
        if (!double.IsFinite(lo) || !double.IsFinite(hi) || lo >= hi ||
            !double.IsFinite(tolerance) || tolerance <= 0)
            throw new ContactRootException(ContactRootStatus.InvalidPhysicalState, "invalid contact domain");
        ContactResidual64<T> At(double x)
        {
            var point = evaluate(x);
            if (!double.IsFinite(point.Residual))
                throw new ContactRootException(ContactRootStatus.InvalidPhysicalState, $"nonfinite residual at {x:R}");
            return point;
        }

        var previous = At(lo);
        var first = previous;
        ContactResidual64<T> left = default, right = default, exact = default;
        int crossings = 0, exactRoots = 0;
        if (previous.Residual == 0) { exact = previous; exactRoots++; }
        for (int i = 1; i <= VerificationIntervals; i++)
        {
            var next = At(i == VerificationIntervals ? hi : lo + (hi - lo) * i / VerificationIntervals);
            if (next.Residual == 0) { exact = next; exactRoots++; }
            if (Math.Sign(previous.Residual) * Math.Sign(next.Residual) == -1)
            {
                (left, right) = (previous, next);
                crossings++;
            }
            previous = next;
        }
        if (crossings + exactRoots > 1)
            throw new ContactRootException(ContactRootStatus.AmbiguousOrMultipleRoots, "multiple crossings on the deterministic domain grid");
        if (exactRoots == 1) return exact;
        if (crossings == 0)
        {
            var status = excludesRoots(Math.Sign(first.Residual))
                ? ContactRootStatus.NoPhysicallyAdmissibleRoot : ContactRootStatus.AmbiguousOrMultipleRoots;
            throw new ContactRootException(status, $"domain [{lo:R},{hi:R}], endpoint residuals [{first.Residual:R},{previous.Residual:R}]");
        }

        // Keep measured endpoint residuals intact when Illinois changes their interpolation weights.
        double weightL = left.Residual, weightR = right.Residual;
        int replaced = 0;
        for (int i = 0; i < IterationLimit; i++)
        {
            if (Math.Abs(left.Residual) <= tolerance) return left;
            if (Math.Abs(right.Residual) <= tolerance) return right;
            if (Math.BitIncrement(left.Ax) >= right.Ax)
                throw new ContactRootException(ContactRootStatus.NumericalDiscontinuity,
                    $"adjacent arguments [{left.Ax:R},{right.Ax:R}], residuals [{left.Residual:R},{right.Residual:R}]");
            double ax = (left.Ax * weightR - right.Ax * weightL) / (weightR - weightL);
            if (bisect || i >= InterpolationIterations || i % 4 == 3 ||
                !double.IsFinite(ax) || ax <= left.Ax || ax >= right.Ax)
                ax = left.Ax + (right.Ax - left.Ax) / 2;
            var point = At(ax);
            if (Math.Abs(point.Residual) <= tolerance) return point;
            if (Math.Sign(point.Residual) == Math.Sign(left.Residual))
            {
                left = point;
                weightL = point.Residual;
                weightR = replaced == -1 ? weightR / 2 : right.Residual;
                replaced = -1;
            }
            else
            {
                right = point;
                weightR = point.Residual;
                weightL = replaced == 1 ? weightL / 2 : left.Residual;
                replaced = 1;
            }
        }
        // Refine only the diagnosis after the solve budget. An attained tolerance here
        // identifies exhaustion of the iteration budget, and never accepts a new result.
        for (int i = 0; i < 64 && Math.BitIncrement(left.Ax) < right.Ax; i++)
        {
            var point=At(left.Ax+(right.Ax-left.Ax)/2);
            if (Math.Abs(point.Residual)<=tolerance)
                throw new ContactRootException(ContactRootStatus.IterationLimit,
                    $"tolerance first recovered during diagnostic bisection at {point.Ax:R}");
            if (Math.Sign(point.Residual)==Math.Sign(left.Residual)) left=point;
            else right=point;
        }
        var failure = Math.BitIncrement(left.Ax) >= right.Ax
            ? ContactRootStatus.NumericalDiscontinuity : ContactRootStatus.IterationLimit;
        throw new ContactRootException(failure, $"bracket [{left.Ax:R},{right.Ax:R}], residuals [{left.Residual:R},{right.Residual:R}]");
    }
}
