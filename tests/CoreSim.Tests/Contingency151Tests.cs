using System.Text.Json;
using Autonomous146;
using Contingency151;
using CoreSim;
using Robust150;
using SharedWorldSpeedway;
using Xunit;
namespace CoreSim.Tests;
[Trait("Shard", "core")]
public class Contingency151Tests {
    static Tape150 Tape() => Program150.Read<Tape150>(PhysicalSteeringAudit.PreviousPath("../robust-autonomous-racing/diagnosis-safe/A-A-original-fixed.json.gz"));
    static RiderSpec Spec() => StandingWorld144.Rider(StartingGate.B) with { SpeedMetersPerSecond = 25, ReferenceMeters = 23, OffsetMeters = 10 };
    [Fact] public void DefaultsAreOffAndUnknownFlagsDoNotRunWorlds() {
        Assert.Throws<InvalidOperationException>(() => new Planner151(Spec(), false));
        Assert.Throws<ArgumentException>(() => Program151.Main(Array.Empty<string>()));
        Assert.Throws<ArgumentException>(() => Program151.Main(new[] { "--not-an-experiment" }));
    }
    [Theory] [InlineData(1)] [InlineData(-1)] public void ObservationInnovationPreservesImmutableQueueAndActuators(int sign) {
        var state = Tape().States[1]; var before = JsonSerializer.Serialize(state, Program151.Json);
        var planner = new Planner151(Spec(), false, new(Enabled: true)); var child = planner.Scenario(state, sign);
        Assert.Equal(before, JsonSerializer.Serialize(state, Program151.Json));
        Assert.Same(state.Input.Pending, child.Input.Pending); Assert.Equal(state.Input.Active, child.Input.Active);
        Assert.Equal(state.Input.Steering, child.Input.Steering); Assert.Equal(state.Input.Time, child.Input.Time);
        Assert.NotEqual(state.Input.Rider.Heading, child.Input.Rider.Heading);
    }
    [Fact] public void NewObservationCannotChangeWhatIsAlreadyExecuting() {
        var state = Tape().States[1]; var planner = new Planner151(Spec(), false, new(Enabled: true)); var runtime = planner.Runtime; runtime.Begin(state.Input.Time);
        var action = new FreeRacingAction(-.03f, 0); var next = runtime.Step(state, action).End;
        var command = Assert.Single(next.Input.Pending);
        Assert.Equal(state.Input.Time + .24, command.ActivationAt, 8); Assert.Equal(action, command.Action);
        Assert.NotEqual(action, next.Input.Active);
        var later = runtime.Step(next, new(.02f, 1)).End; Assert.Equal(action, later.Input.Active);
    }
    [Fact] public void RepairGeneratorOffersBothDirectionsAndAllThrottleChannels() {
        var actions = Planner151.RepairActions(new(0, 1));
        Assert.Contains(actions, a => a.SteeringRadians < 0 && a.Drive == 0);
        Assert.Contains(actions, a => a.SteeringRadians > 0 && a.Drive == 1);
        Assert.Contains(actions, a => a.Drive == (float)Controller143.Policy.Drive);
        Assert.All(actions, a => a.Validate());
    }
    [Fact] public void InvalidTreeBoundsAreRejected() {
        Assert.Throws<ArgumentException>(() => new Planner151(Spec(), false, new(Enabled: true, ObservationDepth: 1)));
        Assert.Throws<ArgumentException>(() => new Planner151(Spec(), false, new(Enabled: true, MaximumRoots: 100)));
    }
    [Fact] public void AxisRestrictionIsLostAsSoonAsAnIrrevocableTurnExists() {
        var v = Tape().States[0]; var input = v.Input;
        var straight = v with { Input = input with { Rider = input.Rider with { Heading = 0, AngularVelocity = 0, Velocity = new(2, 0), Road = input.Rider.Road with { Heading = 0, RadiusMeters = 0 } }, Steering = 0, Active = new(0, 1), Pending = Array.Empty<FreeRacingCommand>() } };
        Assert.True(Planner151.AxisAlignedInvariant(straight));
        var planner = new Planner151(Spec(), false, new(Enabled: true)); var innovation = planner.Scenario(straight, 1);
        Assert.Equal(straight.Input.Rider.Heading, innovation.Input.Rider.Heading); Assert.Equal(0, innovation.Input.Rider.Velocity.Y);
        var turning = straight with { Input = straight.Input with { Pending = new[] { new FreeRacingCommand(straight.Input.Time, straight.Input.Time + .24, new(.01f, 1)) } } };
        Assert.False(Planner151.AxisAlignedInvariant(turning)); Assert.NotEqual(turning.Input.Rider.Heading, planner.Scenario(turning, 1).Input.Rider.Heading);
    }
    [Fact] public void OnlyExplicitPhysicalProposalErrorsAreClassifiedForExclusion() {
        Assert.True(Planner151.PhysicalForecastFailure(new("Crank/clutch passivity or energy identity failed.")));
        Assert.False(Planner151.PhysicalForecastFailure(new("unexpected programming error")));
    }
}
