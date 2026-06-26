module VibeFs.Kernel.ToolCatalog.ToolSpec

/// Pure description of a tool: name, prose description shown to the model,
/// per-parameter doc strings, and the required parameter list.  Every adapter
/// (Opencode Zod, Mux JSON Schema, …) consumes the same record.
type ToolSpec =
    { name: string
      description: string
      paramDocs: Map<string, string>
      requiredFields: string list }

let internal map (entries: (string * string) list) : Map<string, string> = Map.ofList entries
