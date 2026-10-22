namespace Wanxiangshu.Host.Ports

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

/// HNI P-05: product gates around every tool call (product and host built-in tools).
///
/// `ProviderRun` is the provider run the host observed for THIS call, the
/// same evidence the invocation carries. A gate that needs the exact provider
/// fence (the waiting question's fence, the retried delivery's fence) reads it
/// here; it is never resolved from the session's current binding.
type ToolCallView =
    { Session: SessionId
      CallId: ToolCallId
      ProviderRun: ProviderRunIdentity option
      Tool: string
      Args: CanonicalJsonText
      Role: AgentRole option }

[<RequireQualifiedAccess>]
type ToolCallVerdict =
    | Allow
    | Rewrite of args: CanonicalJsonText
    | Refuse of reason: string

[<RequireQualifiedAccess>]
type ToolResultEdit =
    | Keep
    | Replace of result: ToolOutcome

type ToolDefinitionView =
    { Tool: string
      Description: string
      Schema: JsonSchema }

[<RequireQualifiedAccess>]
type ToolDefinitionEdit =
    | Keep
    | Describe of description: string * schema: JsonSchema

type IToolGate =
    abstract BeforeTool: call: ToolCallView -> Task<ToolCallVerdict>
    abstract AfterTool: call: ToolCallView * result: ToolOutcome -> Task<ToolResultEdit>
    abstract DefineTool: definition: ToolDefinitionView -> ToolDefinitionEdit
