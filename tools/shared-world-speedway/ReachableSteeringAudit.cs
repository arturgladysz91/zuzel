using System.Text.Json;

namespace SharedWorldSpeedway;

/// <summary>#120 single comparable trial, gated on complete saved-state diagnosis.</summary>
public static class ReachableSteeringAudit
{
    public const string BaseHead="8a218fc8cb8be3460c8e42116789ece8ff576498";
    public static void Run(string dir,string stage)
    {
        Directory.CreateDirectory(dir);
        using var replay=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"replay.json")));
        if(replay.RootElement.GetProperty("MaximumForceReplayDifferenceNewtons").GetDouble()!=0
            ||replay.RootElement.GetProperty("States").GetInt32()!=1017)throw new InvalidOperationException("Complete exact historical replay first.");
        var p=CornerEntryAudit.Parameters() with{EnableReachableTravelDirectionSteering=true};
        if(stage=="freeze"){
            InverseFeasibilityAudit.Save(dir,"protocol.json",new{BaseHead,Parameters=p,Start=CornerEntryAudit.Rider(),CornerEntryAudit.PartialDrive,CornerEntryAudit.WideOffset,
                MaximumNewTrials=1,Rule="Same #119 combined initial state, reaction deadline, drive0.15 and target5; change only steering search domain and physical mean-angle evaluation. Same #117 score and horizons, 51 queries. No tuning after outcome. Reachable search may sacrifice future recovery by preferring an immediate local minimum; test this risk explicitly. Stop first bend, boundary, beta45 or energy violation. OFF remains original #117."});return;
        }
        if(stage!="trial")throw new ArgumentException("freeze or trial");
        using var frozen=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"protocol.json")));
        if(frozen.RootElement.GetProperty("Parameters").GetRawText()!=JsonSerializer.Serialize(p,InverseFeasibilityAudit.Json))throw new InvalidOperationException("Frozen parameters changed.");
        if(File.Exists(Path.Combine(dir,"reachable.json")))throw new InvalidOperationException("One native comparison only; saved trial exists.");
        var c=CornerEntryAudit.Execute("reachable",5,.15f,reachableSteering:true);
        TwoContactBikeAudit.Export(dir,"reachable",c);CornerEntryAudit.Export(dir,"reachable",c);
        using var writer=InverseFeasibilityAudit.Csv(dir,"reachable-geometry.csv","t,offset,local_lane_curvature");
        var frame=RacingLineRootCauseAudit.Frame();
        foreach(var t in c.Ticks){var v=t.Before;var k=frame.Shape!.At(v.TotalReferenceMeters).Curvature;
            InverseFeasibilityAudit.Row(writer,t.Time,v.Road.OffsetMeters,k/(1+k*v.Road.OffsetMeters));}
        Console.WriteLine(JsonSerializer.Serialize(TwoContactBikeAudit.Summary(c),InverseFeasibilityAudit.Json));
    }
}
