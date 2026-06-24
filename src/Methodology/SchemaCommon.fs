module VibeFs.Methodology.SchemaCommon

open System
open System.Text

[<RequireQualifiedAccess>]
type FieldKind =
    | String
    | StringArray

type MethodologyField =
    { name: string
      description: string
      required: bool
      kind: FieldKind
      minArrayItems: int }

type MethodologySchema =
    { methodologyId: string
      toolName: string
      shortDefinition: string
      triggerWhen: string
      toolDescription: string
      fields: MethodologyField list
      meditatorRole: string
      outputSections: string list }

let notebookRecommendedWords = 512

let intentFieldName = "intent"

let backgroundFieldName = "background"

let intentFieldDescription =
    "Mandatory statement of the fundamental intent this methodology must serve on this turn. Aim for about "
    + string notebookRecommendedWords
    + " words or more when helpful; there is no minimum word count. Explain what root problem or decision you are using this methodology to crack—not a task checklist, but the underlying why (e.g. why first-principles rebuild instead of patching, why abduction instead of blame). Tie intent to user goals, failure symptoms, and what success would unblock. Do not paste generic methodology lectures."

let backgroundFieldDescription =
    "Mandatory notebook context for this methodology note. Aim for about "
    + string notebookRecommendedWords
    + " words or more when helpful; there is no minimum word count. Include: current task objective and acceptance criteria; relevant repository paths and symbols; prior attempts and outcomes; constraints from AGENTS.md, README, PRD, or user messages; open questions; risks; and how this methodology should frame the next work step. Do not paste tool catalogs or generic methodology essays—anchor every paragraph to this workspace and this turn."

let intentField: MethodologyField =
    { name = intentFieldName
      description = intentFieldDescription
      required = true
      kind = FieldKind.String
      minArrayItems = 0 }

let backgroundField: MethodologyField =
    { name = backgroundFieldName
      description = backgroundFieldDescription
      required = true
      kind = FieldKind.String
      minArrayItems = 0 }

let methodologyToolName (methodologyId: string) = "methodology_" + methodologyId

let private yamlEscape (s: string) =
    if isNull s then ""
    elif s.Contains('\n') || s.Contains('"') || s.Contains(':') then
        let indented =
            s.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')
            |> Array.map (fun line -> "    " + line)
            |> String.concat "\n"
        "|\n" + indented
    else s

let renderInputYaml (schema: MethodologySchema) (values: Map<string, string>) (arrayValues: Map<string, string list>) =
    let sb = StringBuilder()
    sb.AppendLine("methodology: " + schema.methodologyId) |> ignore
    sb.AppendLine("tool: " + schema.toolName) |> ignore
    sb.AppendLine("inputs:") |> ignore
    for f in schema.fields do
        match f.kind with
        | FieldKind.String ->
            let v = values |> Map.tryFind f.name |> Option.defaultValue ""
            sb.AppendLine("  " + f.name + ": " + yamlEscape v) |> ignore
        | FieldKind.StringArray ->
            let items = arrayValues |> Map.tryFind f.name |> Option.defaultValue []
            sb.AppendLine("  " + f.name + ":") |> ignore
            for item in items do
                sb.AppendLine("    - " + (yamlEscape (item.Trim()))) |> ignore
    sb.ToString()

let renderMeditatorIntent (schema: MethodologySchema) (inputYaml: string) =
    let sections =
        schema.outputSections
        |> List.mapi (fun i s -> $"{i + 1}. {s}")
        |> String.concat "\n"
    $"""You are applying the "{schema.methodologyId}" methodology.

Definition: {schema.shortDefinition}
Use when: {schema.triggerWhen}

Role: {schema.meditatorRole}

The parent agent filled the YAML template below from structured tool arguments. Treat it as ground truth for this turn; do not invent missing file paths or test results.

```yaml
{inputYaml}
```

Produce the tool output in dense modern Chinese unless the inputs are explicitly English-only. Structure your answer with these sections:
{sections}

Do not call tools. Do not propose code edits unless the inputs ask for implementation plans. End with concrete next actions the parent can execute without you."""

let field (name: string) (description: string) required kind minItems =
    { name = name
      description = description
      required = required
      kind = kind
      minArrayItems = minItems }

let reqStr name desc = field name desc true FieldKind.String 0
let optStr name desc = field name desc false FieldKind.String 0
let reqArr name minItems desc = field name desc true FieldKind.StringArray minItems
let optArr name minItems desc = field name desc false FieldKind.StringArray minItems

let buildSchema
    methodologyId
    shortDefinition
    triggerWhen
    extraFields
    meditatorRole
    outputSections
    =
    let toolName = methodologyToolName methodologyId
    let extraNames = extraFields |> List.map (fun f -> f.name) |> String.concat ", "
    let toolDescription =
        "Record a durable, structured "
        + methodologyId
        + " methodology notebook entry for this workspace and turn. "
        + "Fill every required field; intent and background are required with no minimum word count (about "
        + string notebookRecommendedWords
        + " words recommended when helpful). Method-specific fields capture where and how you are applying this methodology: "
        + extraNames
        + ". Tool output summarizes your note for the session. Definition: "
        + shortDefinition
    { methodologyId = methodologyId
      toolName = toolName
      shortDefinition = shortDefinition
      triggerWhen = triggerWhen
      toolDescription = toolDescription
      fields = intentField :: backgroundField :: extraFields
      meditatorRole = meditatorRole
      outputSections = outputSections }

let toToolCatalogSpec (schema: MethodologySchema) : VibeFs.Kernel.ToolCatalog.ToolSpec =
    let paramDocs =
        schema.fields
        |> List.map (fun f -> f.name, f.description)
        |> Map.ofList
    let requiredFields =
        schema.fields |> List.filter (fun f -> f.required) |> List.map (fun f -> f.name)
    { name = schema.toolName
      description = schema.toolDescription
      paramDocs = paramDocs
      requiredFields = requiredFields }