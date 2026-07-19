module Wanxiangshu.Tests.IntegrationMuxPreludeSpecs

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TestWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Tests.IntegrationMuxSetup

open Wanxiangshu.Hosts.Mux.Plugin
open Wanxiangshu.Runtime.Dyn


let wrapperSpec (reg: obj) =
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let targets = wrappers |> Array.map (fun w -> str w "targetTool") |> Array.sort

    let expected =
        [| "agent_report"
           "file_edit_insert"
           "file_edit_replace_string"
           "file_read"
           "todo_write" |]
        |> Array.sort

    check "wrapper targets correct" (targets = expected)
    let ar = wrappers |> Array.find (fun w -> str w "targetTool" = "agent_report")
    check "agent_report wrapper exists" (not (isNullish ar))

let computeCountSpec (reg: obj) =
    let tools = unbox<obj[]> (get reg "tools")
    let names = tools |> Array.map (fun t -> str t "name")
    check "has coder tool" (names |> Array.contains "coder")
    check "has webfetch tool" (names |> Array.contains "webfetch")
    check "has write tool" (names |> Array.contains "write")
    check "has read tool" (names |> Array.contains "read")
    check "has submit_review tool" (names |> Array.contains "submit_review")
    check "mux does not expose return_reviewer tool" (not (names |> Array.contains "return_reviewer"))

let muxMessageTransformRegisteredSpec () =
    promise {
        let reg = sharedMuxRegistration ()
        let tf = muxMessageTransform reg
        check "mux registration exposes messagesTransform" (not (isNullish tf))
        check "mux messagesTransform is callable" (typeIs tf "function")
    }
