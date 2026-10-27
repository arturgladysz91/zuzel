using SharedWorldSpeedway;
if(args.Length!=3||args[0]!="--enable-racing-objective142")throw new ArgumentException("Default OFF: --enable-racing-objective142 <results.json> offline|freeze|preflight|branches|diagnostic|confirmation|recovery|analyze");
Experiment142.Run(args[1],args[2]);
