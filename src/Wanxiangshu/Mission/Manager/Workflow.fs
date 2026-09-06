namespace Wanxiangshu.Mission.Manager

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Relay
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal

module ManagerWorkflow =
    [<Literal>]
    let private assessPath = "runtime/manager-assess"

    [<Literal>]
    let private workPath = "runtime/manager-work"

    [<Literal>]
    let private finishPath = "runtime/manager-finish"

    let private roadId (sessionId: SessionId) =
        RoadId.create (SessionId.value sessionId)

    let private relayState (journal: AgentJournal) (sessionId: SessionId) =
        AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.Relay)

    let private currentView (journal: AgentJournal) (sessionId: SessionId) =
        relayState journal sessionId
        |> Option.bind (fun state -> Fold.view state (roadId sessionId))

    let private isRetiredObservation
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        =
        journal
        |> Option.bind (fun durable -> currentView durable sessionId)
        |> Option.exists (fun road -> Set.contains (ProviderRunIdentity.value providerRun) road.RetiredProviderRunIds)

    let private resourceForCurrentAction phase =
        match phase with
        | IncumbencyPhase.AuditPending -> assessPath
        | IncumbencyPhase.WorkOwned -> workPath
        | IncumbencyPhase.PerfectAwaitingRetirement
        | IncumbencyPhase.RetirementCleanupBlocked -> finishPath

    let private selectedAction (road: RoadView) =
        match road.ActiveIncumbency, road.ActivePhase with
        | Some _, Some phase -> Some phase
        | _ -> None

    let private activeResourcePath (view: RoadView option) =
        view |> Option.bind selectedAction |> Option.map resourceForCurrentAction

    let private sendNudge
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (journal: AgentJournal)
        (turn: ReconciledTurn)
        (resourcePath: string)
        =
        HostSessionNudge.trySendGateContinuation
            sessionPort
            rootWorkspace
            turn.SessionId
            (ProviderProse.documentFor turn.SessionId resourcePath Map.empty)
            PromptAuthority.ContinuationKind.ManagerGuard
            turn.Directory
            (Some journal)
            (resourcePath + ":" + ProviderRunIdentity.value turn.ProviderRun)
            turn.ProviderRun

    let private scheduleNudge
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (journal: AgentJournal)
        (turn: ReconciledTurn)
        =
        task {
            let view = currentView journal turn.SessionId

            match activeResourcePath view with
            | Some resourcePath ->
                // Durable PromptAuthority gate is the sole dedupe/source of truth:
                // Sent/AlreadyAdmitted/Retired are settled no-op success, Failed stays nonfatal.
                let! _ = sendNudge sessionPort rootWorkspace journal turn resourcePath
                return ()
            | None -> return ()
        }
        :> Task

    let observeIdle
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (journal: AgentJournal option)
        (context: ReconciledTurnContext)
        : Task =
        match journal, context.Failure, context.Turn.Outcome with
        | Some durable, None, ReconcileProgram.TurnCompleted ->
            scheduleNudge sessionPort rootWorkspace durable context.Turn
        | _ -> Task.FromResult()

    let observe
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (journal: AgentJournal option)
        (observeOrdinary: ReconciledTurnContext -> Task)
        (context: ReconciledTurnContext)
        : Task =
        match
            isRetiredObservation journal context.Turn.SessionId context.Turn.ProviderRun,
            context.Failure,
            context.Turn.Outcome
        with
        | true, _, _ -> Task.FromResult()
        | false, Some _, _ -> observeOrdinary context
        | false, None, ReconcileProgram.TurnInProgress
        | false, None, ReconcileProgram.TurnNeedsContinuation _ -> Task.FromResult()
        | false, None, ReconcileProgram.TurnCompleted -> observeIdle sessionPort rootWorkspace journal context
        | false, None, _ -> observeOrdinary context
