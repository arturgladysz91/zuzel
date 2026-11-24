using System.Numerics;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal sealed record CornerLink211(int Visit, double ExitDrive, double StraightDrive, double ReleaseMeters,
    double ReleaseDrive, double ExitSteer, double PrepareSteer, double TurnSteer, double ApexSteer,
    double PrepareLineMeters, double AlignmentGain);

internal sealed class LinkFeedback211
{
    private readonly (CornerLink211 Move, BendVisit Outgoing, BendVisit Incoming, RoadPoint Straight)[] links;
    internal LinkFeedback211(BendMap map, IEnumerable<CornerLink211> moves) =>
        links = moves.Select(move =>
        {
            var bend = map.At(move.Visit / map.Count, move.Visit % map.Count);
            return (move, bend, map.At((move.Visit + 1) / map.Count, (move.Visit + 1) % map.Count),
                map.Locate(bend.Exit + 1).Road);
        }).ToArray();

    internal static double Begin(BendVisit bend) => bend.Entry + bend.Length * .5;
    internal static bool Eligible(double settledS, string? strictFailure, double reserve, double requiredS) =>
        strictFailure is null or "goal" && settledS >= requiredS && reserve >= .50;

    private static double Gate(double from, double to, double s) => ExitPolicy208.Gate((s - from) / (to - from));
    private static double Pulse(double start, double rise, double fall, double end, double s) =>
        Gate(start, rise, s) * (1 - Gate(fall, end, s));

    internal FreeRacingAction Apply(Start197 state, FreeRacingAction original)
    {
        var rider = state.State.Rider;
        double s = rider.TotalReferenceMeters, deltaSteer = 0, deltaDrive = 0;
        foreach (var (move, outgoing, incoming, straight) in links)
        {
            double begin = Begin(outgoing), exit = outgoing.Exit, entry = incoming.Entry;
            double apex = Begin(incoming);
            if (s <= begin || s >= incoming.Exit + 16) continue;
            // Release is a position within the whole linking maneuver. Issued commands
            // still go through Schedule188 and vehicle steering remains rate-limited.
            double release = entry - move.ReleaseMeters;
            double exitWeight = Pulse(begin, exit - 12, exit + 3, exit + 18, s);
            double accelerate = Pulse(exit - 7, exit + 10, release - 4, release + 4, s);
            double preparation = Pulse(exit + 7, entry - 19, entry + 2, entry + 18, s);
            double turn = Pulse(entry - 12, entry + 10, entry + 20, apex + 8, s);
            double mid = Pulse(apex - 17, apex + 6, incoming.Exit - 12, incoming.Exit + 16, s);
            double releaseWeight = Pulse(release - 5, release + 6, entry + 5, entry + 23, s);
            deltaDrive += move.ExitDrive * exitWeight + move.StraightDrive * accelerate + move.ReleaseDrive * releaseWeight;
            double outward = Vector2.Dot(rider.Velocity, straight.Outward);
            double forward = Vector2.Dot(rider.Velocity, straight.Tangent);
            double course = Math.Clamp(Math.Atan2(outward, Math.Max(1, forward)), -.24, .24);
            deltaSteer += move.ExitSteer * exitWeight + move.PrepareSteer * preparation +
                move.TurnSteer * turn + move.ApexSteer * mid +
                preparation * (move.AlignmentGain * (course + .14 * rider.AngularVelocity) -
                    move.PrepareLineMeters / (9 + .27 * rider.Speed));
        }
        if (deltaDrive == 0 && deltaSteer == 0) return original;
        double gas = original.Drive + deltaDrive * (deltaDrive > 0 ? 1 - original.Drive : original.Drive);
        var action = ExitPolicy208.Bound(original.SteeringRadians + deltaSteer, gas);
        ThrottleTrace209.Current?.Add("211 coupled link", original.Drive, action.Drive);
        return action;
    }
}
