namespace SpeedwayHandling;

internal static partial class Study191
{
    private static void Throttle200(string workspace,string output)
    {
        var results=new List<object>();
        var driveProbes=new List<object>();
        foreach(var (label,path) in new[]{
            ("199-C",Path.Combine(workspace,"outputs","badanie199","B-smooth-native.json.gz")),
            ("200-C",Path.Combine(output,"C-native.json.gz"))})
        {
            var saved=Read199<Archive198>(path);
            var driver=saved.Run.Riders.Single();
            var model=Model199(driver.Spec);
            var review=driver.Activations;
            for(int lap=0;lap<Laps;lap++)
            for(int corner=0;corner<Map199.Count;corner++)
            {
                var b=Map199.At(lap,corner);
                double from=b.Entry-54,to=Math.Min(b.Exit+20,Finish199);
                foreach(double meters in new[]{6d,12,24,38})
                {
                    double at=b.Entry-62+meters;
                    if(at<2)continue;
                    int tick=Array.FindIndex(driver.Samples,x=>x.Input.Rider.TotalReferenceMeters>=at &&
                        Math.Abs(x.Input.Time/Predictor198.Review-Math.Round(x.Input.Time/Predictor198.Review))<1e-6);
                    if(tick<0)continue;
                    var sampleAt=driver.Samples[tick];
                    var root=Sample199(sampleAt,saved.Lean[0][tick]);
                    var commands=Tape199(driver,root.Review);
                    var tests=new List<object>();
                    foreach(var (delta,steps) in new[]{(0f,0),(.20f,4),(.40f,8)})
                    {
                        var tape=commands.ToArray();
                        for(int n=0;n<steps;n++)tape[n]=tape[n] with{Drive=Math.Min(1,tape[n].Drive+delta)};
                        var probe=model.Begin(root,tape,0,to,.50,constraint:Corridor().Check);
                        probe.Advance(3000);
                        tests.Add(new{AddedGas=delta,Seconds=steps*.05,probe.Outcome,probe.Reached,probe.GoalTime,
                            probe.Sweep,probe.MaxBeta,End=probe.Snapshot});
                    }
                    driveProbes.Add(new{Label=label,Lap=lap+1,Corner=corner+1,Meters=meters,
                        At=sampleAt.Input.Rider.TotalReferenceMeters,Speed=sampleAt.Input.Rider.Speed,
                        State=root,Target=to,Tests=tests});
                }
                int i=-1,j=-1;
                for(int k=1;k+5<review.Length && i<0;k++)
                {
                    var prior=review[k-1].Action.Drive;
                    if(prior<.45f || prior-review[k].Action.Drive<.012f ||
                        Enumerable.Range(k,6).Min(n=>review[n].Action.Drive)>prior-.11f)continue;
                    j=Array.FindIndex(driver.Samples,x=>Math.Abs(x.Input.Time-review[k].ObservedAt)<1e-8);
                    if(j<0)continue;
                    var s=driver.Samples[j].Input.Rider.TotalReferenceMeters;
                    if(s>=Math.Max(0,from) && s<=b.Entry+1)i=k;
                }
                if(i<0){results.Add(new{Label=label,Lap=lap+1,Corner=corner+1,NoMaterialRelease=true});continue;}
                var sample=driver.Samples[j];
                var initial=Sample199(sample,saved.Lean[0][j]);
                var controls=Tape199(driver,review[i].ObservedAt);
                var variants=new List<object>();
                foreach(int ticks in new[]{0,2,5,8})
                {
                    var policy=controls.ToArray();
                    for(int k=0;k<ticks;k++)policy[k]=policy[k] with {Drive=Math.Max(policy[k].Drive,review[i-1].Action.Drive)};
                    var cursor=model.Begin(initial,policy,0,to,.50,constraint:Corridor().Check);
                    cursor.Advance(3500);
                    variants.Add(new{HoldSeconds=ticks*.05,cursor.Outcome,cursor.Reached,cursor.GoalTime,
                        cursor.Sweep,End=cursor.Snapshot,cursor.MaxBeta,cursor.PeakForce});
                }
                results.Add(new{Label=label,Lap=lap+1,Corner=corner+1,Start=initial,To=to,
                    At=sample.Input.Rider.TotalReferenceMeters,PriorGas=review[i-1].Action.Drive,
                    ReducedGas=review[i].Action.Drive,Variants=variants});
            }
        }
        Save(Path.Combine(output,"audyt-gazu.json.gz"),results,true);
        Save(Path.Combine(output,"audyt-napedu-na-prostych.json.gz"),driveProbes,true);
    }
}
