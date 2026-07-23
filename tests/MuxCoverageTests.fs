module Wanxiangshu.Tests.MuxCoverageTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Hosts.Mux.Wrappers
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Hosts.Mux.CapsCodec
open Wanxiangshu.Hosts.Mux.BuiltinTools
open Wanxiangshu.Hosts.Mux.WrappersReview
open Wanxiangshu.Runtime.RuntimeScope

[<Import("createRequire", "node:module")>]
let private createRequire': string -> (string -> obj) = jsNative

[<Global("import.meta")>]
let private importMeta: obj = jsNative

let private requireFn: string -> obj = createRequire' (string importMeta?url)
let private pathModule: obj = requireFn "path"

let private join (a: string) (b: string) = unbox<string> (pathModule?join (a, b))

module Dyn = Wanxiangshu.Runtime.Dyn

// ── Mux.Wrappers ──────────────────────────────────────────────────────────────

let wrappersOptBoolTruthy () =
    let obj = createObj [ "flag", box true ]
    equal "optBool true" (Some true) (optBool obj "flag")

let wrappersOptBoolFalsy () =
    let obj = createObj [ "flag", box false ]
    equal "optBool false" (Some false) (optBool obj "flag")

let wrappersOptBoolMissing () =
    let obj = createObj [ "other", box 1 ]
    equal "optBool missing" None (optBool obj "flag")

let wrappersOptFieldPresent () =
    let obj = createObj [ "val", box "hello" ]
    equal "optField Some" "hello" (optField obj "val" |> Option.map string |> Option.defaultValue "")

let wrappersOptFieldNull () =
    let obj = createObj [ "val", box null ]
    equal "optField null" None (optField obj "val")

let wrappersOptFieldMissing () =
    let obj = createObj [ "other", box 1 ]
    equal "optField missing" None (optField obj "val")

let wrappersRequireStrArrayValid () =
    let obj = createObj [ "items", box [| "a"; "b"; "c" |] ]
    equal "requireStrArray ok" [| "a"; "b"; "c" |] (requireStrArray obj "items")

let wrappersRequireStrArrayNotArray () =
    let obj = createObj [ "items", box "not-an-array" ]
    equal "requireStrArray not-array" [||] (requireStrArray obj "items")

let wrappersRequireStrArrayNull () =
    let obj = createObj [ "items", box null ]
    equal "requireStrArray null" [||] (requireStrArray obj "items")

let wrappersRequireStrArrayMissing () =
    let obj = createObj [ "other", box 1 ]
    equal "requireStrArray missing" [||] (requireStrArray obj "items")

let wrappersMkSchema () =
    let props = createObj [ "name", box (createObj [ "type", box "string" ]) ]
    let schema = mkSchema props [| "name" |]
    check "mkSchema type=object" (schema.``type`` = "object")
    check "mkSchema required Some" (schema.required = Some [| "name" |])
    check "mkSchema additionalProperties=false" (schema.additionalProperties = Some false)

let wrappersRequireWorkspaceIdPresent () =
    let config = createObj [ "workspaceId", box "ws-123" ]

    match requireWorkspaceId config "test-tool" with
    | Ok ws -> equal "requireWorkspaceId ok" "ws-123" ws
    | Error e -> check "requireWorkspaceId present ok" false

let wrappersRequireWorkspaceIdMissing () =
    let config = createObj [ "other", box 1 ]

    match requireWorkspaceId config "test-tool" with
    | Ok _ -> check "requireWorkspaceId missing expects Error" false
    | Error _ -> check "requireWorkspaceId missing Error" true

let wrappersRequireWorkspaceIdInvalid () =
    // invalidIntent is a plain object, not a record with tags; decoding fails
    let config = createObj [ "execution", box null ]

    match requireWorkspaceId config "test-tool" with
    | Ok _ -> check "requireWorkspaceId invalid expects Error" false
    | Error _ -> check "requireWorkspaceId invalid Error" true

// ── Mux.WebTools ──────────────────────────────────────────────────────────────



// ── Mux.BuiltinTools ──────────────────────────────────────────────────────────

let builtinToolsAddIfSomeSome () =
    let entries = ResizeArray<string * obj>()
    addIfSome entries "key" (Some(box "value"))
    equal "addIfSome Some count" 1 entries.Count

let builtinToolsAddIfSomeNone () =
    let entries = ResizeArray<string * obj>()
    addIfSome entries "key" None
    equal "addIfSome None count" 0 entries.Count

let builtinToolsExecutorTool () =
    let deps = createObj []
    let toolNames = [| "executor" |]
    let scope = RuntimeScope()
    let defn = executorTool deps toolNames scope
    equal "executorTool name" "executor" defn.name
    check "executorTool description non-empty" (defn.description.Length > 0)

let builtinToolsReadTool () =
    let deps = createObj []
    let capture = HostFunctionCapture()
    let defn = readTool deps capture
    equal "readTool name" "read" defn.name
    check "readTool description non-empty" (defn.description.Length > 0)

let builtinToolsWriteTool () =
    let deps = createObj []
    let defn = writeTool deps
    equal "writeTool name" "write" defn.name
    check "writeTool description non-empty" (defn.description.Length > 0)

// ── Mux.WrappersReview ─────────────────────────────────────────────────────────

let wrappersReviewHostFunctionCapture () =
    let cap = HostFunctionCapture()
    check "HostFunctionCapture TryGet None initially" (cap.TryGet() = None)
    cap.Capture(createObj [ "fn", box (System.Func<obj, obj>(fun _ -> null)) ])
    check "HostFunctionCapture TryGet Some after Capture" (cap.TryGet() <> None)

let wrappersReviewMkFileReadCapture () =
    let cap = HostFunctionCapture()
    let wrapper = mkFileReadCapture cap
    check "mkFileReadCapture returns obj" (not (isNullish wrapper))
    let targetTool = str wrapper "targetTool"
    equal "mkFileReadCapture targetTool" "file_read" targetTool

// ── run ──────────────────────────────────────────────────────────────────────

let run () : unit =
    wrappersOptBoolTruthy ()
    wrappersOptBoolFalsy ()
    wrappersOptBoolMissing ()
    wrappersOptFieldPresent ()
    wrappersOptFieldNull ()
    wrappersOptFieldMissing ()
    wrappersRequireStrArrayValid ()
    wrappersRequireStrArrayNotArray ()
    wrappersRequireStrArrayNull ()
    wrappersRequireStrArrayMissing ()
    wrappersMkSchema ()
    wrappersRequireWorkspaceIdPresent ()
    wrappersRequireWorkspaceIdMissing ()
    wrappersRequireWorkspaceIdInvalid ()
    builtinToolsAddIfSomeSome ()
    builtinToolsAddIfSomeNone ()
    builtinToolsExecutorTool ()
    builtinToolsReadTool ()
    builtinToolsWriteTool ()
    wrappersReviewHostFunctionCapture ()
    wrappersReviewMkFileReadCapture ()
