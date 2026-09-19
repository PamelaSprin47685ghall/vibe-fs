namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

/// provider-attempt-recovery-022：宿主「已停止自动重试」的 process-local 发送栅栏。
///
/// 只回答一个问题：这个确切 provider attempt 的宿主终态投影（finalized
/// errored assistant message）是否已被观察？不写 Journal、不参与 crash
/// recovery。重启后栅栏清空 —— 没有观察 → 不发送失败后的恢复 continuation
/// （安全侧失败）；重启后的悬挂态由 boot recovery sweep 处置。
type ProviderAttemptStopFenceSnapshot =
    { Stopped: int
      Waiting: int
      Denied: int }

type ProviderAttemptStopFence =
    new: unit -> ProviderAttemptStopFence

    /// 宿主终态投影到达时调用（幂等）。只唤醒该确切 attempt 的等待者。
    member Observe: sessionId: SessionId * providerRun: ProviderRunIdentity -> unit

    /// 会话中止/替换/删除：该 session 上当前等待中的精确 attempt 一律被拒绝。
    /// 只拒绝这些 attempt，不污染该 session 的后续 attempt。
    member Revoke: sessionId: SessionId -> unit

    /// 等待该确切 attempt 的宿主停止证据。false = 该 session 已被 Revoke。
    member AwaitStop: sessionId: SessionId * providerRun: ProviderRunIdentity -> Task<bool>

    /// Opaque diagnostics: process-local resource cardinality only.
    member Snapshot: unit -> ProviderAttemptStopFenceSnapshot

[<RequireQualifiedAccess>]
module ProviderAttemptStopFence =
    /// 生产单例：宿主观察点与恢复发送点共享同一栅栏。
    val shared: ProviderAttemptStopFence

    val create: unit -> ProviderAttemptStopFence
