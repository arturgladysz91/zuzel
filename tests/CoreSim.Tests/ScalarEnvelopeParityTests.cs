using CoreSim.Analysis;
using CoreSim.Setup;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "trajectory")]
public sealed class ScalarEnvelopeParityTests
{
    [Theory]
    [InlineData(0f)] [InlineData(.137f)] [InlineData(1f)] [InlineData(1.73f)]
    [InlineData(2f)] [InlineData(2.51f)] [InlineData(3f)] [InlineData(3.9999f)] [InlineData(4f)]
    public void ScalarCachedAndAuditedMetreArithmeticAreBitExact(float lateral)
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var phase = track.CornerTopology.Resolve(1, 0f, lateral, track.Geometry)!.Value;
        var progresses = new[] { 0f, .125f, .4999f, MathF.BitDecrement(.5f), .5f,
            MathF.BitIncrement(.5f), .501f, .6137f, .75f,
            MathF.BitDecrement(ContinuousCornerEnvelope.FullDriveProgress),
            ContinuousCornerEnvelope.FullDriveProgress, MathF.BitIncrement(ContinuousCornerEnvelope.FullDriveProgress),
            .875f, .9999f, 1f };
        foreach (var surface in new[] { new TrackSurfaceState(1f, 0f, .35f), new(.6f, .15f, .7f), new(.83f, .047f, .41f) })
        foreach (var skill in new[] { 0f, 50f, 100f })
        foreach (var gearing in new[] { 0f, .5f, 1f })
        foreach (var bias in new[] { 0f, .5f, 1f })
        {
            var skills = new RiderSkills(skill, skill, skill, skill, skill, skill);
            var setup = new BikeSetup(gearing, bias);
            var model = ContinuousCornerEnvelope.Create(phase, lateral, track.Geometry, surface, skills, setup);
            var parameters = ContinuousCornerEnvelope.CreateParameters(phase, lateral, track.Geometry, surface, skills, setup);
            foreach (var progress in progresses.Concat(progresses.Reverse()))
            {
                var expected = Reference(model, progress);
                Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(model.SpeedMetersPerSecond(progress)));
                Assert.Equal(BitConverter.SingleToInt32Bits(expected), BitConverter.SingleToInt32Bits(parameters.SpeedMetersPerSecond(progress)));
            }
        }
    }

    // Independent test oracle freezes the audited operation/cast order. Production
    // has one shared SpeedCore; this never participates in a simulation.
    private static float Reference(ContinuousCornerEnvelope model, float progress)
    {
        if (progress <= .5f)
            return (float)Math.Sqrt((double)model.ApexSpeedMetersPerSecond * model.ApexSpeedMetersPerSecond
                + 2d * model.CorrectionCapabilityMetersPerSecondSquared * (.5f - progress) * model.TotalLengthMeters);
        double end = (progress - .5f) * (double)model.TotalLengthMeters;
        var resolution = LongitudinalDynamics.ProvisionalLongitudinalIntegrationStepMeters;
        var complete = checked((int)Math.Floor(end / resolution));
        var values = new List<float> { model.ApexSpeedMetersPerSecond };
        while (values.Count <= complete)
        {
            var midpoint = (float)(.5f + (values.Count - .5d) * resolution / model.TotalLengthMeters);
            values.Add(LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(values[^1], resolution,
                model.FullDriveReferenceForceNewtons, model.Setup, ContinuousCornerEnvelope.DriveAvailability(midpoint)));
        }
        var speed = values[complete];
        var remainder = end - complete * (double)resolution;
        if (remainder > 0d)
        {
            var midpoint = (float)(.5f + (complete * (double)resolution + remainder * .5d) / model.TotalLengthMeters);
            speed = LongitudinalDynamics.CalculateMidpointDriveEndSpeedMetersPerSecond(speed, (float)remainder,
                model.FullDriveReferenceForceNewtons, model.Setup, ContinuousCornerEnvelope.DriveAvailability(midpoint));
        }
        return speed;
    }
}
