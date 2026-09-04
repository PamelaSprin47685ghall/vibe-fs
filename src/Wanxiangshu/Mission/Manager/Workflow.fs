namespace Wanxiangshu.Mission.Manager

open System
open System.Collections.Generic
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch.OpenCode
open Wanxiangshu.Mission.Relay
open Wanxiangshu.Mission.Relay.OpenCode
open Wanxiangshu.OpenCode
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Persistence.Journal

module ManagerWorkflow =
    [<Literal>]
    let private exitRequiredPath = "runtime/relay-exit-required"

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

    let private initialIncumbency (sessionId: SessionId) (physicalUserMessageId: PhysicalUserMessageId) =
        let session = SessionId.value sessionId
        let physical = PhysicalUserMessageId.value physicalUserMessageId

        HostDigest.sha256Hex ("incumbency-v1\n" + session + "\n" + physical)
        |> fun digest -> IncumbencyId.create ("incumbency:" + digest)

    let private openingEvents (turn: ReconciledTurn) (view: RoadView option) =
        match view, turn.Directory with
        | Some road, _ ->
            road.ActiveIncumbency
            |> Result.requireSome "active Relay road has no incumbent"
            |> Result.map (fun incumbent -> incumbent, [])
        | None, None -> Error "Manager Relay nudge requires a workspace directory"
        | None, Some directory ->
            let road = roadId turn.SessionId

            let authority =
                AuthorityRevision.create (PhysicalUserMessageId.value turn.PhysicalUserMessageId)

            let snapshot = WorkspaceSnapshot.capture directory
            let incumbent = initialIncumbency turn.SessionId turn.PhysicalUserMessageId

            Ok(
                incumbent,
                [ RelayEvent.RoadOpened(road, authority, turn.PhysicalUserMessageId)
                  RelayEvent.IncumbencyOpened(incumbent, snapshot, BatonSource.ExistingWorld) ]
            )

    let private appendNudge
        (journal: AgentJournal)
        (turn: ReconciledTurn)
        (incumbent: IncumbencyId)
        (opening: RelayEvent list)
        =
        let road = roadId turn.SessionId
        let frontier = ProviderRunIdentity.value turn.ProviderRun

        RelayTransaction.create (opening @ [ RelayEvent.ExitRequiredNudgeScheduled(incumbent, frontier) ])
        |> function
            | Error error -> Task.FromResult(Error error)
            | Ok transaction ->
                let fact =
                    AgentFact.Relay(
                        RelayFactCases.TransactionCommitted
                            {| RoadId = road
                               Transaction = transaction |}
                    )

                task {
                    match!
                        AgentJournal.appendAgent (StreamId.Session turn.SessionId) (Some turn.ProviderRun) fact journal
                    with
                    | Ok _ -> return Ok()
                    | Error failure -> return Error(JournalAppendFailure.describe failure)
                }

    let private sendNudge
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (journal: AgentJournal)
        (turn: ReconciledTurn)
        =
        HostSessionNudge.trySendGateContinuationPhysical
            sessionPort
            rootWorkspace
            turn.SessionId
            (ProviderProse.documentFor turn.SessionId exitRequiredPath Map.empty)
            PromptAuthority.ContinuationKind.ManagerGuard
            turn.Directory
            (Some journal)
            (exitRequiredPath + ":" + ProviderRunIdentity.value turn.ProviderRun)
            turn.ProviderRun

    let private shouldSchedule view frontier =
        match view with
        | Some road when road.ActiveIncumbency.IsNone -> false
        | Some road when Set.contains frontier road.ExitRequiredNudgeFrontiers -> false
        | _ -> true

    let private bindTaskResult binder pending =
        task {
            let! outcome = pending

            match outcome with
            | Ok value -> return! binder value
            | Error error -> return Error error
        }

    let private tryScheduleNudge sessionPort rootWorkspace journal turn view =
        match openingEvents turn view with
        | Error error -> Task.FromResult(Error error)
        | Ok(incumbent, opening) ->
            sendNudge sessionPort rootWorkspace journal turn
            |> bindTaskResult (fun _ -> appendNudge journal turn incumbent opening)

    let private scheduleNudge
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (journal: AgentJournal)
        (turn: ReconciledTurn)
        =
        task {
            let view = currentView journal turn.SessionId
            let frontier = ProviderRunIdentity.value turn.ProviderRun

            if shouldSchedule view frontier then
                let! _ = tryScheduleNudge sessionPort rootWorkspace journal turn view
                return ()
            else
                return ()
        }
        :> Task

    let observeIdle
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (nudgeSent: HashSet<string>)
        (hasLivePty: string -> bool)
        (quiescence: ISessionQuiescenceGate)
        (context: ReconciledTurnContext)
        : Task =
        ignore eventPort
        ignore nudgeSent
        ignore hasLivePty
        ignore quiescence

        match journal, context.Failure, context.Turn.Outcome with
        | Some durable, None, ReconcileProgram.TurnCompleted ->
            scheduleNudge sessionPort rootWorkspace durable context.Turn
        | _ -> Task.FromResult()

    let observe
        (sessionPort: ISessionHostPort)
        (rootWorkspace: IRootWorkspaceReader)
        (eventPort: IEventObservationPort)
        (journal: AgentJournal option)
        (nudgeSent: HashSet<string>)
        (joinGuardNudges: HashSet<string>)
        (hasLivePty: string -> bool)
        (quiescence: ISessionQuiescenceGate)
        (observeOrdinary: ReconciledTurnContext -> Task)
        (context: ReconciledTurnContext)
        : Task =
        ignore joinGuardNudges

        match
            isRetiredObservation journal context.Turn.SessionId context.Turn.ProviderRun,
            context.Failure,
            context.Turn.Outcome
        with
        | true, _, _ -> Task.FromResult()
        | false, Some _, _ -> observeOrdinary context
        | false, None, ReconcileProgram.TurnInProgress
        | false, None, ReconcileProgram.TurnNeedsContinuation _ -> Task.FromResult()
        | false, None, ReconcileProgram.TurnCompleted ->
            observeIdle sessionPort rootWorkspace eventPort journal nudgeSent hasLivePty quiescence context
        | false, None, _ -> observeOrdinary context
