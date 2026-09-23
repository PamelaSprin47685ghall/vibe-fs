namespace Wanxiangshu.Sphinx.V2.Runtime

open Wanxiangshu.Sphinx.V2.Core

/// The plugin ABI: a manifest bound to an executable capability.
///
/// WHAT[sphinx-v2-009]: a manifest that names a capability without an implementation
/// behind it is a startup failure, not a runtime surprise. The old `BoundPlugin`
/// wrapped a manifest record and nothing else, so a declared capability could be
/// selected by the scheduler and then fail at the moment of dispatch.

type PluginError = { Code: string; Message: string }

/// What a plugin may observe and what it may propose. The context is a read-only
/// snapshot: no network, no filesystem, no clock, no mutable store handle.
type PluginContext =
    { InquiryId: InquiryId
      GoalRevision: Revision
      ScopeId: string
      ConfigHash: string
      /// Deterministic random state handed in; the plugin returns the advanced state.
      RngState: string
      /// Read-only view over the semantic graph the plugin owns.
      Graph: JsonEnvelope option
      /// Read-only budget facts.
      Budget: JsonEnvelope option
      LockedPluginRefs: string list }

/// A proposed semantic change. Nothing here takes effect until the same admission
/// path the Kernel uses has validated it — a plugin is not trusted because it is
/// built in (WHAT[sphinx-v2-018]).
type PluginDelta =
    { GraphPatches: GraphPatch list
      CertificatePatches: CertificateSlotPatch list
      WorkProposals: WorkProposal list
      Invalidations: string list
      Diagnostics: string list }

and WorkProposal =
    { LocalId: string
      PlanId: PlanId
      Capability: string
      Input: JsonEnvelope option
      OutputSchema: SchemaRef option
      Dependencies: string list
      ConflictKeys: string list
      Reserved: Map<string, float>
      RoundId: RoundId option }

type RefinementInput =
    { ObservationRefs: string list
      DirtyTargets: string list
      StepLimit: int }

type PluginResult<'value> = Result<'value, PluginError>

/// One capability, executable. The ABI is deliberately small: initialize, observe,
/// propose, refine. Anything that performs I/O proposes work instead.
type ExecutablePlugin =
    { Manifest: PluginManifest
      Initialize: PluginContext -> PluginResult<PluginDelta>
      Observe: PluginContext -> ResultAcceptedBody -> PluginResult<PluginDelta>
      Propose: PluginContext -> PluginResult<WorkProposal list>
      Refine: PluginContext -> RefinementInput -> PluginResult<PluginDelta> }

and PluginManifest =
    { Id: string
      Release: string
      /// Hash of the executable artifact, not a version label.
      ImplementationHash: string
      AbiHash: string
      Capabilities: Set<string>
      Dependencies: Set<string>
      Schemas: Map<string, SchemaRef> }

module PluginContract =

    /// A capability must be a non-blank capability name; the ABI hash ties the
    /// declaration to the signature it was compiled against.
    let validateManifest (manifest: PluginManifest) : PluginResult<unit> =
        if System.String.IsNullOrWhiteSpace manifest.Id then
            Error { Code = "invalid-manifest"; Message = "plugin id must not be blank" }
        elif System.String.IsNullOrWhiteSpace manifest.Release then
            Error { Code = "invalid-manifest"; Message = "plugin release must not be blank" }
        elif System.String.IsNullOrWhiteSpace manifest.ImplementationHash then
            Error
                { Code = "invalid-manifest"
                  Message = "plugin implementation hash must not be blank" }
        elif System.String.IsNullOrWhiteSpace manifest.AbiHash then
            Error { Code = "invalid-manifest"; Message = "plugin abi hash must not be blank" }
        else
            Ok()
