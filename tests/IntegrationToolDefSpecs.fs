module VibeFs.Tests.IntegrationToolDefSpecs

open Fable.Core
open Fable.Core.JsInterop
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationToolSetup
open VibeFs.Kernel.Dyn
open VibeFs.Mux.Plugin
open VibeFs.Opencode.Plugin
open VibeFs.Mux.AiSettings
open VibeFs.Shell.ChildAgentRegistry
open VibeFs.Shell.WikiFiles


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
            "properties", box (createObj [ "operation", box (createObj [ "type", box "object" ]) ])
            "required", box [| box "operation" |]
        ]
    let taskDef = createObj [ "description", box "native"; "parameters", box taskParams ]
    do! mimoTd $ (createObj [ "toolID", box "task" ], taskDef) |> unbox<JS.Promise<unit>>
    check "mimo task.definition keeps parameters not jsonSchema" (isNullish (get taskDef "jsonSchema"))
    check "mimo task.definition fuses report into parameters" (not (isNullish (get (get (get taskDef "parameters") "properties") "completedWorkReport")))
    check "mimo task.definition removes host task_id from schema" (isNullish (get (get (get taskDef "parameters") "properties") "task_id"))
    check "mimo task.definition preserves operation" (not (isNullish (get (get (get taskDef "parameters") "properties") "operation")))

    let taskJsonSchema = createObj [
        "type", box "object"
        "properties", box (createObj [ "operation", box (createObj [ "type", box "object" ]); "task_id", box (createObj [ "type", box "string" ]) ])
        "required", box [| box "operation"; box "task_id" |]
    ]
    let taskJsonDef = createObj [ "description", box "native"; "jsonSchema", box taskJsonSchema ]
    do! mimoTd $ (createObj [ "toolID", box "task" ], taskJsonDef) |> unbox<JS.Promise<unit>>
    check "mimo task.definition rewrites jsonSchema when that is the exposed path" (not (isNullish (get (get (get taskJsonDef "jsonSchema") "properties") "completedWorkReport")))
    check "mimo task.definition strips task_id from jsonSchema" (isNullish (get (get (get taskJsonDef "jsonSchema") "properties") "task_id"))
    let jsonRequired = unbox<obj[]> (get (get taskJsonDef "jsonSchema") "required") |> Array.map string
    check "mimo task.definition keeps operation required in jsonSchema" (jsonRequired |> Array.contains "operation")
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
    let teb = get p "tool.execute.before"

    let operation = createObj [ "action", box "done"; "id", box "T1"; "event_summary", box "Finished parser fix" ]
    let originalArgs = createObj [ "operation", operation; "completedWorkReport", box "Detailed backlog report" ]
    let beforeOut = createObj [ "args", box originalArgs ]
    let hookInput = createObj [ "tool", box "task"; "sessionID", box "s1"; "callID", box "c1" ]

    do! teb $ (hookInput, beforeOut) |> unbox<JS.Promise<unit>>
    let sanitizedArgs = get beforeOut "args"
    check "mimo task execute.before keeps operation" (not (isNullish (get sanitizedArgs "operation")))
    check "mimo task execute.before strips report before host call" (isNullish (get sanitizedArgs "completedWorkReport"))
    check "mimo task execute.before captures report for backlog replay" (VibeFs.Opencode.MagicTodo.takeCompletedWorkReport "c1" = "Detailed backlog report")
    do! rmAsync workspaceDir
}

let mimoTaskExecuteNestedReportSpec () = promise {
    let! workspaceDir = mkdtempAsync "mimo-task-nested-report-"
    let! p = VibeFs.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |})
    let teb = get p "tool.execute.before"

    let operation = createObj [ "action", box "create"; "summary", box "Build feature"; "completedWorkReport", box "Misplaced backlog report" ]
    let originalArgs = createObj [ "operation", operation ]
    let beforeOut = createObj [ "args", box originalArgs ]
    let hookInput = createObj [ "tool", box "task"; "sessionID", box "s1"; "callID", box "cn1" ]

    do! teb $ (hookInput, beforeOut) |> unbox<JS.Promise<unit>>
    let sanitizedArgs = get beforeOut "args"
    let sanitizedOperation = get sanitizedArgs "operation"
    check "mimo task execute.before keeps operation when report nested inside" (not (isNullish sanitizedOperation))
    check "mimo task execute.before keeps real operation fields" (str sanitizedOperation "summary" = "Build feature")
    check "mimo task execute.before strips report nested inside operation" (isNullish (get sanitizedOperation "completedWorkReport"))
    check "mimo task execute.before leaves no top-level report" (isNullish (get sanitizedArgs "completedWorkReport"))
    check "mimo task execute.before captures nested report for backlog replay" (VibeFs.Opencode.MagicTodo.takeCompletedWorkReport "cn1" = "Misplaced backlog report")
    do! rmAsync workspaceDir
}

let mimoTaskExecuteInPlaceStripSpec () = promise {
    let! workspaceDir = mkdtempAsync "mimo-task-inplace-"
    let! p = VibeFs.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |})
    let teb = get p "tool.execute.before"

    let operation = createObj [ "action", box "create"; "summary", box "test task tool" ]
    let originalArgs = createObj [ "operation", operation; "completedWorkReport", box "top-level report text"; "task_id", box "T99" ]
    let beforeOut = createObj [ "args", box originalArgs ]
    let hookInput = createObj [ "tool", box "task"; "sessionID", box "s1"; "callID", box "ci1" ]

    do! teb $ (hookInput, beforeOut) |> unbox<JS.Promise<unit>>
    let sanitized = get beforeOut "args"
    check "mimo task strip rebuilds args without completedWorkReport" (isNullish (get sanitized "completedWorkReport"))
    check "mimo task strip rebuilds args without task_id" (isNullish (get sanitized "task_id"))
    check "mimo task strip keeps operation on rebuilt args" (not (isNullish (get sanitized "operation")))
    check "mimo task strip leaves original args reference untouched" (str originalArgs "completedWorkReport" = "top-level report text")

    let nestedOperation = createObj [ "action", box "create"; "summary", box "nested case"; "completedWorkReport", box "nested report text" ]
    let nestedArgs = createObj [ "operation", nestedOperation ]
    let nestedBeforeOut = createObj [ "args", box nestedArgs ]
    do! teb $ (createObj [ "tool", box "task"; "sessionID", box "s1"; "callID", box "ci2" ], nestedBeforeOut) |> unbox<JS.Promise<unit>>
    let sanitizedNested = get nestedBeforeOut "args"
    let sanitizedNestedOperation = get sanitizedNested "operation"
    check "mimo task strip rebuilds operation without nested report" (isNullish (get sanitizedNestedOperation "completedWorkReport"))
    check "mimo task strip keeps real fields on rebuilt operation" (str sanitizedNestedOperation "summary" = "nested case")
    check "mimo task strip leaves original operation reference untouched" (str nestedOperation "completedWorkReport" = "nested report text")
    do! rmAsync workspaceDir
}

let mimoTaskExecuteStripsTaskIdSpec () = promise {
    let! workspaceDir = mkdtempAsync "mimo-task-strip-task-id-"
    let! p = VibeFs.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |})
    let teb = get p "tool.execute.before"

    let operation = createObj [ "action", box "list" ]
    let originalArgs = createObj [ "operation", operation; "task_id", box "T4"; "completedWorkReport", box "noop report" ]
    let beforeOut = createObj [ "args", box originalArgs ]
    do! teb $ (createObj [ "tool", box "task"; "sessionID", box "s1"; "callID", box "ctid" ], beforeOut) |> unbox<JS.Promise<unit>>
    let sanitized = get beforeOut "args"
    check "mimo task execute.before rebuilds args without task_id" (isNullish (get sanitized "task_id"))
    check "mimo task execute.before keeps operation on rebuilt args" (not (isNullish (get sanitized "operation")))
    check "mimo task execute.before leaves original args untouched" (str originalArgs "task_id" = "T4")
    do! rmAsync workspaceDir
}

let mimoTaskDefinitionHandlesZodLikeParametersSpec () = promise {
    let! workspaceDir = mkdtempAsync "mimo-task-zod-params-"
    let! p = VibeFs.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |})
    let td = get p "tool.definition"

    let extendCalls = ResizeArray<obj>()
    let describeCalls = ResizeArray<string>()
    let optionalCalls = ResizeArray<string>()
    let summaryField = createObj [
        "describe", box (System.Func<obj, obj>(fun desc ->
            describeCalls.Add(string desc)
            createObj [
                "optional", box (System.Func<obj>(fun () ->
                    optionalCalls.Add("optional")
                    createObj [ "kind", box "optional-string"; "description", desc ]))
            ]))
    ]
    let zodLikeParams = createObj [
        "safeExtend", box (System.Func<obj, obj>(fun arg ->
            extendCalls.Add(arg)
            createObj [ "kind", box "extended" ]))
        "shape", box (createObj [
            "operation", box (createObj [
                "options", box [| createObj [ "shape", box (createObj [ "summary", box summaryField ]) ] |]
            ])
        ])
    ]
    let taskDef = createObj [ "description", box "native"; "parameters", box zodLikeParams ]

    do! td $ (createObj [ "toolID", box "task" ], taskDef) |> unbox<JS.Promise<unit>>
    check "mimo task.definition rewrites zod-like parameters" (string (get (get taskDef "parameters") "kind") = "extended")
    check "mimo task.definition adds report field through safeExtend" (
        string (get (get extendCalls.[0] "completedWorkReport") "kind") = "optional-string"
        && string (get (get extendCalls.[0] "completedWorkReport") "description") = VibeFs.Kernel.MagicTodo.mimoReportFieldDesc)
    check "mimo task.definition derives report field from host zod schema" (
        describeCalls.Count = 1
        && describeCalls.[0] = VibeFs.Kernel.MagicTodo.mimoReportFieldDesc
        && optionalCalls.Count = 1)
    do! rmAsync workspaceDir
}
