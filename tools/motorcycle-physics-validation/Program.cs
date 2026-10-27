using SharedWorldSpeedway;
if(args.Length!=4||args[0]!="--enable-physics-validation139")throw new ArgumentException("Default OFF: --enable-physics-validation139 <evidence> <raw> freeze|maps|preflight|diagnostic|sensitivity|confirmation");
PhysicsExperiment139.Run(args[1],args[2],args[3]);
