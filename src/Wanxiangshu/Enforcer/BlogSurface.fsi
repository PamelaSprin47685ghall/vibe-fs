namespace Wanxiangshu.Enforcer

/// JS-native owner boundary for the Blogger/chronicle contract and recovery
/// evidence. It exposes semantic outcomes only; Host tool records, journal
/// facts and typed identities stay private.
[<RequireQualifiedAccess>]
module BlogSurface =

    val emptyTextError: string
    val noLiveCycleError: string

    /// Chronicle's canonical text gate.
    val canonicalText: value: obj -> obj

    /// Physical Blogger flight is the only live-cycle authority.
    val hasLiveCycle: hasFlight: bool -> _sessionId: string -> bool

    /// Pure semantic execute decision for the chronicle owner. The real Host
    /// supplies the physical abort; this boundary returns the exact observable
    /// consequence so tests do not construct ToolSpec/HostToolContext values.
    val execute: value: obj -> obj

    val tipFieldNames: unit -> string array

    /// Drive the real transform repair entry: observed terminal/tool facts in,
    /// coordinator verdict out. The exact live request and terminal run cross
    /// explicitly; `rawMessages` is the plain Host transcript.
    val observeTransformRepair:
        scope: obj ->
        journal: obj ->
        request: obj ->
        terminalRun: string ->
        rawMessages: obj ->
            System.Threading.Tasks.Task<obj>

    /// Drive the real idle repair entry. The observation states quiescence
    /// explicitly (`quiescent: bool`): when
    /// quiescent the surface begins the exact provider attempt on a real
    /// SessionQuiescenceGate, observes its idle, and places the resulting
    /// permit in the reconciled turn; otherwise the turn carries no permit.
    /// Session, workspace and event ports arrive as plain JS stubs over
    /// primitive identity strings; only their exercised calls take effect.
    /// The run is read from the turn itself.
    val observeIdleRepair:
        scope: obj -> journal: obj -> request: obj -> observation: obj -> System.Threading.Tasks.Task<obj>

    /// Durable repair-claim facts read through the production probe. No stage
    /// is derived here; the coordinator owns repair sequencing.
    val repairClaimedForKind:
        journal: obj ->
        bloggerSessionId: string ->
        requestId: string ->
        terminalRun: string ->
        repairKind: string ->
            bool

    val repairIssuedForKind:
        journal: obj ->
        bloggerSessionId: string ->
        requestId: string ->
        terminalRun: string ->
        repairKind: string ->
            bool

    /// Serialize the two observation facts with the production FactCodec.
    val serializeFact: value: obj -> string

    /// Decode a fact line and expose only its normalized bytes and semantic case.
    val deserializeFact: line: string -> obj

    val containsLegacyScoreVectorEntry: line: string -> bool

    val tipV2CleanBreakMessage: string

    val serializeEnvelope: value: obj -> string

    val deserializeEnvelope: line: string -> obj

    val serializeObservationFact: value: obj -> string
    val deserializeObservationFact: line: string -> obj

    /// Build the complete Blogger projection plan from semantic frame/tip
    /// inputs. The builder retains pairing, physical-delta ordering and
    /// squash instruction placement behind the Blog owner boundary.
    val buildProjectionPlan: value: obj -> obj

    /// Blog-part status predicates used by continuation repair. The result is
    /// deliberately named and boolean rather than exposing a status DU.
    val classifyPart: part: obj -> obj

    /// Coverage birth guard: sequence and cutoff advance together with the
    /// first durable frame; no synthetic zero/zero coverage is accepted.
    val coverageBirth: value: obj -> obj

    /// Commit branch classification over semantic evidence. Each branch keeps
    /// the production failure meaning visible without leaking a Cycle DU.
    val classifyCommit: value: obj -> obj

    /// Protocol transition for one terminal assistant step.
    val protocol: value: obj -> obj
