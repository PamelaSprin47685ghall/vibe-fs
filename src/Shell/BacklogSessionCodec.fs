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

let backlogReportFromTodoInput (_host: Host) (input: obj) : string =
    let raw = Dyn.get input "completedWorkReport"
    if Dyn.isNullish raw then "" else string raw

let reportFromFlatPartWithProjection (host: Host) (projection: ProjectionStore) (fp: FlatPart<obj>) : string =
    match fp.part with
    | ToolPart(_, callID, Some state, _) ->
        let explicit = backlogReportFromTodoInput host state.input
        if explicit <> "" then explicit
        elif host = Opencode || host = Mux then
            projection.TryGetReport(host, callID) |> Option.defaultValue ""
        else ""
    | _ -> ""