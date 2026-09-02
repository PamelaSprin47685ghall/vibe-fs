namespace Wanxiangshu.OpenCode

open System.Threading.Tasks

/// JS-native owner surface for the Distiller role contract.
///
/// Distillation owns the distinction between its private fixed-cost runtime and
/// the provider-visible execution verb. Role vocabulary and tool permission
/// translation stay in their canonical owners; this surface publishes only the
/// bounded facts that output-distillation promises to callers.
module DistillationSurface =

    /// The internal role label used by the Host-owned runtime.
    val roleLabel: string

    /// The fixed fast-tier managed identity used for bounded-tail distillation.
    val managedAgentName: string

    /// Distiller is an internal Host-owned runtime.
    val isInternalRuntime: bool

    /// Distiller is not a provider-visible fork or horizon target.
    val canBeForkedOrHorizonTarget: bool

    /// Distiller is a leaf runtime and never receives a Blogger companion.
    val hasBloggerCompanion: bool

    /// Distiller has no execution, mutation, or judgement permissions.
    val permissionLabels: string array

    /// Distillation is invoked by the provider-visible run surface.
    val executionToolName: string

    /// Stable JSON-shaped contract for consumers that need one observation.
    val contract: obj

    /// Distillation prompt contracts rendered through a plain language label.
    val distillFragmentPrompt: language: string -> string

    /// Run fixed-cost tail distillation through a JSON-shaped callback runtime. The adapter keeps
    /// ForkResult, ForkError and RunCompletion representations inside this owner.
    val distillSpool: runtime: obj -> spoolPath: string -> language: string -> Task<string>
