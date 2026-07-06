module Wanxiangshu.Kernel.EventLog.Fold

open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Kernel.BacklogProjectionCore
open Thoth.Json

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

let foldBacklogFromEvents (sessionId: string) (events: WanEvent list) : BacklogEntry list =
    forSession sessionId events
    |> List.choose (fun e ->
        if e.Kind <> eventKindWorkBacklogCommitted then None
        else
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
            | _ -> None)

type NudgeDedupState = { DispatchedAnchors: Set<string> }

let emptyNudgeDedupState = { DispatchedAnchors = Set.empty }

let private payloadAnchor (e: WanEvent) : string option =
    e.Payload
    |> Map.tryFind "anchor"
    |> Option.bind (fun t -> if t.Trim() = "" then None else Some (t.Trim()))

/// Nudge dedup integral: collect dispatched anchors; wip record clears.
let foldNudgeDedup (sessionId: string) (events: WanEvent list) : NudgeDedupState =
    forSession sessionId events
    |> List.fold
        (fun st e ->
            match e.Kind with
            | k when k = eventKindNudgeDispatched ->
                match payloadAnchor e with
                | Some anchor -> { DispatchedAnchors = Set.add anchor st.DispatchedAnchors }
                | None -> st
            | k when k = eventKindSubmitReviewWipRecorded || k = eventKindNudgeDedupCleared -> emptyNudgeDedupState
            | _ -> st)
        emptyNudgeDedupState

type NudgeSnapshotState = {
    openTodos: string list
    lastAssistantText: string
    agentFromMessage: string option
    modelFromMessage: string option
    turnId: string
    isLoopActive: bool
    dispatchedAnchors: Set<string>
}

let private parseTodosJson (json: string) : string list =
    if json = "" then []
    else
        match Decode.Auto.fromString<string list> json with
        | Ok list -> list
        | Error _ -> []

let emptyNudgeSnapshotState : NudgeSnapshotState =
    { openTodos = []
      lastAssistantText = ""
      agentFromMessage = None
      modelFromMessage = None
      turnId = ""
      isLoopActive = false
      dispatchedAnchors = Set.empty }

let private strOrEmpty (o: string option) : string =
    match o with Some s -> s | None -> ""

let foldNudgeSnapshot (sessionId: string) (events: WanEvent list) : NudgeSnapshotState =
    forSession sessionId events
    |> List.fold
        (fun st e ->
            match e.Kind with
            | k when k = eventKindAssistantCompleted ->
                let msg = payloadField "assistantMessage" e |> strOrEmpty
                let agent =
                    payloadField "agent" e
                    |> Option.bind (fun a -> if a = "" then None else Some a)
                let model =
                    payloadField "model" e
                    |> Option.bind (fun m -> if m = "" then None else Some m)
                let tid = payloadField "turnId" e |> strOrEmpty
                let todosFromPayload =
                    payloadField "openTodosJson" e
                    |> Option.map parseTodosJson
                { st with
                    lastAssistantText = msg
                    agentFromMessage = agent
                    modelFromMessage = model
                    turnId = tid
                    openTodos = match todosFromPayload with Some t -> t | None -> st.openTodos }
            | k when k = eventKindLoopActivated ->
                { st with isLoopActive = true }
            | k when k = eventKindLoopCancelled ->
                { st with isLoopActive = false }
            | k when k = eventKindReviewVerdict ->
                match payloadVerdict e with
                | Some v when isEndVerdict v -> { st with isLoopActive = false }
                | _ -> st
            | k when k = eventKindNudgeDispatched ->
                match payloadAnchor e with
                | Some anchor -> { st with dispatchedAnchors = Set.add anchor st.dispatchedAnchors }
                | None -> st
            | k when k = eventKindSubmitReviewWipRecorded || k = eventKindNudgeDedupCleared ->
                { st with dispatchedAnchors = Set.empty }
            | k when k = eventKindWorkBacklogCommitted ->
                let todosOpt =
                    payloadField "todosJson" e
                    |> Option.map parseTodosJson
                { st with openTodos = todosOpt |> Option.defaultValue st.openTodos }
            | _ -> st)
        emptyNudgeSnapshotState

let nudgeAnchorKey (turnId: string) (assistantMessage: string) : string =
    let body = assistantMessage.Trim()
    let tid = turnId.Trim()
    if tid = "" then body else tid + "\u001e" + body

let isNudgeBlockedForAnchor (st: NudgeDedupState) (anchorKey: string) : bool =
    Set.contains (anchorKey.Trim()) st.DispatchedAnchors