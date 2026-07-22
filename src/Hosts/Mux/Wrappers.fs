module Wanxiangshu.Hosts.Mux.Wrappers

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Primitives.Identity
open Wanxiangshu.Kernel.Errors.DomainError
open Wanxiangshu.Kernel.HostTools
open Wanxiangshu.Runtime.ToolOutputInfo
open Wanxiangshu.Runtime.PromptFragments
open Wanxiangshu.Kernel.Methodology
open Wanxiangshu.Runtime.TreeSitterShell
open Wanxiangshu.Runtime.RuntimeScope
open Wanxiangshu.Runtime.SessionProjectionStore
open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.JsonSchemaBuilders
open Wanxiangshu.Runtime.ToolRuntimeContext
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Runtime.DynField
open Wanxiangshu.Runtime.MuxHostBindings
open Wanxiangshu.Runtime.ToolExecute
open Wanxiangshu.Runtime.ToolContextCodec
open Wanxiangshu.Hosts.Mux.WrappersReview
open Wanxiangshu.Runtime.MuxToolDefinition
open Wanxiangshu.Kernel.Subsession.Types
open Wanxiangshu.Runtime.SubsessionEventRouter

let strField = Wanxiangshu.Runtime.DynField.strField
let optInt = Wanxiangshu.Runtime.DynField.optInt
let optBool = Wanxiangshu.Runtime.DynField.optBool
let optField = Wanxiangshu.Runtime.DynField.optField

type JsonSchema = Wanxiangshu.Runtime.MuxToolDefinition.JsonSchema
type ToolDefinition = Wanxiangshu.Runtime.MuxToolDefinition.ToolDefinition

let requireStrArray (a: obj) (k: string) : string array =
    strListField a k |> Option.map List.toArray |> Option.defaultValue [||]

let mkSchema (props: obj) (required: string array) : JsonSchema =
    MuxToolDefinition.mkSchema props required

let strProp = jsonStrProp
let numProp = jsonNumProp
let boolProp = jsonBoolProp
let strEnumProp = jsonStrEnumProp
let strEnumPropWithDefault = jsonStrEnumPropWithDefault
let strArrayProp = jsonStrArrayProp

let requireWorkspaceId (config: obj) (toolName: string) : Result<string, DomainError> =
    decodeMuxConfig (unbox<IMuxToolContext> config)
    |> Result.map (fun ctx -> ctx.WorkspaceId |> Option.map Id.workspaceIdValue |> Option.defaultValue "")
    |> Result.mapError (function
        | InvalidIntent(_, "workspaceId", _) -> InvalidIntent(toolName, "workspaceId", "required")
        | e -> e)

let private applySyntaxCheck (result: obj) (args: obj) (config: obj) : JS.Promise<obj> =
    promise {
        match extractFilePath args with
        | None -> return result
        | Some filePath ->
            try
                let cwd =
                    match fromMuxConfig config with
                    | Ok runtime -> runtime.Execution.Directory
                    | Error _ -> ""

                let! formatted = readAndCheckSyntax filePath cwd false

                match formatted with
                | None -> return result
                | Some f ->
                    if Dyn.typeIs result "string" then
                        let wrapped =
                            addSyntax (plainText (string result)) f
                            |> render

                        return box wrapped
                    elif Dyn.typeIs result "object" && Dyn.truthy (Dyn.get result "success") then
                        let out = Dyn.str result "output"

                        let wrapped =
                            addSyntax (plainText out) f
                            |> render

                        return Dyn.withKey result "output" (box wrapped)
                    else
                        return result
            with _ ->
                return result
    }

let private isThenable (value: obj) : bool =
    not (Dyn.isNullish value) && Dyn.typeIs (Dyn.get value "then") "function"

let private mkResultWrapper (targetTool: string) (callback: obj -> obj -> obj -> JS.Promise<obj>) : obj =
    let wrapperFn =
        System.Func<obj, obj, obj>(fun tool config ->
            let orig = tool?execute

            if not (Dyn.typeIs orig "function") then
                tool
            else
                let executeFn =
                    System.Func<obj, obj, JS.Promise<obj>>(fun args opts ->
                        promise {
                            let raw = invokeToolExecute tool args opts

                            let! v =
                                if isThenable raw then
                                    unbox<JS.Promise<obj>> raw
                                else
                                    Promise.lift raw

                            return! callback v args config
                        })

                Dyn.withKey tool "execute" (box executeFn))

    createObj [ "targetTool", box targetTool; "wrapper", box wrapperFn ]

let private mkSyncResultWrapper (targetTool: string) (callback: obj -> obj) : obj =
    mkResultWrapper targetTool (fun result _ _ -> Promise.lift (callback result))

let private mkSyntaxWrappers () : obj array =
    [| mkResultWrapper "file_edit_replace_string" (fun result args config -> applySyntaxCheck result args config)
       mkResultWrapper "file_edit_insert" (fun result args config -> applySyntaxCheck result args config) |]

let createAllWrappersFor
    (host: Host)
    (tools: obj)
    (hostReadExec: HostFunctionCapture)
    (scope: RuntimeScope)
    : obj array =
    let projection = scope.Projection

    Array.append (mkSyntaxWrappers ()) [| mkFileReadCapture hostReadExec; mkAgentReportOverride () |]

let createAllWrappers (tools: obj) (hostReadExec: HostFunctionCapture) (scope: RuntimeScope) : obj array =
    createAllWrappersFor mux tools hostReadExec scope
