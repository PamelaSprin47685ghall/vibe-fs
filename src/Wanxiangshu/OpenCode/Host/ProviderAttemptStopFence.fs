namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity

type ProviderAttemptStopFenceSnapshot =
    { Stopped: int
      Waiting: int
      Denied: int }

/// PAR-022：宿主「已停止自动重试」的 process-local 发送栅栏。
///
/// 精确 key = (SessionId, ProviderRunIdentity)。恢复重投的许可只属于确切的失败
/// attempt（PAR-021），所以栅栏的观察也只属于它：同 session 的其它 run、迟到的
/// idle、粗粒度的 session.error 都不能满足它。
///
/// 唯一状态转换：
///
/// ```text
/// Observe(session, run)        waiting(run) → stopped(run)，等待者由该精确观察释放
/// Revoke(session)              session 上当前 waiting 的精确 attempt → denied，以 false 释放；
///                              只拒绝这些 attempt，不污染该 session 的后续 attempt
/// AwaitStop(session, run)      denied → false；stopped → true；否则登记一次性等待
/// ```
type ProviderAttemptStopFence() =
    let gate = obj ()

    [<Literal>]
    let separator = "\u001f"

    // DSL-MUTABLE: resource — exact host attempt-stop observations
    let stopped = HashSet<string>()
    // DSL-MUTABLE: resource — at most one pending waiter per exact attempt key
    let waiters = Dictionary<string, TaskCompletionSource<unit>>()
    // DSL-MUTABLE: resource — attempts denied by abort/replacement/deletion
    let denied = HashSet<string>()

    let attemptKey (sessionId: SessionId) (providerRun: ProviderRunIdentity) =
        SessionId.value sessionId + separator + ProviderRunIdentity.value providerRun

    let sessionPrefix (sessionId: SessionId) = SessionId.value sessionId + separator

    let releaseWaiter (key: string) =
        match waiters.TryGetValue key with
        | true, waiter ->
            waiters.Remove key |> ignore
            try
                waiter.SetResult(())
            with _ ->
                ()
        | false, _ -> ()

    member _.Observe(sessionId: SessionId, providerRun: ProviderRunIdentity) : unit =
        lock gate (fun () ->
            let key = attemptKey sessionId providerRun

            if stopped.Add key then
                releaseWaiter key)

    member _.Revoke(sessionId: SessionId) : unit =
        lock gate (fun () ->
            let prefix = sessionPrefix sessionId

            let pending =
                waiters
                |> Seq.choose (fun (KeyValue(key, _)) ->
                    if key.StartsWith(prefix, System.StringComparison.Ordinal) then Some key else None)
                |> Seq.toArray

            for key in pending do
                denied.Add key |> ignore
                releaseWaiter key)

    member _.AwaitStop(sessionId: SessionId, providerRun: ProviderRunIdentity) : Task<bool> =
        let key = attemptKey sessionId providerRun

        lock gate (fun () ->
            if denied.Contains key then
                Task.FromResult false
            elif stopped.Contains key then
                Task.FromResult true
            else
                let waiter =
                    match waiters.TryGetValue key with
                    | true, existing -> existing
                    | false, _ ->
                        let created =
                            TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

                        waiters.[key] <- created
                        created

                task {
                    do! waiter.Task
                    return not (lock gate (fun () -> denied.Contains key))
                })

    member _.Snapshot() : ProviderAttemptStopFenceSnapshot =
        lock gate (fun () ->
            { Stopped = stopped.Count
              Waiting = waiters.Count
              Denied = denied.Count })

[<RequireQualifiedAccess>]
module ProviderAttemptStopFence =

    /// 生产单例：宿主观察点与恢复发送点共享同一栅栏。
    let shared = ProviderAttemptStopFence()

    let create () = ProviderAttemptStopFence()
