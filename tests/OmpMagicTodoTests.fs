module VibeFs.Tests.OmpMagicTodoTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.HostTools
open VibeFs.Kernel.Messaging
open VibeFs.Shell.BacklogSessionCodec
open VibeFs.Omp.MagicTodo
module Dyn = VibeFs.Shell.Dyn

/// Two `BacklogSession(omp)` instances must share the same backing store.
let sharedSessionStoreByHost () =
    let s1 = BacklogSession(omp)
    let s2 = BacklogSession(omp)
    let callID = "omp-call-1"
    let report = "omp report body"
    s1.CaptureReport(callID, report)
    check "second omp session sees first's report" (s2.TakeReport callID = report)
    check "report drained" (s2.TakeReport callID = "")

/// CaptureReport by Omp host must not collide with capture by the Opencode host.
let hostPartitionedReports () =
    let sOmp = BacklogSession(omp)
    let sOc = BacklogSession(Opencode)
    let callID = "shared-call-id"
    sOmp.CaptureReport(callID, "omp only")
    sOc.CaptureReport(callID, "opencode only")
    equal "omp retrieves omp report" "omp only" (sOmp.TakeReport callID)
    equal "opencode retrieves oc report" "opencode only" (sOc.TakeReport callID)
    check "omp session drained" (sOmp.TakeReport callID = "")
    check "oc session drained" (sOc.TakeReport callID = "")

let backlogReportFromTodoInputHostAgnostic () =
    let input1 = createObj [ "completedWorkReport", box "from-input" ]
    let input2 = createObj [ "completedWorkReport", box "" ]
    let input3 = createObj []
    equal "host-agnostic 1" "from-input" (backlogReportFromTodoInput omp input1)
    equal "host-agnostic 2" "" (backlogReportFromTodoInput omp input2)
    equal "host-agnostic 3" "" (backlogReportFromTodoInput omp input3)
    equal "host-agnostic opencode same" "from-input" (backlogReportFromTodoInput Opencode input1)

let inputOfPartNonTool () =
    let p : Part<obj> = TextPart "hi"
    equal "non-tool returns null" null (VibeFs.Shell.BacklogSessionCodec.inputOfPart p)