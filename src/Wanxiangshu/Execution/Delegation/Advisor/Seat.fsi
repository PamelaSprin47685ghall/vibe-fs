namespace Wanxiangshu.Execution.Delegation.Advisor

open System.Threading.Tasks
open Wanxiangshu.OpenCode
open Wanxiangshu.Persistence.Journal

/// advisor-[001]: the caller-supplied physical handles one consult runs on.
///
/// `Sessions` owns the advisor child and the terminal subscription, `Journal`/`Snapshot` are the
/// read-back capabilities, `Directory`/`Participant` are the child's dispatch coordinates.
type AdvisorSeatDeps =
    { Sessions: ISessionHostPort
      Snapshot: ISessionSnapshotPort option
      Journal: AgentJournal option
      Directory: string option
      Participant: string }

/// advisor-[001]/[002]/[004]: one caller session's dedicated advisor seat.
///
/// The seat is process-live: it owns no durable fact, and a restart simply forgets it — the next
/// consult creates a fresh advisor child. Every function is `Result`-valued on the consult path: a
/// consult that did not reach prose refuses, and advice is never fabricated from an error path.
[<RequireQualifiedAccess>]
module AdvisorSeat =

    /// advisor-[002]/[003]: append this request's view and intent to the caller session's advisor
    /// as two separate prompts, then return the advisor's prose.
    ///
    /// The first consult creates the seat's advisor child; every later consult reuses it. The view
    /// prompt and the intent are never concatenated. `Ok` carries prose the advisor actually
    /// produced, and a missing terminal, a failed/aborted terminal, or an empty read-back is
    /// `Error "advisor did not answer"` (advisor-[004]).
    ///
    /// advisor-[030]: each consult is fenced to the terminal of its own intent prompt. A Completed
    /// from an older provider run — a sticky replayed terminal or the view prompt finishing first —
    /// never settles the consult, and the recorded fence advances so a later consult cannot accept
    /// an earlier consult's answer either.
    val consult:
        deps: AdvisorSeatDeps ->
        callerSessionId: string ->
        intent: string ->
        viewPrompt: string ->
            Task<Result<string, string>>

    /// Drop one caller session's seat (tests + session close). The advisor child itself is left to
    /// the Host's own session teardown: the seat owns no durable link to close.
    val clear: callerSessionId: string -> unit

    /// Drop every process-live seat.
    val clearAll: unit -> unit

    /// advisor-[030] test/dev hook: drop every recorded consult fence.
    val clearConsultFences: unit -> unit
