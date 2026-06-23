module VibeFs.Tests.IntegrationToolDefSpecs

open Fable.Core
open Fable.Core.JsInterop
open System
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationToolSetup

open VibeFs.Mux.Plugin
open VibeFs.Opencode.Plugin
open VibeFs.Mux.AiSettings
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.KnowledgeGraphFiles
open VibeFs.Shell.Dyn


let toolDefinitionSpec () = promise {
    let! workspaceDir = mkdtempAsync "tool-definition-"
    let! p = plugin (box {| directory = workspaceDir |})
    let td = get p "tool.definition"
    let coderDef = createObj [ "jsonSchema", box (createObj [
        "properties", box (createObj [ "intents", box (createObj [ "type", box "array" ]); "_ui", box (createObj [ "type", box "string" ]) ])
        "required", box [| "intents"; "_ui" |]
    ]) ]
    do! td $ (createObj [ "toolID", box "coder" ], coderDef) |> unbox<JS.Promise<unit>>
    let props = get (get coderDef "jsonSchema") "properties"
    check "tool.definition does not strip coder _ui property" (not (isNullish (get props "_ui")))
    check "tool.definition keeps coder intents" (not (isNullish (get props "intents")))

    let todoParams = createObj [ "__effectSchema", box true ]
    let todoDef = createObj [ "description", box "old desc"; "parameters", box todoParams ]
    do! td $ (createObj [ "toolID", box "todowrite" ], todoDef) |> unbox<JS.Promise<unit>>
    check "tool.definition rewrites todo description" (str todoDef "description" |> fun text -> text.Contains "append-only work backlog")
    check "tool.definition leaves todo parameters untouched" (obj.ReferenceEquals(get todoDef "parameters", todoParams))
    let todoSchema = get todoDef "jsonSchema"
    let todoProps = get todoSchema "properties"
    let reportSchema = get todoProps "completedWorkReport"
    let required = unbox<obj[]> (get todoSchema "required") |> Array.map string
    check "tool.definition builds todo report field" (str reportSchema "type" = "string")
    check "tool.definition builds todo report description" (str reportSchema "description" = VibeFs.Kernel.MagicTodo.reportDesc)
    check "tool.definition requires todo report" (required |> Array.contains "completedWorkReport")
    check "tool.definition requires todos" (required |> Array.contains "todos")
    check "tool.definition builds todos description" (str (get todoProps "todos") "description" = VibeFs.Kernel.MagicTodo.todosDesc)
    let todoItemProps = get (get (get todoProps "todos") "items") "properties"
    check "tool.definition builds todo content description" (str (get todoItemProps "content") "description" = VibeFs.Kernel.MagicTodo.todoContentDesc)
    check "tool.definition builds todo status description" (str (get todoItemProps "status") "description" = VibeFs.Kernel.MagicTodo.todoStatusDesc)
    check "tool.definition builds todo priority description" (str (get todoItemProps "priority") "description" = VibeFs.Kernel.MagicTodo.todoPriorityDesc)

    let! mimoP = VibeFs.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |})
    let mimoTd = get mimoP "tool.definition"
    let taskParams =
        createObj [
            "type", box "object"
            "properties", box (createObj [ "todos", box (createObj [ "type", box "array" ]) ])
            "required", box [| box "todos" |]
        ]
    let taskDef = createObj [ "description", box "native"; "parameters", box taskParams ]
    do! mimoTd $ (createObj [ "toolID", box "task" ], taskDef) |> unbox<JS.Promise<unit>>
    check "mimo task.definition keeps parameters not jsonSchema" (isNullish (get taskDef "jsonSchema"))
    check "mimo task.definition fuses report into parameters" (not (isNullish (get (get (get taskDef "parameters") "properties") "completedWorkReport")))
    check "mimo task.definition preserves todos" (not (isNullish (get (get (get taskDef "parameters") "properties") "todos")))

    let taskJsonSchema = createObj [
        "type", box "object"
        "properties", box (createObj [ "todos", box (createObj [ "type", box "array" ]); "task_id", box (createObj [ "type", box "string" ]) ])
        "required", box [| box "todos"; box "task_id" |]
    ]
    let taskJsonDef = createObj [ "description", box "native"; "jsonSchema", box taskJsonSchema ]
    do! mimoTd $ (createObj [ "toolID", box "task" ], taskJsonDef) |> unbox<JS.Promise<unit>>
    check "mimo task.definition rewrites jsonSchema when that is the exposed path" (not (isNullish (get (get (get taskJsonDef "jsonSchema") "properties") "completedWorkReport")))
    check "mimo task.definition strips task_id from jsonSchema" (isNullish (get (get (get taskJsonDef "jsonSchema") "properties") "task_id"))
    let jsonRequired = unbox<obj[]> (get (get taskJsonDef "jsonSchema") "required") |> Array.map string
    check "mimo task.definition keeps todos required in jsonSchema" (jsonRequired |> Array.contains "todos")
    check "mimo task.definition drops task_id required in jsonSchema" (not (jsonRequired |> Array.contains "task_id"))
    do! rmAsync workspaceDir
}

let toolExecuteBeforeSpec () = promise {
    let! workspaceDir = mkdtempAsync "tool-execute-before-"
    let! p = plugin (box {| directory = workspaceDir |})
    let teb = get p "tool.execute.before"
    let intents : obj array = [|
        sampleCoderIntent "fix bug" "a.ts"
        sampleCoderIntent "add feature" "b.ts"
    |]
    let originalArgs = createObj [ "intents", box intents ]
    let execOut = createObj [ "args", box originalArgs ]
    do! teb $ (createObj [ "tool", box "coder"; "sessionID", box "s1"; "callID", box "c1" ], execOut) |> unbox<JS.Promise<unit>>
    check "tool.execute.before populates _ui" (str (get execOut "args") "_ui" = "fix bug; add feature")
    check "tool.execute.before mutates args in place" (obj.ReferenceEquals(get execOut "args", originalArgs))
    check "tool.execute.before writes _ui onto host args reference" (str originalArgs "_ui" = "fix bug; add feature")
    do! rmAsync workspaceDir
}

let mimoApplyPatchExecuteBeforeSpec () = promise {
    let! workspaceDir = mkdtempAsync "mimo-apply-patch-before-"
    let! p = VibeFs.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |})
    let teb = get p "tool.execute.before"

    let stringArgsOut = createObj [ "args", box "*** Begin Patch\n*** End Patch" ]
    do! teb $ (createObj [ "tool", box "apply_patch"; "sessionID", box "s1"; "callID", box "c1" ], stringArgsOut) |> unbox<JS.Promise<unit>>
    check "mimo apply_patch execute.before wraps string args" (str (get stringArgsOut "args") "patchText" = "*** Begin Patch\n*** End Patch")

    let patchArgsOut = createObj [ "args", box (createObj [ "patch", box "*** Begin Patch\n*** End Patch" ]) ]
    do! teb $ (createObj [ "tool", box "apply_patch"; "sessionID", box "s1"; "callID", box "c2" ], patchArgsOut) |> unbox<JS.Promise<unit>>
    check "mimo apply_patch execute.before rewrites patch field" (str (get patchArgsOut "args") "patchText" = "*** Begin Patch\n*** End Patch")

    let correctArgsOut = createObj [ "args", box (createObj [ "patchText", box "already-correct" ]) ]
    do! teb $ (createObj [ "tool", box "apply_patch"; "sessionID", box "s1"; "callID", box "c3" ], correctArgsOut) |> unbox<JS.Promise<unit>>
    check "mimo apply_patch execute.before preserves patchText" (str (get correctArgsOut "args") "patchText" = "already-correct")
    do! rmAsync workspaceDir
}

let mimoTaskExecuteRoundTripSpec () = promise {
    let! workspaceDir = mkdtempAsync "mimo-task-before-after-"
    let! p = VibeFs.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |})
    let taskTool = get (get p "tool") "task"
    let args = createObj [
        "completedWorkReport", box "Detailed backlog report"
        "todos", box [| createObj [ "content", box "Ship parser fix"; "status", box "completed"; "priority", box "high" ] |]
    ]
    let! result = (get taskTool "execute") $ (args, createObj [ "sessionID", box "s1" ]) |> unbox<JS.Promise<string>>
    check "mimo task execute returns todo update text" (result.Contains "Todos updated.")
    do! rmAsync workspaceDir
}

let mimoTaskExecuteNestedReportSpec () = promise {
    let! workspaceDir = mkdtempAsync "mimo-task-nested-report-"
    let! p = VibeFs.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |})
    let taskTool = get (get p "tool") "task"
    let args = createObj [
        "completedWorkReport", box "Nested report is no longer supported"
        "todos", box [| createObj [ "content", box "Build feature"; "status", box "in_progress"; "priority", box "high" ] |]
    ]
    let! result = (get taskTool "execute") $ (args, createObj [ "sessionID", box "s1" ]) |> unbox<JS.Promise<string>>
    check "mimo task execute still succeeds with explicit top-level report" (result.Contains "Todos updated.")
    do! rmAsync workspaceDir
}

let mimoTaskExecuteInPlaceStripSpec () = promise {
    let! workspaceDir = mkdtempAsync "mimo-task-inplace-"
    let! p = VibeFs.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |})
    let teb = get p "tool.execute.before"
    let originalArgs = createObj [ "completedWorkReport", box "top-level report text"; "todos", box [||] ]
    let beforeOut = createObj [ "args", box originalArgs ]
    do! teb $ (createObj [ "tool", box "task"; "sessionID", box "s1"; "callID", box "ci1" ], beforeOut) |> unbox<JS.Promise<unit>>
    check "mimo task execute.before leaves todo args untouched" (obj.ReferenceEquals(get beforeOut "args", originalArgs))
    do! rmAsync workspaceDir
}

let mimoTaskExecuteStripsTaskIdSpec () = promise {
    let! workspaceDir = mkdtempAsync "mimo-task-strip-task-id-"
    let! p = VibeFs.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |})
    let taskTool = get (get p "tool") "task"
    let args = createObj [
        "completedWorkReport", box "noop report"
        "todos", box [| createObj [ "content", box "List current work"; "status", box "pending"; "priority", box "low" ] |]
        "task_id", box "T4"
    ]
    let! result = (get taskTool "execute") $ (args, createObj [ "sessionID", box "s1" ]) |> unbox<JS.Promise<string>>
    check "mimo task execute ignores stray task_id" (result.Contains "Todos updated.")
    do! rmAsync workspaceDir
}

let mimoTaskDefinitionHandlesZodLikeParametersSpec () = promise {
    let! workspaceDir = mkdtempAsync "mimo-task-zod-params-"
    let! p = VibeFs.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |})
    let td = get p "tool.definition"

    let extendCalls = ResizeArray<obj>()
    let describeCalls = ResizeArray<string>()
    let optionalCalls = ResizeArray<string>()
    let existingReportField = createObj [ "kind", box "existing-report" ]
    let zodLikeParams = createObj [
        "safeExtend", box (System.Func<obj, obj>(fun arg ->
            extendCalls.Add(arg)
            createObj [ "kind", box "extended" ]))
        "shape", box (createObj [ "completedWorkReport", box existingReportField ])
    ]
    let taskDef = createObj [ "description", box "native"; "parameters", box zodLikeParams ]

    do! td $ (createObj [ "toolID", box "task" ], taskDef) |> unbox<JS.Promise<unit>>
    check "mimo task.definition rewrites zod-like parameters" (string (get (get taskDef "parameters") "kind") = "extended")
    check "mimo task.definition reuses existing report field on zod schema" (obj.ReferenceEquals(get extendCalls.[0] "completedWorkReport", existingReportField))
    do! rmAsync workspaceDir
}
