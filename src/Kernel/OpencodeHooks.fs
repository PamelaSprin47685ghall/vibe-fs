module VibeFs.Kernel.OpencodeHooks

type ToolDefinitionSchema =
    { properties: Map<string, obj>
      required: string list option }

type ToolUiRequest =
    | Coder of summaries: string list
    | Reader of intents: string list

let defaultExcludedAgents = [ "browser"; "reader"; "executor"; "title" ]

let stripUiParameter (schema: ToolDefinitionSchema) : ToolDefinitionSchema =
    { properties = Map.remove "_ui" schema.properties
      required = schema.required |> Option.map (List.filter ((<>) "_ui")) }

let resolveToolUi (request: ToolUiRequest) : string =
    match request with
    | Coder summaries -> String.concat "; " summaries
    | Reader intents -> String.concat "; " intents
