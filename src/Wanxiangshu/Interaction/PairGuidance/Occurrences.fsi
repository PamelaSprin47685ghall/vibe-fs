namespace Wanxiangshu.Interaction.PairGuidance

open System.Threading.Tasks
open Wanxiangshu.Host
open Wanxiangshu.Persistence.Journal

/// HOST-013 occurrence owner. It decides whether a request carries a new
/// pair-guidance occurrence, and in which order the occurrence is rendered
/// and recorded. Hosts only decode their transcript and render the result.
[<RequireQualifiedAccess>]
module PairGuidanceOccurrences =
    /// The marker's source identity. Filtering uses this, never the text:
    /// a real user may quote the sentence.
    val source: string

    /// CallId = digest(transcript + source + ordinal). Stable across restarts.
    val stableCallId: sessionId: string option -> ordinal: int64 -> string

    /// Emergency fuse only (`WANXIANGSHU_SKIP_AUTO_INJECTED=1`): replay, never add.
    val skipRequested: unit -> bool

    /// The durable ledger when a journal and a session exist; otherwise the
    /// process-local ledger keyed by transcript identity (tests, no workspace).
    val ledger: journal: AgentJournal option -> sessionId: string option -> PairGuidanceLedger

    /// Companion sessions and sessions of an internal role get no pair guidance.
    val isInternalSession: journal: AgentJournal option -> sessionId: string option -> bool

    /// HOST-013 commit order (fail closed):
    /// 1. every synthetic message on the transcript has a durable record;
    /// 2. internal transcripts and carrier-less requests only replay;
    /// 3. a placement that already has an occurrence only replays;
    /// 4. otherwise the next ordinal is rendered first, then appended;
    ///    an append failure fails the request.
    /// The flag is true when a new occurrence was recorded.
    val occur:
        ledger: PairGuidanceLedger ->
        sessionId: string option ->
        internalSession: bool ->
        markerText: string ->
        transcript: PairGuidanceTranscript ->
        render: (PairGuidanceOccurrence list -> Result<'Wire, string>) ->
            Task<Result<'Wire * bool, string>>
