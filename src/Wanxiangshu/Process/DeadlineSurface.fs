namespace Wanxiangshu.Process

open System

/// JS-native boundary for the typed deadline contract. Deadline remains an
/// opaque immutable capability; callers provide ISO timestamps as clock input
/// and receive only numbers/booleans.
module DeadlineSurface =

    let private parse (value: string) : DateTimeOffset = DateTimeOffset.Parse value

    let create (nowIso: string) (budgetMs: float) : Deadline =
        Deadline.ofBudget (parse nowIso) (TimeSpan.FromMilliseconds budgetMs)

    let remainingMs (nowIso: string) (deadline: Deadline) : float =
        (Deadline.remaining (fun () -> parse nowIso) deadline).TotalMilliseconds

    let isExpired (nowIso: string) (deadline: Deadline) : bool =
        Deadline.isExpired (fun () -> parse nowIso) deadline

    let nextWaitMs (nowIso: string) (deadline: Deadline) : int =
        Deadline.nextWaitMs (fun () -> parse nowIso) deadline

    let maxTimerWaitMs: int = Deadline.MaxTimerWaitMs
