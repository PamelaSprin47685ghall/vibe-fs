module Wanxiangshu.Kernel.EventLog.Fold

open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Kernel.BacklogProjectionCore

let private payloadTask (e: WanEvent) : string option =
    e.Payload |> Map.tryFind "task" |> Option.bind (fun t -> if t = "" then None else Some t)

let private payloadVerdict (e: WanEvent) : string option =
    e.Payload |> Map.tryFind "verdict"

let private forSession (sessionId: string) (events: WanEvent list) : WanEvent list =
    events |> List.filter (fun e -> e.Session = sessionId)

/// Integrate loop state for one session; file line order preserved in `events`.
let foldReviewTask (sessionId: string) (events: WanEvent list) : string option =
    forSession sessionId events
    |> List.fold
        (fun current e ->
            match e.Kind with
            | k when k = eventKindLoopActivated -> payloadTask e |> Option.orElse current
            | k when k = eventKindLoopCancelled -> None
            | k when k = eventKindReviewVerdict ->
                match payloadVerdict e with
                | Some v when isEndVerdict v -> None
                | _ -> current
            | _ -> current)
        None

type WorkBacklogSnapshot = {
    TodosJson: string option
    LatestEntry: BacklogEntry option
}

let private payloadField (key: string) (e: WanEvent) : string option =
    e.Payload |> Map.tryFind key

let foldWorkBacklogSnapshot (sessionId: string) (events: WanEvent list) : WorkBacklogSnapshot =
    forSession sessionId events
    |> List.fold
        (fun snap e ->
            if e.Kind <> eventKindWorkBacklogCommitted then
                snap
            else
                let entryOpt =
                    match
                        payloadField "ahaMoments" e,
                        payloadField "changesAndReasons" e,
                        payloadField "gotchas" e,
                        payloadField "lessonsAndConventions" e,
                        payloadField "plan" e
                    with
                    | Some aha, Some car, Some got, Some les, Some pl ->
                        Some
                            { ahaMoments = aha
                              changesAndReasons = car
                              gotchas = got
                              lessonsAndConventions = les
                              plan = pl }
                    | _ -> None
                { TodosJson = payloadField "todosJson" e |> Option.orElse snap.TodosJson
                  LatestEntry = entryOpt |> Option.orElse snap.LatestEntry })
        { TodosJson = None; LatestEntry = None }

type NudgeDedupState = { BlockedAnchor: string option }

let emptyNudgeDedupState = { BlockedAnchor = None }

let private payloadAnchor (e: WanEvent) : string option =
    e.Payload
    |> Map.tryFind "anchor"
    |> Option.bind (fun t -> if t.Trim() = "" then None else Some (t.Trim()))

/// Nudge dedup integral: last `nudge_dispatched` blocks same assistant anchor; wip record clears.
let foldNudgeDedup (sessionId: string) (events: WanEvent list) : NudgeDedupState =
    forSession sessionId events
    |> List.fold
        (fun st e ->
            match e.Kind with
            | k when k = eventKindNudgeDispatched -> { BlockedAnchor = payloadAnchor e }
            | k when k = eventKindSubmitReviewWipRecorded || k = eventKindNudgeDedupCleared -> emptyNudgeDedupState
            | _ -> st)
        emptyNudgeDedupState

let nudgeAnchorKey (turnId: string) (assistantMessage: string) : string =
    let body = assistantMessage.Trim()
    let tid = turnId.Trim()
    if tid = "" then body else tid + "\u001e" + body

let isNudgeBlockedForAnchor (st: NudgeDedupState) (anchorKey: string) : bool =
    match st.BlockedAnchor with
    | None -> false
    | Some a -> a = anchorKey.Trim()