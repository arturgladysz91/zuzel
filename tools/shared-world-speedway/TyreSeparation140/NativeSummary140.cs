using System.Numerics;
using CoreSim.PhysicalSpace;
namespace SharedWorldSpeedway;

/// <summary>Compact, unit-bearing evidence over captures. Never creates a world.</summary>
public static class NativeSummary140
{
    public static object Build(NativeCaptureResult140 capture,NativeCase139 c,string mode,bool separated,double? preparation,double? opening,bool trace){
        var e=capture.Episode;var samples=e.Replay.Samples;var telemetry=e.Telemetry;var frame=TrackAwarenessExperiment134.Frame(c.Case.Geometry);
        var exit=SpeedThrottleAudit136.Exit(frame,c.Corner);double maxResidual=0,sumResidual=0,maxNormalized=0,maxGain=double.NegativeInfinity;
        double maxYawError=0,maxRoot=0,maxUse=0,maxFrontDrive=0,maxNegativeFrontLoss=0,maximumSlip=0,maximumTorque=0;int maximumIterations=0;
        var frontLoss=0d;var lateralLoss=0d;var longLoss=0d;var bearingLoss=0d;var resistanceWork=0d;var engineWork=0d;
        for(var j=0;j<telemetry.Length;j++){
            var t=telemetry[j];var f=t.Forces;var w=t.Wheel;var a=samples[j].Input.Rider;var b=samples[j+1].Input.Rider;var h=t.Seconds;
            var midVelocity=(a.Velocity+b.Velocity)/2;var midYaw=(a.AngularVelocity+b.AngularVelocity)/2;
            var vf=TwoContactForces.PointVelocity(midVelocity,midYaw,f.FrontArm);var vr=TwoContactForces.PointVelocity(midVelocity,midYaw,f.RearArm);
            var currentFrontLoss=-Vector2.Dot(f.FrontForce,vf)*h;
            var currentLateralLoss=-f.RearLateral*Vector2.Dot(vr,f.BodyLeft)*h;
            var currentLongLoss=f.RearLongitudinal*(.33*w.MidOmega-Vector2.Dot(vr,f.BodyForward))*h;
            var currentResistance=Vector2.Dot(f.Resistance,midVelocity)*h;
            var delta=t.BodyEnergyDelta+.45*(w.AfterOmega*w.AfterOmega-w.BeforeOmega*w.BeforeOmega);
            var residual=delta-w.EngineWorkJoules-currentResistance+currentFrontLoss+currentLateralLoss+currentLongLoss+w.BearingLossJoules;
            var bodyK=Math.Max(TwoContactBikeAudit.BodyKinetic(a,147,52.72097f),TwoContactBikeAudit.BodyKinetic(b,147,52.72097f));
            maxResidual=Math.Max(maxResidual,Math.Abs(residual));sumResidual+=residual;maxNormalized=Math.Max(maxNormalized,Math.Abs(residual)/(.05+1e-6*bodyK));
            maxGain=Math.Max(maxGain,delta-w.EngineWorkJoules);maxNegativeFrontLoss=Math.Max(maxNegativeFrontLoss,-currentFrontLoss);
            maxYawError=Math.Max(maxYawError,Math.Abs((b.AngularVelocity-a.AngularVelocity)-f.NetTorque/52.72097*h));
            maxRoot=Math.Max(maxRoot,Math.Abs(w.RootResidualMetersPerSecond));maxUse=Math.Max(maxUse,w.RearUtilization);maximumIterations=Math.Max(maximumIterations,w.RootIterations);
            maxFrontDrive=Math.Max(maxFrontDrive,Math.Abs(Vector2.Dot(f.FrontForce,new(f.FrontLeft.Y,-f.FrontLeft.X))));
            maximumSlip=Math.Max(maximumSlip,Math.Abs(w.InstantSlipRatio));maximumTorque=Math.Max(maximumTorque,Math.Abs(f.NetTorque));
            frontLoss+=currentFrontLoss;lateralLoss+=currentLateralLoss;longLoss+=currentLongLoss;bearingLoss+=w.BearingLossJoules;resistanceWork+=currentResistance;engineWork+=w.EngineWorkJoules;
        }
        object Point(int index){
            var s=samples[index];var i=s.Input;var v=i.Rider;var t=index>0?telemetry[index-1]:(RobustTelemetry?)null;
            double? chiRate=null,betaRate=null;
            if(index>0){var before=samples[index-1].Input.Rider;var h=t!.Seconds;
                chiRate=BikeAngles.Wrap(Math.Atan2(v.Velocity.Y,v.Velocity.X)-Math.Atan2(before.Velocity.Y,before.Velocity.X))/h;
                betaRate=BikeAngles.Wrap(v.SlideRadians-before.SlideRadians)/h;
            }
            return new{TimeSeconds=i.Time,ReferenceMeters=v.TotalReferenceMeters,SpeedMetersPerSecond=v.Speed,BetaRadians=v.SlideRadians,YawRateRadiansPerSecond=v.AngularVelocity,
                ChiRadians=Math.Atan2(v.Velocity.Y,v.Velocity.X),ChiRateFiniteDifferenceRadiansPerSecond=chiRate,BetaRateFiniteDifferenceRadiansPerSecond=betaRate,
                EstimatedBetaRateFromEndYaw=v.AngularVelocity-chiRate,OmegaRadPerSecond=i.Omega,SteeringRadians=i.Steering,Active=i.Active,
                Requested=new FreeRacingAction(s.RequestedSteering,s.RequestedDrive),InnerClearanceMeters=s.Inner,OuterClearanceMeters=s.Outer,
                PreviousIntervalForces=t?.Forces,PreviousIntervalWheel=t?.Wheel};
        }
        object? At(double reference){for(var j=0;j<samples.Length;j++)if(samples[j].Input.Rider.TotalReferenceMeters>=reference)return Point(j);return null;}
        var maxBetaIndex=Enumerable.Range(0,samples.Length).MaxBy(j=>Math.Abs(samples[j].Input.Rider.SlideRadians));
        var reaction=e.Replay.Activations.Select(a=>Math.Abs(a.ActivationAt-a.ObservedAt-.24)).DefaultIfEmpty(0).Max();
        var reviews=e.Replay.Decisions.Zip(e.Replay.Decisions.Skip(1),(a,b)=>Math.Abs(b.Time-a.Time-.2)).DefaultIfEmpty(0).Max();
        var retained=trace?Enumerable.Range(0,samples.Length).Where(j=>j%10==0||j==samples.Length-1||j==maxBetaIndex).Select(Point).ToArray():null;
        return new{Name=e.Replay.Name,Model=separated?"separated140":"original139",Mode=mode,Case=c,Initial=samples[0].Input,
            PreObservationInitialPendingCommands=Array.Empty<FreeRacingCommand>(),NoteInitial="Recorded Input at0 is annotated with decision0 pending; actual constructor starts with an empty queue and Active(0,1).",
            PreparationRequestTimeSeconds=preparation,OpenRequestTimeSeconds=opening,FullActivationTimeSeconds=opening+.24,
            capture.HistoricalCompleted,capture.HistoricalGateSeconds,FullNextStraightCertified=e.Replay.Metrics.Completed&&mode!="off",
            e.Replay.Metrics,Exit=At(exit),Straight20Meters=At(exit+20),Straight40Meters=At(exit+40),Straight62Meters=At(exit+62),
            MaximumAbsYawTorqueNewtonMeters=maximumTorque,MaximumAbsoluteWheelSlipRatio=maximumSlip,MaximumRearUtilization=maxUse,
            MaximumFrontDriveNewtons=maxFrontDrive,MaximumSolverResidualMetersPerSecond=maxRoot,MaximumSolverIterations=maximumIterations,
            MaximumEnergyLedgerResidualJoules=maxResidual,SumSignedEnergyLedgerResidualJoules=sumResidual,MaximumEnergyLedgerToleranceRatio=maxNormalized,
            MaximumEnergyGainBeyondDriveJoules=maxGain,MaximumNegativeFrontMidpointDissipationJoules=maxNegativeFrontLoss,
            EnergyLedgerPassed=maxNormalized<=1,MaximumYawUpdateErrorRadiansPerSecond=maxYawError,MaximumReactionErrorSeconds=reaction,MaximumReviewIntervalErrorSeconds=reviews,
            Work=new{EngineJoules=engineWork,ResistanceJoules=resistanceWork,FrontSlipJoules=frontLoss,RearLateralSlipJoules=lateralLoss,RearLongitudinalSlipJoules=longLoss,BearingJoules=bearingLoss},
            EnergyMeaning="DeltaK(body+wheel)-driveWork-resistanceWork+slipLosses+bearingLoss. Contact work uses midpoint body velocities and original held world arms; all intervals retained in extrema/sums. Float/Box2D tolerance .05J+1e-6*K_body.",
            PeakBeta=Point(maxBetaIndex),LastTwo=e.Replay.Metrics.Outcome=="completed"?null:Enumerable.Range(Math.Max(0,samples.Length-2),Math.Min(2,samples.Length)).Select(Point).ToArray(),
            Trace=retained,TraceMeaning=trace?"One representative D0 long DNF and one D0 short continuation, sampled every10 accepted steps plus extrema/end. No replay export.":null,
            FullCaptureFingerprint=e.Replay.Metrics.Fingerprint,DecisionCount=e.Replay.Decisions.Length,ActivationCount=e.Replay.Activations.Length,
            capture.NativeContacts,capture.BoundaryContacts};
    }
}
