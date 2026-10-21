using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using CoreSim;

namespace SharedWorldSpeedway;

/// <summary>LIGHT: saved-state work, one short run, optional second bend. No new solver.</summary>
public static class DynamicAxleLoadAudit
{
    public const string BaseHead="fc5d184e95fa3d6d12c27abd59ab8de7e2630bee";
    public static WorldParameters Parameters()=>PhysicalSteeringAudit.Parameters() with{EnableDynamicAxleLoad=true,AxleLoadHeightMeters=.55};
    public static Dictionary<string,double>[] ReadSaved(string name="eight-laps")
    {
        using var file=File.OpenRead(PhysicalSteeringAudit.PreviousPath("../physical-steering-control/"+name+".csv.gz"));
        using var gzip=new GZipStream(file,CompressionMode.Decompress);using var reader=new StreamReader(gzip);var keys=reader.ReadLine()!.Split(',');var rows=new List<Dictionary<string,double>>();
        while(reader.ReadLine() is { } line)rows.Add(line.Split(',').Select((v,i)=>(keys[i],double.Parse(v,CultureInfo.InvariantCulture))).ToDictionary(x=>x.Item1,x=>x.Item2));
        return rows.ToArray();
    }
    public static void Run(string dir,string stage)
    {
        Directory.CreateDirectory(dir);
        if(stage=="freeze"){
            InverseFeasibilityAudit.Save(dir,"freeze.json",new{BaseHead,Parameters=Parameters(),Geometry=TwoContactBikeParameters.Default,HeightProvisional=true,
                Rule="Original #113 controller, contacts, B, engine, INNER and start. Axle share once per accepted state from previous native dv projected on current body axis, initial ax=0. One first bend; second only if safe and beneficial. Stop out of nonnegative loads; never clamp or lift. No new body steps/queries due to transfer."});return;
        }
        using var frozen=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"freeze.json")));
        if(frozen.RootElement.GetProperty("Parameters").GetRawText()!=JsonSerializer.Serialize(Parameters(),InverseFeasibilityAudit.Json))throw new InvalidOperationException("Frozen axle-load inputs changed.");
        if(stage is "performance" or "contact-root-work"){Performance(dir,stage=="contact-root-work");return;}
        if(stage is not ("first-bend" or "second-bend"))throw new ArgumentException("Use freeze, performance, first-bend, second-bend.");
        using var gate=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,stage=="first-bend"?"existing-states.json":"first-bend-gate.json")));
        if(!gate.RootElement.GetProperty(stage=="first-bend"?"RunFirstBend":"RunSecondBend").GetBoolean())throw new InvalidOperationException("Previous LIGHT gate closed.");
        var frame=AuthoredRacingPath.Crossover(false).Frame;
        var capture=TwoContactBikeAudit.Execute(new(stage,Seconds:stage=="first-bend"?12:25,Drive:true),track:true,stopUnsafe:true,parameters:Parameters(),stopReference:frame.SegmentStartMeters(stage=="first-bend"?4:8)+1.05);
        TwoContactBikeAudit.Export(dir,stage,capture);
        using var writer=InverseFeasibilityAudit.Csv(dir,stage+"-load.csv","t,ax,front_fraction,rear_fraction,front_normal,rear_normal,front_budget,rear_budget,queries");
        foreach(var t in capture.Ticks){var f=t.Forces;var load=f.AxleLoad!.Value;var rear=f.Budget*load.RearFraction;
            InverseFeasibilityAudit.Row(writer,t.Time,load.LongitudinalAcceleration,load.FrontFraction,load.RearFraction,capture.Mass*DynamicAxleLoad.GravityMetersPerSecondSquared*load.FrontFraction,
                capture.Mass*DynamicAxleLoad.GravityMetersPerSecondSquared*load.RearFraction,f.Budget-rear,rear,f.SteeringControl!.ContactQueries);}
    }
    private static void Performance(string dir,bool rootWorkOnly)
    {
        var rows=ReadSaved();var path=AuthoredRacingPath.Crossover(false);var frame=path.Frame;var spec=RacingLineRootCauseAudit.Rider(path);var p=PhysicalSteeringAudit.Parameters();var on=Parameters();
        var surface=TrackState.CreateDefault(frame.Track,new(1,0,.5f)).Snapshot();var bike=TwoContactBikeParameters.Default;
        var states=rows.Select(r=>PhysicalSteeringAudit.View(r,frame)).ToArray();var guidance=states.Select(v=>path.Guidance(v,p,true)).ToArray();
        var seconds=rows.Select(r=>(float)r["dt"]).ToArray();var times=rows.Select(r=>r["t"]).ToArray();var angles=rows.Select(r=>(float)r["steer_request"]).ToArray();
        var wheels=rows.Select(r=>new RearWheelState(r["omega_before"]*.33)).ToArray();var steering=rows.Select(r=>new TwoContactBikeState((float)r["steer_before"])).ToArray();
        double checksum=0;var offTimes=new List<double>();var onTimes=new List<double>();
        double Batch(bool enabled,int length){var start=Stopwatch.GetTimestamp();for(var i=0;i<length;i++){
            if(enabled)steering[i].AxleLoad=i==0?DynamicAxleLoad.AtAcceleration(0,bike):DynamicAxleLoad.Accepted(states[i-1],states[i],seconds[i-1],bike);
            var f=TwoContactBikeDynamics.Forces(states[i],spec,frame,surface,enabled?on:p,times[i],147,wheels[i],steering[i],seconds[i],guidance[i],angles[i]);
            checksum+=f.NetTorque;
        }return Stopwatch.GetElapsedTime(start).TotalMilliseconds;}
        if(rootWorkOnly){
            Batch(false,rows.Length);var off=wheels.GroupBy(w=>w.Prepared.RootIterations).ToDictionary(g=>g.Key,g=>g.Count());
            Batch(true,rows.Length);var enabled=wheels.GroupBy(w=>w.Prepared.RootIterations).ToDictionary(g=>g.Key,g=>g.Count());
            InverseFeasibilityAudit.Save(dir,"contact-root-work.json",new{BodySteps=0,ContactsPerVariant=rows.Length,OffRootIterations=off,OnRootIterations=enabled,
                OffTotalIterations=off.Sum(pair=>pair.Key*pair.Value),OnTotalIterations=enabled.Sum(pair=>pair.Key*pair.Value),
                Rule="One extra fixed-state pass per variant solely because matched timings differ. Existing unique wheel root, no added solve; count its data-dependent Newton iterations."});return;
        }
        Batch(false,rows.Length);Batch(true,rows.Length);
        for(var round=0;round<3;round++)if(round%2==0){offTimes.Add(Batch(false,rows.Length));onTimes.Add(Batch(true,rows.Length));}else{onTimes.Add(Batch(true,rows.Length));offTimes.Add(Batch(false,rows.Length));}
        double Loads(){var start=Stopwatch.GetTimestamp();for(var i=0;i<states.Length;i++)checksum+=(i==0?DynamicAxleLoad.AtAcceleration(0,bike):DynamicAxleLoad.Accepted(states[i-1],states[i],seconds[i-1],bike)).RearFraction;
            return Stopwatch.GetElapsedTime(start).TotalMilliseconds;}
        Loads();var loadTimes=Enumerable.Range(0,3).Select(_=>Loads()).ToArray();var loadOnly=loadTimes.Order().ElementAt(1);
        InverseFeasibilityAudit.Save(dir,"performance-matched.json",new{SavedStates=rows.Length,BodySteps=0,ContactEvaluationsPerVariant=rows.Length*4,ExtraRearSolvesPerContact=0,OffMilliseconds=offTimes,OnMilliseconds=onTimes,LoadOnlySamples=loadTimes,
            MedianOffMilliseconds=offTimes.Order().ElementAt(1),MedianOnMilliseconds=onTimes.Order().ElementAt(1),LoadOnlyMilliseconds=loadOnly,LoadOnlyPercentOfOff=100*loadOnly/offTimes.Order().ElementAt(1),Checksum=checksum,
            Rule="Same full #113 states and actual steering; warm one full batch then 3 interleaved batches. One original contact solve per state; include ONE load calculation ON. Native h/t/steering decoded before timing (no CSV/dictionary overhead). Explicit saved steering isolates load overhead from changed #113 inverse-root branch counts. No worlds or commits. Timing diagnostic, not a hard CI threshold."});
    }
}
