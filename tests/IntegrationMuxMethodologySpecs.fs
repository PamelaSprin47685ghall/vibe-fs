module VibeFs.Tests.IntegrationMuxMethodologySpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationToolSetup
open VibeFs.Tests.IntegrationMuxSetup

open VibeFs.Kernel.Methodology
open VibeFs.Mux.Plugin
open VibeFs.Shell.Dyn


let muxMethodologyProbeAppendedSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-methodology-probe-"
    let reg = createRegistration (minimalMuxDeps ())
    let tf = muxMessageTransform reg
    for agent in [| "manager"; "coder"; "reviewer"; "meditator" |] do
        let originalMsg = muxTextMessage ("msg-probe-" + agent) "user" "do the task"
        let out = createObj [ "messages", box [| originalMsg |] ]
        let input = createObj [ "agent", box agent; "directory", box workspaceDir; "sessionID", box ("mux-methodology-probe-" + agent) ]
        do! (tf $ (input, out)) |> unbox<JS.Promise<unit>>
        let msgs = unbox<obj[]> (get out "messages")
        let lastMsg = msgs.[msgs.Length - 1]
        let lastId = str lastMsg "id"
        check (agent + " receives methodology probe") (lastId.StartsWith "methodology-probe-")
        let lastText = firstTextPartText lastMsg
        check (agent + " probe mentions select_methodology") (lastText.Contains "select_methodology")
    do! rmAsync workspaceDir
}

let muxMethodologyProbeSuppressedAfterCallSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-methodology-suppressed-"
    let reg = createRegistration (minimalMuxDeps ())
    let tf = muxMessageTransform reg
    let userMsg = muxTextMessage "msg-user" "user" "do the task"
    let methodologyResult =
        createObj [
            "id", box "msg-method"
            "role", box "assistant"
            "parts", box [| createObj [
                "type", box "dynamic-tool"
                "toolName", box "select_methodology"
                "toolCallId", box "call-1"
                "state", box "output-available"
                "input", box (createObj [])
                "output", box "Continue using the selected methodologies."
            ] |]
        ]
    let out = createObj [ "messages", box [| userMsg; methodologyResult |] ]
    let input = createObj [ "agent", box "manager"; "directory", box workspaceDir; "sessionID", box "mux-methodology-suppressed-session" ]
    do! (tf $ (input, out)) |> unbox<JS.Promise<unit>>
    let msgs = unbox<obj[]> (get out "messages")
    let lastId = str msgs.[msgs.Length - 1] "id"
    check "methodology probe suppressed after completed call" (not (lastId.StartsWith "methodology-probe-"))
    do! rmAsync workspaceDir
}

let muxMethodologyProbeExcludedAgentsSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-methodology-excluded-"
    let reg = createRegistration (minimalMuxDeps ())
    let tf = muxMessageTransform reg
    for agent in [| "compaction"; "title"; "browser"; "bookkeeper"; "investigator"; "executor" |] do
        let originalMsg = muxTextMessage ("msg-" + agent) "user" "do the task"
        let out = createObj [ "messages", box [| originalMsg |] ]
        let input = createObj [ "agent", box agent; "directory", box workspaceDir; "sessionID", box ("mux-methodology-excl-" + agent) ]
        do! (tf $ (input, out)) |> unbox<JS.Promise<unit>>
        let msgs = unbox<obj[]> (get out "messages")
        let lastId = str msgs.[msgs.Length - 1] "id"
        check (agent + " does not receive methodology probe") (not (lastId.StartsWith "methodology-probe-"))
    do! rmAsync workspaceDir
}

let muxMethodologyToolExecuteSpec () = promise {
    let reg = createRegistration (minimalMuxDeps ())
    let tool = muxToolByName reg "select_methodology"
    if isNullish tool then
        check "mux registration exposes select_methodology tool" false
    else
        let! result = ((get tool "execute") $ (createObj [], createObj [])) |> unbox<JS.Promise<string>>
        check "select_methodology execute returns fixed text" (result = methodologyToolResultText)
}

let muxMethodologyToolSchemaSpec () = promise {
    let reg = createRegistration (minimalMuxDeps ())
    let tool = muxToolByName reg "select_methodology"
    if isNullish tool then
        check "mux registration exposes select_methodology tool" false
    else
        let schema = muxToolSchema tool
        let props = get schema "properties"
        let methodsSchema = get props "methods"
        let reasonSchema = get props "reason"
        check "methodology methods is array type" (str methodsSchema "type" = "array")
        let itemsSchema = get methodsSchema "items"
        check "methodology methods items is string type" (str itemsSchema "type" = "string")
        let enumArr = unbox<obj[]> (get itemsSchema "enum")
        check "methodology methods enum has all values" (enumArr.Length = (List.toArray methodologyEnumValues).Length)
        check "methodology methods minItems is 1" (unbox<int> (get methodsSchema "minItems") = 1)
        check "methodology reason is string type" (str reasonSchema "type" = "string")
        let required = muxToolSchemaRequired tool
        check "methodology required includes methods" (required |> Array.contains "methods")
        check "methodology required includes reason" (required |> Array.contains "reason")
}

let muxMethodologyProbeStrippedOnReprojectionSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-methodology-strip-"
    let reg = createRegistration (minimalMuxDeps ())
    let tf = muxMessageTransform reg
    let userMsg = muxTextMessage "msg-real" "user" "do the task"
    let methodologyResult =
        createObj [
            "id", box "msg-method"
            "role", box "assistant"
            "parts", box [| createObj [
                "type", box "dynamic-tool"
                "toolName", box "select_methodology"
                "toolCallId", box "call-1"
                "state", box "output-available"
                "input", box (createObj [])
                "output", box "Continue using the selected methodologies."
            ] |]
        ]
    let staleProbe = muxTextMessage "methodology-probe-1" "user" "stale probe text"
    let out = createObj [ "messages", box [| userMsg; methodologyResult; staleProbe |] ]
    let input = createObj [ "agent", box "manager"; "directory", box workspaceDir; "sessionID", box "mux-methodology-strip-session" ]
    do! (tf $ (input, out)) |> unbox<JS.Promise<unit>>
    let msgs = unbox<obj[]> (get out "messages")
    let hasProbe = msgs |> Array.exists (fun m -> (str m "id").StartsWith "methodology-probe-")
    check "methodology probe stripped on re-projection" (not hasProbe)
    do! rmAsync workspaceDir
}
