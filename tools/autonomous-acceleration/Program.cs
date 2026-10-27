using SharedWorldSpeedway;
if(args.Length!=3||args[0]!="--enable-autonomous-acceleration141")throw new ArgumentException("Default OFF: --enable-autonomous-acceleration141 <results.json> freeze|preflight|diagnostic|confirmation");
Experiment141.Run(args[1],args[2]);
