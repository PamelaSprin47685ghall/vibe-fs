namespace Wanxiangshu.Mission.Manager

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Composition.Bridges.FinalityReview
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Context.Trace
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Review
open Wanxiangshu.Persistence.Journal

/// JS-native semantic surface for Finality laws (PR 6 exemplar).
///
/// A JS test expresses history as plain events:
///
/// ```js
/// const world = finality.project([
///   { kind: 'life-opened', sessionId: 's1', lifeId: 'life-1', ... },
///   { kind: 'finality-requested', ... },
/// ])
/// finality.classifyEnding(world, { callId: 'call-2', hasPlanCommitment: true })
/// // { kind: 'wait-for-current-request' }
/// ```
///
/// `world` is an opaque handle: the production fold runs inside `project`, and
/// the F# `ProjectionSet` / `LifeProjection` / fact types never cross the
/// boundary (JS-SEMANTIC-SURFACE-002/003/005). The JS test does not own
/// `ManagerLifecycleFact`, `EventEnvelope`, `FSharpList`, or any dist module —
/// it only speaks lifecycle vocabulary and reads JS-shaped answers.
module FinalitySurface =

    let private int64Of (value: obj) : int64 =
        match value with
        | :? int64 as v -> v
        | :? int as v -> int64 v
        | :? float as v -> int64 v
        | _ -> int64 (string value)

    let private intOf (value: obj) : int =
        match value with
        | :? int as v -> v
        | :? int64 as v -> int v
        | :? float as v -> int v
        | _ -> int (string value)

    let private boolOf (value: obj) : bool =
        match value with
        | :? bool as v -> v
        | _ -> System.Boolean.Parse(string value)

    let private str (value: obj) : string = string value

    /// The opaque world handle: the folded projection plus the Manager session
    /// the lifecycle facts belong to. Tests pass it back; they never read it
    /// (JS-SEMANTIC-SURFACE-005 opaque-capability: create → pass back, no
    /// inspection). Public functions accept it as `obj` so the Fable record
    /// class is not part of any surface signature.
    type private World =
        { Projection: ProjectionSet
          SessionId: SessionId option }

    let private asWorld (world: obj) : World = world :?> World

    // ── event translation: JS lifecycle vocabulary → typed facts ─────────────

    let private lifecycleEventOf (event: obj) : Result<Fact, string> =
        let kind = str (event?kind)
        let sessionId = SessionId.create (str (event?sessionId))

        match kind with
        | "life-opened" ->
            Ok(
                Fact.ManagerLifecycle(
                    ManagerLifecycleFact.LifeOpened
                        {| SessionId = sessionId
                           LifeId = ManagerLifeId.create (str (event?lifeId))
                           OpeningUserMessageId = PhysicalUserMessageId.create (str (event?openingUserMessageId))
                           OpeningTextRef = BlobRef.create (str (event?openingTextRef))
                           OpeningTextDigest = BlobDigest.create (str (event?openingTextDigest))
                           OpeningCursorSequence = int64Of (event?openingCursorSequence) |}
                )
            )
        | "work-activated" ->
            Ok(
                Fact.ManagerLifecycle(
                    ManagerLifecycleFact.WorkActivated
                        {| SessionId = sessionId
                           LifeId = ManagerLifeId.create (str (event?lifeId))
                           ActivationPromptKey = PromptKey.create (str (event?activationPromptKey))
                           ProtectedPrefixEndSequence = int64Of (event?protectedPrefixEndSequence) |}
                )
            )
        | "finality-requested" ->
            Ok(
                Fact.ManagerLifecycle(
                    ManagerLifecycleFact.FinalityRequested
                        {| SessionId = sessionId
                           LifeId = ManagerLifeId.create (str (event?lifeId))
                           RequestId = FinalityRequestId.create (str (event?requestId))
                           GitTreeHash = GitTreeHash.create (str (event?gitTreeHash))
                           LastWordsRef = BlobRef.create (str (event?lastWordsRef))
                           LastWordsDigest = BlobDigest.create (str (event?lastWordsDigest))
                           ProviderRun = ProviderRunIdentity.create (str (event?providerRun))
                           ToolCallId = ToolCallId.create (str (event?toolCallId)) |}
                )
            )
        | "finality-reviewer-enlisted" ->
            Ok(
                Fact.ManagerLifecycle(
                    ManagerLifecycleFact.FinalityReviewerEnlisted
                        {| SessionId = sessionId
                           LifeId = ManagerLifeId.create (str (event?lifeId))
                           RequestId = FinalityRequestId.create (str (event?requestId))
                           ReviewerSessionId = SessionId.create (str (event?reviewerSessionId))
                           ReviewerOrdinal = intOf (event?reviewerOrdinal)
                           BarrierId = ReviewBarrierId.create (str (event?barrierId))
                           GitTreeHash = GitTreeHash.create (str (event?gitTreeHash))
                           IsNewReviewer = boolOf (event?isNewReviewer) |}
                )
            )
        | "finality-rejected" ->
            Ok(
                Fact.ManagerLifecycle(
                    ManagerLifecycleFact.FinalityRejected
                        {| SessionId = sessionId
                           LifeId = ManagerLifeId.create (str (event?lifeId))
                           RequestId = FinalityRequestId.create (str (event?requestId))
                           RejectingReviewerSessionId = SessionId.create (str (event?rejectingReviewerSessionId))
                           BarrierId = ReviewBarrierId.create (str (event?barrierId))
                           GitTreeHash = GitTreeHash.create (str (event?gitTreeHash))
                           WorkRecordRef = BlobRef.create (str (event?workRecordRef))
                           WorkRecordDigest = BlobDigest.create (str (event?workRecordDigest)) |}
                )
            )
        | "finality-sibling-steered" ->
            Ok(
                Fact.ManagerLifecycle(
                    ManagerLifecycleFact.FinalitySiblingSteered
                        {| SessionId = sessionId
                           LifeId = ManagerLifeId.create (str (event?lifeId))
                           RequestId = FinalityRequestId.create (str (event?requestId))
                           ReviewerSessionId = SessionId.create (str (event?reviewerSessionId))
                           BarrierId = ReviewBarrierId.create (str (event?barrierId))
                           GitTreeHash = GitTreeHash.create (str (event?gitTreeHash))
                           WorkRecordRef = BlobRef.create (str (event?workRecordRef))
                           WorkRecordDigest = BlobDigest.create (str (event?workRecordDigest)) |}
                )
            )
        | "finality-blessed" ->
            Ok(
                Fact.ManagerLifecycle(
                    ManagerLifecycleFact.FinalityBlessed
                        {| SessionId = sessionId
                           LifeId = ManagerLifeId.create (str (event?lifeId))
                           RequestId = FinalityRequestId.create (str (event?requestId))
                           GitTreeHash = GitTreeHash.create (str (event?gitTreeHash))
                           WorkRecordBundleRef = BlobRef.create (str (event?workRecordBundleRef))
                           WorkRecordBundleDigest = BlobDigest.create (str (event?workRecordBundleDigest)) |}
                )
            )
        | "finality-undecided" ->
            Ok(
                Fact.ManagerLifecycle(
                    ManagerLifecycleFact.FinalityUndecided
                        {| SessionId = sessionId
                           LifeId = ManagerLifeId.create (str (event?lifeId))
                           RequestId = FinalityRequestId.create (str (event?requestId))
                           ReviewerSessionId = SessionId.create (str (event?reviewerSessionId))
                           BarrierId = ReviewBarrierId.create (str (event?barrierId))
                           GitTreeHash = GitTreeHash.create (str (event?gitTreeHash)) |}
                )
            )
        | "life-completed" ->
            Ok(
                Fact.ManagerLifecycle(
                    ManagerLifecycleFact.LifeCompleted
                        {| SessionId = sessionId
                           LifeId = ManagerLifeId.create (str (event?lifeId))
                           RequestId = FinalityRequestId.create (str (event?requestId))
                           TerminalRef = BlobRef.create (str (event?terminalRef))
                           TerminalDigest = BlobDigest.create (str (event?terminalDigest)) |}
                )
            )
        | "review-barrier-started" ->
            let reviewerSessionId = SessionId.create (str (event?reviewerSessionId))

            Ok(
                Fact.Agent(
                    AgentFact.Review(
                        ReviewFactCases.ReviewBarrierStarted
                            {| ReviewerSessionId = reviewerSessionId
                               ManagerSessionId = sessionId
                               BarrierId = ReviewBarrierId.create (str (event?barrierId))
                               GitTreeHash = GitTreeHash.create (str (event?gitTreeHash)) |}
                    )
                )
            )
        | "confirmed-review-witness" ->
            let reviewerSessionId = SessionId.create (str (event?reviewerSessionId))

            Ok(
                Fact.Agent(
                    AgentFact.Review(
                        ReviewFactCases.ConfirmedReviewWitness
                            {| ManagerJobId = None
                               ManagerSessionId = sessionId
                               ReviewerSessionId = reviewerSessionId
                               WorktreeIdentity = None
                               BarrierId = ReviewBarrierId.create (str (event?barrierId))
                               GitTreeHash = GitTreeHash.create (str (event?gitTreeHash))
                               FirstProviderRun = ProviderRunIdentity.create (str (event?firstProviderRun))
                               FirstToolCallId = ToolCallId.create (str (event?firstToolCallId))
                               ChallengeResultDigest = SealDigest.create (str (event?challengeResultDigest))
                               SecondProviderRun = ProviderRunIdentity.create (str (event?secondProviderRun))
                               SecondProviderInputDigest = SealDigest.create (str (event?secondProviderInputDigest))
                               SecondToolCallId = ToolCallId.create (str (event?secondToolCallId)) |}
                    )
                )
            )
        | other -> Error $"unknown finality event kind: {other}"

    let private envelopeFor (seq: int64) (stream: StreamId) (fact: Fact) : Envelope =
        { RuntimeId = RuntimeId.create "rt-finality-surface"
          LocalSeq = LocalSeq.create seq
          ObservedAt = DateTimeOffset(2026, 1, 1, 0, 0, 0, System.TimeSpan.Zero)
          EventId = EventId.create (sprintf "e%04d" seq)
          Stream = stream
          ProviderRun = None
          Fact = fact }

    let private lifecycleSession (lifecycle: ManagerLifecycleFact) : SessionId =
        match lifecycle with
        | ManagerLifecycleFact.LifeOpened payload -> payload.SessionId
        | ManagerLifecycleFact.WorkActivated payload -> payload.SessionId
        | ManagerLifecycleFact.FinalityRequested payload -> payload.SessionId
        | ManagerLifecycleFact.FinalityReviewerEnlisted payload -> payload.SessionId
        | ManagerLifecycleFact.FinalityRejected payload -> payload.SessionId
        | ManagerLifecycleFact.FinalitySiblingSteered payload -> payload.SessionId
        | ManagerLifecycleFact.FinalityBlessed payload -> payload.SessionId
        | ManagerLifecycleFact.FinalityUndecided payload -> payload.SessionId
        | ManagerLifecycleFact.LifeCompleted payload -> payload.SessionId

    let private streamOf (fact: Fact) : StreamId =
        match fact with
        | Fact.ManagerLifecycle lifecycle -> StreamId.Session(lifecycleSession lifecycle)
        | Fact.Agent(AgentFact.Review(ReviewFactCases.ReviewBarrierStarted payload)) ->
            StreamId.Session payload.ReviewerSessionId
        | Fact.Agent(AgentFact.Review(ReviewFactCases.ConfirmedReviewWitness payload)) ->
            StreamId.Session payload.ReviewerSessionId
        | _ -> StreamId.Workspace

    let private sessionOfStream (stream: StreamId) : SessionId option =
        match stream with
        | StreamId.Session session -> Some session
        | _ -> None

    /// Keep the first manager session observed; later facts fold onto it.
    let private firstSession (current: SessionId option) (stream: StreamId) : SessionId option =
        match current with
        | Some _ -> current
        | None -> sessionOfStream stream

    /// Fold one event; `(seq, projection, session)` threaded immutably.
    let private foldOneEvent
        (seq: int64)
        (projection: ProjectionSet)
        (managerSession: SessionId option)
        (event: obj)
        : Result<int64 * ProjectionSet * SessionId option, string> =
        lifecycleEventOf event
        |> Result.bind (fun fact ->
            let nextSeq = seq + 1L
            let stream = streamOf fact
            let session = firstSession managerSession stream

            Fold.foldEnvelope projection (envelopeFor nextSeq stream fact)
            |> Result.mapError (fun rejection ->
                sprintf "fold rejected lifecycle event %d (%A): %A" nextSeq stream rejection)
            |> Result.map (fun next -> (nextSeq, next, session)))

    /// Fold a JS event list through the production fold, threading the
    /// projection immutably (no mutable accumulators, no nested decisions).
    let private foldEvents
        (startProjection: ProjectionSet)
        (startSession: SessionId option)
        (events: obj list)
        : Result<World, string> =
        let rec loop (seq: int64) (projection: ProjectionSet) (managerSession: SessionId option) (remaining: obj list) =
            match remaining with
            | [] ->
                Ok
                    { Projection = projection
                      SessionId = managerSession }
            | event :: rest ->
                foldOneEvent seq projection managerSession event
                |> Result.bind (fun (nextSeq, next, session) -> loop nextSeq next session rest)

        loop 0L startProjection startSession events

    /// Fold JS lifecycle events through the production fold.
    /// Returns `{ ok: true, world }` or `{ ok: false, error }`.
    let project (events: obj array) : obj =
        match foldEvents Fold.empty None (List.ofArray events) with
        | Error message -> box {| ok = false; error = message |}
        | Ok world -> box {| ok = true; world = world |}

    /// Fold more events onto an existing world (multi-step traces).
    let applyEvents (world: obj) (events: obj array) : obj =
        let world = asWorld world

        match foldEvents world.Projection world.SessionId (List.ofArray events) with
        | Error message -> box {| ok = false; error = message |}
        | Ok next -> box {| ok = true; world = next |}

    let private currentLife (world: obj) : LifeProjection option =
        let world = asWorld world

        world.SessionId
        |> Option.bind (fun session -> AgentProjection.tryFind session world.Projection.AgentProjections)
        |> Option.bind (fun projection -> projection.ManagerLife)
        |> Option.bind (fun life -> life.CurrentLife)

    // ── JS-native answers ─────────────────────────────────────────────────────

    let private resolutionView (resolution: FinalityResolution) : obj =
        match resolution with
        | FinalityResolution.Open -> box {| kind = "open" |}
        | FinalityResolution.Rejected evidence ->
            box
                {| kind = "rejected"
                   rejectingReviewer = SessionId.value evidence.RejectingReviewer
                   workRecordRef = BlobRef.value evidence.WorkRecordRef
                   workRecordDigest = BlobDigest.value evidence.WorkRecordDigest |}
        | FinalityResolution.Blessed evidence ->
            box
                {| kind = "blessed"
                   requestId = FinalityRequestId.value evidence.RequestId
                   workRecordBundleRef = BlobRef.value evidence.WorkRecordBundleRef
                   workRecordBundleDigest = BlobDigest.value evidence.WorkRecordBundleDigest |}
        | FinalityResolution.Undecided -> box {| kind = "undecided" |}

    let private memberView (memberRef: ReviewMemberRef) : obj =
        box
            {| sessionId = SessionId.value memberRef.ReviewerSessionId
               ordinal = memberRef.ReviewerOrdinal
               barrierId = ReviewBarrierId.value memberRef.BarrierId
               isNew = memberRef.IsNewReviewer |}

    let private requestView (request: FinalityRequestProjection) : obj =
        box
            {| requestId = FinalityRequestId.value request.RequestId
               gitTreeHash = GitTreeHash.value request.GitTreeHash
               lastWordsRef = BlobRef.value request.LastWordsRef
               lastWordsDigest = BlobDigest.value request.LastWordsDigest
               providerRun = ProviderRunIdentity.value request.ProviderRun
               toolCallId = ToolCallId.value request.ToolCallId
               members =
                request.Members
                |> Map.toList
                |> List.map (fun (_, memberRef) -> memberView memberRef)
                |> List.toArray
               resolution = resolutionView request.Resolution |}

    /// The current Life as JS-shaped data. `undefined` when the session has no
    /// open Life (archived after LifeCompleted, or never opened).
    let lifeView (world: obj) : obj =
        let world = asWorld world

        match currentLife world with
        | None -> null
        | Some life ->
            let standingView (standing: ReviewerStanding) =
                box
                    {| ordinal = standing.ReviewerOrdinal
                       barriers = standing.Barriers |> List.map ReviewBarrierId.value |> List.toArray
                       agentId = standing.AgentId |}

            box
                {| lifeId = ManagerLifeId.value life.LifeId
                   openingUserMessageId = PhysicalUserMessageId.value life.OpeningUserMessageId
                   openingTextRef = BlobRef.value life.OpeningTextRef
                   openingTextDigest = BlobDigest.value life.OpeningTextDigest
                   openingCursorSequence = life.OpeningCursor.Sequence
                   protectedPrefixEnd =
                    life.ProtectedPrefixEnd
                    |> Option.map (fun cursor -> box cursor.Sequence)
                    |> Option.defaultValue null
                   activeFinality = life.ActiveFinality |> Option.map requestView |> Option.defaultValue null
                   enlistedReviewers =
                    life.EnlistedReviewers
                    |> Map.toList
                    |> List.map (fun (session, standing) ->
                        box
                            {| sessionId = SessionId.value session
                               standing = standingView standing |})
                    |> List.toArray
                   lastRejectedWorkRecord =
                    life.LastRejectedWorkRecord
                    |> Option.map BlobRef.value
                    |> Option.defaultValue null
                   lastBlessing =
                    life.LastBlessing
                    |> Option.map (fun blessing ->
                        box
                            {| requestId = FinalityRequestId.value blessing.RequestId
                               workRecordBundleRef = BlobRef.value blessing.WorkRecordBundleRef
                               workRecordBundleDigest = BlobDigest.value blessing.WorkRecordBundleDigest |})
                    |> Option.defaultValue null
                   completedTerminal = life.CompletedTerminal |> Option.map BlobRef.value |> Option.defaultValue null
                   completed = life.Completed |}

    /// `{ ok: true, life } | { ok: false, error }` — the Life view, or a typed
    /// reason when the world has no open Life.
    let currentLifeView (world: obj) : obj =
        let world = asWorld world

        match currentLife world with
        | None -> box {| ok = false; error = "no open life" |}
        | Some _ -> box {| ok = true; life = lifeView world |}

    /// GLORY-065 archived Lives (CompletedLives), newest first — Lives closed
    /// by LifeCompleted. JS-shaped; empty when none.
    let archivedLivesView (world: obj) : obj array =
        let world = asWorld world

        let managerLife =
            world.SessionId
            |> Option.bind (fun session -> AgentProjection.tryFind session world.Projection.AgentProjections)
            |> Option.bind (fun projection -> projection.ManagerLife)

        match managerLife with
        | None -> [||]
        | Some life ->
            life.CompletedLives
            |> List.map (fun archived ->
                box
                    {| lifeId = ManagerLifeId.value archived.LifeId
                       completed = archived.Completed
                       completedTerminal =
                        archived.CompletedTerminal
                        |> Option.map BlobRef.value
                        |> Option.defaultValue null
                       activeFinality = archived.ActiveFinality |> Option.map requestView
                       lastBlessing =
                        archived.LastBlessing
                        |> Option.map (fun blessing ->
                            box
                                {| requestId = FinalityRequestId.value blessing.RequestId
                                   workRecordBundleRef = BlobRef.value blessing.WorkRecordBundleRef
                                   workRecordBundleDigest = BlobDigest.value blessing.WorkRecordBundleDigest |})
                        |> Option.defaultValue null |})
            |> List.toArray

    let private dispositionView (disposition: ManagerFinality.EndingDisposition) : obj =
        match disposition with
        | ManagerFinality.EndingDisposition.ContinuePlanning -> box {| kind = "continue-planning" |}
        | ManagerFinality.EndingDisposition.AlreadyCompleted -> box {| kind = "already-completed" |}
        | ManagerFinality.EndingDisposition.ResumeRequest request ->
            box
                {| kind = "resume-request"
                   requestId = FinalityRequestId.value request.RequestId |}
        | ManagerFinality.EndingDisposition.RecoverRequestWithoutReviewers request ->
            box
                {| kind = "recover-request-without-reviewers"
                   requestId = FinalityRequestId.value request.RequestId |}
        | ManagerFinality.EndingDisposition.WaitForCurrentRequest -> box {| kind = "wait-for-current-request" |}
        | ManagerFinality.EndingDisposition.CompleteBlessedLife _ -> box {| kind = "complete-blessed-life" |}
        | ManagerFinality.EndingDisposition.BeginFinality -> box {| kind = "begin-finality" |}

    /// GLORY-065: an archived Life (LifeCompleted cleared CurrentLife and
    /// pushed into CompletedLives) replays as AlreadyCompleted — never restarts.
    let private archivedDisposition (world: World) : obj =
        let managerLife =
            world.SessionId
            |> Option.bind (fun session -> AgentProjection.tryFind session world.Projection.AgentProjections)
            |> Option.bind (fun projection -> projection.ManagerLife)

        match managerLife with
        | Some life when not (List.isEmpty life.CompletedLives) -> box {| kind = "already-completed" |}
        | _ -> box {| kind = "no-life" |}

    /// Interpret one suicide call against the durable Life.
    /// `callId` absent → `undefined`; `hasPlanCommitment` is the typed
    /// obligation-ledger projection (FINALITY-004).
    let classifyEnding (world: obj) (callId: string) (hasPlanCommitment: bool) : obj =
        let world = asWorld world

        let call =
            if isNull callId || callId = "" then
                None
            else
                Some(ToolCallId.create callId)

        match currentLife world with
        | Some life -> ManagerFinality.classifyEnding call life hasPlanCommitment |> dispositionView
        | None -> archivedDisposition world

    let private laborAdmissionView (admission: ManagerFinality.LaborAdmission) : string =
        match admission with
        | ManagerFinality.LaborAdmission.FinalityOwnsLife -> "finality-owns-life"
        | ManagerFinality.LaborAdmission.LaborMayContinue -> "labor-may-continue"

    /// FINALITY-026: ordinary Manager labor is deferred only while an open
    /// Finality request owns the Life. `'finality-owns-life' | 'labor-may-continue'`.
    let admitLabor (world: obj) : string =
        let world = asWorld world

        currentLife world
        |> Option.map (ManagerFinality.admitLabor >> laborAdmissionView)
        |> Option.defaultValue "labor-may-continue"

    /// FINALITY-019 / GLORY-029: JS-native projection of the exact Manager idle
    /// occasion identity. Same terminal => same key; fresh ProviderRun => fresh
    /// key even when Life and pre/post-T1 condition are unchanged.
    let managerIdleOccasionKey
        (sessionId: string)
        (lifeId: string)
        (conditionKey: string)
        (providerRun: string)
        : string =
        ManagerIdle.occasionKey
            (SessionId.create sessionId)
            (ManagerLifeId.create lifeId)
            conditionKey
            (ProviderRunIdentity.create providerRun)

    /// GLORY-070: a Life is archived only by LifeCompleted (CurrentLife cleared
    /// AND CompletedLives non-empty). A fresh session keeps working.
    let isLifeArchived (world: obj) : bool =
        let world = asWorld world

        let managerLife =
            world.SessionId
            |> Option.bind (fun session -> AgentProjection.tryFind session world.Projection.AgentProjections)
            |> Option.bind (fun projection -> projection.ManagerLife)

        match managerLife with
        | None -> false
        | Some life -> ManagerLifecycleProjection.isLifeArchived life

    let private slotView (slot: FinalityReviewCohort.CohortSlot) : obj =
        box
            {| agentId = slot.AgentId
               session = slot.ReviewerSessionId |> Option.map SessionId.value |> Option.defaultValue null
               ordinal = slot.ReviewerOrdinal
               isNew = slot.IsNew |}

    /// GLORY-045 roster algebra: ungraduated historical Reviewers + exactly one
    /// new slot, derived from durable facts only. JS-shaped slots.
    let cohortRoster (world: obj) : obj array =
        let world = asWorld world

        let activeRequest =
            currentLife world
            |> Option.bind (fun life -> life.ActiveFinality |> Option.map (fun request -> (life, request)))

        match activeRequest with
        | None -> [||]
        | Some(life, request) ->
            Wanxiangshu.Composition.Bridges.FinalityReview.FinalityReviewCohort.rosterOf
                world.Projection.AgentProjections
                life
                request
            |> List.map slotView
            |> List.toArray

    /// GLORY-045 roster algebra from a durable `AgentJournal.snapshot`:
    /// the projection is an opaque snapshot, lifeId / requestId are plain
    /// strings, and the answer is JS-shaped slots. No Fable types cross the
    /// boundary beyond the snapshot handle itself.
    let cohortRosterFromSnapshot (snapshot: obj) (lifeId: string) (requestId: string) : obj array =
        let projection = unbox<ProjectionSet> snapshot
        let ps = projection.AgentProjections
        let lifeId = ManagerLifeId.create lifeId
        let requestId = FinalityRequestId.create requestId

        let tryFindRequest managerLife =
            managerLife.CurrentLife
            |> Option.bind (fun life ->
                life.ActiveFinality
                |> Option.filter (fun request -> life.LifeId = lifeId && request.RequestId = requestId)
                |> Option.map (fun request -> life, request))

        let found =
            ps.Sessions
            |> Map.toList
            |> List.tryPick (fun (_, projection) -> projection.ManagerLife |> Option.bind tryFindRequest)

        match found with
        | None -> [||]
        | Some(life, request) ->
            Wanxiangshu.Composition.Bridges.FinalityReview.FinalityReviewCohort.rosterOf ps life request
            |> List.map slotView
            |> List.toArray

    /// GLORY-045: a Reviewer graduated iff it has a confirmed witness on one of
    /// the barriers this Life enlisted it on (derived from durable facts).
    let graduatedReviewer (world: obj) (reviewerSessionId: string) : bool =
        let world = asWorld world
        let reviewer = SessionId.create reviewerSessionId

        currentLife world
        |> Option.bind (fun life -> Map.tryFind reviewer life.EnlistedReviewers)
        |> Option.map (fun standing ->
            Wanxiangshu.Composition.Bridges.FinalityReview.FinalityReviewCohort.graduatedReviewer
                world.Projection.AgentProjections
                reviewer
                standing)
        |> Option.defaultValue false

    // ── Life admission (FINALITY-022 / INTERACTION-AUTHORITY-009) ────────────

    let private authorityProfileOf
        (authorityKind: string)
        (rootMessageId: string)
        (selectedAgent: string)
        (peerAgent: string)
        (tier: string)
        : PromptAuthority.AuthorityExecutionProfile =
        let kind =
            if authorityKind = "human-root" then
                PromptAuthority.RootAuthorityKind.HumanRoot
            else
                PromptAuthority.RootAuthorityKind.AgentOwnerRoot

        let role = Role.Manager

        let tier = if tier = "deep" then AgentTier.Deep else AgentTier.Fast

        { SessionId = SessionId.create "ses-authority"
          LogicalRunId = LogicalRunId.create "run-authority"
          AuthorityRootUserMessageId = AuthorityRootUserMessageId.create rootMessageId
          AuthorityKind = kind
          SelectedAgent = selectedAgent
          PeerAgent = peerAgent
          CanonicalRole = role
          SelectedTier = tier }

    let private lifecycleOf (world: obj) : ManagerLifeProjection =
        let world = asWorld world

        world.SessionId
        |> Option.bind (fun session -> AgentProjection.tryFind session world.Projection.AgentProjections)
        |> Option.bind (fun projection -> projection.ManagerLife)
        |> Option.defaultValue ManagerLifecycleProjection.empty

    /// FINALITY-022 ending admission: `{ kind: 'existing-life' }` when a Life is
    /// open, `{ kind: 'initial-agent-owner-migration' }` for a first AgentOwner
    /// ending, `{ kind: 'no-life' }` after terminal closure.
    let endingAdmission
        (world: obj)
        (authorityKind: string)
        (rootMessageId: string)
        (selectedAgent: string)
        (peerAgent: string)
        (tier: string)
        (opening: obj)
        : obj =
        let world = asWorld world
        let lifecycle = lifecycleOf world

        let profile =
            authorityProfileOf authorityKind rootMessageId selectedAgent peerAgent tier

        let xTrace =
            if isNull opening then
                None
            else
                Some
                    { XTraceProjectionState.Opening =
                        Some
                            { AssignmentText = str (opening?assignmentText)
                              AuthoritativeRequirements = []
                              ConstitutiveBody = "" }
                      Parts = []
                      Terminal = None }

        match ManagerLifeAdmission.ending lifecycle (Some profile) xTrace with
        | EndingLifeAdmission.ExistingLife _ -> box {| kind = "existing-life" |}
        | EndingLifeAdmission.InitialAgentOwnerMigration _ -> box {| kind = "initial-agent-owner-migration" |}
        | EndingLifeAdmission.NoLife -> box {| kind = "no-life" |}

    /// FINALITY-022 HumanRoot opening: true only for the exact authority-root
    /// physical message; session-level authority never generalizes.
    let tryHumanRootOpening (world: obj) (authorityKind: string) (rootMessageId: string) (messageId: string) : bool =
        let world = asWorld world
        let lifecycle = lifecycleOf world

        let profile =
            authorityProfileOf authorityKind rootMessageId "fast-manager" "deep-manager" "fast"

        let opening =
            ManagerLifeAdmission.tryHumanRootOpening lifecycle (Some profile) (PhysicalUserMessageId.create messageId)

        Option.isSome opening

    // ── ReviewerOutcome (FINALITY-006 drain typing) ───────────────────────────

    let reviewerOutcomeKinds () : string array = [| "Revision"; "Confirmed" |]

    let reviewerOutcomeRevision (workRecord: string) : obj =
        box
            {| kind = "revision"
               workRecord = workRecord |}

    let reviewerOutcomeConfirmed (reviewerSessionId: string) (barrierId: string) : obj =
        box
            {| kind = "confirmed"
               reviewerSessionId = reviewerSessionId
               barrierId = barrierId |}
