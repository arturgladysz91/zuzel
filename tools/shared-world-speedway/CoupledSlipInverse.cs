using System.Numerics;

namespace SharedWorldSpeedway;

public enum SlipInverseStatus { Solved, Unreachable, Ambiguous }
public readonly record struct SlipInverseResult(SlipInverseStatus Status,float BetaRadians,double RequiredNewtons,
    double AchievedNewtons,double MinimumNewtons,double MaximumNewtons,int Roots,int ContactEvaluations);
public readonly record struct CoupledSlipInverseObservation(double TimeSeconds,int RiderId,float LegacyBetaRadians,
    float AppliedBetaRadians,SlipInverseResult Result);

/// <summary>Tools-only inverse of the ORIGINAL finite-step contact. No body/wheel commit.</summary>
public static class CoupledSlipInverse
{
    public const double MaximumBetaRadians=Math.PI/4;
    public const int AngularIntervals=24;
    public const int ExtremumIterations=20;
    public const int RootIterations=24;
    public const double ForceToleranceNewtons=.05;
    public const double EnvelopeUncertaintyNewtons=.25;
    public const double AngleToleranceRadians=1e-6;

    public readonly record struct Contact(Vector2 Velocity,Vector2 Resistance,float Budget,float Mass,double Omega,
        double RequestedTorque,double AvailableTorque,double DriveTorque,float Seconds,RearWheelParameters Wheel)
    {
        public RearWheelStep At(double beta,out Vector2 force)
        {
            var heading=MathF.Atan2(Velocity.Y,Velocity.X)+(float)beta;
            var forward=new Vector2(MathF.Cos(heading),MathF.Sin(heading));var side=new Vector2(-forward.Y,forward.X);
            var u=Vector2.Dot(Velocity,forward);var w=Vector2.Dot(Velocity,side);
            var contact=RearWheelDynamics.Evaluate(u,w,Omega,Budget,Mass,SlipCoupledDynamics.VelocityScaleSquared(u,Budget/Mass),
                RequestedTorque,AvailableTorque,DriveTorque,Vector2.Dot(Resistance,forward),Seconds,Wheel);
            force=forward*contact.LongitudinalNewtons+side*contact.LateralNewtons+Resistance;
            return contact;
        }
        public double NormalForce(double beta)
        {
            _=At(beta,out var force);
            return Vector2.Dot(force,new Vector2(-Velocity.Y,Velocity.X)/Velocity.Length());
        }
    }

    public static SlipInverseResult Solve(Contact contact,double requiredNewtons)
    {
        if(!double.IsFinite(requiredNewtons)||!float.IsFinite(contact.Budget)||contact.Budget<=0||contact.Mass<=0
            ||!double.IsFinite(contact.Omega)||contact.Seconds is <0 or >.020001f)throw new ArgumentOutOfRangeException(nameof(contact));
        if(contact.Velocity.Length()<SlipCoupledDynamics.LowSpeedMetersPerSecond)
            return new(SlipInverseStatus.Ambiguous,0,requiredNewtons,0,0,0,0,0);
        // A bounded numerical envelope, refined at EVERY detected local extremum.
        // Tangencies/failed brackets remain ambiguous. No monotone-force assumption.
        Span<double> angles=stackalloc double[AngularIntervals*2+1];
        Span<double> forces=stackalloc double[AngularIntervals*2+1];
        var count=AngularIntervals+1;var evaluations=0;
        double Evaluate(double beta){evaluations++;return contact.NormalForce(beta);}
        for(var i=0;i<count;i++){angles[i]=-MaximumBetaRadians+2*MaximumBetaRadians*i/AngularIntervals;forces[i]=Evaluate(angles[i]);}
        var minimum=forces[0];var maximum=forces[0];
        for(var i=1;i<=AngularIntervals;i++){minimum=Math.Min(minimum,forces[i]);maximum=Math.Max(maximum,forces[i]);}
        for(var i=1;i<AngularIntervals;i++){
            var high=forces[i]>forces[i-1]&&forces[i]>forces[i+1];
            var low=forces[i]<forces[i-1]&&forces[i]<forces[i+1];
            if(!high&&!low)continue;
            var left=angles[i-1];var right=angles[i+1];const double phi=.6180339887498949;
            var a=right-phi*(right-left);var b=left+phi*(right-left);var fa=Evaluate(a);var fb=Evaluate(b);
            for(var k=0;k<ExtremumIterations;k++){
                if((fa<fb)==high){left=a;a=b;fa=fb;b=left+phi*(right-left);fb=Evaluate(b);}
                else{right=b;b=a;fb=fa;a=right-phi*(right-left);fa=Evaluate(a);}
            }
            var beta=(a+b)/2;var value=Evaluate(beta);angles[count]=beta;forces[count++]=value;
            minimum=Math.Min(minimum,value);maximum=Math.Max(maximum,value);
        }
        // Insertion sort over stack memory; no allocations or history dependence.
        for(var i=1;i<count;i++){var a=angles[i];var f=forces[i];var j=i;
            while(j>0&&angles[j-1]>a){angles[j]=angles[j-1];forces[j]=forces[j-1];j--;}
            angles[j]=a;forces[j]=f;}
        var roots=0;var best=double.PositiveInfinity;var achieved=0d;
        void Accept(double beta,double force){
            if(Math.Abs(force-requiredNewtons)>ForceToleranceNewtons)return;
            roots++;
            // Least |beta|; exact ties use the smaller signed angle. This avoids
            // selecting the high-slide branch just because it was visited first.
            if(Math.Abs(beta)<Math.Abs(best)||Math.Abs(beta)==Math.Abs(best)&&beta<best){best=beta;achieved=force;}
        }
        for(var i=0;i<count;i++)Accept(angles[i],forces[i]);
        for(var i=1;i<count;i++){
            var left=angles[i-1];var right=angles[i];var fl=forces[i-1]-requiredNewtons;var fr=forces[i]-requiredNewtons;
            if(fl*fr>=0)continue;
            for(var k=0;k<RootIterations;k++){
                var beta=(left+right)/2;var force=Evaluate(beta);var residual=force-requiredNewtons;
                if(Math.Abs(residual)<=ForceToleranceNewtons){Accept(beta,force);break;}
                if(fl*residual<=0){right=beta;fr=residual;}else{left=beta;fl=residual;}
                if(right-left<AngleToleranceRadians)break;
            }
        }
        if(roots>0)return new(SlipInverseStatus.Solved,(float)best,requiredNewtons,achieved,minimum,maximum,roots,evaluations);
        var outside=requiredNewtons<minimum-EnvelopeUncertaintyNewtons||requiredNewtons>maximum+EnvelopeUncertaintyNewtons;
        return new(outside?SlipInverseStatus.Unreachable:SlipInverseStatus.Ambiguous,0,requiredNewtons,0,minimum,maximum,0,evaluations);
    }
}
