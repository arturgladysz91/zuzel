using SharedWorldSpeedway;
namespace Autonomous146;
/// <summary>Optional research execution services; null preserves the original algorithm and forecasts.</summary>
public sealed record ExecutionHooks146(
    Func<State146,FreeRacingAction,double,Transition146>? Step=null,
    Func<State146,Decision144>? Seed=null,
    Func<string,IDisposable>? Phase=null,
    Func<string,IDisposable>? Operation=null,
    Action<double>? BeginReview=null,
    Func<FreeRacingInput,Plan146?,bool,bool>? ScheduledSearch=null,
    bool UseOwnWorkBudget=false,
    Action<string>? SearchBudgetFallback=null,
    bool EarlyReject=false,
    Func<Plan146,FreeRacingAction[]>? SearchTail=null,
    bool FreezeCandidateTail=false,
    Action<Plan146,FreeRacingAction[]?>? ObservePlan=null,
    Func<State146,FreeRacingAction[],FreeRacingAction[]>? Proposals=null);
public sealed class PlanningBudget146(string message):InvalidOperationException(message);
public sealed record Memory146(Plan146? Previous,State146? ExpectedState,double NextSearch);
