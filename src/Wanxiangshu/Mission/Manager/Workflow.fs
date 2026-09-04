namespace Wanxiangshu.Mission.Manager

open System
open System.Collections.Generic
open System.Threading.Tasks
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

    let private roadId (sessionId: SessionId) = RoadId.create (SessionId.value sessionId)

    let private relayState (journal: AgentJournal) sessionId =
        AgentProjection.tryFind sessionId (AgentJournal.snapshot journal).AgentProjections
        |> Option.bind (fun session -> session.Relay)

    let private currentView journal sessionId =
        relayState journal sessionId |> Option.bind (fun state -> Fold.view state (roadId sessionId))

    let private initialIncumbency sessionId physicalUserMessageId =
        let session = SessionId.value sessionId
        let physical = PhysicalUserMessageId.value physicalUserMessageId

        HostDigest.sha256Hex ("incumbency-v1\n" + session + "\n" + physical)
        |> fun digest -> IncumbencyId.create ("incumbency:" + digest)

    let private openingEvents turn view =
        match view with
        | Some road ->
            match road.ActiveIncumbency with
            | Some incumbent -> Ok(incumbent, [])
            | None -> Error "active Relay road has no incumbent"
        | None ->
            match turn.Directory with
            | None -> Error "Manager Relay nudge requires a workspace directory"
            | Some directory ->
                let road = roadId turn.SessionId
                let authority = AuthorityRevision.create (PhysicalUserMessageId.value turn.PhysicalUserMessageId)
                let snapshot = WorkspaceSnapshot.capture directory
                let incumbent = initialIncumbency turn.SessionId turn.PhysicalUserMessageId

                Ok(
                    incumbent,
                    [ RelayEvent.RoadOpened(road, authority)
                      RelayEvent.IncumbencyOpened(incumbent, snapshot, BatonSource.ExistingWorld) ]
                )

    let private appendNudge journal turn incumbent opening =
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
                        AgentJournal.appendAgent
                            (StreamId.Session turn.SessionId)
                            (Some turn.ProviderRun)
                            fact
                            journal
                    with
                    | Ok _ -> return Ok()
                    | Error failure -> return Error(JournalAppendFailure.describe failure)
                }

    let private sendNudge sessionPort journal turn =
        HostSessionNudge.trySendGateContinuationPhysical
            sessionPort
            turn.SessionId
            (ProviderProse.documentFor turn.SessionId exitRequiredPath Map.empty)
            PromptAuthority.ContinuationKind.ManagerGuard
            turn.Directory
            (Some journal)
            (exitRequiredPath + ":" + ProviderRunIdentity.value turn.ProviderRun)
            turn.ProviderRun

    let private scheduleNudge sessionPort journal turn =
        task {
            let view = currentView journal turn.SessionId
            let frontier = ProviderRunIdentity.value turn.ProviderRun

            match view with
            | Some road when road.ActiveIncumbency.IsNone -> return ()
            | Some road when Set.contains frontier road.ExitRequiredNudgeFrontiers -> return ()
            | _ ->
                match openingEvents turn view with
                | Error _ -> return ()
                | Ok(incumbent, opening) ->
                    match! sendNudge sessionPort journal turn with
                    | Error _ -> return ()
                    | Ok _ ->
                        let! _ = appendNudge journal turn incumbent opening
                        return ()
        }
        :> Task

    let observeIdle
        sessionPort
        _eventPort
        journal
        (_nudgeSent: HashSet<string>)
        _hasLivePty
        _quiescence
        context
        : Task =
        match journal, context.Failure, context.Turn.Outcome with
        | Some durable, None, ReconcileProgram.TurnCompleted -> scheduleNudge sessionPort durable context.Turn
        | _ -> Task.FromResult()

    let observe
        sessionPort
        eventPort
        journal
        nudgeSent
        _joinGuardNudges
        hasLivePty
        quiescence
        observeOrdinary
        context
        : Task =
        match context.Failure, context.Turn.Outcome with
        | Some _, _ -> observeOrdinary context
        | None, ReconcileProgram.TurnCompleted ->
            observeIdle sessionPort eventPort journal nudgeSent hasLivePty quiescence context
        | None, _ -> observeOrdinary context
