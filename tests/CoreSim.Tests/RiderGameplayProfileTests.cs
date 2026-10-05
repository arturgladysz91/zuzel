using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CoreSim.Decisions;
using CoreSim.Race;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard", "core")]
public sealed class RiderGameplayProfileTests
{
    [Fact]
    public void CanonicalSurfaceHasExactlyEightIntegerAbilitiesAndNoSpeed()
    {
        var json = JsonSerializer.SerializeToElement(RiderAbilities.Balanced);
        Assert.Equal(new[] { "Reaction", "Start", "Technique", "TrackReading", "Attack", "Defense", "PairRiding", "Strength" },
            json.EnumerateObject().Select(p => p.Name));
        Assert.All(json.EnumerateObject(), p => Assert.Equal(50, p.Value.GetInt32()));
    }

    public static IEnumerable<object[]> AbilityBoundaries()
    {
        for (var index = 0; index < 8; index++)
        foreach (var value in new[] { 0, 1, 99, 100 })
            yield return new object[] { index, value };
    }

    [Theory]
    [MemberData(nameof(AbilityBoundaries))]
    public void EveryAbilityValidatesInclusiveOneToNinetyNine(int index, int value)
    {
        var values = Enumerable.Repeat(50, 8).ToArray(); values[index] = value;
        RiderAbilities Create() => new(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7]);
        if (value is 0 or 100) Assert.Throws<ArgumentOutOfRangeException>(Create);
        else Assert.Equal(value, JsonSerializer.SerializeToElement(Create()).EnumerateObject().ElementAt(index).Value.GetInt32());
    }

    [Fact]
    public void LegacyFallbackUsesOnlyTheDeclaredMappedSkills()
    {
        var skills = new RiderSkills(80, 95, 70, 60, 40, 20);
        Assert.Equal(new RiderAbilities(80, 80, 70, 60, 50, 50, 40, 50), RiderAbilityCompatibility.ToAbilities(skills));
        Assert.Equal(RiderAbilityCompatibility.ToAbilities(new(80, 20, 70, 60, 40, 0)),
            RiderAbilityCompatibility.ToAbilities(new(80, 90, 70, 60, 40, 100)));
    }

    [Theory]
    [InlineData(0, 1)] [InlineData(100, 99)] [InlineData(20, 20)] [InlineData(50, 50)] [InlineData(80, 80)]
    [InlineData(49.5f, 50)] [InlineData(50.5f, 51)]
    public void EveryMappedLegacySkillRoundsMidpointsAwayFromZeroThenClamps(float legacy, int expected)
        => Assert.Equal(new RiderAbilities(expected, expected, expected, expected, 50, 50, expected, 50),
            RiderAbilityCompatibility.ToAbilities(new(legacy, 95, legacy, legacy, legacy, 20)));

    [Fact]
    public void UnmappedSpeedAndAdaptabilityAreIndependentlyIgnored()
    {
        var baseline = RiderAbilityCompatibility.ToAbilities(RiderSkills.Balanced);
        Assert.Equal(baseline, RiderAbilityCompatibility.ToAbilities(new(50, 20, 50, 50, 50, 50)));
        Assert.Equal(baseline, RiderAbilityCompatibility.ToAbilities(new(50, 90, 50, 50, 50, 50)));
        Assert.Equal(baseline, RiderAbilityCompatibility.ToAbilities(new(50, 50, 50, 50, 50, 20)));
        Assert.Equal(baseline, RiderAbilityCompatibility.ToAbilities(new(50, 50, 50, 50, 50, 90)));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void InteractionStyleAcceptsNormalizedEndpoints(float value)
    {
        var style = new RiderInteractionStyle(value, PreferredLine.Neutral, value);
        Assert.Equal(value, style.Combativeness); Assert.Equal(value, style.SetupIndependence);
    }

    [Theory]
    [InlineData(-.01f)] [InlineData(1.01f)] [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)] [InlineData(float.NegativeInfinity)]
    public void InteractionStyleRejectsInvalidNormalizedValues(float value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RiderInteractionStyle(value, PreferredLine.Neutral, .5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RiderInteractionStyle(.5f, PreferredLine.Neutral, value));
    }

    [Fact]
    public void InvalidLineCategoryIsRejected()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new RiderInteractionStyle(.5f, (PreferredLine)3, .5f));

    [Fact]
    public void RiskAndLaneChangePreferenceCannotChangeNeutralCombativeness()
    {
        var low = RiderAbilityCompatibility.ToInteractionStyle(new(0, 0, .5f, .6f));
        var high = RiderAbilityCompatibility.ToInteractionStyle(new(1, 1, .5f, .6f));
        Assert.Equal(low, high); Assert.Equal(.5f, high.Combativeness);
        Assert.Equal(.6f, high.SetupIndependence);
    }

    [Theory]
    [InlineData(0, PreferredLine.Inside)] [InlineData(1f / 3f, PreferredLine.Neutral)]
    [InlineData(.5f, PreferredLine.Neutral)] [InlineData(2f / 3f, PreferredLine.Neutral)]
    [InlineData(1, PreferredLine.Outside)]
    public void PreferredLineMapsInclusiveNeutralThresholds(float outside, PreferredLine expected)
        => Assert.Equal(expected, RiderAbilityCompatibility.ToInteractionStyle(new(.5f, .5f, outside, .5f)).PreferredLine);

    [Fact]
    public void NearestFloatOnEachSideOfLineThresholdHasTheCorrectCategory()
    {
        PreferredLine Map(float value) => RiderAbilityCompatibility.ToInteractionStyle(new(.5f, .5f, value, .5f)).PreferredLine;
        Assert.Equal(PreferredLine.Inside, Map(MathF.BitDecrement(1f / 3f)));
        Assert.Equal(PreferredLine.Neutral, Map(MathF.BitIncrement(1f / 3f)));
        Assert.Equal(PreferredLine.Neutral, Map(MathF.BitDecrement(2f / 3f)));
        Assert.Equal(PreferredLine.Outside, Map(MathF.BitIncrement(2f / 3f)));
    }

    [Theory]
    [InlineData(.001f)] [InlineData(68)] [InlineData(1000)] [InlineData(float.MaxValue)]
    public void MassAcceptsPositiveFiniteKilogramsWithoutAnArbitraryBodyWeightRange(float mass)
        => Assert.Equal(mass, new RiderPhysicalProfile(mass).MassKg);

    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)] [InlineData(float.NegativeInfinity)]
    public void MassRejectsNonpositiveOrNonfiniteValues(float mass)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new RiderPhysicalProfile(mass));

    [Fact]
    public void DefaultProfilePreservesExactLegacyPayloadAndUsesNamedCompatibilityMass()
    {
        var profile = RiderProfile.CreateDefault(7);
        Assert.Equal(new RiderSkills(50, 50, 50, 50, 50, 50), profile.Skills);
        Assert.Equal(new RiderStyle(.5f, .5f, .5f, .5f), profile.Style);
        Assert.Equal("Rider 7", profile.Name); Assert.False(profile.HasExplicitGameplay);
        Assert.Equal(70f, RiderAbilityCompatibility.LegacyCompatibilityMassKg);
        Assert.Equal(RiderAbilityCompatibility.LegacyCompatibilityMassKg, profile.Gameplay.Physical.MassKg);
        Assert.Equal(RiderAbilities.Balanced, profile.Gameplay.Abilities);
    }

    [Fact]
    public void ExplicitCanonicalExampleRetainsValuesAndRequiresAnIndependentLegacyPayload()
    {
        var gameplay = RiderCompatibilityFixture.ExampleGameplay();
        var skills = new RiderSkills(80, 95, 70, 60, 40, 20);
        var style = new RiderStyle(.1f, .2f, .3f, .4f);
        var profile = RiderProfile.CreateCanonical(7, "Modern", gameplay, skills, style);
        Assert.True(profile.HasExplicitGameplay); Assert.Same(gameplay, profile.Gameplay);
        Assert.Equal(new RiderAbilities(91, 84, 88, 76, 93, 67, 55, 72), profile.Gameplay.Abilities);
        Assert.Equal(68f, profile.Gameplay.Physical.MassKg);
        Assert.Equal(new RiderInteractionStyle(.82f, PreferredLine.Inside, .60f), profile.Gameplay.InteractionStyle);
        Assert.Same(skills, profile.Skills); Assert.Same(style, profile.Style);
        Assert.Throws<ArgumentNullException>(() => RiderProfile.CreateCanonical(7, "Modern", null!, skills, style));
        Assert.Throws<ArgumentNullException>(() => RiderProfile.CreateCanonical(7, "Modern", gameplay, null!, style));
        Assert.Throws<ArgumentNullException>(() => RiderProfile.CreateCanonical(7, "Modern", gameplay, skills, null!));
        Assert.Throws<ArgumentNullException>(() => new RiderGameplayProfile(null!, gameplay.Physical, gameplay.InteractionStyle));
        Assert.Throws<ArgumentNullException>(() => new RiderGameplayProfile(gameplay.Abilities, null!, gameplay.InteractionStyle));
        Assert.Throws<ArgumentNullException>(() => new RiderGameplayProfile(gameplay.Abilities, gameplay.Physical, null!));
    }

    [Fact]
    public void OldConstructorDeconstructionEqualityWithAndJsonRemainCompatible()
    {
        var old = RiderProfile.CreateDefault(7);
        var (id, name, skills, style) = old;
        Assert.Equal(7, id); Assert.Equal("Rider 7", name); Assert.Same(old.Skills, skills); Assert.Same(old.Style, style);
        const string expected = "{\"Id\":7,\"Name\":\"Rider 7\",\"Skills\":{\"Start\":50,\"Speed\":50,\"SlideControl\":50,\"TrackReading\":50,\"PairRiding\":50,\"Adaptability\":50},\"Style\":{\"RiskTolerance\":0.5,\"LaneChangeTendency\":0.5,\"OutsidePreference\":0.5,\"SetupIndependence\":0.5}}";
        Assert.Equal(expected, JsonSerializer.Serialize(old));
        Assert.Equal(old, JsonSerializer.Deserialize<RiderProfile>(expected));
        Assert.Equal(old, old with { });
        var changed = old with { Skills = new RiderSkills(80, 95, 70, 60, 40, 20) };
        Assert.Equal(80, changed.Gameplay.Abilities.Reaction);
        var modern = RiderCompatibilityFixture.Modern(old);
        Assert.Equal(expected, JsonSerializer.Serialize(modern));
        Assert.Same(modern.Gameplay, (modern with { Skills = changed.Skills }).Gameplay);
        Assert.NotEqual(modern, RiderCompatibilityFixture.Modern(old, "Attack"));
        Assert.Equal(modern, RiderCompatibilityFixture.Modern(old));
        Assert.Equal(modern.Gameplay, JsonSerializer.Deserialize<RiderGameplayProfile>(JsonSerializer.Serialize(modern.Gameplay)));
    }

    [Fact]
    public void NewStatesHaveFullConditionAndHistoricalJsonOmitsCanonicalAdditions()
    {
        var track = Track.CreateStandingStartExample();
        var old = new RiderState(1, 2);
        var modern = new RiderState(RiderCompatibilityFixture.Modern(old.Profile), 2);
        Assert.Equal(1f, old.Condition); Assert.Equal(1f, modern.Condition);
        Assert.Equal(1f, new RiderState(old.Profile, StartingGate.A, track).Condition);
        modern.Condition = .72f;
        Assert.Equal(JsonSerializer.Serialize(old), JsonSerializer.Serialize(modern));
    }

    [Theory]
    [InlineData(-.1f, 0f)] [InlineData(0f, 0f)] [InlineData(.72f, .72f)]
    [InlineData(1f, 1f)] [InlineData(1.1f, 1f)]
    public void ConditionClampsFiniteValuesLikeMorale(float input, float expected)
    {
        var state = new RiderState(1, 2) { Morale = input, Condition = input };
        Assert.Equal(expected, state.Condition); Assert.Equal(state.Morale, state.Condition);
        state.ApplyConditionDelta(2); Assert.Equal(1f, state.Condition);
        state.ApplyConditionDelta(-2); Assert.Equal(0f, state.Condition);
    }

    [Theory]
    [InlineData(float.NaN)] [InlineData(float.PositiveInfinity)] [InlineData(float.NegativeInfinity)]
    public void ConditionRejectsNonfiniteStateAndDeltas(float input)
    {
        var state = new RiderState(1, 2);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.Condition = input);
        Assert.Throws<ArgumentOutOfRangeException>(() => state.ApplyConditionDelta(input));
        Assert.Equal(1f, state.Condition);
    }

    [Fact]
    public void BothHeatResetsPreserveConditionAndSnapshotsKeepDetachedState()
    {
        var track = Track.CreateStandingStartExample();
        var state = new RiderState(1, 2) { Condition = .72f };
        state.ResetForHeat(3); Assert.Equal(.72f, state.Condition);
        state.ResetForHeat(StartingGate.B, track); Assert.Equal(.72f, state.Condition);
        var model = new InspectCondition();
        var engine = new SimulationEngine(model);
        var snapshot = engine.CaptureSnapshot(track, TrackState.CreateDefault(track), new[] { state }, new(56, 0, 0, 0, 7, 4));
        Assert.Equal(.72f, snapshot.Rider(1).Condition);
        state.Condition = .2f;
        Assert.Equal(.72f, snapshot.Rider(1).Condition);
        engine.Decide(snapshot); Assert.Equal(.72f, model.Seen);
        Assert.DoesNotContain("Condition", JsonSerializer.Serialize(snapshot.Rider(1)));
        new HeatSimulator(new InspectCondition()).SimulateHeat(track, TrackState.CreateDefault(track), new() { state });
        Assert.Equal(.2f, state.Condition); // No fatigue, recovery or reset consumer.
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompleteFourRiderLegacyHeatMatchesUnmodifiedMainByteForByte(bool adaptive)
    {
        var bytes = RiderCompatibilityFixture.Capture(adaptive);
        Assert.Equal(adaptive ? RiderCompatibilityFixture.AdaptiveSha256 : RiderCompatibilityFixture.ConvergenceSha256,
            Convert.ToHexString(SHA256.HashData(bytes)));
        if (!adaptive) Assert.Contains("contact rider=", Encoding.UTF8.GetString(bytes));
    }

    public static IEnumerable<object[]> CanonicalChanges()
    {
        foreach (var adaptive in new[] { false, true })
        foreach (var change in new[] { "None", "Reaction", "Start", "Technique", "TrackReading", "Attack", "Defense",
                     "PairRiding", "Strength", "MassKg", "Combativeness", "PreferredLine", "SetupIndependence", "Condition" })
            yield return new object[] { adaptive, change };
    }

    [Theory]
    [MemberData(nameof(CanonicalChanges))]
    public void ExplicitCanonicalDataAndEachIndependentChangeLeaveTheCompleteHeatUnchanged(bool adaptive, string change)
    {
        var expected = RiderCompatibilityFixture.Capture(adaptive, p => RiderCompatibilityFixture.Modern(p));
        var actual = RiderCompatibilityFixture.Capture(adaptive, p => RiderCompatibilityFixture.Modern(p, change),
            state => state.Condition = change == "Condition" ? .2f : 1f);
        Assert.Equal(expected, actual);
        Assert.Equal(adaptive ? RiderCompatibilityFixture.AdaptiveSha256 : RiderCompatibilityFixture.ConvergenceSha256,
            Convert.ToHexString(SHA256.HashData(actual)));
    }

    private sealed class InspectCondition : IRiderDecisionModel
    {
        public float Seen { get; private set; }
        public RiderDecision Decide(TrackSegment segment, RiderState rider)
        {
            Seen = rider.Condition;
            return new(rider.Lane);
        }
    }
}
