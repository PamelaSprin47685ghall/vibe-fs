module Wanxiangshu.Tests.OmpMagicTodoTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Runtime.BacklogSessionCodec
open Wanxiangshu.Runtime.BacklogSession
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Tests.BacklogMessageBuilders
open Wanxiangshu.Runtime.BacklogProjectionBuild

module Dyn = Wanxiangshu.Runtime.Dyn

/// Two `BacklogSession(omp)` instances must share the same backing store.
let sharedSessionStoreByHost () =
    let scope = RuntimeScope()
    let s1 = BacklogSession(omp, scope)
    let s2 = BacklogSession(omp, scope)
    let callID = "omp-call-1"
    let report = "omp report body"
    s1.CaptureReport(callID, report)
    check "second omp session sees first's report" (s2.TakeReport callID = report)
    check "report drained" (s2.TakeReport callID = "")

/// CaptureReport by Omp host must not collide with capture by the Opencode host.
let hostPartitionedReports () =
    let scope = RuntimeScope()
    let sOmp = BacklogSession(omp, scope)
    let sOc = BacklogSession(Opencode, scope)
    let callID = "shared-call-id"
    sOmp.CaptureReport(callID, "omp only")
    sOc.CaptureReport(callID, "opencode only")
    equal "omp retrieves omp report" "omp only" (sOmp.TakeReport callID)
    equal "opencode retrieves oc report" "opencode only" (sOc.TakeReport callID)
    check "omp session drained" (sOmp.TakeReport callID = "")
    check "oc session drained" (sOc.TakeReport callID = "")

let backlogEntryFromTodoInputHostAgnostic () =
    let input1 =
        createObj
            [ "ahaMoments", box "from-input"
              "changesAndReasons", box ""
              "gotchas", box ""
              "lessonsAndConventions", box ""
              "plan", box "" ]

    let input2 =
        createObj
            [ "ahaMoments", box ""
              "changesAndReasons", box ""
              "gotchas", box ""
              "lessonsAndConventions", box ""
              "plan", box "" ]

    let input3 = createObj []
    equal "host-agnostic 1" "from-input" (backlogEntryFromTodoInput input1).ahaMoments
    equal "host-agnostic 2" "" (backlogEntryFromTodoInput input2).ahaMoments
    equal "host-agnostic 3" "" (backlogEntryFromTodoInput input3).ahaMoments
    equal "host-agnostic opencode same" "from-input" (backlogEntryFromTodoInput input1).ahaMoments

let inputOfPartNonTool () =
    let p: Part<obj> = TextPart "hi"
    equal "non-tool returns null" null (Wanxiangshu.Runtime.BacklogSessionCodec.inputOfPart p)

/// Mirror BacklogReplaySpecs.opencode: CaptureReport on BacklogSession(omp), replay
/// with empty ahaMoments in input, captured report is returned.
let replayBacklogOmpFallsBackToCapturedReport () =
    let scope = RuntimeScope()
    let session = BacklogSession(omp, scope)
    let callID = "omp-fallback-c1"
    session.CaptureReport(callID, "captured omp report")

    let input =
        box (
            createObj
                [ "ahaMoments", box ""
                  "changesAndReasons", box ""
                  "gotchas", box ""
                  "lessonsAndConventions", box ""
                  "plan", box "" ]
        )

    let msgs =
        [ { info =
              { mkInfo "m1" Assistant with
                  sessionID = "test" }
            parts = [ ToolPart(todoWriteToolName Omp, callID, Some(mkState "completed" "Todos updated." input), null) ]
            source = Native
            raw = null } ]

    let backlog = replayBacklogFor omp scope msgs
    check "omp replay: one entry" (backlog.Length = 1)
    equal "omp replay: captured report preserved" "captured omp report" backlog.[0].ahaMoments
