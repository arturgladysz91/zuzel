using SharedWorldSpeedway;
if(args.Length!=4||args[0]!="--enable-multi-action")throw new ArgumentException("Default OFF. Usage: --enable-multi-action <data> <raw> freeze|preflight|diagnostic|confirm");
if(args[3]=="finish-stopped-confirmation"){FinishAudit135.Run(args[1],args[2]);return;}
if(args[3]=="sequence-cost"){CostAudit135.Run(args[1]);return;}
if(args[3]=="mechanism-controls"){MechanismAudit135.Run(args[1],args[2]);return;}
MultiActionExperiment135.Run(args[1],args[2],args[3]);
