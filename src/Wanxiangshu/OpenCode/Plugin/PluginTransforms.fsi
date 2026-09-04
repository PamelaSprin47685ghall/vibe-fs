namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Replica

module PluginTransforms =

    type TraceTransformCapture =
        { RawMessages: obj list
          Current: XTraceProjectionState option }

    type NormalTransformCapabilities =
        { BeginPhysicalProviderAttempt: string option -> obj -> Task<unit>
          BindSessionStartedAt: string option -> Task<DateTimeOffset option>
          ApplyStrengthReplay: string option -> obj -> Task<StrengthReplayPlan list>
          ApplyRelayProjection: string option -> obj -> Task<unit>
          CaptureXTraceMessages: string option -> obj -> Task<TraceTransformCapture>
          CommitStrengthTrace: string option -> XTraceProjectionState option -> StrengthReplayPlan list -> Task<unit>
          RefreshCompanionXTrace: string option -> XTraceProjectionState option -> unit
          ApplyCompanion: string option -> obj -> obj -> Task<unit>
          ApplyXWire: obj -> Task<PrefixPresentationHorizon>
          FreezeProviderAttemptPlan: string option -> obj -> Task<unit>
          ApplyEnforcerContinuation: string option -> obj -> Task<unit>
          ApplyStrengthSpeculate: obj -> Task<unit>
          InjectPairGuideline: string option -> DateTimeOffset option -> obj -> Task<unit>
          ProjectRequirementGrounding: string option -> obj -> Task<unit>
          InjectBloggerChronicle: string option -> obj -> unit
          SanitizeMessages: obj -> unit }

    type TransformBranchCapabilities =
        { IsExplicitResume: string option -> obj -> bool
          RegisterOwned: string -> unit
          ReplicaRuntime: string option -> StrengthReplicaRuntime option
          ReplicaXWire: obj -> Task<unit>
          ReplicaSanitize: obj -> unit
          ExplicitResumeSanitize: obj -> unit }

    val defaultCapabilities: boot: PluginBoot.Boot -> host: PluginHostWiring.Host -> NormalTransformCapabilities

    val defaultBranchCapabilities: boot: PluginBoot.Boot -> host: PluginHostWiring.Host -> TransformBranchCapabilities

    val normalTransform:
        caps: NormalTransformCapabilities ->
        projectionSessionIdOpt: string option ->
        inObj: obj ->
        outObj: obj ->
            Task<unit>

    val createWithCaps:
        caps: NormalTransformCapabilities -> branches: TransformBranchCapabilities -> (obj -> obj -> Task<unit>)

    /// Provider-facing transform composition: order only.
    /// Relay cut → Strength replay/trace → Companion/XWire → speculation;
    /// retired raw history is removed before any downstream context owner.
    val create: boot: PluginBoot.Boot -> host: PluginHostWiring.Host -> (obj -> obj -> Task<unit>)
