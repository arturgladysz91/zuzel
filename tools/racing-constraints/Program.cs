using SharedWorldSpeedway;
if(args.Length!=4||args[0]!="--enable-constraints143")throw new ArgumentException("Default OFF: --enable-constraints143 <results.json> <raw-directory> offline|freeze|preflight|isolated|combinations|diagnostics|confirmation|benchmark");
Experiment143.Run(args[1],args[2],args[3]);
