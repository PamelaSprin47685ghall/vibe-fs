namespace Wanxiangshu.Mission.Manager.Life

open Wanxiangshu.Composition.Durable

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation.Identity

/// GLORY-011: one cohort member inside a FinalityRequest. A real identity
/// (session + stable ordinal + the request's barrier), not a program counter.
type ReviewMemberRef =
    { ReviewerSessionId: SessionId
      ReviewerOrdinal: int
      BarrierId: ReviewBarrierId
      IsNewReviewer: bool }

/// GLORY-045: a Reviewer's accumulated standing inside one Life. Accumulated
/// from `FinalityReviewerEnlisted` facts (every request's enlistment survives);
/// graduation is DERIVED from the reviewer's confirmed witness on one of these
/// barriers, never stored as a bool.
type ReviewerStanding =
    {
        ReviewerOrdinal: int
        Barriers: ReviewBarrierId list
        /// GLORY-045: the stable runtime agent id of this Reviewer session. Set
        /// on the FIRST enlistment (`finality-new-<requestId>` of the request
        /// that created it) and never overwritten, so every later request reuses
        /// the same id and its HandleCompleted still matches the original
        /// HandleLinked (handle id IS agent id, EXEC-009).
        AgentId: string
    }

/// GLORY-011: the rejecting member's evidence. Only durable facts — the
/// canonical work record blob — never a decision.
type RejectionEvidence =
    { RejectingReviewer: SessionId
      WorkRecordRef: BlobRef
      WorkRecordDigest: BlobDigest }

/// GLORY-060/062: the minor-work evidence handed to the Manager after the
/// whole cohort confirmed. The stable-ordinal canonical LWR bundle plus the
/// request that produced it (the second suicide completes THAT request).
type BlessingEvidence =
    { RequestId: FinalityRequestId
      WorkRecordBundleRef: BlobRef
      WorkRecordBundleDigest: BlobDigest }

/// GLORY-011: how an open FinalityRequest resolved. This is world state
/// derived from facts — not the interpreter's next step (ARCH-001). A closed
/// request may be replaced by a new `FinalityRequested` (GLORY-055).
[<RequireQualifiedAccess>]
type FinalityResolution =
    | Open
    | Rejected of RejectionEvidence
    | Blessed of BlessingEvidence
    | Undecided

/// GLORY-044: one sibling REVISE that was steered (not the rejecting wound).
type SiblingSteerEvidence =
    { ReviewerSessionId: SessionId
      BarrierId: ReviewBarrierId
      WorkRecordRef: BlobRef
      WorkRecordDigest: BlobDigest }

/// GLORY-011: one FinalityRequest's derived view inside a Life.
type FinalityRequestProjection =
    {
        RequestId: FinalityRequestId
        GitTreeHash: GitTreeHash
        LastWordsRef: BlobRef
        LastWordsDigest: BlobDigest
        ProviderRun: ProviderRunIdentity
        ToolCallId: ToolCallId
        Members: Map<SessionId, ReviewMemberRef>
        /// GLORY-044: sibling steers recorded for this request; does not affect Resolution.
        SiblingSteers: Map<SessionId, SiblingSteerEvidence>
        Resolution: FinalityResolution
    }

/// GLORY-011: one Manager Life's derived view. Answers "who is the current
/// Life, is it activated, where is the compression floor, is a suicide active,
/// what was the last rejection, is there a blessing, is the Life complete". It
/// never answers "what runs next" (ARCH-001).
/// DSL-state-combination: domain — optional finality/rejection/blessing and
/// completion evidence are durable facets of one Manager Life; they do not encode
/// what action executes next.
type LifeProjection =
    {
        LifeId: ManagerLifeId
        OpeningUserMessageId: PhysicalUserMessageId
        OpeningTextRef: BlobRef
        OpeningTextDigest: BlobDigest
        OpeningCursor: XTraceCursor
        ProtectedPrefixEnd: XTraceCursor option
        ActiveFinality: FinalityRequestProjection option
        /// Every Reviewer this Life ever enlisted, across all requests
        /// (GLORY-045 roster source). Never pruned.
        EnlistedReviewers: Map<SessionId, ReviewerStanding>
        LastRejectedWorkRecord: BlobRef option
        LastBlessing: BlessingEvidence option
        CompletedTerminal: BlobRef option
        Completed: bool
    }

/// GLORY-066: per-session lifecycle state. `CurrentLife` is the open Life
/// (None after LifeCompleted until the next HumanRoot); `CompletedLives`
/// preserves every finished Life's cursor range for Reawakening materialisation.
type ManagerLifeProjection =
    { CurrentLife: LifeProjection option
      CompletedLives: LifeProjection list }

[<RequireQualifiedAccess>]
type ManagerLifeFoldRejection =
    /// A lifecycle fact named a Life this session never opened.
    | LifeUnknown
    /// LifeOpened while an unfinished Life is open.
    | LifeAlreadyOpen
    /// FinalityRequested while an open (not rejected/confirmed) request exists.
    | FinalityAlreadyActive
    /// A review/rejection/confirmation named a request this Life does not have.
    | UnknownRequest

module ManagerLifecycleProjection =

    let empty: ManagerLifeProjection =
        { CurrentLife = None
          CompletedLives = [] }

    /// GLORY-065: LifeCompleted archives the Life and clears the current slot.
    let private archive (life: LifeProjection) (state: ManagerLifeProjection) =
        { CurrentLife = None
          CompletedLives = life :: state.CompletedLives }

    let private withLife (life: LifeProjection) (state: ManagerLifeProjection) = { state with CurrentLife = Some life }

    let private requireLife (lifeId: ManagerLifeId) (state: ManagerLifeProjection) =
        match state.CurrentLife with
        | Some life when life.LifeId = lifeId -> Ok life
        | _ -> Error ManagerLifeFoldRejection.LifeUnknown

    let private requireActiveRequest (requestId: FinalityRequestId) (life: LifeProjection) =
        match life.ActiveFinality with
        | Some request when request.RequestId = requestId -> Ok request
        | _ -> Error ManagerLifeFoldRejection.UnknownRequest

    let private requireOpenRequest (requestId: FinalityRequestId) (life: LifeProjection) =
        match life.ActiveFinality with
        | Some({ Resolution = FinalityResolution.Open } as request) when request.RequestId = requestId -> Ok request
        | _ -> Error ManagerLifeFoldRejection.UnknownRequest

    /// GLORY-055: closed request may be replaced; an open one may not.
    let private ensureFinalitySlotFree (life: LifeProjection) =
        match life.ActiveFinality with
        | Some { Resolution = FinalityResolution.Open } -> Error ManagerLifeFoldRejection.FinalityAlreadyActive
        | _ -> Ok()

    let private decideLifeOpened
        (state: ManagerLifeProjection)
        (payload:
            {| SessionId: SessionId
               LifeId: ManagerLifeId
               OpeningUserMessageId: PhysicalUserMessageId
               OpeningTextRef: BlobRef
               OpeningTextDigest: BlobDigest
               OpeningCursorSequence: int64 |})
        =
        match state.CurrentLife with
        | None ->
            Ok(
                withLife
                    { LifeId = payload.LifeId
                      OpeningUserMessageId = payload.OpeningUserMessageId
                      OpeningTextRef = payload.OpeningTextRef
                      OpeningTextDigest = payload.OpeningTextDigest
                      OpeningCursor = { Sequence = payload.OpeningCursorSequence }
                      ProtectedPrefixEnd = None
                      ActiveFinality = None
                      EnlistedReviewers = Map.empty
                      LastRejectedWorkRecord = None
                      LastBlessing = None
                      CompletedTerminal = None
                      Completed = false }
                    state
            )
        | Some life when life.LifeId = payload.LifeId -> Ok state
        | Some _ -> Error ManagerLifeFoldRejection.LifeAlreadyOpen

    let private applyWorkActivated
        (payload:
            {| SessionId: SessionId
               LifeId: ManagerLifeId
               ActivationPromptKey: PromptKey
               ProtectedPrefixEndSequence: int64 |})
        (life: LifeProjection)
        (state: ManagerLifeProjection)
        =
        match life.ProtectedPrefixEnd with
        | Some _ -> state
        | None ->
            withLife
                { life with
                    ProtectedPrefixEnd = Some { Sequence = payload.ProtectedPrefixEndSequence } }
                state

    let private openFinalityRequest
        (payload:
            {| SessionId: SessionId
               LifeId: ManagerLifeId
               RequestId: FinalityRequestId
               GitTreeHash: GitTreeHash
               LastWordsRef: BlobRef
               LastWordsDigest: BlobDigest
               ProviderRun: ProviderRunIdentity
               ToolCallId: ToolCallId |})
        (life: LifeProjection)
        (state: ManagerLifeProjection)
        =
        withLife
            { life with
                ActiveFinality =
                    Some
                        { RequestId = payload.RequestId
                          GitTreeHash = payload.GitTreeHash
                          LastWordsRef = payload.LastWordsRef
                          LastWordsDigest = payload.LastWordsDigest
                          ProviderRun = payload.ProviderRun
                          ToolCallId = payload.ToolCallId
                          Members = Map.empty
                          SiblingSteers = Map.empty
                          Resolution = FinalityResolution.Open } }
            state

    /// First enlistment: `finality-new-<requestId>` (GLORY-040); later requests reuse it.
    let private reviewerAgentId
        (payload:
            {| SessionId: SessionId
               LifeId: ManagerLifeId
               RequestId: FinalityRequestId
               ReviewerSessionId: SessionId
               ReviewerOrdinal: int
               BarrierId: ReviewBarrierId
               GitTreeHash: GitTreeHash
               IsNewReviewer: bool |})
        =
        if payload.IsNewReviewer then
            sprintf "finality-new-%s" (FinalityRequestId.value payload.RequestId)
        else
            sprintf "finality-reviewer-%s" (SessionId.value payload.ReviewerSessionId)

    let private upsertReviewerStanding
        (payload:
            {| SessionId: SessionId
               LifeId: ManagerLifeId
               RequestId: FinalityRequestId
               ReviewerSessionId: SessionId
               ReviewerOrdinal: int
               BarrierId: ReviewBarrierId
               GitTreeHash: GitTreeHash
               IsNewReviewer: bool |})
        (life: LifeProjection)
        =
        match Map.tryFind payload.ReviewerSessionId life.EnlistedReviewers with
        | Some previous ->
            { previous with
                Barriers = payload.BarrierId :: previous.Barriers }
        | None ->
            { ReviewerOrdinal = payload.ReviewerOrdinal
              Barriers = [ payload.BarrierId ]
              AgentId = reviewerAgentId payload }

    let private enlistReviewer
        (payload:
            {| SessionId: SessionId
               LifeId: ManagerLifeId
               RequestId: FinalityRequestId
               ReviewerSessionId: SessionId
               ReviewerOrdinal: int
               BarrierId: ReviewBarrierId
               GitTreeHash: GitTreeHash
               IsNewReviewer: bool |})
        (life: LifeProjection)
        (request: FinalityRequestProjection)
        (state: ManagerLifeProjection)
        =
        match Map.tryFind payload.ReviewerSessionId request.Members with
        | Some existing when existing.BarrierId = payload.BarrierId -> state
        | _ ->
            let memberRef =
                { ReviewerSessionId = payload.ReviewerSessionId
                  ReviewerOrdinal = payload.ReviewerOrdinal
                  BarrierId = payload.BarrierId
                  IsNewReviewer = payload.IsNewReviewer }

            let standing = upsertReviewerStanding payload life

            withLife
                { life with
                    EnlistedReviewers = Map.add payload.ReviewerSessionId standing life.EnlistedReviewers
                    ActiveFinality =
                        Some
                            { request with
                                Members = Map.add payload.ReviewerSessionId memberRef request.Members } }
                state

    let private rejectFinality
        (payload:
            {| SessionId: SessionId
               LifeId: ManagerLifeId
               RequestId: FinalityRequestId
               RejectingReviewerSessionId: SessionId
               BarrierId: ReviewBarrierId
               GitTreeHash: GitTreeHash
               WorkRecordRef: BlobRef
               WorkRecordDigest: BlobDigest |})
        (life: LifeProjection)
        (request: FinalityRequestProjection)
        (state: ManagerLifeProjection)
        =
        withLife
            { life with
                ActiveFinality =
                    Some
                        { request with
                            Resolution =
                                FinalityResolution.Rejected
                                    { RejectingReviewer = payload.RejectingReviewerSessionId
                                      WorkRecordRef = payload.WorkRecordRef
                                      WorkRecordDigest = payload.WorkRecordDigest } }
                LastRejectedWorkRecord = Some payload.WorkRecordRef }
            state

    let private recordSiblingSteer
        (payload:
            {| SessionId: SessionId
               LifeId: ManagerLifeId
               RequestId: FinalityRequestId
               ReviewerSessionId: SessionId
               BarrierId: ReviewBarrierId
               GitTreeHash: GitTreeHash
               WorkRecordRef: BlobRef
               WorkRecordDigest: BlobDigest |})
        (life: LifeProjection)
        (request: FinalityRequestProjection)
        (state: ManagerLifeProjection)
        =
        match Map.tryFind payload.ReviewerSessionId request.SiblingSteers with
        | Some existing when
            existing.BarrierId = payload.BarrierId
            && existing.WorkRecordRef = payload.WorkRecordRef
            ->
            state
        | _ ->
            let evidence =
                { ReviewerSessionId = payload.ReviewerSessionId
                  BarrierId = payload.BarrierId
                  WorkRecordRef = payload.WorkRecordRef
                  WorkRecordDigest = payload.WorkRecordDigest }

            withLife
                { life with
                    ActiveFinality =
                        Some
                            { request with
                                SiblingSteers = Map.add payload.ReviewerSessionId evidence request.SiblingSteers } }
                state

    let private blessFinality
        (payload:
            {| SessionId: SessionId
               LifeId: ManagerLifeId
               RequestId: FinalityRequestId
               GitTreeHash: GitTreeHash
               WorkRecordBundleRef: BlobRef
               WorkRecordBundleDigest: BlobDigest |})
        (life: LifeProjection)
        (request: FinalityRequestProjection)
        (state: ManagerLifeProjection)
        =
        let blessing =
            { RequestId = payload.RequestId
              WorkRecordBundleRef = payload.WorkRecordBundleRef
              WorkRecordBundleDigest = payload.WorkRecordBundleDigest }

        withLife
            { life with
                ActiveFinality =
                    Some
                        { request with
                            Resolution = FinalityResolution.Blessed blessing }
                LastBlessing = Some blessing }
            state

    let private undecideFinality
        (life: LifeProjection)
        (request: FinalityRequestProjection)
        (state: ManagerLifeProjection)
        =
        withLife
            { life with
                ActiveFinality =
                    Some
                        { request with
                            Resolution = FinalityResolution.Undecided } }
            state

    let private completeLife
        (payload:
            {| SessionId: SessionId
               LifeId: ManagerLifeId
               RequestId: FinalityRequestId
               TerminalRef: BlobRef
               TerminalDigest: BlobDigest |})
        (life: LifeProjection)
        (state: ManagerLifeProjection)
        =
        if life.Completed then
            state
        else
            archive
                { life with
                    CompletedTerminal = Some payload.TerminalRef
                    Completed = true }
                state

    /// Fold one lifecycle fact onto the session's lifecycle state.
    ///
    /// Replays are idempotent by identity (PERSIST-010): re-applying the same
    /// LifeOpened / WorkActivated / FinalityRejected leaves the state unchanged.
    let fold
        (state: ManagerLifeProjection)
        (fact: ManagerLifecycleFact)
        : Result<ManagerLifeProjection, ManagerLifeFoldRejection> =
        match fact with
        | ManagerLifecycleFact.LifeOpened payload -> decideLifeOpened state payload
        | ManagerLifecycleFact.WorkActivated payload ->
            result {
                let! life = requireLife payload.LifeId state
                return applyWorkActivated payload life state
            }
        | ManagerLifecycleFact.FinalityRequested payload ->
            result {
                let! life = requireLife payload.LifeId state
                do! ensureFinalitySlotFree life
                return openFinalityRequest payload life state
            }
        | ManagerLifecycleFact.FinalityReviewerEnlisted payload ->
            result {
                let! life = requireLife payload.LifeId state
                let! request = requireOpenRequest payload.RequestId life
                return enlistReviewer payload life request state
            }
        | ManagerLifecycleFact.FinalityRejected payload ->
            result {
                let! life = requireLife payload.LifeId state
                let! request = requireOpenRequest payload.RequestId life
                return rejectFinality payload life request state
            }
        | ManagerLifecycleFact.FinalitySiblingSteered payload ->
            // GLORY-044: record a sibling steer; never rewrite Rejected/Blessed/Undecided.
            result {
                let! life = requireLife payload.LifeId state
                let! request = requireActiveRequest payload.RequestId life
                return recordSiblingSteer payload life request state
            }
        | ManagerLifecycleFact.FinalityBlessed payload ->
            result {
                let! life = requireLife payload.LifeId state
                let! request = requireOpenRequest payload.RequestId life
                return blessFinality payload life request state
            }
        | ManagerLifecycleFact.FinalityUndecided payload ->
            // GLORY-057: closes the request exactly like a rejection, but never
            // fabricates a wound record.
            result {
                let! life = requireLife payload.LifeId state
                let! request = requireOpenRequest payload.RequestId life
                return undecideFinality life request state
            }
        | ManagerLifecycleFact.LifeCompleted payload ->
            result {
                let! life = requireLife payload.LifeId state
                return completeLife payload life state
            }

    /// GLORY-011: an open request is still awaiting its cohort resolution.
    let isOpen (request: FinalityRequestProjection) =
        match request.Resolution with
        | FinalityResolution.Open -> true
        | FinalityResolution.Rejected _
        | FinalityResolution.Blessed _
        | FinalityResolution.Undecided -> false

    /// GLORY-070/062: the session's Life is archived by the final rest-in-peace
    /// suicide (LifeCompleted clears `CurrentLife` and pushes into
    /// `CompletedLives`). The Manager's terminal was already `last_words`; a
    /// leftover turn must not be re-awakened with an idle encouragement.
    /// Distinct from a fresh session (CurrentLife None, CompletedLives empty),
    /// which is still planning and must keep working.
    let isLifeArchived (projection: ManagerLifeProjection) : bool =
        match projection.CurrentLife with
        | None -> not (List.isEmpty projection.CompletedLives)
        | Some _ -> false
