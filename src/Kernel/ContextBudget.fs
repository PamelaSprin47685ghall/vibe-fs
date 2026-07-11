module Wanxiangshu.Kernel.ContextBudget

/// Nudge 触发所需的 todo anchor 数。foldAfterFirst=true 需 2 个 anchor
/// 才缩减投影，foldAfterFirst=false 需 3 个。每次 anchor = 一次 todowrite
/// 调用。Nudge 触发后 LLM 需连续 N 次 todowrite 才能让投影缩减上下文，
/// 故触发点到 compaction 之间须预留 N 份 todowrite 空间 + 1 份 reserve。
let requiredFoldAnchorCount (foldAfterFirst: bool) : int = if foldAfterFirst then 2 else 3

type ContextState =
    { phaseBaseTokens: int64
      backlogTokensAtPhaseStart: int64 }

/// Nudge 触发判定 F。
///
/// 物理量：a=当前token, bEff=调用方已扣 reserve 的有效窗口上限, P=phaseBaseTokens,
/// N=requiredFoldAnchorCount(foldAfterFirst)。
///
/// 安全条件：触发 nudge 后到 compaction(bEff)，LLM 需 N 次 todowrite
/// 才缩减投影。可用空间 (bEff-P) 分成 N+1 份：N 份给 N 次 todowrite，
/// 1 份 reserve。当阶段新增 u=a-P 消耗了 1/(N+1) 份时触发：
///   u >= (bEff - P) / (N + 1)
/// 等价：
///   (N+1)(a-P) >= bEff - P
///   (N+1)a >= bEff + N*P
///
/// 首次 phase P=0 → a >= bEff/(N+1)。
/// N=3(foldAfterFirst=false) → a >= bEff/4 = 25%（75% 空间做 3 次 todo+reserve）。
/// N=2(foldAfterFirst=true)  → a >= bEff/3 ≈ 33%。
let F (a: int64) (bEff: int64) (P: int64) (N: int) (R: int) : bool =
    let n = int64 N
    let r = int64 R
    (n + 1L) * a >= (r + 1L) * bEff + (n - r) * P

/// phaseBaseTokens 占有效窗口 80% 以上 → 折叠基线已饱和，
/// 持续 nudge 无意义，回落宿主 compact。
let isCompactingRequired (phaseBaseTokens: int64) (maxInputTokens: int64) : bool =
    phaseBaseTokens >= (maxInputTokens * 8L) / 10L

let beginPhase (totalTokens: int64) (totalBytes: int64) (backlogBytes: int64) : ContextState =
    let backlogTokens =
        if totalBytes <= 0L then
            0L
        else
            totalTokens * backlogBytes / totalBytes

    { phaseBaseTokens = totalTokens
      backlogTokensAtPhaseStart = backlogTokens }

let afterSuccessfulTodo (totalTokens: int64) (totalBytes: int64) (backlogBytes: int64) : ContextState =
    beginPhase totalTokens totalBytes backlogBytes

let estimateTokens (currentTextBytes: int) (lastUsage: {| tokenCount: int; textBytes: int |} option) : int option =
    match lastUsage with
    | Some u when u.textBytes > 0 && currentTextBytes >= 0 ->
        let estimated = (int64 u.tokenCount * int64 currentTextBytes) / int64 u.textBytes
        Some(int estimated)
    | _ -> None

/// Host strips synthetic nudge each transform round; reinject whenever pressure still holds.
type BudgetNudgeTrack =
    | Idle
    | EmergencySignaled

type ContextBudgetPressure =
    | Disabled
    | BelowThreshold
    | Compacting
    | RequireTodoWriteEmergency

/// Effective budget calculation.
/// The caller/host provides the effective budget (e.g. limit minus reserve).
/// This function acts as an identity.
let effectiveMaxInputTokens (maxInputTokens: int) : int64 = int64 maxInputTokens

let classifyPressure
    (maxInputTokens: int)
    (foldAfterFirst: bool)
    (currentTokens: int64)
    (state: ContextState)
    (completedTodoCount: int)
    : ContextBudgetPressure =
    if maxInputTokens <= 0 then
        Disabled
    else
        let bEff = effectiveMaxInputTokens maxInputTokens

        if isCompactingRequired state.phaseBaseTokens bEff then
            Compacting
        else
            let N = requiredFoldAnchorCount foldAfterFirst
            let R = max 0 (min completedTodoCount (N - 1))

            if F currentTokens bEff state.phaseBaseTokens N R then
                RequireTodoWriteEmergency
            else
                BelowThreshold

let afterPhaseBoundaryReset (_track: BudgetNudgeTrack) : BudgetNudgeTrack = Idle

let afterEmergencyNudge (_track: BudgetNudgeTrack) : BudgetNudgeTrack = EmergencySignaled
