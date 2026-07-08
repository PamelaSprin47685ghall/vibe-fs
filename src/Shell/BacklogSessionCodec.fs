module Wanxiangshu.Shell.BacklogSessionCodec

open Fable.Core.JsInterop
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.SessionProjectionStore

let inputOfPart (part: Part<obj>) : obj =
    match part with
    | ToolPart(_, _, Some state, _) -> state.input
    | _ -> null

let backlogEntryFromTodoInput (input: obj) : BacklogEntry =
    { ahaMoments = Dyn.str input "ahaMoments" |> fun s -> s.Trim()
      changesAndReasons = Dyn.str input "changesAndReasons" |> fun s -> s.Trim()
      gotchas = Dyn.str input "gotchas" |> fun s -> s.Trim()
      lessonsAndConventions = Dyn.str input "lessonsAndConventions" |> fun s -> s.Trim()
      plan = Dyn.str input "plan" |> fun s -> s.Trim() }

let reportFromFlatPartWithProjection
    (host: Host)
    (projection: ProjectionStore)
    (fp: FlatPart<obj>)
    : BacklogEntry option =
    match fp.part with
    | ToolPart(_, callID, Some state, _) ->
        let entry = backlogEntryFromTodoInput state.input

        let hasAny =
            entry.ahaMoments <> ""
            || entry.changesAndReasons <> ""
            || entry.gotchas <> ""
            || entry.lessonsAndConventions <> ""
            || entry.plan <> ""

        if hasAny then
            Some entry
        elif host = Opencode || host = Mux || host = Omp then
            projection.TryGetReport(host, callID)
            |> Option.map (fun r -> { entry with ahaMoments = r })
        else
            None
    | _ -> None
