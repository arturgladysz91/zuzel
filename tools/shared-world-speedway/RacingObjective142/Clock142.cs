using System.Text.Json;
namespace SharedWorldSpeedway;
/// <summary>Comparison only: accepted-sample clocks and chooser clocks differ by
/// floating clock accumulation (~1e-14s). No physical quantity is rounded or fed back.</summary>
public static class Clock142
{
    public static FreeRacingInput Canonical(FreeRacingInput input)=>input with{
        Time=Math.Round(input.Time,10),NextReview=Math.Round(input.Time,10),
        Pending=input.Pending.Select(x=>x with{ObservedAt=Math.Round(x.ObservedAt,10),ActivationAt=Math.Round(x.ActivationAt,10)}).ToArray()};
    public static string StateHash(FreeRacingInput input)=>Offline142.Hash(JsonSerializer.Serialize(Canonical(input),FreeLearningExperiment.Json));
}
