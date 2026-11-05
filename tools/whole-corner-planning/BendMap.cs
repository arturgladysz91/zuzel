using CoreSim;
using SharedWorldSpeedway;

namespace WholeCorner171;

internal readonly record struct BendVisit(int Lap, int Index, double Entry, double Exit, double NextEntry)
{
    public double Length => Exit - Entry;
}

internal sealed class BendMap
{
    private readonly TrackFrame frame;
    private readonly LogicalCorner[] corners;

    public BendMap(TrackFrame frame)
    {
        this.frame = frame;
        corners = frame.Track.CornerTopology.Corners.ToArray();
    }

    public int Count => corners.Length;

    public BendVisit At(int lap, int index)
    {
        var nextIndex = (index + 1) % Count;
        double origin = lap * frame.LapMeters;
        return new(lap, index,
            origin + frame.SegmentStartMeters(corners[index].StartSegmentIndex),
            origin + frame.SegmentStartMeters(corners[index].EndSegmentIndex + 1),
            origin + frame.SegmentStartMeters(corners[nextIndex].StartSegmentIndex) +
                (nextIndex == 0 ? frame.LapMeters : 0));
    }

    public (int Lap, double Local, RoadPoint Road, BendVisit Next, BendVisit? Current) Locate(double total)
    {
        int lap = checked((int)Math.Floor(total / frame.LapMeters));
        double local = total - lap * frame.LapMeters;
        var road = frame.RoadAt(local, 0);
        int active = Array.FindIndex(corners, corner => corner.ContainsSegment(road.Segment));
        BendVisit? current = active < 0 ? null : At(lap, active);
        int next = Array.FindIndex(corners, corner => frame.SegmentStartMeters(corner.StartSegmentIndex) > local);
        return (lap, local, road, next < 0 ? At(lap + 1, 0) : At(lap, next), current);
    }

    public double[] Sections(BendVisit visit)
    {
        var corner = corners[visit.Index];
        return Enumerable.Range(corner.StartSegmentIndex, corner.SegmentCount + 1)
            .Select(i => visit.Lap * frame.LapMeters + frame.SegmentStartMeters(i)).ToArray();
    }
}

