namespace SharedWorldSpeedway;

/// <summary>Outward enclosures for scalar contact certification.</summary>
public readonly record struct ContactInterval64(double Low, double High)
{
    private static ContactInterval64 Whole => new(double.NegativeInfinity,double.PositiveInfinity);
    public bool ExcludesZero => Low>0||High<0;
    public static implicit operator ContactInterval64(double x) => new(x,x);
    private static ContactInterval64 Out(double lo,double hi) =>
        double.IsNaN(lo)||double.IsNaN(hi)?Whole:new(Math.BitDecrement(lo),Math.BitIncrement(hi));
    public static ContactInterval64 operator +(ContactInterval64 a,ContactInterval64 b) => Out(a.Low+b.Low,a.High+b.High);
    public static ContactInterval64 operator -(ContactInterval64 a,ContactInterval64 b) => Out(a.Low-b.High,a.High-b.Low);
    public static ContactInterval64 operator -(ContactInterval64 a) => new(-a.High,-a.Low);
    public static ContactInterval64 operator *(ContactInterval64 a,ContactInterval64 b)
    {
        double p=a.Low*b.Low,q=a.Low*b.High,r=a.High*b.Low,s=a.High*b.High;
        return Out(Math.Min(Math.Min(p,q),Math.Min(r,s)),Math.Max(Math.Max(p,q),Math.Max(r,s)));
    }
    public static ContactInterval64 operator /(ContactInterval64 a,ContactInterval64 b) =>
        b.ExcludesZero?a*Out(1/b.High,1/b.Low):Whole;
    public ContactInterval64 Square()
    {
        double a=Low*Low,b=High*High;
        return Out(ExcludesZero?Math.Min(a,b):0,Math.Max(a,b));
    }
    public ContactInterval64 Sqrt() => Out(Math.Sqrt(Math.Max(0,Low)),Math.Sqrt(High));
    public ContactInterval64 Asin() => Out(Math.Asin(Low),Math.Asin(High));
    public ContactInterval64 Clamp(double lo,double hi) => new(Math.Clamp(Low,lo,hi),Math.Clamp(High,lo,hi));
    public ContactInterval64 Hull(ContactInterval64 b) => new(Math.Min(Low,b.Low),Math.Max(High,b.High));
    public ContactInterval64 Sin() => Low>=-Math.PI/2&&High<=Math.PI/2?Out(Math.Sin(Low),Math.Sin(High)):new(-1,1);
    public ContactInterval64 Cos() => Low>=-Math.PI&&High<=Math.PI
        ?Out(Math.Min(Math.Cos(Low),Math.Cos(High)),ExcludesZero?Math.Max(Math.Cos(Low),Math.Cos(High)):1):new(-1,1);
}

/// <summary>Value and derivative intervals along the axle acceleration coordinate.</summary>
public readonly record struct ContactSlope64(ContactInterval64 Value,ContactInterval64 Slope)
{
    public static implicit operator ContactSlope64(double scalar) => new(scalar,0);
    public static ContactSlope64 operator +(ContactSlope64 a,ContactSlope64 b) => new(a.Value+b.Value,a.Slope+b.Slope);
    public static ContactSlope64 operator -(ContactSlope64 a,ContactSlope64 b) => new(a.Value-b.Value,a.Slope-b.Slope);
    public static ContactSlope64 operator -(ContactSlope64 a) => new(-a.Value,-a.Slope);
    public static ContactSlope64 operator *(ContactSlope64 a,ContactSlope64 b) =>
        new(a.Value*b.Value,a.Slope*b.Value+a.Value*b.Slope);
    public static ContactSlope64 operator /(ContactSlope64 a,ContactSlope64 b) =>
        new(a.Value/b.Value,(a.Slope*b.Value-a.Value*b.Slope)/b.Value.Square());
    public ContactSlope64 Square() => new(Value.Square(),2*Value*Slope);
    public ContactSlope64 Sqrt()
    {
        var root=Value.Sqrt();
        return new(root,Slope/(2*root));
    }
    public ContactSlope64 Sin() => new(Value.Sin(),Value.Cos()*Slope);
    public ContactSlope64 Cos() => new(Value.Cos(),-Value.Sin()*Slope);
    public ContactSlope64 Asin() => new(Value.Asin(),Slope/(1-Value.Square()).Sqrt());
    public ContactSlope64 Clamp(double lo,double hi)
    {
        var val=Value.Clamp(lo,hi);
        if(Value.High<=lo||Value.Low>=hi) return new(val,0);
        return new(val,Value.Low>lo&&Value.High<hi?Slope:Slope.Hull(0));
    }
}
