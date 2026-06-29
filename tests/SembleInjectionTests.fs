module Wanxiangshu.Tests.SembleInjectionTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.CapsFormat
open Wanxiangshu.Shell.SembleMcp
open Wanxiangshu.Shell.SembleSearch

[<Global("process")>]
let private procEnv : obj = jsNative

let private sampleResult : SembleResult =
    { filePath = "src/auth.py"; startLine = 127; endLine = 129
      content = "def save_pretrained(self, path: PathLike):\n    if not os.path.exists(path):\n        os.makedirs(path)"
      score = 0.95 }

let private emptyInfo id role =
    { id = id; sessionID = "session-1"; role = role; agent = "investigator"
      isError = false; toolName = ""; details = null; time = null }

let formatReadOutputPrefixesLines () =
    let out = formatReadOutput sampleResult.filePath (sliceFromContent sampleResult.content)
    check "has path tag" (out.Contains "<path>src/auth.py</path>")
    check "has type tag" (out.Contains "<type>file</type>")
    check "has content tag" (out.Contains "<content>")
    check "has 1 line" (out.Contains "1: def save_pretrained")
    check "has EOF footer" (out.Contains "(End of file - total 3 lines)")
    check "has closing content tag" (out.Contains "</content>")

let isBreakpointDetectsToolResultFinal () =
    let toolResultMsg =
        box (createObj [
            "info", box (createObj [ "role", box "toolResult"; "id", box "msg-1" ])
            "parts", box [||]
        ])
    check "breakpoint true" (isBreakpoint [| toolResultMsg |])

let isBreakpointFalseForAssistantFinal () =
    let assistantMsg =
        box (createObj [
            "info", box (createObj [ "role", box "assistant"; "id", box "msg-1" ])
            "parts", box [||]
        ])
    check "breakpoint false" (not (isBreakpoint [| assistantMsg |]))

let isBreakpointFalseForEmpty () =
    check "breakpoint empty false" (not (isBreakpoint [||]))

let extractContextCollectsUserAndAssistantText () =
    let userMsg =
        { info = emptyInfo "msg-0" User; parts = [ TextPart "Find the auth function" ]; source = Native; raw = null }
    let assistantText =
        { info = emptyInfo "msg-1" Assistant; parts = [ TextPart "Investigating authentication" ]; source = Native; raw = null }
    let toolResult =
        { info = emptyInfo "msg-2" ToolResult
          parts = [ ToolPart("read", "call-1", Some { status = "completed"; output = "file content"; error = ""; input = null; operationAction = "" }, null) ]
          source = Native; raw = null }
    let ctx = extractContextFromMessages 0 [ userMsg; assistantText; toolResult ]
    check "context contains user text" (ctx.Contains "Find the auth function")
    check "context contains assistant text" (ctx.Contains "Investigating authentication")
    check "context excludes tool output" (not (ctx.Contains "file content"))

let extractContextRespectsStartIndex () =
    let m0 = { info = emptyInfo "m0" Assistant; parts = [ TextPart "first turn already injected" ]; source = Native; raw = null }
    let m1 = { info = emptyInfo "m1" User; parts = [ TextPart "new request since breakpoint" ]; source = Native; raw = null }
    let ctx = extractContextFromMessages 1 [ m0; m1 ]
    check "context excludes pre-start" (not (ctx.Contains "first turn"))
    check "context includes post-start" (ctx.Contains "new request")

let extractContextExcludesAssistantToolParts () =
    let assistantWithTool =
        { info = emptyInfo "m0" Assistant; parts = [ TextPart "thinking"; ToolPart("read", "c1", None, null) ]; source = Native; raw = null }
    let ctx = extractContextFromMessages 0 [ assistantWithTool ]
    check "context keeps assistant text" (ctx.Contains "thinking")

let extractContextCollectsReasoningParts () =
    let reasoningPart = RawPart (box (createObj [
        "type", box "reasoning"; "text", box "I should check the auth module first"; "id", box "prt-1"
    ]))
    let assistantWithReasoning =
        { info = emptyInfo "m0" Assistant; parts = [ reasoningPart; ToolPart("read", "c1", None, null) ]; source = Native; raw = null }
    let ctx = extractContextFromMessages 0 [ assistantWithReasoning ]
    check "context includes reasoning text" (ctx.Contains "I should check the auth module")

let debugDisabledByDefault () =
    check "debug disabled by default" (not (debugEnabled ()))

let debugEnabledViaEnv () =
    let prev = if isNull (procEnv?env?SEMBLE_INJECT_DEBUG) then "" else string procEnv?env?SEMBLE_INJECT_DEBUG
    try
        procEnv?env?SEMBLE_INJECT_DEBUG <- "1"
        check "debug enabled via env" (debugEnabled ())
        procEnv?env?SEMBLE_INJECT_DEBUG <- "0"
        check "debug disabled when env=0" (not (debugEnabled ()))
    finally
        procEnv?env?SEMBLE_INJECT_DEBUG <- prev

let dumpInjectionNoThrowWhenDisabled () =
    dumpInjection "s" "investigator" "ctx" [ sampleResult ] 2
    check "dumpInjection disabled no throw" true

let run () =
    formatReadOutputPrefixesLines ()
    isBreakpointDetectsToolResultFinal ()
    isBreakpointFalseForAssistantFinal ()
    isBreakpointFalseForEmpty ()
    extractContextCollectsUserAndAssistantText ()
    extractContextRespectsStartIndex ()
    extractContextExcludesAssistantToolParts ()
    extractContextCollectsReasoningParts ()
    debugDisabledByDefault ()
    debugEnabledViaEnv ()
    dumpInjectionNoThrowWhenDisabled ()
