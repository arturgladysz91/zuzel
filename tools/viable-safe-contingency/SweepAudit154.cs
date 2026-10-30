using Autonomous146;
using Safety153;
using SharedWorldSpeedway;
namespace Viable154;
public static class SweepAudit154 {
 public static void Run(){using var budget=new Budget154("swept-two-cone-diagnostic-v1",0,25);Program154.Sources("swept-two-cone-diagnostic-v1");var run=Program154.Read<Run144>(Program153.Doc+"/native/ranked-S-v1.json.gz");var ss=run.Riders[0].Samples;var boundary=new RacingLineGeometry(NativeWorld153.Frame());var rows=new List<object>();int oldRejected=0,newRejected=0,recovered=0,poseRejected=0;double oldMinimum=double.PositiveInfinity,newMinimum=double.PositiveInfinity;
  for(int j=1;j<ss.Length;j++){budget.Guard();var x=ss[j-1];var y=ss[j];var a=new FreeRacingPoint(x.Input.Time,x.Input.Rider,x.Input.Omega,x.Input.Steering,x.Inner,x.Outer);var b=new FreeRacingPoint(y.Input.Time,y.Input.Rider,y.Input.Omega,y.Input.Steering,y.Inner,y.Outer);
   var old=Clearance153.SweepLowerBound(a.Rider,b.Rider,b.Time-a.Time,(a.Inner,a.Outer),(b.Inner,b.Outer));var tight=Sweep154.Interval(a,b);oldMinimum=Math.Min(oldMinimum,old);newMinimum=Math.Min(newMinimum,tight);
   if(tight+1e-12<old)throw new InvalidOperationException("Two-cone bound cannot be worse");
   if(Math.Min(Math.Min(a.Inner,a.Outer),Math.Min(b.Inner,b.Outer))>.9)continue;
   foreach(var shift in new[]{-.2,-.1,0,.1,.2}){var d=a.Rider.Road.Outward*(float)shift;var va=a.Rider with{Position=a.Rider.Position+d};var vb=b.Rider with{Position=b.Rider.Position+d};var ga=boundary.Clearance(va);var gb=boundary.Clearance(vb);var pa=a with{Rider=va,Inner=ga.Inner,Outer=ga.Outer};var pb=b with{Rider=vb,Inner=gb.Inner,Outer=gb.Outer};var lower=Clearance153.SweepLowerBound(va,vb,b.Time-a.Time,ga,gb);var newer=Sweep154.Interval(pa,pb);var minPose=Math.Min(Math.Min(ga.Inner,ga.Outer),Math.Min(gb.Inner,gb.Outer));if(lower<.5)oldRejected++;if(newer<.5)newRejected++;if(lower<.5&&newer>=.5)recovered++;if(minPose<.5)poseRejected++;rows.Add(new{Time=a.Time,ShiftWorldConstantMeters=shift,MinimumPose=minPose,Original=lower,TwoCone=newer,Recovered=lower<.5&&newer>=.5});}
  }
  Program154.Save("sweep-diagnostic.json",new{Passed=true,NativeIntervals=ss.Length-1,NativeOriginalMinimum=oldMinimum,NativeTwoConeMinimum=newMinimum,GeometryCounterfactuals=rows.Count,OldRejected=oldRejected,NewRejected=newRejected,Recovered=recovered,EndpointViolations=poseRejected,Rows=rows,Scope="Native153 intervals plus rigidly translated endpoint footprint geometry counterfactuals. These are not new native races or counts of all historical rejected predictions. Two-cone proof uses exactly the same held-wrench motion Lipschitz constant, dimensions and numerical tube as153. Native/forecast certificate remains153 until separately promoted and tested."});budget.Finish("two-endpoint Lipschitz bound diagnostic");Console.WriteLine("Sweep recovered "+recovered+" of "+oldRejected+" synthetic rejections; native min "+oldMinimum+" -> "+newMinimum+" CPU="+budget.Elapsed);
 }
}
