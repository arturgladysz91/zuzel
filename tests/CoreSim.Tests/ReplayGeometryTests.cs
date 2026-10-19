using System.Text.Json;
using CoreSim.Analysis;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using Xunit;

namespace CoreSim.Tests;

[Trait("Shard","core")]
public sealed class ReplayGeometryTests
{
    private static string GeometryPath() {
        for(var dir=new DirectoryInfo(AppContext.BaseDirectory);dir is not null;dir=dir.Parent) {
            var path=Path.Combine(dir.FullName,"tools/replay-viewer/geometry.json");if(File.Exists(path))return path;
        }throw new FileNotFoundException("Missing checked-in replay geometry.");
    }
    [Fact]
    public void CheckedInGeometryExactlyMatchesDeterministicExportAndNativeTrackFrame() {
        Assert.Equal(ReplayGeometry.Serialize(),File.ReadAllText(GeometryPath()));
        var data=ReplayGeometry.Create();var frame=new TrackFrame(MatchedVenueProfiles.CreateMotoarenaStandingStartTrack());
        Assert.Equal(frame.LapMeters,data.LapMeters);Assert.Equal(frame.Embedding.FrameId,data.FrameId);
        Assert.Equal(1,data.InnerReferenceOffsetMeters);Assert.Equal(frame.Track.Segments.Count,data.Segments.Length);
        foreach(var segment in data.Segments) {
            var native=frame.Embedding.Segments[segment.Index];
            Assert.Equal(LaneModel.PhysicalTrackWidthMeters(native.SegmentType,frame.Track.Geometry),segment.PhysicalWidthMeters);
            foreach(var p in segment.Sections) {
                var actual=frame.At(p.ReferenceMeters,0).Position;
                Assert.InRange(Math.Abs(p.Reference.X-actual.X),0,2e-5);Assert.InRange(Math.Abs(p.Reference.Y-actual.Y),0,2e-5);
                Assert.Equal(p.Reference.X-p.Outward.X,p.Inner.X,10);Assert.Equal(p.Reference.Y-p.Outward.Y,p.Inner.Y,10);
                Assert.Equal(p.Reference.X+p.Outward.X*(segment.PhysicalWidthMeters-1),p.Outer.X,10);
                Assert.Equal(p.Reference.Y+p.Outward.Y*(segment.PhysicalWidthMeters-1),p.Outer.Y,10);
            }
            // Bound the visible chord error between the exported native points.
            for(var i=1;i<segment.Sections.Length;i++) {
                var a=segment.Sections[i-1];var b=segment.Sections[i];
                var middle=(a.ReferenceMeters+b.ReferenceMeters)/2;
                var expected=frame.At(middle,0).Position;
                Assert.InRange(Math.Abs((a.Reference.X+b.Reference.X)/2-expected.X),0,.001);
                Assert.InRange(Math.Abs((a.Reference.Y+b.Reference.Y)/2-expected.Y),0,.001);
                var outer=frame.At(middle,segment.PhysicalWidthMeters-1).Position;
                Assert.InRange(Math.Abs((a.Outer.X+b.Outer.X)/2-outer.X),0,.001);
                Assert.InRange(Math.Abs((a.Outer.Y+b.Outer.Y)/2-outer.Y),0,.001);
            }
        }
        using var loaded=JsonDocument.Parse(File.ReadAllText(GeometryPath()));
        Assert.Equal(data.Enums["channels"]["1"],loaded.RootElement.GetProperty("enums").GetProperty("channels").GetProperty("1").GetString());
    }
    [Fact]
    public void ExportedCapsulesAndEnumNamesAreTheNativeDefinitions() {
        var data=ReplayGeometry.Create();var footprint=BikeFootprint.Create(new(0,0),0,SpeedwayBikeDimensions.Reference);
        Assert.Equal(2.10,data.Dimensions.OverallMechanicalLengthMeters);Assert.Equal(.30,data.Dimensions.ChassisBodyWidthMeters);
        Assert.Equal(.80,data.Dimensions.HandlebarWidthMeters);Assert.Equal(.65,data.Dimensions.HandlebarLongitudinalOffsetMeters);
        foreach(var part in data.Capsules) {
            var native=part.Component=="Chassis"?footprint.Chassis:footprint.Handlebar;
            Assert.Equal(native.Start.X,part.Start.X);Assert.Equal(native.Start.Y,part.Start.Y);
            Assert.Equal(native.End.X,part.End.X);Assert.Equal(native.End.Y,part.End.Y);Assert.Equal(native.RadiusMeters,part.RadiusMeters);
        }
        Assert.Equal(nameof(ControlChannels.Line),data.Enums["channels"]["1"]);
        Assert.Equal(nameof(ControlChannels.Throttle),data.Enums["channels"]["2"]);
        Assert.Equal(nameof(RacingIntent.ContestInside),data.Enums["intent"]["5"]);
        Assert.Equal(nameof(RiderActivity.Finished),data.Enums["activity"]["3"]);
    }
}
