using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using Safety153;
using WholeCorner171;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record Trial194(RiderSpec Spec,StartSample144 Start, LeanSample Roll,
        FreeRacingAction Active, BendVisit Bend, string Lane);

    private static void Family194(string source, string output)
    {
        using var file=File.OpenRead(source);
        using var gzip=new GZipStream(file,CompressionMode.Decompress);
        using var document=JsonDocument.Parse(gzip);
        var root=document.RootElement;
        var field=new WideTrack194(Track);
        var choices=new List<Trial194>();
        int rider=0;
        foreach(var record in root.GetProperty("Run").GetProperty("Riders").EnumerateArray())
        {
            var catalog=record.GetProperty("Samples").Deserialize<StartSample144[]>(Json)!;
            var rolls=root.GetProperty("Lean")[rider].Deserialize<LeanSample[]>(Json)!;
            var commands=record.GetProperty("Activations").Deserialize<FreeRacingCommand[]>(Json)!;
            var gate=record.GetProperty("Gate").Deserialize<StartingGate>(Json);
            double ratio=10-2*record.GetProperty("Spec").GetProperty("Setup").GetProperty("Gearing").GetDouble();
            var spec=Spec193(gate,++rider,ratio);
            foreach(int lap in Enumerable.Range(0,3))
            foreach(int bendId in Enumerable.Range(0,field.Bends.Count))
            {
                var bend=field.Bends.At(lap,bendId);
                double start=bend.Entry+.36*bend.Length;
                int index=Array.FindIndex(catalog,s=>s.Input.Rider.TotalReferenceMeters>=start);
                if (index<0) continue;
                var sample=catalog[index];
                double offset=sample.Input.Rider.Road.OffsetMeters;
                var active=commands.LastOrDefault(c=>c.ActivationAt<=sample.Input.Time+1e-9)?.Action??default;
                string lane=offset<2.4?"inside":offset<6.75?"middle":"wide";
                choices.Add(new(spec,sample,rolls[index],active,bend,lane));
            }
        }
        if(choices.Select(t=>t.Lane).Distinct().Count()!=3)
            throw new InvalidOperationException("The archive must contain three different apex corridors.");
        var trial=choices.GroupBy(c=>c.Lane).SelectMany(g=>
        {
            var ordered=g.OrderBy(c=>c.Start.Input.Rider.Speed).ToArray();
            return new[] {ordered[0],ordered[^1]};
        }).ToArray();
        var evaluations=trial.SelectMany(c=>c.Lane=="middle"?new[]{Window194(field,c,false),Window194(field,c,true)}:
            new[]{Window194(field,c,false)}).ToArray();
        Save(output,new{Source=Path.GetFileName(source),Cases=evaluations});
        Console.WriteLine($"#194 saved {evaluations.Length} H25-only windows from accepted states and corridor occupancy tests.");
    }

    private static object Window194(WideTrack194 field,Trial194 trial,bool temporarilyBlocked)
    {
        var sample=trial.Start;
        var spec=trial.Spec;
        var v=sample.Input.Rider;
        var bend=trial.Bend;
        var mind=new RaceMind194(field,Surface,Physics,spec,Handling,Timing,Mass,Inertia);
        var initial=new HandlingForecast(v,sample.Input.Time,sample.Input.Omega,sample.Input.Steering,
            trial.Roll.ActualRadians,sample.CrankOmega);
        var surface=Surface.SampleSurface(0,2);
        var state=initial;
        var active=trial.Active;
        var pending=sample.Input.Pending.Where(c=>c.ActivationAt>state.Time+1e-9).ToArray();
        double review=Math.Max(state.Time,sample.Input.NextReview);
        var points=new List<object>();
        var margin=field.Boundary.Clearance(v);
        double swept=10;
        double forceUse=0;
        double lastTarget=double.NaN;
        double? established=null;
        int changes=0;
        bool excluded=false,resumed=false;
        while(state.Time-initial.Time<10 && state.Rider.TotalReferenceMeters<bend.Exit+38)
        {
            if(pending.Length>0 && pending[0].ActivationAt<=state.Time+1e-8)
            {
                active=pending[0].Action;
                pending=pending[1..];
            }
            if(state.Time>=review-1e-8)
            {
                bool blocked=temporarilyBlocked && state.Time-initial.Time<.85;
                // In a separate occupancy probe, one observed rider covers the
                // outside corridor before pulling clear. No collision model is simulated.
                RiderView[] world=blocked?[state.Rider,field.Pose(state.Rider.TotalReferenceMeters+.5,
                    field.WideOffset(bend)-.2) with {Id=99,Velocity=state.Rider.Road.Tangent*state.Rider.Speed}]:[state.Rider];
                var input=new FreeRacingInput(state.Rider with {Drive=active.Drive},state.Time,state.Wheel,state.Steer,
                    active,pending,review);
                var choice=mind.Choose(input,new(omega:state.Shaft),state.Lean,new(state.Time,Array.AsReadOnly(world)));
                var entry=mind.Journal[^1];
                pending=[..pending,new(state.Time,Timing.Due(state.Time,RiderController.ReactionSeconds(spec),
                    pending.LastOrDefault()),choice.Action)];
                if(Math.Abs(entry.Target-lastTarget)>.25) changes++;
                lastTarget=entry.Target;
                if(blocked && entry.Target<field.WideOffset(bend)-.50) excluded=true;
                if(!blocked && excluded && entry.Target>field.WideOffset(bend)-.25) resumed=true;
                double course=BikeAngles.Wrap(state.Rider.Heading-state.Rider.SlideRadians);
                double angle=BikeAngles.Wrap(course-state.Rider.Road.Heading);
                if(state.Rider.TotalReferenceMeters>=bend.Exit && margin.Outer is >=.8 and <=1.5 &&
                    Math.Abs(angle)<.07 && Math.Abs(state.Rider.AngularVelocity)<.16)
                    established??=state.Rider.TotalReferenceMeters-bend.Exit;
                points.Add(new { state.Time,S=state.Rider.TotalReferenceMeters,offset=state.Rider.Road.OffsetMeters,
                    state.Lean,state.Rider.Speed,course,angle,state.Rider.SlideRadians,state.Rider.AngularVelocity,
                    margin.Outer,entry.Target,entry.Reason,entry.Wide,entry.Motion });
                review+=Timing.Period(review);
            }
            double dt=Math.Min(.01,review-state.Time);
            if(pending.Length>0) dt=Math.Min(dt,pending[0].ActivationAt-state.Time);
            if(dt<=1e-8) continue;
            var before=state;
            state=HandlingPreview.Advance(state,dt,active,[],surface,Handling,spec,Physics,Frame,Mass,Inertia,
                LaunchParameters144.Default,(_,power)=>forceUse=Math.Max(forceUse,power.Wheel.RearUtilization));
            var next=field.Boundary.Clearance(state.Rider);
            swept=Math.Min(swept,Clearance153.SweepLowerBound(before.Rider,state.Rider,dt,margin,next));
            margin=next;
            if(swept<.5 || Math.Abs(state.Rider.SlideRadians)>Math.PI/4 || forceUse>1.000001) break;
        }
        return new {trial.Lane,TemporarilyBlocked=temporarilyBlocked,Excluded=excluded,Resumed=resumed,
            Spec=spec.Profile.Id, Ratio=10-2*spec.Setup.Gearing, Bend=bend, Initial=initial,EstablishedAfterExitMeters=established,
            FinalS=state.Rider.TotalReferenceMeters,Swept=swept,PeakRearUse=forceUse,TargetChanges=changes,Trace=points};
    }
}

