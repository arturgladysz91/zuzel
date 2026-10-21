using System.Globalization;
using System.IO.Compression;
using System.Numerics;
using System.Text.Json;
using CoreSim;

namespace SharedWorldSpeedway;

public static class SpeedGripCrossoverAudit
{
    public const string BaseHead="3dd410254e7957b1e5d8604c82eff4b229a28d75";
    public const int Laps=8;
    public static ExperimentalDriveEnvelope?[] Engines()=>new ExperimentalDriveEnvelope?[]{null,new("power2",2,.9f),new("power3",3,.8f),new("power4",4,.7f),new("power6",6,.6f)};
    public static WorldParameters Parameters(ExperimentalDriveEnvelope? engine)=>LocalOffsetAudit.Parameters() with{ExperimentalDrive=engine};
    public static (SlipControlValidation.Capture Body,RearWheelObservation[] Wheels) Execute(bool wide,ExperimentalDriveEnvelope? engine=null,float grip=1,float initialSpeed=18,
        float drive=1,double initialReference=32,double step=.01,int laps=Laps,double duration=240,string? csv=null,bool equilibrium=false,
        bool coupled=false,Action<CoupledSlipInverseObservation>? inverseObserver=null,bool fullContactYaw=false,AuthoredRacingPath? pathOverride=null)
    {
        if(pathOverride is not null&&(drive!=1||equilibrium||coupled||fullContactYaw))throw new ArgumentException("Natural-line comparison freezes Drive1 and the original controller.");
        var path=pathOverride??AuthoredRacingPath.Crossover(wide);var rider=RacingLineRootCauseAudit.Rider(path) with{ReferenceMeters=initialReference,OffsetMeters=(float)path.Offset(initialReference).D,
            TargetOffsetMeters=(float)path.Offset(initialReference).D,SpeedMetersPerSecond=initialSpeed};
        // The ordinary rolling constructor supports a changing offset; the old steady-start
        // guard intentionally accepts only a straight, flat authored line. Both yield v/r.
        var start=pathOverride is not null?null:equilibrium?InverseFeasibilityAudit.SteadyStart(initialSpeed):new AuthoredSteadyStart(initialSpeed/.33,drive);
        var parameters=coupled?Parameters(engine) with{EnableCoupledSlipInverse=true}:Parameters(engine);
        if(fullContactYaw)parameters=parameters with{EnableFullContactYawFeedForward=true};
        var wheels=new List<RearWheelObservation>();var c=SlipControlValidation.Execute(new(path.Name,new[]{rider},duration,laps),parameters,
            csvPath:csv,surface:TrackState.CreateDefault(path.Frame.Track,new(grip,0,.5f)).Snapshot(),wheelObserver:wheels.Add,stepSeconds:step,
            trackShape:RacingLineRootCauseAudit.Variant(),authoredPath:path,diagnosticPathGuidance:true,diagnosticPathPreview:true,diagnosticSteadyStart:start,stopOnCrash:true,inverseObserver:inverseObserver);
        return(c,wheels.ToArray());
    }
    private static void Save(string dir,string name,object value)=>InverseFeasibilityAudit.Save(dir,name,value);
    public static object Capture(string dir,string name,bool wide,ExperimentalDriveEnvelope? engine=null,float grip=1,float initialSpeed=18,
        float drive=1,double initialReference=32,double step=.01,int laps=Laps,double duration=240,bool equilibrium=false,
        bool coupled=false,Action<CoupledSlipInverseObservation>? inverseObserver=null,bool fullContactYaw=false,AuthoredRacingPath? pathOverride=null)
    {
        foreach(var suffix in new[]{".json","-accepted.csv","-tracking.csv.gz","-forces.csv.gz"})if(File.Exists(Path.Combine(dir,name+suffix)))throw new IOException("Evidence exists: "+name+suffix);
        var(c,wheels)=Execute(wide,engine,grip,initialSpeed,drive,initialReference,step,laps,duration,Path.Combine(dir,name+"-accepted.csv"),equilibrium,coupled,inverseObserver,fullContactYaw,pathOverride);
        var result=RacingLineRootCauseAudit.ExportCapture(dir,name,c,wheels,pathOverride??AuthoredRacingPath.Crossover(wide),diagnosticPathFrame:true,requiredLaps:laps,compressTracking:true);
        var byTime=wheels.ToDictionary(x=>Math.Round(x.TimeSeconds,8));var mech=c.Mechanical[1];
        using var zip=new GZipStream(File.Create(Path.Combine(dir,name+"-forces.csv.gz")),CompressionLevel.SmallestSize);using var writer=new StreamWriter(zip);
        writer.WriteLine("t,dt,s,mass,inertia,force_x,force_y,accel_x,accel_y,normal_force,measured_normal_force,body_force_residual,yaw_residual,requested_engine_torque,available_engine_torque,delivered_engine_torque,omega_before,omega_after,engine_work,longitudinal_loss,lateral_loss,resistance_loss,bearing_loss,feed_forward_curvature,feed_forward_acceleration");
        var path=pathOverride??AuthoredRacingPath.Crossover(wide);
        foreach(var tick in c.Ticks){var v=tick.Before;var middle=(v.Velocity+tick.After.Velocity)/2;var speed=middle.Length();var normal=speed>.01f?new Vector2(-middle.Y,middle.X)/speed:Vector2.Zero;
            var acceleration=(tick.After.Velocity-v.Velocity)/(float)tick.DurationSeconds;var force=tick.Applied.ForceNewtons;var wheel=byTime[Math.Round(tick.TimeSeconds,8)].Step;
            var guidance=path.Guidance(v,Parameters(engine),true);var along=v.Velocity.X*guidance.Tangent.X+v.Velocity.Y*guidance.Tangent.Y;
            InverseFeasibilityAudit.Row(writer,tick.TimeSeconds,tick.DurationSeconds,v.TotalReferenceMeters,mech.MassKg,mech.Inertia,
                force.X,force.Y,acceleration.X,acceleration.Y,Vector2.Dot(force,normal),mech.MassKg*Vector2.Dot(acceleration,normal),
                (acceleration*mech.MassKg-force).Length(),mech.Inertia*(tick.After.AngularVelocity-v.AngularVelocity)/tick.DurationSeconds-tick.Applied.TorqueNewtonMeters,
                wheel.RequestedTorqueNewtonMeters,wheel.AvailableTorqueNewtonMeters,wheel.DriveTorqueNewtonMeters,wheel.BeforeOmega,wheel.AfterOmega,
                wheel.EngineWorkJoules,wheel.LongitudinalLossJoules,tick.Slip.LateralLossWatts*tick.DurationSeconds,tick.Slip.ResistanceLossWatts*tick.DurationSeconds,wheel.BearingLossJoules,
                guidance.FeedForwardCurvature,along*along*guidance.FeedForwardCurvature);
        }
        Console.WriteLine("Captured "+name);return new{Name=name,Wide=wide,Engine=engine,Grip=grip,InitialSpeed=initialSpeed,Drive=equilibrium?InverseFeasibilityAudit.SteadyStart(initialSpeed).Drive:drive,InitialReference=initialReference,Step=step,Laps=laps,Summary=result};
    }
    public static void Run(string dir,string stage)
    {
        Directory.CreateDirectory(dir);
        if(stage=="freeze"){
            Save(dir,"freeze.json",new{BaseHead,Variant=RacingLineRootCauseAudit.Variant(),Parameters=Parameters(null),Engines=Engines(),Laps,
                InitialReference=32,InitialSpeed=18,Step=.01,Drive=1,Paths=new[]{"crossover-inner","crossover-wide-straight"},
                GeometryRule="Full periodic septic Hermite XY, knot spacing <=1m, shared jets from symmetric five-point source samples +/-0.25/0.5m. A offset1. B holds1 through apex50%, opens to5 by exit+10m, holds5 toexit+25m, returns to1 by next entry-5m. Track unchanged. No line fitting after times.",
                RankingRule="Both worlds complete8 laps, whole-run footprint safety, last3 lap durations max/min <=1.005; final2 laps RMS lateral<=.5/max<=1m and RMS/max velocity heading<=3/10deg. Same engine/grip/Drive. No ranking across configs.",
                SurfaceSelectionRule="After engine matrix: strongest configuration for which both paths complete and are boundary-safe; if none, original baseline. Tracking still gates all ranking; do not force130-140. Grip1 reuses matrix data, then .85/.70/.55.",
                ExtremesRule="160/200kmh initialized at reference34 (1m before bend), rolling wheel, Drive0, no speed reset after initialization. Original forces, no brakes, stop at original crash classification. Repeat h20/10/5ms for numerical convergence.",
                ControlsRule="Two separate slow original-engine straight-equilibrium18/maintenance-throttle proofs for C3 feasibility and viewer. Not substitutes for full-drive matrix. No tactical AI/contacts/other riders.",
                KphTargets=new[]{110,120,130,140},SurfaceGrips=new[]{1,.85,.70,.55}});return;
        }
        using var freeze=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"freeze.json")));
        if(freeze.RootElement.GetProperty("BaseHead").GetString()!=BaseHead||freeze.RootElement.GetProperty("Engines").GetRawText()!=JsonSerializer.Serialize(Engines(),InverseFeasibilityAudit.Json)
            ||freeze.RootElement.GetProperty("Parameters").GetRawText()!=JsonSerializer.Serialize(Parameters(null),InverseFeasibilityAudit.Json))throw new InvalidOperationException("Frozen inputs changed.");
        if(stage=="geometry"){
            Save(dir,"geometry-summary.json",new[]{false,true}.Select(w=>RacingLineRootCauseAudit.ExportGeometry(dir,AuthoredRacingPath.Crossover(w))).ToArray());
            foreach(var wide in new[]{false,true}){
                var path=AuthoredRacingPath.Crossover(wide);using var writer=InverseFeasibilityAudit.Csv(dir,path.Name+"-c3.csv","s,curvature,curvature_derivative");
                for(var i=0;i<=6400;i++){var s=i*path.Frame.LapMeters/6400;InverseFeasibilityAudit.Row(writer,s,path.At(s).Curvature,path.CurvatureDerivative(s));}
            }return;
        }
        if(stage=="engines"){
            var results=new List<object>();foreach(var engine in Engines())foreach(var wide in new[]{false,true})results.Add(Capture(dir,(engine?.Name??"baseline")+(wide?"-wide":"-inner"),wide,engine));
            Save(dir,"engine-runs.json",results);return;
        }
        if(stage=="controls"){
            Save(dir,"control-runs.json",new[]{false,true}.Select(wide=>Capture(dir,"control"+(wide?"-wide":"-inner"),wide,equilibrium:true)).ToArray());return;
        }
        if(stage=="surface-freeze"){
            using var matrix=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"engine-runs.json")));ExperimentalDriveEnvelope? selected=null;var found=false;
            foreach(var engine in Engines().Reverse()){
                var key=engine?.Name??"baseline";var pair=matrix.RootElement.EnumerateArray().Where(r=>r.GetProperty("Name").GetString()!.StartsWith(key+"-",StringComparison.Ordinal)).ToArray();
                if(pair.Length==2&&pair.All(r=>r.GetProperty("Summary").GetProperty("Result").GetProperty("Completed").GetBoolean()&&r.GetProperty("Summary").GetProperty("BoundarySafe").GetBoolean())){selected=engine;found=true;break;}
            }
            Save(dir,"surface-freeze.json",new{Engine=selected,FoundBoundaryCertifiedPair=found,
                Reason=found?"Strongest engine with both complete boundary-certified worlds; tracking still separately gates ranking.":"No engine has a boundary-certified pair. Keep original baseline for surface diagnosis; no physically justified130-140kph pair was established.",
                EngineRunsSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(dir,"engine-runs.json")))),Grips=new[]{1,.85,.70,.55},Drive=1,Parameters=Parameters(selected)});return;
        }
        if(stage=="extreme-freeze"){
            var path=AuthoredRacingPath.Crossover(false);double? flat=null;
            for(var s=34d;s>=30;s-=.25)if(Math.Abs(path.Offset(s).First)<=1e-10&&Math.Abs(path.Offset(s).Second)<=1e-10){flat=s;break;}
            if(flat is null)throw new InvalidOperationException("No exact straight initialization before bend.");
            Save(dir,"extreme-freeze.json",new{InitialReference=flat,InitialReferenceInitiallySpecified=34,
                Reason="C3 interpolation begins smoothing slightly before reference bend join. Initial-equilibrium preflight rejected reference34 before any stress replay. Select latest exact flat point on quarter-metre grid, using geometry only; rolling initial wheel and Drive0 unchanged.",
                FreezeSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(dir,"freeze.json"))))});return;
        }
        if(stage is "surfaces" or "extremes"){
            using var selection=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"surface-freeze.json")));
            var saved=selection.RootElement.GetProperty("EngineRunsSha256").GetString();var actual=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(Path.Combine(dir,"engine-runs.json"))));
            if(saved!=actual)throw new InvalidOperationException("Matrix changed after surface selection.");
            var value=selection.RootElement.GetProperty("Engine");var engine=value.ValueKind==JsonValueKind.Null?null:JsonSerializer.Deserialize<ExperimentalDriveEnvelope>(value.GetRawText(),InverseFeasibilityAudit.Json);
            var results=new List<object>();
            if(stage=="surfaces"){
                foreach(var grip in new[]{.85f,.70f,.55f})foreach(var wide in new[]{false,true})
                    results.Add(Capture(dir,"grip"+((int)Math.Round(grip*100)).ToString(CultureInfo.InvariantCulture)+(wide?"-wide":"-inner"),wide,engine,grip));
                Save(dir,"surface-runs.json",results);
            }else{
                using var initial=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"extreme-freeze.json")));var reference=initial.RootElement.GetProperty("InitialReference").GetDouble();
                foreach(var speed in new[]{160,200})foreach(var step in new[]{.02,.01,.005})
                    results.Add(Capture(dir,$"stress{speed}-h{step*1000:0}",false,engine,initialSpeed:speed/3.6f,drive:0,initialReference:reference,step:step,laps:1,duration:4));
                Save(dir,"extreme-runs.json",results);
            }return;
        }
        if(stage=="real-data"){
            var file="data/calibration/pge/v1/pge_rider_heats.csv";var data=CoreSim.Analysis.RealWorldCalibrationDataset.ParseCsv(File.ReadAllText(file));
            object Stats(IEnumerable<CoreSim.Analysis.RealWorldRiderHeatObservation> rows){var a=rows.Where(r=>r.CleanPhysics).ToArray();
                double Q(IEnumerable<double?> values,double p){var v=values.Select(x=>x!.Value).OrderBy(x=>x).ToArray();var z=(v.Length-1)*p;var i=(int)z;return v[i]+(v[Math.Min(i+1,v.Length-1)]-v[i])*(z-i);}
                return new{CleanRows=a.Length,VmaxP10=Q(a.Select(r=>r.MaximumSpeedKph),.1),VmaxP50=Q(a.Select(r=>r.MaximumSpeedKph),.5),VmaxP90=Q(a.Select(r=>r.MaximumSpeedKph),.9),VmaxP99=Q(a.Select(r=>r.MaximumSpeedKph),.99),
                    AverageSpeedMedian=Q(a.Select(r=>r.AverageSpeedMetersPerSecond),.5),FlyingLapMedian=Q(a.Select(r=>r.FlyingLapMedianSeconds),.5)};
            }
            Save(dir,"real-evidence.json",new{Source=CoreSim.Analysis.RealWorldCalibrationDataset.SourceName,Version=CoreSim.Analysis.RealWorldCalibrationDataset.SourceVersion,
                DatasetCsvSha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(file))),All=Stats(data.Rows),
                ExactVenueGroups=data.Rows.Where(r=>r.CleanPhysics&&(r.SourceTrackLabel=="Motoarena im. Mariana Rosego"||r.SourceTrackLabel=="Toruń")).GroupBy(r=>new{r.Season,r.SourceTrackLabel}).Select(g=>new{Selector=g.Key,Stats=Stats(g)}).ToArray(),
                TelemetryGuide="https://ekstraliga.pl/se/news/telemetria-w-pge-ekstralidze-przewodnik",OfficialMaximumContext="140km/h measured in tests at Tarnow; not a Motoarena corner profile.",
                MotoarenaReference="https://ekstraliga.pl/se/news/czy-podczas-pge-imme-im-zenona-plecha-padnie-nowy-rekord-toru",PublishedReferenceLength=318,PublishedFourLapRecordSeconds=56.50,
                NominalRecordAverageMetersPerSecond=4*318/56.5,Unsupported="No numeric Motoarena entry/25/50/75/exit speed trace in this snapshot; curve_speed is a ranking flag. No corner values imputed from Vmax."});return;
        }
        if(stage=="probes"){
            var path=AuthoredRacingPath.Crossover(false);var rider=RacingLineRootCauseAudit.Rider(path);var mech=InverseFeasibilityAudit.Mechanical();var values=new List<object>();
            foreach(var grip in new[]{1f,.85f,.70f,.55f})foreach(var turn in new[]{false,true}){
                var s=turn?70:32;var q=path.At(s);var beta=turn?.15f:0;const float speed=18;
                var v=new RiderView(1,new((float)q.Position.X,(float)q.Position.Y),new((float)(speed*q.Tangent.X),(float)(speed*q.Tangent.Y)),(float)q.Heading+beta,0,path.Frame.RoadAt(s,q.OffsetMeters),s,0,RiderActivity.Racing,1,(float)q.OffsetMeters,0);
                var surface=TrackState.CreateDefault(path.Frame.Track,new(grip,0,.5f)).Snapshot();var wheel=new RearWheelState(v.ForwardSpeed*1.1);SlipObservation slip=default;
                _=SlipCoupledDynamics.Forces(v,rider,path.Frame,surface,Parameters(null),0,mech.Mass,mech.Inertia,observeSlip:x=>slip=x,rearWheel:wheel,stepSeconds:.01f,diagnosticPath:new(q.Position,q.Heading,q.Curvature));
                values.Add(new{Grip=grip,EffectiveGrip=surface.GetSurface(v.Road.Segment,0).EffectiveGrip,Turn=turn,slip.GripBudgetNewtons,AccelerationBudget=slip.GripBudgetNewtons/mech.Mass,
                    slip.DriveForceNewtons,slip.LateralForceNewtons,slip.GripUtilization,wheel.Prepared.AvailableTorqueNewtonMeters,StateRule="same18m/s, beta0/.15, wheel circumferential speed1.1*forward, Drive1; original equations"});
            }
            Save(dir,"grip-force-map.json",values);
            using var stream=File.Create(Path.Combine(dir,"drive-envelope.csv"));using var writer=new StreamWriter(stream);writer.WriteLine("engine,turn,wheel_speed,available_force,available_torque,shaft_power,resistance");
            foreach(var engine in Engines())foreach(var turn in new[]{false,true})for(var i=0;i<=140;i++){
                var speed=i*.5f;var surface=new TrackSurfaceState(1,0,.5f);var reference=turn?LongitudinalDynamics.CalculateTurnExitAvailableDriveForceNewtons(rider.Profile.Skills,rider.Setup,surface):LongitudinalDynamics.CalculateStraightAvailableDriveForceNewtons(rider.Profile.Skills,rider.Setup,surface);
                var available=engine?.AvailableForce(reference,speed,rider.Setup)??LongitudinalDynamics.CalculateAvailableDriveForceAtSpeedNewtons(reference,speed,rider.Setup);
                writer.Write(engine?.Name??"baseline");writer.Write(',');InverseFeasibilityAudit.Row(writer,turn?1:0,speed,available,.33*available,speed*available,LongitudinalDynamics.CalculateLongitudinalResistanceForceNewtons(speed));
            }return;
        }
        if(stage=="verify"){
            var checks=new List<object>();
            using var previous=JsonDocument.Parse(File.ReadAllText("docs/experiments/progressive-bend-width/measurements.json"));
            foreach(var plan in LocalOffsetAudit.Plans()){
                var expected=previous.RootElement.EnumerateArray().Single(r=>r.GetProperty("Measurement").GetProperty("Geometry").GetString()=="progressive-b"&&r.GetProperty("Measurement").GetProperty("Plan").GetProperty("Name").GetString()==plan.Name).GetProperty("Measurement").GetProperty("Evaluation").GetProperty("Fingerprint").GetString();
                var actual=LocalOffsetAudit.Plain(RacingLineRootCauseAudit.Variant(),plan).Fingerprint;if(actual!=expected)throw new InvalidOperationException("#103 changed.");checks.Add(new{Case="#103 "+plan.Name,Expected=expected,Actual=actual});
            }
            var old=RacingLineRootCauseAudit.Execute("outside-inside-outside",true,preview:true).Capture.Result.Fingerprint;
            using var oldFile=JsonDocument.Parse(File.ReadAllText("docs/experiments/racing-line-root-cause/outside-inside-outside-path-preview.json"));var saved=oldFile.RootElement.GetProperty("Result").GetProperty("Fingerprint").GetString();
            if(old!=saved)throw new InvalidOperationException("#104 changed.");checks.Add(new{Case="#104 OIO preview",Expected=saved,Actual=old});
            var matched=InverseFeasibilityAudit.Replay(AuthoredRacingPath.Matched(8.5,15),18).Capture.Result.Fingerprint;
            using var file105=JsonDocument.Parse(File.ReadAllText("docs/experiments/inverse-feasibility-matched-apex/matched-wide-early-steady-18.json"));var expected105=file105.RootElement.GetProperty("Result").GetProperty("Fingerprint").GetString();
            if(matched!=expected105)throw new InvalidOperationException("#105 changed.");checks.Add(new{Case="#105 matched-wide-early",Expected=expected105,Actual=matched});
            var path=AuthoredRacingPath.Crossover(true);var rider=RacingLineRootCauseAudit.Rider(path) with{ReferenceMeters=32,OffsetMeters=1,TargetOffsetMeters=1};
            using var plain=new WorldRunner(new[]{rider},parameters:Parameters(null),trackShape:RacingLineRootCauseAudit.Variant(),authoredPath:path,diagnosticPathGuidance:true,diagnosticPathPreview:true,
                diagnosticSteadyStart:InverseFeasibilityAudit.SteadyStart(18),frozenSurface:TrackState.CreateDefault(path.Frame.Track,new(1,0,.5f)).Snapshot());
            var repeat=plain.Run("plain",.01,240,Laps,lapsFromInitialPosition:true,stopOnCrash:true).Fingerprint;
            using var file106=JsonDocument.Parse(File.ReadAllText(Path.Combine(dir,"control-wide.json")));var expected106=file106.RootElement.GetProperty("Result").GetProperty("Fingerprint").GetString();
            if(repeat!=expected106)throw new InvalidOperationException("#106 observer parity failed.");checks.Add(new{Case="#106 control-wide observed/plain",Expected=expected106,Actual=repeat});Save(dir,"verification.json",checks);return;
        }
        throw new ArgumentException("Use freeze, geometry, engines, controls, surface-freeze, surfaces, extreme-freeze, extremes, real-data, probes or verify.");
    }
}
