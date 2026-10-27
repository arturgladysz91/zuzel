using System.Numerics;
using Box2D.NET;
using CoreSim.Interactions;
using CoreSim.PhysicalSpace;
using static Box2D.NET.B2Bodies;
using static Box2D.NET.B2Shapes;
using static Box2D.NET.B2Worlds;
using static Box2D.NET.B2Types;
using static Box2D.NET.B2MathFunction;

namespace SharedWorldSpeedway;

/// <summary>Box2D owns transforms, velocities, broad/narrow phase and ALL impulses.</summary>
public sealed class ContactAdapter139 : IDisposable
{
    public B2WorldId World { get; }
    public B2BodyId[] Bodies { get; }
    public float[] Masses { get; }
    public float[] Inertias { get; }
    private readonly Dictionary<B2ShapeId, int> owners = new();
    private readonly Dictionary<B2ShapeId, BikeComponent> components = new();
    private readonly B2ContactData[] contactBuffer = new B2ContactData[64];
    private readonly double[,] impulses;
    private readonly double[,] closingSpeeds;
    private readonly bool[,] interacting;
    private readonly bool[,] active;
    private readonly bool twoContactsEnabled;
    public ContactAdapter139(IReadOnlyList<RiderSpec> specs, TrackFrame frame, WorldParameters p,TwoContactInitialState? twoContactInitial=null,double inertiaScale=1)
    {
        if(twoContactInitial is not null&&!p.EnableTwoContactBikeDynamics)throw new ArgumentException("Controlled initial spin/heading require the opt-in variant.");
        twoContactsEnabled=p.EnableTwoContactBikeDynamics;
        var wd = b2DefaultWorldDef(); wd.gravity = new(0, 0); wd.enableSleep = false;
        wd.hitEventThreshold = .01f; // event observation only; restitution remains zero
        World = b2CreateWorld(wd);
        Bodies = new B2BodyId[specs.Count]; Masses = new float[specs.Count]; Inertias = new float[specs.Count];
        impulses = new double[specs.Count, specs.Count]; closingSpeeds = new double[specs.Count, specs.Count]; active = new bool[specs.Count, specs.Count];
        interacting = new bool[specs.Count, specs.Count];
        for (var i = 0; i < specs.Count; i++)
        {
            var spec = specs[i]; var at = frame.At(spec.ReferenceMeters, spec.OffsetMeters);
            var bd = b2DefaultBodyDef(); bd.type = B2BodyType.b2_dynamicBody;
            bd.position = To(at.Position); bd.rotation = b2MakeRot((float)at.Road.Heading);
            bd.linearVelocity = To(at.Road.Tangent * spec.SpeedMetersPerSecond + at.Road.Outward * spec.InitialOutwardSpeed);
            if(twoContactInitial is { } initial){
                bd.rotation=b2MakeRot((float)at.Road.Heading+initial.BetaRadians);
            }
            bd.enableSleep = false; bd.isBullet = spec.Bullet;
            Bodies[i] = b2CreateBody(World, bd);
            var sd = b2DefaultShapeDef(); sd.density = 1;
            sd.material.friction = p.ContactFriction; sd.material.restitution = 0;
            sd.enableContactEvents = true; sd.enableHitEvents = true;
            var d = SpeedwayBikeDimensions.Reference;
            var half = (float)((d.OverallMechanicalLengthMeters - d.ChassisBodyWidthMeters) / 2);
            var chassis = b2CreateCapsuleShape(Bodies[i], sd,
                new(new(-half, 0), new(half, 0), (float)d.ChassisBodyWidthMeters / 2));
            owners.Add(chassis, i); components.Add(chassis, BikeComponent.Chassis);
            var barHalf = (float)((d.HandlebarWidthMeters - d.HandlebarTubeDiameterMeters) / 2);
            var x = (float)d.HandlebarLongitudinalOffsetMeters;
            var handlebar = b2CreateCapsuleShape(Bodies[i], sd,
                new(new(x, -barHalf), new(x, barHalf), (float)d.HandlebarTubeDiameterMeters / 2));
            owners.Add(handlebar, i); components.Add(handlebar, BikeComponent.Handlebar);
            Masses[i] = spec.Profile.Gameplay.Physical.MassKg + (float)new PhysicalContactParameters().ReferenceBikeMassKg;
            // Same mass distributed over the existing geometry. Explicit symmetric body origin avoids a bar-driven COM shift.
            var natural = b2Body_GetMassData(Bodies[i]);
            var inertia = natural.rotationalInertia / natural.mass * Masses[i];
            if(inertiaScale!=1)inertia*=(float)inertiaScale;
            b2Body_SetMassData(Bodies[i], new() { mass = Masses[i], center = new(0, 0), rotationalInertia = inertia });
            Inertias[i] = inertia;
            // Set the controlled initial spin AFTER the final CoM/mass. Box2D's
            // mass updates transport linear velocity when CoM shifts; an earlier
            // nonzero spin would therefore alter the requested initial v_CoM.
            if(twoContactInitial is { } initialSpin)b2Body_SetAngularVelocity(Bodies[i],initialSpin.BodyAngularVelocity);
        }
    }
    public static B2Vec2 To(Vector2 v) => new(v.X, v.Y);
    public static Vector2 From(B2Vec2 v) => new(v.X, v.Y);
    public (Vector2 Position, Vector2 Velocity, float Heading, float Spin) Read(int i)
        => (From(b2Body_GetPosition(Bodies[i])), From(b2Body_GetLinearVelocity(Bodies[i])),
            b2Rot_GetAngle(b2Body_GetRotation(Bodies[i])), b2Body_GetAngularVelocity(Bodies[i]));
    public void Force(int i, Vector2 force, float torque)
    {
        if(twoContactsEnabled)throw new InvalidOperationException("Direct yaw torque is excluded in the two-contact variant.");
        b2Body_ApplyForceToCenter(Bodies[i], To(force), true);
        b2Body_ApplyTorque(Bodies[i], torque, true);
    }
    public void ForcesAtContacts(int i,TwoContactForces f)
    {
        if(!twoContactsEnabled)throw new InvalidOperationException("Point-force variant is OFF.");
        b2Body_ApplyForce(Bodies[i],To(f.FrontForce),To(f.FrontPoint),true);
        b2Body_ApplyForce(Bodies[i],To(f.RearForce),To(f.RearPoint),true);
        b2Body_ApplyForceToCenter(Bodies[i],To(f.Resistance),true);
        // NO ApplyTorque here: Box2D already computes both r x F moments.
    }
    public void Step(float seconds, int substeps) => b2World_Step(World, seconds, substeps);
    public void ReadContacts(RiderView[] views, WorldWork work, Action<int, int, double, double> onEpisode,
        Action<int, int> onEnd, WorldDiagnostics? diagnostics = null, RiderView[]? before = null,
        double time = 0, float step = 0)
    {
        Array.Clear(impulses); Array.Clear(closingSpeeds); Array.Clear(interacting);
        var events = b2World_GetContactEvents(World);
        work.BeginShapes += events.beginCount; work.EndShapes += events.endCount; work.HitShapes += events.hitCount;
        for (var i = 0; i < Bodies.Length; i++)
        {
            var capacity = b2Body_GetContactCapacity(Bodies[i]);
            if (capacity > contactBuffer.Length) throw new InvalidOperationException("Unexpected four-bike contact capacity.");
            var count = b2Body_GetContactData(Bodies[i], contactBuffer, contactBuffer.Length);
            for (var k = 0; k < count; k++)
            {
                var c = contactBuffer[k]; var a = owners[c.shapeIdA]; var b = owners[c.shapeIdB];
                if (a > b) (a, b) = (b, a);
                if (i != a) continue; // each shape pair once
                work.ContactManifolds++;
                for (var n = 0; n < c.manifold.pointCount; n++)
                {
                    var point = c.manifold.points[n];
                    if (diagnostics?.ContactPoint is { } observe)
                    {
                        var indexA = owners[c.shapeIdA]; var indexB = owners[c.shapeIdB];
                        var p = From(point.point); var normal = From(c.manifold.normal);
                        Vector2 PointVelocity(int index)
                        {
                            var v = before![index]; var arm = p - v.Position;
                            return v.Velocity + v.AngularVelocity * new Vector2(-arm.Y, arm.X);
                        }
                        var actual = Vector2.Dot(PointVelocity(indexB) - PointVelocity(indexA), normal);
                        observe(new(time, step, views[indexA].Id, views[indexB].Id,
                            components[c.shapeIdA], components[c.shapeIdB], p, normal, point.separation,
                            point.normalVelocity, point.normalImpulse, point.totalNormalImpulse, point.tangentImpulse, point.persisted, actual));
                    }
                    if (point.totalNormalImpulse > 0)
                    {
                        work.SolvedPoints++;
                        impulses[a, b] += point.totalNormalImpulse;
                        closingSpeeds[a, b] = Math.Max(closingSpeeds[a, b], -point.normalVelocity);
                        interacting[a, b] = true;
                    }
                    if (point.separation <= 0) interacting[a, b] = true;
                }
            }
        }
        for (var a = 0; a < Bodies.Length; a++) for (var b = a + 1; b < Bodies.Length; b++)
        {
            // Independent #55 endpoint diagnostic, only inside the capsule bounding circles.
            // No interval search or second contact solver runs here.
            var near = Vector2.DistanceSquared(views[a].Position, views[b].Position)
                <= Math.Pow(2 * SpeedwayBikeDimensions.Reference.BoundingRadiusMeters + .03, 2);
            var gap = near ? MechanicalSeparation.Between(Pose(views[a]), Pose(views[b])).SignedMeters : double.PositiveInfinity;
            if (near) { work.GeometryChecks++; work.MinimumGapMeters = Math.Min(work.MinimumGapMeters, gap); }
            if (interacting[a, b] && !active[a, b])
            { active[a, b] = true; work.Episodes++; onEpisode(a, b, impulses[a, b], closingSpeeds[a, b]); }
            else if (active[a, b] && !interacting[a, b] && gap > .02)
            { active[a, b] = false; onEnd(a, b); }
        }
    }
    private static PhysicalBikePose Pose(RiderView v) => new(v.Id, "shared-world", new(v.Position.X, v.Position.Y),
        new(0, v.Speed > .01f ? Math.Atan2(v.Velocity.Y, v.Velocity.X) : v.Heading, v.Heading), SpeedwayBikeDimensions.Reference);
    public void Dispose() => b2DestroyWorld(World);
}
