namespace Wanxiangshu.Strength

open System.Threading.Tasks

/// JS-native owner surface for Strength semantics.
///
/// Strength records, unions, identities, collections and live registries remain
/// private to their owners. Tests cross this module with JSON-shaped values and
/// opaque handles only; Fable representation is never a contract.
module StrengthSurface =

    /// Apply Strength's native completed-tool Host adaptation to rendered rows.
    val tryApplyRenderedMessages: sessionId: string -> sha256: (string -> string) -> rendered: obj -> obj

    val projectionMirror: value: obj -> obj

    val candidate: sha256: (string -> string) -> value: obj -> obj

    val promoted: sha256: (string -> string) -> value: obj -> obj

    val replicaLocal: sha256: (string -> string) -> value: obj -> obj

    /// Build one deterministic frame bundle from plain request batches.
    val frameTryBuild: sha256: (string -> string) -> maxBytes: int -> batches: obj array -> obj

    /// Localize owner wire ids into decision-local ids without changing semantics.
    val frameTryLocalizeMirror:
        sha256: (string -> string) -> decisionId: string -> semanticDigest: string -> messages: obj array -> obj

    val frameWireToolCallId:
        sha256: (string -> string) ->
        ownerSessionId: string ->
        decisionId: string ->
        requestOrdinal: int ->
        exchangeOrdinal: int ->
        semanticDigest: string ->
            string

    val collectCompleteBatches: messages: obj array -> obj array

    val renderWire: messages: obj array -> string

    val renderSemantic: messages: obj array -> string

    val costEstimate:
        p1: float ->
        p2: float ->
        savedDeep1: float ->
        savedDeep2: float ->
        fast1: float ->
        fast2: float ->
        byte1: float ->
        byte2: float ->
        delay1: float ->
        delay2: float ->
        risk1: float ->
        risk2: float ->
            obj

    val policyDecide:
        opportunity: obj -> control: bool -> shadow: bool -> prediction: obj -> estimate: obj -> config: obj -> obj

    val policyControlBucket:
        sha256: (string -> string) -> policyVersion: string -> authorityRoot: string -> targetRun: string -> int

    val policyIsControlHoldout: rateBasisPoints: int -> bucket: int -> bool

    val readonlyCapabilities: role: string -> requestKind: string -> string array

    /// StrengthReplica readonly capability labels for a canonical role.
    /// Kept as the short owner name consumed by policy and authority laws.
    val capabilities: role: string -> string array

    val readonlyCapabilitiesResult: role: string -> requestKind: string -> obj

    val exactReadonlyHostToolMap: obj array

    val isAllowedTool: tool: string -> bool

    /// Prompt identity remains role-owned and cannot inherit Strength metadata.
    val systemPromptIdForRole: role: string -> string

    val systemPromptForRole: role: string -> string

    val clearsFailureCountOnSuccess: requestKind: string -> bool

    val mayCarryProbe: requestKind: string -> bool

    val associationFacts: ownerSessionId: string -> obj

    val commitResolvePrepared: appendOutcome: string -> evidence: string -> string

    val commitResolvePromotion: appendOutcome: string -> evidence: string -> string

    val promotionDecide: targetRun: string -> observedRun: string -> evidence: string -> string

    val eventPrepared:
        owner: string ->
        decision: string ->
        target: string ->
        replica: string ->
        budget: string ->
        anchor: string ->
        digest: string ->
        byteLength: int ->
        refs: string array ->
            obj

    val eventPromoted:
        owner: string -> decision: string -> target: string -> digest: string -> refs: string array -> obj

    val eventTraced: decision: string -> startInclusive: int64 -> endExclusive: int64 -> obj

    val eventAbandoned: decision: string -> target: string -> obj

    val eventType: value: obj -> string

    val eventView: value: obj -> obj

    val projectionEmpty: unit -> obj

    val projectionApply: projection: obj -> event: obj -> obj

    val projectionHasPrepared: decision: string -> projection: obj -> bool

    val projectionIsPromoted: decision: string -> projection: obj -> bool

    val projectionDecisionForTarget: target: string -> projection: obj -> string

    val projectionCandidate: decision: string -> projection: obj -> obj

    val projectionTraceRange: decision: string -> projection: obj -> obj

    val storeToEnvelope: sha256: (string -> string) -> event: obj -> obj

    val envelopeView: value: obj -> obj

    val storeTryDecodeEnvelope: value: obj -> obj

    val storeAppend: store: obj -> sha256: (string -> string) -> event: obj -> Task<obj>

    val storeWritePayload: store: obj -> bytes: byte array -> Task<obj>

    val storeReadPayload: store: obj -> reference: string -> Task<obj>

    val storeCurrent: store: obj -> obj

    val durabilityCreate: store: obj -> obj

    val durabilityPublishPrepared: durability: obj -> request: obj -> Task<obj>

    val durabilityLoadProjection: durability: obj -> Task<obj>

    val durabilityLoadBundleForDecision: durability: obj -> projection: obj -> decision: string -> Task<obj>

    val durabilityAppend: durability: obj -> event: obj -> Task<obj>

    val traceExpectedParts: bundle: obj -> obj array

    val traceRecoverRange: bundle: obj -> observed: obj array -> obj

    val turnEvidenceClassify: parts: obj array -> obj

    val lifecycleReconcileEvent: projection: obj -> turn: obj -> obj

    val lifecycleReconcileHandle: projection: obj -> turn: obj -> obj

    val lifecycleReplayPlans: owner: string -> messages: obj array -> bundle: obj -> projection: obj -> Task<obj>

    val lifecycleReplayPlansObserved:
        owner: string -> messages: obj array -> loadResponses: obj array -> projection: obj -> Task<obj>

    val lifecycleNeedsRawReplay: coveredThrough: obj -> plan: obj -> bool

    val lifecycleReplayIntents: sha256: (string -> string) -> plans: obj array -> obj

    val predictorCreate: unit -> obj

    val predictorFeature: role: string -> recent: string array -> visibleBytes: int -> obj

    val predictorObserveFirst: state: obj -> feature: obj -> symbol: string -> obj

    val predictorObserveSecond: state: obj -> feature: obj -> symbol: string -> obj

    val predictorBucket: state: obj -> feature: obj -> obj

    val predictorPredict: state: obj -> feature: obj -> obj

    val rolloutEstimate: prediction: obj -> costs: obj -> obj

    val rolloutIsShadow: mode: string -> bool

    val settingsLoad: unit -> obj

    val settingsDryRunBudget: unit -> string

    val settingsHostCanaryHealthy: unit -> bool

    val settingsHostCanaryFingerprint: string

    val scopeCreate: unit -> obj

    val scopeFuseReason: scope: obj -> string

    val scopeTripFuse: scope: obj -> reason: string -> unit

    val scopeClearSession: scope: obj -> session: string -> unit

    val scopeDispose: scope: obj -> unit

    val runtimeCreate: unit -> obj

    val runtimeBinding:
        owner: string ->
        replica: string ->
        decision: string ->
        target: string ->
        role: string ->
        budget: string ->
        maxFrameBytes: int ->
        semanticDigest: string ->
        localizedMirrorMessages: obj array ->
            obj

    val runtimeRegister: runtime: obj -> binding: obj -> obj

    val runtimeFindByReplica: runtime: obj -> replica: string -> obj

    val runtimeRetire: runtime: obj -> replica: string -> obj

    val transformApply: sha256: (string -> string) -> runtime: obj -> output: obj -> Task<obj>
