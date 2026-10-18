namespace Wanxiangshu.Repository.Programming.Js.OpenCode

open System.Threading.Tasks

/// Generated js-* Host boundary. Host schema factories and ToolSpec records are
/// opaque; the registered tool exposes only metadata and bounded execution.
[<RequireQualifiedAccess>]
module JsToolHostSurface =



    /// `sessionDirectories` mirrors the plugin's live session id → directory map:
    /// the registered session's own directory wins over `workspaceRoot`.
    val createRegistered:
        toolModule: obj ->
        role: string ->
        language: string ->
        workspaceRoot: string ->
        store: obj ->
        sessionDirectories: obj ->
            obj

    val name: handle: obj -> string
    val description: handle: obj -> string
    val argumentNames: handle: obj -> string array
    val argumentSchema: handle: obj -> name: string -> obj
    val execute: handle: obj -> args: obj -> context: obj -> Task<obj>
