using CoreSim.Analysis;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public sealed class ContactDecisionGeometryTests
{
    [Fact]
    public void EnabledTacticalDirectionsAndClassificationsUseTheContactKernelExactly()
    {
        var differsFromNative=false;
        foreach(var t in ContactDecisionGeometryFixtures.Tactics())
        {
            var heading=BikeAngles.Interpolate(t.A.ReferenceTangentHeadingRadians,t.B.ReferenceTangentHeadingRadians,.5);
            var forward=ContactFrameArithmetic.Direction(heading,true);var outward=new MeterPoint(forward.Y,-forward.X);
            differsFromNative |= BitConverter.DoubleToInt64Bits(forward.X)!=BitConverter.DoubleToInt64Bits(Math.Cos(heading))
                || BitConverter.DoubleToInt64Bits(forward.Y)!=BitConverter.DoubleToInt64Bits(Math.Sin(heading));
            var delta=t.B.Position-t.A.Position;
            var along=MeterPoint.Dot(delta,forward);
            var closing=MeterPoint.Dot(t.RateB.CenterVelocityMetersPerSecond-t.RateA.CenterVelocityMetersPerSecond,forward);
            var a=InteractionGeometryModel.Project(t.A.Footprint,forward);var b=InteractionGeometryModel.Project(t.B.Footprint,forward);
            var overlap=Math.Min(a.Max,b.Max)-Math.Max(a.Min,b.Min);
            var actual=InteractionGeometryModel.Describe(t.Space,t.A,t.B,t.RateA,t.RateB);
            Bits(along,actual.ForwardBFromAMeters);Bits(MeterPoint.Dot(delta,outward),actual.OutwardBFromAMeters);
            Bits(closing,actual.ForwardClosingMetersPerSecond);Bits(overlap,actual.ForwardFootprintOverlapMeters);
            var state=overlap>0 ? Math.Abs(along)<overlap*.5 ? OverlapState.SideBySide : OverlapState.PartialOverlap
                : along>0 ? closing<0 ? OverlapState.Approaching : OverlapState.Behind : OverlapState.Ahead;
            Assert.Equal(state,actual.AOverlap);
            foreach(var id in new[]{1,2})
            {
                var sign=id==1?1:-1;var ahead=along*sign;var outside=MeterPoint.Dot(delta,outward)*sign;var relative=closing*sign;
                var role=overlap>0&&outside<0 ? InteractionTacticalRole.Defender
                    : ahead>0&&relative<0 || overlap>0&&outside>0&&relative<=0 ? InteractionTacticalRole.Attacker
                    : ahead<0 ? InteractionTacticalRole.Defender : InteractionTacticalRole.Neutral;
                Assert.Equal(role,InteractionGeometryModel.Role(id,actual));
            }
        }
        Assert.True(differsFromNative,"The fixtures must distinguish deterministic from native arithmetic.");
    }

    [Fact]
    public void InnerAndOuterClearanceRejectsInvalidCandidatesAndCertifiesPositiveMargins()
    {
        foreach(var edge in ContactDecisionGeometryFixtures.Edges().Where(e=>e.Margin!=0))
            Assert.True((edge.Margin>0)==ContestedSpaceInteractionCoordinator.WithinTrack(new[]{edge.Interval},edge.Track,edge.Embedding),edge.Name);
        // At zero clearance there is no strictly positive certificate, even for a stationary bike.
        foreach(var edge in ContactDecisionGeometryFixtures.Edges().Where(e=>e.Margin==0 && e.Interval.Source!.SegmentIndex==0
            && e.Interval.Sample(0).Attitude.RelativeSlideAngleRadians==0))
            Assert.False(ContestedSpaceInteractionCoordinator.WithinTrack(new[]{edge.Interval},edge.Track,edge.Embedding));
    }

    [Fact]
    public void ZeroMarginCertificationUsesDeterministicAxesAndCornerCentreBits()
    {
        foreach(var edge in ContactDecisionGeometryFixtures.Edges().Where(e=>e.Margin==0))
        {
            var pose=edge.Interval.Sample(0);var source=pose.Source!;var segment=edge.Embedding.Segments[source.SegmentIndex];
            var forward=ContactFrameArithmetic.Direction(pose.ReferenceTangentHeadingRadians,true);
            var outward=new MeterPoint(forward.Y,-forward.X);
            var support=InteractionGeometryModel.Project(pose.Footprint,outward);var center=MeterPoint.Dot(pose.Position,outward);
            var entry=ContactFrameArithmetic.Direction(segment.StartTangentHeadingRadians,true);
            var cornerCentre=segment.StartReferencePosition+new MeterPoint(-entry.Y,entry.X)*segment.InnerRadiusMeters;
            var offset=source.SegmentType==SegmentType.Straight ? MeterPoint.Dot(pose.Position-segment.StartReferencePosition,outward)
                : (pose.Position-cornerCentre).Length-edge.Track.Geometry.InnerRadiusMeters;
            var bulge=source.SegmentType==SegmentType.Straight ? 0 : pose.Dimensions.BoundingRadiusMeters*pose.Dimensions.BoundingRadiusMeters
                /(2*Math.Max(1,edge.Track.Geometry.InnerRadiusMeters-pose.Dimensions.BoundingRadiusMeters));
            var clearance=Math.Min(offset+support.Min-center+TrackGeometry.InnerReferenceOffsetFromTrackEdgeMeters,
                LaneModel.UsableRacingWidthMeters(source.SegmentType,edge.Track.Geometry)
                    +TrackGeometry.ProvisionalOuterReferenceOffsetFromTrackEdgeMeters-offset-support.Max+center-bulge);
            // A stationary fixture is certified exactly iff this clearance is positive.
            // A tiny negative value must fail; a zero cannot pass the recursive certificate.
            Assert.True((clearance>0)==ContestedSpaceInteractionCoordinator.WithinTrack(new[]{edge.Interval},edge.Track,edge.Embedding),edge.Name);
        }
    }

    [Fact]
    public void TrimmedHistoryAndProductionPoseSamplesPreserveTheEmbeddingMode()
    {
        foreach(var grip in new[]{.55f,1f,1.25f})
        foreach(var weather in new[]{WeatherState.Dry,WeatherState.LightRain})
        {
            var scenario=ContestedSpaceResponseEvidence.Scenarios().Single(s=>s.Name=="G-four-first-bend") with{OuterGrip=grip,Seed=19};
            var snapshot=ContestedSpaceResponseEvidence.Snapshot(scenario);
            var engine=new SimulationEngine(new CoreSim.Decisions.AdaptiveDecisionModel());
            var step=engine.Resolve(snapshot,engine.Decide(snapshot),PhysicalContactConsequenceEvidence.Options with{Weather=weather});
            var embedding=new TrackMetricEmbedding(snapshot.Track,true);
            foreach(var interval in step.Motions.SelectMany(m=>ResolvedBikePoses.FromMotion(m,snapshot.Track,embedding:embedding)))
            {
                Assert.True(interval.DeterministicArithmetic);
                foreach(var time in new[]{interval.StartTimeSeconds,(interval.StartTimeSeconds+interval.EndTimeSeconds)/2,interval.EndTimeSeconds})
                {
                    var pose=interval.Sample(time);var value=interval.SampleValue(time);
                    Assert.True(pose.DeterministicArithmetic);Assert.True(value.DeterministicArithmetic);
                    Bits(pose.Position.X,value.Position.X);Bits(pose.Position.Y,value.Position.Y);
                    Bits(pose.Attitude.TravelHeadingRadians,value.TravelHeadingRadians);
                    Bits(pose.Attitude.BikeHeadingRadians,value.BikeHeadingRadians);
                }
            }
        }
        var a=ContactDecisionGeometryFixtures.Pose(1,default,.7);
        PhysicalBikePose At(double time)=>new(1,a.FrameId,a.Position,new(time,.7,.7),a.Dimensions){DeterministicArithmetic=true};
        var old=new LinearBikePoseInterval(At(0),At(MathF.BitIncrement(1f)));
        var next=new LinearBikePoseInterval(At(1),At(2));
        var trimmed=CommonTimePoseHistory.Stitch(new[]{old,next})[0];
        Assert.True(trimmed.EndTimeSeconds<old.EndTimeSeconds);
        Assert.True(trimmed.DeterministicArithmetic);Assert.True(trimmed.SampleValue(.5).DeterministicArithmetic);
    }

    [Fact]
    public void TacticalGeometryRejectsUnrelatedFramesAndArithmeticModes()
    {
        var a=ContactDecisionGeometryFixtures.Pose(1,default,.7);
        foreach(var b in new[]{ContactDecisionGeometryFixtures.Pose(2,new(1,0),.7,false),
            ContactDecisionGeometryFixtures.Pose(2,new(1,0),.7,true,"other-frame")})
            Assert.Throws<ArgumentException>(()=>InteractionGeometryModel.Describe(ContactDecisionGeometryFixtures.Space(a,b),a,b,default,default));
    }

    [Fact]
    public void ArithmeticModeCannotChangeAtAnIntervalEndOrBeforeTheFirstCommittedStep()
    {
        var a=ContactDecisionGeometryFixtures.Pose(1,default,.7);
        var b=new PhysicalBikePose(1,a.FrameId,a.Position,new(1,.7,.7),a.Dimensions){DeterministicArithmetic=false};
        Assert.Throws<ArgumentException>(()=>new LinearBikePoseInterval(a,b));
        var snapshot=ContestedSpaceResponseEvidence.Snapshot(ContestedSpaceResponseEvidence.Scenarios()[0]);
        var tracker=new InteractionEpisodeTracker();tracker.Bind(snapshot,true);
        Assert.Throws<InvalidOperationException>(()=>tracker.Bind(snapshot,false));
    }

    [Fact]
    public void PreparedGeometryKeepsTheOriginalModeSpecificArithmeticAndHeadingBits()
    {
        foreach(var enabled in new[]{false,true})
        foreach(var type in new[]{SegmentType.Straight,SegmentType.TurnEntry,SegmentType.TurnMiddle,SegmentType.TurnExit})
        foreach(var heading in new[]{-Math.PI,-.0,.7,2.9,Math.Tau})
        foreach(var dp in new[]{0d,.125,1d})
        foreach(var dr in new[]{-.3,0,.3})
        {
            var s=new MetricTrackSegment(0,1,type,new(3.1,-9.2),heading,60,24,MathF.PI/3){DeterministicArithmetic=enabled};
            var prepared=s.Prepare(dp,dr);
            foreach(var progress in new[]{0d,.125,.5,1d})
            foreach(var offset in new[]{0d,.1,12d})
            {
                var angle=heading+(type==SegmentType.Straight?0:s.TurnAngleRadians*progress);
                var tangent=ContactFrameArithmetic.Direction(angle,enabled);var outward=new MeterPoint(tangent.Y,-tangent.X);
                MeterPoint position,velocity;
                if(type==SegmentType.Straight)
                {position=s.StartReferencePosition+tangent*(s.StraightLengthMeters*progress)+outward*offset;
                 velocity=tangent*(s.StraightLengthMeters*dp)+outward*dr;}
                else
                {var entry=ContactFrameArithmetic.Direction(heading,enabled);
                 var left=enabled?new MeterPoint(-entry.Y,entry.X):new MeterPoint(-Math.Sin(heading),Math.Cos(heading));
                 var centre=s.StartReferencePosition+left*s.InnerRadiusMeters;
                 position=centre+outward*(s.InnerRadiusMeters+offset);
                 velocity=tangent*((s.InnerRadiusMeters+offset)*s.TurnAngleRadians*dp)+outward*dr;}
                var actual=prepared.Map(progress,offset);
                Bits(position.X,actual.Position.X);Bits(position.Y,actual.Position.Y);
                Bits(velocity.X,actual.VelocityMetersPerSecond.X);Bits(velocity.Y,actual.VelocityMetersPerSecond.Y);
                Bits(BikeAngles.Wrap(angle),actual.TangentHeadingRadians);
                Bits(velocity.Length==0?BikeAngles.Wrap(angle):ContactFrameArithmetic.Heading(velocity.Y,velocity.X,enabled),actual.TravelHeadingRadians);
            }
        }
    }
    private static void Bits(double expected,double actual)
        => Assert.Equal(BitConverter.DoubleToInt64Bits(expected),BitConverter.DoubleToInt64Bits(actual));
}
