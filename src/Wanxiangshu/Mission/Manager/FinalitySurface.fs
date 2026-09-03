namespace Wanxiangshu.Mission.Manager

open System
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Change
open Wanxiangshu.Composition.Bridges.FinalityReview
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Context.Trace
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Persistence.Journal

/// JS-native semantic surface for Finality laws (PR 6 exemplar).
///
/// A JS test starts with `emptyWorld()` and applies each plain lifecycle event
/// through `applyEvent`. `world` is an opaque handle: the production fold runs
/// once per call, and the F# `ProjectionSet` / `LifeProjection` / fact types
/// never cross the boundary (JS-SEMANTIC-SURFACE-002/003/005). The JS test does not own
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

    let private roleResult (value: obj) : Result<Role, string> =
        match Roles.tryParseRole (str value) with
        | Some role -> Ok role
        | None -> Error(sprintf "unknown role: %s" (str value))

    let private participantIdentityInputResult (value: obj) : Result<ParticipantIdentityInput, string> =
        let role =
            if str (value?canonicalRole) = "bookkeeper" then
                Some None
            else
                Roles.tryParseRole (str (value?canonicalRole)) |> Option.map Some

        let origin =
            match str (value?origin) with
            | "ResolvedAtRoot" -> Some PersonaOrigin.ResolvedAtRoot
            | "InheritedFromOwner" -> Some PersonaOrigin.InheritedFromOwner
            | _ -> None

        match role, origin with
        | None, _ -> Error(sprintf "unknown role: %s" (str (value?canonicalRole)))
        | _, None -> Error(sprintf "unknown participant identity origin: %s" (str (value?origin)))
        | Some role, Some origin ->
            Ok
                { SelectedAgent = str (value?selectedAgent)
                  Role = role
                  Persona = str (value?persona)
                  PersonaCatalogVersion = unbox<int> value?personaCatalogVersion
                  Origin = origin }

    let private identitySeedResult (value: obj) : Result<PromptAuthority.IdentitySeed, string> =
        if isNull value then
            Error "authority root requires an identity seed"
        else
            participantIdentityInputResult (value?participantIdentity)
            |> Result.bind (fun identity ->
                match str (value?kind) with
                | "RootSelection" ->
                    PromptAuthority.IdentitySeedInput.RootSelectionInput identity
                    |> PromptIdentitySeed.rehydrate
                    |> Result.mapError (fun error -> sprintf "invalid identity seed: %A" error)
                | "InheritedFromOwner" ->
                    PromptAuthority.IdentitySeedInput.InheritedFromOwnerInput
                        { OwnerSessionId = SessionId.create (str (value?ownerSession))
                          OwnerLogicalRunId = LogicalRunId.create (str (value?ownerLogicalRun))
                          OwnerAuthorityRootUserMessageId =
                            AuthorityRootUserMessageId.create (str (value?ownerAuthorityRoot))
                          ParticipantIdentity = identity }
                    |> PromptIdentitySeed.rehydrate
                    |> Result.mapError (fun error -> sprintf "invalid identity seed: %A" error)
                | kind -> Error(sprintf "unknown identity seed kind: %s" kind))

    let private ownershipResult (value: obj) : Result<HandleOwnership, string> =
        match str value with
        | "host-owned-hidden" -> Ok HandleOwnership.HostOwnedHidden
        | "durable-parent-handle" -> Ok HandleOwnership.DurableParentHandle
        | unknown -> Error(sprintf "unknown handle ownership: %s" unknown)

    let private completionResult (value: obj) : Result<HandleCompletionKind, string> =
        match str value with
        | "send-failure" -> Ok HandleCompletionKind.SendFailure
        | "cancelled" -> Ok HandleCompletionKind.Cancelled
        | "terminal" -> Ok HandleCompletionKind.Terminal
        | unknown -> Error(sprintf "unknown handle completion kind: %s" unknown)

    let private stringArrayOf (value: obj) : string list =
        if isNull value then
            []
        else
            unbox<string array> value |> Array.toList

    let private optionalBlobRef (value: obj) : BlobRef option =
        if isNull value then
            None
        else
            Some(BlobRef.create (str value))

    let private optionalBlobDigest (value: obj) : BlobDigest option =
        if isNull value then
            None
        else
            Some(BlobDigest.create (str value))

    let private handleOf (value: obj) : HandleId =
        HandleId.Agent(AgentHandleId.create (str value))

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
        | "authority-root-accepted" ->
            identitySeedResult (event?identitySeed)
            |> Result.map (fun identitySeed ->
                Fact.Agent(
                    AgentFact.Prompt(
                        PromptFactCases.AuthorityRootAccepted
                            { SchemaVersion = 2
                              SessionId = sessionId
                              LogicalRunId = LogicalRunId.create (str (event?logicalRunId))
                              AuthorityRootUserMessageId =
                                AuthorityRootUserMessageId.create (str (event?authorityRootUserMessageId))
                              AuthorityKind = str (event?authorityKind)
                              IdentitySeed = identitySeed }
                    )
                ))
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
                               FirstPhysicalUserMessageId =
                                PhysicalUserMessageId.create (str (event?firstPhysicalUserMessageId))
                               SecondProviderRun = ProviderRunIdentity.create (str (event?secondProviderRun))
                               SecondToolCallId = ToolCallId.create (str (event?secondToolCallId))
                               SecondPhysicalUserMessageId =
                                PhysicalUserMessageId.create (str (event?secondPhysicalUserMessageId)) |}
                    )
                )
            )
        | "handle-linked" ->
            match roleResult event?role, ownershipResult event?ownership with
            | Ok role, Ok ownership ->
                Ok(
                    Fact.Agent(
                        AgentFact.Execution(
                            ExecutionFactCases.HandleLinked
                                {| ParentSessionId = sessionId
                                   ChildSessionId = SessionId.create (str (event?childSessionId))
                                   Handle = handleOf (event?handleId)
                                   TargetAgent = str (event?targetAgent)
                                   Byname = str (event?byname)
                                   CanonicalRole = role
                                   Ownership = ownership |}
                        )
                    )
                )
            | Error error, _
            | _, Error error -> Error error
        | "handle-completed" ->
            match completionResult event?completionKind with
            | Error error -> Error error
            | Ok completionKind ->
                Ok(
                    Fact.Agent(
                        AgentFact.Execution(
                            ExecutionFactCases.HandleCompleted
                                {| ParentSessionId = sessionId
                                   Handle = handleOf (event?handleId)
                                   Kind = completionKind
                                   CompletionRef = optionalBlobRef (event?completionRef)
                                   CompletionDigest = optionalBlobDigest (event?completionDigest) |}
                        )
                    )
                )
        | "handle-retired" ->
            Ok(
                Fact.Agent(
                    AgentFact.Execution(
                        ExecutionFactCases.HandleRetired
                            {| ParentSessionId = sessionId
                               Handle = handleOf (event?handleId) |}
                    )
                )
            )
        | other -> Error $"unknown finality event kind: {other}"

    let private sessionOfFact (fact: Fact) : SessionId option =
        match fact with
        | Fact.ManagerLifecycle lifecycle ->
            match lifecycle with
            | ManagerLifecycleFact.LifeOpened payload -> Some payload.SessionId
            | ManagerLifecycleFact.WorkActivated payload -> Some payload.SessionId
            | ManagerLifecycleFact.FinalityRequested payload -> Some payload.SessionId
            | ManagerLifecycleFact.FinalityReviewerEnlisted payload -> Some payload.SessionId
            | ManagerLifecycleFact.FinalityRejected payload -> Some payload.SessionId
            | ManagerLifecycleFact.FinalitySiblingSteered payload -> Some payload.SessionId
            | ManagerLifecycleFact.FinalityBlessed payload -> Some payload.SessionId
            | ManagerLifecycleFact.FinalityUndecided payload -> Some payload.SessionId
            | ManagerLifecycleFact.LifeCompleted payload -> Some payload.SessionId
        | Fact.Agent(AgentFact.Review(ReviewFactCases.ReviewBarrierStarted payload)) -> Some payload.ReviewerSessionId
        | Fact.Agent(AgentFact.Review(ReviewFactCases.ConfirmedReviewWitness payload)) -> Some payload.ReviewerSessionId
        | Fact.Agent(AgentFact.Execution(ExecutionFactCases.HandleLinked payload)) -> Some payload.ParentSessionId
        | Fact.Agent(AgentFact.Execution(ExecutionFactCases.HandleCompleted payload)) -> Some payload.ParentSessionId
        | Fact.Agent(AgentFact.Execution(ExecutionFactCases.HandleRetired payload)) -> Some payload.ParentSessionId
        | Fact.Agent(AgentFact.Prompt(PromptFactCases.AuthorityRootAccepted payload)) -> Some payload.SessionId
        | _ -> None

    /// Keep the first manager session observed; later facts fold onto it.
    let private firstSession (current: SessionId option) (fact: Fact) : SessionId option =
        match current with
        | Some _ -> current
        | None -> sessionOfFact fact

    /// Fold one event through the fact-only fold (no synthetic envelope).
    let private foldOneEvent
        (projection: ProjectionSet)
        (managerSession: SessionId option)
        (event: obj)
        : Result<ProjectionSet * SessionId option, string> =
        lifecycleEventOf event
        |> Result.bind (fun fact ->
            let session = firstSession managerSession fact

            let eventKind =
                match str event?kind with
                | "finality-requested" -> "FinalityRequested"
                | other -> other

            Fold.foldFact projection fact
            |> Result.mapError (fun rejection -> sprintf "fold rejected %s: %A" eventKind rejection)
            |> Result.map (fun next -> (next, session)))

    /// Create an opaque empty projection capability.
    let emptyWorld () : obj =
        box
            { Projection = Fold.empty
              SessionId = None }

    /// Apply exactly one JS lifecycle event through the production fold.
    /// Returns `{ ok: true, world }` or `{ ok: false, error }`.
    let applyEvent (world: obj) (event: obj) : obj =
        let world = asWorld world

        match foldOneEvent world.Projection world.SessionId event with
        | Error message -> box {| ok = false; error = message |}
        | Ok(projection, sessionId) ->
            box
                {| ok = true
                   world =
                    { Projection = projection
                      SessionId = sessionId } |}

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
                   openingCursorSequence = int (XTraceCursor.sequence life.OpeningCursor)
                   protectedPrefixEnd =
                    life.ProtectedPrefixEnd
                    |> Option.map (XTraceCursor.sequence >> int >> box)
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

    /// GLORY-045 roster algebra from an opaque projection handle: the lifeId /
    /// requestId are plain strings and the answer is JS-shaped slots. The
    /// projection remains inside the FinalitySurface world capability.
    let cohortRosterFromSnapshot (snapshot: obj) (lifeId: string) (requestId: string) : obj array =
        let world = asWorld snapshot
        let ps = world.Projection.AgentProjections
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

    let private authorityKindResult (value: string) : Result<PromptAuthority.RootAuthorityKind, string> =
        match value with
        | "human-root" -> Ok PromptAuthority.RootAuthorityKind.HumanRoot
        | "agent-owner-root" -> Ok PromptAuthority.RootAuthorityKind.AgentOwnerRoot
        | unknown -> Error(sprintf "unknown authority kind: %s" unknown)

    let private inheritedIdentitySeedResult (value: obj) : Result<PromptAuthority.IdentitySeed, string> =
        if isNull value then
            Error "AgentOwnerRoot requires an inherited owner identity seed"
        elif str (value?kind) <> "InheritedFromOwner" then
            Error "AgentOwnerRoot requires an inherited owner identity seed"
        else
            identitySeedResult value

    let private authorityProfileOf
        (agentProjections: AgentProjectionSet)
        (authorityKind: string)
        (rootMessageId: string)
        (selectedAgent: string)
        (identitySeedValue: obj)
        : Result<PromptAuthority.AuthorityExecutionProfile, string> =
        match authorityKindResult authorityKind with
        | Ok PromptAuthority.RootAuthorityKind.HumanRoot ->
            ParticipantIdentity.resolveAtRoot selectedAgent
            |> Result.bind (fun identity ->
                let input = ParticipantIdentity.toInput identity

                ParticipantIdentity.rehydrate None { input with Role = Some Role.Manager })
            |> Result.mapError (fun error -> sprintf "invalid participant identity: %A" error)
            |> Result.bind (fun participantIdentity ->
                PromptAuthority.createAuthorityExecutionProfile
                    (SessionId.create "ses-authority")
                    (LogicalRunId.create "run-authority")
                    (AuthorityRootUserMessageId.create rootMessageId)
                    PromptAuthority.RootAuthorityKind.HumanRoot
                    participantIdentity)
        | Ok PromptAuthority.RootAuthorityKind.AgentOwnerRoot ->
            inheritedIdentitySeedResult identitySeedValue
            |> Result.bind (fun identitySeed ->
                let activeOwner =
                    PromptAuthority.identitySeedOwner identitySeed
                    |> Option.bind (fun (ownerSessionId, _, _) ->
                        AgentProjection.tryFind ownerSessionId agentProjections
                        |> Option.bind (fun projection -> projection.PromptAuthority)
                        |> Option.bind (fun authority -> authority.ActiveLogicalRun))

                PromptAuthority.validateInheritedIdentitySeedAgainstActiveOwner activeOwner identitySeed
                |> Result.mapError (fun error -> sprintf "invalid identity seed: %A" error)
                |> Result.bind (fun identity ->
                    let actualRole = ParticipantIdentity.role identity
                    let actualAgent = ParticipantIdentity.selectedAgent identity

                    if actualAgent <> selectedAgent then
                        Error(
                            sprintf
                                "participant identity selected agent mismatch: expected %s, actual %s"
                                selectedAgent
                                actualAgent
                        )
                    elif actualRole <> Some Role.Manager then
                        Error(
                            sprintf
                                "invalid participant identity: %A"
                                (ParticipantIdentityError.RoleMismatch(Some Role.Manager, actualRole))
                        )
                    else
                        PromptAuthority.createAuthorityExecutionProfileFromSeed
                            (SessionId.create "ses-authority")
                            (LogicalRunId.create "run-authority")
                            (AuthorityRootUserMessageId.create rootMessageId)
                            PromptAuthority.RootAuthorityKind.AgentOwnerRoot
                            identitySeed))
        | Error error -> Error error

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
        ignore peerAgent
        ignore tier
        let world = asWorld world
        let lifecycle = lifecycleOf world

        let identitySeed = if isNull opening then null else opening?identitySeed

        match
            authorityProfileOf world.Projection.AgentProjections authorityKind rootMessageId selectedAgent identitySeed
        with
        | Error error -> box {| ok = false; error = error |}
        | Ok profile ->
            let openingEvidence =
                if isNull opening then
                    None
                else
                    Some
                        { XTraceOpeningEvidence.AssignmentText = str (opening?assignmentText)
                          AuthoritativeRequirements = []
                          ConstitutiveBody = "" }

            match ManagerLifeAdmission.ending lifecycle (Some profile) openingEvidence with
            | EndingLifeAdmission.ExistingLife _ -> box {| kind = "existing-life" |}
            | EndingLifeAdmission.InitialAgentOwnerMigration _ -> box {| kind = "initial-agent-owner-migration" |}
            | EndingLifeAdmission.NoLife -> box {| kind = "no-life" |}

    /// FINALITY-022 HumanRoot opening: true only for the exact authority-root
    /// physical message; session-level authority never generalizes.
    let tryHumanRootOpening (world: obj) (authorityKind: string) (rootMessageId: string) (messageId: string) : bool =
        let world = asWorld world
        let lifecycle = lifecycleOf world

        match authorityProfileOf world.Projection.AgentProjections authorityKind rootMessageId "manager" null with
        | Error _ -> false
        | Ok profile ->
            let opening =
                ManagerLifeAdmission.tryHumanRootOpening
                    lifecycle
                    (Some profile)
                    (PhysicalUserMessageId.create messageId)

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

    /// FINALITY-001: the Finality capability is granted only to Manager. The
    /// role and permission labels are plain strings at this boundary.
    let isAllowed (role: string) (permission: string) : bool =
        Wanxiangshu.Participant.Persona.OfficeCapabilitySurface.isAllowed role permission

    /// FINALITY-027: durable parent-visible handles are the Manager's only
    /// background obligation. Hidden Reviewer handles stay invisible through
    /// the same HandleProjection.listable rule used by TerminalPolicy.
    let backgroundOutstanding (world: obj) (sessionId: string) : bool =
        let world = asWorld world
        let session = SessionId.create sessionId

        AgentProjection.tryFind session world.Projection.AgentProjections
        |> Option.bind (fun projection -> projection.Handles)
        |> Option.map (HandleProjection.listable >> List.isEmpty >> not)
        |> Option.defaultValue false

    // ── ManagerJob history projection (FINALITY-028) ─────────────────────────

    let private recordPublishClaimFact
        (projection: OrchestratorProjection)
        (jobId: ManagerJobId)
        (event: obj)
        : Result<OrchestratorProjection, string> =
        match
            OrchestratorProjection.tryFind jobId projection
            |> Option.bind (fun job -> job.RebasedCandidateReady)
        with
        | Some rebased ->
            Ok(
                OrchestratorProjection.recordPublishClaimed
                    jobId
                    {| RebasedCommit = rebased.RebasedCommit
                       ExpectedHead = CommitHash.create (str (event?expectedHead)) |}
                    projection
            )
        | None -> Error "publish claimed for a job with no rebased candidate (ORCH-004)"

    let private recordJobFact
        (projection: OrchestratorProjection)
        (event: obj)
        : Result<OrchestratorProjection, string> =
        let jobId = ManagerJobId.create (str (event?jobId))

        match str (event?fact) with
        | "CandidateReady" ->
            Ok(
                OrchestratorProjection.recordCandidateReady
                    jobId
                    {| CandidateCommit = CommitHash.create (str (event?candidateCommit))
                       PreRebaseReviewBarrierId = ReviewBarrierId.create (str (event?preRebaseReviewBarrierId)) |}
                    projection
            )
        | "ConflictDetected" ->
            Ok(
                OrchestratorProjection.recordConflictDetected
                    jobId
                    {| CandidateCommit = CommitHash.create (str (event?candidateCommit))
                       TargetHeadSnapshot = CommitHash.create (str (event?targetHeadSnapshot))
                       ConflictFiles = stringArrayOf (event?conflictFiles)
                       DiagnosticsDigest = str (event?diagnosticsDigest) |}
                    projection
            )
        | "RebasedCandidateReady" ->
            Ok(
                OrchestratorProjection.recordRebasedCandidateReady
                    jobId
                    {| RebasedCommit = CommitHash.create (str (event?rebasedCommit))
                       TargetHeadSnapshot = CommitHash.create (str (event?targetHeadSnapshot))
                       PostRebaseReviewBarrierId = ReviewBarrierId.create (str (event?postRebaseReviewBarrierId)) |}
                    projection
            )
        | "PublishClaimed" -> recordPublishClaimFact projection jobId event
        | "Published" ->
            Ok(
                OrchestratorProjection.recordTerminal
                    jobId
                    (Wanxiangshu.Change.TerminalOutcome.Published
                        {| CandidateCommit = CommitHash.create (str (event?candidateCommit))
                           ResultingTargetHead = CommitHash.create (str (event?resultingTargetHead)) |})
                    projection
            )
        | "JobFailed" ->
            Ok(
                OrchestratorProjection.recordTerminal
                    jobId
                    (Wanxiangshu.Change.TerminalOutcome.Failed(str (event?reason)))
                    projection
            )
        | "JobAbandoned" ->
            Ok(OrchestratorProjection.recordTerminal jobId Wanxiangshu.Change.TerminalOutcome.Abandoned projection)
        | other -> Error $"unknown ManagerJob fact: {other}"

    let private foldOneJobProjectionEvent
        (projection: OrchestratorProjection)
        (event: obj)
        : Result<OrchestratorProjection, string> =
        match str (event?kind) with
        | "job-created" ->
            let job =
                {| ManagerJobId = ManagerJobId.create (str (event?jobId))
                   ManagerSessionId = SessionId.create (str (event?managerSessionId))
                   ManagerAgent = str (event?managerAgent)
                   Byname = str (event?byname)
                   WorktreeIdentity = WorktreeIdentity.create (str (event?worktreeIdentity))
                   WorktreePath = WorktreePath.create (str (event?worktreePath))
                   TargetRef = TargetRef.create (str (event?targetRef))
                   TargetBranchFrozen = str (event?targetBranchFrozen) |}

            Ok(OrchestratorProjection.createJob job projection)
        | "job-fact" -> recordJobFact projection event
        | other -> Error $"unknown ManagerJob event kind: {other}"

    let private jobFactsView (job: ManagerJobProjection) =
        [ if job.CandidateReady.IsSome then
              yield "CandidateReady"

          if job.ConflictDetected.IsSome then
              yield "ConflictDetected"

          if job.RebasedCandidateReady.IsSome then
              yield "RebasedCandidateReady"

          if job.PublishClaimed.IsSome then
              yield "PublishClaimed"

          match job.Terminal with
          | Some(Wanxiangshu.Change.TerminalOutcome.Published _) -> yield "Published"
          | Some(Wanxiangshu.Change.TerminalOutcome.Failed _) -> yield "JobFailed"
          | Some Wanxiangshu.Change.TerminalOutcome.Abandoned -> yield "JobAbandoned"
          | None -> () ]
        |> List.toArray

    let private jobView (job: ManagerJobProjection) : obj =
        box
            {| jobId = ManagerJobId.value job.ManagerJobId
               managerSessionId = SessionId.value job.ManagerSessionId
               managerAgent = job.ManagerAgent
               byname = job.Byname
               worktreeIdentity = WorktreeIdentity.value job.WorktreeIdentity
               worktreePath = WorktreePath.value job.WorktreePath
               targetRef = TargetRef.value job.TargetRef
               targetBranchFrozen = job.TargetBranchFrozen
               facts = jobFactsView job |}

    /// Create an opaque empty ManagerJob projection capability.
    let emptyJobProjection () : obj = box OrchestratorProjection.empty

    /// Apply exactly one plain ManagerJob event through its owner projection.
    let applyJobProjectionEvent (projection: obj) (event: obj) : obj =
        match foldOneJobProjectionEvent (projection :?> OrchestratorProjection) event with
        | Error message -> box {| ok = false; error = message |}
        | Ok next -> box {| ok = true; projection = box next |}

    /// Return the JS-native job and active-job views of an opaque projection.
    let jobProjectionView (projection: obj) : obj =
        let projection = projection :?> OrchestratorProjection

        let jobs =
            projection.Jobs
            |> Map.toList
            |> List.map (fun (_, job) -> jobView job)
            |> List.toArray

        let activeJobs =
            OrchestratorProjection.activeJobs projection |> List.map jobView |> List.toArray

        box
            {| jobs = jobs
               activeJobs = activeJobs |}

    // ── ConfirmedReviewWitness & Blessing admission (FINALITY-002 / FINALITY-016) ──

    type private ConfirmedReviewWitnessHandle(witness: ConfirmedReviewWitness) =
        member _.Witness = witness

        static member Create(witness: ConfirmedReviewWitness) = ConfirmedReviewWitnessHandle(witness)

    let private confirmedReviewWitnessOf (value: obj) : ConfirmedReviewWitness =
        match value with
        | :? ConfirmedReviewWitnessHandle as handle -> handle.Witness
        | _ -> invalidArg "value" "FinalitySurface: expected a confirmed review witness handle"

    let private field (value: obj) (name: string) : obj =
        if isNull value then
            null
        else
            emitJsExpr (value, name) "$0[$1]"

    let private firstField (value: obj) (names: string array) : obj =
        names
        |> Array.tryPick (fun name ->
            let candidate = field value name
            if isNull candidate then None else Some candidate)
        |> Option.defaultValue null

    let private treeOf (value: obj) = GitTreeHash.create (str value)
    let private sessionOf (value: obj) = SessionId.create (str value)
    let private barrierOf (value: obj) = ReviewBarrierId.create (str value)

    let private physicalOf (value: obj) =
        PhysicalUserMessageId.create (str value)

    let private runOf (value: obj) = ProviderRunIdentity.create (str value)
    let private callOf (value: obj) = ToolCallId.create (str value)

    let private witnessOf (value: obj) : VerdictWitness =
        { ProviderRun = runOf (firstField value [| "ProviderRun"; "run" |])
          ToolCallId = callOf (firstField value [| "ToolCallId"; "call" |])
          GitTreeHash = treeOf (firstField value [| "GitTreeHash"; "tree" |])
          ReviewerSessionId = sessionOf (firstField value [| "ReviewerSessionId"; "reviewer" |]) }

    let private reviewWitnessOf (value: obj) : ReviewWitness =
        if isNull value then
            ReviewWitness.NoReview
        else
            match str (field value "state") with
            | "RevisionWitness" ->
                ReviewWitness.RevisionWitness
                    {| Report = str (field value "report")
                       GitTreeHash = treeOf (field value "tree") |}
            | "Confirmed" ->
                ReviewWitness.Confirmed
                    {| BarrierId = barrierOf (field value "barrier")
                       First = witnessOf (field value "first")
                       Second = witnessOf (field value "second")
                       GitTreeHash = treeOf (field value "tree")
                       FirstPhysicalUserMessageId =
                        physicalOf (firstField value [| "FirstPhysicalUserMessageId"; "firstPhysical" |])
                       SecondPhysicalUserMessageId =
                        physicalOf (firstField value [| "SecondPhysicalUserMessageId"; "secondPhysical" |]) |}
            | _ -> ReviewWitness.NoReview

    /// FINALITY-002: Project a ConfirmedReviewWitness from cohort member witnesses.
    let projectConfirmedReview (lifeId: string) (requestId: string) (tree: string) (memberWitnesses: obj array) : obj =
        let members =
            if isNull memberWitnesses then
                []
            else
                memberWitnesses
                |> Array.toList
                |> List.map (fun item ->
                    let reviewer = sessionOf (firstField item [| "reviewer"; "ReviewerSessionId" |])
                    let barrier = barrierOf (firstField item [| "barrier"; "BarrierId" |])
                    let witness = reviewWitnessOf (firstField item [| "witness"; "Witness" |])
                    (reviewer, barrier, witness))

        match
            ConfirmedReviewWitness.create
                (ManagerLifeId.create lifeId)
                (FinalityRequestId.create requestId)
                (treeOf (box tree))
                members
        with
        | Ok witness ->
            box
                {| ok = true
                   witness = ConfirmedReviewWitnessHandle.Create witness :> obj |}
        | Error error -> box {| ok = false; error = error |}

    let confirmedReviewWitnessTree (witness: obj) : string =
        let typed = confirmedReviewWitnessOf witness
        GitTreeHash.value (ConfirmedReviewWitness.gitTreeHash typed)

    /// FINALITY-002 / FINALITY-016: Blessing authorization gate.
    /// Evaluates currentTree against ConfirmedReviewWitness.
    /// Grants BlessingPermit on match; rejects with StaleWitness on mismatch.
    let grantBlessing (currentTree: string) (witness: obj) : obj =
        let typed = confirmedReviewWitnessOf witness

        match FinalityAdmission.grantBlessing (treeOf (box currentTree)) typed with
        | Ok permit ->
            box
                {| ok = true
                   permit =
                    box
                        {| tree = GitTreeHash.value (FinalityAdmission.permitTree permit)
                           lifeId = ManagerLifeId.value (FinalityAdmission.permitLifeId permit)
                           requestId = FinalityRequestId.value (FinalityAdmission.permitRequestId permit) |} |}
        | Error(BlessingAdmissionFailure.StaleWitness(curr, expected)) ->
            box
                {| ok = false
                   error = "StaleWitness"
                   currentTree = GitTreeHash.value curr
                   witnessTree = GitTreeHash.value expected |}
        | Error(BlessingAdmissionFailure.IncompleteCohort reason) ->
            box
                {| ok = false
                   error = "IncompleteCohort"
                   reason = reason |}
