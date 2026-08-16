namespace Wanxiangshu.OpenCode

open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

/// Builtin filesystem tools cross as one small JS-native output boundary. The
/// Host ToolContext and cancellation token remain implementation resources.
[<RequireQualifiedAccess>]
module FileToolsSurface =

    let private context workspace : ToolContext =
        { SessionId = SessionId.create "repository-programming-surface"
          Workspace = workspace
          Cancellation = CancellationToken.None }

    let private execute (tool: Tool) (workspace: string) (payload: string) : Task<obj> =
        task {
            let! output = tool.Execute (context workspace) { Payload = payload }
            return box {| result = output.Result; truncated = output.Truncated |}
        }

    let toolName (kind: string) : string =
        match kind with
        | "read"
        | "write"
        | "edit" -> kind
        | _ -> ""

    let read (workspace: string) (payload: string) : Task<obj> =
        execute (FileTools.fileReadTool ()) workspace payload

    let write (workspace: string) (payload: string) : Task<obj> =
        execute (FileTools.fileWriteTool ()) workspace payload

    let edit (workspace: string) (payload: string) : Task<obj> =
        execute (FileTools.fileEditTool ()) workspace payload
