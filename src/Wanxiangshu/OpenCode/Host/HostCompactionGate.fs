namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation.HostSession

open Wanxiangshu.Persistence.Journal.JournalOutcome
open Wanxiangshu.Foundation
open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Persistence.Journal

/// HOST-006 Host adapter: force the compaction settings off, refuse the compaction
/// hooks, and turn an observed pseudo-run into one `ContextReanchored`.
///
/// The decisions live in `Domain.HostCompactionPolicy`; this module only reads and
/// writes Host objects and the journal. `HostCompactionPolicy` is what a layer-1 test
/// exercises, which is why the judgement is not inlined here (verification-system-001 Pure laws).
module HostCompactionGate =

    /// HOST-007 diagnostic. All emits go through `Diagnostic.emit` (CTX-014
    /// whitelist); there is deliberately no raw `console.warn` here.
    let private logWarn = Diagnostic.emit

    /// HOST-007 diagnostic for a reanchor that could not be appended.
    ///
    /// A failure here is deliberately not fatal to the turn that just completed.
    /// PERSIST-003 already owns the poisoned-journal path; what the reconcile loop must
    /// not do is throw and leave its `Running` latch set, which would silence every
    /// later pass for that session.
    ///
    /// The consequence of a missed reanchor is bounded and self-correcting: the
    /// compaction message stays in the transcript, so the next pass observes it again.
    let logReanchorFailure (sessionId: SessionId) (reason: string) : unit =
        logWarn "reanchor_failed" [ "session_id", SessionId.value sessionId; "result", reason ]

    /// Append the one `ContextReanchored` line a proposed candidate describes.
    let private appendReanchor
        (journal: AgentJournal)
        (sessionId: SessionId)
        (candidate: PrefixReanchorCandidate)
        : Task<Result<ProviderRunIdentity option, string>> =
        task {
            let run = candidate.CompactionRun

            let fact =
                ContextFact.ContextReanchored
                    {| SessionId = sessionId
                       PreviousEpochId = candidate.PreviousEpochId
                       NextEpochId = candidate.NextEpochId
                       ObservedCompactionRun = run |}

            let! appended = AgentJournal.appendAgent (StreamId.Session sessionId) (Some run) fact journal

            return
                appended
                |> Result.map (fun _ -> Some run)
                |> Result.mapError JournalAppendFailure.describe
        }

    /// HOST-006 containment: write one `ContextReanchored` for an observed pseudo-run.
    ///
    /// `observed` is every compaction run in the session's snapshot, oldest first.
    /// Which one to act on — and whether any is left — is the prefix owner's
    /// candidate (`PrefixEpochProjection.proposeCompaction`, wanxiangli/prefix); this
    /// function supplies the journal's committed state and does the append.
    ///
    /// Idempotency has two independent guards, deliberately. Here, the predicate skips
    /// a run the journal already reanchored. At the fold, `PrefixEpochProjection`
    /// refuses a stale epoch. The second is what survives a crash between the decision
    /// and the append.
    let reanchorObserved
        (journal: AgentJournal)
        (sessionId: SessionId)
        (observed: ProviderRunIdentity list)
        : Task<Result<ProviderRunIdentity option, string>> =
        task {
            let snapshot = AgentJournal.snapshot journal

            let epoch =
                AgentProjection.tryFind sessionId snapshot.AgentProjections
                |> Option.bind (fun session -> session.PrefixEpoch)
                |> Option.defaultValue PrefixEpochProjection.empty

            // "Already reanchored" is answered from the durable projection, not a runtime
            // set: the observation repeats on every reconcile, and a memory-only set would
            // let a restart reanchor the same compaction a second time.
            // The candidate is a proposal only; the fold of the written line commits it.
            match PrefixEpochProjection.proposeCompaction observed epoch with
            | None -> return Ok None
            | Some candidate -> return! appendReanchor journal sessionId candidate
        }

    /// HOST-006 startup probe: judge the first turn of the first managed session.
    ///
    /// `None` means "not yet applicable" — this snapshot does not represent a completed
    /// first turn, so there is nothing to judge. The caller keeps the probe armed.
    ///
    /// A first turn is recognised by the presence of a COMPLETED assistant message, and
    /// the pseudo-runs counted are the ones inside that same window — everything up to
    /// and including the first completed assistant. The alternative — judging on the
    /// first snapshot of any kind — would fire mid-stream, before the Host has had the
    /// opportunity to compact at all, and would therefore pass unconditionally: a probe
    /// that cannot fail is not a probe.
    ///
    /// Why the first turn specifically. A first turn is necessarily far below any
    /// context threshold, so an automatic compaction there cannot be legitimate. A
    /// pseudo-run inside the window means something compacted outside the configuration
    /// the plugin can reach, and that is the state HOST-006 refuses to run in:
    /// reanchoring every few rounds means probe coverage never accumulates while
    /// everything looks normal from outside. A pseudo-run after the window is a user
    /// `/compact` or a later-round compaction; it belongs to the containment layer,
    /// which must reanchor rather than refuse startup.
    ///
    /// The residual misjudgement is a user manually compacting an empty session between
    /// plugin start and the first turn finishing. That costs one startup refusal with a
    /// stated reason. The inverse — missing a second compaction implementation — has no
    /// symptom at all.
    let judgeStartup
        (settingGap: CompactionSetting option)
        (sessionId: SessionId)
        (messages: SessionMessage list)
        : CompactionGateVerdict option =
        let window =
            messages
            |> List.map (fun message ->
                { CompletedAssistant = message.Role = "assistant" && message.Completed
                  Compaction = HostCompactionPolicy.isContainableCompaction message.IsCompaction })

        match HostCompactionPolicy.firstTurnCompactionRuns window with
        | None -> None
        | Some pseudoRunsOnFirstTurn ->
            Some(HostCompactionPolicy.judgeFirstTurn settingGap sessionId pseudoRunsOnFirstTurn)
