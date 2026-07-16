module Wanxiangshu.Tests.FallbackConfigCodecTests

open Fable.Core
open Fable.Core.JsInterop
open Wanxiangshu.Tests.Assert
open Wanxiangshu.Runtime.Fallback.FallbackConfigCodec
open Wanxiangshu.Hosts.Opencode.Fallback.ConfigLoader
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types

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
    // Simulates PluginComposition.applyFallbackModelOverrides: AGENTS.md key is
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
// resolveSubagentChain / prependCurrentModel
// ---------------------------------------------------------------------------

let resolveSubagentChain_usesParentLiveModelWhenEmpty () =
    let cfg = emptyConfig
    let parent = mkModel "openai" "gpt-4.1" None

    let chain = resolveSubagentChain cfg "coder" [] [] (Some parent)

    equal "singleton from parent" 1 chain.Length
    equal "provider" "openai" chain.[0].ProviderID
    equal "model" "gpt-4.1" chain.[0].ModelID

let resolveSubagentChain_neverInventDefaultDefault () =
    let cfg = emptyConfig
    let chain = resolveSubagentChain cfg "coder" [] [] None
    equal "empty when nothing known" 0 chain.Length

let resolveSubagentChain_prefersConfigThenPrependsParent () =
    let cfg: FallbackConfig =
        { emptyConfig with
            DefaultChain = [ mkModel "a" "fallback" None; mkModel "b" "spare" None ] }

    let parent = mkModel "openai" "gpt-4.1" (Some "high")

    let chain = resolveSubagentChain cfg "coder" [] [] (Some parent)

    equal "length" 3 chain.Length
    equal "head is parent" "openai" chain.[0].ProviderID
    equal "head model" "gpt-4.1" chain.[0].ModelID
    equal "second from config" "a" chain.[1].ProviderID

let resolveSubagentChain_configFirstMatchesParentNoDup () =
    let parent = mkModel "openai" "gpt-4.1" None

    let cfg: FallbackConfig =
        { emptyConfig with
            DefaultChain = [ mkModel "openai" "gpt-4.1" (Some "high"); mkModel "b" "spare" None ] }

    let chain = resolveSubagentChain cfg "coder" [] [] (Some parent)

    equal "no dup" 2 chain.Length
    equal "keeps config head" "openai" chain.[0].ProviderID
    equal "keeps config variant" (Some "high") chain.[0].Variant

let resolveSubagentChain_childRuntimeBeatsParent () =
    let child = [ mkModel "child" "m1" None ]
    let parent = [ mkModel "parent" "m2" None ]
    let live = mkModel "live" "m3" None

    let chain = resolveSubagentChain emptyConfig "coder" child parent (Some live)

    equal "uses child runtime chain" 1 chain.Length
    equal "child provider" "child" chain.[0].ProviderID

let resolveSubagentChain_parentRuntimeWhenNoConfigOrChild () =
    let parent = [ mkModel "parent" "m2" None ]

    let chain = resolveSubagentChain emptyConfig "coder" [] parent None

    equal "uses parent runtime" 1 chain.Length
    equal "parent provider" "parent" chain.[0].ProviderID

let resolveConfiguredChain_normalizesAgentName () =
    let cfg: FallbackConfig =
        { emptyConfig with
            AgentChains = Map.ofList [ "sisyphus-ultraworker", [ mkModel "oai" "gpt5" None ] ] }

    let chain = resolveConfiguredChain cfg "Sisyphus - Ultraworker"
    equal "found via normalize" 1 chain.Length
    equal "provider" "oai" chain.[0].ProviderID

// ---------------------------------------------------------------------------
// resolveModelDirective: three-state priority
// ---------------------------------------------------------------------------

let resolveModelDirective_hostConfiguredAlwaysDelegates () =
    let cfg: FallbackConfig =
        { emptyConfig with
            DefaultChain = [ mkModel "a" "default" None ] }

    match
        resolveModelDirective
            cfg
            "coder"
            true
            [ mkModel "child" "m1" None ]
            [ mkModel "parent" "m2" None ]
            (Some(mkModel "live" "m3" None))
    with
    | DelegateToHost -> ()
    | _ -> check "expected DelegateToHost" false

let resolveModelDirective_notHostConfiguredWithNonEmptyChainRetries () =
    match resolveModelDirective emptyConfig "coder" false [] [] (Some(mkModel "openai" "gpt-4.1" None)) with
    | RetryChain [ m ] -> equal "provider" "openai" m.ProviderID
    | _ -> check "expected RetryChain singleton" false

let resolveModelDirective_notHostConfiguredWithEmptyEverythingDelegatesInsteadOfRejecting () =
    match resolveModelDirective emptyConfig "coder" false [] [] None with
    | DelegateToHost -> ()
    | _ -> check "expected DelegateToHost when nothing known" false

let resolveModelDirective_configuredChainStillWinsOverParentWhenNotHostConfigured () =
    let cfg: FallbackConfig =
        { emptyConfig with
            AgentChains = Map.ofList [ "coder", [ mkModel "oai" "gpt5" None ] ] }

    let parent = Some(mkModel "openai" "gpt-4.1" None)

    match resolveModelDirective cfg "coder" false [] [] parent with
    | RetryChain chain ->
        equal "length" 2 chain.Length
        equal "parent prepended to head" "openai" chain.[0].ProviderID
        equal "config chain follows" "oai" chain.[1].ProviderID
    | _ -> check "expected RetryChain" false

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
    resolveSubagentChain_usesParentLiveModelWhenEmpty ()
    resolveSubagentChain_neverInventDefaultDefault ()
    resolveSubagentChain_prefersConfigThenPrependsParent ()
    resolveSubagentChain_configFirstMatchesParentNoDup ()
    resolveSubagentChain_childRuntimeBeatsParent ()
    resolveSubagentChain_parentRuntimeWhenNoConfigOrChild ()
    resolveConfiguredChain_normalizesAgentName ()
    resolveModelDirective_hostConfiguredAlwaysDelegates ()
    resolveModelDirective_notHostConfiguredWithNonEmptyChainRetries ()
    resolveModelDirective_notHostConfiguredWithEmptyEverythingDelegatesInsteadOfRejecting ()
    resolveModelDirective_configuredChainStillWinsOverParentWhenNotHostConfigured ()
