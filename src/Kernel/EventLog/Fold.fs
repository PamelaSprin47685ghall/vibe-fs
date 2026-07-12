module Wanxiangshu.Kernel.EventLog.Fold

open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Kernel.EventLog.FallbackInjectionFold
open Wanxiangshu.Kernel.EventLog.ReviewLoopFold
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.EventLog.ReviewVerdictWire

let private forSession (sessionId: string) (events: WanEvent list) : WanEvent list =
    events |> List.filter (fun e -> e.Session = sessionId)

let foldEventStream
    (sessionId: string)
    (zero: 'State)
    (folder: 'State -> WanEvent -> 'State)
    (events: WanEvent list)
    : 'State =
    forSession sessionId events |> List.fold folder zero

let private reviewLoopFolder (current: ReviewLoopFold) (e: WanEvent) : ReviewLoopFold =
    ReviewLoopFold.foldEvent current e

let foldReviewLoop (sessionId: string) (events: WanEvent list) : ReviewLoopFold =
    foldEventStream sessionId ReviewLoopFold.initial reviewLoopFolder events

let foldReviewTask (sessionId: string) (events: WanEvent list) : string option =
    foldReviewLoop sessionId events |> ReviewLoopFold.activeTask

type WorkBacklogSnapshot =
    { TodosJson: string option
      LatestEntry: BacklogEntry option }

let private payloadField (key: string) (e: WanEvent) : string option = e.Payload |> Map.tryFind key

let private payloadVerdict (e: WanEvent) : string option = payloadField "verdict" e

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

let foldNudgeDedup (sessionId: string) (events: WanEvent list) : NudgeDedupState =
    foldEventStream sessionId emptyNudgeDedupState nudgeDedupFolder events

type NudgeSnapshotState =
    { openTodos: string list
      lastAssistantText: string
      agentFromMessage: string option
      modelFromMessage: string option
      turnId: string
      reviewLoop: ReviewLoopFold
      workState: SessionWorkState
      dispatchedAnchors: Set<string> }

let private parseTodosJson (json: string) : string list =
    let trimmed = json.Trim()

    if trimmed = "" || trimmed = "[]" then
        []
    else if trimmed.Length < 2 || trimmed.[0] <> '[' || trimmed.[trimmed.Length - 1] <> ']' then
        []
    else
        let inner = trimmed.Substring(1, trimmed.Length - 2)

        inner.Split(',')
        |> Array.choose (fun segment ->
            let s = segment.Trim()

            if s.Length < 2 || s.[0] <> '"' || s.[s.Length - 1] <> '"' then
                None
            else
                Some(s.Substring(1, s.Length - 2)))
        |> Array.toList

let private syncWorkState (st: NudgeSnapshotState) : NudgeSnapshotState =
    { st with
        workState = workStateFromAxes false (ReviewLoopFold.isLoopActive st.reviewLoop) st.openTodos }

let emptyNudgeSnapshotState: NudgeSnapshotState =
    { openTodos = []
      lastAssistantText = ""
      agentFromMessage = None
      modelFromMessage = None
      turnId = ""
      reviewLoop = ReviewLoopFold.initial
      workState = SessionWorkState.Idle
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

        let openTodos =
            match todosFromPayload with
            | Some t -> t
            | None -> st.openTodos

        syncWorkState
            { st with
                lastAssistantText = msg
                agentFromMessage = agent
                modelFromMessage = model
                turnId = tid
                openTodos = openTodos }
    | k when
        k = eventKindLoopActivated
        || k = eventKindLoopCancelled
        || k = eventKindReviewVerdict
        ->
        let reviewLoop = ReviewLoopFold.foldEvent st.reviewLoop e
        syncWorkState { st with reviewLoop = reviewLoop }
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

        syncWorkState
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

type SubagentState =
    { ChildId: string
      Agent: string
      Title: string
      ContinuedPrompts: string list }

let private subagentFolder (current: Map<string, SubagentState>) (e: WanEvent) : Map<string, SubagentState> =
    match e.Kind with
    | k when k = eventKindSubagentSpawned ->
        let childId = defaultArg (e.Payload |> Map.tryFind "childId") ""

        if childId = "" then
            current
        else
            let agent = defaultArg (e.Payload |> Map.tryFind "agent") ""
            let title = defaultArg (e.Payload |> Map.tryFind "title") ""

            let state =
                { ChildId = childId
                  Agent = agent
                  Title = title
                  ContinuedPrompts = [] }

            Map.add childId state current
    | k when k = eventKindSubagentContinued ->
        let childId = defaultArg (e.Payload |> Map.tryFind "childId") ""

        if childId = "" then
            current
        else
            match Map.tryFind childId current with
            | Some state ->
                let prompt = defaultArg (e.Payload |> Map.tryFind "prompt") ""

                let updated =
                    { state with
                        ContinuedPrompts = prompt :: state.ContinuedPrompts }

                Map.add childId updated current
            | None ->
                let prompt = defaultArg (e.Payload |> Map.tryFind "prompt") ""

                let state =
                    { ChildId = childId
                      Agent = ""
                      Title = ""
                      ContinuedPrompts = [ prompt ] }

                Map.add childId state current
    | _ -> current

type SessionState =
    { ReviewTask: string option
      Backlog: BacklogEntry list
      BacklogSnapshot: WorkBacklogSnapshot
      NudgeDedup: NudgeDedupState
      NudgeSnapshot: NudgeSnapshotState
      Subagents: Map<string, SubagentState>
      FallbackInjection: FallbackInjectionState }

let emptySessionState () : SessionState =
    { ReviewTask = None
      Backlog = []
      BacklogSnapshot = { TodosJson = None; LatestEntry = None }
      NudgeDedup = emptyNudgeDedupState
      NudgeSnapshot = emptyNudgeSnapshotState
      Subagents = Map.empty
      FallbackInjection = emptyFallbackInjectionState }

let applyEvent (st: SessionState) (e: WanEvent) : SessionState =
    let isBacklog = e.Kind = eventKindWorkBacklogCommitted

    { ReviewTask =
        reviewLoopFolder
            (match st.ReviewTask with
             | Some t ->
                 Active
                     { task = t
                       reviewLoopId = ""
                       currentRound = 1
                       latestVerdict = None
                       latestFeedback = None }
             | None -> Inactive)
            e
        |> activeTask
      Backlog =
        if isBacklog then
            match backlogEntryFromPayload e.Payload with
            | Some entry -> entry :: st.Backlog
            | None -> st.Backlog
        else
            st.Backlog
      BacklogSnapshot = workBacklogFolder st.BacklogSnapshot e
      NudgeDedup = nudgeDedupFolder st.NudgeDedup e
      NudgeSnapshot = nudgeSnapshotFolder st.NudgeSnapshot e
      Subagents = subagentFolder st.Subagents e
      FallbackInjection = fallbackInjectionFolder st.FallbackInjection e }

let foldSubagents (sessionId: string) (events: WanEvent list) : Map<string, SubagentState> =
    foldEventStream sessionId Map.empty subagentFolder events
