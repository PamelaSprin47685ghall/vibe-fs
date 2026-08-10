namespace Wanxiangshu.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Journal
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// Vocabulary: Manager idle → labor encouragement (rabbit §8.3 / GLORY-029).
///
/// Owns occasion dedupe, durable idle claim check, quiescence admission, and
/// the Detached ManagerIdleEncouragement send.
module ManagerIdle =

    /// Encourage the Manager to resume labor on this idle occasion.
    /// At-most-once per (session, life, trigger ProviderRun); stale permits
    /// supersede without error.
    let encourageLabor
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (nudgeSent: HashSet<string>)
        (quiescence: SessionQuiescenceGate)
        (context: ReconciledTurnContext)
        (life: LifeProjection)
        : Task =
        let turn = context.Turn
        let sessionKey = SessionId.value turn.SessionId

        let encouragementKey =
            sprintf "manager-idle:%s:%s" sessionKey (ProviderRunIdentity.value turn.ProviderRun)

        match context.Quiescence with
        | Some permit when not (nudgeSent.Contains encouragementKey) ->
            let idleAlreadyClaimed =
                match journal, HostSessionNudge.tryActiveProfile journal turn.SessionId with
                | Some durable, Some profile ->
                    PromptDispatcher.forJournal(durable).IdleAlreadyClaimed profile life.LifeId turn.ProviderRun
                | _ -> false

            if idleAlreadyClaimed then
                AsyncSupport.completedTask ()
            else
                nudgeSent.Add encouragementKey |> ignore

                task {
                    match!
                        HostSessionNudge.trySendIdleManagerEncouragement
                            quiescence
                            permit
                            sessionPort
                            turn.SessionId
                            ManagerLifecyclePrompt.IdleEncouragement
                            turn.Directory
                            journal
                            life.LifeId
                            turn.ProviderRun
                    with
                    | HostSessionNudge.IdleContinuationOutcome.Sent _
                    | HostSessionNudge.IdleContinuationOutcome.Superseded -> ()
                    | HostSessionNudge.IdleContinuationOutcome.Failed error ->
                        eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed error) |> ignore
                }
                :> Task
        | _ -> AsyncSupport.completedTask ()
