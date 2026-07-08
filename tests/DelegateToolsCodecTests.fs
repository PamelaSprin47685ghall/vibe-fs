module Wanxiangshu.Tests.DelegateToolsCodecTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Shell.DelegateToolsCodec

let decodeTaskCreateResultSuccessFalse () =
    let createResult = createObj [ "success", box false; "error", box "quota exceeded" ]

    match decodeTaskCreateResult createResult with
    | Error(InvalidIntent("delegate.create", "taskService", msg)) ->
        check "create success=false error msg" (msg = "quota exceeded")
    | _ -> check "create success=false" false

let decodeTaskCreateResultSuccessFalseDefaultMsg () =
    let createResult = createObj [ "success", box false ]

    match decodeTaskCreateResult createResult with
    | Error(InvalidIntent("delegate.create", "taskService", "create failed")) ->
        check "create success=false default msg" true
    | _ -> check "create success=false default msg" false

let decodeTaskCreateResultSuccessTrue () =
    let createResult =
        createObj [ "success", box true; "data", box (createObj [ "taskId", box "task-42" ]) ]

    match decodeTaskCreateResult createResult with
    | Ok r ->
        check "create ok success" r.Success
        check "create ok taskId" (r.TaskId = "task-42")
    | Error _ -> check "create success=true with taskId" false

let decodeTaskCreateResultNull () =
    match decodeTaskCreateResult null with
    | Error(InvalidIntent("delegate", "createResult", "missing")) -> check "createResult null" true
    | _ -> check "createResult null" false

let decodeTaskCreateResultSuccessNoData () =
    let createResult = createObj [ "success", box true ]

    match decodeTaskCreateResult createResult with
    | Error(InvalidIntent("delegate.create", "taskId", "missing or empty")) -> check "create success=true no data" true
    | _ -> check "create success=true no data" false

let decodeTaskCreateResultSuccessEmptyTaskId () =
    let createResult =
        createObj [ "success", box true; "data", box (createObj [ "taskId", box "" ]) ]

    match decodeTaskCreateResult createResult with
    | Error(InvalidIntent("delegate.create", "taskId", "missing or empty")) ->
        check "create success=true empty taskId" true
    | _ -> check "create success=true empty taskId" false

let decodeTaskReportEmpty () =
    let report = createObj [ "reportMarkdown", box "   " ]

    match decodeTaskReport report with
    | Error(InvalidIntent("delegate.report", "reportMarkdown", "missing or empty")) -> check "report empty trimmed" true
    | _ -> check "report empty" false

let decodeTaskReportOk () =
    let report = createObj [ "reportMarkdown", box "## done" ]

    match decodeTaskReport report with
    | Ok md -> check "report ok markdown" (md = "## done")
    | Error _ -> check "report non-empty" false

let run () =
    decodeTaskCreateResultSuccessFalse ()
    decodeTaskCreateResultSuccessFalseDefaultMsg ()
    decodeTaskCreateResultSuccessTrue ()
    decodeTaskCreateResultNull ()
    decodeTaskCreateResultSuccessNoData ()
    decodeTaskCreateResultSuccessEmptyTaskId ()
    decodeTaskReportEmpty ()
    decodeTaskReportOk ()
