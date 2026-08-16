namespace Wanxiangshu.OpenCode

open System.Threading.Tasks
open Fable.Core
open Fable.Core.JsInterop

/// Registered mv/rm owner boundary. The Host factory and ToolSpec records stay
/// opaque while names, argument vocabulary and bounded execution cross as JS
/// values.
type private FileMutationHandle(spec: ToolSpec, registered: obj) =
    member _.Spec = spec
    member _.Registered = registered

[<RequireQualifiedAccess>]
module FileMutationSurface =

    [<Emit("$0.execute($1, $2)")>]
    let private invokeRegistered (registered: obj) (args: obj) (context: obj) : Task<obj> = jsNative

    let private create (builder: HostToolFactory -> ToolSpec) (toolModule: obj) : obj =
        let factory = ToolHostCodec.factory toolModule
        let spec = builder factory
        let registered = ToolHostCodec.register factory spec
        box (FileMutationHandle(spec, registered))

    let createMv (toolModule: obj) : obj = create FileMutationTools.mvSpec toolModule

    let createRm (toolModule: obj) : obj = create FileMutationTools.rmSpec toolModule

    let name (handle: obj) : string =
        (unbox<FileMutationHandle> handle).Spec.Name

    let argumentNames (handle: obj) : string array =
        (unbox<FileMutationHandle> handle).Spec.Arguments |> List.map fst |> List.toArray

    let description (handle: obj) : string =
        (unbox<FileMutationHandle> handle).Spec.Description

    let execute (handle: obj) (args: obj) (context: obj) : Task<obj> =
        invokeRegistered (unbox<FileMutationHandle> handle).Registered args context
