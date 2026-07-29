namespace Wanxiangshu.Next.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Session

/// Host-stopped provider failures have no assistant snapshot and therefore
/// cannot be classified by reconcile. After a stable idle, this adapter both
/// advances the durable fallback cursor for the same Logical Run and sends the
/// next ProviderRetryAttempt continuation.
type ProviderFailureContinuation
    (
        sessionPort: ISessionHostPort,
        journal: AgentJournal option,
        recorded: HashSet<string>,
        userBindings: Dictionary<string, MessageId>,
        ensureAuthority: SessionId -> Task<bool>,
        continuationAccepted: SessionId -> MessageId -> unit
    ) =

    let pending = Dictionary<string, ProviderFailureWakeup>()
    let idleObserved = HashSet<string>()
    let dispatching = HashSet<string>()
    let mutable attemptSeq = 0L

    let accepted sessionId messageId =
        let key = SessionId.value sessionId
        pending.Remove key |> ignore
        dispatching.Remove key |> ignore
        continuationAccepted sessionId messageId

    let recordDurableAdvance (failure: ProviderFailureWakeup) =
        match RetrySignalHandler.authorityIdentity journal userBindings failure.SessionId with
        | None -> ()
        | Some identity when System.String.IsNullOrWhiteSpace(MessageId.value identity.AuthorityRootUserMessageId) -> ()
        | Some identity ->
            attemptSeq <- attemptSeq + 1L

            let assistantMessageId =
                match failure.MessageId with
                | Some mid -> MessageId.value mid
                | None -> MessageId.value identity.AuthorityRootUserMessageId

            // Host stopped auto-retry: this continuation is the next physical
            // provider request for the same Logical Run. It therefore owns the
            // durable cursor advance that a typed provider-retry signal would have
            // written. Identity remains authority-root scoped.
            FallbackDetect.recordFallbackFailure
                journal
                recorded
                (SessionId.value failure.SessionId)
                identity.LogicalRunId
                (MessageId.value identity.AuthorityRootUserMessageId)
                assistantMessageId
                (string attemptSeq)
                failure.Reason
            |> ignore

    member _.Observe(failure: ProviderFailureWakeup) =
        pending.[SessionId.value failure.SessionId] <- failure

    member _.OnIdle(sessionId: SessionId) =
        let key = SessionId.value sessionId

        // Host emits an early idle while it is still publishing the failed turn,
        // followed by the admission idle that can accept a new prompt. Consume
        // the first idle as a wakeup and dispatch on the next.
        if pending.ContainsKey key && idleObserved.Add key then
            ()
        elif pending.ContainsKey key && dispatching.Add key then
            idleObserved.Remove key |> ignore
            let failure = pending.[key]

            task {
                let! ready = ensureAuthority sessionId
                // Existing authority is preferred; the dispatcher itself still
                // fail-closes if the Host snapshot cannot prove it.
                if ready || journal.IsSome then
                    recordDurableAdvance failure
                    PluginFallbackRetry.markPending recorded sessionId

                    let! sent =
                        PluginFallbackRetry.flushOnIdle
                            sessionPort
                            journal
                            recorded
                            sessionId
                            (Some(accepted sessionId))

                    if not sent then
                        pending.[key] <- failure
                else
                    pending.[key] <- failure

                dispatching.Remove key |> ignore
            }
            |> ignore

    member _.Remove(sessionId: SessionId) =
        let key = SessionId.value sessionId
        pending.Remove key |> ignore
        idleObserved.Remove key |> ignore
        dispatching.Remove key |> ignore
        PluginFallbackRetry.cancelPendingFor sessionId
