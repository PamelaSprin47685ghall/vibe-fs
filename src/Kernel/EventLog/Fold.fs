module Wanxiangshu.Kernel.EventLog.Fold

open Wanxiangshu.Kernel.EventLog.Types
open Wanxiangshu.Kernel.EventLog.FallbackInjectionFold
open Wanxiangshu.Kernel.EventLog.ReviewLoopFold
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Kernel.Nudge.Types
open Wanxiangshu.Kernel.EventLog.ReviewVerdictWire
open Wanxiangshu.Kernel.FallbackKernel.Types

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

type HumanTurnState =
    { TurnId: string
      Provider: string
      Model: string
      Variant: string
      Agent: string }

type ReplayLeaseState =
    { ContinuationID: string
      SessionGeneration: int
      HumanTurnID: string
      CancelGeneration: int
      Owner: string
      Model: string
      PromptText: string option
      Status: string }

let private humanTurnFolder (st: HumanTurnState option) (e: WanEvent) : HumanTurnState option =
    if e.Kind = eventKindHumanTurnStarted then
        let turnId = payloadField "turnId" e |> Option.defaultValue ""
        let provider = payloadField "provider" e |> Option.defaultValue ""
        let model = payloadField "model" e |> Option.defaultValue ""
        let variant = payloadField "variant" e |> Option.defaultValue ""
        let agent = payloadField "agent" e |> Option.defaultValue ""

        Some
            { TurnId = turnId
              Provider = provider
              Model = model
              Variant = variant
              Agent = agent }
    else
        st

let private generationFolder (sessionGen: int, cancelGen: int, activeContGen: int, activeCancelGen: int) (e: WanEvent) =
    match e.Kind with
    | k when k = eventKindHumanTurnStarted ->
        let nextGen = sessionGen + 1
        nextGen, cancelGen, nextGen, cancelGen
    | k when k = eventKindUserAbortObserved -> sessionGen, cancelGen + 1, activeContGen, activeCancelGen
    | k when k = eventKindContinuationRequested ->
        let reqGen =
            e.Payload
            |> Map.tryFind "generation"
            |> Option.bind (fun s -> Some(int s))
            |> Option.defaultValue sessionGen

        let reqCancel =
            e.Payload
            |> Map.tryFind "cancelGeneration"
            |> Option.bind (fun s -> Some(int s))
            |> Option.defaultValue cancelGen

        sessionGen, cancelGen, reqGen, reqCancel
    | k when k = eventKindContextGenerationChanged ->
        let newGen =
            e.Payload
            |> Map.tryFind "generation"
            |> Option.bind (fun s -> Some(int s))
            |> Option.defaultValue sessionGen

        newGen, cancelGen, activeContGen, activeCancelGen
    | _ -> sessionGen, cancelGen, activeContGen, activeCancelGen

let private fallbackLifecycleFolder (st: FallbackLifecycle option) (e: WanEvent) : FallbackLifecycle option =
    match e.Kind with
    | k when k = eventKindUserAbortObserved -> Some FallbackLifecycle.Cancelled
    | k when k = eventKindHumanTurnStarted -> Some FallbackLifecycle.Active
    | _ -> st

let private fallbackPhaseFolder (st: FallbackPhase option) (e: WanEvent) : FallbackPhase option =
    match e.Kind with
    | k when k = eventKindHumanTurnStarted -> Some FallbackPhase.Idle
    | _ -> st

let private ownerAndLeaseFolder
    (
        owner: string option,
        lease: ReplayLeaseState option,
        compId: string option,
        isCompacted: bool,
        currentGen: int,
        currentCancelGen: int,
        latestHumanTurn: HumanTurnState option
    )
    (e: WanEvent)
    =
    match e.Kind with
    | k when k = eventKindHumanTurnStarted -> Some "Human", None, None, false
    | k when k = eventKindUserAbortObserved -> Some "None", None, None, false
    | k when k = eventKindCompactionStarted ->
        let cid = payloadField "compactionId" e
        Some "Compaction", None, cid, false
    | k when k = eventKindContextGenerationChanged -> owner, lease, compId, true
    | k when k = eventKindCompactionSettled -> Some "None", None, None, false
    | k when k = eventKindContinuationRequested ->
        let contId = payloadField "continuationId" e |> Option.defaultValue ""
        let model = payloadField "model" e |> Option.defaultValue ""
        let agent = payloadField "agent" e |> Option.defaultValue ""

        let gen =
            e.Payload
            |> Map.tryFind "generation"
            |> Option.bind (fun s -> Some(int s))
            |> Option.defaultValue currentGen

        let cancelGen =
            e.Payload
            |> Map.tryFind "cancelGeneration"
            |> Option.bind (fun s -> Some(int s))
            |> Option.defaultValue currentCancelGen

        let humanTurnId =
            payloadField "humanTurnId" e
            |> Option.orElse (latestHumanTurn |> Option.map (fun t -> t.TurnId))
            |> Option.defaultValue ""

        let ownerVal = payloadField "owner" e |> Option.defaultValue "Fallback"

        let nextLease =
            { ContinuationID = contId
              SessionGeneration = gen
              HumanTurnID = humanTurnId
              CancelGeneration = cancelGen
              Owner = ownerVal
              Model = model
              PromptText = None
              Status = "requested" }

        Some ownerVal, Some nextLease, compId, isCompacted
    | k when k = eventKindContinuationDispatchStarted ->
        let cid = payloadField "continuationId" e |> Option.defaultValue ""

        let nextLease =
            lease
            |> Option.map (fun l ->
                if l.ContinuationID = cid then
                    { l with Status = "dispatch_started" }
                else
                    l)

        owner, nextLease, compId, isCompacted
    | k when k = eventKindContinuationDispatched ->
        let cid = payloadField "continuationId" e |> Option.defaultValue ""

        let nextLease =
            lease
            |> Option.map (fun l ->
                if l.ContinuationID = cid then
                    { l with Status = "dispatched" }
                else
                    l)

        owner, nextLease, compId, isCompacted
    | k when k = eventKindContinuationFailed ->
        let cid = payloadField "continuationId" e |> Option.defaultValue ""

        let nextLease =
            lease
            |> Option.map (fun l ->
                if l.ContinuationID = cid then
                    { l with Status = "failed" }
                else
                    l)

        owner, nextLease, compId, isCompacted
    | k when k = eventKindContinuationCancelled ->
        let cid = payloadField "continuationId" e |> Option.defaultValue ""

        let nextLease =
            lease
            |> Option.map (fun l ->
                if l.ContinuationID = cid then
                    { l with Status = "cancelled" }
                else
                    l)

        owner, nextLease, compId, isCompacted
    | k when k = eventKindNudgeDispatched -> Some "Nudge", lease, compId, isCompacted
    | k when k = eventKindAssistantCompleted ->
        let nextOwner =
            match owner with
            | Some "Nudge" -> Some "None"
            | _ -> owner

        nextOwner, lease, compId, isCompacted
    | k when k = eventKindNudgeDedupCleared || k = eventKindSubmitReviewWipRecorded ->
        Some "None", lease, compId, isCompacted
    | _ -> owner, lease, compId, isCompacted

type SessionState =
    { ReviewLoop: ReviewLoopFold
      ReviewTask: string option
      Backlog: BacklogEntry list
      BacklogSnapshot: WorkBacklogSnapshot
      NudgeDedup: NudgeDedupState
      NudgeSnapshot: NudgeSnapshotState
      Subagents: Map<string, SubagentState>
      FallbackInjection: FallbackInjectionState
      LatestHumanTurn: HumanTurnState option
      SessionGeneration: int
      CancelGeneration: int
      ActiveContinuationGen: int
      ActiveContinuationCancelGen: int
      FallbackLifecycle: FallbackLifecycle option
      FallbackPhase: FallbackPhase option
      SessionOwner: string option
      PendingLease: ReplayLeaseState option
      ActiveCompactionId: string option
      IsCompacted: bool
      ProcessedKeys: Set<string> }

let emptySessionState () : SessionState =
    { ReviewLoop = ReviewLoopFold.initial
      ReviewTask = None
      Backlog = []
      BacklogSnapshot = { TodosJson = None; LatestEntry = None }
      NudgeDedup = emptyNudgeDedupState
      NudgeSnapshot = emptyNudgeSnapshotState
      Subagents = Map.empty
      FallbackInjection = emptyFallbackInjectionState
      LatestHumanTurn = None
      SessionGeneration = 0
      CancelGeneration = 0
      ActiveContinuationGen = 0
      ActiveContinuationCancelGen = 0
      FallbackLifecycle = None
      FallbackPhase = None
      SessionOwner = None
      PendingLease = None
      ActiveCompactionId = None
      IsCompacted = false
      ProcessedKeys = Set.empty }

let private getEventDuplicateKeys (e: WanEvent) : string list =
    let payload = e.Payload
    let eventIdOpt = Map.tryFind "eventId" payload
    let messageIdOpt = Map.tryFind "messageId" payload
    let partIdOpt = Map.tryFind "partId" payload
    let callIdOpt = Map.tryFind "callId" payload

    let contIdOpt =
        (payloadField "continuationId" e)
        |> Option.orElse (payloadField "continuationID" e)

    [ match eventIdOpt with
      | Some id when id <> "" -> yield e.Kind + "_" + id
      | _ -> ()

      match messageIdOpt with
      | Some id when id <> "" ->
          let rev =
              payload
              |> Map.tryFind "revision"
              |> Option.orElse (payload |> Map.tryFind "messageRevision")
              |> Option.defaultValue ""

          yield e.Kind + "_" + id + "_" + rev
      | _ -> ()

      match partIdOpt with
      | Some id when id <> "" ->
          let state =
              payload
              |> Map.tryFind "state"
              |> Option.orElse (payload |> Map.tryFind "partState")
              |> Option.defaultValue ""

          yield e.Kind + "_" + id + "_" + state
      | _ -> ()

      match callIdOpt with
      | Some id when id <> "" -> yield e.Kind + "_" + id
      | _ -> ()

      match contIdOpt with
      | Some id when id <> "" -> yield id + "_" + e.Kind
      | _ -> () ]

let applyEvent (st: SessionState) (e: WanEvent) : SessionState =
    let candidateKeys = getEventDuplicateKeys e

    let isDuplicate =
        candidateKeys |> List.exists (fun key -> Set.contains key st.ProcessedKeys)

    if isDuplicate then
        st
    else
        let nextProcessedKeys =
            candidateKeys |> List.fold (fun acc key -> Set.add key acc) st.ProcessedKeys

        let isBacklog = e.Kind = eventKindWorkBacklogCommitted
        let nextReviewLoop = ReviewLoopFold.foldEvent st.ReviewLoop e
        let nextHumanTurn = humanTurnFolder st.LatestHumanTurn e

        let nextSessionGen, nextCancelGen, nextActiveContGen, nextActiveCancelGen =
            generationFolder
                (st.SessionGeneration, st.CancelGeneration, st.ActiveContinuationGen, st.ActiveContinuationCancelGen)
                e

        let nextLifecycle = fallbackLifecycleFolder st.FallbackLifecycle e
        let nextPhase = fallbackPhaseFolder st.FallbackPhase e

        let nextOwner, nextLease, nextCompId, nextIsCompacted =
            ownerAndLeaseFolder
                (st.SessionOwner,
                 st.PendingLease,
                 st.ActiveCompactionId,
                 st.IsCompacted,
                 nextSessionGen,
                 nextCancelGen,
                 nextHumanTurn)
                e

        { ReviewLoop = nextReviewLoop
          ReviewTask = ReviewLoopFold.activeTask nextReviewLoop
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
          FallbackInjection = fallbackInjectionFolder st.FallbackInjection e
          LatestHumanTurn = nextHumanTurn
          SessionGeneration = nextSessionGen
          CancelGeneration = nextCancelGen
          ActiveContinuationGen = nextActiveContGen
          ActiveContinuationCancelGen = nextActiveCancelGen
          FallbackLifecycle = nextLifecycle
          FallbackPhase = nextPhase
          SessionOwner = nextOwner
          PendingLease = nextLease
          ActiveCompactionId = nextCompId
          IsCompacted = nextIsCompacted
          ProcessedKeys = nextProcessedKeys }

let foldSubagents (sessionId: string) (events: WanEvent list) : Map<string, SubagentState> =
    foldEventStream sessionId Map.empty subagentFolder events
