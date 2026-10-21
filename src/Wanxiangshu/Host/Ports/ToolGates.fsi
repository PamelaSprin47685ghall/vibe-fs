namespace Wanxiangshu.Host.Ports

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

/// HNI P-05: product gates around every tool call (product and host built-in tools).
type ToolCallView =
    { Session: SessionId
      CallId: ToolCallId
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
