"""Independent decimal constitutive-law evaluation of an archived H28-F guard query."""
from decimal import Decimal as D, localcontext
import gzip
import json
from pathlib import Path
import sys

PI = D('3.141592653589793238462643383279502884197169399375105820974944592307816406286')


def exact(number):
    return D.from_float(number) if isinstance(number, float) else D(number)


def clamp(value, low, high):
    return max(low, min(high, value))


def sine_cosine(x):
    sine, cosine = x, D(1)
    s, c = sine, cosine
    for i in range(1, 90):
        s *= -x*x / D(2*i*(2*i+1))
        c *= -x*x / D((2*i-1)*2*i)
        sine += s
        cosine += c
        if abs(s) + abs(c) < D('1e-74'):
            return sine, cosine
    raise ArithmeticError('Trigonometric series did not converge')


def atan(x):
    if abs(x) > D('.22'):
        return 2 * atan(x / (1 + (1+x*x).sqrt()))
    term = total = x
    for i in range(1, 180):
        term *= -x*x
        add = term / (2*i+1)
        total += add
        if abs(add) < D('1e-74'):
            return total
    raise ArithmeticError('Arctangent series did not converge')


def asin(x):
    return atan(x / (1 - x*x).sqrt())


def residual_function(inputs):
    everything = {key: exact(value) for key,value in inputs.items()
                  if isinstance(value, (int,float)) and not isinstance(value,bool)}
    g = everything.__getitem__
    wheels = {k: exact(v) for k,v in inputs['Wheel'].items()}
    clutch = {k: exact(v) for k,v in inputs['Clutch'].items()}
    radius = wheels['WheelRadiusMeters']
    inertia = wheels['WheelInertiaKgMetersSquared']
    dt = g('Dt')
    half = dt/2
    bearing = 1 + half*wheels['BearingResistanceNewtonMeterSeconds']/inertia
    mobility = half*radius*radius/(inertia*bearing)
    b = g('RearNormalizedSide')
    rear_forward = g('RearForward')
    scale0 = wheels['LongitudinalSlipStiffnessRatio']**2 * (rear_forward**2 + wheels['LowSpeedMetersPerSecond']**2)
    relax = (radius**2 / inertia + 1/g('Mass')) * wheels['MinimumContactRelaxationSeconds']
    temp = clamp((g('ShaftOmega')*30/PI-clutch['DisengagedBelowRpm']) /
                 (clutch['FullyEngagedAboveRpm']-clutch['DisengagedBelowRpm']), D(0), D(1))
    engage = temp*temp*(3-2*temp) if inputs['Released'] else D(0)
    capacity = clutch['ClutchCapacityNewtonMeters']*engage
    viscosity = clutch['ClutchViscosityNewtonMeterSeconds']*engage
    throttle = g('Throttle')
    crank_c = 2*clutch['CrankInertiaKgMetersSquared']/dt
    knots = [(exact(k['Rpm'])*PI/30, exact(k['NewtonMeters'])) for k in inputs['EngineKnots']]
    lines = []
    for i,(low,a) in enumerate(knots):
        hi,z = knots[i+1] if i+1 < len(knots) else (D('Infinity'),D(0))
        slope = (z-a)/(hi-low) if hi.is_finite() else D(0)
        lines.append((low,hi,crank_c*g('ShaftOmega')+throttle*(a-slope*low),
                      crank_c+g('NetViscosity')-throttle*slope))
    u,v,speed = g('FrontU'),g('FrontV'),g('FrontSpeed')
    angle = atan(v/u)
    if u < 0:
        angle += PI if v >= 0 else -PI
    front_relax = (g('TotalCapacity')/g('Mass')*D('.03'))**2
    front_slip = g('FrontSlipRatio')
    inverse_scale = (front_slip**2*(speed**2+D('.25'))+front_relax).sqrt()

    def evaluate(ax):
        rear_normal = g('Normal') * ((g('FrontAxle') + ax*g('Height')/g('Gravity'))/(g('FrontAxle')+g('RearAxle')))
        rear = rear_normal*g('Grip')
        front = (g('Normal')-rear_normal)*g('Grip')
        scale = (scale0+(rear*relax)**2).sqrt()
        d2 = scale**2*(1+b*b)

        def wheel(torque):
            drive = torque*g('Ratio')*(g('Efficiency') if torque >= 0 else 1/g('Efficiency'))
            free = (g('WheelOmega')+half*drive/inertia)/bearing
            slip = radius*free-rear_forward-half*ax
            magnitude = abs(slip)
            z = magnitude/(1+mobility*rear/d2.sqrt())
            for _ in range(32):
                norm = (d2+z*z).sqrt()
                failure = z + mobility*rear*z/norm - magnitude
                if abs(failure)<D('1e-65'):
                    break
                z -= failure / (1+mobility*rear*d2/(norm**3))
            else:
                raise ArithmeticError('Independent wheel residual did not converge')
            z = z.copy_sign(slip)
            denominator = (1+(z/scale)**2+b*b).sqrt()
            fx = rear*z/scale/denominator
            return fx, -rear*b/denominator, free-half*radius*fx/(inertia*bearing)

        def C(torque):
            for low,hi,constant,slope in lines:
                engine = (constant-torque)/slope
                if low <= engine <= hi:
                    break
            else:
                raise ArithmeticError('No decimal crank segment')
            fx,fy,mid = wheel(torque)
            return torque-clamp(viscosity*(engine-g('Ratio')*mid),-capacity,capacity), fx, fy, mid, engine

        if capacity == 0:
            torque = D(0)
        else:
            low,high = -capacity,capacity
            a,z = C(low)[0],C(high)[0]
            if a == 0:
                high = low
            elif z == 0:
                low = high
            else:
                if a > 0 or z < 0:
                    raise ArithmeticError('Unbracketed independent clutch solve')
                for _ in range(230):
                    mid = (low+high)/2
                    y = C(mid)[0]
                    if abs(y) < D('1e-61'):
                        low=high=mid
                        break
                    if y < 0:
                        low=mid
                    else:
                        high=mid
            torque = (low+high)/2
        clutch_residual,fx,fy,omega,engine = C(torque)
        target = (g('TargetYaw')-g('RearMomentArm')*fy)/max(D('.1'),g('FrontAxle'))
        steer = g('SteerRequest')
        for _ in range(3):
            utilization = clamp(target/(front*sine_cosine(steer)[1]),-D('.98'),D('.98'))
            slip_angle = inverse_scale*utilization/(1-utilization**2).sqrt()
            steer = clamp(angle+asin(clamp(slip_angle/speed,-D('.98'),D('.98'))),-g('MaxSteer'),g('MaxSteer'))
        travel = clamp(steer-g('SteerBefore'),-g('SteerTravel'),g('SteerTravel'))
        sn,cs = sine_cosine(g('SteerBefore')+travel/2)
        local_u,local_v = u*cs+v*sn,v*cs-u*sn
        d = (local_v**2 + front_slip**2*(local_u**2+D('.25'))+front_relax).sqrt()
        front_fx = sn*front*local_v/d
        return {'residual': ax-(fx+front_fx+g('Drag'))/g('Mass'), 'steer': steer,
                'steer_travel':travel, 'rear_force': fx, 'front_projection': front_fx, 'rear_normal':rear_normal,
                'torque':torque, 'clutch_residual':clutch_residual, 'wheel_mid':omega, 'engine_mid':engine}

    return evaluate


class Box:
    """Small outward-padded Decimal boxes for the local anchored diagnostic."""
    pad = D('1e-60')

    def __init__(self, low, high=None):
        self.lo=low-self.pad
        self.hi=(low if high is None else high)+self.pad

    @staticmethod
    def cast(x):
        return x if isinstance(x,Box) else Box(D(x))

    def __add__(self,y):
        y=self.cast(y)
        return Box(self.lo+y.lo,self.hi+y.hi)
    __radd__=__add__

    def __neg__(self):
        return Box(-self.hi,-self.lo)

    def __sub__(self,y):
        return self+-self.cast(y)

    def __rsub__(self,y):
        return self.cast(y)+-self

    def __mul__(self,y):
        y=self.cast(y)
        p=[a*b for a in (self.lo,self.hi) for b in (y.lo,y.hi)]
        return Box(min(p),max(p))
    __rmul__=__mul__

    def __truediv__(self,y):
        y=self.cast(y)
        if y.lo <= 0 <= y.hi:
            raise ArithmeticError('Interval divisor includes zero')
        return self*Box(1/y.hi,1/y.lo)

    def square(self):
        a,b=self.lo**2,self.hi**2
        return Box(0 if self.lo<=0<=self.hi else min(a,b),max(a,b))

    def sqrt(self):
        return Box(max(D(0),self.lo).sqrt(),self.hi.sqrt())

    def clip(self,lo,hi):
        return Box(clamp(self.lo,lo,hi),clamp(self.hi,lo,hi))

    def trig(self,index):
        center=(self.lo+self.hi)/2
        radius=(self.hi-self.lo)/2
        value=sine_cosine(center)[index]
        return Box(value-radius,value+radius)


def anchored_box(inputs, low, high, mid, anchor):
    """Enclose one rejected box for the fixed captured query constants."""
    g=lambda k: exact(inputs[k])
    w=lambda k: exact(inputs['Wheel'][k])
    m,dt=g('Mass'),g('Dt')
    half=dt/2
    radius=w('WheelRadiusMeters')
    iw=w('WheelInertiaKgMetersSquared')
    bearing=1+half*w('BearingResistanceNewtonMeterSeconds')/iw
    mobility=half*radius**2/(iw*bearing)
    relax=(radius**2/iw+1/m)*w('MinimumContactRelaxationSeconds')
    grad=g('Grip')*m*g('Height')/(g('FrontAxle')+g('RearAxle'))
    full=g('TotalCapacity')
    side=g('RearNormalizedSide')
    c=w('LongitudinalSlipStiffnessRatio')**2*(g('RearForward')**2+w('LowSpeedMetersPerSecond')**2)
    ax=Box(low,high)
    rear=(g('FrontAxle')+ax*(g('Height')/g('Gravity')))*(g('Normal')*g('Grip')/(g('FrontAxle')+g('RearAxle')))
    sensitivity=full/((1+side**2)*c).sqrt()
    halfwidth=max(mid-low,high-mid)
    # The common constitutive derivative bound accounts for passive wheel/clutch
    # feedback. Extra anchor pads dominate residual closure at 76 digits.
    force_radius=(grad+half*sensitivity)*halfwidth+D('1e-12')
    fx=Box(anchor['rear_force']-force_radius,anchor['rear_force']+force_radius)
    slip_mid=radius*anchor['wheel_mid']-g('RearForward')-half*mid
    slip_radius=half*halfwidth+mobility*force_radius+D('1e-12')
    slip=Box(slip_mid-slip_radius,slip_mid+slip_radius)
    scale=(c+(rear*relax).square()).sqrt()
    fy=(-side)*rear*scale/((1+side**2)*scale.square()+slip.square()).sqrt()
    front=full-rear
    target=(g('TargetYaw')-g('RearMomentArm')*fy)/max(D('.1'),g('FrontAxle'))
    u,v,spd=g('FrontU'),g('FrontV'),g('FrontSpeed')
    theta=atan(v/u)+(PI if v>=0 else -PI) if u<0 else atan(v/u)
    fl=g('FrontSlipRatio')
    res=(full/m*D('.03'))**2
    ini=(fl*fl*(spd*spd+D('.25'))+res).sqrt()
    steer=Box(g('SteerRequest'))
    for _ in range(3):
        util=(target/(front*steer.trig(1))).clip(-D('.98'),D('.98'))
        t=(ini*util/(1-util.square()).sqrt()/spd).clip(-D('.98'),D('.98'))
        steer=(Box(asin(t.lo),asin(t.hi))+theta).clip(-g('MaxSteer'),g('MaxSteer'))
    delta=steer-g('SteerBefore')
    steering=g('SteerBefore')+D('.5')*delta.clip(-g('SteerTravel'),g('SteerTravel'))
    sn,cs=steering.trig(0),steering.trig(1)
    fwd,lat=u*cs+v*sn,v*cs-u*sn
    front_fx=sn*front*lat/(lat.square()+fl**2*(fwd.square()+D('.25'))+res).sqrt()
    residual=ax-(fx+front_fx+g('Drag'))/m
    return dict(Residual=[residual.lo,residual.hi], ForceRadius=force_radius, SlipRadius=slip_radius,
                SteeringDelta=[delta.lo,delta.hi], SteeringTravel=g('SteerTravel'),
                ExcludesZero=residual.lo>0 or residual.hi<0)


def main():
    with gzip.open(sys.argv[1], 'rt', encoding='utf-8') as f:
        original = json.load(f)
    box = original['PublishedBox']
    with localcontext() as ctx:
        ctx.prec = 76
        at = residual_function(original['Inputs'])
        lo,hi = (exact(box[k]['Ax']) for k in ('Lo','Hi'))
        mid = (lo+hi)/2
        points = [lo, hi, mid] + [mid+D(i)*D('1e-8') for i in range(-16,17)]
        # Production's bounded scanner covers the full domain. The independent
        # solve concentrates on the rejected box and observed sign changes.
        domains = [(exact(a['Lo']),exact(a['Hi'])) for a in original['Certificate']['Crossings'][:16]]
        domains.append((lo,hi))
        points += [p for x in domains for p in x]
        rows = [(x,at(x)) for x in sorted(set(points))]
        brackets = []
        for (a,fa),(b,fb) in zip(rows,rows[1:]):
            if fa['residual'] * fb['residual'] < 0:
                low,high=a,b
                sign=fa['residual']
                for _ in range(70):
                    x=(low+high)/2
                    if at(x)['residual']*sign>0:
                        low=x
                    else:
                        high=x
                root=(low+high)/2
                delta=D('1e-18')
                slope=(at(root+delta)['residual']-at(root-delta)['residual'])/(2*delta)
                brackets.append({'low':a,'high':b,'root':root,'slope':slope,'evaluation':at(root)})
        output = dict(DecimalDigits=ctx.prec, Method='independent decimal wheel Newton, clutch bisection and front law',
                      PublishedBox=[{'Ax': p, **at(p)} for p in (lo,mid,hi)],
                      Local=[{'Ax': x, **r} for x,r in rows], SignBrackets=brackets,
                      AnchoredBox=anchored_box(original['Inputs'],lo,hi,mid,at(mid)),
                      Scope='Case-specific anchored enclosure and diagnostic sign brackets, not a general replacement certificate.')
    Path(sys.argv[2]).write_text(json.dumps(output, default=str, indent=2)+'\n',encoding='utf-8')


if __name__ == '__main__':
    main()
