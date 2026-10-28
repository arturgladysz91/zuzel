using SharedWorldSpeedway;
namespace Autonomous146;
/// <summary>Optional research execution services; null preserves the original algorithm and forecasts.</summary>
public sealed record ExecutionHooks146(
    Func<State146,FreeRacingAction,double,Transition146>? Step=null,
    Func<State146,Decision144>? Seed=null,
    Func<string,IDisposable>? Phase=null,
    Func<string,IDisposable>? Operation=null,
    Action<double>? BeginReview=null,
    Func<double,bool>? ScheduledSearch=null);
public sealed record Memory146(Plan146? Previous,State146? ExpectedState,double NextSearch);
