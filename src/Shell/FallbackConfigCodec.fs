module Wanxiangshu.Shell.FallbackConfigCodec

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Shell.Dyn
open Wanxiangshu.Kernel.FallbackKernel.Types

[<Import("parse", "yaml")>]
let private yamlParse (text: string) : obj = jsNative

[<Import("readFileSync", "node:fs")>]
let private readFileSync (path: string) (encoding: string) : string = jsNative

[<Import("join", "node:path")>]
let private pathJoin (a: string) (b: string) : string = jsNative

let private splitFrontMatter (content: string) : obj option =
    let trimmed = content.TrimStart('\r', '\n')

    if not (trimmed.StartsWith("---")) then
        None
    else
        let afterFirst = trimmed.[3..].TrimStart('\r', '\n')

        match afterFirst.IndexOf("---") with
        | -1 -> None
        | closeIdx ->
            let yamlText = afterFirst.[.. closeIdx - 1]

            try
                Some(yamlParse yamlText)
            with _ ->
                None

let normalizeAgentName (name: string) : string =
    name
    |> Seq.filter (fun c ->
        not (
            System.Char.IsWhiteSpace(c)
            || System.Char.GetUnicodeCategory(c) = System.Globalization.UnicodeCategory.Format
        ))
    |> Seq.map System.Char.ToLowerInvariant
    |> Seq.toArray
    |> System.String

let parseModelId (s: string) : string * string * ModelVariant option =
    let parts = s.Split(':')
    let basePart = parts.[0].Trim()
    let variantOpt = if parts.Length > 1 then Some(parts.[1].Trim()) else None
    let slash = basePart.IndexOf('/')

    if slash >= 0 then
        let provider = basePart.Substring(0, slash).Trim()
        let model = basePart.Substring(slash + 1).Trim()
        provider, model, variantOpt
    else
        "", basePart, variantOpt

let private parseThinking (o: obj) : bool =
    match Dyn.opt o "thinking" with
    | Some thinkingObj ->
        match Dyn.str thinkingObj "type" with
        | "enabled" -> true
        | _ -> false
    | None -> false

let parseModelEntry (entry: obj) : FallbackModel =
    if Dyn.typeIs entry "string" then
        let provider, model, variant = parseModelId (string entry)

        { ProviderID = provider
          ModelID = model
          Variant = variant
          Temperature = None
          TopP = None
          MaxTokens = None
          ReasoningEffort = None
          Thinking = false }
    elif Dyn.typeIs entry "object" then
        let id =
            match Dyn.opt entry "id" with
            | Some idObj -> string idObj
            | None -> ""

        let provider, model, variant = parseModelId id

        { ProviderID = provider
          ModelID = model
          Variant = variant
          Temperature = Dyn.opt entry "temperature" |> Option.map (fun o -> float (string o))
          TopP = Dyn.opt entry "topP" |> Option.map (fun o -> float (string o))
          MaxTokens = Dyn.opt entry "maxTokens" |> Option.map (fun o -> int (string o))
          ReasoningEffort = Dyn.opt entry "reasoningEffort" |> Option.map (fun o -> string o)
          Thinking = parseThinking entry }
    else
        { ProviderID = ""
          ModelID = ""
          Variant = None
          Temperature = None
          TopP = None
          MaxTokens = None
          ReasoningEffort = None
          Thinking = false }

let private parseChain (entries: obj list) : FallbackChain = entries |> List.map parseModelEntry

let emptyConfig: FallbackConfig =
    { DefaultChain = []
      AgentChains = Map.ofList []
      MaxRetries = 2
      LoopMaxContinues = 3
      MaxRecoveries = 5 }

/// True when two models share the same provider/model identity (variant ignored).
let sameModelIdentity (a: FallbackModel) (b: FallbackModel) : bool =
    a.ProviderID = b.ProviderID && a.ModelID = b.ModelID

/// Resolve the configured agent chain (normalized agent name) or the default chain.
let resolveConfiguredChain (cfg: FallbackConfig) (agentName: string) : FallbackChain =
    let key = if agentName = "" then "" else normalizeAgentName agentName

    match Map.tryFind key cfg.AgentChains with
    | Some c -> c
    | None -> cfg.DefaultChain

/// Prepend the live session model to a configured chain, de-duplicating by identity.
/// Mirrors FallbackEventBridge's first-time chain materialization.
let prependCurrentModel (current: FallbackModel option) (resolved: FallbackChain) : FallbackChain =
    match current with
    | Some current ->
        match resolved with
        | first :: _ when sameModelIdentity first current -> resolved
        | _ ->
            let filtered = resolved |> List.filter (fun m -> not (sameModelIdentity m current))

            current :: filtered
    | None -> resolved

/// Build a subagent fallback chain without inventing a fake "default/default" model.
/// Priority: non-empty config chain → child runtime chain → parent runtime chain →
/// parent live model (as singleton) → empty (caller must fail closed).
let resolveSubagentChain
    (cfg: FallbackConfig)
    (agentName: string)
    (childRuntimeChain: FallbackChain)
    (parentRuntimeChain: FallbackChain)
    (parentLiveModel: FallbackModel option)
    : FallbackChain =
    let configured = resolveConfiguredChain cfg agentName

    if not configured.IsEmpty then
        prependCurrentModel parentLiveModel configured
    elif not childRuntimeChain.IsEmpty then
        childRuntimeChain
    elif not parentRuntimeChain.IsEmpty then
        parentRuntimeChain
    else
        match parentLiveModel with
        | Some m -> [ m ]
        | None -> []

let extractFallbackConfig (frontmatter: obj) : FallbackConfig option =
    if isNullish frontmatter then
        None
    else
        match Dyn.opt frontmatter "models" with
        | None -> None
        | Some modelsObj ->
            let defaultChain =
                match Dyn.opt modelsObj "default" with
                | None -> []
                | Some defaultObj ->
                    match defaultObj with
                    | :? System.Collections.IEnumerable as arr -> parseChain (arr |> Seq.cast<obj> |> Seq.toList)
                    | _ -> []

            let agentChains =
                match Dyn.opt modelsObj "agents" with
                | None -> Map.empty
                | Some agentsObj when Dyn.typeIs agentsObj "object" ->
                    let keys = Dyn.keys agentsObj

                    keys
                    |> Array.fold
                        (fun acc key ->
                            let valObj = Dyn.get agentsObj key

                            match valObj with
                            | :? System.Collections.IEnumerable as arr ->
                                let c = arr |> Seq.cast<obj> |> Seq.toList |> parseChain
                                Map.add (normalizeAgentName key) c acc
                            | _ -> acc)
                        Map.empty
                | _ -> Map.empty

            Some
                { DefaultChain = defaultChain
                  AgentChains = agentChains
                  MaxRetries = 2
                  LoopMaxContinues = 3
                  MaxRecoveries = 5 }

let loadFallbackConfig (directory: string) : FallbackConfig option =
    try
        let agentsPath = pathJoin directory "AGENTS.md"
        let content = readFileSync agentsPath "utf-8"

        match splitFrontMatter content with
        | None -> None
        | Some frontmatter -> extractFallbackConfig frontmatter
    with _ ->
        None
