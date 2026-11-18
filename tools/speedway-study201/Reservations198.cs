using CoreSim.PhysicalSpace;

namespace SpeedwayHandling;

internal sealed class Reservations198
{
    private const double Step=.05;
    private const double PairLimit=.30;
    private readonly Dictionary<int,SortedDictionary<int,HeadingSample198>> paths=[];
    private static int Key(double time)=>(int)Math.Round(time/Step);

    internal void Add(int rider,Predictor198.Cursor198 forecast,double first,double until)
    {
        var curve=new SortedDictionary<int,HeadingSample198>();
        for(double t=first;t<=until+1e-9;t+=Step)
        {
            forecast.Advance(12,t);
            if(forecast.Outcome is not null)break;
            curve.Add(Key(t),forecast.Snapshot);
        }
        if(curve.Count==0) throw new InvalidOperationException("No published reservation.");
        paths.Add(rider,curve);
    }

    internal HeadingSample198? At(int rider,double time)
    {
        if(!paths.TryGetValue(rider,out var stream))return null;
        if(stream.TryGetValue(Key(time),out var sample))return sample;
        var last=stream.Last().Value;
        double dt=time-last.Time;
        if(dt<0 || dt>.90)return null;
        double course=last.Heading-last.Beta;
        return last with {Time=time,X=last.X+(float)(last.Speed*dt*Math.Cos(course)),
            Y=last.Y+(float)(last.Speed*dt*Math.Sin(course))};
    }

    internal bool TryPublish(int rider,IReadOnlyList<HeadingSample198> proposal)
    {
        if(proposal.Count==0) return false;
        foreach(var own in proposal)
        {
            foreach(int peer in paths.Keys)
            {
                if(peer==rider || At(peer,own.Time) is not { } reserved)continue;
                if(Math.Pow(own.X-reserved.X,2)+Math.Pow(own.Y-reserved.Y,2)>16)continue;
                if(MechanicalSeparation.Between(Pose(rider,own),Pose(peer,reserved)).SignedMeters<PairLimit)
                    return false;
            }
        }
        var map=paths[rider];
        int at=Key(proposal[0].Time);
        foreach(int old in map.Keys.Where(t=>t>=at).ToArray())map.Remove(old);
        foreach(var row in proposal)map[Key(row.Time)]=row;
        return true;
    }

    internal static PhysicalBikePose Pose(int rider,HeadingSample198 s) =>
        new(rider,"traffic191",new(s.X,s.Y),new(0,s.Heading-s.Beta,s.Heading),SpeedwayBikeDimensions.Reference);
}
