module Wanxiangshu.Kernel.SessionControl.Projection

/// SessionControl projection: generation fold + owner/lease stream fold.
/// Wire decode lives in SessionControl.Event; episode transitions live in
/// SessionControl.LeaseTransitions.

open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.SessionControl.HumanTurn
open Wanxiangshu.Kernel.SessionControl.State
open Wanxiangshu.Kernel.SessionControl.Event
open Wanxiangshu.Kernel.SessionControl.LeaseTransitions

let foldGeneration
    (sessionGen: int, cancelGen: int, activeContGen: int, activeCancelGen: int, latestHumanTurn: HumanTurnState option)
    (ev: SessionControlEvent)
    : (int * int * int * int) =
    match ev with
    | HumanTurn(_, turn) ->
        if
            turn.TurnId <> ""
            && latestHumanTurn |> Option.exists (fun t -> t.TurnId = turn.TurnId)
        then
            sessionGen, cancelGen, activeContGen, activeCancelGen
        else
            let nextCancelGen = cancelGen + 1
            sessionGen, nextCancelGen, sessionGen, nextCancelGen
    | UserAbort -> sessionGen, cancelGen + 1, activeContGen, activeCancelGen
    | ContinuationRequested req ->
        let reqGen = req.Generation |> Option.defaultValue sessionGen
        let reqCancel = req.CancelGeneration |> Option.defaultValue cancelGen
        sessionGen, cancelGen, reqGen, reqCancel
    | ContextGenerationChanged change ->
        let newGen = change.Generation |> Option.defaultValue sessionGen

        if change.CompactionId <> "" && change.Ordinal.IsSome then
            sessionGen, cancelGen, activeContGen, activeCancelGen
        else
            newGen, cancelGen, activeContGen, activeCancelGen
    | _ -> sessionGen, cancelGen, activeContGen, activeCancelGen

let foldOwnerAndLease (st: OwnerEpisodeState) (e: WanEvent) : OwnerEpisodeState =
    match Event.decode e with
    | Some ev -> foldOwnerAndLeaseEvent st ev
    | None -> st

/// Fold a full event stream into OwnerEpisodeState.
let foldOwnerAndLeaseStream (sessionId: string) (events: WanEvent list) : OwnerEpisodeState =
    events
    |> List.filter (fun e -> e.Session = sessionId)
    |> List.fold foldOwnerAndLease emptyEpisodeState

/// Check if an event is an episode event (late-event eligible).
let isEpisodeEvent (e: WanEvent) : bool = EventOrder.isEpisodeEvent e
