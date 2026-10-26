using SharedWorldSpeedway;
if(args.Length!=4||args[0]!="--enable-speed-throttle")
    throw new ArgumentException("Default OFF. Usage: --enable-speed-throttle <evidence> <raw> freeze|preflight|diagnostic|confirm");
SpeedThrottleAudit136.Run(args[1],args[2],args[3]);
