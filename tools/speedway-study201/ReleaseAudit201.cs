using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private static void ReleaseAudit201(string workspace,string output)
    {
        var source=Read199<Archive198>(Path.Combine(workspace,"outputs","badanie200","C-native.json.gz"));
        var rider=source.Run.Riders.Single();
        var model=Model199(rider.Spec);
        var requests=rider.Samples.Where(s=>Math.Abs(s.Input.Time/.05-Math.Round(s.Input.Time/.05))<1e-7).ToArray();
        double previous=-1;
        var events=new List<object>();
        var counts=new Dictionary<string,int>();
        foreach(var (sample,index) in requests.Select((x,i)=>(x,i)))
        {
            if(index<1 || index+5>=requests.Length || sample.Input.Time-previous<.8 ||
                requests[index-1].RequestedDrive<.60 ||
                requests.Skip(index).Take(6).Min(x=>x.RequestedDrive)>requests[index-1].RequestedDrive-.10)
                continue;
            previous=sample.Input.Time;
            int at=Array.IndexOf(rider.Samples,sample);
            var start=Sample199(sample,source.Lean[0][at]);
            var tape=Tape199(rider,start.Review);
            var baseline=model.Begin(start,tape,0,Finish199,.50,constraint:Corridor().Check);
            baseline.Advance(10000);
            var probes=new List<object>();
            string cause="A-sampled-physical-failure";
            foreach(int ticks in new[]{5,10})
            {
                var modified=tape.ToArray();
                for(int i=0;i<ticks;i++)modified[i]=modified[i] with{Drive=Math.Min(1,modified[i].Drive+.20f)};
                var historical=model.Begin(start,modified,0,Finish199,.50,constraint:Corridor().Check);
                historical.Advance(10000);
                var physical=model.Begin(start,modified,0,Finish199,.50);
                physical.Advance(10000);
                var reserve=model.Begin(start,modified,0,Finish199,.532,constraint:Corridor().Check);
                reserve.Advance(10000);
                var selected=historical.Reached && baseline.Reached &&
                    historical.GoalTime<baseline.GoalTime-1e-5?"E-candidate-not-found":
                    historical.Reached && !reserve.Reached?"C-reserve":
                    !historical.Reached && physical.Reached?"H-style-yaw-position":
                    historical.Reached?"A-sampled-slower":"A-sampled-physical-failure";
                if(selected=="E-candidate-not-found" || cause=="A-sampled-physical-failure")cause=selected;
                probes.Add(new{HoldSeconds=ticks*.05,Category=selected,Historical=new{historical.Outcome,historical.GoalTime,
                    historical.Sweep,End=historical.Snapshot},Physical=new{physical.Outcome,physical.GoalTime,
                    physical.Sweep,End=physical.Snapshot},Reserve=new{reserve.Outcome,reserve.GoalTime}});
            }
            counts[cause]=counts.GetValueOrDefault(cause)+1;
            events.Add(new{At=sample.Input.Time,S=sample.Input.Rider.TotalReferenceMeters,
                Speed=sample.Input.Rider.Speed*3.6,GasBefore=requests[index-1].RequestedDrive,
                GasAt=sample.RequestedDrive,Category=cause,Start=start,
                Baseline=new{baseline.Outcome,baseline.GoalTime},Probes=probes});
        }
        Save(Path.Combine(output,"release-audit.json.gz"),new{Definition=">=10 percentage points over 0.25 s, prior gas >=60%, 0.8 s episode separation",
            Counts=counts,Events=events,CategoriesNotObserved=new[]{"B","D","F","G"},
            Scope="Two throttle interventions with fixed later steering. Physical impossibility outside these candidates is unproven."},true);
    }
}
