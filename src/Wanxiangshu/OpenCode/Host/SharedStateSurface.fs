namespace Wanxiangshu.OpenCode

/// JS-native boundary for HOST-BOUNDARY-010 shared cross-instance state and
/// HOST-BOUNDARY-031 root workspace first-binding. The physical Map/Set
/// singletons and RootWorkspace runtime stay opaque. Every operation
/// closes over the same module-level singleton, so a mutation made through
/// one import is visible through another — the behavioral proof that
/// SessionParents / RootWorkspace are live singletons
/// shared across plugin instances (root + worktree), not per-instance copies.
module SharedStateSurface =

    /// SessionParents: cross-instance parent registry.
    let putSessionParent (childId: string) (parentId: string) : unit =
        SharedState.SessionParents.[childId] <- parentId

    let getSessionParent (childId: string) : string =
        match SharedState.SessionParents.TryGetValue(childId) with
        | true, v -> v
        | false, _ -> null

    let clearSessionParents () : unit = SharedState.SessionParents.Clear()

    let tryBindRootWorkspace (path: string) : bool =
        (RootWorkspaceProcess.local ()).Binder.TryBind(Option.ofObj path)

    let tryGetRootWorkspace () : string =
        (RootWorkspaceProcess.local ()).Reader.TryRead() |> Option.defaultValue null

    let firstBoundRootWorkspace (candidates: string array) : string =
        let runtime = RootWorkspaceRuntime()

        candidates
        |> Array.iter (fun candidate -> runtime.Binder.TryBind(Option.ofObj candidate) |> ignore)

        runtime.Reader.TryRead() |> Option.defaultValue null

    let selectContinuationDirectory (candidate: string) (candidateExists: bool) (rootWorkspace: string) : string =
        let reader =
            { new IRootWorkspaceReader with
                member _.TryRead() = Option.ofObj rootWorkspace }

        RootWorkspaceDirectory.select (fun path -> candidateExists && path = candidate) reader (Option.ofObj candidate)
        |> Option.defaultValue null
