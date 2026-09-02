namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

/// Registered mv/rm owner boundary. The Host factory and ToolSpec records stay
/// opaque while names, argument vocabulary and bounded execution cross as JS
/// values.
[<RequireQualifiedAccess>]
module FileMutationSurface =

    val createMv: toolModule: obj -> obj
    val createRm: toolModule: obj -> obj
    val name: handle: obj -> string
    val argumentNames: handle: obj -> string array
    val description: handle: obj -> string
    val execute: handle: obj -> args: obj -> context: obj -> Task<obj>
