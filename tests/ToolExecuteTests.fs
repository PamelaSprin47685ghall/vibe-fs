module Wanxiangshu.Tests.ToolExecuteTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.ToolExecute
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.Session.Causality
open Wanxiangshu.Kernel.SubagentIntents
open Wanxiangshu.Kernel.ToolArgs

let wireDecodeFailureReturnsString () =
    let result = wireDecodeFailure "executor" (UpstreamTimeout 5)
    check "non-empty" (result.Length > 0)

let wireDomainFailureReturnsString () =
    let result = wireDomainFailure "test" (UpstreamTimeout 5)
    check "non-empty" (result.Length > 0)

let mapDecodeErrorOk () =
    let result = mapDecodeError "executor" (Ok 42)
    equal "ok" (Ok 42) result

let mapDecodeErrorError () =
    let result = mapDecodeError "executor" (Error(UpstreamTimeout 5))

    match result with
    | Error msg -> check "contains tool name" (msg.Contains "executor")
    | _ -> failwith "expected Error"

let run () =
    wireDecodeFailureReturnsString ()
    wireDomainFailureReturnsString ()
    mapDecodeErrorOk ()
    mapDecodeErrorError ()
