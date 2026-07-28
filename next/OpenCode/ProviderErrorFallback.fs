namespace Wanxiangshu.Next.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal
open Wanxiangshu.Next.Session

module private ProviderErrorCausal =

    [<Emit("Promise.resolve()")>]
    let yieldNow: Task<unit> = jsNative

/// Converts host-stopped provider errors into same-run continuations only after
/// the host's subsequent settled-idle signal. Cursor writes stay in RetrySignalHandler.
type ProviderErrorFallback
    (
        sessionPort: ISessionHostPort,
        journal: AgentJournal option,
        recorded: HashSet<string>,
        rootBindings: Dictionary<string, MessageId>,
        ensureAuthority: SessionId -> Task<bool>,
        continuationAccepted: SessionId -> MessageId -> unit
    ) =

    let pending = Dictionary<string, ProviderErrorSignal>()
    let idleArmed = HashSet<string>()

    let nextAttempt sessionId =
        journal
        |> Option.bind (fun j ->
            (AgentJournal.snapshot j).AgentProjections.Sessions
            |> Map.tryFind sessionId
            |> Option.bind (fun session -> session.Fallback)
            |> Option.bind (fun fallback -> fallback.LastProviderAttempt))
        |> Option.defaultValue 0L
        |> fun attempt -> attempt + 1L

    let accepted sessionId messageId =
        let key = SessionId.value sessionId
        pending.Remove key |> ignore
        idleArmed.Remove key |> ignore
        continuationAccepted sessionId messageId

    let handleError (error: ProviderErrorSignal) =
        task {
            do! ProviderErrorCausal.yieldNow
            let! authorityReady = ensureAuthority error.SessionId

            if authorityReady then
                let messageId =
                    error.MessageId
                    |> Option.defaultWith (fun () ->
                        MessageId.create (
                            sprintf
                                "provider-error-%s-%d"
                                (SessionId.value error.SessionId)
                                (nextAttempt error.SessionId)
                        ))

                if
                    RetrySignalHandler.handleProviderError
                        journal
                        recorded
                        rootBindings
                        { error with
                            MessageId = Some messageId }
                then
                    PluginFallbackRetry.markPending recorded error.SessionId

                PluginFallbackRetry.flushOnIdle
                    sessionPort
                    journal
                    recorded
                    error.SessionId
                    (Some(accepted error.SessionId))
        }

    member _.Observe(error: ProviderErrorSignal) =
        let key = SessionId.value error.SessionId

        if not (pending.ContainsKey key) then
            pending.[key] <- error
            idleArmed.Remove key |> ignore

    member _.OnIdle(sessionId: SessionId) =
        let key = SessionId.value sessionId

        match pending.TryGetValue key with
        | true, _ when idleArmed.Add key -> ()
        | true, error -> handleError error |> ignore
        | false, _ -> PluginFallbackRetry.flushOnIdle sessionPort journal recorded sessionId (Some(accepted sessionId))

    member _.Remove(sessionId: SessionId) =
        let key = SessionId.value sessionId
        pending.Remove key |> ignore
        idleArmed.Remove key |> ignore
        PluginFallbackRetry.cancelPendingFor sessionId
