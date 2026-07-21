module Wanxiangshu.Runtime.RuntimeScopeForgetSession

open Fable.Core.JsInterop
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.EventLogRuntimeStore
open Wanxiangshu.Runtime.FuzzyIteratorStore
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.SubagentIteratorStore

let private forgetRunnerSession (sessionId: string) (scope: RuntimeScope) : unit =
    match scope.TryFindKey "wanxiangshu.runner_state" with
    | None -> ()
    | Some v ->
        let rs = unbox<obj> v
        let active = Dyn.get rs "ActiveSessions" |> unbox<Set<string>>
        let logs = Dyn.get rs "LogBuffers" |> unbox<Map<string, string>>
        let childMap = Dyn.get rs "ChildByParent" |> unbox<Map<string, string>>
        let disposeMap = Dyn.get rs "ChildDispose" |> unbox<Map<string, unit -> unit>>

        Dyn.setKey rs "ActiveSessions" (box (Set.remove sessionId active))
        Dyn.setKey rs "LogBuffers" (box (Map.remove sessionId logs))

        Dyn.setKey
            rs
            "ChildByParent"
            (box (childMap |> Map.remove sessionId |> Map.filter (fun _ cid -> cid <> sessionId)))

        Dyn.setKey rs "ChildDispose" (box (Map.remove sessionId disposeMap))

let forgetSession (scope: RuntimeScope) (sessionId: string) : unit =
    if sessionId = "" then
        ()
    else
        scope.CloseSessionExecutor sessionId
        scope.RemoveTempFiles sessionId
        let prefix = sessionId + "\u0000"
        scope.ClearCapsFilesForSession prefix
        scope.ClearCapsInflightForSession prefix
        clearTypedIteratorScope scope.IteratorStore sessionId
        clearSubagentIteratorScope scope.SubagentIteratorStore sessionId
        scope.Remove("contextbudget_" + sessionId)

        match scope.TryFindKey("caps_epoch_session_" + sessionId) with
        | Some ep ->
            scope.Remove("caps_epoch_reverse_session_" + unbox<string> ep)
            scope.Remove("caps_epoch_session_" + sessionId)
        | None -> ()

        match scope.TryFindKey "livelock_state" with
        | Some m -> scope.Add("livelock_state", box (Map.remove sessionId (unbox<Map<string, obj>> m)))
        | None -> ()

        match scope.TryFindKey "wanxiangshu.semble_breakpoints" with
        | Some m -> scope.Add("wanxiangshu.semble_breakpoints", box (Map.remove sessionId (unbox<Map<string, int>> m)))
        | None -> ()

        forgetRunnerSession sessionId scope

        let key =
            if scope.WorkspaceRoot <> "" then
                scope.WorkspaceRoot
            else
                sessionId

        remove key |> ignore
