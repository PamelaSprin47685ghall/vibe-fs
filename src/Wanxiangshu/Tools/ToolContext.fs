namespace Wanxiangshu.Tools

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Process


type ToolContext =
    { SessionId: SessionId
      Workspace: string
      Cancellation: CancellationToken
      Deadline: Deadline }

type ToolInput = { Payload: string }
type ToolOutput = { Result: string; Truncated: bool }

type Tool =
    { Name: string
      Description: string
      SchemaJson: string
      Execute: ToolContext -> ToolInput -> Task<ToolOutput> }
