module Wanxiangshu.Kernel.WorkspaceState

open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality

type ChildSessionMeta =
    { agent: string
      parentSessionId: SessionId option }

type WorkspaceState =
    { childSessions: Map<ChildId, ChildSessionMeta> }

type WorkspaceEvent =
    | ChildRegistered of childId: ChildId * meta: ChildSessionMeta
    | ChildUnregistered of childId: ChildId

let empty: WorkspaceState = { childSessions = Map.empty }

let reduce (state: WorkspaceState) (event: WorkspaceEvent) : WorkspaceState =
    match event with
    | ChildRegistered(childId, meta) ->
        { state with
            childSessions = Map.add childId meta state.childSessions }
    | ChildUnregistered childId ->
        { state with
            childSessions = Map.remove childId state.childSessions }
