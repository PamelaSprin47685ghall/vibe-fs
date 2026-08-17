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

/// GLORY-029: Manager idle → business-condition-aware encouragement (§7.4.6).
module ManagerIdle =

    [<RequireQualifiedAccess>]
    type private IdleEncouragementKind =
        | BeforePlanCommitment
        | AfterPlanCommitment

    let private encouragementKind (journal: AgentJournal option) (life: LifeProjection) =
        let preT1 =
            match journal with
            | None -> true
            | Some durable ->
                let snapshot = AgentJournal.snapshot durable

                Map.tryFind (ManagerLifeId.value life.LifeId) snapshot.AgentProjections.MagicTodo.ByLife
                |> Option.map (MagicTodoProjection.isPlanCommitted >> not)
                |> Option.defaultValue true

        if preT1 then
            IdleEncouragementKind.BeforePlanCommitment
        else
            IdleEncouragementKind.AfterPlanCommitment

    let private encouragementKey =
        function
        | IdleEncouragementKind.BeforePlanCommitment -> "pre-t1"
        | IdleEncouragementKind.AfterPlanCommitment -> "post-t1"

    let private idleEncouragement (sessionId: SessionId) kind =
        let path =
            match kind with
            | IdleEncouragementKind.BeforePlanCommitment -> ManagerLifecyclePrompt.Path.IdleEncouragementPreT1
            | IdleEncouragementKind.AfterPlanCommitment -> ManagerLifecyclePrompt.Path.IdleEncouragementPostT1

        ProviderProse.documentFor sessionId path Map.empty

    /// GLORY-029: process-local dedupe uses the same exact terminal occasion
    /// identity as the durable claim. Life/condition classify the encouragement;
    /// ProviderRunIdentity prevents one physical terminal from sending twice.
    let occasionKey
        (sessionId: SessionId)
        (lifeId: ManagerLifeId)
        (conditionKey: string)
        (terminalProviderRun: ProviderRunIdentity)
        =
        sprintf
            "manager-idle:%s:%s:%s:%s"
            (SessionId.value sessionId)
            (ManagerLifeId.value lifeId)
            conditionKey
            (ProviderRunIdentity.value terminalProviderRun)

    let private idleClaimed
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        (life: LifeProjection)
        (kindKey: string) =
        match journal, HostSessionNudge.tryActiveProfile journal turn.SessionId with
        | Some durable, Some profile ->
            PromptDispatcher.forJournal(durable).IdleAlreadyClaimed profile life.LifeId kindKey turn.ProviderRun
        | _ -> false

    let private sendIdleEncouragement
        (quiescence: SessionQuiescenceGate)
        (permit: QuiescencePermit)
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (turn: ReconciledTurn)
        (journal: AgentJournal option)
        (life: LifeProjection)
        (kindKey: string)
        (kind: IdleEncouragementKind) =
        task {
            match!
                HostSessionNudge.trySendIdleManagerEncouragement
                    quiescence
                    permit
                    sessionPort
                    turn.SessionId
                    (idleEncouragement turn.SessionId kind)
                    turn.Directory
                    journal
                    life.LifeId
                    kindKey
                    turn.ProviderRun
            with
            | HostSessionNudge.IdleContinuationOutcome.Sent _
            | HostSessionNudge.IdleContinuationOutcome.Superseded -> ()
            | HostSessionNudge.IdleContinuationOutcome.Failed error ->
                eventPort.NotifyTerminal turn.SessionId (TerminalOutcome.Failed error) |> ignore
        }

    let private processQuiescent
        (sessionPort: ISessionHostPort)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (nudgeSent: HashSet<string>)
        (quiescence: SessionQuiescenceGate)
        (turn: ReconciledTurn)
        (life: LifeProjection)
        (permit: QuiescencePermit)
        (kind: IdleEncouragementKind)
        (kindKey: string)
        (processKey: string) =
        if idleClaimed journal turn life kindKey then
            AsyncSupport.completedTask ()
        else
            nudgeSent.Add processKey |> ignore
            sendIdleEncouragement quiescence permit sessionPort eventPort turn journal life kindKey kind :> Task

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
        let kind = encouragementKind journal life
        let kindKey = encouragementKey kind

        let processKey = occasionKey turn.SessionId life.LifeId kindKey turn.ProviderRun

        match context.Quiescence with
        | Some permit when not (nudgeSent.Contains processKey) ->
            processQuiescent
                sessionPort
                eventPort
                journal
                nudgeSent
                quiescence
                turn
                life
                permit
                kind
                kindKey
                processKey
        | _ -> AsyncSupport.completedTask ()
