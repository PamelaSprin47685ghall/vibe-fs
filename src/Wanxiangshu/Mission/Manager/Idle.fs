namespace Wanxiangshu.Mission.Manager
open Wanxiangshu.OpenCode
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Execution.Delegation.Fork.OpenCode
open Wanxiangshu.Change
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Strength.Persistence

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Host
open Wanxiangshu.Resources
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.Handle
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

/// GLORY-029: Manager idle → phase-aware encouragement (§7.4.6).
module ManagerIdle =

    let private idleEncouragement (journal: AgentJournal option) (sessionId: SessionId) (life: LifeProjection) =
        let preT1 =
            match journal with
            | None -> true
            | Some durable ->
                let snapshot = AgentJournal.snapshot durable

                Map.tryFind (ManagerLifeId.value life.LifeId) snapshot.AgentProjections.MagicTodo.ByLife
                |> Option.map (MagicTodoProjection.isPlanCommitted >> not)
                |> Option.defaultValue true

        let path =
            if preT1 then
                ManagerLifecyclePrompt.Path.IdleEncouragementPreT1
            else
                ManagerLifecyclePrompt.Path.IdleEncouragementPostT1

        ProviderProse.documentFor sessionId path Map.empty

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
                            (idleEncouragement journal turn.SessionId life)
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
