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

public sealed class ContactRootException(ContactRootStatus status, string detail, string variant)
    : InvalidOperationException($"{variant} {status}: {detail}")
{
    public ContactRootException(ContactRootStatus status, string detail) : this(status, detail, "H27-N") { }

    public ContactRootStatus Status { get; } = status;
    public string Detail { get; } = detail;
}

public readonly record struct ContactResidual64<T>(double Ax, double Residual, T Value);

public sealed class ContactSearchMetrics64
{
    public int VisitedBoxes { get; internal set; }
    public int EarlyExclusions { get; internal set; }
    public long IsolationTicks { get; internal set; }
    public long RefinementTicks { get; internal set; }
}

public static class ContactLoadSolver64
{
    public const int IterationLimit = 48;
    private const int InterpolationIterations = 12;
    public const int VerificationIntervals = 4;

    // Bounds are supplied by the specific constitutive law. An unresolved box
    // prevents acceptance, including when another box exhibited a valid root.
    public static ContactResidual64<T> SolveCertified<T>(double lo,double hi,double tolerance,
        Func<double,ContactResidual64<T>> at,
        Func<double,double,ContactResidual64<T>,ContactSlope64> enclose,bool bisect=false,
        Func<ContactResidual64<T>,ContactResidual64<T>,bool>? earlyExclude=null,
        ContactSearchMetrics64? metrics=null)
    {
        if (!double.IsFinite(lo)||!double.IsFinite(hi)||lo>=hi||!double.IsFinite(tolerance)||tolerance<=0)
            throw new ContactRootException(ContactRootStatus.InvalidPhysicalState,"invalid certified contact domain");
        long started=metrics is null?0:System.Diagnostics.Stopwatch.GetTimestamp();
        var first=at(lo);
        var last=at(hi);
        if(!double.IsFinite(first.Residual)||!double.IsFinite(last.Residual))
            throw new ContactRootException(ContactRootStatus.InvalidPhysicalState,"nonfinite certified endpoints");
        var stack=new Stack<(ContactResidual64<T> Low,ContactResidual64<T> High)>();
        stack.Push((first,last));
        (ContactResidual64<T> Low,ContactResidual64<T> High)? bracket=null;
        ContactResidual64<T> bracketMiddle=default;
        const int boxBudget=512;
        for (int i=0;i<boxBudget&&stack.TryPop(out var box);i++)
        {
            if(metrics is not null) metrics.VisitedBoxes++;
            if(earlyExclude is not null&&Math.Sign(box.Low.Residual)==Math.Sign(box.High.Residual)&&
                box.Low.Residual!=0&&earlyExclude(box.Low,box.High))
            {
                if(metrics is not null) metrics.EarlyExclusions++;
                continue;
            }
            double a=box.Low.Ax,b=box.High.Ax;
            var mid=at(a+(b-a)/2);
            var enclosure=enclose(a,b,mid);
            if (!double.IsFinite(mid.Residual))
                throw new ContactRootException(ContactRootStatus.InvalidPhysicalState,"nonfinite certified residual");
            if (enclosure.Value.ExcludesZero) continue;
            if (enclosure.Slope.ExcludesZero)
            {
                int sa=Math.Sign(box.Low.Residual),sb=Math.Sign(box.High.Residual);
                if (sa==sb&&sa!=0) continue;
                var candidate=sa==0?(box.Low,box.Low):sb==0?(box.High,box.High):(box.Low,box.High);
                if (bracket is { } known&&
                    (known.Low.Ax!=candidate.Item1.Ax||known.High.Ax!=candidate.Item2.Ax))
                    throw new ContactRootException(ContactRootStatus.AmbiguousOrMultipleRoots,"distinct isolated contact roots");
                bracket=candidate;
                bracketMiddle=mid;
                continue;
            }
            if (b-a<=1e-9)
                throw new ContactRootException(ContactRootStatus.AmbiguousOrMultipleRoots,
                    $"cannot isolate root in [{a:R},{b:R}]");
            stack.Push((mid,box.High));
            stack.Push((box.Low,mid));
        }
        if(metrics is not null)
        {
            metrics.IsolationTicks=System.Diagnostics.Stopwatch.GetTimestamp()-started;
            started=System.Diagnostics.Stopwatch.GetTimestamp();
        }
        if (stack.Count!=0)
            throw new ContactRootException(ContactRootStatus.AmbiguousOrMultipleRoots,"contact enclosure budget exhausted");
        if (bracket is not { } root)
            throw new ContactRootException(ContactRootStatus.NoPhysicallyAdmissibleRoot,
                $"H28-C interval exclusion, endpoint residuals [{first.Residual:R},{last.Residual:R}]");
        var result=Refine();
        if(metrics is not null) metrics.RefinementTicks=System.Diagnostics.Stopwatch.GetTimestamp()-started;
        return result;

        ContactResidual64<T> Refine()
        {
            if(root.Low.Ax==root.High.Ax) return root.Low;
            if(root.Low.Ax<0&&root.High.Ax>0)
            {
                var rest=at(0);
                if(rest.Residual==0) return rest;
            }
            // Preserve the frozen refinement arithmetic, including its grid.
            // Reuse only exact abscissas from this same immutable contact query.
            return Solve(root.Low.Ax,root.High.Ax,tolerance,earlyExclude is null?at:Reuse,_=>false,bisect);
        }

        ContactResidual64<T> Reuse(double ax)
        {
            if(ax==root.Low.Ax) return root.Low;
            if(ax==root.High.Ax) return root.High;
            if(ax==bracketMiddle.Ax) return bracketMiddle;
            return at(ax);
        }
    }

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
