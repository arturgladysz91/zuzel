
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private enum Ablation217
    {
        Baseline,
        NoEntryDriveCut,
        NoLeanDemandClamp,
        NoExtraSteeringClamp,
        NoProportionalRoll,
        NoRollRateDamping,
        NoRollLatencyLead,
        StateRollInertia,
        HoldRiderTarget,
        RetractLeftFoot,
        NoExitDriveBoost,
        NoCourseErrorClamp,
        NoFrontAuthorityThreshold,
        SignedFrontLever,
        NoUtilizationReserve,
        NoRadiusFloor,
        NoAlignmentSpeedFloor,
        NoCornerPreviewTaper,
        NoCurvatureLookahead,
        NoLaneSine,
        NoLaneCourseCorrection,
        NoHeadingAccelerationCorrection,
        NoFallbackSteerCorrection
    }

    private sealed record CornerObservation217(Geometry215 Geometry, State215 State, RiderView Bike, double Reference,
        BendVisit Bend, CornerTuning216 Tuning, Forces215? Previous, Activation215[] Queue, double Clearance);

    private readonly record struct LegacyDecision217(Command215 Command, float PlannedDrive, bool EntryGate,
        bool EntryCutApplied, bool ExitBoostApplied, double CentripetalDemand, double RawLeanDemand,
        double LeanDemand, double TargetRoll, double RawSteering, double? FrontUtilization,
        double RollInertia, double FrontLever);

    private static LegacyDecision217 LegacyControl217(Geometry215 geometry, State215 s, Kinematics215 motion,
        RoadPoint road, double reference, BendVisit bend, CornerTuning216 tuning, Forces215? observed,
        Ablation217 ablation = Ablation217.Baseline, Vec215 heldPosture = default)
    {
        double speed = s.Momentum.Plane.Norm / geometry.Base.Mass;
        double heading = Math.Atan2(s.Momentum.Y, s.Momentum.X);
        double phase = Math.Clamp((reference - bend.Entry) / bend.Length, 0, 1);
        double line = tuning.Lane - (ablation == Ablation217.NoLaneSine ? 0 : 1.60 * Math.Sin(phase * Math.PI));
        double lookahead = ablation == Ablation217.NoCurvatureLookahead ? 0 : 5;
        double radius = Frame.RoadAt(Math.Clamp(reference + lookahead, bend.Entry + .01, bend.Exit - .01), line).RadiusMeters;
        double turnWeight = Math.Clamp((reference - bend.Entry + 8) / 13, 0, 1) *
            Math.Clamp((bend.Exit + 1 - reference) / 9, 0, 1);
        if (ablation == Ablation217.NoCornerPreviewTaper) turnWeight = road.IsTurn ? 1 : 0;
        double course = road.Heading +
            (ablation == Ablation217.NoLaneCourseCorrection ? 0 : Math.Atan2(road.OffsetMeters - line, 11));
        double correction = BikeAngles.Wrap(course - heading);
        if (ablation != Ablation217.NoCourseErrorClamp) correction = Math.Clamp(correction, -.34, .34);
        double centripetal = turnWeight * speed * speed /
            Math.Max(ablation == Ablation217.NoRadiusFloor ? double.Epsilon : 15, radius);
        double rawDemand = centripetal + (ablation == Ablation217.NoHeadingAccelerationCorrection ? 0 : 1.8 * speed * correction);
        double usedDemand = ablation == Ablation217.NoLeanDemandClamp ? rawDemand : Math.Clamp(rawDemand, -10.5, 11.8);
        double targetLean = -Math.Atan(usedDemand / Gravity216);
        double roll = s.Pose.Roll;
        double gain = ablation == Ablation217.NoProportionalRoll ? 0 : tuning.RollGain;
        double lead = ablation == Ablation217.NoRollLatencyLead ? 0 : ReactionSeconds216;
        double damping = ablation == Ablation217.NoRollRateDamping ? 0 : 5;
        double desiredRate = gain * (targetLean - roll - lead * motion.BodyRate.X) - damping * motion.BodyRate.X;
        var unsteered = geometry.Contact(s with { Steer = 0 }, motion, true, 0);
        double forward = unsteered.Velocity.Dot(unsteered.Forward);
        double aligned = Math.Atan2(unsteered.Velocity.Dot(unsteered.Side),
            ablation == Ablation217.NoAlignmentSpeedFloor ? forward : Math.Max(1, forward));
        double steer = ablation == Ablation217.NoFallbackSteerCorrection
            ? aligned : aligned - .036 * roll + .034 * desiredRate;
        double? utilizationFound = null;
        double leverFound = 0;
        double rollInertia = ablation == Ablation217.StateRollInertia ? EffectiveRollInertia217(geometry, s, motion) : 20;
        double capacityGate = ablation == Ablation217.NoFrontAuthorityThreshold ? 0 : 40;
        if (observed is { } last && last.Front.Capacity > capacityGate)
        {
            var front = last.Front;
            var world = front.Patch.Arm.Cross(Vec215.Up * front.Normal) +
                last.Rear.Patch.Arm.Cross(last.Rear.Force) +
                (last.LeftFoot is { } boot ? boot.Arm.Cross(boot.Force) : default);
            double existing = s.Pose.Body(world).X;
            double lever = s.Pose.Body(unsteered.Arm.Cross(unsteered.Side)).X;
            leverFound = lever;
            double gyro = motion.BodyRate.Cross(s.Pose.Body(s.Angular)).X;
            double moment = rollInertia * desiredRate + gyro;
            double effectiveLever = ablation == Ablation217.SignedFrontLever ? lever : Math.Max(.15, lever);
            if (Math.Abs(effectiveLever) > 1e-9)
            {
                double reserve = ablation == Ablation217.NoUtilizationReserve ? 1 : .90;
                double utilization = Math.Clamp((moment - existing) / (effectiveLever * front.Capacity), -reserve, reserve);
                utilizationFound = utilization;
                double ratio = Math.Sqrt(1 - utilization * utilization);
                steer = aligned + (ablation == Ablation217.NoUtilizationReserve
                    ? Math.Atan2(.115 * utilization, ratio) : Math.Atan(.115 * utilization / ratio));
            }
        }
        float drive = tuning.BendDrive;
        bool gate = reference < bend.Entry + 2 && centripetal > 10.5;
        bool cut = gate && ablation != Ablation217.NoEntryDriveCut;
        if (cut) drive = 0;
        bool boost = reference > bend.Exit - 8 && ablation != Ablation217.NoExitDriveBoost;
        if (boost) drive = (float)Math.Min(1, drive + .35);
        var move = ablation == Ablation217.HoldRiderTarget ? heldPosture :
            new Vec215(.085 - .085 * phase, -.10 * targetLean, -.025 * turnWeight);
        double foot = tuning.Leg && ablation != Ablation217.RetractLeftFoot
            ? Math.Clamp((Math.Abs(targetLean) - .24) * 1.23, 0, .75) : 0;
        float requestedSteer = (float)(ablation == Ablation217.NoExtraSteeringClamp ? steer : Math.Clamp(steer, -.37, .37));
        var command = new Command215(new(requestedSteer, drive), move) { LeftFoot = foot };
        return new(command, tuning.BendDrive, gate, cut && tuning.BendDrive > 0, boost,
            centripetal, rawDemand, usedDemand, targetLean, steer, utilizationFound, rollInertia, leverFound);
    }

    private static double EffectiveRollInertia217(Geometry215 geometry, State215 state, Kinematics215 motion)
    {
        // The momentum-to-rate map is linear at fixed pose, rotor momenta and shape.
        var offset = state with { Angular = state.Angular + state.Pose.Apply(Vec215.Forward) };
        double mobility = geometry.Motion(offset).BodyRate.X - motion.BodyRate.X;
        if (!(mobility > 0) || !double.IsFinite(mobility))
            throw new InvalidOperationException("invalid physical roll mobility");
        return 1 / mobility;
    }
}
