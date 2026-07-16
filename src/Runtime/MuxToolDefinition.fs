module Wanxiangshu.Runtime.MuxToolDefinition

open Fable.Core
open Fable.Core.JsInterop

type JsonSchema =
    { ``type``: string
      properties: obj
      required: string array option
      additionalProperties: bool option }

type ToolDefinition =
    { name: string
      description: string
      parameters: JsonSchema
      execute: obj -> obj -> JS.Promise<string>
      condition: (obj -> bool) option }

let mkSchema (props: obj) (required: string array) : JsonSchema =
    { ``type`` = "object"
      properties = props
      required = Some required
      additionalProperties = Some false }
