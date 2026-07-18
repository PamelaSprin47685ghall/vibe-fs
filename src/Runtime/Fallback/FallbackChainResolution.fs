module Wanxiangshu.Runtime.Fallback.FallbackChainResolution

open Wanxiangshu.Runtime
open Wanxiangshu.Runtime.Dyn
open Wanxiangshu.Kernel.FallbackKernel.Types
open Wanxiangshu.Kernel.Subsession.Types

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
/// Mirrors the fallback coordinator's first-time chain materialization.
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

/// Three-state priority for subagent model selection:
/// 1. Host already has an explicit static model configured for this agent
///    (e.g. opencode.jsonc agent.<name>.model) → DelegateToHost unconditionally.
///    Wanxiangshu must never override an explicit host-side config with an
///    injected parent-session model.
/// 2. Otherwise, resolve via the existing chain logic (config → child runtime
///    → parent runtime → parent live model). A non-empty result becomes a
///    RetryChain (wanxiangshu owns model selection/retry for this run).
/// 3. If nothing is known at all, delegate to the host instead of failing
///    the whole subagent run with NoModelConfigured — an empty chain most
///    likely means the host has its own default resolution path (agent
///    config, or its own currentModel fallback), and rejecting outright
///    would be a false negative.
let resolveModelDirective
    (cfg: FallbackConfig)
    (agentName: string)
    (hostConfiguredModel: bool)
    (childRuntimeChain: FallbackChain)
    (parentRuntimeChain: FallbackChain)
    (parentLiveModel: FallbackModel option)
    : ModelDirective =
    if hostConfiguredModel then
        DelegateToHost
    else
        let chain =
            resolveSubagentChain cfg agentName childRuntimeChain parentRuntimeChain parentLiveModel

        if chain.IsEmpty then DelegateToHost else RetryChain chain
