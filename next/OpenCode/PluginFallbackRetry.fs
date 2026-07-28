namespace Wanxiangshu.Next.OpenCode

open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

/// Host-stopped provider retries wait for the next idle admission boundary.
/// Durable cursor advancement remains exclusively in RetrySignalHandler.
module PluginFallbackRetry =

    let private pendingKey (sessionId: SessionId) =
        "provider-retry-pending|" + SessionId.value sessionId

    let markPending (recorded: System.Collections.Generic.HashSet<string>) (sessionId: SessionId) =
        recorded.Add(pendingKey sessionId) |> ignore

    let flushOnIdle
        (sessionPort: ISessionHostPort)
        (journal: AgentJournal option)
        (recorded: System.Collections.Generic.HashSet<string>)
        (sessionId: SessionId)
        (onAccepted: (MessageId -> unit) option)
        =
        if recorded.Remove(pendingKey sessionId) then
            task {
                let! result =
                    HostSessionNudge.sendContinuationResult
                        sessionPort
                        sessionId
                        "Continue after provider failure."
                        PromptAuthority.ProviderRetryAttempt
                        None
                        journal
                        onAccepted

                match result with
                | Ok _ -> ()
                | Error _ -> markPending recorded sessionId
            }
            |> ignore

    /// Signal routing is unregistered before cancellation; retained diagnostic
    /// identities cannot emit another physical request.
    let cancelPendingFor (_sessionId: SessionId) : unit = ()
