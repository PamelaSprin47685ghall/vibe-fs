namespace Wanxiangshu.Journal

open Wanxiangshu.Domain
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity

/// GLORY-011: one FinalityRequest's derived view inside a Life.
///
/// `Rejected` and `Confirmed` mark a closed request: a rejected request may be
/// replaced by a new `FinalityRequested` (GLORY-055); a confirmed request is
/// consumed by `LifeCompleted`.
type FinalityRequestProjection =
    { RequestId: FinalityRequestId
      GitTreeHash: GitTreeHash
      LastWordsRef: BlobRef
      LastWordsDigest: BlobDigest
      ProviderRun: ProviderRunIdentity
      ToolCallId: ToolCallId
      ReviewerSessionId: SessionId option
      BarrierId: ReviewBarrierId option
      Rejected: bool
      Confirmed: bool }

/// GLORY-011: one Manager Life's derived view. Answers "who is the current
/// Life, is it activated, where is the compression floor, is a suicide active,
/// what was the last rejection, is the Life complete". It never answers "what
/// runs next" (ARCH-001).
type LifeProjection =
    { LifeId: ManagerLifeId
      OpeningUserMessageId: PhysicalUserMessageId
      OpeningTextRef: BlobRef
      OpeningTextDigest: BlobDigest
      OpeningCursor: XTraceCursor
      ProtectedPrefixEnd: XTraceCursor option
      ActiveFinality: FinalityRequestProjection option
      LastRejectedWorkRecord: BlobRef option
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
                          LastRejectedWorkRecord = None
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
                // GLORY-055: a rejected (or consumed) request is closed; a new
                // suicide opens a new request.
                | Some request when not request.Rejected && not request.Confirmed ->
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
                                          ReviewerSessionId = None
                                          BarrierId = None
                                          Rejected = false
                                          Confirmed = false } }
                            state
                    )
            | _ -> Error ManagerLifeFoldRejection.LifeUnknown

        | ManagerLifecycleFact.FinalityReviewStarted payload ->
            match state.CurrentLife with
            | Some life when life.LifeId = payload.LifeId ->
                match life.ActiveFinality with
                | Some request when request.RequestId = payload.RequestId ->
                    Ok(
                        withLife
                            { life with
                                ActiveFinality =
                                    Some
                                        { request with
                                            ReviewerSessionId = Some payload.ReviewerSessionId
                                            BarrierId = Some payload.BarrierId } }
                            state
                    )
                | _ -> Error ManagerLifeFoldRejection.UnknownRequest
            | _ -> Error ManagerLifeFoldRejection.LifeUnknown

        | ManagerLifecycleFact.FinalityRejected payload ->
            match state.CurrentLife with
            | Some life when life.LifeId = payload.LifeId ->
                match life.ActiveFinality with
                | Some request when request.RequestId = payload.RequestId ->
                    Ok(
                        withLife
                            { life with
                                ActiveFinality = Some { request with Rejected = true }
                                LastRejectedWorkRecord = Some payload.WorkRecordRef }
                            state
                    )
                | _ -> Error ManagerLifeFoldRejection.UnknownRequest
            | _ -> Error ManagerLifeFoldRejection.LifeUnknown

        | ManagerLifecycleFact.FinalityConfirmed payload ->
            match state.CurrentLife with
            | Some life when life.LifeId = payload.LifeId ->
                match life.ActiveFinality with
                | Some request when request.RequestId = payload.RequestId ->
                    Ok(
                        withLife
                            { life with
                                ActiveFinality = Some { request with Confirmed = true } }
                            state
                    )
                | _ -> Error ManagerLifeFoldRejection.UnknownRequest
            | _ -> Error ManagerLifeFoldRejection.LifeUnknown

        | ManagerLifecycleFact.FinalityUndecided payload ->
            // GLORY-057: closes the request exactly like a rejection, but never
            // fabricates a wound record.
            match state.CurrentLife with
            | Some life when life.LifeId = payload.LifeId ->
                match life.ActiveFinality with
                | Some request when request.RequestId = payload.RequestId ->
                    Ok(
                        withLife
                            { life with
                                ActiveFinality = Some { request with Rejected = true } }
                            state
                    )
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
