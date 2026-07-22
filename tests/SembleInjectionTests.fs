module Wanxiangshu.Tests.SembleInjectionTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Kernel.ToolExecutionStatusModule
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.CapsFormat
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.SembleSearch

[<Global("process")>]
let private procEnv: obj = jsNative

let internal sampleResult: SembleResult =
    { filePath = "src/auth.py"
      startLine = 127
      endLine = 129
      content =
        "def save_pretrained(self, path: PathLike):\n    if not os.path.exists(path):\n        os.makedirs(path)"
      score = 0.95
      totalLines = 300 }

let private sampleResult2: SembleResult =
    { filePath = "src/util.py"
      startLine = 10
      endLine = 10
      content = "let helper = () => ()"
      score = 0.88
      totalLines = 100 }

let private emptyInfo id role =
    { id = id
      sessionID = "session-1"
      role = role
      agent = "inspector"
      isError = false
      toolName = ""
      details = null
      time = null }

let formatReadOutputMatchesOpenCodeNative () =
    let out = formatReadOutput sampleResult.filePath sampleResult.content sampleResult.startLine (Some sampleResult.totalLines)

    check "starts with path tag" (out.StartsWith "<path>src/auth.py</path>")
    check "has type file tag" (out.Contains "<type>file</type>")
    check "has content open tag" (out.Contains "<content>")
    check "has native colon line" (out.Contains "127: def save_pretrained")
    check "no pipe line prefix" (not (out.Contains "127|"))
    check "has EOF footer" (out.Contains "(End of file - total 300 lines)")
    check "closes content tag" (out.Contains "</content>")

let buildReadToolPartsProducesOnePartPerResult () =
    let parts = buildReadToolParts "msg-1" "session-1" [ sampleResult; sampleResult2 ]
    equal "part count" 2 parts.Length

let buildReadToolPartsStructureMatchesCaps () =
    let parts = buildReadToolParts "msg-1" "session-1" [ sampleResult ]
    let p = parts.[0]
    equal "type" "tool" (str p "type")
    equal "tool" "read" (str p "tool")
    check "callID semble prefix" ((str p "callID").StartsWith "semble-call-")
    equal "messageID" "msg-1" (str p "messageID")
    check "id starts with prt_" ((str p "id").StartsWith "prt_")
    let state = get p "state"
    equal "state status" "completed" (str state "status")
    let input = get state "input"
    equal "input filePath" "src/auth.py" (str input "filePath")
    equal "input offset" 127 (getValue<int> input "offset")
    equal "input limit" 3 (getValue<int> input "limit")
    let output = str state "output"
    check "output has path tag" (output.Contains "<path>src/auth.py</path>")
    check "output has 127: line" (output.Contains "127: def save_pretrained")
    check "output has type tag" (output.Contains "<type>file</type>")
    let metadata = get state "metadata"
    check "metadata preview is string" ((str metadata "preview").Contains "def save_pretrained")
    equal "metadata truncated bool" true (getValue<bool> metadata "truncated")
    check "metadata loaded is array" (isArray (get metadata "loaded"))
    let display = get metadata "display"
    equal "display type file" "file" (str display "type")
    equal "display path" "src/auth.py" (str display "path")
    equal "display lineStart" 127 (getValue<int> display "lineStart")
    equal "display lineEnd" 129 (getValue<int> display "lineEnd")
    equal "display totalLines" 300 (getValue<int> display "totalLines")
    equal "display truncated" true (getValue<bool> display "truncated")
    let time = get state "time"
    check "time start > 0" ((getValue<int> time "start") > 0)
    check "time end >= start" ((getValue<int> time "end") >= (getValue<int> time "start"))
    equal "title is path" "src/auth.py" (str state "title")

let buildReadToolPartsCallIDsUnique () =
    let parts = buildReadToolParts "msg-1" "session-1" [ sampleResult; sampleResult2 ]
    let ids = parts |> Array.map (fun p -> str p "callID") |> Set.ofArray
    equal "unique callIDs" 2 ids.Count

let isBreakpointDetectsToolResultFinal () =
    let toolResultMsg =
        box (
            createObj
                [ "info", box (createObj [ "role", box "toolResult"; "id", box "msg-1" ])
                  "parts", box [||] ]
        )

    check "breakpoint true" (isBreakpoint [| toolResultMsg |])

let isBreakpointFalseForAssistantFinal () =
    let assistantMsg =
        box (
            createObj
                [ "info", box (createObj [ "role", box "assistant"; "id", box "msg-1" ])
                  "parts", box [||] ]
        )

    check "breakpoint false" (not (isBreakpoint [| assistantMsg |]))

let isBreakpointFalseForEmpty () =
    check "breakpoint empty false" (not (isBreakpoint [||]))

let extractContextCollectsUserAndAssistantText () =
    let userMsg =
        { info = emptyInfo "msg-0" User
          parts = [ TextPart "Find the auth function" ]
          source = Native
          raw = null }

    let assistantText =
        { info = emptyInfo "msg-1" Assistant
          parts = [ TextPart "Investigating authentication" ]
          source = Native
          raw = null }

    let toolResult =
        { info = emptyInfo "msg-2" ToolResult
          parts =
            [ ToolPart(
                  "read",
                  "call-1",
                  Some
                      { status = fromString "completed"
                        output = "file content"
                        error = ""
                        input = null
                        operationAction = "" },
                  null
              ) ]
          source = Native
          raw = null }

    let ctx = extractContextFromMessages 0 [ userMsg; assistantText; toolResult ]
    check "context contains user text" (ctx.Contains "Find the auth function")
    check "context contains assistant text" (ctx.Contains "Investigating authentication")
    check "context excludes tool output" (not (ctx.Contains "file content"))

let extractContextRespectsStartIndex () =
    let m0 =
        { info = emptyInfo "m0" Assistant
          parts = [ TextPart "first turn already injected" ]
          source = Native
          raw = null }

    let m1 =
        { info = emptyInfo "m1" User
          parts = [ TextPart "new request since breakpoint" ]
          source = Native
          raw = null }

    let ctx = extractContextFromMessages 1 [ m0; m1 ]
    check "context excludes pre-start" (not (ctx.Contains "first turn"))
    check "context includes post-start" (ctx.Contains "new request")

let extractContextExcludesAssistantToolParts () =
    let assistantWithTool =
        { info = emptyInfo "m0" Assistant
          parts = [ TextPart "thinking"; ToolPart("read", "c1", None, null) ]
          source = Native
          raw = null }

    let ctx = extractContextFromMessages 0 [ assistantWithTool ]
    check "context keeps assistant text" (ctx.Contains "thinking")

let extractContextCollectsReasoningParts () =
    let reasoningPart =
        RawPart(
            box (
                createObj
                    [ "type", box "reasoning"
                      "text", box "I should check the auth module first"
                      "id", box "prt-1" ]
            )
        )

    let assistantWithReasoning =
        { info = emptyInfo "m0" Assistant
          parts = [ reasoningPart; ToolPart("read", "c1", None, null) ]
          source = Native
          raw = null }

    let ctx = extractContextFromMessages 0 [ assistantWithReasoning ]
    check "context includes reasoning text" (ctx.Contains "I should check the auth module")

let debugDisabledByDefault () =
    check "debug disabled by default" (not (debugEnabled ()))

let debugEnabledViaEnv () =
    let prev =
        if isNull (procEnv?env?SEMBLE_INJECT_DEBUG) then
            ""
        else
            string procEnv?env?SEMBLE_INJECT_DEBUG

    try
        procEnv?env?SEMBLE_INJECT_DEBUG <- "1"
        check "debug enabled via env" (debugEnabled ())
        procEnv?env?SEMBLE_INJECT_DEBUG <- "0"
        check "debug disabled when env=0" (not (debugEnabled ()))
    finally
        procEnv?env?SEMBLE_INJECT_DEBUG <- prev

let dumpInjectionNoThrowWhenDisabled () =
    dumpInjection "s" "inspector" "ctx" [ sampleResult ] 2
    check "dumpInjection disabled no throw" true

let breakpointsAreScopedByRuntimeScope () =
    let scopeA = create ()
    let scopeB = create ()
    let sessionID = "semble-scope-test"

    markBreakpoint scopeA sessionID 7
    equal "scope A breakpoint" (Some 7) (breakpointStart scopeA sessionID)
    equal "scope B no breakpoint" None (breakpointStart scopeB sessionID)

    clearBreakpoint scopeA sessionID
    equal "scope A cleared" None (breakpointStart scopeA sessionID)

type SembleBreakpointScopeTests =
    static member run() = breakpointsAreScopedByRuntimeScope ()

let run () =
    formatReadOutputMatchesOpenCodeNative ()
    buildReadToolPartsProducesOnePartPerResult ()
    buildReadToolPartsStructureMatchesCaps ()
    buildReadToolPartsCallIDsUnique ()
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
    SembleBreakpointScopeTests.run ()
