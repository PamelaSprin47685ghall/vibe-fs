namespace Wanxiangshu.Next.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

/// Host-stopped provider failures have no assistant snapshot and therefore
/// cannot be classified by reconcile. This adapter only wakes the same
/// Logical Run; it never appends or advances the durable fallback cursor.
type ProviderFailureContinuation
    (
        sessionPort: ISessionHostPort,
        journal: AgentJournal option,
        recorded: HashSet<string>,
        ensureAuthority: SessionId -> Task<bool>,
        continuationAccepted: SessionId -> MessageId -> unit
    ) =

    let pending = HashSet<string>()

    let accepted sessionId messageId =
        pending.Remove(SessionId.value sessionId) |> ignore
        continuationAccepted sessionId messageId

    member _.Observe(failure: ProviderFailureWakeup) =
        pending.Add(SessionId.value failure.SessionId) |> ignore

    member _.OnIdle(sessionId: SessionId) =
        let key = SessionId.value sessionId

        if pending.Contains key then
            task {
                let! ready = ensureAuthority sessionId
                // Existing authority is preferred; the dispatcher itself still
                // fail-closes if the Host snapshot cannot prove it.
                if ready || journal.IsSome then
                    PluginFallbackRetry.markPending recorded sessionId
                    PluginFallbackRetry.flushOnIdle sessionPort journal recorded sessionId (Some(accepted sessionId))
            }
            |> ignore

    member _.Remove(sessionId: SessionId) =
        pending.Remove(SessionId.value sessionId) |> ignore
        PluginFallbackRetry.cancelPendingFor sessionId
