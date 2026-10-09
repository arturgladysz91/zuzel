namespace CoreSim.PhysicalSpace;

/// <summary>Fixed arithmetic for enabled contact frames; never rounds model outputs.
/// Native libm differs by an ulp on some track angles. Range-reduced Taylor
/// polynomials keep the detector's geometry and the analyzer's impulse identical
/// across platforms. Legacy frames retain their original native operations.</summary>
internal static class ContactFrameArithmetic
{
    internal static MeterPoint Direction(double angle, bool deterministic)
    {
        if (!deterministic) return new(Math.Cos(angle), Math.Sin(angle));
        var wrapped=BikeAngles.Wrap(angle);
        var quadrant=(int)Math.Round(wrapped/(Math.PI/2));
        var x=wrapped-quadrant*(Math.PI/2); // |x| <= pi/4
        var z=x*x;
        // Remainders on this interval are below 3e-18 before IEEE rounding.
        var sin=x*(1+z*(-1d/6+z*(1d/120+z*(-1d/5040+z*(1d/362880+z*(-1d/39916800
            +z*(1d/6227020800+z*(-1d/1307674368000+z/355687428096000))))))));
        var cos=1+z*(-1d/2+z*(1d/24+z*(-1d/720+z*(1d/40320+z*(-1d/3628800
            +z*(1d/479001600+z*(-1d/87178291200+z/20922789888000)))))));
        return ((quadrant%4+4)%4) switch
        { 0=>new(cos,sin),1=>new(-sin,cos),2=>new(-cos,-sin),_=>new(sin,-cos) };
    }

    internal static double Heading(double y,double x,bool deterministic)
    {
        if(!deterministic)return Math.Atan2(y,x);
        if(x==0)return y==0?Math.CopySign(0,y):Math.CopySign(Math.PI/2,y);
        var ax=Math.Abs(x);var ay=Math.Abs(y);
        var r=ax>=ay?ay/ax:ax/ay;
        var offset=0d;
        if(r>0.4142135623730950488){offset=Math.PI/4;r=(r-1)/(r+1);}
        var square=r*r;
        // Unrolled Horner form preserves the trace bits and avoids an array/loop
        // in the heavily sampled contact heading path.
        var polynomial=-1d/51;
        polynomial=1d/49+square*polynomial;
        polynomial=-1d/47+square*polynomial;
        polynomial=1d/45+square*polynomial;
        polynomial=-1d/43+square*polynomial;
        polynomial=1d/41+square*polynomial;
        polynomial=-1d/39+square*polynomial;
        polynomial=1d/37+square*polynomial;
        polynomial=-1d/35+square*polynomial;
        polynomial=1d/33+square*polynomial;
        polynomial=-1d/31+square*polynomial;
        polynomial=1d/29+square*polynomial;
        polynomial=-1d/27+square*polynomial;
        polynomial=1d/25+square*polynomial;
        polynomial=-1d/23+square*polynomial;
        polynomial=1d/21+square*polynomial;
        polynomial=-1d/19+square*polynomial;
        polynomial=1d/17+square*polynomial;
        polynomial=-1d/15+square*polynomial;
        polynomial=1d/13+square*polynomial;
        polynomial=-1d/11+square*polynomial;
        polynomial=1d/9+square*polynomial;
        polynomial=-1d/7+square*polynomial;
        polynomial=1d/5+square*polynomial;
        polynomial=-1d/3+square*polynomial;
        var angle=offset+r*(1+square*polynomial);
        if(ay>ax)angle=Math.PI/2-angle;
        if(x<0)angle=Math.PI-angle;
        return Math.CopySign(angle,y);
    }
}
