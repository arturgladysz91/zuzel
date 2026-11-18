using System.Diagnostics;
using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;
using WholeCorner171;

namespace SpeedwayHandling;

internal sealed record Window199(double Start,double End);
internal sealed class Corridor199(BendMap map,double yawLimit=.23)
{
    internal const double CoreStart=12, CoreEnd=54, OuterLimit=1.80, EntryLimit=2.20;
    internal const double CourseLimit=4*Math.PI/180, YawLimit=.23;
    internal Window199[] Windows {get;}=Enumerable.Range(0,3).SelectMany(lap=>Enumerable.Range(0,map.Count).Select(i=>
    {
        var visit=map.At(lap,i);
        var previous=i==0?map.At(lap-1,map.Count-1):map.At(lap,i-1);
        return new Window199(previous.Exit,visit.Entry);
    })).ToArray();

    internal string? Check(RiderView body,double outer)
    {
        var s=body.TotalReferenceMeters;
        foreach(var straight in Windows)
        {
            double phase=s-straight.Start;
            if(phase>=CoreStart && phase<=CoreEnd)
            {
                if(outer>OuterLimit)return "wide-position";
                if(body.Speed>3 && (Math.Abs(CourseError(body))>CourseLimit || Math.Abs(body.AngularVelocity)>yawLimit))
                    return "wide-direction";
            }
            if(s>=straight.End-1 && s<=straight.End+.25 && outer>EntryLimit)return "wide-entry";
        }
        return null;
    }

    internal static double CourseError(RiderView body) =>
        Math.Atan2(Vector2.Dot(body.Velocity,body.Road.Outward),Vector2.Dot(body.Velocity,body.Road.Tangent));
}

internal sealed record Knot199(Start197 Start,FreeRacingAction Action,double Outer,double Sweep)
{
    internal double S=>Start.State.Rider.TotalReferenceMeters;
}

internal sealed record Trial199(bool Legal,string Stop,double Finish,double Progress,double Sweep,
    double? FirstLap,double? SecondLap,Knot199[]? Knots,FreeRacingAction[]? Controls);
internal sealed record Winner199(string Name,string Variant,Trial199 Result,int Evaluations,
    double TrainingCpu,double WallSeconds);
internal sealed record Edit199(double Center,double Span,double Gas,double Steer,double Phase,bool Periodic,double OuterBias=0);
internal sealed record TrialLog199(int Round,int Id,string Family,bool Legal,string Stop,
    double Finish,double Progress,double Sweep,double? Lap1,double? Lap2,int PrefixTicks,Edit199[] Edits);
internal sealed record Selection199(int Round,int Id,double Finish,double Sweep,string Method);
internal sealed record Training199(Winner199 Winner,TrialLog199[] Candidates,Selection199[] History);
internal sealed record MarginTrial199(int Id,double Incumbent,TrialLog199 AtPhysicalLimit);

internal sealed class CyclePlanner199(Predictor198 model, double lapMeters,double goal, Corridor199 corridor,
    Technique200? technique=null)
{
    private const double Swing=.05;
    private const double Margin=.532;
    private const double SearchAhead=85;
    private readonly float wheelbase=TwoContactBikeParameters.Default.FrontAxleFromComMeters+
        TwoContactBikeParameters.Default.RearAxleFromComMeters;
    internal Trial199 Score200(Start197 start,Func<HandlingForecast,FreeRacingAction> policy,bool wide,
        bool record=false) => Evaluate(start,null,policy,0,wide,record);

    internal Trial199 Replay(Start197 start,FreeRacingAction[] tape,bool wide,bool record=true) =>
        Evaluate(start,i=>tape[Math.Min(i,tape.Length-1)],null,0,wide,record);

    internal Training199 Learn(string name,string variant,Start197 start,List<(string Name,FreeRacingAction[] Controls)> sources,
        int rounds,int count,int seedNumber,List<MarginTrial199>? marginAudit=null,bool explore=false)
    {
        bool wide=variant=="B";
        var cpu=Process.GetCurrentProcess().TotalProcessorTime;
        var timer=Stopwatch.StartNew();
        var random=new Random(seedNumber);
        var log=new List<TrialLog199>();
        var history=new List<Selection199>();
        Trial199? incumbent=null;
        int id=0;
        foreach(var source in sources)
        {
            var current=Replay(start,source.Controls,wide);
            log.Add(ToLog(0,id,source.Name,current,0,[]));
            if(incumbent is null || Better(current,incumbent))
            {
                incumbent=current;
                history.Add(new(0,id,current.Finish,current.Sweep,source.Name));
            }
            id++;
        }
        if(incumbent is not {Legal:true,Knots:not null})
        {
            var warm=incumbent??throw new InvalidOperationException("No initial proposal.");
            return new(new(name,variant,warm,id,(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,
                timer.Elapsed.TotalSeconds),log.ToArray(),history.ToArray());
        }

        double radius=1;
        int stale=0;
        for(int iteration=1;iteration<=rounds;iteration++)
        {
            var previous=incumbent;
            var knots=previous.Knots!;
            var edits=new Edit199[count][];
            var offsets=new int[count];
            for(int k=0;k<count;k++)
            {
                var set=new Edit199[k%11==0?2:1];
                for(int j=0;j<set.Length;j++)
                {
                    double progress=knots[4+random.Next(knots.Length-8)].S;
                    double span=9+random.NextDouble()*44;
                    bool repeated=k%7==0 && iteration>4;
                    double gas=(random.NextDouble()*2-1)*(k%3==0?.55:.28)*radius;
                    double steer=(random.NextDouble()*2-1)*(k%5==0?.033:.013)*radius;
                    double phase=(random.NextDouble()*2-1)*5.0*radius;
                    if(k%6==0)steer=phase=0;
                    if(k%6==1)gas=0;
                    if(explore && k%4==0)
                    {
                        var straight=corridor.Windows[random.Next(corridor.Windows.Length)];
                        progress=straight.End-55+random.NextDouble()*125;
                        span=14+random.NextDouble()*66;
                        // Coupled drive/steering around transitions plus an explicit throttle-only family.
                        if(k%8==0){gas=Math.Abs(gas)*1.65;steer=phase=0;}
                        else {steer*=1.45;phase*=2.15;}
                    }
                    set[j]=new(progress,span,gas,steer,phase,repeated);
                }
                edits[k]=set;
                offsets[k]=set.Any(x=>x.Periodic)?0:Lower(knots,set.Min(e=>e.Center-e.Span)-1.0);
            }
            var result=new Trial199[count];
            var audit=marginAudit is null?null:new Trial199?[count];
            Parallel.For(0,count,new ParallelOptions{MaxDegreeOfParallelism=6},k=>
            {
                var root=knots[offsets[k]].Start;
                var law=new Profile199(knots,edits[k],wheelbase,lapMeters);
                result[k]=Evaluate(root,null,law.At,offsets[k],wide,false,knots);
                if(audit is not null && result[k] is {Stop:"swept",Sweep:>=.50})
                    audit[k]=Evaluate(root,null,law.At,offsets[k],wide,false,knots,margin:.50);
            });
            int best=-1;
            for(int k=0;k<count;k++)
            {
                log.Add(ToLog(iteration,id+k,"feedback-edit",result[k],offsets[k],edits[k]));
                if(audit?[k] is {} physical)
                    marginAudit!.Add(new(id+k,previous.Finish,ToLog(iteration,id+k,"same-edit",physical,offsets[k],edits[k])));
                if(result[k].Legal && Better(result[k],best<0?previous:result[best]))best=k;
            }
            if(best>=0)
            {
                incumbent=Evaluate(knots[offsets[best]].Start,null,new Profile199(knots,edits[best],wheelbase,lapMeters).At,
                    offsets[best],wide,true,knots);
                if(Math.Abs(incumbent.Finish-result[best].Finish)>1e-8 || !incumbent.Legal)
                    throw new InvalidOperationException("The selected rollout changed when recording it.");
                history.Add(new(iteration,id+best,incumbent.Finish,incumbent.Sweep,"feedback-edit"));
                stale=0;
            }
            else if(++stale>=7){radius=Math.Max(.30,radius*.82);stale=0;}
            if(explore && iteration%34==0)radius=Math.Max(radius,.73);
            id+=count;
        }
        return new(new(name,variant,incumbent,id,(Process.GetCurrentProcess().TotalProcessorTime-cpu).TotalSeconds,
            timer.Elapsed.TotalSeconds),log.ToArray(),history.ToArray());
    }

    internal Trial199 Transfer(Start197 start,Trial199 learned,bool wide) =>
        Evaluate(start,null,new Profile199(learned.Knots??throw new InvalidOperationException("No learned state trace."),
            [],wheelbase,lapMeters).At,0,wide,true);

    internal (Training199 Training,TrialLog199[] Blends) Adapt(string name,string variant,Start197 root,
        FreeRacingAction[] safeControls,Trial199 prior,int rounds)
    {
        bool wide=variant=="B";
        var safe=Replay(root,safeControls,wide);
        if(!safe.Legal) throw new InvalidOperationException("Adaptation needs a physically confirmed reserve.");
        var basis=new Profile199(safe.Knots!,[],wheelbase,lapMeters);
        var target=new Profile199(prior.Knots!,[],wheelbase,lapMeters);
        var trials=(from offset in new[]{0,20,45,75}
                    from length in new[]{25,45,65,90}
                    from weight in new[]{.30,.55,.80,1.0}
                    select (offset,length,weight)).ToArray();
        FreeRacingAction Law(HandlingForecast v,(int offset,int length,double weight) recipe)
        {
            var first=basis.At(v);
            var later=target.At(v);
            double d=v.Rider.TotalReferenceMeters-root.State.Rider.TotalReferenceMeters;
            double u=Math.Clamp((d-recipe.offset)/recipe.length,0,1);
            double a=u*u*(3-2*u)*recipe.weight;
            return new((float)(first.SteeringRadians+a*(later.SteeringRadians-first.SteeringRadians)),
                (float)(first.Drive+a*(later.Drive-first.Drive)));
        }
        var scores=new Trial199[trials.Length];
        Parallel.For(0,trials.Length,new ParallelOptions{MaxDegreeOfParallelism=6},i=>
            scores[i]=Evaluate(root,null,v=>Law(v,trials[i]),0,wide,false));
        var warmed=new List<(string Name,FreeRacingAction[] Controls)>{("verified reserve",safeControls)};
        foreach(var i in Enumerable.Range(0,trials.Length).Where(i=>scores[i].Legal)
            .OrderBy(i=>scores[i].Finish).Take(3))
        {
            var forecast=Evaluate(root,null,v=>Law(v,trials[i]),0,wide,true);
            warmed.Add(($"warm blend {trials[i]}",forecast.Controls!));
        }
        return (Learn(name,variant,root,warmed,rounds,24,variant=="A"?199311:199422),
            scores.Select((trial,i)=>ToLog(0,i,$"retarget {trials[i]}",trial,0,[])).ToArray());
    }

    internal object NearBand(Trial199 learned,Window199 approach)
    {
        const double clearance=.02;
        double checkFrom=approach.End-18,checkTo=approach.End-8;
        var reference=learned.Knots??throw new InvalidOperationException("Lost reference B.");
        var origin=reference[Lower(reference,approach.Start-43)].Start;
        var baseline=Evaluate(origin,i=>reference[Math.Min(Lower(reference,origin.State.Rider.TotalReferenceMeters)+i,reference.Length-1)].Action,
            null,0,false,true);
        string? Gate(RiderView r,double outer)
        {
            if(r.TotalReferenceMeters<checkFrom || r.TotalReferenceMeters>checkTo)return null;
            if(outer<.10 || outer>.30) return "near-outer";
            return Math.Abs(Corridor199.CourseError(r))>Corridor199.CourseLimit?"near-course":null;
        }
        var candidates=(from width in new[]{40,52,66,82}
                        from push in new[]{.55,.68,.81,.94,1.07,1.20,1.33,1.46}
                        from steering in new[]{-.002,0,.002}
                        from gas in new[]{-.10,0d,.065}
                        select new Edit199(approach.End-13,width,gas,steering,0,false,push)).ToArray();
        var results=new Trial199[candidates.Length];
        Parallel.For(0,candidates.Length,new ParallelOptions{MaxDegreeOfParallelism=6},i=>
        {
            var law=new Profile199(reference,[candidates[i]],wheelbase,lapMeters);
            results[i]=Evaluate(origin,null,law.At,0,false,false,margin:clearance,overrideGate:Gate);
        });
        int best=Enumerable.Range(0,candidates.Length).Where(i=>results[i].Legal)
            .OrderBy(i=>results[i].Finish).DefaultIfEmpty(-1).First();
        Trial199? selected=null,split=null;
        if(best>=0)
        {
            var law=new Profile199(reference,[candidates[best]],wheelbase,lapMeters);
            selected=Evaluate(origin,null,law.At,0,false,true,margin:clearance,overrideGate:Gate);
            var commands=selected.Controls!;
            split=Evaluate(origin,i=>commands[Math.Min(i,commands.Length-1)],null,0,false,true,
                margin:clearance,overrideGate:Gate,step:.005);
        }
        Console.WriteLine($"Near {results.Count(x=>x.Legal)}/{results.Length}, best {best}, " +
            $"time {selected?.Finish-origin.State.Time:F6}, swept {selected?.Sweep:F6}, half-step {split?.Stop}");
        return new {Origin=origin,Goal=goal,Approach=approach,CheckFrom=checkFrom,CheckTo=checkTo,Clearance=clearance,
            Baseline=baseline,Winner=selected,FiveMilliseconds=split,Best=best,
            Trials=results.Select((x,i)=>ToLog(0,i,"near",x,0,[candidates[i]])).ToArray()};
    }

    internal object ParallelBand(Trial199 learned,Window199 approach,WideTrack194 field)
    {
        const double clearance=.02;
        double checkFrom=approach.End-18,checkTo=approach.End-8;
        var knots=learned.Knots!;
        int first=Lower(knots,approach.Start-15);
        var root=knots[first].Start;
        var basis=new Profile199(knots,[],wheelbase,lapMeters);
        var baseline=Evaluate(root,i=>knots[Math.Min(first+i,knots.Length-1)].Action,null,0,false,true);
        string? Gate(RiderView r,double gap) =>
            r.TotalReferenceMeters<checkFrom || r.TotalReferenceMeters>checkTo?null:
            gap is <.10 or >.30?"near-position":
            Math.Abs(Corridor199.CourseError(r))>Corridor199.CourseLimit?"near-course":null;
        var pool=(from target in new[]{.19,.22,.245,.265,.285}
                  from begin in new[]{-4,2,8}
                  from settled in new[]{25,30,35}
                  from release in new[]{0,6,12}
                  from throttle in new[]{0d,.07}
                  select (target,begin,settled,release,throttle)).ToArray();
        double NominalOffset(double s)
        {
            int j=Lower(knots,s),i=Math.Max(0,j-1);
            var a=knots[i]; var b=knots[j];
            double w=j==i?0:Math.Clamp((s-a.S)/Math.Max(.001,b.S-a.S),0,1);
            return (1-w)*a.Start.State.Rider.Road.OffsetMeters+w*b.Start.State.Rider.Road.OffsetMeters;
        }
        double Aligned(double gap)
        {
            double a=8,b=11;
            for(int q=0;q<20;q++)
            {
                double m=(a+b)/2;
                if(field.Boundary.Clearance(field.Pose((checkFrom+checkTo)/2,m)).Outer>=gap) a=m;
                else b=m;
            }
            return (a+b)/2;
        }
        var offsets=pool.Select(x=>x.target).Distinct().ToDictionary(x=>x,Aligned);
        FreeRacingAction Request(HandlingForecast now,(double target,int begin,int settled,int release,double throttle) trial)
        {
            const double delta=.8;
            var v=now.Rider;
            double s=v.TotalReferenceMeters;
            var unmodified=basis.At(now);
            double Blend(double x)
            {
                double p=x-approach.Start;
                double start=Math.Clamp((p-trial.begin)/(trial.settled-trial.begin),0,1);
                double stop=Math.Clamp((p-(checkTo-approach.Start))/Math.Max(1,approach.End+trial.release-checkTo),0,1);
                return start*start*(3-2*start)*(1-stop*stop*(3-2*stop));
            }
            double weight=Blend(s);
            if(weight<.001 || v.Speed<5)return unmodified;
            double Offset(double x)
            {
                double a=Blend(x);
                return (1-a)*NominalOffset(x)+a*offsets[trial.target];
            }
            Vector2 P(double x)=>field.Frame.At(x,Offset(x)).Position;
            double Course(double x)
            {
                var v=P(x+delta)-P(x-delta);
                return Math.Atan2(v.Y,v.X);
            }
            double course=Course(s);
            double k=BikeAngles.Wrap(Course(s+delta)-Course(s-delta))/
                Math.Max(.01,Vector2.Distance(P(s+delta),P(s-delta)));
            double error=v.Road.OffsetMeters-Offset(s);
            double slope=(Offset(s+delta)-Offset(s-delta))/(2*delta);
            double vOut=Vector2.Dot(v.Velocity,v.Road.Outward);
            double aim=course+Math.Atan((error+.23*(vOut-v.Speed*slope))/(4+.16*v.Speed));
            double feed=v.Speed*k;
            double yaw=feed+2.6*BikeAngles.Wrap(aim-Math.Atan2(v.Velocity.Y,v.Velocity.X))+
                .26*(feed-v.AngularVelocity)-1.25*v.SlideRadians;
            double steer=Math.Atan(wheelbase*yaw/v.Speed);
            double impact=Math.Min(1,3*weight);
            return new((float)Math.Clamp(unmodified.SteeringRadians+(steer-unmodified.SteeringRadians)*impact,-.30,.30),
                (float)Math.Clamp(unmodified.Drive+trial.throttle*weight,0,1));
        }
        var results=new Trial199[pool.Length];
        Parallel.For(0,pool.Length,new ParallelOptions{MaxDegreeOfParallelism=6},i=>
            results[i]=Evaluate(root,null,s=>Request(s,pool[i]),0,false,false,margin:clearance,overrideGate:Gate));
        var passed=Enumerable.Range(0,pool.Length).Where(i=>results[i].Legal).OrderBy(i=>results[i].Finish).ToArray();
        Trial199? selected=null,split=null;
        int best=-1;
        foreach(int i in passed.Take(8))
        {
            var target=Evaluate(root,null,s=>Request(s,pool[i]),0,false,true,margin:clearance,overrideGate:Gate);
            var tape=target.Controls!;
            var refined=Evaluate(root,j=>tape[Math.Min(j,tape.Length-1)],null,0,false,true,margin:clearance,overrideGate:Gate,step:.005);
            if(best<0 || refined.Legal)
            {
                best=i;selected=target;split=refined;
            }
            if(refined.Legal)break;
        }
        Console.WriteLine($"Parallel near {passed.Length}/{pool.Length}, best {best}: " +
            $"delta {selected?.Finish-baseline.Finish:F6}, swept {selected?.Sweep:F6}, five {split?.Stop}");
        return new {Root=root,Goal=goal,Approach=approach,CheckFrom=checkFrom,CheckTo=checkTo,Clearance=clearance,
            Baseline=baseline,Winner=selected,FiveMilliseconds=split,Best=best,
            Trials=results.Select((x,i)=>ToLog(0,i,$"parallel {pool[i]}",x,0,[])).ToArray()};
    }

    private Trial199 Evaluate(Start197 root,Func<int,FreeRacingAction>? tape,Func<HandlingForecast,FreeRacingAction>? law,int prefix,
        bool wide,bool capture,Knot199[]? old=null,double margin=Margin,
        Func<RiderView,double,string?>? overrideGate=null,double step=.01)
    {
        var kept=capture?new List<Knot199>():null;
        if(old is not null && prefix>0)kept?.AddRange(old.Take(prefix));
        double min=old is null||prefix==0?100:old[prefix-1].Sweep;
        double? lap1=FirstCrossing(old,prefix,lapMeters);
        double? lap2=FirstCrossing(old,prefix,2*lapMeters);
        double outer=old is not null?old[prefix].Outer:100;
        var gate=overrideGate??(wide?corridor.Check:(Func<RiderView,double,string?>?)null);
        var visual=wide && overrideGate is null?technique?.Begin():null;
        if(visual is not null && old is not null)
            for(int i=0;i<prefix;i++)visual.Observe(old[i].Start.State.Rider,old[i].Start.State.Time);
        var cursor=model.Begin(root,[],0,goal,margin,constraint:gate,stepSeconds:step);
        cursor.Advance(20,root.Review);
        var state=cursor.Checkpoint();
        min=Math.Min(cursor.Sweep,min);
        if(cursor.Outcome is not null)return Finish(cursor.Outcome,cursor);
        for(int ix=0;ix<Math.Ceiling(SearchAhead/.05);ix++)
        {
            if(visual?.Observe(state.State.Rider,state.State.Time) is {} styleStop)return Finish(styleStop,cursor);
            var action=tape is not null?tape(ix):law!(state.State);
            kept?.Add(new(state,action,outer,min));
            cursor=model.Begin(state,[action],0,goal,margin,constraint:gate,stepSeconds:step);
            cursor.Advance(22,state.State.Time+Swing);
            var after=cursor.Checkpoint();
            var sa=state.State.Rider.TotalReferenceMeters;
            var sb=after.State.Rider.TotalReferenceMeters;
            double Cross(double s)=>state.State.Time+(after.State.Time-state.State.Time)*(s-sa)/(sb-sa);
            if(lap1 is null && sa<lapMeters && sb>=lapMeters)lap1=Cross(lapMeters);
            if(lap2 is null && sa<2*lapMeters && sb>=2*lapMeters)lap2=Cross(2*lapMeters);
            state=after;
            min=Math.Min(min,cursor.Sweep);
            outer=cursor.Snapshot.Outer;
            if(cursor.Outcome is not null)return Finish(cursor.Outcome,cursor);
        }
        return Finish("deadline",cursor);
        Trial199 Finish(string outcome,Predictor198.Cursor198 c) =>
            new(outcome=="goal",outcome,outcome=="goal"?c.GoalTime:c.Snapshot.Time,c.Snapshot.S,min,
                lap1,lap2,kept?.ToArray(),kept?.Select(x=>x.Action).ToArray());
    }
    private static double? FirstCrossing(Knot199[]? rows,int stop,double s)
    {
        if(rows is null || stop<2 || rows[stop-1].S<s)return null;
        int j=Lower(rows,s);
        if(j<=0)return null;
        var a=rows[j-1];var b=rows[j];
        return a.Start.State.Time+(b.Start.State.Time-a.Start.State.Time)*(s-a.S)/(b.S-a.S);
    }
    private static bool Better(Trial199 a,Trial199 b) => a.Legal!=b.Legal?a.Legal:
        a.Legal?a.Finish<b.Finish-1e-6:a.Progress>b.Progress;
    private static TrialLog199 ToLog(int gen,int id,string family,Trial199 r,int prefix,Edit199[] edits)=>
        new(gen,id,family,r.Legal,r.Stop,r.Finish,r.Progress,r.Sweep,r.FirstLap,r.SecondLap,prefix,edits);

    private static int Lower(Knot199[] p,double s)
    {
        int a=0,b=p.Length-1;
        while(a<b)
        {
            int mid=(a+b)/2;
            if(p[mid].S<s)a=mid+1;else b=mid;
        }
        return a;
    }

    private sealed class Profile199(Knot199[] knots,Edit199[] edits,double wheelbase,double lap)
    {
        internal FreeRacingAction At(HandlingForecast current)
        {
            var body=current.Rider;
            double s=body.TotalReferenceMeters,gas=0,steer=0,phase=0,offset=0;
            foreach(var edit in edits)
            {
                double d=s-edit.Center;
                if(edit.Periodic)d=Math.IEEERemainder(d,lap);
                double weight=Math.Abs(d)<edit.Span?.5+.5*Math.Cos(Math.PI*d/edit.Span):0;
                gas+=weight*edit.Gas;steer+=weight*edit.Steer;phase+=weight*edit.Phase;offset+=weight*edit.OuterBias;
            }
            var actual=Route(s);
            // Phase perturbs feed-forward steering only. The accepted trajectory supplies the live-state feedback.
            var shifted=Math.Abs(phase)>1e-9?Route(s+phase):actual;
            var v=body.Speed;
            double vOut=Vector2.Dot(body.Velocity,body.Road.Outward);
            double target=actual.Offset+offset;
            double error=BikeAngles.Wrap(actual.Course-Math.Atan2(body.Velocity.Y,body.Velocity.X))+
                Math.Atan((body.Road.OffsetMeters-target+.24*(vOut-actual.Outward))/(7+.27*v));
            double feedback=2.25*error+.36*(actual.Yaw-body.AngularVelocity)+.6*(actual.Beta-body.SlideRadians);
            double request=shifted.Steer+steer+(v<4?0:Math.Atan(wheelbase*feedback/v));
            double throttle=actual.Gas+gas+.11*(actual.Speed-v);
            double limit=TwoContactBikeParameters.Default.MaximumSteeringRadians;
            return new((float)Math.Clamp(request,-limit,limit),(float)Math.Clamp(throttle,0,1));
        }
        private Route199 Route(double s)
        {
            int j=Lower(knots,s),i=Math.Max(0,j-1);
            if(j==i)return From(knots[j]);
            var a=knots[i];var b=knots[j];
            double u=Math.Clamp((s-a.S)/Math.Max(.001,b.S-a.S),0,1);
            var x=From(a);var y=From(b);
            double Blend(double s0,double s1)=>s0+u*(s1-s0);
            return new(Blend(x.Offset,y.Offset),BikeAngles.Wrap(x.Course+u*BikeAngles.Wrap(y.Course-x.Course)),
                Blend(x.Yaw,y.Yaw),Blend(x.Beta,y.Beta),Blend(x.Speed,y.Speed),Blend(x.Outward,y.Outward),
                Blend(x.Gas,y.Gas),Blend(x.Steer,y.Steer));
        }
        private static Route199 From(Knot199 k)
        {
            var r=k.Start.State.Rider;
            return new(r.Road.OffsetMeters,Math.Atan2(r.Velocity.Y,r.Velocity.X),r.AngularVelocity,r.SlideRadians,
                r.Speed,Vector2.Dot(r.Velocity,r.Road.Outward),k.Action.Drive,k.Action.SteeringRadians);
        }
        private readonly record struct Route199(double Offset,double Course,double Yaw,double Beta,double Speed,
            double Outward,double Gas,double Steer);
    }
}
