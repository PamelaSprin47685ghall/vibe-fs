namespace Wanxiangshu.Mission.Manager

/// JS-native semantic surface for Finality laws (PR 6 exemplar).
///
/// A JS test starts with `emptyWorld()` and applies each plain lifecycle event
/// through `applyEvent`. `world` is an opaque handle: the production fold runs
/// once per call, and the F# `ProjectionSet` / `LifeProjection` / fact types
/// never cross the boundary (JS-SEMANTIC-SURFACE-002/003/005). The JS test does not own
/// `ManagerLifecycleFact`, `EventEnvelope`, `FSharpList`, or any dist module —
/// it only speaks lifecycle vocabulary and reads JS-shaped answers.
module FinalitySurface =

    /// Create an opaque empty projection capability.
    val emptyWorld: unit -> obj

    /// Apply exactly one JS lifecycle event through the production fold.
    /// Returns `{ ok: true, world }` or `{ ok: false, error }`.
    val applyEvent: world: obj -> event: obj -> obj

    /// The current Life as JS-shaped data. `undefined` when the session has no
    /// open Life (archived after LifeCompleted, or never opened).
    val lifeView: world: obj -> obj

    /// `{ ok: true, life } | { ok: false, error }` — the Life view, or a typed
    /// reason when the world has no open Life.
    val currentLifeView: world: obj -> obj

    /// GLORY-065 archived Lives (CompletedLives), newest first — Lives closed
    /// by LifeCompleted. JS-shaped; empty when none.
    val archivedLivesView: world: obj -> obj array

    /// Interpret one suicide call against the durable Life.
    /// `callId` absent → `undefined`; `hasPlanCommitment` is the typed
    /// obligation-ledger projection (FINALITY-004).
    val classifyEnding: world: obj -> callId: string -> hasPlanCommitment: bool -> obj

    /// FINALITY-026: ordinary Manager labor is deferred only while an open
    /// Finality request owns the Life. `'finality-owns-life' | 'labor-may-continue'`.
    val admitLabor: world: obj -> string

    /// FINALITY-019 / GLORY-029: JS-native projection of the exact Manager idle
    /// occasion identity. Same terminal => same key; fresh ProviderRun => fresh
    /// key even when Life and pre/post-T1 condition are unchanged.
    val managerIdleOccasionKey:
        sessionId: string -> lifeId: string -> conditionKey: string -> providerRun: string -> string

    /// GLORY-070: a Life is archived only by LifeCompleted (CurrentLife cleared
    /// AND CompletedLives non-empty). A fresh session keeps working.
    val isLifeArchived: world: obj -> bool

    /// GLORY-045 roster algebra: ungraduated historical Reviewers + exactly one
    /// new slot, derived from durable facts only. JS-shaped slots.
    val cohortRoster: world: obj -> obj array

    /// GLORY-045 roster algebra from an opaque projection handle: the lifeId /
    /// requestId are plain strings and the answer is JS-shaped slots. The
    /// projection remains inside the FinalitySurface world capability.
    val cohortRosterFromSnapshot: snapshot: obj -> lifeId: string -> requestId: string -> obj array

    /// GLORY-045: a Reviewer graduated iff it has a confirmed witness on one of
    /// the barriers this Life enlisted it on (derived from durable facts).
    val graduatedReviewer: world: obj -> reviewerSessionId: string -> bool

    /// FINALITY-022 ending admission: `{ kind: 'existing-life' }` when a Life is
    /// open, `{ kind: 'initial-agent-owner-migration' }` for a first AgentOwner
    /// ending, `{ kind: 'no-life' }` after terminal closure.
    val endingAdmission:
        world: obj ->
        authorityKind: string ->
        rootMessageId: string ->
        selectedAgent: string ->
        peerAgent: string ->
        tier: string ->
        opening: obj ->
            obj

    /// FINALITY-022 HumanRoot opening: true only for the exact authority-root
    /// physical message; session-level authority never generalizes.
    val tryHumanRootOpening: world: obj -> authorityKind: string -> rootMessageId: string -> messageId: string -> bool

    val reviewerOutcomeKinds: unit -> string array

    val reviewerOutcomeRevision: workRecord: string -> obj

    val reviewerOutcomeConfirmed: reviewerSessionId: string -> barrierId: string -> obj

    /// FINALITY-001: the Finality capability is granted only to Manager. The
    /// role and permission labels are plain strings at this boundary.
    val isAllowed: role: string -> permission: string -> bool

    /// FINALITY-027: durable parent-visible handles are the Manager's only
    /// background obligation. Hidden Reviewer handles stay invisible through
    /// the same HandleProjection.listable rule used by TerminalPolicy.
    val backgroundOutstanding: world: obj -> sessionId: string -> bool

    /// Create an opaque empty ManagerJob projection capability.
    val emptyJobProjection: unit -> obj

    /// Apply exactly one plain ManagerJob event through its owner projection.
    val applyJobProjectionEvent: projection: obj -> event: obj -> obj

    /// Return the JS-native job and active-job views of an opaque projection.
    val jobProjectionView: projection: obj -> obj

    /// FINALITY-002: Project a ConfirmedReviewWitness from cohort member witnesses.
    val projectConfirmedReview: lifeId: string -> requestId: string -> tree: string -> memberWitnesses: obj array -> obj

    val confirmedReviewWitnessTree: witness: obj -> string

    /// FINALITY-002 / FINALITY-016: Blessing authorization gate.
    /// Evaluates currentTree against ConfirmedReviewWitness.
    /// Grants BlessingPermit on match; rejects with StaleWitness on mismatch.
    val grantBlessing: currentTree: string -> witness: obj -> obj
