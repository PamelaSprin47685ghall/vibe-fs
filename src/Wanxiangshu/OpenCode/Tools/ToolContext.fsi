namespace Wanxiangshu.OpenCode

open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

type ToolContext =
    { SessionId: SessionId
      Workspace: string
      Cancellation: CancellationToken }

type ToolInput = { Payload: string }

type ToolOutput = { Result: string; Truncated: bool }

type Tool =
    { Name: string
      Description: string
      SchemaJson: string
      Execute: ToolContext -> ToolInput -> Task<ToolOutput> }
