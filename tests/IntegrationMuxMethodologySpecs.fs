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
    let todoResult =
        createObj [
            "id", box "msg-todo"
            "role", box "assistant"
            "parts", box [| createObj [
                "type", box "dynamic-tool"
                "toolName", box "todo_write"
                "toolCallId", box "call-1"
                "state", box "output-available"
                "input", box (createObj [])
                "output", box "Todos updated"
            ] |]
        ]
    let out = createObj [ "messages", box [| userMsg; todoResult |] ]
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

let muxTodoWriteMethodologySchemaSpec () = promise {
    let reg = createRegistration (minimalMuxDeps ())
    let wrappers = unbox<obj[]> (get reg "wrappers")
    let todoWrapper = wrappers |> Array.tryFind (fun w -> str w "targetTool" = "todo_write") |> Option.defaultValue null
    if isNullish todoWrapper then
        check "mux registration exposes todo_write wrapper for methodology" false
    else
        let fakeHostTodo =
            box {| execute =
                    System.Func<obj, obj, JS.Promise<obj>>(fun args _ ->
                        promise { return box {| success = true; count = (unbox<obj[]> (get args "todos")).Length |} }) |}
        let wrapped = (get todoWrapper "wrapper") $ (fakeHostTodo, createObj [])
        let schema = get wrapped "parameters"
        let props = get schema "properties"
        let methodologySchema = get props "select_methodology"
        check "todo_write select_methodology is array type" (str methodologySchema "type" = "array")
        let itemsSchema = get methodologySchema "items"
        check "todo_write select_methodology items is string type" (str itemsSchema "type" = "string")
        let enumArr = unbox<obj[]> (get itemsSchema "enum")
        check "todo_write select_methodology enum has all values" (enumArr.Length = (List.toArray methodologyEnumValues).Length)
        check "todo_write select_methodology minItems is 1" (unbox<int> (get methodologySchema "minItems") = 1)
}

let muxMethodologyProbeStrippedOnReprojectionSpec () = promise {
    let! workspaceDir = mkdtempAsync "mux-methodology-strip-"
    let reg = createRegistration (minimalMuxDeps ())
    let tf = muxMessageTransform reg
    let userMsg = muxTextMessage "msg-real" "user" "do the task"
    let todoResult =
        createObj [
            "id", box "msg-todo"
            "role", box "assistant"
            "parts", box [| createObj [
                "type", box "dynamic-tool"
                "toolName", box "todo_write"
                "toolCallId", box "call-1"
                "state", box "output-available"
                "input", box (createObj [])
                "output", box "Todos updated"
            ] |]
        ]
    let staleProbe = muxTextMessage "methodology-probe-1" "user" "stale probe text"
    let out = createObj [ "messages", box [| userMsg; todoResult; staleProbe |] ]
    let input = createObj [ "agent", box "manager"; "directory", box workspaceDir; "sessionID", box "mux-methodology-strip-session" ]
    do! (tf $ (input, out)) |> unbox<JS.Promise<unit>>
    let msgs = unbox<obj[]> (get out "messages")
    let hasProbe = msgs |> Array.exists (fun m -> (str m "id").StartsWith "methodology-probe-")
    check "methodology probe stripped on re-projection" (not hasProbe)
    do! rmAsync workspaceDir
}
