using CoreSim.Analysis;
using CoreSim.Decisions;
using CoreSim.Race;
using Xunit;

namespace CoreSim.Tests;

public sealed class ProjectionMaterializationTests
{
    [Fact]
    public void NormalDecideCapturesNoRichGraphEventsLogsOrMetreNodes()
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var context = TrajectoryEvaluatorTests.Context(track, TrajectoryEvaluatorTests.At(track, 1, 2, 22f));
        var counts = new Dictionary<ProjectionMaterialization, int>();
        ProjectionCaptureAudit.Observer = (kind, n) => counts[kind] = counts.GetValueOrDefault(kind) + n;
        try
        {
            var model = new AdaptiveDecisionModel();
            var decision = model.Decide(context);
            Assert.Equal(model.Evaluate(context).Decision, decision);
            Assert.True(counts.GetValueOrDefault(ProjectionMaterialization.PhysicalEvaluation) > 0);
            Assert.All(Enum.GetValues<ProjectionMaterialization>().Where(k => k != ProjectionMaterialization.PhysicalEvaluation
                && (int)k <= (int)ProjectionMaterialization.CornerNode),
                k => Assert.Equal(0, counts.GetValueOrDefault(k)));
            counts.Clear();
            var rich = new TrajectoryEvaluator(context).Evaluate(new(3,2,3), true);
            Assert.NotEmpty(rich.ResolvedMotions);
            foreach(var k in new[]{ProjectionMaterialization.RichMotion, ProjectionMaterialization.MotionSample,
                ProjectionMaterialization.Event, ProjectionMaterialization.FormattedLog,
                ProjectionMaterialization.ExecutedPath, ProjectionMaterialization.ExecutedNode, ProjectionMaterialization.ExecutedStep})
                Assert.True(counts.GetValueOrDefault(k) > 0, k.ToString());
        }
        finally { ProjectionCaptureAudit.Observer = null; }
    }

    [Fact]
    public void EveryUniqueTypedPrefixExecutesPhysicsOnceIncludingReversedAndRepeatedRequests()
    {
        var track = MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();
        var context = TrajectoryEvaluatorTests.Context(track, TrajectoryEvaluatorTests.At(track,1,2,22f));
        var candidates = TrajectoryCandidates.Generate(SegmentType.TurnEntry);
        var evaluator = new TrajectoryEvaluator(context); var physical = 0;
        ProjectionCaptureAudit.Observer = (kind,n) => { if(kind == ProjectionMaterialization.PhysicalEvaluation) physical += n; };
        try
        {
            foreach(var intent in candidates) evaluator.Evaluate(intent);
            var unique = evaluator.ProductionResolutionCount;
            var prefixes = candidates.SelectMany(intent => Enumerable.Range(1,evaluator.Horizon.Count)
                .Select(length => string.Join(",",evaluator.Horizon.Take(length)
                    .Select(part => TrajectoryEvaluator.Target(intent,part.Phase))))).Distinct().Count();
            Assert.Equal(unique,physical); Assert.Equal(prefixes,unique);
            foreach(var intent in candidates.Reverse()) evaluator.Evaluate(intent);
            foreach(var intent in candidates) evaluator.Evaluate(intent);
            Assert.Equal(unique,physical); Assert.Equal(unique,evaluator.ProductionResolutionCount);
            Assert.Equal(105,evaluator.CandidateTraversalCount);
        }
        finally { ProjectionCaptureAudit.Observer = null; }
    }
}
