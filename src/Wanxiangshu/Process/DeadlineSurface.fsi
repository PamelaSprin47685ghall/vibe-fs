namespace Wanxiangshu.Process

module DeadlineSurface =
    val create: nowIso: string -> budgetMs: float -> Deadline
    val remainingMs: nowIso: string -> deadline: Deadline -> float
    val isExpired: nowIso: string -> deadline: Deadline -> bool
    val nextWaitMs: nowIso: string -> deadline: Deadline -> int
    val maxTimerWaitMs: int
