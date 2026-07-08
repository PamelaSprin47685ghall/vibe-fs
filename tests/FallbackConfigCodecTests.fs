module Wanxiangshu.Tests.FallbackConfigCodecTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Shell.FallbackConfigCodec
open Wanxiangshu.Opencode.FallbackConfigLoader
open Wanxiangshu.Kernel.FallbackKernel.Types

// ---------------------------------------------------------------------------
// normalizeAgentName
// ---------------------------------------------------------------------------

let normalizeAgentName_basic () =
    equal "lowercases" "reviewer" (normalizeAgentName "Reviewer")

let normalizeAgentName_stripsSpaces () =
    equal "leading space" "reviewer" (normalizeAgentName "  reviewer")
    equal "trailing space" "reviewer" (normalizeAgentName "reviewer  ")
    equal "inner spaces" "reviewer" (normalizeAgentName "rev  iewer")

let normalizeAgentName_collapsesDashes () =
    equal "dashes collapsed" "sisyphus-ultraworker" (normalizeAgentName "Sisyphus - Ultraworker")

// ---------------------------------------------------------------------------
// parseModelId
// ---------------------------------------------------------------------------

let parseModelId_simple () =
    let p, m, v = parseModelId "openai/gpt-5.5"
    equal "provider" "openai" p
    equal "model" "gpt-5.5" m
    equal "variant" None v

let parseModelId_withVariant () =
    let p, m, v = parseModelId "anthropic/claude-opus:high"
    equal "provider" "anthropic" p
    equal "model" "claude-opus" m
    equal "variant" (Some "high") v

// ---------------------------------------------------------------------------
// parseModelEntry
// ---------------------------------------------------------------------------

let parseModelEntry_string () =
    let e = parseModelEntry (box "openai/gpt-5.5")
    equal "provider" "openai" e.ProviderID
    equal "model" "gpt-5.5" e.ModelID
    equal "variant" None e.Variant

let parseModelEntry_object () =
    let o: obj =
        createObj
            [ "id" ==> box "openai/gpt-5.5"
              "temperature" ==> box 0.7
              "maxTokens" ==> box 4096 ]

    let e = parseModelEntry o
    equal "provider" "openai" e.ProviderID
    equal "model" "gpt-5.5" e.ModelID
    equal "temp" 0.7 (defaultArg e.Temperature 0.0)
    equal "maxTokens" 4096 (defaultArg e.MaxTokens 0)

let parseModelEntry_withReasoningEffort () =
    let o: obj =
        createObj [ "id" ==> box "openai/gpt-5.5"; "reasoningEffort" ==> box "high" ]

    let e = parseModelEntry o
    equal "reasoning" "high" (defaultArg e.ReasoningEffort "")

// ---------------------------------------------------------------------------
// extractFallbackConfig
// ---------------------------------------------------------------------------

let extractFallbackConfig_agentsKeyNormalized () =
    // Keys in agents are normalized (lowercased, whitespace stripped)
    let fm: obj =
        createObj
            [ "models"
              ==> box (
                  createObj
                      [ "agents"
                        ==> box (createObj [ "Sisyphus - Ultraworker" ==> box ([ "openai/gpt-5.5" ]: obj list) ]) ]
              ) ]

    match extractFallbackConfig fm with
    | Some cfg ->
        let chain = Map.tryFind "sisyphus-ultraworker" cfg.AgentChains
        equal "agent chain found after normalization" true (chain.IsSome)
    | None -> check "config extracted" false

let extractFallbackConfig_missingModels () =
    let fm: obj = createObj [ "other" ==> box "value" ]
    equal "missing models → None" None (extractFallbackConfig fm)

let extractFallbackConfig_defaultChain () =
    let fm: obj =
        createObj
            [ "models"
              ==> box (createObj [ "default" ==> box ([ "anthropic/claude-sonnet-4" ]: obj list) ]) ]

    match extractFallbackConfig fm with
    | Some cfg ->
        equal "default chain length" 1 cfg.DefaultChain.Length
        equal "first model provider" "anthropic" cfg.DefaultChain.[0].ProviderID
    | None -> check "default chain extracted" false

// ---------------------------------------------------------------------------
// buildAgentModelOverrides + defaultPreferredModel (config override)
// ---------------------------------------------------------------------------

let private mkModel (pid: string) (mid: string) (variant: string option) : FallbackModel =
    { ProviderID = pid
      ModelID = mid
      Variant = variant
      Temperature = None
      TopP = None
      MaxTokens = None
      ReasoningEffort = None
      Thinking = false }

let buildAgentModelOverrides_producesFirstModel () =
    let cfg: FallbackConfig =
        { DefaultChain = [ mkModel "a" "default" None ]
          AgentChains = Map.ofList [ "sisyphus", [ mkModel "oai" "gpt5" (Some "high") ] ]
          MaxRetries = 2
          LoopMaxContinues = 3
          MaxRecoveries = 5 }

    let overrides = buildAgentModelOverrides cfg

    match Map.tryFind "sisyphus" overrides with
    | Some m -> equal "model string with variant" "oai/gpt5:high" m
    | None -> check "override found" false

let defaultPreferredModel_returnsFirst () =
    let cfg: FallbackConfig =
        { DefaultChain = [ mkModel "a" "m1" None ]
          AgentChains = Map.ofList []
          MaxRetries = 2
          LoopMaxContinues = 3
          MaxRecoveries = 5 }

    match defaultPreferredModel cfg with
    | Some m -> equal "default model" "a/m1" m
    | None -> check "default found" false

let defaultPreferredModel_emptyChain () =
    let cfg: FallbackConfig =
        { DefaultChain = []
          AgentChains = Map.ofList []
          MaxRetries = 2
          LoopMaxContinues = 3
          MaxRecoveries = 5 }

    equal "empty chain → None" None (defaultPreferredModel cfg)

let applyOverride_matchesSpacedAgentName () =
    // Simulates PluginCore.applyFallbackModelOverrides: AGENTS.md key is
    // normalized, opencode.json keeps original display name. The match must
    // work through normalizeAgentName on both sides.
    let fm: obj =
        createObj
            [ "models"
              ==> box (
                  createObj
                      [ "agents"
                        ==> box (createObj [ "Sisyphus - Ultraworker" ==> box ([ "oai/gpt5:high" ]: obj list) ]) ]
              ) ]

    let agentObj: obj =
        createObj [ "Sisyphus - Ultraworker" ==> createObj [ "model" ==> box "old" ] ]

    match extractFallbackConfig fm with
    | Some cfg ->
        let overrides = buildAgentModelOverrides cfg
        let keys: string[] = unbox (JS.Constructors.Object.keys agentObj)

        let matched =
            keys
            |> Array.choose (fun origKey ->
                Map.tryFind (normalizeAgentName origKey) overrides
                |> Option.map (fun m -> origKey, m))

        equal "matched one" 1 matched.Length
        equal "model" "oai/gpt5:high" (snd matched.[0])
    | None -> check "config extracted" false

// ---------------------------------------------------------------------------
// Suite entry
// ---------------------------------------------------------------------------

let run () =
    normalizeAgentName_basic ()
    normalizeAgentName_stripsSpaces ()
    normalizeAgentName_collapsesDashes ()
    parseModelId_simple ()
    parseModelId_withVariant ()
    parseModelEntry_string ()
    parseModelEntry_object ()
    parseModelEntry_withReasoningEffort ()
    extractFallbackConfig_agentsKeyNormalized ()
    extractFallbackConfig_missingModels ()
    extractFallbackConfig_defaultChain ()
    buildAgentModelOverrides_producesFirstModel ()
    defaultPreferredModel_returnsFirst ()
    defaultPreferredModel_emptyChain ()
    applyOverride_matchesSpacedAgentName ()
