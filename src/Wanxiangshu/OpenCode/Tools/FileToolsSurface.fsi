namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

/// Builtin filesystem tools cross as one small JS-native output boundary. The
/// Host ToolContext and cancellation token remain implementation resources.
[<RequireQualifiedAccess>]
module FileToolsSurface =

    val toolName: kind: string -> string
    val read: workspace: string -> payload: string -> Task<obj>
    val write: workspace: string -> payload: string -> Task<obj>
    val edit: workspace: string -> payload: string -> Task<obj>
