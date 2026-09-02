namespace Wanxiangshu.Persistence.Journal

open System
open System.Threading.Tasks

/// Review-owned journal adapter. JournalHandle remains the only durable
/// capability; facts cross this boundary as plain family/case/payload values.
/// This keeps review tests off AgentFact unions, Fable option/list helpers, and
/// internal AgentJournal exports while preserving the production fold.
[<RequireQualifiedAccess>]
module ReviewJournalSurface =
    /// Append a generic agent fact and return a boxed result.
    val appendAgent:
        handle: JournalHandle ->
        sessionId: string ->
        providerRun: obj ->
        family: string ->
        caseName: string ->
        payload: obj ->
            Task<obj>

    /// Append a review fact and return a boxed result.
    val appendReview:
        handle: JournalHandle -> sessionId: string -> providerRun: obj -> caseName: string -> payload: obj -> Task<obj>

    /// Append an authority-root-accepted fact for the given session.
    val appendAuthorityRoot: handle: JournalHandle -> sessionId: string -> agent: string -> Task<obj>

    /// Projected review session view as a JS object.
    val sessionView: handle: JournalHandle -> sessionId: string -> obj

    /// Raw review session witness as a JS object.
    val sessionViewRaw: handle: JournalHandle -> sessionId: string -> obj

    /// Current XTrace head cursor sequence for a session.
    val xTraceHead: handle: JournalHandle -> sessionId: string -> int64

    /// Current XTrace part kinds for a session.
    val xTracePartKinds: handle: JournalHandle -> sessionId: string -> string array
