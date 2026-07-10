module Wanxiangshu.Kernel.ReviewSession.Registry

open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.ReviewSession.StateMachine

[<RequireQualifiedAccess>]
type RegistryAction =
    | Activate of id: string * task: string * createdAt: int64
    | Deactivate of id: string
    | Evict of cutoff: int64
    | Lock of id: string * reviewerId: string
    | Unlock of id: string
    | Accept of id: string
    | RequestRevision of id: string * feedback: string
    | AddChild of parentId: string * childId: string
    | Clear
    | NoOp

type Registry = Map<string, ReviewSession>

let emptyRegistry: Registry = Map.empty

let private set registry id session = Map.add id session registry

let private updateSession registry id f =
    match Map.tryFind id registry with
    | None -> registry
    | Some session -> set registry id (f session)

let private transitionSessionWithExtra registry id command extra =
    updateSession registry id (fun session ->
        let updated = applyCommand session command
        if updated = session then session else extra updated)

let private transitionSession registry id command =
    transitionSessionWithExtra registry id command (fun s -> s)

let private evictStale registry cutoff : Registry =
    let changed = registry |> Map.exists (fun _ s -> s.createdAt < cutoff)

    if not changed then
        registry
    else
        registry |> Map.filter (fun _ s -> s.createdAt >= cutoff)

let reduce (registry: Registry) (action: RegistryAction) : Registry =
    match action with
    | RegistryAction.Activate(id, task, createdAt) ->
        let seed =
            Map.tryFind id registry
            |> Option.defaultValue (empty id createdAt)
            |> withTask task

        set registry id (applyCommand seed (ReviewCommand.Activate task))
    | RegistryAction.Lock(id, reviewerId) -> transitionSession registry id (ReviewCommand.Lock reviewerId)
    | RegistryAction.Unlock id -> transitionSession registry id ReviewCommand.Unlock
    | RegistryAction.Accept id -> transitionSession registry id ReviewCommand.Accept
    | RegistryAction.RequestRevision(id, feedback) ->
        transitionSessionWithExtra registry id (ReviewCommand.RequestRevision feedback) (fun s ->
            withFeedback s feedback)
    | RegistryAction.Deactivate id -> Map.remove id registry
    | RegistryAction.Evict cutoff -> evictStale registry cutoff
    | RegistryAction.AddChild(parentId, childId) -> updateSession registry parentId (fun s -> addChild s childId)
    | RegistryAction.Clear -> emptyRegistry
    | RegistryAction.NoOp -> registry

let actionFor (id: string) (result: ReviewResult) : RegistryAction =
    match result with
    | Accepted _ -> RegistryAction.Accept id
    | NeedsRevision feedback -> RegistryAction.RequestRevision(id, feedback)
    | Terminated -> RegistryAction.NoOp
