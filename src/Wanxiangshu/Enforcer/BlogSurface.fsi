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

    /// Read a real durable blob and use the production recovery decoder.
    val reloadRequest: journal: obj -> value: obj -> System.Threading.Tasks.Task<obj>

    val continueTerminal:
        scope: obj ->
        journal: obj ->
        request: obj ->
        messageId: string ->
        callCount: int ->
        rawMessages: obj ->
            System.Threading.Tasks.Task<obj>

    /// Drive the real stop boundary: the admission barrier lands before the
    /// detached physical abort is requested. `terminate` is the Host
    /// termination capability as `sessionId -> reason -> Promise`.
    val applyPhysicalStop:
        terminate: obj -> sessionId: string -> physicalUserMessageId: string -> reason: string -> unit

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

    /// Coverage birth guard: sequence and cutoff advance together with the
    /// first durable frame; no synthetic zero/zero coverage is accepted.
    val coverageBirth: value: obj -> obj

    val decodeCycle: messages: obj array -> obj
