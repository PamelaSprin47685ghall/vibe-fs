module VibeFs.Tests.IntegrationToolDefSpecsMimo

open Fable.Core
open Fable.Core.JsInterop
open System
open VibeFs.Tests.Assert
open VibeFs.Tests.TempWorkspace
open VibeFs.Tests.IntegrationToolSetup
open VibeFs.Opencode.Plugin
open VibeFs.Kernel.Domain
open VibeFs.Kernel.ToolResult
open VibeFs.Kernel.ToolOutputInfo
open VibeFs.Shell.Dyn

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
    let invalidArgsOut = createObj [ "args", box (createObj []) ]
    do! teb $ (createObj [ "tool", box "apply_patch"; "sessionID", box "s1"; "callID", box "c4" ], invalidArgsOut) |> unbox<JS.Promise<unit>>
    let errText = str invalidArgsOut "error"
    let expected =
        wireEncodeToolError "apply_patch" (InvalidIntent ("apply_patch", "patchText", "required"))
    check "mimo apply_patch execute.before invalid args sets error" (errText <> "")
    check "mimo apply_patch execute.before error uses wireEncodeToolError InvalidIntent" (errText = expected)
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
    check "mimo task execute returns todo envelope" (result.StartsWith "---" && result.Contains hintTodosUpdated)
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
    check "mimo task execute still succeeds with explicit top-level report" (result.Contains hintTodosUpdated)
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
    check "mimo task execute ignores stray task_id" (result.Contains hintTodosUpdated)
    do! rmAsync workspaceDir
}

let mimoTaskDefinitionHandlesZodLikeParametersSpec () = promise {
    let! workspaceDir = mkdtempAsync "mimo-task-zod-params-"
    let! p = VibeFs.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |})
    let td = get p "tool.definition"
    let extendCalls = ResizeArray<obj>()
    let arrayCalls = ResizeArray<string>()
    let minCalls = ResizeArray<int>()
    let describeCalls = ResizeArray<string>()
    let optionalCalls = ResizeArray<string>()
    let methodologyField = createObj [ "kind", box "methodology-optional" ]
    let describedMethodology =
        createObj [
            "optional", box (System.Func<obj>(fun () ->
                optionalCalls.Add("select_methodology")
                methodologyField))
        ]
    let minMethodology =
        createObj [
            "describe", box (System.Func<obj, obj>(fun desc ->
                describeCalls.Add(string desc)
                describedMethodology))
        ]
    let arrayMethodology =
        createObj [
            "min", box (System.Func<obj, obj>(fun count ->
                minCalls.Add(unbox<int> count)
                minMethodology))
        ]
    let existingReportField =
        createObj [
            "kind", box "existing-report"
            "_def", box (createObj [ "typeName", box "ZodString" ])
            "array", box (System.Func<obj>(fun () ->
                arrayCalls.Add("select_methodology")
                arrayMethodology))
        ]
    let zodLikeParams = createObj [
        "safeExtend", box (System.Func<obj, obj>(fun arg ->
            extendCalls.Add(arg)
            createObj [ "kind", box "extended" ]))
        "shape", box (createObj [ "completedWorkReport", box existingReportField ])
    ]
    let taskDef = createObj [ "description", box "native"; "parameters", box zodLikeParams ]
    do! td $ (createObj [ "toolID", box "task" ], taskDef) |> unbox<JS.Promise<unit>>
    check "mimo task.definition rewrites zod-like parameters" (string (get (get taskDef "parameters") "kind") = "extended")
    check "mimo task.definition does not overwrite existing report field on zod schema" (isNullish (get extendCalls.[0] "completedWorkReport"))
    check "mimo task.definition builds methodology from zod string template" (obj.ReferenceEquals(get extendCalls.[0] "select_methodology", describedMethodology))
    check "mimo task.definition calls zod array for methodology" (arrayCalls.Count = 1)
    check "mimo task.definition calls zod min 1 for methodology" (minCalls.Count = 1 && minCalls.[0] = 1)
    check "mimo task.definition describes methodology field" (describeCalls.Count = 1 && describeCalls.[0] = VibeFs.Opencode.HookSchema.selectMethodologyFieldDescription)
    check "mimo task.definition makes methodology required" (optionalCalls.Count = 0)
    do! rmAsync workspaceDir
}