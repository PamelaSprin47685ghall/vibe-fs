module Wanxiangshu.Tests.SembleInjectionTests

open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Kernel.Messaging
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Shell.SembleSearch

let private sampleResult : SembleResult =
    { filePath = "src/auth.py"
      startLine = 127
      endLine = 129
      content = "def save_pretrained(self, path: PathLike):\n    if not os.path.exists(path):\n        os.makedirs(path)"
      score = 0.95 }

let private sampleResult2 : SembleResult =
    { filePath = "src/util.py"
      startLine = 10
      endLine = 10
      content = "let helper = () => ()"
      score = 0.88 }

let private emptyInfo id role =
    { id = id
      sessionID = "session-1"
      role = role
      agent = "investigator"
      isError = false
      toolName = ""
      details = null
      time = null }

let formatReadOutputPrefixesLines () =
    let out = formatReadOutput sampleResult.content sampleResult.startLine
    let lines = out.Split('\n')
    equal "line count" 3 lines.Length
    check "line 1 prefix" (lines.[0].StartsWith "   127|")
    check "line 2 prefix" (lines.[1].StartsWith "   128|")
    check "line 3 prefix" (lines.[2].StartsWith "   129|")

let buildReadPairProducesTwoMessages () =
    let pair = buildReadPair "session-1" "investigator" sampleResult
    equal "pair length" 2 pair.Length
    equal "first role" Assistant pair.[0].info.role
    equal "second role" ToolResult pair.[1].info.role
    equal "ids synthetic" true (pair.[0].source <> Native)
    equal "ids synthetic 2" true (pair.[1].source <> Native)

let buildReadPairStateInputIsCorrect () =
    let pair = buildReadPair "session-1" "investigator" sampleResult
    match pair.[1].parts with
    | [ ToolPart(_, _, Some state, _) ] ->
        equal "state status" "completed" state.status
        let input = state.input
        equal "path" "src/auth.py" (str input "path")
        equal "offset" 127 (unbox<int> (get input "offset"))
        equal "limit" 3 (unbox<int> (get input "limit"))
        check "output starts with line prefix" (state.output.Contains "   127|")
    | _ -> failwith "expected single tool result part"

let buildReadPairCallIDsMatch () =
    let pair = buildReadPair "session-1" "investigator" sampleResult
    match pair.[0].parts, pair.[1].parts with
    | [ ToolPart(_, id1, None, _) ], [ ToolPart(_, id2, Some _, _) ] -> equal "callID" id1 id2
    | _ -> failwith "expected matching callIDs"

let stripSyntheticBySourceRemovesSembleSynth () =
    let pair = buildReadPair "session-1" "investigator" sampleResult
    let stripped = stripSyntheticBySource pair
    equal "stripped empty" 0 stripped.Length

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

let extractContextCollectsTextAndOutput () =
    let assistantText =
        { info = emptyInfo "msg-1" Assistant
          parts = [ TextPart "Investigate authentication" ]
          source = Native
          raw = null }
    let toolResult =
        { info = emptyInfo "msg-2" ToolResult
          parts = [ ToolPart("read", "call-1", Some { status = "completed"; output = "file content"; error = ""; input = null; operationAction = "" }, null) ]
          source = Native
          raw = null }
    let ctx = extractContextFromMessages [ assistantText; toolResult ]
    check "context contains assistant text" (ctx.Contains "Investigate authentication")
    check "context contains tool output" (ctx.Contains "file content")

let extractContextStopsAtToolCallBoundary () =
    let assistantCall =
        { info = emptyInfo "msg-1" Assistant
          parts = [ ToolPart("read", "call-1", None, null) ]
          source = Native
          raw = null }
    let toolResult =
        { info = emptyInfo "msg-2" ToolResult
          parts = [ ToolPart("read", "call-1", Some { status = "completed"; output = "result A"; error = ""; input = null; operationAction = "" }, null) ]
          source = Native
          raw = null }
    let ctx = extractContextFromMessages [ assistantCall; toolResult ]
    check "context only result A" (ctx = "result A")

let run () =
    formatReadOutputPrefixesLines ()
    buildReadPairProducesTwoMessages ()
    buildReadPairStateInputIsCorrect ()
    buildReadPairCallIDsMatch ()
    stripSyntheticBySourceRemovesSembleSynth ()
    isBreakpointDetectsToolResultFinal ()
    isBreakpointFalseForAssistantFinal ()
    isBreakpointFalseForEmpty ()
    extractContextCollectsTextAndOutput ()
    extractContextStopsAtToolCallBoundary ()
