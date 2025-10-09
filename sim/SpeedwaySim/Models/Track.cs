using System;
using System.Collections.Generic;
using System.Linq;

namespace SpeedwaySim.Models;

public sealed class Track
{
    public Track(string name, double lapLength, IReadOnlyList<TrackSegment> segments, IReadOnlyList<RacingLine> lines)
    {
        Name = name;
        LapLength = lapLength;
        Segments = segments;
        Lines = lines;
    }

    public string Name { get; }

    public double LapLength { get; }

    public IReadOnlyList<TrackSegment> Segments { get; }

    public IReadOnlyList<RacingLine> Lines { get; }

    public RacingLine GetLineOrThrow(int index)
    {
        var line = Lines.FirstOrDefault(l => l.Index == index);
        return line ?? throw new ArgumentOutOfRangeException(nameof(index), $"Line {index} is not defined on track {Name}");
    }
}
