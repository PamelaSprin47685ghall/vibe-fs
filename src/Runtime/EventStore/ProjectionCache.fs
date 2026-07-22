module Wanxiangshu.Runtime.ProjectionCache

open Fable.Core
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Kernel.EventSourcing.Fold
open Wanxiangshu.Kernel.Nudge.NudgeProjection
open Wanxiangshu.Kernel.SessionOverview
open Wanxiangshu.Kernel.Wanxiangzhen.Dag
open Wanxiangshu.Kernel.Wanxiangzhen.SquadEvent
open Wanxiangshu.Runtime.EventLogSquadProjection
open Wanxiangshu.Runtime.Wanxiangzhen.SquadEventWanCodec
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.Nudge

type ProjectionCache() =
    let mutable sessionStates: Map<string, SessionState> = Map.empty
    let mutable squadProj = emptyProjection ()
    let mutable latestSessionId: string option = None
    let mutable revision = 0

    member _.Revision = revision

    member _.Clear() =
        sessionStates <- Map.empty
        squadProj <- emptyProjection ()
        latestSessionId <- None
        revision <- 0

    member _.ClearSessionStatesOnly() =
        sessionStates <- Map.empty
        revision <- revision + 1

    member _.FoldWan(e: WanEvent) =
        let sId = e.Session

        // Evict closed sessions — the accumulated state is useless and
        // without this the map grows unboundedly with short-lived sessions.
        if e.Kind = eventKindSubsessionPhysicalSessionClosed then
            sessionStates <- Map.remove sId sessionStates
            revision <- revision + 1
        else
            let oldState =
                match Map.tryFind sId sessionStates with
                | Some st -> st
                | None -> emptySessionState ()

            sessionStates <- Map.add sId (applyEvent oldState e) sessionStates
            squadProj <- applyWanEvent squadProj e
            revision <- revision + 1

            if isSquadEventKind e.Kind then
                latestSessionId <- Some e.Session
                ()

    member _.GetSessionStateSync(sessionId: string) : SessionState =
        match Map.tryFind sessionId sessionStates with
        | Some st -> st
        | None -> emptySessionState ()

    member _.GetAllSessionStates() : Map<string, SessionState> = sessionStates

    member _.GetSquadDag(sessionId: string) : Dag = getDag squadProj sessionId

    member _.GetLatestSquadSessionId() : string option = latestSessionId

    member _.GetSquadSessions() : Map<string, Dag> = squadProj.Dags

    member _.CanClaimNudgeDispatch
        (sessionId: string)
        (trimmedAnchor: string)
        (sessionGen: int)
        (cancelGen: int)
        (humanTurnId: string)
        (nudgeOrdinal: int)
        (isBlocked: NudgeDedupState -> string -> bool)
        : bool =
        let oldState =
            match Map.tryFind sessionId sessionStates with
            | Some st -> st
            | None -> emptySessionState ()

        let snap = oldState.NudgeSnapshot
        let currentAnchor = nudgeAnchorKey snap.turnId snap.agentFromMessage snap.modelFromMessage

        let currentHumanTurnId =
            oldState.LatestHumanTurn
            |> Option.map (fun t -> t.TurnId)
            |> Option.defaultValue ""

        currentAnchor.Trim() = trimmedAnchor
        && not (isBlocked oldState.NudgeDedup trimmedAnchor)
        && oldState.SessionGeneration = sessionGen
        && oldState.CancelGeneration = cancelGen
        && currentHumanTurnId = humanTurnId
        && oldState.SessionOwner <> Some "Fallback"
        && oldState.SessionOwner <> Some "Compaction"
        && oldState.SessionOwner <> Some "Nudge"
        && oldState.PendingLease.IsNone
        && oldState.PendingNudgeLease.IsNone
        && oldState.NudgeStage <> Requested
        && oldState.NudgeStage <> Dispatched
        && oldState.NudgeOrdinal < nudgeOrdinal
