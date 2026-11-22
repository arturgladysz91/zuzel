using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

// Tracks the measured consequences of the accepted controls in a single rollout.
// The base corridor still verifies direction and clearance at every integration step.
internal sealed class Technique200(Window199[] windows)
{
    internal const double StableYawLimit=.10,CorrectionYawThreshold=.16,TurnYawThreshold=.25;
    internal const double RequiredSeconds=.12,StableCourseLimit=3*Math.PI/180,CorrectionCourseLimit=Math.PI/90;
    internal const double SlipThreshold=Math.PI/30;

    internal readonly record struct Checkpoint(int Index,double? StableSince,double? CorrectionSince,double? TurnSince,
        double? SlipSince,bool Settled,bool Turned,bool Slid,double CorrectionCourse,int CorrectionSign);

    internal Cursor Begin(double reference=double.NegativeInfinity)=>new(windows,reference);
    internal Cursor Restore(Checkpoint state)=>new(windows,state);
    internal sealed class Cursor
    {
        private readonly Window199[] windows;
        private int index;
        internal Cursor(Window199[] windows,double reference)
        {
            this.windows=windows;
            while(index<windows.Length && reference>windows[index].End+78)index++;
        }
        internal Cursor(Window199[] windows,Checkpoint snapshot)
        {
            this.windows=windows;
            (index,stableSince,correctionSince,turnSince,slipSince,settled,turned,slid,correctionCourse,correctionSign)=snapshot;
        }
        internal Checkpoint Capture()=>new(index,stableSince,correctionSince,turnSince,slipSince,
            settled,turned,slid,correctionCourse,correctionSign);
        internal string? Finish()=>index>=windows.Length?null:
            !turned||index<windows.Length-1?"technique-turn":!slid?"technique-slip":null;

        private double? stableSince, correctionSince, turnSince, slipSince;
        private bool settled, turned, slid;
        private double correctionCourse;
        private int correctionSign;

        internal string? Observe(RiderView body,double time)
        {
            double s=body.TotalReferenceMeters;
            while(index<windows.Length && s>windows[index].End+78)
            {
                if(!turned || !slid)return !turned?"technique-turn":"technique-slip";
                index++;
                stableSince=correctionSince=turnSince=slipSince=null;
                settled=turned=slid=false;
            }
            if(index>=windows.Length)return null;
            var window=windows[index];
            double yaw=body.AngularVelocity;
            double course=Corridor199.CourseError(body);
            if(s>=window.Start+12 && s<=window.End-12)
            {
                if(!settled)
                {
                    stableSince=Math.Abs(yaw)<StableYawLimit && Math.Abs(course)<StableCourseLimit?stableSince??time:null;
                    settled=stableSince is {} stable && time-stable>=RequiredSeconds;
                }
                else if(Math.Abs(yaw)>=CorrectionYawThreshold)
                {
                    int sign=Math.Sign(yaw);
                    if(correctionSince is null || sign!=correctionSign)
                    {
                        correctionSign=sign;
                        correctionSince=time;
                        correctionCourse=course;
                    }
                    if(time-correctionSince>=RequiredSeconds && Math.Abs(BikeAngles.Wrap(course-correctionCourse))>CorrectionCourseLimit)
                        return "technique-correction";
                }
                else correctionSince=null;
            }
            if(s>=window.End-30 && s<=window.End+8 && !turned)
            {
                turnSince=yaw>=TurnYawThreshold?turnSince??time:null;
                turned=turnSince is {} turn && time-turn>=RequiredSeconds;
            }
            if(s>=window.End-22 && s<=window.End+70 && !slid)
            {
                slipSince=Math.Abs(body.SlideRadians)>=SlipThreshold?slipSince??time:null;
                slid=slipSince is {} slide && time-slide>=RequiredSeconds;
            }
            return null;
        }
    }
}
