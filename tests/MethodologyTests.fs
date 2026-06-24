module VibeFs.Tests.MethodologyTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Messaging
open VibeFs.Kernel.Methodology

let private mkInfo (id: string) (role: Role) : MessageInfo<obj> =
    { id = id; sessionID = "test"; role = role; agent = ""; isError = false
      toolName = ""; details = null; time = null }

let private mkToolState (status: string) : ToolState<obj> =
    { status = status; output = "ok"; error = ""; input = null; operationAction = "" }

let private userMsg (id: string) (text: string) : Message<obj> =
    { info = mkInfo id User; parts = [ TextPart text ]; source = Native; raw = null }

let private assistantToolMsg (id: string) (toolName: string) (status: string) : Message<obj> =
    { info = mkInfo id Assistant
      parts = [ ToolPart(toolName, "call-1", Some (mkToolState status), null) ]
      source = Native; raw = null }

let private assistantTextMsg (id: string) (text: string) : Message<obj> =
    { info = mkInfo id Assistant; parts = [ TextPart text ]; source = Native; raw = null }

let probeTextContent () =
    check "probe text: exact fixed text" (
        methodologyProbeText = "<important>Before the task, please decide which methodologies are useful for this turn. Now you SHOULD Call the todowrite tool to update todos unless no progress, and select_methodology with one or more relevant methodologies. </important>")

let toolResultTextExact () =
    check "tool result: single methodology" (
        methodologyToolResultText [ "first_principles" ] = "Great! How to proceed with [first_principles] as your methodology?")
    check "tool result: multiple methodologies" (
        methodologyToolResultText [ "first_principles"; "deduction" ] = "Great! How to proceed with [first_principles, deduction] as your methodology?")

let todoResultTextExact () =
    check "todo result: empty" (todoResultText [] = "Todos updated.")
    check "todo result: single methodology" (
        todoResultText [ "first_principles" ] = "Great! How to proceed with [first_principles] as your methodology?")
    check "todo result: multiple methodologies" (
        todoResultText [ "first_principles"; "deduction" ] = "Great! How to proceed with [first_principles, deduction] as your methodology?")

let enumCount () =
    check "enum: 54 values" (methodologyEnumValues.Length = 54)

let enumAllInCatalog () =
    methodologyEnumValues
    |> List.iter (fun v -> check ("catalog contains " + v) (methodologyCatalog.Contains(v)))

let catalogContainsKeyphrase () =
    check "catalog: contains keyphrase" (methodologyCatalog.Contains("Methodology catalog"))

let shouldAppendEmptySession () =
    check "empty session: append probe" (shouldAppendMethodologyProbe [ userMsg "u1" "do task" ] = true)

let shouldNotAppendAfterCompletedTodo () =
    check "completed todowrite: do not append" (
        shouldAppendMethodologyProbe [ userMsg "u1" "task"; assistantToolMsg "a1" "todowrite" "completed" ] = false)
    check "completed task: do not append" (
        shouldAppendMethodologyProbe [ userMsg "u1" "task"; assistantToolMsg "a1" "task" "completed" ] = false)

let shouldAppendAfterOtherTool () =
    check "other tool: still append" (
        shouldAppendMethodologyProbe [ userMsg "u1" "task"; assistantToolMsg "a1" "fuzzy_find" "completed" ] = true)
    check "old select_methodology tool: still append" (
        shouldAppendMethodologyProbe [ userMsg "u1" "task"; assistantToolMsg "a1" "select_methodology" "completed" ] = true)

let shouldAppendAfterEarlierTodoThenOtherTool () =
    check "earlier todo then other tool in new wave: append" (
        shouldAppendMethodologyProbe
            [ userMsg "u1" "task"
              assistantToolMsg "a1" "todowrite" "completed"
              assistantToolMsg "a2" "fuzzy_find" "completed" ] = true)

let shouldAppendAfterErroredTodo () =
    check "errored todowrite: still append" (
        shouldAppendMethodologyProbe [ userMsg "u1" "task"; assistantToolMsg "a1" "todowrite" "error" ] = true)

let shouldAppendNewTurnAfterOldTodo () =
    check "new turn after old todo: append" (
        shouldAppendMethodologyProbe
            [ userMsg "u1" "task1"
              assistantToolMsg "a1" "todowrite" "completed"
              userMsg "u2" "task2" ] = true)

let shouldNotAppendNoUserMessage () =
    check "no user message: do not append" (
        shouldAppendMethodologyProbe [ assistantToolMsg "a1" "todowrite" "completed" ] = false)

let shouldAppendPendingTodoDoesNotCount () =
    check "pending todowrite: still append" (
        shouldAppendMethodologyProbe [ userMsg "u1" "task"; assistantToolMsg "a1" "todowrite" "pending" ] = true)

let probeMessageShape () =
    let msg = buildProbeMessage "coder" "sess1"
    check "probe msg: id prefix" (msg.info.id.StartsWith(methodologyProbeIdPrefix))
    check "probe msg: user role" (msg.info.role = User)
    check "probe msg: synthetic source" (msg.source = Synthetic "methodology-probe")
    check "probe msg: session id" (msg.info.sessionID = "sess1")
    check "probe msg: reflects agent" (msg.info.agent = "coder")
    check "probe msg: single text part" (
        match msg.parts with [ TextPart _ ] -> true | _ -> false)

let run () =
    probeTextContent ()
    toolResultTextExact ()
    todoResultTextExact ()
    enumCount ()
    enumAllInCatalog ()
    catalogContainsKeyphrase ()
    shouldAppendEmptySession ()
    shouldNotAppendAfterCompletedTodo ()
    shouldAppendAfterOtherTool ()
    shouldAppendAfterEarlierTodoThenOtherTool ()
    shouldAppendAfterErroredTodo ()
    shouldAppendNewTurnAfterOldTodo ()
    shouldNotAppendNoUserMessage ()
    shouldAppendPendingTodoDoesNotCount ()
    probeMessageShape ()
