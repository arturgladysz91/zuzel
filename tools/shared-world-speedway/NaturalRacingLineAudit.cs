using System.Globalization;
using System.IO.Compression;
using System.Text.Json;

namespace SharedWorldSpeedway;

/// <summary>One geometry hypothesis, two existing engines at most. No fitting or physics changes.</summary>
public static class NaturalRacingLineAudit
{
    public const string BaseHead="a5164d9daddb091eb86a2809792703941069fd56";
    public const string HistoricalDirectory="docs/experiments/speed-grip-line-crossover";
    public static NaturalRacingLineParameters Line=>new();
    public static ExperimentalDriveEnvelope Engine(string name)=>SpeedGripCrossoverAudit.Engines().Single(e=>e?.Name==name)!;
    public static AuthoredRacingPath Path()=>AuthoredRacingPath.Natural(Line);
    private static object Inputs()=>new{BaseHead,Line,Track=RacingLineRootCauseAudit.Variant(),
        Parameters=SpeedGripCrossoverAudit.Parameters(null),Engines=new[]{Engine("power4"),Engine("power3")},
        InitialReferenceMeters=32,InitialSpeedMetersPerSecond=18,InitialBodyHeadingRadians=0,
        InitialVelocityX=18,InitialVelocityY=0,InitialWheelOmega=18/.33,Drive=1,StepSeconds=.01,Laps=8,DurationLimitSeconds=240,
        InitialRule="Common reference32, road-parallel heading/velocity18m/s and rolling wheel v/r. Only initial XY/target follow each authored line. No pose or speed corrections during the run. Natural begins with a different target tangent; whole-run transient is retained.",
        GeometryRule="One full-XY periodic septic Hermite curve, existing <=1m knots and shared jets from +/-0.25/0.5m samples; offset source uses septic smoothstep. No parameter changes after timing. Drawing unavailable in attachment; implements the written concept, not a digitization.",
        SelectionRule="Power4 first. Power3 only if BOTH Power4 lines fail original validation. INNER reuses exact #106 records after initial/force-prefix parity. No other engine or line search.",
        RankingRule="Both complete8 laps; whole-run footprint inner>=.05m, outer>=.5m, interval inner>=0m/outer>=.5m; last2 RMS/max lateral<=.5/1m and velocity heading<=3/10deg; last3 lap max/min<=1.005. Otherwise diagnostic only.",
        ControlRule="Existing path guidance/preview from #106, unchanged gains; #108 inverse OFF and #109 full-contact-yaw OFF, rear wheel ON. Drive1, Grip1/ruts0/moisture.5. No braking or speed cap."};
    private static void Save(string dir,string name,object value)=>InverseFeasibilityAudit.Save(dir,name,value);
    public static object VerifyReuse()
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);
        while(root is not null&&!File.Exists(System.IO.Path.Combine(root.FullName,"SpeedwayManager.sln")))root=root.Parent;
        var historical=System.IO.Path.Combine(root?.FullName??throw new DirectoryNotFoundException("Repository root required."),HistoricalDirectory);
        var checks=new List<object>();
        using var oldFreeze=JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(historical,"freeze.json")));
        if(oldFreeze.RootElement.GetProperty("Parameters").GetRawText()!=JsonSerializer.Serialize(SpeedGripCrossoverAudit.Parameters(null),InverseFeasibilityAudit.Json))
            throw new InvalidOperationException("#106 parameter mismatch.");
        foreach(var engineName in new[]{"power4","power3"}){
            var engine=Engine(engineName);var path=AuthoredRacingPath.Crossover(false);
            // A short native prefix, not another 8-lap historical run. Use the SAME ordinary
            // rolling initialization as B and prove it matches A's saved steady-start path.
            var(c,wheels)=SpeedGripCrossoverAudit.Execute(false,engine,duration:.5,pathOverride:path);
            using var old=JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(historical,engineName+"-inner.json")));
            var actual=JsonSerializer.Serialize(c.Initial,InverseFeasibilityAudit.Json);
            if(actual!=old.RootElement.GetProperty("Initial").GetRawText())throw new InvalidOperationException("Initial body/wheel differs from #106.");
            using var zip=new GZipStream(File.OpenRead(System.IO.Path.Combine(historical,engineName+"-inner-tracking.csv.gz")),CompressionMode.Decompress);
            using var reader=new StreamReader(zip);var header=reader.ReadLine()!.Split(',');var count=0;
            foreach(var tick in c.Ticks){
                var values=reader.ReadLine()!.Split(',').Select(x=>double.Parse(x,CultureInfo.InvariantCulture)).ToArray();
                double At(string key)=>values[Array.IndexOf(header,key)];var v=tick.Before;
                if(At("t")!=tick.TimeSeconds||At("x")!=v.Position.X||At("y")!=v.Position.Y||At("vx")!=v.Velocity.X||At("vy")!=v.Velocity.Y
                    ||At("body_heading")!=v.Heading||At("fx")!=tick.Slip.DriveForceNewtons||At("fy")!=tick.Slip.LateralForceNewtons)
                    throw new InvalidOperationException("#106 native prefix differs at "+tick.TimeSeconds);
                count++;
            }
            if(wheels[0].Step.BeforeOmega!=18/.33)throw new InvalidOperationException("Rolling-wheel initial state differs.");
            checks.Add(new{Engine=engine,InitialExactlyMatches=true,NativePrefixSamples=count,PrefixSeconds=.5,
                SourceFingerprint=old.RootElement.GetProperty("Result").GetProperty("Fingerprint").GetString()});
        }
        return checks;
    }
    public static void Run(string dir,string stage)
    {
        Directory.CreateDirectory(dir);
        if(stage=="freeze"){Save(dir,"freeze.json",Inputs());return;}
        using var freeze=JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(dir,"freeze.json")));
        if(freeze.RootElement.GetRawText()!=JsonSerializer.Serialize(Inputs(),InverseFeasibilityAudit.Json))throw new InvalidOperationException("Frozen inputs changed.");
        if(stage=="verify"){Save(dir,"reuse-verification.json",VerifyReuse());return;}
        if(stage=="probes"){NaturalRacingLineProbes.Run(dir);return;}
        if(stage=="geometry"){
            Save(dir,"geometry-summary.json",new[]{AuthoredRacingPath.Crossover(false),Path()}.Select(p=>RacingLineRootCauseAudit.ExportGeometry(dir,p)).ToArray());return;
        }
        if(stage is "power4" or "power3"){
            using var geometry=JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(dir,"geometry-summary.json")));
            if(geometry.RootElement.EnumerateArray().Any(g=>!g.GetProperty("GeometricallySafe").GetBoolean()))throw new InvalidOperationException("Geometry failed before simulation.");
            if(!File.Exists(System.IO.Path.Combine(dir,"reuse-verification.json")))throw new InvalidOperationException("Prove reuse before new worlds.");
            if(stage=="power3"){
                using var gate=JsonDocument.Parse(File.ReadAllText(System.IO.Path.Combine(dir,"power4-gate.json")));
                if(!gate.RootElement.GetProperty("BothRejected").GetBoolean())throw new InvalidOperationException("Power3 gate closed.");
            }
            Save(dir,stage+"-run.json",SpeedGripCrossoverAudit.Capture(dir,stage+"-natural",false,Engine(stage),pathOverride:Path()));return;
        }
        throw new ArgumentException("Use freeze, verify, geometry, power4, power3 or probes.");
    }
}
