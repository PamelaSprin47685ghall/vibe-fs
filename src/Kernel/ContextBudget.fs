module Wanxiangshu.Kernel.ContextBudget

type ContextState =
    { phaseBaseTokens: int64
      backlogTokensAtPhaseStart: int64 }

/// <summary>
/// Nudge 触发的判定式 F。
/// 其数学公式为：2 * a >= b + s + c
/// 
/// 【参数定义】：
/// - a: 当前总估算 tokens (currentTokens)
/// - b: 上下文 token 窗口的最大限制 (maxInputTokens)
/// - c: 本阶段起点时 backlog 的 token 数 (backlogTokensAtPhaseStart)
/// - s: 本阶段起点时非 backlog 的基础开销 (phaseBaseTokens - c)
/// 
/// 【数学推导与场景解释】：
/// 阶段起点的基础开销为 c + s。上下文的最大可用自由空间为 b - (c + s)。
/// 为了保证模型在被 nudge todowrite 强制压缩之前，依然拥有足够的余量来进行下一步的工具调用（如 executor 运行），
/// 我们应将本阶段新增的消息空间 (a - (c + s)) 限制在最大可用自由空间的一半以内：
///     a - (c + s) >= (b - (c + s)) / 2
/// 整理该不等式：
///     2 * (a - (c + s)) >= b - (c + s)
///     2a - 2c - 2s >= b - c - s
///     2a >= b + c + s
/// 
/// 只要该式成立，说明当前阶段新增的会话内容已经消耗了自由空间的一半，必须立即注入 Nudge 以强制 todowrite 压缩。
/// </summary>
let F (a: int64) (b: int64) (c: int64) (s: int64) : bool =
    2L * a >= b + s + c

/// 实在腾挪不开的界定条件：
/// 当折叠完的基线 tokens (phaseBaseTokens) 占总上下文空间上限 (maxInputTokens) 的 80% 以上时，
/// 剩余的可支配自由空间已小于 20%。由于下一步的工具调用（如 executor 运行）和对话很容易再次撑爆上下文，
/// 使得持续进行 todo 折叠与 nudge 意义不大，应直接回落至系统的 compact。
let isCompactingRequired (phaseBaseTokens: int64) (maxInputTokens: int64) : bool =
    phaseBaseTokens >= (maxInputTokens * 8L) / 10L

let beginPhase (totalTokens: int64) (totalBytes: int64) (backlogBytes: int64) : ContextState =
    let backlogTokens =
        if totalBytes <= 0L then 0L
        else totalTokens * backlogBytes / totalBytes
    { phaseBaseTokens = totalTokens
      backlogTokensAtPhaseStart = backlogTokens }

let afterSuccessfulTodo (totalTokens: int64) (totalBytes: int64) (backlogBytes: int64) : ContextState =
    beginPhase totalTokens totalBytes backlogBytes

let estimateTokens (currentTextBytes: int) (lastUsage: {| tokenCount: int; textBytes: int |} option) : int option =
    match lastUsage with
    | Some u when u.textBytes > 0 && currentTextBytes >= 0 ->
        let estimated = (int64 u.tokenCount * int64 currentTextBytes) / int64 u.textBytes
        Some (int estimated)
    | _ -> None
