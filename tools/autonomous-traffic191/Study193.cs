using System.IO.Compression;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using CoreSim;
using CoreSim.PhysicalSpace;
using Cyclic172;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

internal static partial class Study191
{
    private sealed record ForecastError193(int Rider,double Time,double Seconds,double XYMeters,
        double SpeedMetersPerSecond,double CourseRadians,double YawRadiansPerSecond,double RollRadians,
        double SteeringRadians,double WheelRadiansPerSecond,double CrankRadiansPerSecond);
    private sealed record SavedRider193(StartingGate Gate,double Ratio,StartSample144[] Samples,
        FreeRacingCommand[] Activations,Decision191[] Decisions,LeanSample[] Lean);

    private static readonly string[] NativeArms193 = ["solo-stock", "solo-exit", "four-exit", "alternate-exit"];

    private static int Study193(string[] args)
    {
        if (args is ["audit", var input, var output])
        {
            Audit193(input,output);
            return 0;
        }
        if (args is ["native", var arm,var dir] && NativeArms193.Contains(arm))
        {
            Native193(arm,dir);
            return 0;
        }
        Console.Error.WriteLine("Study #193 is OFF. Use --study-193 audit INPUT OUTPUT or native ARM DIR.");
        return 2;
    }

    private static RiderSpec Spec193(StartingGate gate, int id, double ratio)
    {
        var rider = TrafficWorld191.Rider(gate,id);
        var ranges = Engine500Parameters.Default;
        return rider with { Setup=new((float)((ranges.ShortOverallRatio-ratio)/
            (ranges.ShortOverallRatio-ranges.LongOverallRatio)),rider.Setup.TractionBias) };
    }

    private static void Native193(string arm, string dir)
    {
        Directory.CreateDirectory(dir);
        using var gate = new FileStream(Path.Combine(dir,"world-budget.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        string binary = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(Study191).Assembly.Location)));
        var started = Directory.GetFiles(dir,"*.reserved");
        if (started.Length >= NativeArms193.Length || arm != NativeArms193[started.Length] ||
            started.Any(file => File.ReadAllText(file)!=binary))
            throw new InvalidOperationException("Four-world limit, order or frozen executable violated.");
        using (var reservation = new StreamWriter(new FileStream(Path.Combine(dir,arm+".reserved"),FileMode.CreateNew)))
            reservation.Write(binary);

        StartingGate[] grid = arm.StartsWith("solo") ? [StartingGate.B] :
            [StartingGate.A,StartingGate.B,StartingGate.C,StartingGate.D];
        double[] ratios = arm.StartsWith("solo") ? [9] :
            arm=="four-exit" ? [10,9,8,9] : [8,9,8,9];
        var specs = grid.Select((g,i)=>Spec193(g,i+1,ratios[i])).ToArray();
        RunRace(arm,dir,specs,grid,experimentalExit:arm!="solo-stock");
    }

    private static void Audit193(string input, string output)
    {
        using var file = File.OpenRead(input);
        using var gzip = new GZipStream(file,CompressionMode.Decompress);
        using var document = JsonDocument.Parse(gzip);
        var root = document.RootElement;
        var run = root.GetProperty("Run");
        var journal = root.GetProperty("Journal");
        var roll = root.GetProperty("Lean");
        if (run.GetProperty("LapMeters").GetDouble()!=Frame.LapMeters)
            throw new ArgumentException("Archive does not use the original H25 geometry.");
        var riders = run.GetProperty("Riders").EnumerateArray().Select((r,i)=>
            new SavedRider193(r.GetProperty("Gate").Deserialize<StartingGate>(Json),
                10-2*r.GetProperty("Spec").GetProperty("Setup").GetProperty("Gearing").GetDouble(),
                r.GetProperty("Samples").Deserialize<StartSample144[]>(Json)!,
                r.GetProperty("Activations").Deserialize<FreeRacingCommand[]>(Json)!,
                journal[i].Deserialize<Decision191[]>(Json)!,roll[i].Deserialize<LeanSample[]>(Json)!)).ToArray();
        var specs = riders.Select((r,i)=>Spec193(r.Gate,i+1,r.Ratio)).ToArray();
        var stock = specs.Select(spec => Mind(spec,true)).ToArray();
        var trial = specs.Select(spec=>new RaceMind191(Track,Surface,Physics,spec,Handling,Timing,true,true)).ToArray();
        var forecasts = new List<ForecastError193>();
        // Both reconstruction and candidate evaluation start on accepted native observations.
        // A fresh Box2D world is never created here.
        var times = riders[0].Samples.Select(p=>p.Input.Time).ToArray();
        var bends = new WholeCorner171.BendMap(Frame);
        var exitZones = Enumerable.Range(0,3).SelectMany(lap=>Enumerable.Range(0,bends.Count)
            .Select(i=>bends.At(lap,i).Exit)).ToArray();
        int reproduced=0,total=0;
        var mismatches = new List<object>();
        var tests = new List<object>();
        foreach (var d in riders[0].Decisions)
        {
            int k = At(times,d.Time);
            if (Math.Abs(times[k]-d.Time) > 1e-7)
                throw new InvalidOperationException("Decision not aligned to an accepted sample.");
            var active = riders.Select(r=>r.Activations.LastOrDefault(x=>x.ActivationAt<=d.Time+1e-10)?.Action ?? default).ToArray();
            var snapshot = new WorldSnapshot(d.Time,Array.AsReadOnly(riders.Select(r=>r.Samples[k].Input.Rider
                with { Drive=0, TargetOffsetMeters=(float)r.Samples[k].Input.Rider.Road.OffsetMeters }).ToArray()));
            for (int i=0;i<riders.Length;i++)
            {
                var r = riders[i];
                var before = r.Samples[k];
                var model = before.Input with { Time=d.Time, Rider=before.Input.Rider with { Drive=active[i].Drive },
                    Active=active[i], Pending=before.Input.Pending.Where(p=>p.ActivationAt>d.Time+1e-10).ToArray() };
                var shaft = new CrankClutch144(omega:before.CrankOmega);
                var replayed = stock[i].Choose(model,shaft,r.Lean[k].ActualRadians,snapshot);
                var logged = r.Decisions[stock[i].Journal.Count-1].Control;
                total++;
                bool agrees = Math.Abs(replayed.Action.SteeringRadians-logged.Action.SteeringRadians)<.00005 &&
                    Math.Abs(replayed.Action.Drive-logged.Action.Drive)<.0002 &&
                    Math.Abs(replayed.TargetOffset-logged.TargetOffset)<.01;
                if (agrees) reproduced++;
                else if (mismatches.Count<24)
                    mismatches.Add(new { d.Time, Rider=i+1, Replayed=replayed.Action, Recorded=logged.Action });
                var choice = trial[i].Choose(model,shaft,r.Lean[k].ActualRadians,snapshot);
                var observation = trial[i].Journal[^1].Exit;
                if (observation is { Action: not null })
                    tests.Add(new { d.Time, Rider=i+1, Reference=model.Rider.TotalReferenceMeters,
                        Choice=choice.Action, Stock=logged.Action, Observation=observation });
                if (stock[i].Journal.Count%4!=0 ||
                    !exitZones.Any(exit => Math.Abs(model.Rider.TotalReferenceMeters-exit-3)<28)) continue;
                foreach (var horizon in new[] { .15,.50,1.00 })
                {
                    int endIndex=At(times,d.Time+horizon);
                    double until = times[endIndex];
                    if (until-d.Time < horizon-.02 || run.GetProperty("Contacts").EnumerateArray().Any(c=>
                        c.GetProperty("Kind").GetString()=="begin" && c.GetProperty("TimeSeconds").GetDouble()>d.Time &&
                        c.GetProperty("TimeSeconds").GetDouble()<=until)) continue;
                    var known = r.Activations.Where(p=>p.ActivationAt>d.Time+1e-10 && p.ActivationAt<until).ToArray();
                    var initial = new HandlingForecast(model.Rider,d.Time,model.Omega,model.Steering,r.Lean[k].ActualRadians,before.CrankOmega);
                    var predicted = HandlingPreview.Advance(initial,until-d.Time,model.Active,known,Surface.SampleSurface(0,2),
                        Handling,specs[i],Physics,Frame,Mass,Inertia,shaft.Parameters);
                    var actual = r.Samples[endIndex];
                    var av = actual.Input.Rider;
                    var pv = predicted.Rider;
                    double courseError = BikeAngles.Wrap((pv.Heading-pv.SlideRadians)-(av.Heading-av.SlideRadians));
                    forecasts.Add(new(i+1,d.Time,horizon,Vector2.Distance(pv.Position,av.Position), pv.Speed-av.Speed,
                        courseError,pv.AngularVelocity-av.AngularVelocity,predicted.Lean-r.Lean[endIndex].ActualRadians,
                        predicted.Steer-actual.Input.Steering,predicted.Wheel-actual.Input.Omega,predicted.Shaft-actual.CrankOmega));
                }
            }
        }
        Save(output,new { Reproduced=reproduced, Total=total, Mismatches=mismatches, Forecasts=forecasts,
            Candidates=tests, Journal=stock.Select(m=>m.Journal.ToArray()).ToArray() });
        Console.WriteLine($"Archived decisions reproduced {reproduced}/{total}; forecast comparisons {forecasts.Count}; modified candidates {tests.Count}. No Box2D.");
    }

    private static int At(double[] times, double time)
    {
        int index = Array.BinarySearch(times,time);
        if (index>=0) return index;
        index = ~index;
        if (index==0) return index;
        if (index==times.Length || time-times[index-1]<times[index]-time) return index-1;
        return index;
    }
}

