namespace Wanxiangshu.OpenCode

/// JS-native boundary for HOST-BOUNDARY-010 / HOST-012 shared cross-instance
/// state. The physical Map/Set/atom singletons in SharedState stay opaque;
/// only narrow put/get/clear/root operations cross the edge. Every operation
/// closes over the same module-level singleton, so a mutation made through
/// one import is visible through another — the behavioral proof that
/// SessionParents / ReviewGuardNudges / RootWorkspace are live singletons
/// shared across plugin instances (root + worktree), not per-instance copies.
module SharedStateSurface =

    /// SessionParents: cross-instance parent registry. The worktree plugin's
    /// VerdictTool reads the entry the root instance registered; a per-instance
    /// Map would miss it and REVIEW-008 would fail closed.
    let putSessionParent (childId: string) (parentId: string) : unit =
        SharedState.SessionParents.[childId] <- parentId

    let getSessionParent (childId: string) : string =
        match SharedState.SessionParents.TryGetValue(childId) with
        | true, v -> v
        | false, _ -> null

    let clearSessionParents () : unit = SharedState.SessionParents.Clear()

    /// ReviewGuardNudges: cross-instance at-most-once reservation. The key
    /// must NOT contain RuntimeId (root + worktree would each send a twin
    /// nudge for the same missing-verdict occasion).
    let addReviewGuardNudge (key: string) : unit =
        SharedState.ReviewGuardNudges.Add(key) |> ignore

    let hasReviewGuardNudge (key: string) : bool =
        SharedState.ReviewGuardNudges.Contains(key)

    let clearReviewGuardNudges () : unit =
        SharedState.clearReviewGuardNudgesForTests ()

    /// RootWorkspace: mutable atom set by whichever plugin instance boots
    /// first. Worktree instances pin blogger companions here so the system
    /// prompt survives the manager worktree release at publish.
    let setRootWorkspace (path: string) : unit = SharedState.RootWorkspace <- Some path

    let clearRootWorkspace () : unit = SharedState.RootWorkspace <- None

    let tryGetRootWorkspace () : string =
        SharedState.RootWorkspace |> Option.defaultValue null
