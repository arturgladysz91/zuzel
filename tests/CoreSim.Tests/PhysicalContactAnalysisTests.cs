using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;
using CoreSim.Race;
using System.Text.Json;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public sealed class PhysicalContactAnalysisTests
{
    private static readonly Lazy<IReadOnlyList<PhysicalContactFixture>> Cases = new(PhysicalContactEvidence.Fixtures);
    private static PhysicalContactFixture F(string name) => Cases.Value.Single(f => f.Name==name);
    private static PhysicalContactPairAnalysis Pair(string name) => F(name).Analysis.AuditPairs.Single(p => p.Status==PhysicalContactStatus.Analyzed);
    private static string Json(object value) => JsonSerializer.Serialize(value,PhysicalContactEvidence.JsonOptions);
    public static IEnumerable<object[]> Fixtures => PhysicalContactEvidence.Fixtures().Select(f => new object[]{f.Name});
    [Theory,MemberData(nameof(Fixtures))]
    public void PairAndObserverIntervalOrderAreExactlyInvariant(string name)
    {
        var f=F(name);
        Assert.Equal(Json(f.Analysis),Json(PhysicalContactAnalyzer.Analyze(f.Inputs.Reverse())));
        Assert.Equal(Json(f.Analysis),Json(PhysicalContactAnalyzer.Analyze(f.Inputs.Reverse().Select(Swap))));
    }
    [Fact]
    public void GentleBrushIsSmallAndOrdinaryContactAndCrossingIncreaseDemand()
    {
        var gentle=F("A-gentle-parallel").Analysis.Riders[0];
        Assert.Equal(PhysicalContactSeverity.Brush,gentle.ProvisionalSeverity);
        Assert.InRange(gentle.SeverityRatio,0,.1);
        Assert.True(F("B-moderate-side").Analysis.Riders[0].SeverityRatio>gentle.SeverityRatio);
        Assert.True(F("C-rear-closing").Analysis.Riders[0].SeverityRatio>gentle.SeverityRatio);
        Assert.True(F("D-crossing").Analysis.Riders.Max(r=>r.SeverityRatio)>1);
    }
    [Theory,InlineData(0),InlineData(.3),InlineData(1)]
    public void ImpulsesConserveMomentumAndNeverCreateKineticEnergy(double restitution)
    {
        foreach(var f in Cases.Value) foreach(var input in f.Inputs)
        {
            var pair=PhysicalContactAnalyzer.AnalyzePair(input,new(){Restitution=restitution});
            if(pair.Impulse is not {} i) continue;
            Assert.InRange((i.ImpulseOnANewtonSeconds+i.ImpulseOnBNewtonSeconds).Length,0,1e-10);
            Assert.InRange(Math.Abs(i.PreNormalMomentumKgMetersPerSecond-i.PostNormalMomentumKgMetersPerSecond),0,1e-9);
            Assert.True(i.PostNormalKineticEnergyJoules<=i.PreNormalKineticEnergyJoules+1e-7);
            var swapped=PhysicalContactAnalyzer.AnalyzePair(Swap(input),new(){Restitution=restitution});
            Assert.Equal(i.ImpulseMagnitudeNewtonSeconds,swapped.Impulse!.ImpulseMagnitudeNewtonSeconds);
            Assert.Equal(pair.DemandA!.PairDemand,swapped.DemandB!.PairDemand,10);
            Assert.Equal(i.ImpulseOnANewtonSeconds,swapped.Impulse.ImpulseOnBNewtonSeconds);
        }
    }
    [Fact]
    public void SharedTwentyMetersPerSecondIsNotIncomingImpactSpeed()
    {
        var a=PhysicalContactEvidence.Side("stationary-forward",.05,0).Analysis.AuditPairs.Single().Impulse!;
        var b=Pair("A-gentle-parallel").Impulse!;
        Assert.Equal(.05,b.NormalClosingSpeedMetersPerSecond,9);
        Assert.Equal(a.ImpulseMagnitudeNewtonSeconds,b.ImpulseMagnitudeNewtonSeconds,9);
    }
    [Fact]
    public void TravelFrameControlsForwardVersusLateralDemandAndNormalIsUnitLength()
    {
        var side=Pair("B-moderate-side"); var rear=Pair("C-rear-closing");
        Assert.True(Math.Abs(side.DemandA!.LateralDeltaVelocityMetersPerSecond)>Math.Abs(side.DemandA.ForwardDeltaVelocityMetersPerSecond));
        Assert.Equal(0,rear.DemandA!.LateralDeltaVelocityMetersPerSecond,9);
        Assert.Equal(1,side.Manifold!.NormalAtoB.Length,12);
        Assert.True(new PhysicalContactParameters().LateralDisturbanceWeight>new PhysicalContactParameters().ForwardDisturbanceWeight);
    }
    [Fact]
    public void ComponentDifferenceComesFromLeverGeometryWithoutAHandlebarMultiplier()
    {
        Assert.Equal(0,Pair("E-center").DemandA!.YawDemand,12);
        Assert.True(Pair("E-lever").DemandA!.YawDemand>0);
        var chassis=Pair("F-chassis"); var bar=Pair("F-handlebar");
        Assert.Equal(BikeComponent.Chassis,chassis.Manifold!.ComponentA);
        Assert.Equal(BikeComponent.Handlebar,bar.Manifold!.ComponentA);
        Assert.Equal(chassis.Impulse!.ImpulseMagnitudeNewtonSeconds,bar.Impulse!.ImpulseMagnitudeNewtonSeconds,9);
        Assert.True(bar.DemandA!.NormalizedYawLever>chassis.DemandA!.NormalizedYawLever);
    }
    [Theory,InlineData("G-technique-20","G-technique-50","G-technique-80"),InlineData("H-strength-20","H-strength-50","H-strength-80"),
        InlineData("I-condition-0.4","I-condition-0.7","I-condition-1"),InlineData("K-grip-0.35","K-grip-0.6","K-grip-0.85")]
    public void HigherControlCapacityRaisesReserveAndLowersRatioWithoutChangingImpulse(string low,string mid,string high)
    {
        var rows=new[]{low,mid,high}.Select(n=>F(n).Analysis.AuditRiders[0]).ToArray();
        Assert.True(rows[0].Reserve.StabilityReserveMetersPerSecond<rows[1].Reserve.StabilityReserveMetersPerSecond);
        Assert.True(rows[1].Reserve.StabilityReserveMetersPerSecond<rows[2].Reserve.StabilityReserveMetersPerSecond);
        Assert.True(rows[0].SeverityRatio>rows[1].SeverityRatio&&rows[1].SeverityRatio>rows[2].SeverityRatio);
        Assert.Equal(Json(Pair(low).Impulse!),Json(Pair(high).Impulse!));
    }
    [Fact]
    public void ConditionEffectIsWeakerThanTechniqueAndIrrelevantAbilitiesAreExactlyInert()
    {
        double Ratio(string low,string high)=>F(low).Analysis.Riders[0].SeverityRatio/F(high).Analysis.Riders[0].SeverityRatio;
        Assert.True(Ratio("I-condition-0.4","I-condition-1")<Ratio("G-technique-20","G-technique-80"));
        Assert.Equal(Json(F("B-moderate-side").Analysis),Json(F("L-irrelevant-abilities").Analysis));
    }
    [Fact]
    public void CanonicalMassPlusSeventySevenKgBikeGivesSmallerDeltaVToHeavierRider()
    {
        var input=F("B-moderate-side").Inputs.Single();
        var pair=PhysicalContactAnalyzer.AnalyzePair(input with {RiderA=PhysicalContactEvidence.Physiology(1,mass:60),RiderB=PhysicalContactEvidence.Physiology(2,mass:75)});
        Assert.Equal(137,pair.Impulse!.MassAKg); Assert.Equal(152,pair.Impulse.MassBKg);
        Assert.True(pair.Impulse.DeltaVAMetersPerSecond.Length>pair.Impulse.DeltaVBMetersPerSecond.Length);
        Assert.Equal(pair.Impulse.ImpulseOnANewtonSeconds.Length,pair.Impulse.ImpulseOnBNewtonSeconds.Length,12);
    }
    [Fact]
    public void SqueezeCancelsNetMovementButPreservesNonCancellingDemand()
    {
        var f=F("M-three-squeeze"); var b=f.Analysis.AuditRiders.Single(r=>r.RiderId==2);
        Assert.InRange(b.NetImpulseNewtonSeconds.Length,0,1e-9);
        Assert.True(b.CombinedStabilityDemand>f.Analysis.AuditPairs[0].DemandB!.PairDemand);
        Assert.Equal(2,f.Analysis.Work.AnalyzedPairs); Assert.Equal(1,f.Analysis.Work.ContactGroups);
        Assert.Equal(3,F("N-four-frontier").Analysis.Work.AnalyzedPairs);
    }
    [Fact]
    public void DisconnectedGroupsHaveIndependentFrontiersAndLaterContactsAreExplicitlyDeferred()
    {
        Assert.Equal(2,F("O-disjoint-groups").Analysis.Work.ContactGroups);
        var f=F("P-later-deferred"); Assert.Equal(1,f.Analysis.Work.DeferredContacts);
        Assert.Contains(f.Analysis.AuditPairs,p=>p.DeferredByEarlierContact&&p.Impulse is null);
        Assert.Equal(2,PhysicalContactAnalyzer.Analyze(f.Inputs,new(){ContactSimultaneityWindowSeconds=.12}).Work.AnalyzedPairs);
    }
    [Theory]
    [InlineData("frontier-F1-late-bridge",2,2,1,4)]
    [InlineData("frontier-F2-simultaneous-chain",1,3,0,4)]
    [InlineData("frontier-F3-start-anchored-window",1,2,1,3)]
    [InlineData("frontier-F4-simultaneous-disconnected",2,2,0,4)]
    [InlineData("frontier-F5-distant-untouched",2,2,0,4)]
    [InlineData("frontier-F6-later-mixed",1,1,1,2)]
    [InlineData("frontier-F7-two-frontiers-then-bridge",2,2,1,4)]
    public void CausalFrontiersNeverUseFutureBridgesOrChainedWindows(string name,int groups,int analyzed,int deferred,int riders)
    {
        var fixture=F(name);var result=fixture.Analysis;
        Assert.Equal(groups,result.Work.ContactGroups);Assert.Equal(analyzed,result.Work.AnalyzedPairs);
        Assert.Equal(deferred,result.Work.DeferredContacts);Assert.Equal(riders,result.AuditRiders.Count);
        foreach(var input in fixture.Inputs)
        {
            var pair=result.AuditPairs.Single(p=>p.RiderA==input.Contact.RiderA&&p.RiderB==input.Contact.RiderB);
            var shouldDefer=deferred>0&&input==fixture.Inputs[^1];
            Assert.Equal(shouldDefer?PhysicalContactStatus.DeferredByEarlierContact:PhysicalContactStatus.Analyzed,pair.Status);
            if(shouldDefer){Assert.Null(pair.Manifold);Assert.Null(pair.Impulse);Assert.Null(pair.DemandA);Assert.Null(pair.DemandB);}
            else Assert.Equal(Json(PhysicalContactAnalyzer.AnalyzePair(input)),Json(pair));
        }
        // F8: input order, A/B order and ID order may only change serialization identifiers.
        Assert.Equal(Json(result),Json(PhysicalContactAnalyzer.Analyze(fixture.Inputs.Reverse())));
        Assert.Equal(Json(result),Json(PhysicalContactAnalyzer.Analyze(fixture.Inputs.Reverse().Select(Swap))));
        int Id(int id)=>id switch{1=>90,2=>30,3=>70,_=>10};
        AssertRenamedAnalysis(result,PhysicalContactAnalyzer.Analyze(fixture.Inputs.Reverse().Select(i=>Rename(i,Id))),Id);
    }
    [Fact]
    public void FixedPointFindsAnEarlierDisconnectedEdgeAfterItsWithinWindowBridge()
    {
        var inputs=new[]{PhysicalContactEvidence.TimedContact(1,2,0),PhysicalContactEvidence.TimedContact(3,4,.01),
            PhysicalContactEvidence.TimedContact(2,3,.035)};
        var result=PhysicalContactAnalyzer.Analyze(inputs);
        Assert.Equal(1,result.Work.ContactGroups);Assert.Equal(3,result.Work.AnalyzedPairs);Assert.Equal(4,result.Riders.Count);
    }
    [Theory,InlineData(0),InlineData(.5),InlineData(1),InlineData(2)]
    public void FrontierWindowUsesExistingTimeToleranceWithoutIdOrEnumerationPriority(double toleranceMultiple)
    {
        var inputs=new[]{PhysicalContactEvidence.TimedContact(1,2,0),
            PhysicalContactEvidence.TimedContact(3,4,GeometryNumerics.TimeToleranceSeconds*.5),
            PhysicalContactEvidence.TimedContact(2,3,.04+GeometryNumerics.TimeToleranceSeconds*toleranceMultiple)};
        var result=PhysicalContactAnalyzer.Analyze(inputs);
        Assert.Equal(toleranceMultiple<=1?1:2,result.Work.ContactGroups);
        Assert.Equal(toleranceMultiple<=1?3:2,result.Work.AnalyzedPairs);
        Assert.Equal(toleranceMultiple<=1?0:1,result.Work.DeferredContacts);
        Assert.Equal(Json(result),Json(PhysicalContactAnalyzer.Analyze(inputs.Reverse().Select(Swap))));
        int Id(int id)=>5-id;
        AssertRenamedAnalysis(result,PhysicalContactAnalyzer.Analyze(inputs.Select(i=>Rename(i,Id))),Id);
    }
    [Fact]
    public void AnIneligibleBridgeCannotJoinFrontiersOrInvalidateUntouchedRiders()
    {
        var bridge=PhysicalContactEvidence.TimedContact(2,3,.01);
        var result=PhysicalContactAnalyzer.Analyze(new[]{PhysicalContactEvidence.TimedContact(1,2,0),
            bridge with{Contact=bridge.Contact with{NumericallyResolved=false}},PhysicalContactEvidence.TimedContact(3,4,.02)});
        Assert.Equal(2,result.Work.ContactGroups);Assert.Equal(2,result.Work.AnalyzedPairs);Assert.Equal(0,result.Work.DeferredContacts);
        Assert.Equal(1,result.Work.RejectedContacts);
    }
    [Fact]
    public void UnresolvedVerifiedContactStillInvalidatesItsLaterFrozenTrajectory()
    {
        var first=PhysicalContactEvidence.TimedContact(1,2,0);
        var result=PhysicalContactAnalyzer.Analyze(new[]{first with{PoseA=null},PhysicalContactEvidence.TimedContact(2,3,.2)});
        Assert.Equal(PhysicalContactStatus.GeometryUnresolved,result.AuditPairs[0].Status);
        Assert.Equal(PhysicalContactStatus.DeferredByEarlierContact,result.AuditPairs[1].Status);
        Assert.Empty(result.Riders);Assert.Equal(1,result.Work.ContactGroups);
    }
    [Fact]
    public void OnsetPositionOrientsDegenerateTouchAndPreservesPairSwapSymmetry()
    {
        var input=OnsetFallbackContact();
        Assert.True(input.Contact.EligibleForFutureInteraction);
        Assert.True(input.Contact.RelativePositionAtOnsetMeters.Length>PhysicalContactGeometry.DirectionToleranceMeters);
        var a=PhysicalContactAnalyzer.AnalyzePair(input);var b=PhysicalContactAnalyzer.AnalyzePair(Swap(input));
        Assert.Equal(ContactNormalSource.RelativePositionAtOnset,a.Manifold!.NormalSource);
        Assert.Equal(1,a.Manifold.NormalAtoB.Length,12);Assert.Equal(a.Manifold.NormalAtoB*-1,b.Manifold!.NormalAtoB);
        Assert.True(a.Impulse!.NormalClosingSpeedMetersPerSecond>0);Assert.True(a.Impulse.ImpulseMagnitudeNewtonSeconds>0);
        Assert.Equal(a.Impulse.ImpulseMagnitudeNewtonSeconds,b.Impulse!.ImpulseMagnitudeNewtonSeconds);
        Assert.Equal(a.DemandA!.PairDemand,b.DemandB!.PairDemand);Assert.Equal(a.DemandB!.PairDemand,b.DemandA!.PairDemand);
        Assert.Equal(a.Manifold.ContactPointA,b.Manifold.ContactPointB);Assert.Equal(a.Manifold.ContactPointB,b.Manifold.ContactPointA);
        Assert.Equal(Json(PhysicalContactAnalyzer.Analyze(new[]{input})),Json(PhysicalContactAnalyzer.Analyze(new[]{Swap(input)})));
    }
    [Theory,InlineData(-1,0),InlineData(1,0),InlineData(-1,.5),InlineData(1,1)]
    public void NoPhysicalOrientationFailsClosedDespiteRelativeMotion(double relativeLateral,double onsetToleranceMultiple)
    {
        var f=PhysicalContactEvidence.FromPoses("orientation-degenerate",new[]{
            PhysicalContactEvidence.Linear(1,new(0,0),new(20,-relativeLateral*.5)),
            PhysicalContactEvidence.Linear(2,new(0,0),new(20,relativeLateral*.5))});
        var input=f.Inputs.Single();
        input=input with{Contact=input.Contact with{RelativePositionAtOnsetMeters=new(0,
            PhysicalContactGeometry.DirectionToleranceMeters*onsetToleranceMultiple)}};
        foreach(var row in new[]{input,Swap(input)})
        {
            var result=PhysicalContactAnalyzer.Analyze(new[]{row});var pair=result.AuditPairs.Single();
            Assert.Equal(PhysicalContactStatus.GeometryUnresolved,pair.Status);
            Assert.Null(pair.Manifold);Assert.Null(pair.Impulse);Assert.Null(pair.DemandA);Assert.Null(pair.DemandB);
            Assert.Null(result.Pairs.Single().SeverityRatioA);Assert.Null(result.Pairs.Single().SeverityA);Assert.Empty(result.Riders);
        }
    }
    [Theory,InlineData(ContactNormalSource.ClosestComponentAxes),InlineData(ContactNormalSource.BikeCenters),
        InlineData(ContactNormalSource.RelativePositionAtOnset)]
    public void EveryProducedNormalSourcePreservesScalarsAndReversesVectors(ContactNormalSource source)
    {
        var input=source switch
        {
            ContactNormalSource.RelativePositionAtOnset=>OnsetFallbackContact(),
            ContactNormalSource.BikeCenters=>PhysicalContactEvidence.FromPoses("intersecting-axes",new[]{
                PhysicalContactEvidence.Linear(1,new(0,0),new(22,0)),PhysicalContactEvidence.Linear(2,new(.5,0),new(20,0))}).Inputs.Single(),
            _=>F("B-moderate-side").Inputs.Single()
        };
        var a=PhysicalContactAnalyzer.AnalyzePair(input);var b=PhysicalContactAnalyzer.AnalyzePair(Swap(input));
        Assert.Equal(source,a.Manifold!.NormalSource);Assert.Equal(source,b.Manifold!.NormalSource);
        Assert.Equal(a.Manifold.NormalAtoB*-1,b.Manifold.NormalAtoB);
        Assert.Equal(a.Impulse!.ImpulseMagnitudeNewtonSeconds,b.Impulse!.ImpulseMagnitudeNewtonSeconds);
        Assert.Equal(a.Impulse.NormalClosingSpeedMetersPerSecond,b.Impulse.NormalClosingSpeedMetersPerSecond);
        Assert.Equal(a.Impulse.ImpulseOnANewtonSeconds,b.Impulse.ImpulseOnBNewtonSeconds);
        Assert.Equal(a.DemandA!.PairDemand,b.DemandB!.PairDemand);Assert.Equal(a.DemandB!.PairDemand,b.DemandA!.PairDemand);
    }
    private static PhysicalContactInput OnsetFallbackContact()
    {
        // #55 certifies an initially overlapping interval with a nonzero B-A onset position.
        // Within its accepted time uncertainty, both supplied sampled centers/axes coincide.
        var time=GeometryNumerics.TimeToleranceSeconds*.5;
        var poses=new[]{PhysicalContactEvidence.Linear(1,new(0,0),new(20,.5)),
            PhysicalContactEvidence.Linear(2,new(0,time),new(20,-.5))};
        var input=PhysicalContactEvidence.FromPoses("onset-fallback",poses).Inputs.Single();
        return input with{PoseA=poses[0].Sample(time),PoseB=poses[1].Sample(time)};
    }
    private static PhysicalContactInput Rename(PhysicalContactInput input,Func<int,int> id)
    {
        PhysicalBikePose? Pose(PhysicalBikePose? p)=>p is null?null:new(id(p.RiderId),p.FrameId,p.Position,p.Attitude,p.Dimensions,
            p.ReferenceTangentHeadingRadians,p.Source);
        return input with{Contact=input.Contact with{RiderA=id(input.Contact.RiderA),RiderB=id(input.Contact.RiderB)},
            PoseA=Pose(input.PoseA),PoseB=Pose(input.PoseB),RiderA=input.RiderA with{RiderId=id(input.RiderA.RiderId)},
            RiderB=input.RiderB with{RiderId=id(input.RiderB.RiderId)}};
    }
    private static void AssertRenamedAnalysis(PhysicalContactAnalysis original,PhysicalContactAnalysis renamed,Func<int,int> id)
    {
        Assert.Equal(original.Work,renamed.Work);
        foreach(var pair in original.AuditPairs)
        {
            var other=renamed.AuditPairs.Single(p=>p.RiderA==Math.Min(id(pair.RiderA),id(pair.RiderB))
                &&p.RiderB==Math.Max(id(pair.RiderA),id(pair.RiderB)));
            Assert.Equal(pair.Status,other.Status);Assert.Equal(pair.FirstTouchCommonTimeSeconds,other.FirstTouchCommonTimeSeconds);
            Assert.Equal(pair.Impulse?.ImpulseMagnitudeNewtonSeconds,other.Impulse?.ImpulseMagnitudeNewtonSeconds);
            Assert.Equal(pair.DemandA?.PairDemand,id(pair.RiderA)<id(pair.RiderB)?other.DemandA?.PairDemand:other.DemandB?.PairDemand);
        }
        foreach(var rider in original.AuditRiders)
        {
            var other=renamed.AuditRiders.Single(r=>r.RiderId==id(rider.RiderId));
            Assert.Equal(rider.CombinedStabilityDemand,other.CombinedStabilityDemand);Assert.Equal(rider.SeverityRatio,other.SeverityRatio);
            Assert.Equal(rider.NetImpulseNewtonSeconds,other.NetImpulseNewtonSeconds);
        }
    }
    [Fact]
    public void LaterDisconnectedContactStartsItsOwnFrontierAndInvalidEarlierDuplicateCannotHideVerifiedContact()
    {
        var f=F("O-disjoint-groups"); var first=f.Inputs[0]; var second=f.Inputs[1];
        PhysicalBikePose Move(PhysicalBikePose pose)=>pose.Repose(pose.Position,pose.Attitude.BikeHeadingRadians,pose.CommonTimeSeconds+1);
        second=second with{Contact=second.Contact with{IntervalStartSeconds=1,IntervalEndSeconds=1.15,FirstTouchCommonTimeSeconds=1,
            MinimumSeparationCommonTimeSeconds=second.Contact.MinimumSeparationCommonTimeSeconds+1},PoseA=Move(second.PoseA!),PoseB=Move(second.PoseB!)};
        var result=PhysicalContactAnalyzer.Analyze(new[]{first,second}); Assert.Equal(2,result.Work.AnalyzedPairs); Assert.Equal(0,result.Work.DeferredContacts);
        var invalid=first with{Contact=first.Contact with{NumericallyResolved=false}};
        Assert.Equal(Json(PhysicalContactAnalyzer.Analyze(new[]{first}).Riders),Json(PhysicalContactAnalyzer.Analyze(new[]{invalid,first}).Riders));
    }
    [Theory,InlineData("side"),InlineData("rear")]
    public void FirstTouchManifoldMatchesUnchangedCapsulesAndReversedPoseCollections(string geometry)
    {
        var poses=geometry=="side"?new[]{PhysicalContactEvidence.Linear(1,new(0,0),new(20,.5)),PhysicalContactEvidence.Linear(2,new(0,.8),new(20,-.5))}
            :new[]{PhysicalContactEvidence.Linear(1,new(0,0),new(22,0)),PhysicalContactEvidence.Linear(2,new(2.1,0),new(20,0))};
        var f=PhysicalContactEvidence.FromPoses("order",poses); var reversed=PhysicalContactEvidence.FromPoses("order",poses.Reverse());
        Assert.Equal(Json(f.Analysis),Json(reversed.Analysis)); var input=f.Inputs.Single(); var m=f.Analysis.AuditPairs.Single().Manifold!;
        Assert.Equal(MechanicalSeparation.Between(input.PoseA!,input.PoseB!).SignedMeters,m.SignedSeparationMeters);
        Assert.InRange((m.ContactPointA-m.ContactPointB).Length,0,GeometryNumerics.MinimumSeparationToleranceMeters);
    }
    [Fact]
    public void EachIrrelevantAbilityAndPreferenceIndividuallyLeavesFrozenAnalysisIdentical()
    {
        var input=F("B-moderate-side").Inputs.Single(); var expected=Json(PhysicalContactAnalyzer.Analyze(new[]{input}));
        for(var index=0;index<10;index++)
        {
            var values=Enumerable.Repeat(50,8).ToArray();if(index<8&&index is not 2 and not 7) values[index]=99;
            var profile=new RiderGameplayProfile(new(values[0],values[1],values[2],values[3],values[4],values[5],values[6],values[7]),
                input.RiderA.Profile.Physical,new(index==8?1:.5f,index==9?PreferredLine.Inside:PreferredLine.Neutral,index==8?0:.5f));
            Assert.Equal(expected,Json(PhysicalContactAnalyzer.Analyze(new[]{input with{RiderA=input.RiderA with{Profile=profile}}})));
        }
    }
    [Fact]
    public void FrozenSurfaceLookupAcceptsValidReferenceEdgesAndRejectsMateriallyOutsidePositions()
    {
        var scenario=ContestedSpaceResponseEvidence.OwnershipScenarios()[0] with{OuterGrip=.4f};
        var snapshot=ContestedSpaceResponseEvidence.Snapshot(scenario);var embedding=new TrackMetricEmbedding(snapshot.Track);
        PhysicalBikePose Pose(MetricTrackSegment segment,double progress,double offset)
        {
            var sample=segment.Map(progress,offset);
            return new(1,embedding.FrameId,sample.Position,new(0,sample.TravelHeadingRadians,sample.TravelHeadingRadians),
                SpeedwayBikeDimensions.Reference,sample.TangentHeadingRadians,new(0,segment.SegmentIndex,segment.SegmentId,segment.SegmentType,null));
        }
        foreach(var segment in embedding.Segments) foreach(var progress in new[]{0d,.1,.3,.5,.7,.9,1}) foreach(var lane in new[]{0,2,4})
        {
            var width=LaneModel.UsableRacingWidthMeters(segment.SegmentType,snapshot.Track.Geometry);
            var pose=Pose(segment,progress,width*lane/4d);
            Assert.Equal((double)snapshot.TrackState.GetSurface(segment.SegmentIndex,lane).EffectiveGrip,
                PhysicalContactSnapshotAdapter.Grip(pose,snapshot,embedding),6);
        }
        var outside=Pose(embedding.Segments[0],.5,100);
        Assert.Throws<ArgumentOutOfRangeException>(()=>PhysicalContactSnapshotAdapter.Grip(outside,snapshot,embedding));
    }
    [Theory,InlineData(-.1),InlineData(1.1),InlineData(double.NaN)]
    public void InvalidPhysicalParametersFailExplicitly(double restitution)
        => Assert.Throws<ArgumentOutOfRangeException>(()=>PhysicalContactAnalyzer.Analyze(F("B-moderate-side").Inputs,new(){Restitution=restitution}));
    [Fact]
    public void AmbiguousFramesMissingCoverageAndUnorientableCoincidenceProduceNoInventedSeverity()
    {
        Assert.Empty(F("Q-boundary-ambiguous").Analysis.Riders);
        Assert.Empty(F("R-coverage-gap").Analysis.Riders); Assert.NotEmpty(F("R-coverage-gap").Verification.FrameCoverageGaps);
        var input=F("B-moderate-side").Inputs.Single();
        var missing=PhysicalContactAnalyzer.Analyze(new[]{input with{PoseA=null}});
        Assert.Equal(PhysicalContactStatus.GeometryUnresolved,missing.Pairs[0].Status); Assert.Empty(missing.Riders);
        var same=PhysicalContactEvidence.FromPoses("coincident",new[]{PhysicalContactEvidence.Linear(1,default,new(20,0)),PhysicalContactEvidence.Linear(2,default,new(20,0))});
        Assert.Empty(same.Analysis.Riders); Assert.All(same.Analysis.Pairs,p=>Assert.Equal(PhysicalContactStatus.GeometryUnresolved,p.Status));
    }
    [Fact]
    public void IntersectingAxisFallbackPointsLieOnActualCapsuleSurfaces()
    {
        var f=PhysicalContactEvidence.FromPoses("initial-longitudinal-overlap",new[]{PhysicalContactEvidence.Linear(1,default,new(22,0)),
            PhysicalContactEvidence.Linear(2,new(1,0),new(20,0))});
        var pair=f.Analysis.AuditPairs.Single();Assert.Equal(ContactNormalSource.BikeCenters,pair.Manifold!.NormalSource);
        var input=f.Inputs.Single();
        void Surface(PhysicalBikePose pose,BikeComponent component,MeterPoint point)
        {
            var capsule=pose.Footprint.Component(component);var axis=capsule.End-capsule.Start;
            var t=Math.Clamp(MeterPoint.Dot(point-capsule.Start,axis)/MeterPoint.Dot(axis,axis),0,1);
            Assert.Equal(capsule.RadiusMeters,(point-(capsule.Start+axis*t)).Length,10);
        }
        Surface(input.PoseA!,pair.Manifold.ComponentA,pair.Manifold.ContactPointA);Surface(input.PoseB!,pair.Manifold.ComponentB,pair.Manifold.ContactPointB);
    }
    [Fact]
    public void SimultaneousMixedComponentTiesUseGeometryRatherThanRiderIdentity()
    {
        var f=PhysicalContactEvidence.FromPoses("mixed-component-tie",new[]{PhysicalContactEvidence.Linear(1,default,new(20,.5)),
            PhysicalContactEvidence.Linear(2,new(.1,.2),new(20,-.5))});
        var input=f.Inputs.Single();var a=PhysicalContactAnalyzer.AnalyzePair(input);var b=PhysicalContactAnalyzer.AnalyzePair(Swap(input));
        Assert.Equal(a.Manifold!.ComponentA,b.Manifold!.ComponentB);Assert.Equal(a.Manifold.ComponentB,b.Manifold.ComponentA);
        Assert.Equal(a.DemandA!.PairDemand,b.DemandB!.PairDemand);Assert.Equal(a.DemandB!.PairDemand,b.DemandA!.PairDemand);
        Assert.Equal(a.Manifold.ContactPointA,b.Manifold.ContactPointB);Assert.Equal(a.Manifold.ContactPointB,b.Manifold.ContactPointA);
    }
    [Fact]
    public void RotationUsesOnlyPositiveRotationalAttributionAndHasNoInventedZeroTimeOnset()
    {
        var pair=Pair("rotation-onset"); var input=F("rotation-onset").Inputs.First(i=>i.Contact.EligibleForFutureInteraction);
        Assert.True(pair.Impulse!.RotationOnsetAvailable); Assert.True(pair.Impulse.RotationalClosureEquivalentMetersPerSecond>0);
        var changed=PhysicalContactAnalyzer.AnalyzePair(input with {Contact=input.Contact with {Contributions=input.Contact.Contributions with
            {ATranslationMeters=1e6,BTranslationMeters=1e6,ActualClosingMeters=1e6}}});
        Assert.Equal(pair.Impulse.RotationalClosureEquivalentMetersPerSecond,changed.Impulse!.RotationalClosureEquivalentMetersPerSecond);
        Assert.False(Pair("B-moderate-side").Impulse!.RotationOnsetAvailable);
        Assert.Equal(0,Pair("B-moderate-side").Impulse!.RotationalClosureEquivalentMetersPerSecond);
    }
    [Fact]
    public void SummaryDropsManifoldsAndNoneDoesNotEnumerateContacts()
    {
        var full=F("M-three-squeeze").Analysis;
        var summary=PhysicalContactAnalyzer.Analyze(F("M-three-squeeze").Inputs,level:PhysicalContactDiagnosticsLevel.Summary);
        Assert.Equal(Json(full.Pairs),Json(summary.Pairs)); Assert.Equal(Json(full.Riders),Json(summary.Riders));
        Assert.Empty(summary.AuditPairs); Assert.Empty(summary.AuditRiders);
        Assert.Empty(PhysicalContactAnalyzer.Analyze(Enumerable.Range(0,1).Select<int,PhysicalContactInput>(_=>throw new InvalidOperationException()),
            level:PhysicalContactDiagnosticsLevel.None).Pairs);
    }
    [Fact]
    public void RenamingRidersChangesOnlyIdentifiersAndDuplicateRowsAreNotDoubleCounted()
    {
        var original=F("M-three-squeeze");
        // These valid binary-exact masses expose the one-ULP asymmetry in mA*(mB/(mA+mB)).
        var unequal=original.Inputs.Select(i=>i with{RiderA=i.RiderA with{Profile=PhysicalContactEvidence.Physiology(i.RiderA.RiderId,mass:60+.125f*(i.RiderA.RiderId-1)).Profile},
            RiderB=i.RiderB with{Profile=PhysicalContactEvidence.Physiology(i.RiderB.RiderId,mass:60+.125f*(i.RiderB.RiderId-1)).Profile}}).ToArray();
        var f=original with{Inputs=unequal,Analysis=PhysicalContactAnalyzer.Analyze(unequal)};int Id(int id)=>id switch{1=>90,2=>20,_=>10};
        PhysicalBikePose? Pose(PhysicalBikePose? p)=>p is null?null:new(Id(p.RiderId),p.FrameId,p.Position,p.Attitude,p.Dimensions,p.ReferenceTangentHeadingRadians,p.Source);
        var renamed=f.Inputs.Select(i=>i with {Contact=i.Contact with{RiderA=Id(i.Contact.RiderA),RiderB=Id(i.Contact.RiderB)},
            PoseA=Pose(i.PoseA),PoseB=Pose(i.PoseB),RiderA=i.RiderA with{RiderId=Id(i.RiderA.RiderId)},RiderB=i.RiderB with{RiderId=Id(i.RiderB.RiderId)}});
        var result=PhysicalContactAnalyzer.Analyze(renamed);
        foreach(var r in f.Analysis.AuditRiders){var other=result.AuditRiders.Single(o=>o.RiderId==Id(r.RiderId));
            Assert.Equal(r.CombinedStabilityDemand,other.CombinedStabilityDemand); Assert.Equal(r.SeverityRatio,other.SeverityRatio);
            Assert.Equal(r.NetImpulseNewtonSeconds,other.NetImpulseNewtonSeconds);}
        var duplicate=PhysicalContactAnalyzer.Analyze(f.Inputs.Concat(f.Inputs));
        Assert.Equal(Json(f.Analysis.Riders),Json(duplicate.Riders)); Assert.Equal(2,duplicate.Work.AnalyzedPairs);
    }
    [Fact]
    public void LargeGridHasNoPhysicalWarningsAndSeverityGrowsWithClosingSpeed()
    {
        var grid=PhysicalContactEvidence.Grid(); Assert.Equal(4374,grid.Count);
        Assert.Empty(PhysicalContactEvidence.Warnings(Cases.Value,grid));
        foreach(var g in grid.GroupBy(r=>(r.Geometry,r.Technique,r.Strength,r.Condition,r.RiderMassKg,r.Grip)))
        {var sorted=g.OrderBy(r=>r.ClosingSpeed).ToArray(); for(var i=1;i<sorted.Length;i++) Assert.True(sorted[i].SeverityRatio>=sorted[i-1].SeverityRatio);}
    }
    [Theory,InlineData("two-disjoint-unresolved"),InlineData("real-bridge"),InlineData("one-disjoint-clears")]
    public void EnablingShadowDiagnosticsPreservesAllProductionValuesAndFallbackOwnership(string name)
    {
        var s=ContestedSpaceResponseEvidence.OwnershipScenarios().Single(s=>s.Name==name);
        string Capture(ResolvedSimulationStep step)=>Json(new{step.Changes,step.Events,step.Diagnostics,step.Motions,step.Interaction});
        var none=PhysicalContactEvidence.Resolve(s,PhysicalContactDiagnosticsLevel.None);
        var full=PhysicalContactEvidence.Resolve(s,PhysicalContactDiagnosticsLevel.FullAudit);
        Assert.Equal(Capture(none),Capture(full)); Assert.Null(none.Interaction!.PhysicalContactAnalysis);
        Assert.Equal(Capture(full),Capture(PhysicalContactEvidence.Resolve(s,PhysicalContactDiagnosticsLevel.Summary,reverse:true)));
        foreach(var pair in full.Interaction!.PhysicalContactAnalysis!.AuditPairs)
        {Assert.Contains(full.Interaction.Episodes,e=>e.EpisodeId==pair.EpisodeId&&e.UnresolvedMechanicalContacts.Any(c=>
            c.RiderA==pair.RiderA&&c.RiderB==pair.RiderB&&c.LegacyFallbackAuthorized==pair.LegacyFallbackAuthorized));}
    }
    [Theory,InlineData("I",7),InlineData("G",19)]
    public void FullHeatShadowLevelsPreserveClassificationEveryStepAndRawSurface(string scenarioId,int seed)
    {
        string Run(PhysicalContactDiagnosticsLevel level)
        {
            var s=FourRiderBehaviorSuite.CreateScenarios().Single(s=>s.Id==scenarioId); var surface=s.CreateSurface();
            var riders=s.Riders.Select(r=>r.Create(s.Track)).ToList(); var observer=new CaptureObserver();
            var result=new HeatSimulator(new AdaptiveDecisionModel()).SimulateHeat(s.Track,surface,riders,
                new(){Laps=1,Seed=seed,EnableContestedSpaceResponses=true,PhysicalContactDiagnostics=level},58,observer);
            var snapshot=surface.Snapshot();
            return Json(new{result.Classification,observer.Rows,Riders=riders,
                Surface=Enumerable.Range(0,s.Track.Segments.Count).SelectMany(segment=>Enumerable.Range(0,5).Select(lane=>snapshot.GetSurface(segment,lane))).ToArray()});
        }
        Assert.Equal(Run(PhysicalContactDiagnosticsLevel.None),Run(PhysicalContactDiagnosticsLevel.FullAudit));
    }
    private sealed class CaptureObserver:ISimulationStepObserver
    {public List<string> Rows {get;}=new(); public void OnStepResolved(ResolvedSimulationStep step)=>Rows.Add(Json(new{step.Changes,step.Events,step.Diagnostics,step.Motions,step.Interaction}));}
    private static PhysicalContactInput Swap(PhysicalContactInput i)
    {
        var c=i.Contact;var v=c.Contributions;
        return i with {Contact=c with {RiderA=c.RiderB,RiderB=c.RiderA,ComponentA=c.ComponentB,ComponentB=c.ComponentA,SourceA=c.SourceB,SourceB=c.SourceA,
            RelativePositionAtOnsetMeters=c.RelativePositionAtOnsetMeters*-1,RelativeVelocityAtOnsetMetersPerSecond=c.RelativeVelocityAtOnsetMetersPerSecond*-1,
            Contributions=new(v.BTranslationMeters,v.BRotationMeters,v.ATranslationMeters,v.ARotationMeters,v.ActualClosingMeters,v.NonadditiveResidualMeters)},
            PoseA=i.PoseB,PoseB=i.PoseA,VelocityA=i.VelocityB,VelocityB=i.VelocityA,RiderA=i.RiderB,RiderB=i.RiderA};
    }
}
