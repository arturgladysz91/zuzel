using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Autonomous146;
using Efficient147;
using SharedWorldSpeedway;
if(args.Length==2&&args[0]=="--finalize147"){Finalize147.Run(args[1]);return;}
if(args.Length==3&&args[0]=="--enable-native147"){Native147.Run(args[1],args[2]);return;}
if(args.Length==2&&args[0]=="--compare147"){Benchmark147.Run(args[1]);return;}
if(args.Length==2&&args[0]=="--profile-replay147"){ReplayProfile147.Run(args[1]);return;}
if(args.Length!=2||args[0]!="--profile-cold147")throw new ArgumentException("Default OFF: --profile-cold147 <new-directory>");
var output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);var file=Path.Combine(output,"cold-profile.json");if(File.Exists(file))throw new IOException("Preserve prior profile.");
var process=Process.GetCurrentProcess();var cpu=process.TotalProcessorTime;
using var z=new GZipStream(File.OpenRead("docs/experiments/autonomous-corner-planning/native/autonomous146-viable.json.gz"),CompressionMode.Decompress);
var run=JsonSerializer.Deserialize<Run144>(z,FreeLearningExperiment.Json)!;var sample=run.Riders[0].Samples[0];var frame=StandingWorld144.Frame();var spec=run.Riders[0].Spec;
var input=sample.Input with{Time=0,NextReview=0,Pending=Array.Empty<FreeRacingCommand>()};
var profiler=new CpuProfile147();var planner=new Planner146(spec,frame,MultiActionExperiment135.Surface(frame),FreeLearningExperiment.Parameters(),new(Enabled:true),
    budgetGuard:()=>{if((process.TotalProcessorTime-cpu).TotalSeconds>60)throw new InvalidOperationException("Study147 profile CPU cap");},
    hooks:new(Phase:profiler.Phase,Operation:profiler.Operation));
var decision=planner.Choose(input,new CrankClutch144(omega:sample.CrankOmega));var measured=profiler.Snapshot();
var result=new{NativeWorlds=0,CpuSeconds=(process.TotalProcessorTime-cpu).TotalSeconds,decision.Choice.Action,OriginalAction=run.Riders[0].Decisions[0].Choice.Action,CommandsMatch=decision.Choice.Action==run.Riders[0].Decisions[0].Choice.Action,
    decision.Choice.Forecasts,decision.Choice.ForceEvaluations,planner.SeedDecisions,Profile=measured,Review=new{planner.Reviews[0].Candidates,planner.Reviews[0].Reason,planner.Reviews[0].Selected!.GoalTime,planner.Reviews[0].Selected!.Safety}};
File.WriteAllText(file,JsonSerializer.Serialize(result,FreeLearningExperiment.Json));Console.WriteLine(File.ReadAllText(file));
