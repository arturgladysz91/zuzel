using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal sealed class WideTrack194
{
    internal const double CorridorNearMeters = .80;
    internal const double CorridorFarMeters = 1.50;
    internal const double NominalWallMeters = 1.12;
    internal const double TangentToleranceRadians = .09;
    internal const double PrepareNextSeconds = .68;
    internal Track191 Track { get; }
    internal TrackFrame Frame => Track.Frame;
    internal BendMap Bends { get; }
    internal RacingLineGeometry Boundary { get; }
    private readonly double[] offsets;

    internal WideTrack194(Track191 track)
    {
        Track = track;
        Bends = new(track.Frame);
        Boundary = new(track.Frame);
        offsets = Enumerable.Range(0,Bends.Count).Select(FindStraightOffset).ToArray();
    }

    internal double WideOffset(BendVisit bend) => offsets[bend.Index];

    internal BendVisit Sector(double s, double referenceSpeed)
    {
        var where = Bends.Locate(s);
        if (where.Current is { } inBend) return inBend;
        var next = where.Next;
        double lead = Math.Clamp(PrepareNextSeconds*referenceSpeed,12,19);
        if (s >= next.Entry-lead || s < Bends.At(0,0).Entry) return next;
        int prior = (next.Index+Bends.Count-1)%Bends.Count;
        return Bends.At(next.Lap-(next.Index==0?1:0),prior);
    }

    internal RiderView Pose(double s, double offset, double angleError = 0)
    {
        var at = Frame.At(s,offset);
        return new(1,at.Position,default,(float)(at.Road.Heading+angleError),0,at.Road,s,0,RiderActivity.Racing,0,(float)offset,0);
    }

    private double FindStraightOffset(int corner)
    {
        var bend = Bends.At(0,corner);
        double s = (bend.Exit+bend.NextEntry)*.5;
        double left = 0, right = Frame.PhysicalWidthMeters(Frame.RoadAt(s,0))-1;
        // Signed distance of both capsules against the actual wall, with yaw tolerance.
        for (int i=0;i<22;i++)
        {
            double d = (left+right)*.5;
            double clearance = new[] { -TangentToleranceRadians,0,TangentToleranceRadians }
                .Min(angle => Boundary.Clearance(Pose(s,d,angle)).Outer);
            if (clearance<NominalWallMeters) right=d;
            else left=d;
        }
        return left;
    }
}

internal sealed class SectorPath194
{
    private readonly List<Curve191> legs = [];
    internal BendVisit Bend { get; }
    internal Intent191 Intent { get; }
    internal double Apex { get; }
    internal double Settle { get; }
    internal double Target { get; }
    internal double Preparation { get; }

    internal SectorPath194(WideTrack194 field, BendVisit bend, double s, Lateral191 initial, double referenceSpeed,
        double apexOffset, double terminalOffset, Intent191 intent, bool lateRecovery)
    {
        Bend = bend;
        Intent = intent;
        Target = terminalOffset;
        Apex = bend.Entry+.48*bend.Length;
        double normalSettle = bend.Exit+Math.Clamp(.27*referenceSpeed,5,7.5);
        Settle = normalSettle;
        Preparation = bend.NextEntry-Math.Clamp(WideTrack194.PrepareNextSeconds*referenceSpeed,12,19);
        // Preserve the endpoint of the normal exit. A blocked rider may return later
        // only if an entire gradual transition fits before the next preparation.
        if (lateRecovery)
            Settle = Math.Max(normalSettle,s+Math.Clamp(1.60*referenceSpeed,19,38));

        double origin = s;
        Lateral191 value = initial;
        void Join(double at, double offset)
        {
            if (at<=origin+1e-6) return;
            legs.Add(new(origin,value,offset,at-origin));
            origin=at;
            value=Lateral191.Rest(offset);
        }
        if (s < Apex-12) Join(Apex,apexOffset);
        if (s < Settle-.5)
        {
            // Short safety edits are damped over a travel-based interval, including
            // cases where a late warning comes after the normal settlement point.
            double targetAt = Math.Max(Settle,s+Math.Clamp(.72*referenceSpeed,8,20));
            if (s>=bend.Exit && Math.Abs(Target-initial.Offset)>.70)
                targetAt=Math.Max(targetAt,s+Math.Clamp(1.15*referenceSpeed,15,30));
            Join(targetAt,Target);
        }
        else
            Join(s+Math.Clamp(1.05*referenceSpeed,14,29),Target);

        Join(Preparation,Target);
        var next = field.Bends.At(bend.Lap+(bend.Index+1==field.Bends.Count?1:0),(bend.Index+1)%field.Bends.Count);
        Join(next.Entry+.48*next.Length,apexOffset);
        Join(next.Exit+Math.Clamp(.27*referenceSpeed,5,7.5),field.WideOffset(next));
    }

    internal Lateral191 At(double s)
    {
        foreach (var leg in legs)
            if (s <= leg.Origin+leg.Length) return leg.At(s);
        return legs[^1].At(s);
    }
}

