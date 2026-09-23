namespace Wanxiangshu.Sphinx.V2.Runtime

open Wanxiangshu.Sphinx.V2.Core

type PluginError = { Code: string; Message: string }

type PluginContext =
    {
        InquiryId: InquiryId
        GoalRevision: Revision
        ScopeId: string
        ConfigHash: string
        /// Deterministic random state handed in; the plugin returns the advanced state.
        RngState: string
        /// Read-only view over the semantic graph the plugin owns.
        Graph: JsonEnvelope option
        /// Read-only budget facts.
        Budget: JsonEnvelope option
        LockedPluginRefs: string list
    }

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

type ExecutablePlugin =
    { Manifest: PluginManifest
      Initialize: PluginContext -> PluginResult<PluginDelta>
      Observe: PluginContext -> ResultAcceptedBody -> PluginResult<PluginDelta>
      Propose: PluginContext -> PluginResult<WorkProposal list>
      Refine: PluginContext -> RefinementInput -> PluginResult<PluginDelta> }

and PluginManifest =
    {
        Id: string
        Release: string
        /// Hash of the executable artifact, not a version label.
        ImplementationHash: string
        AbiHash: string
        Capabilities: Set<string>
        Dependencies: Set<string>
        Schemas: Map<string, SchemaRef>
    }

module PluginContract =
    val validateManifest: PluginManifest -> PluginResult<unit>
