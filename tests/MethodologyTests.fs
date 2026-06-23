module VibeFs.Tests.MethodologyTests

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Kernel.Messaging
open VibeFs.Kernel.Methodology

let private mkInfo (id: string) (role: Role) : MessageInfo =
    { id = id; sessionID = "test"; role = role; agent = ""; isError = false
      toolName = ""; details = null; time = null }

let private mkToolState (status: string) : ToolState =
    { status = status; output = "ok"; error = ""; input = null; operationAction = "" }

let private userMsg (id: string) (text: string) : Message =
    { info = mkInfo id User; parts = [ TextPart text ]; source = Native; raw = null }

let private assistantToolMsg (id: string) (toolName: string) (status: string) : Message =
    { info = mkInfo id Assistant
      parts = [ ToolPart(toolName, "call-1", Some (mkToolState status), null) ]
      source = Native; raw = null }

let private assistantTextMsg (id: string) (text: string) : Message =
    { info = mkInfo id Assistant; parts = [ TextPart text ]; source = Native; raw = null }

let probeTextContent () =
    check "probe text: mentions tool name" (methodologyProbeText.Contains("select_methodology"))
    check "probe text: mentions reasoning methodologies" (methodologyProbeText.Contains("reasoning methodologies"))

let toolResultTextExact () =
    check "tool result: exact fixed text" (methodologyToolResultText = "Continue using the selected methodologies.")

let enumCount () =
    check "enum: 54 values" (methodologyEnumValues.Length = 54)

let enumAllInCatalog () =
    methodologyEnumValues
    |> List.iter (fun v -> check ("catalog contains " + v) (methodologyCatalog.Contains(v)))

let catalogContainsKeyphrase () =
    check "catalog: contains keyphrase" (methodologyCatalog.Contains("Methodology catalog"))

let shouldAppendEmptySession () =
    check "empty session: append probe" (shouldAppendMethodologyProbe [ userMsg "u1" "do task" ] = true)

let shouldNotAppendAfterCompletedMethodology () =
    check "completed methodology: do not append" (
        shouldAppendMethodologyProbe [ userMsg "u1" "task"; assistantToolMsg "a1" selectMethodologyToolName "completed" ] = false)

let shouldAppendAfterOtherTool () =
    check "other tool: still append" (
        shouldAppendMethodologyProbe [ userMsg "u1" "task"; assistantToolMsg "a1" "fuzzy_find" "completed" ] = true)

let shouldAppendAfterErroredMethodology () =
    check "errored methodology: still append" (
        shouldAppendMethodologyProbe [ userMsg "u1" "task"; assistantToolMsg "a1" selectMethodologyToolName "error" ] = true)

let shouldAppendNewTurnAfterOldMethodology () =
    check "new turn after old methodology: append" (
        shouldAppendMethodologyProbe
            [ userMsg "u1" "task1"
              assistantToolMsg "a1" selectMethodologyToolName "completed"
              userMsg "u2" "task2" ] = true)

let shouldNotAppendNoUserMessage () =
    check "no user message: do not append" (
        shouldAppendMethodologyProbe [ assistantToolMsg "a1" selectMethodologyToolName "completed" ] = false)

let shouldAppendPendingMethodologyDoesNotCount () =
    check "pending methodology: still append" (
        shouldAppendMethodologyProbe [ userMsg "u1" "task"; assistantToolMsg "a1" selectMethodologyToolName "pending" ] = true)

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
    enumCount ()
    enumAllInCatalog ()
    catalogContainsKeyphrase ()
    shouldAppendEmptySession ()
    shouldNotAppendAfterCompletedMethodology ()
    shouldAppendAfterOtherTool ()
    shouldAppendAfterErroredMethodology ()
    shouldAppendNewTurnAfterOldMethodology ()
    shouldNotAppendNoUserMessage ()
    shouldAppendPendingMethodologyDoesNotCount ()
    probeMessageShape ()
