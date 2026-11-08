using CoreSim;
using Efficient147;
using Efficient152;
using FullDrive169;
using Safety153;
using SharedWorldSpeedway;

namespace Cyclic172;

// Only armed inside one synchronous observed decision in the research harness.
// BASE, tape and original K4 calls delegate directly to the sealed predictor.
internal static class ForecastDispatch176
{
    private sealed record Observation(double Height, double Ax);
    [ThreadStatic] private static Observation? observed;

    internal static CyclicDecision172 Choose(CyclicController172 k4, FreeRacingInput input, CrankClutch144 shaft,
        double height, double acceptedAx)
    {
        if (observed is not null || height is not (.35 or .55))
            throw new InvalidOperationException("Expected one explicit load-aware K4 observation.");
        observed = new(height, acceptedAx);
        try { return k4.Choose(input, shaft); }
        finally { observed = null; }
    }

    internal static FreeRacingPrediction Predict(FreeRacingInput input, RiderSpec spec, TrackFrame frame,
        TrackStateSnapshot surface, WorldParameters parameters, float mass, float inertia, double horizon,
        FreeRacingAction? candidate = null, CrankClutch144? powertrain = null, Action<double, double>? observeCrank = null,
        PredictionBoundary147? boundaryCache = null, Boundary152? fastBoundary = null, bool exactInitialWheel = false)
    {
        if (observed is not { } q)
            return PredictorA153.Predict(input, spec, frame, surface, parameters, mass, inertia, horizon,
                candidate: candidate, powertrain: powertrain, observeCrank: observeCrank,
                boundaryCache: boundaryCache, fastBoundary: fastBoundary, exactInitialWheel: exactInitialWheel);
        return Predictor176.Predict(input, spec, frame, surface, parameters, mass, inertia, horizon,
            candidate: candidate, powertrain: powertrain, observeCrank: observeCrank, acceptedAx: q.Ax,
            experiment: new PhysicsVariant("transfer-aware forecast", q.Height), fastBoundary: fastBoundary);
    }
}
