using SharedWorldSpeedway;
if(args.Length!=4||args[0]!="--enable-throttle-steering-decoupling")throw new ArgumentException("Default OFF: --enable-throttle-steering-decoupling <evidence> <raw> freeze|offline|preflight|diagnostic|selection|confirmation");
ThrottleSteering138.Run(args[1],args[2],args[3]);
