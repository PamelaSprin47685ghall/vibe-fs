module Wanxiangshu.Kernel.EventLog.Fold

open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Kernel.BacklogProjectionCore
open Thoth.Json

let private payloadTask (e: WanEvent) : string option =
    e.Payload
    |> Map.tryFind "task"
    |> Option.bind (fun t -> if t = "" then None else Some t)

let private payloadVerdict (e: WanEvent) : string option = e.Payload |> Map.tryFind "verdict"

let private forSession (sessionId: string) (events: WanEvent list) : WanEvent list =
    events |> List.filter (fun e -> e.Session = sessionId)

let foldEventStream
    (sessionId: string)
    (zero: 'State)
    (folder: 'State -> WanEvent -> 'State)
    (events: WanEvent list)
    : 'State =
    forSession sessionId events |> List.fold folder zero

let private reviewTaskFolder (current: string option) (e: WanEvent) : string option =
    match e.Kind with
    | k when k = eventKindLoopActivated -> payloadTask e |> Option.orElse current
    | k when k = eventKindLoopCancelled -> None
    | k when k = eventKindReviewVerdict ->
        match payloadVerdict e with
        | Some v when isEndVerdict v -> None
        | _ -> current
    | _ -> current

/// Integrate loop state for one session; file line order preserved in `events`.
let foldReviewTask (sessionId: string) (events: WanEvent list) : string option =
    foldEventStream sessionId None reviewTaskFolder events

type WorkBacklogSnapshot =
    { TodosJson: string option
      LatestEntry: BacklogEntry option }

let private payloadField (key: string) (e: WanEvent) : string option = e.Payload |> Map.tryFind key

let backlogEntryFromPayload (payload: Map<string, string>) : BacklogEntry option =
    match
        Map.tryFind "ahaMoments" payload,
        Map.tryFind "changesAndReasons" payload,
        Map.tryFind "gotchas" payload,
        Map.tryFind "lessonsAndConventions" payload,
        Map.tryFind "plan" payload
    with
    | Some aha, Some car, Some got, Some les, Some pl ->
        Some
            { ahaMoments = aha
              changesAndReasons = car
              gotchas = got
              lessonsAndConventions = les
              plan = pl }
    | _ -> None

let private workBacklogFolder (snap: WorkBacklogSnapshot) (e: WanEvent) : WorkBacklogSnapshot =
    if e.Kind <> eventKindWorkBacklogCommitted then
        snap
    else
        let entryOpt = backlogEntryFromPayload e.Payload

        { TodosJson = payloadField "todosJson" e |> Option.orElse snap.TodosJson
          LatestEntry = entryOpt |> Option.orElse snap.LatestEntry }

let foldWorkBacklogSnapshot (sessionId: string) (events: WanEvent list) : WorkBacklogSnapshot =
    foldEventStream sessionId { TodosJson = None; LatestEntry = None } workBacklogFolder events

type NudgeDedupState = { DispatchedAnchors: Set<string> }

let emptyNudgeDedupState = { DispatchedAnchors = Set.empty }

let private payloadAnchor (e: WanEvent) : string option =
    e.Payload
    |> Map.tryFind "anchor"
    |> Option.bind (fun t -> if t.Trim() = "" then None else Some(t.Trim()))

let private nudgeDedupFolder (st: NudgeDedupState) (e: WanEvent) : NudgeDedupState =
    match e.Kind with
    | k when k = eventKindNudgeDispatched ->
        match payloadAnchor e with
        | Some anchor -> { DispatchedAnchors = Set.add anchor st.DispatchedAnchors }
        | None -> st
    | k when k = eventKindSubmitReviewWipRecorded || k = eventKindNudgeDedupCleared -> emptyNudgeDedupState
    | _ -> st

/// Nudge dedup integral: collect dispatched anchors; wip record clears.
let foldNudgeDedup (sessionId: string) (events: WanEvent list) : NudgeDedupState =
    foldEventStream sessionId emptyNudgeDedupState nudgeDedupFolder events

type NudgeSnapshotState =
    { openTodos: string list
      lastAssistantText: string
      agentFromMessage: string option
      modelFromMessage: string option
      turnId: string
      isLoopActive: bool
      dispatchedAnchors: Set<string> }

let private parseTodosJson (json: string) : string list =
    if json = "" then
        []
    else
        match Decode.Auto.fromString<string list> json with
        | Ok list -> list
        | Error _ -> []

let emptyNudgeSnapshotState: NudgeSnapshotState =
    { openTodos = []
      lastAssistantText = ""
      agentFromMessage = None
      modelFromMessage = None
      turnId = ""
      isLoopActive = false
      dispatchedAnchors = Set.empty }

let private strOrEmpty (o: string option) : string =
    match o with
    | Some s -> s
    | None -> ""

let private nudgeSnapshotFolder (st: NudgeSnapshotState) (e: WanEvent) : NudgeSnapshotState =
    match e.Kind with
    | k when k = eventKindAssistantCompleted ->
        let msg = payloadField "assistantMessage" e |> strOrEmpty

        let agent =
            payloadField "agent" e |> Option.bind (fun a -> if a = "" then None else Some a)

        let model =
            payloadField "model" e |> Option.bind (fun m -> if m = "" then None else Some m)

        let tid = payloadField "turnId" e |> strOrEmpty
        let todosFromPayload = payloadField "openTodosJson" e |> Option.map parseTodosJson

        { st with
            lastAssistantText = msg
            agentFromMessage = agent
            modelFromMessage = model
            turnId = tid
            openTodos =
                match todosFromPayload with
                | Some t -> t
                | None -> st.openTodos }
    | k when k = eventKindLoopActivated -> { st with isLoopActive = true }
    | k when k = eventKindLoopCancelled -> { st with isLoopActive = false }
    | k when k = eventKindReviewVerdict ->
        match payloadVerdict e with
        | Some v when isEndVerdict v -> { st with isLoopActive = false }
        | _ -> st
    | k when k = eventKindNudgeDispatched ->
        match payloadAnchor e with
        | Some anchor ->
            { st with
                dispatchedAnchors = Set.add anchor st.dispatchedAnchors }
        | None -> st
    | k when k = eventKindSubmitReviewWipRecorded || k = eventKindNudgeDedupCleared ->
        { st with
            dispatchedAnchors = Set.empty }
    | k when k = eventKindWorkBacklogCommitted ->
        let todosOpt = payloadField "todosJson" e |> Option.map parseTodosJson

        { st with
            openTodos = todosOpt |> Option.defaultValue st.openTodos }
    | _ -> st

let foldNudgeSnapshot (sessionId: string) (events: WanEvent list) : NudgeSnapshotState =
    foldEventStream sessionId emptyNudgeSnapshotState nudgeSnapshotFolder events

let nudgeAnchorKey (turnId: string) (assistantMessage: string) : string =
    let body = assistantMessage.Trim()
    let tid = turnId.Trim()
    if tid = "" then body else tid + "\u001e" + body

let isNudgeBlockedForAnchor (st: NudgeDedupState) (anchorKey: string) : bool =
    Set.contains (anchorKey.Trim()) st.DispatchedAnchors

/// SessionState invariants:
/// - `Backlog` is stored in **reverse chronological order** (newest first) to allow
///   O(1) prepend during fold. Consumers must `List.rev` to get chronological order.
/// - `BacklogSnapshot.LatestEntry` is always the most recent backlog entry (or None).
type SessionState =
    { ReviewTask: string option
      Backlog: BacklogEntry list
      BacklogSnapshot: WorkBacklogSnapshot
      NudgeDedup: NudgeDedupState
      NudgeSnapshot: NudgeSnapshotState }

let emptySessionState () : SessionState =
    { ReviewTask = None
      Backlog = []
      BacklogSnapshot = { TodosJson = None; LatestEntry = None }
      NudgeDedup = emptyNudgeDedupState
      NudgeSnapshot = emptyNudgeSnapshotState }

let applyEvent (st: SessionState) (e: WanEvent) : SessionState =
    let isBacklog = e.Kind = eventKindWorkBacklogCommitted

    { ReviewTask = reviewTaskFolder st.ReviewTask e
      Backlog =
        if isBacklog then
            match backlogEntryFromPayload e.Payload with
            | Some entry -> entry :: st.Backlog
            | None -> st.Backlog
        else
            st.Backlog
      BacklogSnapshot = workBacklogFolder st.BacklogSnapshot e
      NudgeDedup = nudgeDedupFolder st.NudgeDedup e
      NudgeSnapshot = nudgeSnapshotFolder st.NudgeSnapshot e }
