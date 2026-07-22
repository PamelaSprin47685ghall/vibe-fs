module Wanxiangshu.Runtime.EventLogRuntimeStore

open Fable.Core
open Wanxiangshu.Kernel.EventSourcing.EventEnvelope
open Wanxiangshu.Kernel.EventSourcing.EventKind
open Wanxiangshu.Runtime.EventStore
open Thoth.Json

[<Import("stat", "node:fs/promises")>]
let private statAsync (path: string) : JS.Promise<obj> = jsNative

let directoryExists (path: string) : JS.Promise<bool> =
    promise {
        try
            let! stats = statAsync path
            return unbox<bool> (Wanxiangshu.Runtime.Dyn.callMethod0 stats "isDirectory")
        with _ ->
            return false
    }

let mutable private stores: Map<string, EventLogStore> = Map.empty

let getStore (workspaceRoot: string) : EventLogStore =
    match Map.tryFind workspaceRoot stores with
    | Some s ->
        if s.Poisoned then s.ResetPoison()
        s
    | None ->
        let s = EventLogStore workspaceRoot
        stores <- Map.add workspaceRoot s stores
        s

let appendAndCache (workspaceRoot: string) (e: WanEvent) : JS.Promise<Result<unit, string>> =
    getStore(workspaceRoot).AppendEvent e

let appendAndCacheOrFail (workspaceRoot: string) (e: WanEvent) : JS.Promise<unit> =
    getStore(workspaceRoot).AppendEventOrFail e

let appendEventsAndCacheOrFail (workspaceRoot: string) (events: WanEvent list) : JS.Promise<unit> =
    getStore(workspaceRoot).AppendEventsOrFail events

let remove (workspaceRoot: string) : bool =
    let found = Map.containsKey workspaceRoot stores
    stores <- Map.remove workspaceRoot stores
    found

let tryRemove (workspaceRoot: string) : unit =
    stores <- Map.remove workspaceRoot stores

let clear () : unit = stores <- Map.empty

let count () : int = Map.count stores

let ids () : string list = stores |> Map.toList |> List.map fst

let assistantPayload
    (assistantMessage: string)
    (agent: string option)
    (model: string option)
    (turnId: string)
    (openTodos: string list)
    : Map<string, string> =
    let skipTodo = Wanxiangshu.Runtime.Nudge.NudgeSkipTokens.parseSkipTodo assistantMessage
    let skipReview = Wanxiangshu.Runtime.Nudge.NudgeSkipTokens.parseSkipReview assistantMessage

    let baseMap =
        Map
            [ "assistantMessage", assistantMessage
              "turnId", turnId
              "skipTodo", if skipTodo then "true" else "false"
              "skipReview", if skipReview then "true" else "false"
              "openTodosJson", Encode.Auto.toString (0, openTodos) ]

    let withAgent =
        match agent with
        | Some a when a <> "" -> Map.add "agent" a baseMap
        | _ -> baseMap

    match model with
    | Some m when m <> "" -> Map.add "model" m withAgent
    | _ -> withAgent
