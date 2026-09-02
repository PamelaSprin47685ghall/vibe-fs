namespace Wanxiangshu.Composition.Turn

open System.Threading.Tasks

/// JS boundary for reconciliation's classifiers, evidence, wakes and publish
/// seals. Maps and idle permits remain opaque handles; decisions and
/// observations cross as plain objects or stable strings.
module ReconcileSurface =
    val empty: unit -> obj

    val turnFixture: value: obj -> obj

    /// Stable JS contract for the fields accepted by `turnFixture` and the
    /// publish seal operations. This is an owner-defined vocabulary view, not
    /// Fable reflection metadata.
    val acceptedTurnFields: unit -> string array

    /// Classify a publishable turn from its JS outcome name. `TurnUnknown` is
    /// deliberately rejected by the domain classifier: it is a snapshot
    /// observation, never a business turn.
    val classifyTurn: outcomeName: string -> obj

    val isTerminalOutcome: outcomeName: string -> bool

    /// These predicates keep structural clean-break checks at the owner boundary
    /// without exporting the underlying union constructors or case metadata.
    val isPublishableOutcome: outcomeName: string -> bool

    val isSnapshotObservation: observationName: string -> bool

    val tryOutcome: outcomeName: string -> obj

    val idleWake: session: string -> obj

    val retryWake: unit -> obj

    val failureWake: unit -> obj

    val failureWakeFor: physical: string -> obj

    val abortWake: unit -> obj

    val mergeWakeKind: currentPhysical: string -> previous: obj -> incoming: obj -> string

    val evidenceSnapshotError: reason: string -> obj

    val evidenceNoTurn: unit -> obj

    val evidenceProvisional: outcomeName: string -> obj

    val evidenceUnknown: unit -> obj

    val evidenceTerminal: outcomeName: string -> obj

    val evidenceTerminalFor: physical: string -> outcomeName: string -> obj

    val evidenceSessionCleared: unit -> obj

    val decideStep: wake: obj -> evidence: obj -> obj

    val decisionName: decision: obj -> string

    val consumeKey: turn: obj -> string

    val provisionalHas: maps: obj -> turn: obj -> bool

    val consumedHas: maps: obj -> turn: obj -> bool

    val publishDecision: maps: obj -> turn: obj -> obj

    val clearProvisional: maps: obj -> session: string -> obj

    val unboundFailureScenario: unit -> Task<obj>

    val idleProvisionalWithoutProjectionEdgeScenario: unit -> Task<obj>

    val idleProjectionEdgeScenario: unit -> Task<obj>

    val failureProjectionEdgeScenario: unit -> Task<obj>

    val failureWitnessCurrentAssistantScenario: unit -> Task<obj>
