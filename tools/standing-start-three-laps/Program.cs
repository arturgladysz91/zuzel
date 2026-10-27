using SharedWorldSpeedway;
if(args.Length!=3||args[0]!="--enable-standing144")throw new ArgumentException("Default OFF: --enable-standing144 <new-output-directory> A|B|F|K1|A-repeat");
Experiment144.Run(Path.GetFullPath(args[1]),args[2]);
