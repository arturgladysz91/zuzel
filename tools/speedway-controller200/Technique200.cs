using CoreSim.PhysicalSpace;
using SharedWorldSpeedway;

namespace SpeedwayHandling;

// Tracks the measured consequences of the accepted controls in a single rollout.
// The base corridor still verifies direction and clearance at every integration step.
internal sealed class Technique200(Window199[] windows)
{
    internal Cursor Begin()=>new(windows);
    internal sealed class Cursor(Window199[] windows)
    {
        private int index;
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
                    stableSince=Math.Abs(yaw)<.10 && Math.Abs(course)<3*Math.PI/180?stableSince??time:null;
                    settled=stableSince is {} stable && time-stable>=.12;
                }
                else if(Math.Abs(yaw)>=.16)
                {
                    int sign=Math.Sign(yaw);
                    if(correctionSince is null || sign!=correctionSign)
                    {
                        correctionSign=sign;
                        correctionSince=time;
                        correctionCourse=course;
                    }
                    if(time-correctionSince>=.12 && Math.Abs(BikeAngles.Wrap(course-correctionCourse))>Math.PI/90)
                        return "technique-correction";
                }
                else correctionSince=null;
            }
            if(s>=window.End-30 && s<=window.End+8 && !turned)
            {
                turnSince=yaw>=.25?turnSince??time:null;
                turned=turnSince is {} turn && time-turn>=.12;
            }
            if(s>=window.End-22 && s<=window.End+70 && !slid)
            {
                slipSince=Math.Abs(body.SlideRadians)>=Math.PI/30?slipSince??time:null;
                slid=slipSince is {} slide && time-slide>=.12;
            }
            return null;
        }
    }
}
