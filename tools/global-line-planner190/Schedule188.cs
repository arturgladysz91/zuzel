using System.Numerics;
using CoreSim;
using CoreSim.PhysicalSpace;
using Cyclic172;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal sealed record Schedule188(double Branch, double ReviewSeconds, double ReactionSeconds)
{
    internal double Period(double time) => time >= Branch-1e-9 ? ReviewSeconds : FreeLearningController.ReviewSeconds;
    internal double Delay(double time, double original) => time >= Branch-1e-9 ? ReactionSeconds : original;

    internal double Due(double time, double original, FreeRacingCommand? tail) =>
        Math.Max(time+Delay(time,original),tail?.ActivationAt ?? time);

    internal double Horizon(FreeRacingInput input,double original)
    {
        double delay=Delay(input.Time,original);
        double tail=input.Pending.LastOrDefault()?.ActivationAt ?? input.Time;
        return tail>input.Time+delay ? tail-input.Time : delay;
    }

    internal static void Activate(List<FreeRacingCommand> pending,List<FreeRacingCommand> log,
        double time,ref FreeRacingAction active)
    {
        while (pending.Count>0 && pending[0].ActivationAt<=time+1e-10)
        {
            var command=pending[0];
            pending.RemoveAt(0);
            active=command.Action;
            log.Add(command);
        }
    }

    internal static void Check()
    {
        foreach (var config in new[] { new Schedule188(0,.05,.05),new Schedule188(0,.05,.24),new Schedule188(0,.10,.05) })
        {
            var pending=new List<FreeRacingCommand>();
            var actual=new List<FreeRacingCommand>();
            var expected=new List<FreeRacingCommand>();
            var active=new FreeRacingAction(0,0);
            double next=0;
            for (int tick=0;tick<=130;tick++)
            {
                double time=tick*.01;
                Activate(pending,actual,time,ref active);
                if (time<1 && time>=next-1e-10)
                {
                    var cmd=new FreeRacingCommand(time,config.Due(time,.24,pending.LastOrDefault()),
                        new(.005f*tick,.01f*tick));
                    pending.Add(cmd);
                    expected.Add(cmd);
                    next+=config.Period(time);
                }
                if (actual.Any(c=>c.ActivationAt>time+1e-10) ||
                    !pending.SequenceEqual(expected.Skip(actual.Count)) ||
                    actual.Count>0 && active!=actual[^1].Action)
                    throw new InvalidOperationException("Pending changed outside its activation deadline.");
            }
            if (actual.Count!=Math.Round(1/config.ReviewSeconds) || !actual.SequenceEqual(expected) ||
                actual.Where((c,i)=>Math.Abs(c.ObservedAt-i*config.ReviewSeconds)>1e-10 ||
                    Math.Abs(c.ActivationAt-c.ObservedAt-config.ReactionSeconds)>1e-10).Any())
                throw new InvalidOperationException("Cadence or delay differs from its configured value.");
        }

        var frame=NativeWorld172.Frame();
        var spec=NativeWorld172.Rider(StartingGate.B);
        var track=MultiActionExperiment135.Surface(frame).SampleSurface(0,2);
        var physics=FreeLearningExperiment.Parameters();
        var road=frame.At(52,7);
        var view=new RiderView(1,road.Position,new Vector2(23,0),0,0,road.Road,52,0,RiderActivity.Racing,.4f,7,0);
        var state=new HandlingForecast(view,9,73,0,0,657);
        var before=new FreeRacingAction(.02f,.40f);
        var inherited=new FreeRacingCommand(8.8,9.04,new(-.02f,.10f));
        var queued=new FreeRacingCommand(9,9.05,new(.07f,.80f));
        HandlingForecast Advance(HandlingForecast input,double seconds,FreeRacingAction action,FreeRacingCommand[] pending) =>
            HandlingPreview.Advance(input,seconds,action,pending,track,new(),spec,physics,frame,147,52.72097f,LaunchParameters144.Default);
        var all=Advance(state,.09,before,[inherited,queued]);
        var prefix=Advance(state,.04,before,[]);
        var mid=Advance(prefix,.01,inherited.Action,[]);
        var after=Advance(mid,.04,queued.Action,[]);
        if (all.Rider.Position!=after.Rider.Position || all.Rider.Velocity!=after.Rider.Velocity ||
            all.Steer!=after.Steer || Math.Abs(all.Lean-after.Lean)>1e-10 ||
            Math.Abs(all.Shaft-after.Shaft)>1e-10 || all.Rider.Drive!=queued.Action.Drive ||
            new Schedule188(9,.05,.05).Due(9,.24,inherited)!=9.05)
            throw new InvalidOperationException("Forecast consumed the inherited or later command at the wrong time.");
        Console.WriteLine("PASS three 1-second cadence sequences and the inherited-command forecast.");
    }
}
