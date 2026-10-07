using CoreSim.PhysicalSpace;

namespace CoreSim.Interactions;

/// <summary>Reads the existing final actual verification, poses and snapshot; performs no production projection.</summary>
internal static class PhysicalContactSnapshotAdapter
{
    internal static PhysicalContactAnalysis Analyze(ContestedSpaceReport verified, IReadOnlyList<PhysicalPoseInterval> poses,
        SimulationSnapshot snapshot, TrackMetricEmbedding embedding, IReadOnlyList<InteractionEpisodeDiagnostic> episodes,
        IReadOnlyList<SimulationStepEvent> legacyEvents, PhysicalContactParameters parameters, PhysicalContactDiagnosticsLevel level,
        Func<PhysicalContactPairAnalysis, bool>? applicable = null)
    {
        var contacts = episodes.SelectMany(e => e.UnresolvedMechanicalContacts
            .Select(c => (Episode:e,Contact:c))).ToArray();
        if (contacts.Length == 0) return PhysicalContactAnalysis.Empty(level);
        var history = CommonTimePoseHistory.Stitch(poses);
        PhysicalPoseInterval? Interval(int rider, ContestedSpaceEvent c) => history.Where(i => i.RiderId == rider
            && i.FrameId == c.FrameId && i.StartTimeSeconds <= c.FirstTouchCommonTimeSeconds!.Value
            && i.EndTimeSeconds >= c.FirstTouchCommonTimeSeconds.Value).OrderByDescending(i => i.StartTimeSeconds).FirstOrDefault();
        PhysicalContactRiderInput Rider(int id, PhysicalBikePose? pose)
        {
            var rider = snapshot.Rider(id);
            // Missing pose stays GeometryUnresolved. No reconstructed path or invented surface challenge.
            var grip = pose is null ? snapshot.TrackState.SampleSurface(snapshot.Step.SegmentIndex,rider.LateralPosition).EffectiveGrip
                : Grip(pose,snapshot,embedding);
            return new(id,rider.Profile.Gameplay,rider.Condition,grip);
        }
        var inputs = new List<PhysicalContactInput>();
        foreach (var item in contacts)
        {
            var c = verified.Intervals.Where(c => c.EligibleForFutureInteraction
                && Math.Min(c.RiderA,c.RiderB) == Math.Min(item.Contact.RiderA,item.Contact.RiderB)
                && Math.Max(c.RiderA,c.RiderB) == Math.Max(item.Contact.RiderA,item.Contact.RiderB)
                && c.FirstTouchCommonTimeSeconds == item.Contact.CommonTimeSeconds).FirstOrDefault();
            if (c is null) throw new InvalidOperationException("Fallback authorization must reference the final actual #55 contact.");
            var a = Interval(c.RiderA,c); var b = Interval(c.RiderB,c); var time = c.FirstTouchCommonTimeSeconds!.Value;
            var pa = a?.Sample(time); var pb = b?.Sample(time);
            var observations = new[] {c.RiderA,c.RiderB}.Select(id => new RiderLegacyContactObservation(id,
                !item.Contact.LegacyFallbackAuthorized ? LegacyContactOutcome.NotAuthorized
                : legacyEvents.Any(e => e.RiderId == id && e.OtherRiderId == (id == c.RiderA ? c.RiderB : c.RiderA)
                    && e.Type == SimulationEventType.ContactCrash) ? LegacyContactOutcome.LegacyCrash
                : legacyEvents.Any(e => e.RiderId == id && e.OtherRiderId == (id == c.RiderA ? c.RiderB : c.RiderA)
                    && e.Type == SimulationEventType.ContactLostRhythm) ? LegacyContactOutcome.LegacyLostRhythm
                : LegacyContactOutcome.NoLegacyOccurrence)).ToArray();
            inputs.Add(new(c,pa,pb,a?.RateBounds(time,time).CenterVelocityMetersPerSecond ?? default,
                b?.RateBounds(time,time).CenterVelocityMetersPerSecond ?? default,Rider(c.RiderA,pa),Rider(c.RiderB,pb),
                item.Episode.EpisodeId,item.Episode.FallbackProvenance.Select(p => p.OriginEpisodeId).ToArray(),item.Contact.LegacyFallbackAuthorized,observations));
        }
        return applicable is null ? PhysicalContactAnalyzer.Analyze(inputs,parameters,level)
            : PhysicalContactAnalyzer.AnalyzeForConsequences(inputs,parameters,applicable);
    }
    internal const double SurfaceMappingRoundoffToleranceMeters = 1e-9;
    internal static double Grip(PhysicalBikePose pose, SimulationSnapshot snapshot, TrackMetricEmbedding embedding)
    {
        if (pose.Source is null) throw new ArgumentException("Production poses require their sampled segment source.");
        var segment = embedding.Segments[pose.Source.SegmentIndex]; var heading = segment.StartTangentHeadingRadians;
        var tangent=ContactFrameArithmetic.Direction(heading,segment.DeterministicArithmetic);
        var offset = segment.SegmentType == SegmentType.Straight
            ? MeterPoint.Dot(pose.Position-segment.StartReferencePosition,new(tangent.Y,-tangent.X))
            : (pose.Position-(segment.StartReferencePosition+new MeterPoint(-tangent.Y,tangent.X)*segment.InnerRadiusMeters)).Length
                - segment.InnerRadiusMeters;
        // No displacement or new surface read location: inverse of the existing #55 metric mapping at first touch.
        var width=LaneModel.UsableRacingWidthMeters(segment.SegmentType,snapshot.Track.Geometry);
        if(offset < -SurfaceMappingRoundoffToleranceMeters || offset > width+SurfaceMappingRoundoffToleranceMeters)
            throw new ArgumentOutOfRangeException(nameof(pose),"First-touch surface location is outside the frozen usable track.");
        // Inverting a valid edge pose can produce -7e-15 m from subtraction/sqrt.
        // Admit only named numerical roundoff; never move or repair a bike pose.
        var lateral = LaneModel.LateralPositionFromPhysicalOffsetMeters((float)Math.Clamp(offset,0,width),segment.SegmentType,snapshot.Track.Geometry);
        return snapshot.TrackState.SampleSurface(pose.Source.SegmentIndex,lateral).EffectiveGrip;
    }
}
