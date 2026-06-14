module VibeFs.Kernel.ReviewRuntime

open VibeFs.Kernel.Review
open VibeFs.Kernel.ReviewSession

/// Pending review resolutions and their abort suppressors, keyed by session.
/// Mutable because it holds host callbacks the kernel cannot replay purely.
type SessionEffects =
    { pendingResolutions: Map<string, ReviewResult -> unit>
      abortSuppressors: Map<string, unit -> unit> }

let emptyEffects : SessionEffects =
    { pendingResolutions = Map.empty; abortSuppressors = Map.empty }

let private setPending effects sessionId resolve =
    { effects with pendingResolutions = Map.add sessionId resolve effects.pendingResolutions }

/// Fire the pending resolver for a session, then clean up its suppressor.
/// Returns true when a resolver was actually waiting.
let resolvePending (effects: SessionEffects) sessionId result : SessionEffects * bool =
    match Map.tryFind sessionId effects.pendingResolutions with
    | None -> effects, false
    | Some resolve ->
        resolve result
        let without = { effects with pendingResolutions = Map.remove sessionId effects.pendingResolutions }
        match Map.tryFind sessionId effects.abortSuppressors with
        | Some suppress -> suppress(); { without with abortSuppressors = Map.remove sessionId without.abortSuppressors }, true
        | None -> without, true

/// Resolve every pending session in a tree as Terminated, firing suppressors.
let disposeSessionTree (effects: SessionEffects) sessionIds : SessionEffects =
    sessionIds
    |> List.fold (fun acc id ->
        match Map.tryFind id acc.pendingResolutions with
        | Some resolve ->
            resolve Terminated
            let cleared = { acc with pendingResolutions = Map.remove id acc.pendingResolutions }
            match Map.tryFind id cleared.abortSuppressors with
            | Some suppress -> suppress(); { cleared with abortSuppressors = Map.remove id cleared.abortSuppressors }
            | None -> cleared
        | None -> acc) effects

/// The full host-facing review store: pure registry kernel plus effect side-table.
type ReviewStore =
    abstract member activateReview: sessionID: string * task: string * createdAt: int -> unit
    abstract member deactivateReview: sessionID: string -> unit
    abstract member clearReviewSessions: unit -> unit
    abstract member tryLockReview: sessionID: string -> bool
    abstract member unlockReview: sessionID: string -> unit
    abstract member setPendingReview: sessionID: string * resolve: (ReviewResult -> unit) -> unit
    abstract member setAbortSuppressor: sessionID: string * suppress: (unit -> unit) -> unit
    abstract member resolvePendingReview: sessionID: string * result: ReviewResult -> bool
    abstract member getReviewTask: sessionID: string -> string option
    abstract member getReviewState: sessionID: string -> ReviewState option
    abstract member isReviewActive: sessionID: string -> bool
    abstract member addChild: parentID: string * childID: string -> unit

let createReviewStore () : ReviewStore =
    let mutable registry = emptyRegistry
    let mutable effects = emptyEffects

    let allDescendantIds sessionId =
        let rec collect id =
            match Map.tryFind id registry with
            | None -> [ id ]
            | Some session -> id :: (session.childIds |> List.collect collect)
        collect sessionId

    { new ReviewStore with
        member _.activateReview(sessionID, task, createdAt) =
            registry <- reduce registry (RegistryAction.Activate(sessionID, task, createdAt))
        member _.deactivateReview(sessionID) =
            effects <- disposeSessionTree effects (allDescendantIds sessionID)
            registry <- reduce registry (RegistryAction.Deactivate sessionID)
        member _.clearReviewSessions() =
            effects <- disposeSessionTree effects (Map.keys effects.pendingResolutions |> List.ofSeq)
            registry <- emptyRegistry
        member _.tryLockReview(sessionID) =
            if not (canTransition registry sessionID (Lock sessionID)) then false
            else
                registry <- reduce registry (RegistryAction.Lock(sessionID, sessionID))
                true
        member _.unlockReview(sessionID) =
            registry <- reduce registry (RegistryAction.Unlock sessionID)
        member _.setPendingReview(sessionID, resolve) =
            effects <- setPending effects sessionID resolve
        member _.setAbortSuppressor(sessionID, suppress) =
            effects <- { effects with abortSuppressors = Map.add sessionID suppress effects.abortSuppressors }
        member _.resolvePendingReview(sessionID, result) =
            registry <- reduce registry (actionFor sessionID result)
            let next, fired = resolvePending effects sessionID result
            effects <- next
            fired
        member _.getReviewTask(sessionID) = taskOf registry sessionID
        member _.getReviewState(sessionID) = stateOf registry sessionID
        member _.isReviewActive(sessionID) = sessionIsActive registry sessionID
        member _.addChild(parentID, childID) =
            registry <- reduce registry (RegistryAction.AddChild(parentID, childID)) }
