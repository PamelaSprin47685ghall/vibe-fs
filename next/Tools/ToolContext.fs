namespace Wanxiangshu.Next.Tools

open System
open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Process


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
