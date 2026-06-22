module VibeFs.Shell.ReviewRuntime

open VibeFs.Kernel.ReviewSession

/// The full host-facing review store: pure registry kernel plus effect side-table.
type ReviewStore =
    abstract member activateReview: sessionID: string * task: string * createdAt: int64 -> unit
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

let syncReviewProjection (store: ReviewStore) (sessionID: string) (task: string option) : unit =
    if sessionID = "" then ()
    else
        match task with
        | Some nextTask ->
            if store.getReviewTask sessionID <> Some nextTask || not (store.isReviewActive sessionID) then
                if store.getReviewState sessionID |> Option.isSome then
                    store.deactivateReview sessionID
                store.activateReview(sessionID, nextTask, System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        | None ->
            if store.getReviewState sessionID |> Option.isSome then
                store.deactivateReview sessionID
