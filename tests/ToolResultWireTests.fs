module Wanxiangshu.Tests.ToolResultWireTests

open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Domain
open Wanxiangshu.Kernel.ToolResult

let wireEncodeResultOk () =
    check "wireEncodeResult Ok" (wireEncodeResult (Ok "done") = "done")

let wireEncodeResultError () =
    let err = InvalidIntent("coder", "intents", "required")
    let text = wireEncodeResult (Error err)
    check "wireEncodeResult Error contains failed" (text.Contains "failed")
    check "wireEncodeResult Error contains formatDomainError" (text.Contains "invalid intents")

let wireEncodeToolErrorFormat () =
    let err = ParseError("ctx", "detail")
    let text = wireEncodeToolError "Subagent" err
    check "wireEncodeToolError context" (text.StartsWith "Subagent failed:")
    check "wireEncodeToolError detail" (text.Contains "parse error in ctx")

let run () =
    wireEncodeResultOk ()
    wireEncodeResultError ()
    wireEncodeToolErrorFormat ()
