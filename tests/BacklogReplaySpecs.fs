module Wanxiangshu.Tests.BacklogReplaySpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.BacklogMessageBuilders
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.BacklogProjectionCore
open Wanxiangshu.Kernel.BacklogProjection
open Wanxiangshu.Opencode.BacklogSession
open Wanxiangshu.Shell.RuntimeScope

open Wanxiangshu.Tests.BacklogReplaySpecsMimocode
open Wanxiangshu.Tests.BacklogReplaySpecsFold

let private scope () = create ()

let replayBacklogOpencodeFallsBackToCapturedReportWhenInputMissing () =
    let s = scope ()
    s.Projection.CaptureReport(Opencode, "fallback-c1", "captured report")
    let input = box (createObj [ "todos", box [||] ])
    let msgs =
        [ { info = { mkInfo "m1" Assistant with sessionID = "test" }
            parts =
              [ ToolPart(
                  todoWriteToolNameDefault,
                  "fallback-c1",
                  Some(mkState "completed" "Todos updated." input),
                  null) ]
            source = Native
            raw = null } ]
    let backlog = replayBacklogFor Opencode s msgs
    check "opencode replay: tryGetReport fallback when input has no report" (backlog.Length = 1)
    check "opencode replay: captured report preserved" (backlog.[0].report = "captured report")

let replayBacklogMuxFallsBackToCapturedReportWhenInputMissing () =
    let s = scope ()
    s.Projection.CaptureReport(Mux, "fallback-m1", "captured mux report")
    let input = box (createObj [ "todos", box [||] ])
    let msgs =
        [ { info = { mkInfo "m1" Assistant with sessionID = "test" }
            parts =
              [ ToolPart(
                  todoWriteToolName Mux,
                  "fallback-m1",
                  Some(mkState "completed" "Todos updated." input),
                  null) ]
            source = Native
            raw = null } ]
    let backlog = replayBacklogFor Mux s msgs
    check "mux replay: tryGetReport fallback when input has no report" (backlog.Length = 1)
    check "mux replay: captured report preserved" (backlog.[0].report = "captured mux report")

let replayBacklogOpencodeDoesNotMergeConsecutiveTodoWrite () =
    let s = scope ()
    let msgs = [ todoWriteMsg "m1" "c1" "W1"; todoWriteMsg "m2" "c2" "W2"; todoWriteMsg "m3" "c3" "W3" ]
    let backlog = replayBacklogFor Opencode s msgs
    check "opencode: each todowrite is one backlog entry" (backlog.Length = 3)
    check "opencode: reports not merged" (backlog.[0].report = "W1" && backlog.[1].report = "W2" && backlog.[2].report = "W3")

let replayBacklogTest () =
    let s = scope ()
    let msgs = [ todoWriteMsg "m1" "c1" "Implemented parser"; todoWriteMsg "m2" "c2" "Fixed critical bug" ]
    let backlog = replayBacklog s msgs
    check "replay: backlog count" (backlog.Length = 2)
    check "replay: entry 1 report" (backlog.[0].report = "Implemented parser")
    check "replay: entry 2 report" (backlog.[1].report = "Fixed critical bug")

let replayEmpty () =
    let s = scope ()
    let backlog = replayBacklog s []
    check "replay empty: no backlog" (backlog.IsEmpty)

let replaySkipsEmpty () =
    let s = scope ()
    let msgs = [ todoWriteMsg "m1" "c1" "Report A"; todoWriteMsg "m2" "c2" "" ]
    let backlog = replayBacklog s msgs
    check "replay skips empty: only 1" (backlog.Length = 1)

let backlogSessionRefreshesBacklog () =
    let s = scope ()
    let session = BacklogSession(Opencode, s)
    let first = [ todoWriteMsg "m1" "c1" "R1" ]
    let second = [ todoWriteMsg "m1" "c1" "R1"; todoWriteMsg "m2" "c2" "R2" ]
    let _ = session.GetOrRebuildBacklog("test", first)
    let backlog = session.GetOrRebuildBacklog("test", second)
    check "backlog session: rebuilds stale backlog" (backlog.Length = 2)