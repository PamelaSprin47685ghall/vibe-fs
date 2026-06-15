module VibeFs.Kernel.WorkspaceState

open VibeFs.Kernel.Boundary

/// Metadata kept for every child session spawned inside a workspace.
/// The parent session id is optional because a top-level child session may
/// have no explicit parent.
type ChildSessionMeta =
    { agent: string
      parentSessionId: SessionId option }

/// Pure workspace state.  Right now it only tracks child sessions; more
/// workspace-level facts can be added here as the domain grows.
type WorkspaceState =
    { childSessions: Map<ChildId, ChildSessionMeta> }

/// Events that change workspace state.  Events are facts: once emitted they
/// describe something that already happened, and `reduce` folds them into the
/// current state without side effects.
type WorkspaceEvent =
    | ChildRegistered of childId: ChildId * meta: ChildSessionMeta
    | ChildUnregistered of childId: ChildId

let empty: WorkspaceState = { childSessions = Map.empty }

let reduce (state: WorkspaceState) (event: WorkspaceEvent) : WorkspaceState =
    match event with
    | ChildRegistered(childId, meta) ->
        { state with childSessions = Map.add childId meta state.childSessions }
    | ChildUnregistered childId ->
        { state with childSessions = Map.remove childId state.childSessions }
