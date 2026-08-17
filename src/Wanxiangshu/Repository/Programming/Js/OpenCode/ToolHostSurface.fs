namespace Wanxiangshu.Repository.Programming.Js.OpenCode

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.OpenCode

/// Generated js-* Host boundary. Host schema factories and ToolSpec records are
/// opaque; the registered tool exposes only metadata and bounded execution.
[<RequireQualifiedAccess>]
module JsToolHostSurface =

    type private JsRegisteredToolHandle(spec: ToolSpec, registered: obj) =
        member _.Spec = spec
        member _.Registered = registered

    [<Emit("$0.execute($1, $2)")>]
    let private invokeRegistered (registered: obj) (args: obj) (context: obj) : Task<obj> = jsNative

    let builtinTools () : string array =
        BuiltinToolDescriptionHook.BuiltinFilesystemTools |> Set.toArray

    let annotate (builtinName: string) (description: string) (jsRoleToolName: string) : string =
        BuiltinToolDescriptionHook.annotate builtinName description jsRoleToolName

    let validateRecommendation (jsRoleToolName: string) (visibleToolNames: string array) : obj =
        match BuiltinToolDescriptionHook.validateRecommendation jsRoleToolName (Set.ofArray visibleToolNames) with
        | Ok() -> box {| ok = true |}
        | Error message -> box {| ok = false; error = message |}

    let createRegistered
        (toolModule: obj)
        (role: string)
        (language: string)
        (workspaceRoot: string)
        (store: obj)
        : obj =
        match JsGeneratorSurface.typedRole role language with
        | None -> null
        | Some surface ->
            let persistence =
                if isNull store then
                    None
                else
                    Some(JsTransactionSurface.persistenceOf store)

            let factory = ToolHostCodec.factory toolModule
            let spec = JsToolSpec.create factory surface workspaceRoot persistence
            let registered = ToolHostCodec.register factory spec
            box (JsRegisteredToolHandle(spec, registered))

    let name (handle: obj) : string =
        (unbox<JsRegisteredToolHandle> handle).Spec.Name

    let description (handle: obj) : string =
        (unbox<JsRegisteredToolHandle> handle).Spec.Description

    let argumentNames (handle: obj) : string array =
        (unbox<JsRegisteredToolHandle> handle).Spec.Arguments
        |> List.map fst
        |> List.toArray

    let execute (handle: obj) (args: obj) (context: obj) : Task<obj> =
        invokeRegistered (unbox<JsRegisteredToolHandle> handle).Registered args context
