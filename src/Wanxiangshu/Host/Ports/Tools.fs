namespace Wanxiangshu.Host.Ports

open System.Threading
open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

/// HNI P-04: product tools contributed to a host.
type ToolInvocation =
    { Session: SessionId
      CallId: ToolCallId
      Workspace: WorkspacePath
      Args: CanonicalJsonText
      Cancellation: CancellationToken
      Progress: (string -> unit) option }

type ToolOutcome =
    { Text: string
      Truncated: bool
      IsError: bool
      Details: CanonicalJsonText option }

type ToolSpec =
    { Name: string
      Description: string
      Schema: JsonSchema
      Execute: ToolInvocation -> Task<ToolOutcome> }

type IToolRegistrar =
    abstract Register: tools: ToolSpec list -> Result<unit, HostError>
