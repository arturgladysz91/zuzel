using CoreSim.Analysis;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;
using CoreSim.Race;
using Xunit;

namespace CoreSim.Tests;

public sealed partial class PhysicalSpaceTests
{
    [Fact]
    public void PreparedSegmentMappingPreservesOriginalIEEEArithmetic()
    {
        foreach (var type in new[] {SegmentType.Straight, SegmentType.TurnEntry})
        foreach (var heading in new[] {-Math.PI, -.0, 0, .7, Math.PI, 2*Math.PI})
        foreach (var progressRate in new[] {0d, .125, 1d})
        foreach (var offsetRate in new[] {-.3, 0, .3})
        {
            var segment = new MetricTrackSegment(0, 1, type, new(3.1,-9.2), heading, 60,24,MathF.PI/3);
            var prepared = segment.Prepare(progressRate,offsetRate);
            foreach (var progress in new[] {0d, double.Epsilon, .125, .5, Math.BitDecrement(1d), 1d})
            foreach (var offset in new[] {0d, .1, 3.75, 12d})
            {
                var expected = OriginalMap(segment, progress, offset, progressRate,offsetRate);
                AssertSample(expected, segment.Map(progress,offset,progressRate,offsetRate));
                AssertSample(expected, prepared.Map(progress,offset));
            }
        }
    }

    [Fact]
    public void ReusedCapsuleVectorsPreserveOriginalIEEEAndComponentTies()
    {
        var random = new Random(60);
        var dimensions = new[] {SpeedwayBikeDimensions.Reference, new SpeedwayBikeDimensions(.3,.3,.8,0,.04)};
        for (var i=0; i<10000; i++)
        {
            var a = BikeFootprint.Create(new(i%4==0 ? -.0 : random.NextDouble()*12, random.NextDouble()*12),
                i%3==0 ? 0 : random.NextDouble()*Math.Tau, dimensions[i%2]);
            var b = i%7==0 ? a : BikeFootprint.Create(new(random.NextDouble()*12,random.NextDouble()*12),
                i%3==0 ? 0 : random.NextDouble()*Math.Tau, dimensions[(i/2)%2]);
            var expected = OriginalSeparation(a,b); var actual = MechanicalSeparation.Between(a,b);
            Bits(expected.SignedMeters,actual.SignedMeters);
            Assert.Equal(expected.ComponentA,actual.ComponentA); Assert.Equal(expected.ComponentB,actual.ComponentB);
        }
    }

    [Theory]
    [InlineData("K-far-apart")]
    [InlineData("H-three-squeeze")]
    [InlineData("G-four-first-bend")]
    [InlineData("imminent-overlap")]
    public void VerifiedHistoryPruningEqualsFreshFullObservation(string name)
    {
        var scenario = ContestedSpaceResponseEvidence.Scenarios().Single(s=>s.Name==name);
        var step = ContestedSpaceResponseEvidence.Resolve(scenario);
        var cold = new InteractionEpisodeTracker(); var reused = new InteractionEpisodeTracker();
        cold.Bind(step.Snapshot); reused.Bind(step.Snapshot);
        var poses = step.Motions.SelectMany(m=>ResolvedBikePoses.FromMotion(m,step.Snapshot.Track,embedding:reused.Embedding)).ToArray();
        var observation = CommonTimePoseHistory.Observe(reused.History.Concat(poses));
        cold.Retain(step); reused.RetainVerified(step,poses,observation);
        Assert.Equal(cold.History.Select(Identity),reused.History.Select(Identity));
        Assert.Equal(cold.PruneHistory(5),reused.PruneHistory(5));
        Assert.Equal(cold.History.Select(Identity),reused.History.Select(Identity));
        static object Identity(PhysicalPoseInterval p) => new {p.RiderId,p.FrameId,p.StartTimeSeconds,p.EndTimeSeconds,
            p.StartsAtDiscontinuity,p.Source,Start=p.Sample(p.StartTimeSeconds),End=p.Sample(p.EndTimeSeconds)};
    }

    private static void Bits(double a,double b) => Assert.Equal(BitConverter.DoubleToInt64Bits(a),BitConverter.DoubleToInt64Bits(b));
    private static void AssertSample(MetricTrackSample a, MetricTrackSample b)
    {
        Bits(a.Position.X,b.Position.X); Bits(a.Position.Y,b.Position.Y);
        Bits(a.VelocityMetersPerSecond.X,b.VelocityMetersPerSecond.X); Bits(a.VelocityMetersPerSecond.Y,b.VelocityMetersPerSecond.Y);
        Bits(a.TangentHeadingRadians,b.TangentHeadingRadians); Bits(a.TravelHeadingRadians,b.TravelHeadingRadians);
    }
    // Independent pre-optimization arithmetic: neither oracle calls a prepared helper.
    private static MetricTrackSample OriginalMap(MetricTrackSegment s,double progress,double offset,double dp,double dr)
    {
        var heading=s.StartTangentHeadingRadians+(s.SegmentType==SegmentType.Straight?0:s.TurnAngleRadians*progress);
        var tangent=new MeterPoint(Math.Cos(heading),Math.Sin(heading)); var outward=new MeterPoint(tangent.Y,-tangent.X);
        MeterPoint position,velocity;
        if(s.SegmentType==SegmentType.Straight)
        {position=s.StartReferencePosition+tangent*(s.StraightLengthMeters*progress)+outward*offset;velocity=tangent*(s.StraightLengthMeters*dp)+outward*dr;}
        else
        {var left=new MeterPoint(-Math.Sin(s.StartTangentHeadingRadians),Math.Cos(s.StartTangentHeadingRadians));
         var centre=s.StartReferencePosition+left*s.InnerRadiusMeters;position=centre+outward*(s.InnerRadiusMeters+offset);
         velocity=tangent*((s.InnerRadiusMeters+offset)*s.TurnAngleRadians*dp)+outward*dr;}
        return new(position,velocity,BikeAngles.Wrap(heading),velocity.Length==0?BikeAngles.Wrap(heading):Math.Atan2(velocity.Y,velocity.X));
    }
    private static FootprintSeparation OriginalSeparation(BikeFootprint a,BikeFootprint b)
    {
        var best=new FootprintSeparation(double.PositiveInfinity,BikeComponent.Chassis,BikeComponent.Chassis);
        for(var i=0;i<2;i++)for(var j=0;j<2;j++)
        {var ca=a.Component((BikeComponent)i);var cb=b.Component((BikeComponent)j);
         var gap=Distance(ca.Start,ca.End,cb.Start,cb.End)-ca.RadiusMeters-cb.RadiusMeters;
         if(gap<best.SignedMeters)best=new(gap,(BikeComponent)i,(BikeComponent)j);}
        return best;
        static double Distance(MeterPoint a,MeterPoint b,MeterPoint c,MeterPoint d)
        {var u=b-a;var v=d-c;var w=c-a;var cross=MeterPoint.Cross(u,v);
         if(cross!=0){var s=MeterPoint.Cross(w,v)/cross;var t=MeterPoint.Cross(w,u)/cross;if(s>=0&&s<=1&&t>=0&&t<=1)return 0;}
         return Math.Min(Math.Min(Point(a,c,d),Point(b,c,d)),Math.Min(Point(c,a,b),Point(d,a,b)));}
        static double Point(MeterPoint p,MeterPoint a,MeterPoint b)
        {var line=b-a;var square=MeterPoint.Dot(line,line);return(p-(a+line*(square==0?0:Math.Clamp(MeterPoint.Dot(p-a,line)/square,0,1)))).Length;}
    }
}
