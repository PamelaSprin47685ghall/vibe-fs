module Wanxiangshu.Kernel.ReviewSession.Query

open Wanxiangshu.Kernel.ReviewSession.Types
open Wanxiangshu.Kernel.ReviewSession.StateMachine
open Wanxiangshu.Kernel.ReviewSession.Registry

let sessionIsActive registry id =
    Map.tryFind id registry |> Option.map (fun s -> isActive s.state) |> Option.defaultValue false

let taskOf registry id = Map.tryFind id registry |> Option.bind (fun s -> s.originalTask)

let stateOf registry id =
    Map.tryFind id registry |> Option.map (fun s -> s.state)

let canTransition registry id command =
    match Map.tryFind id registry with
    | None -> false
    | Some session ->
        let nextState, _ = transition session.state command
        nextState <> session.state

let versionOf registry id =
    Map.tryFind id registry |> Option.map (fun session -> session.version)

let reduceIfVersionMatches (registry: Registry) (id: string) (expectedVersion: int) (action: RegistryAction) : Registry option =
    match Map.tryFind id registry with
    | Some session when session.version = expectedVersion -> Some(reduce registry action)
    | _ -> None
