namespace Wanxiangshu.Enforcer

/// JS-native owner boundary for the Blogger/chronicle contract and recovery
/// evidence. It exposes semantic outcomes only; Host tool records, journal
/// facts, typed identities and BloggerToolRecovery stay private.
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

    /// Rejudge transcript evidence. One completed chronicle only proves
    /// recovery; any other terminal is still the nudge stage.
    val rejudgeFromEvidence: claimedRun: obj -> terminals: obj array -> obj

    /// Rejudge named chronicle tool-part evidence from a compact semantic
    /// transcript. `chronicleCount` counts raw named calls, while
    /// `completedChronicleCount` proves exactly-one completion.
    val rejudgeChronicleEvidence: claimedRun: obj -> terminals: obj array -> obj

    /// Compact request-scoped recovery evidence. A claim is active only when
    /// its request matches and it was not abandoned; an older request cannot
    /// consume a new request's repair budget.
    val repairState: value: obj -> obj

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

    /// Bounded repair transition. A pure terminal first receives one nudge;
    /// subsequent different invalid terminals stay in AABB until the shared
    /// provider fallback budget is actually exhausted.
    val repairProtocol: value: obj -> obj
