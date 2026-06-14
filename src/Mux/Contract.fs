module VibeFs.Mux.Contract

open Fable.Core
open Fable.Core.JsInterop

/// JSON-Schema shape for a tool's parameters.
type JsonSchema =
    { ``type``: string
      properties: obj
      required: string array option
      additionalProperties: bool option }

/// A tool's declarative contract plus its executor.  Pure data except `execute`.
type ToolDefinition =
    { name: string
      description: string
      parameters: JsonSchema
      execute: obj -> obj -> JS.Promise<string>
      condition: (obj -> bool) option }

/// A structural view of a host tool, for wrapping without the ai-sdk dependency.
[<Emit("$0")>]
let asToolLike (tool: obj) : obj = jsNative

/// Require a workspaceId from the config, returning an error Result when absent.
let requireWorkspaceId (config: obj) (toolName: string) : Result<string, string> =
    let wid = config?("workspaceId")
    if isNull wid || string wid = "" then Error $"{toolName} requires workspaceId"
    else Ok(string wid)
