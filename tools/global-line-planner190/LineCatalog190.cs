using System.Numerics;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal sealed record LineShape190(string Id, string Family, double Entry, double Apex, double Exit,
    double InitialTurnMeters, double ApexFraction, double StraightenMeters, double Sweep, double Distance);
internal sealed record LaneKnot190(double Offset, MeterPoint XY, double Course, double Curvature);
internal sealed record Sector190(LineShape190 Shape, LaneKnot190[] Points);
internal sealed record Catalog190(int Draws, int Boundary, int Similar, Sector190[] Accepted);

/// <summary>Two sectors extend to the midpoints of adjacent straights. Every
/// sector joins at the same offset, with zero first and second derivatives.</summary>
internal sealed class LineCatalog190(TrackFrame frame)
{
    private const int SampleCount = 320;
    private const int DrawCount = 1600;
    private const double StraightJoinMeters = 6.5;
    private const double MaxReferenceCurvature = .1; // bound used by ExperimentalTrackShape
    private readonly EdgeIndex edges = new(frame);
    private readonly BendMap bends = new(frame);
    private static readonly int[] Primes = [2, 3, 5, 7, 11, 13, 17];

    internal double Origin => bends.At(0,0).Entry - 31;
    internal int HalfSamples => SampleCount;
    internal double Step => frame.LapMeters / (2 * SampleCount);

    internal Catalog190 Build(int index)
    {
        var bend = bends.At(0,index);
        double length = bend.Length + 62;
        if (Math.Abs(length - SampleCount * Step) > 1e-6)
            throw new InvalidOperationException("The two frozen sectors must have equal length.");
        var accepted = new List<Sector190>();
        var signatures = new List<double[]>();
        int boundaryRejected = 0, similarRejected = 0;
        for (int i = 0; i < DrawCount; i++)
        {
            var raw = Enumerable.Range(0,Primes.Length).Select(j=>RadicalInverse(i+1,Primes[j])).ToArray();
            var family = (i % 5) switch {0=>"late-apex",1=>"early-apex",2=>"wide-radius",3=>"full-width",_=>"progressive"};
            double At(double x, double a, double b) => a + (b-a)*x;
            double entry = At(raw[0],2,9.65), apex = At(raw[1],1,13.15), exit = At(raw[2],2,9.65);
            double turnIn = At(raw[3], -15, 7), straighten = At(raw[4], -11, 14);
            double apexFraction = At(raw[5],.32,.68);
            switch (family)
            {
                case "early-apex": apexFraction = At(raw[5],.24,.43); turnIn -= 3; break;
                case "late-apex": apexFraction = At(raw[5],.56,.77); straighten += 4; break;
                case "wide-radius": entry = At(raw[0],7,9.6); apex = At(raw[1],8.5,12.9); exit = At(raw[2],7,9.6); break;
                case "full-width": apex = At(raw[1],9,13.25); if (raw[6] > .5) entry = 9.6; else exit = 9.6; break;
            }
            // Include useful near-constant offset families in the same deterministic pool.
            if (i < 17)
            {
                family = "constant-offset";
                entry = apex = exit = 2 + .45 * i;
                turnIn = -9;
                straighten = 9;
                apexFraction = .5;
            }
            double start = bend.Entry - 31;
            double[] positions = [start, bend.Entry+turnIn, bend.Entry+bend.Length*apexFraction,
                bend.Exit+straighten, start+length];
            double[] offsets = [StraightJoinMeters,entry,apex,exit,StraightJoinMeters];
            double Offset(double s)
            {
                if (s<=start || s>=start+length) return StraightJoinMeters;
                int k = Array.FindIndex(positions, p=>p>s)-1;
                double t = (s-positions[k])/(positions[k+1]-positions[k]);
                return offsets[k]+(offsets[k+1]-offsets[k])*Smooth(t);
            }
            // Demand a substantive lateral difference across the entire sector,
            // using fixed reference stations and RMS between descriptors.
            var sig = Enumerable.Range(1,22).Select(k=>Offset(start+length*k/23)).ToArray();
            if (signatures.Any(other=>sig.Zip(other,(a,b)=>(a-b)*(a-b)).Sum() < sig.Length*.26*.26))
            {
                similarRejected++;
                continue;
            }
            MeterPoint Pos(double s)
            {
                var r = frame.Shape!.At(s);
                return r.Position+new MeterPoint(Math.Sin(r.Heading),-Math.Cos(r.Heading))*Offset(s);
            }
            double rate = Enumerable.Range(1,4).Max(k=>1.875*Math.Abs(offsets[k]-offsets[k-1])/(positions[k]-positions[k-1]));
            // 1+k*d bounds tangent stretch and rate bounds lateral stretch.
            double movementBound = Step * Math.Sqrt(Math.Pow(1+MaxReferenceCurvature*offsets.Max(),2)+rate*rate) / 2;
            double sweep = double.PositiveInfinity, distance = 0;
            bool legal = true;
            var points = new LaneKnot190[SampleCount+1];
            for (int j = 0; j <= SampleCount; j++)
            {
                double s = start+j*Step;
                MeterPoint xy=Pos(s), left=Pos(s-.20), right=Pos(s+.20);
                var a=xy-left; var b=right-xy;
                double k = 2*MeterPoint.Cross(a,b)/(a.Length*b.Length*(a+b).Length);
                double offset = Offset(s);
                double gap = edges.Gap(xy)-SpeedwayBikeDimensions.Reference.BoundingRadiusMeters-movementBound;
                sweep = Math.Min(sweep,gap);
                if (!double.IsFinite(k) || gap < .5 || offset <= -1 || offset >= frame.Shape!.Width(s)-1)
                {
                    legal = false;
                    break;
                }
                if (j>0) distance += (xy-points[j-1].XY).Length;
                points[j] = new(offset,xy,Math.Atan2(right.Y-left.Y,right.X-left.X),k);
            }
            if (!legal)
            {
                boundaryRejected++;
                continue;
            }
            accepted.Add(new(new($"{index+1}-{i+1:D4}",family,entry,apex,exit,turnIn,apexFraction,straighten,sweep,distance),points));
            signatures.Add(sig);
        }
        return new(DrawCount,boundaryRejected,similarRejected,accepted.ToArray());
    }

    internal static double Smooth(double t)
    {
        double u = Math.Clamp(t,0,1);
        return u*u*u*(10+u*(-15+6*u));
    }

    private static double RadicalInverse(int i, int radix)
    {
        double inverse = 1d/radix, value = 0;
        while (i>0)
        {
            value += i%radix*inverse;
            i /= radix;
            inverse /= radix;
        }
        return value;
    }

    /// <summary>Conservative unsigned edge distance with cell culling. Membership
    /// is independently checked against the frozen cross-section at each knot.</summary>
    private sealed class EdgeIndex
    {
        private const double CellMeters = 4;
        private const double SearchMeters = 6;
        private const double ChordAllowanceMeters = .001; // native 5 cm edge discretization
        private readonly Dictionary<(int X,int Y),List<(MeterPoint Point,MeterPoint Delta)>> cells = new();

        internal EdgeIndex(TrackFrame frame)
        {
            int count = (int)Math.Ceiling(frame.LapMeters/.05);
            for (int i=0; i<count; i++)
            {
                var a = At(i);
                var b = At(i+1);
                Add(a.Inner,b.Inner);
                Add(a.Outer,b.Outer);
            }
            (MeterPoint Inner,MeterPoint Outer) At(int i)
            {
                double s = i*frame.LapMeters/count;
                var r = frame.Shape!.At(s);
                var n = new MeterPoint(Math.Sin(r.Heading),-Math.Cos(r.Heading));
                return (r.Position-n,r.Position+n*(frame.Shape.Width(s)-1));
            }
        }

        private static int Cell(double x) => (int)Math.Floor(x/CellMeters);

        private void Add(MeterPoint a, MeterPoint b)
        {
            for (int x=Cell(Math.Min(a.X,b.X)-SearchMeters); x<=Cell(Math.Max(a.X,b.X)+SearchMeters); x++)
            for (int y=Cell(Math.Min(a.Y,b.Y)-SearchMeters); y<=Cell(Math.Max(a.Y,b.Y)+SearchMeters); y++)
            {
                var key=(x,y);
                if (!cells.TryGetValue(key,out var list)) cells[key]=list=[];
                list.Add((a,b-a));
            }
        }

        internal double Gap(MeterPoint xy)
        {
            double distanceSquared = SearchMeters*SearchMeters;
            if (cells.TryGetValue((Cell(xy.X),Cell(xy.Y)),out var edgesInCell))
                foreach (var (a,d) in edgesInCell)
                {
                    var relative=xy-a;
                    double t=Math.Clamp(MeterPoint.Dot(relative,d)/MeterPoint.Dot(d,d),0,1);
                    var delta=relative-d*t;
                    distanceSquared=Math.Min(distanceSquared,MeterPoint.Dot(delta,delta));
                }
            return Math.Sqrt(distanceSquared)-ChordAllowanceMeters;
        }
    }
}

