module Wanxiangshu.Tests.IntegrationToolDefSpecs

open Fable.Core
open Fable.Core.JsInterop
open System
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Tests.TempWorkspace
open Wanxiangshu.Tests.IntegrationToolSetup
open Wanxiangshu.Opencode.Plugin
open Wanxiangshu.Shell.Dyn

open Wanxiangshu.Tests.IntegrationToolDefSpecsMimo

[<Import("Schema", "effect")>]
let private effectSchemaNs: obj = jsNative

let private effectStruct (shape: obj) : obj = effectSchemaNs?("Struct") (shape)
let private effectString: obj = get effectSchemaNs "String"

let toolDefinitionSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "tool-definition-"
        let! p = plugin (box {| directory = workspaceDir |})
        let td = get p "tool.definition"

        let coderDef =
            createObj
                [ "jsonSchema",
                  box (
                      createObj
                          [ "properties",
                            box (
                                createObj
                                    [ "intents", box (createObj [ "type", box "array" ])
                                      "_ui", box (createObj [ "type", box "string" ]) ]
                            )
                            "required", box [| "intents"; "_ui" |] ]
                  ) ]

        do! td $ (createObj [ "toolID", box "coder" ], coderDef) |> unbox<JS.Promise<unit>>
        let props = get (get coderDef "jsonSchema") "properties"
        check "tool.definition does not strip coder _ui property" (not (isNullish (get props "_ui")))
        check "tool.definition keeps coder intents" (not (isNullish (get props "intents")))

        let editParameters =
            effectStruct (
                createObj
                    [ "filePath", effectString
                      "oldString", effectString
                      "newString", effectString ]
            )

        let editDef =
            createObj [ "description", box "native"; "parameters", box editParameters ]

        do! td $ (createObj [ "toolID", box "edit" ], editDef) |> unbox<JS.Promise<unit>>

        check
            "tool.definition preserves edit parameters reference"
            (obj.ReferenceEquals(get editDef "parameters", editParameters))

        let editJsonSchema = get editDef "jsonSchema"
        check "tool.definition builds edit jsonSchema from effect parameters" (not (isNullish editJsonSchema))

        check
            "tool.definition injects warn_tdd into edit jsonSchema"
            (not (isNullish (get (get editJsonSchema "properties") "warn_tdd")))

        let patchParameters = effectStruct (createObj [ "patchText", effectString ])

        let patchDef =
            createObj [ "description", box "native"; "parameters", box patchParameters ]

        do!
            td $ (createObj [ "toolID", box "apply_patch" ], patchDef)
            |> unbox<JS.Promise<unit>>

        check
            "tool.definition preserves apply_patch parameters reference"
            (obj.ReferenceEquals(get patchDef "parameters", patchParameters))

        check
            "tool.definition injects warn_tdd into apply_patch jsonSchema"
            (not (isNullish (get (get (get patchDef "jsonSchema") "properties") "warn_tdd")))

        let patchRequired =
            unbox<obj[]> (get (get patchDef "jsonSchema") "required") |> Array.map string

        check
            "tool.definition does NOT require warn_tdd in required for apply_patch jsonSchema"
            (not (patchRequired |> Array.contains "warn_tdd"))

        let patchProps = get (get patchDef "jsonSchema") "properties"
        let warnTddProp = get patchProps "warn_tdd"
        check "warn_tdd soft-required in jsonSchema properties" (truthy (get warnTddProp "x-wanxiangshu-soft-required"))

        let todoParams = createObj [ "__effectSchema", box true ]

        let todoDef =
            createObj [ "description", box "old desc"; "parameters", box todoParams ]

        do!
            td $ (createObj [ "toolID", box "todowrite" ], todoDef)
            |> unbox<JS.Promise<unit>>

        check
            "tool.definition rewrites todo description"
            (str todoDef "description"
             |> fun text -> text.Contains "append-only work backlog")

        check
            "tool.definition leaves todo parameters untouched"
            (obj.ReferenceEquals(get todoDef "parameters", todoParams))

        let todoSchema = get todoDef "jsonSchema"
        let todoProps = get todoSchema "properties"
        let reportSchema = get todoProps "ahaMoments"
        let required = unbox<obj[]> (get todoSchema "required") |> Array.map string
        check "tool.definition builds todo report field" (str reportSchema "type" = "string")

        check
            "tool.definition builds todo report description"
            ((str reportSchema "description")
                .Contains(Wanxiangshu.Kernel.WorkBacklog.ahaMomentsDesc))

        check "tool.definition does NOT require todo report" (not (required |> Array.contains "ahaMoments"))
        check "tool.definition requires todos" (required |> Array.contains "todos")

        check
            "tool.definition builds todos description"
            (str (get todoProps "todos") "description" = Wanxiangshu.Kernel.WorkBacklog.todosDesc)

        let todoItemProps = get (get (get todoProps "todos") "items") "properties"

        check
            "tool.definition builds todo content description"
            (str (get todoItemProps "content") "description" = Wanxiangshu.Kernel.WorkBacklog.todoContentDesc)

        check
            "tool.definition builds todo status description"
            (str (get todoItemProps "status") "description" = Wanxiangshu.Kernel.WorkBacklog.todoStatusDesc)

        check
            "tool.definition builds todo priority description"
            (str (get todoItemProps "priority") "description" = Wanxiangshu.Kernel.WorkBacklog.todoPriorityDesc)

        let tools = get p "tool"
        let executorTool = get tools "executor"

        let executorDef =
            createObj
                [ "description", get executorTool "description"
                  "args", get executorTool "args" ]

        do!
            td $ (createObj [ "toolID", box "executor" ], executorDef)
            |> unbox<JS.Promise<unit>>

        let executorSchema = get executorDef "jsonSchema"
        let executorProps = get executorSchema "properties"
        check "tool.definition keeps executor program schema" (not (isNullish (get executorProps "program")))
        check "tool.definition keeps executor mode schema" (not (isNullish (get executorProps "mode")))
        check "tool.definition injects executor warn_tdd schema" (not (isNullish (get executorProps "warn_tdd")))
        check "tool.definition injects executor warn schema" (not (isNullish (get executorProps "warn")))
        check "tool.definition does not replace executor with backlog schema" (isNullish (get executorProps "todos"))
        let! mimoP = Wanxiangshu.Opencode.PluginMimo.plugin (box {| directory = workspaceDir |})
        let mimoTd = get mimoP "tool.definition"

        let taskParams =
            createObj
                [ "type", box "object"
                  "properties", box (createObj [ "todos", box (createObj [ "type", box "array" ]) ])
                  "required", box [| box "todos" |] ]

        let taskDef =
            createObj [ "description", box "native"; "parameters", box taskParams ]

        do!
            mimoTd $ (createObj [ "toolID", box "task" ], taskDef)
            |> unbox<JS.Promise<unit>>

        check "mimo task.definition keeps parameters not jsonSchema" (isNullish (get taskDef "jsonSchema"))

        check
            "mimo task.definition fuses report into parameters"
            (not (isNullish (get (get (get taskDef "parameters") "properties") "ahaMoments")))

        check
            "mimo task.definition preserves todos"
            (not (isNullish (get (get (get taskDef "parameters") "properties") "todos")))

        let taskJsonSchema =
            createObj
                [ "type", box "object"
                  "properties",
                  box (
                      createObj
                          [ "todos", box (createObj [ "type", box "array" ])
                            "task_id", box (createObj [ "type", box "string" ]) ]
                  )
                  "required", box [| box "todos"; box "task_id" |] ]

        let taskJsonDef =
            createObj [ "description", box "native"; "jsonSchema", box taskJsonSchema ]

        do!
            mimoTd $ (createObj [ "toolID", box "task" ], taskJsonDef)
            |> unbox<JS.Promise<unit>>

        check
            "mimo task.definition rewrites jsonSchema when that is the exposed path"
            (not (isNullish (get (get (get taskJsonDef "jsonSchema") "properties") "ahaMoments")))

        check
            "mimo task.definition strips task_id from jsonSchema"
            (isNullish (get (get (get taskJsonDef "jsonSchema") "properties") "task_id"))

        let jsonRequired =
            unbox<obj[]> (get (get taskJsonDef "jsonSchema") "required") |> Array.map string

        check "mimo task.definition keeps todos required in jsonSchema" (jsonRequired |> Array.contains "todos")

        check
            "mimo task.definition drops task_id required in jsonSchema"
            (not (jsonRequired |> Array.contains "task_id"))

        do! rmAsync workspaceDir
    }

let toolExecuteBeforeSpec () =
    promise {
        let! workspaceDir = mkdtempAsync "tool-execute-before-"
        let! p = plugin (box {| directory = workspaceDir |})
        let teb = get p "tool.execute.before"

        let intents: obj array =
            [| sampleCoderIntent "fix bug" "a.ts"; sampleCoderIntent "add feature" "b.ts" |]

        let originalArgs =
            createObj
                [ "intents", box intents
                  "warn_tdd", box "i-am-sure-i-have-followed-tdd-and-kolmogorov-principles-and-kept-todo-updated"
                  "warn_reuse", box "this-task-is-not-suitable-to-be-completed-via-continue-tool" ]

        let execOut = createObj [ "args", box originalArgs ]

        do!
            teb
            $ (createObj [ "tool", box "coder"; "sessionID", box "s1"; "callID", box "c1" ], execOut)
            |> unbox<JS.Promise<unit>>

        let nextArgs = get execOut "args"
        check "tool.execute.before populates _ui on returned args" (str nextArgs "_ui" = "fix bug; add feature")

        check
            "tool.execute.before clones and does not mutate in place"
            (not (obj.ReferenceEquals(nextArgs, originalArgs)))

        check
            "tool.execute.before writes _ui onto host args reference"
            (str originalArgs "_ui" = "fix bug; add feature")

        do! rmAsync workspaceDir
    }
