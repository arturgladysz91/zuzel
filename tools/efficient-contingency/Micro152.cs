using System.Diagnostics;
using SharedWorldSpeedway;
namespace Efficient152;
public static class Micro152 {
 public static void Run(){using var budget=new Budget152("prepared-wheel-bit-parity-v1",0,10);int count=0;var p=RearWheelParameters.Default;
  foreach(var u in new[]{0d,.001,1,10,30})foreach(var side in new[]{-10d,0,1,10})foreach(var omega in new[]{0d,20,100,1000})foreach(var h in new[]{0f,.005f,.01f,.02f})foreach(var torque in new[]{-200d,0,200}){
   var a=new PreparedWheelA152(u,side,omega,1800,147,.16,-80,h,p,1000);var raw=RearWheelDynamics.Evaluate(u,side,omega,1800,147,.16,torque,Math.Abs(torque),torque,-80,h,p,1000);
   var b=new PreparedWheelB152(u,side,omega,1800,147,-80,h,p,700,1.28);var old=Wheel149.Evaluate(u,side,omega,1800,147,.16,torque,Math.Abs(torque),torque,-80,h,p,700,1.28);
   if(!Bits152.Wheel(a.Evaluate(torque,Math.Abs(torque),torque),raw)||!Bits152.Wheel(b.Evaluate(torque,Math.Abs(torque),torque),old))throw new InvalidOperationException($"Kernel mismatch at {u},{side},{omega},{h},{torque}");count+=2;
  }
  Program152.Save("kernel-parity.json",new{Pairs=count,AllBitsEqual=true,Scope="Every RearWheelStep field, including signed zero, iterations, residuals and energy. Original A/B equations and parameters unchanged.",Sources=Program152.Sources()});budget.Finish("prepared A/B contact kernels bit-identical");Console.WriteLine("Kernel parity "+count+" pairs CPU="+budget.Elapsed);
 }
}
