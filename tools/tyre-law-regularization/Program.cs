using SharedWorldSpeedway;
if(args.Length!=3||args[0]!="--enable-tyre-separation140")throw new ArgumentException("Default OFF: --enable-tyre-separation140 <results.json> freeze|math|preflight|diagnostic|confirmation");
Experiment140.Run(args[1],args[2]);
