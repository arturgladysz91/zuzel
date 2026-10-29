using System.IO.Compression;
using System.Text.Json;
using CoreSim;
using CoreSim.Analysis;
using CoreSim.PhysicalSpace;
using Efficient147;
using Autonomous146;
using SharedWorldSpeedway;
namespace Speed148;
public static class Diagnostics148{
 public static TrackFrame Circular(float straight,float radius){var original=MatchedVenueProfiles.CreateMotoarenaStandingStartTrack();return new(new Track(original.Segments.Select((s,i)=>new TrackSegment(s.Id,s.Type,i==0?straight*35/62:i==8?straight*27/62:s.StraightLengthMetersOverride,s.IsStandingStartSegment)).ToArray(),new TrackGeometry(straight,radius,12,16.6f,MathF.PI/3)));}
 static Run144 Execute(string name,TrackFrame frame,double seconds,RiderSpec? rider,string control,Func<FreeRacingInput,CrankClutch144,RiderSpec,TrackFrame,TrackStateSnapshot,WorldParameters,float,float,string,Decision144>? choose=null){
  var folder=Program.Doc+"/diagnostics";Directory.CreateDirectory(folder);using var budget=new Budget148(name,1,100);
  using var world=new DiagnosticWorld148(new(Enabled:true,RequiredLaps:1,MaximumSeconds:seconds),diagnosticFrame:frame,diagnosticRider:rider);
  ExactRuntime148? rt=null;
  Decision144 Chooser(FreeRacingInput i,CrankClutch144 c,RiderSpec s,TrackFrame f,TrackStateSnapshot surface,WorldParameters p,float m,float inertia,string variant){budget.Guard();if(choose is not null)return choose(i,c,s,f,surface,p,m,inertia,variant);rt??=new(s,f,surface,p,mass:m,inertia:inertia);rt.Begin(i.Time);if(control=="gas1-straight")return new(new(i.Time,new(0,1),FreeActionSafety.Uncertain,false,Array.Empty<FreeLearningCandidate>(),0,0),0);return rt.Seed(new(i,c.Omega));}
  var run=world.Run(name,"K1",Chooser);using(var z=new GZipStream(File.Create(folder+"/"+name+".json.gz"),CompressionLevel.Fastest))JsonSerializer.Serialize(z,run,Program.Json);
  Program.Save(folder+"/"+name+"-result.json",new{run.Name,run.Outcome,run.EndSeconds,run.LapMeters,run.Fingerprint,run.ControllerCpuSeconds,run.NativeAndObservationCpuSeconds,run.Forecasts,run.ForecastContactQueries,MinimumInner=run.Riders[0].Samples.Min(s=>s.Inner),MinimumOuter=run.Riders[0].Samples.Min(s=>s.Outer),MaximumKph=run.Riders[0].Samples.Max(s=>s.Input.Rider.Speed*3.6),InitialReference=run.Riders[0].Samples[0].Input.Rider.TotalReferenceMeters,InitialSpeed=run.Riders[0].Samples[0].Input.Rider.Speed,Control=control,CpuSeconds=budget.Elapsed,Counts=rt?.Counters()});budget.Finish(run.Outcome);Console.WriteLine(name+": "+run.Outcome+" CPU="+budget.Elapsed);Console.Out.Flush();return run;
 }
 public static void Geometry(){
  var variants=new[]{("G0c",62f,31f),("G1",75f,31f),("G2",90f,31f),("G3",62f,36f),("G4",62f,42f)};
  foreach(var (name,l,r) in variants){if(File.Exists(Program.Doc+"/diagnostics/geometry-"+name+"-result.json"))continue;var f=Circular(l,r);Program.Save(Program.Doc+"/geometry-"+name+".json",ReplayGeometry.Create(f));Execute("geometry-"+name,f,40,null,"causal-K1");}
 }
 public static void Capabilities(){
  foreach(var kph in new[]{0,40,70,90}){var name="long-straight-"+kph;if(File.Exists(Program.Doc+"/diagnostics/"+name+"-result.json"))continue;var spec=StandingWorld144.Rider(StartingGate.B) with{SpeedMetersPerSecond=kph/3.6f};Execute(name,Circular(1200,31),10,spec,"gas1-straight");}
  foreach(var kph in new[]{70,90})foreach(var offset in new[]{2f,8f}){var name=$"turn-{kph}-offset-{offset:0}";if(File.Exists(Program.Doc+"/diagnostics/"+name+"-result.json"))continue;var f=StandingWorld144.Frame();var spec=StandingWorld144.Rider(StartingGate.B) with{SpeedMetersPerSecond=kph/3.6f,ReferenceMeters=f.SegmentStartMeters(1)-12,OffsetMeters=offset};Execute(name,f,15,spec,"causal-K1-diagnostic-rolling-fixture");}
 }
 public static void GeometryAudit(){
  using var b=new Budget148("geometry-curvature-audit",0,20);var f=StandingWorld144.Frame();var rows=new List<object>();double maxK=0;for(int i=0;i<2000;i++){var s=i*f.LapMeters/2000;var q=f.Shape!.At(s);maxK=Math.Max(maxK,q.Curvature);rows.Add(new{ReferenceMeters=s,q.Heading,q.Curvature,ReferenceRadius=q.Curvature>1e-8?1/q.Curvature:(double?)null,InnerEdgeRadius=q.Curvature>1e-8?1/q.Curvature-1:(double?)null,OuterEdgeRadius=q.Curvature>1e-8?1/q.Curvature+f.Shape.Width(s)-1:(double?)null,Width=f.Shape.Width(s)});}
  var joins=Enumerable.Range(0,9).Select(i=>{var s=f.SegmentStartMeters(i);var a=f.Shape!.At(s-1e-5);var c=f.Shape.At(s+1e-5);return new{Segment=i,Reference=s,HeadingJump=BikeAngles.Wrap(c.Heading-a.Heading),CurvatureBefore=a.Curvature,CurvatureAfter=c.Curvature,PositionGap=(c.Position-a.Position).Length};}).ToArray();
  Program.Save(Program.Doc+"/geometry-audit.json",new{f.LapMeters,MaximumCurvature=maxK,MinimumReferenceRadius=1/maxK,Joins=joins,Profile=rows,BankingModeled=false,VerticalLoadTransferModeled=false,GeneratorConstraint="Frozen progressive shape requires 35/62/27 and 318.77875 m. G0c/G1..G4 are closed circular diagnostic controls; G0 progressive remains main result.",CpuSeconds=b.Elapsed});b.Finish("read-only C2 geometry audit complete");
 }
}
