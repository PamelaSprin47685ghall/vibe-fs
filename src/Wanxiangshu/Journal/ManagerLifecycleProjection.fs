namespace Wanxiangshu.Journal

open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity

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
    { ReviewerOrdinal: int
      Barriers: ReviewBarrierId list }

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

/// GLORY-011: one FinalityRequest's derived view inside a Life.
type FinalityRequestProjection =
    { RequestId: FinalityRequestId
      GitTreeHash: GitTreeHash
      LastWordsRef: BlobRef
      LastWordsDigest: BlobDigest
      ProviderRun: ProviderRunIdentity
      ToolCallId: ToolCallId
      Members: Map<SessionId, ReviewMemberRef>
      Resolution: FinalityResolution }

/// GLORY-011: one Manager Life's derived view. Answers "who is the current
/// Life, is it activated, where is the compression floor, is a suicide active,
/// what was the last rejection, is there a blessing, is the Life complete". It
/// never answers "what runs next" (ARCH-001).
type LifeProjection =
    { LifeId: ManagerLifeId
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
      Completed: bool }

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

    /// Fold one lifecycle fact onto the session's lifecycle state.
    ///
    /// Replays are idempotent by identity (PERSIST-010): re-applying the same
    /// LifeOpened / WorkActivated / FinalityRejected leaves the state unchanged.
    let fold
        (state: ManagerLifeProjection)
        (fact: ManagerLifecycleFact)
        : Result<ManagerLifeProjection, ManagerLifeFoldRejection> =
        match fact with
        | ManagerLifecycleFact.LifeOpened payload ->
            match state.CurrentLife with
            // First Life of the session, or the first Life after a completed one.
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
            // Replay of the same Life opener.
            | Some life when life.LifeId = payload.LifeId -> Ok state
            | Some _ -> Error ManagerLifeFoldRejection.LifeAlreadyOpen

        | ManagerLifecycleFact.WorkActivated payload ->
            match state.CurrentLife with
            | Some life when life.LifeId = payload.LifeId ->
                // Replay of the same activation is idempotent.
                match life.ProtectedPrefixEnd with
                | Some _ -> Ok state
                | None ->
                    Ok(
                        withLife
                            { life with
                                ProtectedPrefixEnd = Some { Sequence = payload.ProtectedPrefixEndSequence } }
                            state
                    )
            | _ -> Error ManagerLifeFoldRejection.LifeUnknown

        | ManagerLifecycleFact.FinalityRequested payload ->
            match state.CurrentLife with
            | Some life when life.LifeId = payload.LifeId ->
                match life.ActiveFinality with
                // GLORY-055: a closed request (rejected/blessed/undecided) may
                // be replaced; an open one may not.
                | Some { Resolution = FinalityResolution.Open } ->
                    Error ManagerLifeFoldRejection.FinalityAlreadyActive
                | _ ->
                    Ok(
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
                                          Resolution = FinalityResolution.Open } }
                            state
                    )
            | _ -> Error ManagerLifeFoldRejection.LifeUnknown

        | ManagerLifecycleFact.FinalityReviewerEnlisted payload ->
            match state.CurrentLife with
            | Some life when life.LifeId = payload.LifeId ->
                match life.ActiveFinality with
                | Some request when request.RequestId = payload.RequestId ->
                    match request.Resolution with
                    | FinalityResolution.Open ->
                        let memberRef =
                            { ReviewerSessionId = payload.ReviewerSessionId
                              ReviewerOrdinal = payload.ReviewerOrdinal
                              BarrierId = payload.BarrierId
                              IsNewReviewer = payload.IsNewReviewer }

                        // Replay of the same enlistment is idempotent.
                        match Map.tryFind payload.ReviewerSessionId request.Members with
                        | Some existing when existing.BarrierId = payload.BarrierId -> Ok state
                        | _ ->
                            let standing =
                                match Map.tryFind payload.ReviewerSessionId life.EnlistedReviewers with
                                | Some previous ->
                                    { previous with
                                        Barriers = payload.BarrierId :: previous.Barriers }
                                | None ->
                                    { ReviewerOrdinal = payload.ReviewerOrdinal
                                      Barriers = [ payload.BarrierId ] }

                            Ok(
                                withLife
                                    { life with
                                        EnlistedReviewers =
                                            Map.add payload.ReviewerSessionId standing life.EnlistedReviewers
                                        ActiveFinality =
                                            Some
                                                { request with
                                                    Members = Map.add payload.ReviewerSessionId memberRef request.Members } }
                                    state
                            )
                    | _ -> Error ManagerLifeFoldRejection.UnknownRequest
                | _ -> Error ManagerLifeFoldRejection.UnknownRequest
            | _ -> Error ManagerLifeFoldRejection.LifeUnknown

        | ManagerLifecycleFact.FinalityRejected payload ->
            match state.CurrentLife with
            | Some life when life.LifeId = payload.LifeId ->
                match life.ActiveFinality with
                | Some request when request.RequestId = payload.RequestId ->
                    match request.Resolution with
                    | FinalityResolution.Open ->
                        Ok(
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
                        )
                    | _ -> Error ManagerLifeFoldRejection.UnknownRequest
                | _ -> Error ManagerLifeFoldRejection.UnknownRequest
            | _ -> Error ManagerLifeFoldRejection.LifeUnknown

        | ManagerLifecycleFact.FinalityBlessed payload ->
            match state.CurrentLife with
            | Some life when life.LifeId = payload.LifeId ->
                match life.ActiveFinality with
                | Some request when request.RequestId = payload.RequestId ->
                    match request.Resolution with
                    | FinalityResolution.Open ->
                        let blessing =
                            { RequestId = payload.RequestId
                              WorkRecordBundleRef = payload.WorkRecordBundleRef
                              WorkRecordBundleDigest = payload.WorkRecordBundleDigest }

                        Ok(
                            withLife
                                { life with
                                    ActiveFinality =
                                        Some
                                            { request with
                                                Resolution = FinalityResolution.Blessed blessing }
                                    LastBlessing = Some blessing }
                                state
                        )
                    | _ -> Error ManagerLifeFoldRejection.UnknownRequest
                | _ -> Error ManagerLifeFoldRejection.UnknownRequest
            | _ -> Error ManagerLifeFoldRejection.LifeUnknown

        | ManagerLifecycleFact.FinalityUndecided payload ->
            // GLORY-057: closes the request exactly like a rejection, but never
            // fabricates a wound record.
            match state.CurrentLife with
            | Some life when life.LifeId = payload.LifeId ->
                match life.ActiveFinality with
                | Some request when request.RequestId = payload.RequestId ->
                    match request.Resolution with
                    | FinalityResolution.Open ->
                        Ok(
                            withLife
                                { life with
                                    ActiveFinality =
                                        Some
                                            { request with
                                                Resolution = FinalityResolution.Undecided } }
                                state
                        )
                    | _ -> Error ManagerLifeFoldRejection.UnknownRequest
                | _ -> Error ManagerLifeFoldRejection.UnknownRequest
            | _ -> Error ManagerLifeFoldRejection.LifeUnknown

        | ManagerLifecycleFact.LifeCompleted payload ->
            match state.CurrentLife with
            | Some life when life.LifeId = payload.LifeId ->
                // Replay idempotent: a completed Life is already archived.
                if life.Completed then
                    Ok state
                else
                    Ok(
                        archive
                            { life with
                                CompletedTerminal = Some payload.TerminalRef
                                Completed = true }
                            state
                    )
            | _ -> Error ManagerLifeFoldRejection.LifeUnknown

    /// GLORY-011: an open request is still awaiting its cohort resolution.
    let isOpen (request: FinalityRequestProjection) =
        match request.Resolution with
        | FinalityResolution.Open -> true
        | FinalityResolution.Rejected _
        | FinalityResolution.Blessed _
        | FinalityResolution.Undecided -> false
