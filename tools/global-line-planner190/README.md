# Global Line Planner #190

Local opt-in experiment over the frozen SpeedwayHandling H25. The game and the
original harness are separate entry points. FAST50 uses 50 ms between reviews
and before command activation from the start. The inherited clutch release at
240 ms and original crank model remain in use.

From the repository root with .NET 8 and Python:

    python tools/global-line-planner190/prepare.py
    dotnet restore tools/global-line-planner190/global-line-planner190.csproj --locked-mode
    dotnet build tools/global-line-planner190/global-line-planner190.csproj -c Release --no-restore --warnaserror

    $dll = './tools/global-line-planner190/bin/Release/net8.0/global-line-planner190.dll'
    dotnet $dll --study-190 check
    dotnet $dll --study-190 plan <fresh-evidence-directory>

The plan command generates both catalogs, estimates a separate speed envelope
for each sector, evaluates linked cyclic pairs and forecasts a diverse shortlist
at 10 ms with H25 contact calls and the FAST50 activation queue. The fastest
eligible complete forecast supplies the cached reference. Live correction changes
the target speed smoothly as the predicted tracking state diverges.

The native command takes the arm name followed by the evidence directory and
runs one world. It stores a raw trace, summary and frozen-plan reservation. Use
baseline and planner in order. The confirmation arm is available only after a
paired result clears the sporting gate. It uses a distinct held start, so its
purpose within the three-world budget is completion confirmation.

The executed #190 evidence in the companion workspace's outputs/badanie190 used
two native worlds. The independent telemetry audit and plotting scripts are there.

