namespace Wanxiangshu.Strength

open System.Threading.Tasks

/// JS-native owner surface for Strength semantics.
///
/// Strength records, unions, identities, collections and live registries remain
/// private to their owners. Tests cross this module with JSON-shaped values and
/// opaque handles only; Fable representation is never a contract.
///
/// The surface describes the readonly-delegation contract only: an explicit
/// integer authorization from the owner model, one real execution, and honest
/// durable facts. Predictors, cost models, control holdouts, rollout modes,
/// tier budgets, byte ceilings and production DryRun have no exports here.
module StrengthSurface =

    /// Apply Strength's native completed-tool Host adaptation to rendered rows.
    val tryApplyRenderedMessages: sessionId: string -> sha256: (string -> string) -> rendered: obj -> obj

    val projectionMirror: value: obj -> obj

    val candidate: sha256: (string -> string) -> value: obj -> obj

    val promoted: sha256: (string -> string) -> value: obj -> obj

    val replicaLocal: sha256: (string -> string) -> value: obj -> obj

    /// Build one deterministic frame bundle from plain request batches. No
    /// Delegate-specific byte ceiling; integrity is enforced inside tryBuild.
    val frameTryBuild: sha256: (string -> string) -> batches: obj array -> obj

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

    val readonlyCapabilities: role: string -> requestKind: string -> string array

    /// StrengthReplica readonly capability labels for a canonical role.
    val capabilities: role: string -> string array

    val readonlyCapabilitiesResult: role: string -> requestKind: string -> obj

    val exactReadonlyHostToolMap: obj array

    val isAllowedTool: tool: string -> bool

    val isProjectionTool: tool: string -> bool

    /// Prompt identity remains role-owned and cannot inherit Strength metadata.
    val systemPromptIdForRole: role: string -> string

    val systemPromptForRole: role: string -> string

    val clearsFailureCountOnSuccess: requestKind: string -> bool

    val mayCarryProbe: requestKind: string -> bool

    val associationFacts: ownerSessionId: string -> obj

    val commitResolvePrepared: appendOutcome: string -> evidence: string -> string

    val commitResolvePromotion: appendOutcome: string -> evidence: string -> string

    val promotionDecide: targetRun: string -> observedRun: string -> evidence: string -> string

    // Drive-only events and projection, all under the new vocabulary.

    val eventRequested: value: obj -> obj

    val eventBound: decision: string -> target: string -> replica: string -> anchorDigest: string -> obj

    val eventClosed: decision: string -> closedFrom: string -> reason: string -> obj

    val eventHistoryImported:
        decision: string ->
        sourceStreamId: string ->
        sourceEventId: string ->
        importId: string ->
        oldBudgetEvidence: string ->
        outcome: obj ->
            obj

    val eventPrepared:
        owner: string ->
        decision: string ->
        target: string ->
        replica: string ->
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

    /// Folded view of one decision: immutable request, legal attachments and
    /// the closed lifecycle state. Never boolean combinations.
    val projectionCandidate: decision: string -> projection: obj -> obj

    val projectionCandidateBySource:
        ownerSessionId: string ->
        logicalRunId: string ->
        authorityRootUserMessageId: string ->
        sourcePhysicalUserMessageId: string ->
        sourceProviderRun: string ->
        projection: obj ->
            obj

    val projectionRequestedRoundsBySource:
        ownerSessionId: string ->
        logicalRunId: string ->
        authorityRootUserMessageId: string ->
        sourcePhysicalUserMessageId: string ->
        sourceProviderRun: string ->
        projection: obj ->
            obj

    /// Read the requested rounds from the immutable projection.
    val projectionRequestedRounds: decision: string -> projection: obj -> obj

    val projectionTraceRange: decision: string -> projection: obj -> obj

    /// Evidence-only imported history, keyed by import identity (DELEGATE-015):
    /// the folded material keeps its causal position, digest and trace coverage
    /// as evidence, and never yields a runnable delegation.
    val projectionImported: importId: string -> projection: obj -> obj

    // Speculative-investigation estimate protocol (WHAT[016]): the revision
    // constant, the one per-tool classifier, and the strict pairing parser.

    /// The code-level protocol revision; never read from configuration.
    val protocolRevision: int

    /// The single classification source, answered as its stable policy code
    /// so no union instance crosses into JS.
    val classifyTool: toolName: string -> string

    /// `{ ok = true; rounds; selfNote }` or `{ ok = false; error }`. The
    /// Result, its struct tuple and its option stay on the F# side.
    val parseParticipatingArguments: arguments: obj -> obj

    /// WHAT[016] §6: the stable machine code answered as its natural-language
    /// explanation in the requested language (§3-§5 own the per-error rules;
    /// §6 keeps the code vocabulary and the prose apart). `lang` follows
    /// `ProviderLanguage.parse` and the code resolves through the contract's
    /// own reverse projection, so neither is re-decoded here.
    val investigationArgumentErrorText: lang: string -> errorCode: string -> string

    // Budget: one plain non-negative integer chosen by the owner model.

    val budgetTryCreate: value: int -> obj

    /// Collapse one batch of the owner's integers to its maximum.
    val budgetMaxOf: values: int array -> obj

    // Admission: evidence in, decision out. No economic or statistical input.

    val policyEligibility: opportunity: obj -> obj

    val policyDecide: sha256: (string -> string) -> opportunity: obj -> obj

    // Authorization lifecycle transitions with illegal edges refused.

    val delegationDeriveDecisionId:
        sha256: (string -> string) ->
        contractRevision: int ->
        logicalRunId: string ->
        authorityRootUserMessageId: string ->
        sourceProviderRun: string ->
            string

    val delegationRequest: value: obj -> obj

    val delegationBind: lifecycle: obj -> binding: obj -> obj

    val delegationPrepare: lifecycle: obj -> obj

    val delegationPromote: lifecycle: obj -> obj

    val delegationTrace: lifecycle: obj -> obj

    val delegationClose: lifecycle: obj -> closed: obj -> obj

    val delegationAbandon: lifecycle: obj -> obj

    val delegationDecisionId: lifecycle: obj -> string

    val storeToEnvelope: sha256: (string -> string) -> event: obj -> obj

    val envelopeView: value: obj -> obj

    val storeTryDecodeEnvelope: value: obj -> obj

    // Offline migration of pre-delegation Strength history (DELEGATE-015).
    // Boundary shapes are JS-native views; the classifier and the planner stay
    // inside the Migration module.

    val migrationClassifyEnvelope: eventType: string -> payloadJson: string -> obj

    val migrationReadLegacyEnvelope: envelopeJson: string -> obj

    val migrationPlanDecision: sha256: (string -> string) -> contractRevision: int -> envelopes: obj array -> obj

    val migrationImportEvent: sha256: (string -> string) -> value: obj -> obj

    val storeAppend: store: obj -> sha256: (string -> string) -> event: obj -> Task<obj>

    val storeWritePayload: store: obj -> bytes: byte array -> Task<obj>

    val storeReadPayload: store: obj -> reference: string -> Task<obj>

    val storeCurrent: store: obj -> obj

    val durabilityCreate: store: obj -> obj

    val durabilityLoadProjection: durability: obj -> Task<obj>

    val durabilityLoadBundleForDecision: durability: obj -> projection: obj -> decision: string -> Task<obj>

    val durabilityAppend: durability: obj -> event: obj -> Task<obj>

    val durabilityPublishPrepared: durability: obj -> request: obj -> Task<obj>

    val traceExpectedParts: bundle: obj -> obj array

    val traceRecoverRange: bundle: obj -> observed: obj array -> obj

    val turnEvidenceClassify: parts: obj array -> obj

    val lifecycleReconcileEvent: projection: obj -> turn: obj -> obj

    val lifecycleReconcileHandle: projection: obj -> turn: obj -> obj

    val lifecycleReplayPlans: owner: string -> messages: obj array -> bundle: obj -> projection: obj -> Task<obj>

    val lifecycleReplayPlansObserved:
        owner: string -> messages: obj array -> loadResponses: obj array -> projection: obj -> Task<obj>

    val lifecycleNeedsRawReplay: coveredThrough: obj -> plan: obj -> bool

    val lifecycleReplayIntents: sha256: (string -> string) -> plans: obj array -> ownerRole: string -> obj

    val scopeCreate: unit -> obj

    val scopeAcquireShared: key: string -> obj

    val scopeReleaseShared: scope: obj -> unit

    val scopeFuseReason: scope: obj -> string

    val scopeTripFuse: scope: obj -> reason: string -> unit

    val scopeClearSession: scope: obj -> session: string -> unit

    val scopeDispose: scope: obj -> unit

    val scopeRuntimeRegister: scope: obj -> binding: obj -> obj

    val scopeRuntimeFindByReplica: scope: obj -> replica: string -> obj

    val runtimeCreate: unit -> obj

    val runtimeBinding:
        owner: string ->
        replica: string ->
        decision: string ->
        target: string ->
        role: string ->
        requestedRounds: int ->
        semanticDigest: string ->
        localizedMirrorMessages: obj array ->
            obj

    val runtimeRegister: runtime: obj -> binding: obj -> obj

    val runtimeFindByReplica: runtime: obj -> replica: string -> obj

    val runtimeRetire: runtime: obj -> replica: string -> obj

    /// Mirror one outbound request. `outboundRequest` marks a real provider
    /// request boundary; the live registry owns the admission verdict.
    val transformApply: sha256: (string -> string) -> runtime: obj -> output: obj -> outboundRequest: bool -> Task<obj>

    val replicaRuntimeCreate: unit -> obj

    val replicaPreparationCreate: owner: string -> role: string -> eventPort: obj -> ports: obj -> obj

    val replicaPrepare: handle: obj -> request: obj -> Task<obj>

    val replicaSendPrepared: handle: obj -> replica: string -> Task<obj>

    val replicaDecisionOutcome: handle: obj -> replica: string -> decision: string -> obj

    val replicaReleaseDecisionOutcome: handle: obj -> decision: string -> unit

    /// Attach an already-live binding to the real coordinator.
    val replicaAttach: handle: obj -> binding: obj -> obj

    val replicaLiveRegister: handle: obj -> binding: obj -> obj

    val replicaLiveFind: handle: obj -> replica: string -> obj

    val replicaAwaitOutcome: completion: obj -> Task<obj>

    val replicaHandleTurn: handle: obj -> turn: obj -> obj

    val replicaHandleTransform: handle: obj -> output: obj -> Task<obj>

    val replicaSessionDeleted: handle: obj -> session: string -> unit

    val replicaCancelOwner: handle: obj -> owner: string -> Task

    /// Live admission book: real admitted request count, completed batches and
    /// the first immutable semantic terminal (DELEGATE-5.3).
    val replicaPeek: handle: obj -> replica: string -> obj

    val replicaIsReplica: handle: obj -> session: string -> bool

    val replicaDispose: handle: obj -> unit

    val replicaAborted: handle: obj -> string array

    val replicaReleased: handle: obj -> string array

    val TwinBijectionSurface_restore: child: obj array -> owner: obj array -> obj array

    val TwinBijectionSurface_preservesOwnerOrder: child: obj array -> owner: obj array -> bool

    val TwinBijectionSurface_introducesNothing: child: obj array -> owner: obj array -> bool

    val TwinBijectionSurface_dropsNoSpeech: child: obj array -> owner: obj array -> bool

    val TwinBijectionSurface_extensionIsPrefix:
        childBefore: obj array -> ownerBefore: obj array -> childAfter: obj array -> ownerAfter: obj array -> bool
