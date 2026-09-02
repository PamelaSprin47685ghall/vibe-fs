namespace Wanxiangshu.Repository.Programming.Js.OpenCode

open System.Threading.Tasks

/// Generated js-* Host boundary. Host schema factories and ToolSpec records are
/// opaque; the registered tool exposes only metadata and bounded execution.
[<RequireQualifiedAccess>]
module JsToolHostSurface =

    val builtinTools: unit -> string array
    val annotate: builtinName: string -> description: string -> jsRoleToolName: string -> string
    val validateRecommendation: jsRoleToolName: string -> visibleToolNames: string array -> obj

    val createRegistered:
        toolModule: obj -> role: string -> language: string -> workspaceRoot: string -> store: obj -> obj

    val name: handle: obj -> string
    val description: handle: obj -> string
    val argumentNames: handle: obj -> string array
    val execute: handle: obj -> args: obj -> context: obj -> Task<obj>
