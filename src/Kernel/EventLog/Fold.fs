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

type NudgeDedupState =
    { PendingNudge: (string * string) option // (anchor, nudgeId)
      LastDispatchedAnchor: string option }

let emptyNudgeDedupState =
    { PendingNudge = None
      LastDispatchedAnchor = None }

let private payloadAnchor (e: WanEvent) : string option =
    e.Payload
    |> Map.tryFind "anchor"
    |> Option.bind (fun t -> if t.Trim() = "" then None else Some(t.Trim()))

let private nudgeDedupFolder (st: NudgeDedupState) (e: WanEvent) : NudgeDedupState =
    match e.Kind with
    | k when k = eventKindNudgeRequested ->
        match payloadAnchor e, payloadField "nudgeId" e with
        | Some anchor, Some nid ->
            { st with
                PendingNudge = Some(anchor, nid) }
        | _ -> st
    | k when k = eventKindNudgeDispatched ->
        match payloadAnchor e with
        | Some anchor ->
            { st with
                LastDispatchedAnchor = Some anchor
                PendingNudge = None }
        | None -> st
    | k when k = eventKindNudgeFailed || k = eventKindNudgeCancelled ->
        let nidOpt = payloadField "nudgeId" e

        match nidOpt with
        | Some nid ->
            match st.PendingNudge with
            | Some(_, pendingNid) when pendingNid = nid -> { st with PendingNudge = None }
            | _ -> st
        | None -> st
    | k when
        (k = eventKindSubmitReviewWipRecorded
         || k = eventKindNudgeDedupCleared
         || k = eventKindHumanTurnStarted)
        ->
        emptyNudgeDedupState
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
      pendingNudge: (string * string) option
      lastDispatchedAnchor: string option }

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
      pendingNudge = None
      lastDispatchedAnchor = None }

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
        (k = eventKindLoopActivated
         || k = eventKindLoopCancelled
         || k = eventKindReviewVerdict)
        ->
        let reviewLoop = ReviewLoopFold.foldEvent st.reviewLoop e
        syncWorkState { st with reviewLoop = reviewLoop }
    | k when k = eventKindNudgeRequested ->
        match payloadAnchor e, payloadField "nudgeId" e with
        | Some anchor, Some nid ->
            { st with
                pendingNudge = Some(anchor, nid) }
        | _ -> st
    | k when k = eventKindNudgeDispatched ->
        match payloadAnchor e with
        | Some anchor ->
            { st with
                lastDispatchedAnchor = Some anchor
                pendingNudge = None }
        | None -> st
    | k when k = eventKindNudgeFailed || k = eventKindNudgeCancelled ->
        let nidOpt = payloadField "nudgeId" e

        match nidOpt with
        | Some nid ->
            match st.pendingNudge with
            | Some(_, pendingNid) when pendingNid = nid -> { st with pendingNudge = None }
            | _ -> st
        | None -> st
    | k when
        (k = eventKindSubmitReviewWipRecorded
         || k = eventKindNudgeDedupCleared
         || k = eventKindHumanTurnStarted)
        ->
        { st with
            lastDispatchedAnchor = None
            pendingNudge = None }
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
    let trimmed = anchorKey.Trim()

    match st.PendingNudge with
    | Some(anchor, _) when anchor = trimmed -> true
    | _ ->
        match st.LastDispatchedAnchor with
        | Some anchor when anchor = trimmed -> true
        | _ -> false

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
                let nextList = prompt :: state.ContinuedPrompts

                let updated =
                    { state with
                        ContinuedPrompts =
                            if nextList.Length > 5 then
                                List.truncate 5 nextList
                            else
                                nextList }

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

let private humanTurnFolder (st: HumanTurnState option) (e: WanEvent) : HumanTurnState option =
    if e.Kind = eventKindHumanTurnStarted then
        let turnId = defaultArg (payloadField "turnId" e) ""
        let provider = defaultArg (payloadField "provider" e) ""
        let model = defaultArg (payloadField "model" e) ""
        let variant = defaultArg (payloadField "variant" e) ""
        let agent = defaultArg (payloadField "agent" e) ""

        Some
            { TurnId = turnId
              Provider = provider
              Model = model
              Variant = variant
              Agent = agent }
    else
        st

type ReplayLeaseState =
    { ContinuationID: string
      ContinuationOrdinal: int
      SessionGeneration: int
      HumanTurnID: string
      CancelGeneration: int
      Owner: string
      Model: string
      PromptText: string option
      Status: string }

type ReplayNudgeLeaseState =
    { NudgeID: string
      NudgeOrdinal: int
      Nonce: string
      Anchor: string
      HumanTurnID: string
      SessionGeneration: int
      CancelGeneration: int
      Status: string }

type ReplayCompactionState =
    { CompactionID: string
      CompactionOrdinal: int
      SessionGeneration: int
      HumanTurnID: string
      Status: string }

let private parseIntOpt (raw: string) : int option =
    if raw = "" then
        None
    else
        try
            Some(int raw)
        with _ ->
            None

let private humanTurnMessageId (e: WanEvent) : string option =
    payloadField "messageId" e
    |> Option.bind (fun s -> if s = "" then None else Some s)

let private generationFolder
    (sessionGen: int, cancelGen: int, activeContGen: int, activeCancelGen: int, latestHumanTurn: HumanTurnState option)
    (e: WanEvent)
    =
    match e.Kind with
    | k when k = eventKindHumanTurnStarted ->
        let turnId = payloadField "turnId" e |> Option.defaultValue ""

        if turnId <> "" && latestHumanTurn |> Option.exists (fun t -> t.TurnId = turnId) then
            sessionGen, cancelGen, activeContGen, activeCancelGen
        else
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
            |> Option.bind parseIntOpt
            |> Option.defaultValue sessionGen

        let eventCompactionId =
            e.Payload |> Map.tryFind "compactionId" |> Option.defaultValue ""

        let eventCompactionOrdinal =
            e.Payload |> Map.tryFind "compactionOrdinal" |> Option.bind parseIntOpt

        if eventCompactionId <> "" && eventCompactionOrdinal.IsSome then
            sessionGen, cancelGen, activeContGen, activeCancelGen
        else
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

let private continuationStartOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    e.Payload
    |> Map.tryFind "continuationOrdinal"
    |> Option.bind parseIntOpt
    |> Option.defaultValue (currentOrdinal + 1)

let private continuationStageOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    e.Payload
    |> Map.tryFind "continuationOrdinal"
    |> Option.bind parseIntOpt
    |> Option.defaultValue currentOrdinal

let private nudgeStartOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    e.Payload
    |> Map.tryFind "nudgeOrdinal"
    |> Option.bind parseIntOpt
    |> Option.defaultValue (currentOrdinal + 1)

let private nudgeStageOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    e.Payload
    |> Map.tryFind "nudgeOrdinal"
    |> Option.bind parseIntOpt
    |> Option.defaultValue currentOrdinal

let private compactionStartOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    e.Payload
    |> Map.tryFind "compactionOrdinal"
    |> Option.bind parseIntOpt
    |> Option.defaultValue (currentOrdinal + 1)

let private compactionStageOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    e.Payload
    |> Map.tryFind "compactionOrdinal"
    |> Option.bind parseIntOpt
    |> Option.defaultValue currentOrdinal

let private humanTurnOrdinal (currentOrdinal: int) (e: WanEvent) : int =
    e.Payload
    |> Map.tryFind "humanTurnOrdinal"
    |> Option.bind parseIntOpt
    |> Option.defaultValue (currentOrdinal + 1)

type OwnerEpisodeState =
    { Owner: string option
      ContinuationLease: ReplayLeaseState option
      ContinuationOrdinal: int
      ContinuationStage: EpisodeStage
      NudgeLease: ReplayNudgeLeaseState option
      NudgeOrdinal: int
      NudgeStage: EpisodeStage
      Compaction: ReplayCompactionState option
      CompactionOrdinal: int
      CompactionStage: EpisodeStage
      IsCompacted: bool
      CompactionGeneration: int
      SessionGeneration: int
      CancelGeneration: int
      HumanTurn: HumanTurnState option
      HumanTurnOrdinal: int
      LastHumanTurnMessageId: string option }

let private clearEpisodeState (st: OwnerEpisodeState) : OwnerEpisodeState =
    { st with
        Owner = Some "Human"
        ContinuationLease = None
        ContinuationStage = NoEpisode
        NudgeLease = None
        NudgeStage = NoEpisode
        Compaction = None
        CompactionStage = NoEpisode
        IsCompacted = false
        CompactionGeneration = 0 }

let private ownerAndLeaseFolder (st: OwnerEpisodeState) (e: WanEvent) : OwnerEpisodeState =
    match e.Kind with
    | k when k = eventKindHumanTurnStarted ->
        let newOrdinal = humanTurnOrdinal st.HumanTurnOrdinal e
        let msgId = humanTurnMessageId e
        let turnId = payloadField "turnId" e |> Option.defaultValue ""

        if
            newOrdinal <= st.HumanTurnOrdinal
            || (msgId.IsSome
                && st.LastHumanTurnMessageId.IsSome
                && msgId.Value = st.LastHumanTurnMessageId.Value)
        then
            st
        else
            { st with
                HumanTurnOrdinal = newOrdinal
                LastHumanTurnMessageId = msgId
                CancelGeneration = st.CancelGeneration }
            |> clearEpisodeState

    | k when k = eventKindUserAbortObserved ->
        { st with
            Owner = Some "None"
            ContinuationLease = None
            ContinuationStage = NoEpisode
            NudgeLease = None
            NudgeStage = NoEpisode
            Compaction = None
            CompactionStage = NoEpisode
            IsCompacted = false
            CompactionGeneration = 0 }

    | k when k = eventKindCompactionStarted ->
        let newOrdinal = compactionStartOrdinal st.CompactionOrdinal e
        let cid = payloadField "compactionId" e |> Option.defaultValue ""

        if newOrdinal <= st.CompactionOrdinal then
            st
        else
            let genVal =
                e.Payload
                |> Map.tryFind "generationAtStart"
                |> Option.orElse (e.Payload |> Map.tryFind "generation")
                |> Option.bind parseIntOpt
                |> Option.defaultValue st.SessionGeneration

            let humanTurnId =
                payloadField "humanTurnId" e
                |> Option.orElse (st.HumanTurn |> Option.map (fun t -> t.TurnId))
                |> Option.defaultValue ""

            { st with
                Owner = Some "Compaction"
                CompactionOrdinal = newOrdinal
                CompactionStage = Requested
                Compaction =
                    Some
                        { CompactionID = cid
                          CompactionOrdinal = newOrdinal
                          SessionGeneration = genVal
                          HumanTurnID = humanTurnId
                          Status = "started" }
                CompactionGeneration = genVal
                IsCompacted = false }

    | k when k = eventKindContextGenerationChanged ->
        let eventCompactionId =
            e.Payload |> Map.tryFind "compactionId" |> Option.defaultValue ""

        let eventCompactionOrdinal =
            e.Payload |> Map.tryFind "compactionOrdinal" |> Option.bind parseIntOpt

        let isMatch =
            eventCompactionId = ""
            || eventCompactionOrdinal.IsNone
            || (st.Compaction
                |> Option.exists (fun c ->
                    c.CompactionID = eventCompactionId
                    && c.CompactionOrdinal = eventCompactionOrdinal.Value))

        if isMatch then { st with IsCompacted = true } else st

    | k when k = eventKindCompactionSettled ->
        let eventOrdinal = compactionStageOrdinal st.CompactionOrdinal e
        let cid = payloadField "compactionId" e |> Option.defaultValue ""

        let isMatch =
            eventOrdinal = st.CompactionOrdinal
            && st.CompactionStage <> Terminal
            && st.Compaction |> Option.exists (fun c -> c.CompactionID = cid)

        if isMatch then
            { st with
                Owner =
                    (if st.Owner = Some "Compaction" then
                         Some "None"
                     else
                         st.Owner)
                CompactionStage = Terminal
                Compaction = None
                IsCompacted = false
                CompactionGeneration = 0 }
        else
            st

    | k when k = eventKindContinuationRequested ->
        let newOrdinal = continuationStartOrdinal st.ContinuationOrdinal e
        let contId = payloadField "continuationId" e |> Option.defaultValue ""

        if newOrdinal <= st.ContinuationOrdinal then
            st
        else
            let model = payloadField "model" e |> Option.defaultValue ""
            let agent = payloadField "agent" e |> Option.defaultValue ""

            let gen =
                e.Payload
                |> Map.tryFind "generation"
                |> Option.bind parseIntOpt
                |> Option.defaultValue st.SessionGeneration

            let cancelGen =
                e.Payload
                |> Map.tryFind "cancelGeneration"
                |> Option.bind parseIntOpt
                |> Option.defaultValue st.CancelGeneration

            let humanTurnId =
                payloadField "humanTurnId" e
                |> Option.orElse (st.HumanTurn |> Option.map (fun t -> t.TurnId))
                |> Option.defaultValue ""

            let ownerVal = payloadField "owner" e |> Option.defaultValue "Fallback"

            let nextLease =
                { ContinuationID = contId
                  ContinuationOrdinal = newOrdinal
                  SessionGeneration = gen
                  HumanTurnID = humanTurnId
                  CancelGeneration = cancelGen
                  Owner = ownerVal
                  Model = model
                  PromptText = None
                  Status = "requested" }

            { st with
                Owner = Some ownerVal
                ContinuationOrdinal = newOrdinal
                ContinuationStage = Requested
                ContinuationLease = Some nextLease }

    | k when k = eventKindContinuationDispatchStarted ->
        let eventOrdinal = continuationStageOrdinal st.ContinuationOrdinal e
        let cid = payloadField "continuationId" e |> Option.defaultValue ""

        if eventOrdinal <> st.ContinuationOrdinal || st.ContinuationStage <> Requested then
            st
        else
            let nextLease =
                st.ContinuationLease
                |> Option.bind (fun l ->
                    if l.ContinuationID = cid then
                        Some { l with Status = "dispatch_started" }
                    else
                        None)

            match nextLease with
            | Some l ->
                { st with
                    ContinuationLease = Some l
                    ContinuationStage = DispatchStarted }
            | None -> st

    | k when k = eventKindContinuationDispatched ->
        let eventOrdinal = continuationStageOrdinal st.ContinuationOrdinal e
        let cid = payloadField "continuationId" e |> Option.defaultValue ""

        if
            eventOrdinal <> st.ContinuationOrdinal
            || st.ContinuationStage <> DispatchStarted
        then
            st
        else
            let nextLease =
                st.ContinuationLease
                |> Option.bind (fun l ->
                    if l.ContinuationID = cid then
                        Some { l with Status = "dispatched" }
                    else
                        None)

            match nextLease with
            | Some l ->
                { st with
                    ContinuationLease = Some l
                    ContinuationStage = Dispatched }
            | None -> st

    | k when
        (k = eventKindContinuationFailed
         || k = eventKindContinuationCancelled
         || k = eventKindContinuationSettled)
        ->
        let eventOrdinal = continuationStageOrdinal st.ContinuationOrdinal e
        let cid = payloadField "continuationId" e |> Option.defaultValue ""

        if eventOrdinal <> st.ContinuationOrdinal || st.ContinuationStage = Terminal then
            st
        else
            let isMatch =
                st.ContinuationLease |> Option.exists (fun l -> l.ContinuationID = cid)

            if isMatch then
                let nextOwner = if st.Owner = Some "Fallback" then Some "None" else st.Owner

                { st with
                    Owner = nextOwner
                    ContinuationLease = None
                    ContinuationStage = Terminal }
            else
                st

    | k when k = eventKindNudgeRequested ->
        let newOrdinal = nudgeStartOrdinal st.NudgeOrdinal e
        let nid = payloadField "nudgeId" e |> Option.defaultValue ""

        if newOrdinal <= st.NudgeOrdinal then
            st
        else
            let nonce = payloadField "nonce" e |> Option.defaultValue ""
            let anchor = payloadField "anchor" e |> Option.defaultValue ""

            let humanTurnId =
                payloadField "humanTurnId" e
                |> Option.orElse (st.HumanTurn |> Option.map (fun t -> t.TurnId))
                |> Option.defaultValue ""

            let gen =
                e.Payload
                |> Map.tryFind "generation"
                |> Option.bind parseIntOpt
                |> Option.defaultValue st.SessionGeneration

            let cancelGen =
                e.Payload
                |> Map.tryFind "cancelGeneration"
                |> Option.bind parseIntOpt
                |> Option.defaultValue st.CancelGeneration

            let nextNudgeLease =
                { NudgeID = nid
                  NudgeOrdinal = newOrdinal
                  Nonce = nonce
                  Anchor = anchor
                  HumanTurnID = humanTurnId
                  SessionGeneration = gen
                  CancelGeneration = cancelGen
                  Status = "requested" }

            { st with
                Owner = Some "Nudge"
                NudgeOrdinal = newOrdinal
                NudgeStage = Requested
                NudgeLease = Some nextNudgeLease }

    | k when k = eventKindNudgeDispatched ->
        let eventOrdinal = nudgeStageOrdinal st.NudgeOrdinal e
        let nid = payloadField "nudgeId" e |> Option.defaultValue ""

        if eventOrdinal <> st.NudgeOrdinal || st.NudgeStage <> Requested then
            st
        else
            let nextLease =
                st.NudgeLease
                |> Option.bind (fun nl ->
                    if nl.NudgeID = nid then
                        Some { nl with Status = "dispatched" }
                    else
                        None)

            match nextLease with
            | Some l ->
                { st with
                    NudgeLease = Some l
                    NudgeStage = Dispatched }
            | None -> st

    | k when
        (k = eventKindNudgeFailed
         || k = eventKindNudgeCancelled
         || k = eventKindNudgeSettled)
        ->
        let eventOrdinal = nudgeStageOrdinal st.NudgeOrdinal e
        let nid = payloadField "nudgeId" e |> Option.defaultValue ""

        if eventOrdinal <> st.NudgeOrdinal || st.NudgeStage = Terminal then
            st
        else
            let isMatch = st.NudgeLease |> Option.exists (fun nl -> nl.NudgeID = nid)

            if isMatch then
                { st with
                    Owner = (if st.Owner = Some "Nudge" then Some "None" else st.Owner)
                    NudgeLease = None
                    NudgeStage = Terminal }
            else
                st

    | k when k = eventKindAssistantCompleted ->
        let nextOwner =
            match st.Owner with
            | Some "Nudge" -> Some "None"
            | _ -> st.Owner

        { st with Owner = nextOwner }

    | k when k = eventKindNudgeDedupCleared || k = eventKindSubmitReviewWipRecorded ->
        { st with
            Owner = Some "None"
            NudgeLease = None
            NudgeStage = NoEpisode }

    | _ -> st

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
      ContinuationOrdinal: int
      ContinuationStage: EpisodeStage
      PendingNudgeLease: ReplayNudgeLeaseState option
      NudgeOrdinal: int
      NudgeStage: EpisodeStage
      ActiveCompaction: ReplayCompactionState option
      ActiveCompactionId: string option
      CompactionOrdinal: int
      CompactionStage: EpisodeStage
      IsCompacted: bool
      CompactionGeneration: int
      HumanTurnOrdinal: int
      LastHumanTurnMessageId: string option
      EventCount: int }

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
      ContinuationOrdinal = 0
      ContinuationStage = NoEpisode
      PendingNudgeLease = None
      NudgeOrdinal = 0
      NudgeStage = NoEpisode
      ActiveCompaction = None
      ActiveCompactionId = None
      CompactionOrdinal = 0
      CompactionStage = NoEpisode
      IsCompacted = false
      CompactionGeneration = 0
      HumanTurnOrdinal = 0
      LastHumanTurnMessageId = None
      EventCount = 0 }

let private isEpisodeEvent (e: WanEvent) : bool =
    e.Kind = eventKindContinuationRequested
    || e.Kind = eventKindContinuationDispatchStarted
    || e.Kind = eventKindContinuationDispatched
    || e.Kind = eventKindContinuationFailed
    || e.Kind = eventKindContinuationCancelled
    || e.Kind = eventKindContinuationSettled
    || e.Kind = eventKindNudgeRequested
    || e.Kind = eventKindNudgeDispatched
    || e.Kind = eventKindNudgeFailed
    || e.Kind = eventKindNudgeCancelled
    || e.Kind = eventKindNudgeSettled
    || e.Kind = eventKindCompactionStarted
    || e.Kind = eventKindCompactionSettled
    || e.Kind = eventKindHumanTurnStarted

let private isLateEvent (st: SessionState) (e: WanEvent) : bool =
    match e.Kind with
    | k when k = eventKindContinuationRequested ->
        continuationStartOrdinal st.ContinuationOrdinal e <= st.ContinuationOrdinal
    | k when
        (k = eventKindContinuationDispatchStarted
         || k = eventKindContinuationDispatched
         || k = eventKindContinuationFailed
         || k = eventKindContinuationCancelled
         || k = eventKindContinuationSettled)
        ->
        continuationStageOrdinal st.ContinuationOrdinal e < st.ContinuationOrdinal
    | k when k = eventKindNudgeRequested -> nudgeStartOrdinal st.NudgeOrdinal e <= st.NudgeOrdinal
    | k when
        (k = eventKindNudgeDispatched
         || k = eventKindNudgeFailed
         || k = eventKindNudgeCancelled
         || k = eventKindNudgeSettled)
        ->
        nudgeStageOrdinal st.NudgeOrdinal e < st.NudgeOrdinal
    | k when k = eventKindCompactionStarted -> compactionStartOrdinal st.CompactionOrdinal e <= st.CompactionOrdinal
    | k when k = eventKindCompactionSettled -> compactionStageOrdinal st.CompactionOrdinal e < st.CompactionOrdinal
    | k when k = eventKindHumanTurnStarted -> humanTurnOrdinal st.HumanTurnOrdinal e <= st.HumanTurnOrdinal
    | _ -> false

let private isDuplicateHumanTurn (currentHumanTurnOrdinal: int) (lastMsgId: string option) (e: WanEvent) : bool =
    if e.Kind <> eventKindHumanTurnStarted then
        false
    else
        let newOrdinal = humanTurnOrdinal currentHumanTurnOrdinal e
        let msgId = humanTurnMessageId e

        newOrdinal <= currentHumanTurnOrdinal
        || (msgId.IsSome && lastMsgId.IsSome && msgId.Value = lastMsgId.Value)

let applyEvent (st: SessionState) (e: WanEvent) : SessionState =
    if isLateEvent st e then
        st
    else
        let nextReviewLoop = ReviewLoopFold.foldEvent st.ReviewLoop e

        let nextHumanTurn =
            if isDuplicateHumanTurn st.HumanTurnOrdinal st.LastHumanTurnMessageId e then
                st.LatestHumanTurn
            else
                humanTurnFolder st.LatestHumanTurn e

        let nextSessionGen, nextCancelGen, nextActiveContGen, nextActiveCancelGen =
            generationFolder
                (st.SessionGeneration,
                 st.CancelGeneration,
                 st.ActiveContinuationGen,
                 st.ActiveContinuationCancelGen,
                 st.LatestHumanTurn)
                e

        let episodeState =
            { Owner = st.SessionOwner
              ContinuationLease = st.PendingLease
              ContinuationOrdinal = st.ContinuationOrdinal
              ContinuationStage = st.ContinuationStage
              NudgeLease = st.PendingNudgeLease
              NudgeOrdinal = st.NudgeOrdinal
              NudgeStage = st.NudgeStage
              Compaction = st.ActiveCompaction
              CompactionOrdinal = st.CompactionOrdinal
              CompactionStage = st.CompactionStage
              IsCompacted = st.IsCompacted
              CompactionGeneration = st.CompactionGeneration
              SessionGeneration = nextSessionGen
              CancelGeneration = nextCancelGen
              HumanTurn = nextHumanTurn
              HumanTurnOrdinal = st.HumanTurnOrdinal
              LastHumanTurnMessageId = st.LastHumanTurnMessageId }

        let nextEpisode = ownerAndLeaseFolder episodeState e

        let nextFallbackInjection =
            fallbackInjectionFolder st.ContinuationOrdinal st.ContinuationStage st.FallbackInjection e

        let isBacklog = e.Kind = eventKindWorkBacklogCommitted

        let shouldUpdateNudgeDedup = not (isEpisodeEvent e) || not (isLateEvent st e)

        let nextNudgeDedup =
            if shouldUpdateNudgeDedup then
                nudgeDedupFolder st.NudgeDedup e
            else
                st.NudgeDedup

        let nextNudgeSnapshot =
            if shouldUpdateNudgeDedup then
                nudgeSnapshotFolder st.NudgeSnapshot e
            else
                st.NudgeSnapshot

        { ReviewLoop = nextReviewLoop
          ReviewTask = ReviewLoopFold.activeTask nextReviewLoop
          Backlog =
            if isBacklog then
                match backlogEntryFromPayload e.Payload with
                | Some entry ->
                    let nextList = entry :: st.Backlog

                    if nextList.Length > 5 then
                        List.truncate 5 nextList
                    else
                        nextList
                | None -> st.Backlog
            else
                st.Backlog
          BacklogSnapshot = workBacklogFolder st.BacklogSnapshot e
          NudgeDedup = nextNudgeDedup
          NudgeSnapshot = nextNudgeSnapshot
          Subagents = subagentFolder st.Subagents e
          FallbackInjection = nextFallbackInjection
          LatestHumanTurn = nextHumanTurn
          SessionGeneration = nextSessionGen
          CancelGeneration = nextCancelGen
          ActiveContinuationGen = nextActiveContGen
          ActiveContinuationCancelGen = nextActiveCancelGen
          FallbackLifecycle = fallbackLifecycleFolder st.FallbackLifecycle e
          FallbackPhase = fallbackPhaseFolder st.FallbackPhase e
          SessionOwner = nextEpisode.Owner
          PendingLease = nextEpisode.ContinuationLease
          ContinuationOrdinal = nextEpisode.ContinuationOrdinal
          ContinuationStage = nextEpisode.ContinuationStage
          PendingNudgeLease = nextEpisode.NudgeLease
          NudgeOrdinal = nextEpisode.NudgeOrdinal
          NudgeStage = nextEpisode.NudgeStage
          ActiveCompaction = nextEpisode.Compaction
          ActiveCompactionId = nextEpisode.Compaction |> Option.map (fun c -> c.CompactionID)
          CompactionOrdinal = nextEpisode.CompactionOrdinal
          CompactionStage = nextEpisode.CompactionStage
          IsCompacted = nextEpisode.IsCompacted
          CompactionGeneration = nextEpisode.CompactionGeneration
          HumanTurnOrdinal = nextEpisode.HumanTurnOrdinal
          LastHumanTurnMessageId = nextEpisode.LastHumanTurnMessageId
          EventCount = st.EventCount + 1 }

let foldSubagents (sessionId: string) (events: WanEvent list) : Map<string, SubagentState> =
    foldEventStream sessionId Map.empty subagentFolder events
