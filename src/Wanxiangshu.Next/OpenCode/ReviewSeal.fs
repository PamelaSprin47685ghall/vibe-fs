namespace Wanxiangshu.Next.OpenCode

open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Next.Domain
open Wanxiangshu.Next.Host
open Wanxiangshu.Next.Kernel
open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

/// REVIEW-010: the only writer of `ProviderInputSealed`.
///
/// Seals the canonical provider input at `messages.transform` time and binds it
/// to the `ProviderRunIdentity` that is about to consume it.
///
/// The SDK's `input` for that hook is `{}`, which reads as "no identity is
/// available at transform time". That conclusion is wrong, and acting on it is
/// what forced the old code into REVIEW-003's forbidden same-root guessing. Host
/// source (`session/prompt.ts`) creates and PERSISTS the assistant message at
/// :1186-1201, before triggering transform at :1255 — so the target run already
/// exists in the transcript and is readable through the SDK. Evidence:
/// `docs/archive/shock-anneal-2026/evidence/host-transform-run-binding.md`.
module ReviewSeal =

    /// Why no seal was written. Every case forbids confirmation rather than
    /// weakening it.
    type SealRejection =
        /// No assistant message matched the binding criteria. The compaction path
        /// triggers transform BEFORE creating its message (`session/compaction.ts`
        /// :349-360), so this is the expected answer there.
        | NoBindableRun
        /// More than one candidate. The Host serialises one prompt loop per session,
        /// so this means Host behaviour no longer matches the recorded evidence —
        /// most likely a version change. Guessing between them could seal the wrong
        /// run's input and confirm a review that never saw the challenge.
        | AmbiguousRun of count: int
        /// The only matching assistant is not the newest assistant in the session.
        /// Accepting it would bind the seal to an older provider run.
        | NotLatestRun
        /// The transform output carries no user message, so there is no
        /// `PhysicalUserMessageId` to seal against (PROMPT-001).
        | NoPhysicalUserMessage
        | SnapshotUnavailable of reason: string
        | JournalUnavailable

    /// HOST-010 binding: the one assistant message this transform is about to feed.
    ///
    /// All four conditions are required together. Any three of them admit a wrong
    /// answer: without `Completed` a finished earlier run matches, without
    /// `ParentId` a different branch matches, without the max-id rule two
    /// concurrent runs are indistinguishable, and without the compaction exclusion
    /// the Host's summariser is mistaken for a managed run.
    let bindableRun (physicalUserMessage: string) (messages: SessionMessage list) =
        let candidates =
            messages
            |> List.filter (fun message ->
                message.Role = "assistant"
                && not message.Completed
                && not message.IsCompaction
                && message.ParentId = Some physicalUserMessage)

        match candidates with
        | [] -> Error NoBindableRun
        | [ single ] ->
            let assistants = messages |> List.filter (fun message -> message.Role = "assistant")

            match assistants with
            | [] -> Error NoBindableRun
            | _ ->
                let latest = assistants |> List.maxBy (fun message -> message.Id)

                if latest.Id = single.Id then
                    Ok single
                else
                    Error NotLatestRun
        // `MessageID.ascending` is monotonic, so the newest is the largest. Reported
        // rather than silently taking the max: the clause's premise is that exactly
        // one exists, and more than one means the premise no longer holds.
        | many -> Error(AmbiguousRun(List.length many))

    /// The physical user message this request is answering (PROMPT-001).
    ///
    /// Resolved by the caller from the raw payload, because the wire projection
    /// deliberately excludes ids (VERIFY-007) and `Projection.lastUserMessageId`
    /// already owns that read.
    let seal
        (snapshot: ISessionSnapshotPort option)
        (journal: AgentJournal option)
        (sha256: string -> string)
        (sessionId: SessionId)
        (transformed: ProviderProjection.ProviderWireProjection)
        (physicalUserAddress: PhysicalUserMessageId option)
        : Task<Result<ProviderRunIdentity, SealRejection>> =
        task {
            match journal, snapshot, physicalUserAddress with
            | None, _, _ -> return Error JournalUnavailable
            | _, None, _ -> return Error(SnapshotUnavailable "no session snapshot port")
            | _, _, None -> return Error NoPhysicalUserMessage
            | Some durable, Some port, Some physicalAddress ->
                let physicalAddressValue = PhysicalUserMessageId.value physicalAddress

                match! port.GetMessages sessionId with
                | Error reason -> return Error(SnapshotUnavailable reason)
                | Ok messages ->
                    match bindableRun physicalAddressValue messages with
                    | Error rejection -> return Error rejection
                    | Ok assistant ->
                        let providerRun = ProviderRunIdentity.create assistant.Id

                        let fact =
                            AgentFact.ProviderInputSealed
                                {| SessionId = sessionId
                                   ProviderRun = providerRun
                                   PhysicalUserMessageId = physicalAddress
                                   SealDigest = ProviderProjection.sealDigest sha256 transformed
                                   CanonicalVersion = ProviderProjection.CanonicalVersion
                                   IncludedToolResultDigests = ProviderProjection.toolResultDigests sha256 transformed |}

                        match AgentJournal.appendAgent (StreamId.Session sessionId) (Some providerRun) fact durable with
                        | Ok _ -> return Ok providerRun
                        | Error failure -> return Error(SnapshotUnavailable(JournalAppendFailure.describe failure))
        }

    /// REVIEW-010: a seal candidate before its provider run exists.
    ///
    /// `sealTransform` runs before the request is answered, so the assistant that
    /// carries the run is not in the snapshot yet. The ordinary case binds the
    /// run in `seal` via `bindableRun`; a challenge request (the previous
    /// turn's tool result IS the challenge, and no assistant follows the
    /// challenge user yet) has nothing to bind. REVIEW-010 says "the next
    /// assistant/provider run, when it appears, binds the identity" — so the
    /// candidate is parked here and `bindPendingSeal` commits it once the
    /// assistant exists. Without this, a second PERFECT always failed with
    /// `ChallengeUnproven` (measured on Host 1.18.10: every dual-PERFECT flow).
    /// The type lives in `SharedState` so the shared dictionary (HOST-012) can
    /// be typed before this module compiles.
    /// Bind a parked seal to the turn's provider run and persist it.
    ///
    /// Called from the reconcile `onTurn` path: the assistant that answers the
    /// sealed request is the run REVIEW-003's second PERFECT will query, so the
    /// seal must be keyed by exactly that run. Fail closed: a journal failure
    /// here means the second PERFECT cannot confirm, which is the correct
    /// outcome when persistence is unavailable.
    let bindPendingSeal
        (journal: AgentJournal option)
        (pendingSeals: Dictionary<string, SharedState.PendingSeal>)
        (turn: ReconciledTurn)
        : Task =
        task {
            let key = SessionId.value turn.SessionId

            match pendingSeals.TryGetValue key with
            | false, _ -> return ()
            | true, pending ->
                // REVIEW-010: the parked candidate is intentionally NOT removed
                // here. The `onTurn` binding keys the seal by the reconcile run,
                // but the tool executes under `context.ProviderRunId`, which on
                // Host 1.18.10 disagrees for challenge responses; the second
                // PERFECT then fails `ChallengeUnproven` and VerdictTool's
                // fallback re-binds the same candidate to the tool's run. The
                // fallback removes it once the retry succeeds. A stale candidate
                // is harmless: the next transform of the same reviewer session
                // overwrites the same key.

                match journal with
                | None -> return ()
                | Some durable ->
                    let fact =
                        AgentFact.ProviderInputSealed
                            {| SessionId = pending.SessionId
                               ProviderRun = turn.ProviderRun
                               PhysicalUserMessageId = pending.PhysicalUserMessageId
                               SealDigest = pending.SealDigest
                               CanonicalVersion = pending.CanonicalVersion
                               IncludedToolResultDigests = pending.IncludedToolResultDigests |}

                    match
                        AgentJournal.appendAgent
                            (StreamId.Session pending.SessionId)
                            (Some turn.ProviderRun)
                            fact
                            durable
                    with
                    | Ok _ -> return ()
                    | Error failure ->
                        failwith (
                            sprintf
                                "REVIEW-010 ProviderInputSealed append failed: %s"
                                (JournalAppendFailure.describe failure)
                        )
        }

    /// Seal at the `messages.transform` boundary.
    ///
    /// Only reviewer sessions are sealed. REVIEW-010's seal exists to prove a
    /// second PERFECT consumed the challenge, and nothing else reads it — sealing
    /// every session would add one SDK round-trip to every provider step of every
    /// agent, for evidence no clause consults.
    ///
    /// Returns `unit`: a rejection here IS the fail-closed outcome. No seal means
    /// `ReviewController` cannot confirm, which is what REVIEW-003 requires when the
    /// binding is unavailable. The `Result` stays on `seal` so tests can assert
    /// Whether this request is the answer to an outstanding challenge.
    ///
    /// The challenge request's wire carries the challenge as the previous turn's
    /// tool result, so its view's tool-result digests contain the pending
    /// challenge's content digest. REVIEW-003 needs the SEAL of the challenge
    /// request's own assistant run — which does not exist at transform time — so
    /// these requests must always defer binding, even when `bindableRun` finds a
    /// candidate (it would bind the PREVIOUS attempt's assistant, whose run is
    /// not the one the next PERFECT will query — measured: every challenge
    /// request sealed the prior attempt and the second PERFECT still failed
    /// `ChallengeUnproven`).
    let private isChallengeRequest
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (transformed: ProviderProjection.ProviderWireProjection)
        =
        match journal with
        | None -> false
        | Some durable ->
            let projection = (AgentJournal.snapshot durable).AgentProjections

            match AgentProjection.tryFind sessionId projection with
            | Some session ->
                match session.ReviewGuard with
                | Some guard ->
                    match guard.PendingChallenge with
                    | Some challenge ->
                        let challengeDigest = SealDigest.value challenge.ChallengeContentDigest

                        ProviderProjection.toolResultDigests HostDigest.sha256Hex transformed
                        |> List.exists (fun digest -> SealDigest.value digest = challengeDigest)
                    | None -> false
                | None -> false
            | None -> false

    let private parkSeal
        (pendingSeals: Dictionary<string, SharedState.PendingSeal>)
        (sessionId: SessionId)
        (physicalUserAddress: PhysicalUserMessageId option)
        (transformed: ProviderProjection.ProviderWireProjection)
        : unit =
        match physicalUserAddress with
        | None -> ()
        | Some physical ->
            pendingSeals.[SessionId.value sessionId] <-
                { SessionId = sessionId
                  PhysicalUserMessageId = physical
                  SealDigest = ProviderProjection.sealDigest HostDigest.sha256Hex transformed
                  CanonicalVersion = ProviderProjection.CanonicalVersion
                  IncludedToolResultDigests = ProviderProjection.toolResultDigests HostDigest.sha256Hex transformed }

    /// which rejection occurred.
    let sealTransform
        (snapshot: ISessionSnapshotPort option)
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (transformed: ProviderProjection.ProviderWireProjection)
        (physicalUserAddress: PhysicalUserMessageId option)
        (pendingSeals: Dictionary<string, SharedState.PendingSeal>)
        : Task<unit> =
        task {
            let isReviewer =
                journal
                |> Option.bind (fun durable ->
                    PromptAuthorityLedger.activeProfile sessionId (AgentJournal.snapshot durable).AgentProjections)
                |> Option.exists (fun profile -> profile.CanonicalRole = Role.Reviewer)

            if isReviewer then
                // REVIEW-010 deferred binding for EVERY reviewer request.
                //
                // `bindableRun` only works for a request whose own assistant
                // already exists at transform time. A challenge request's wire
                // carries the challenge as the PREVIOUS turn's tool result and
                // its own assistant does not exist yet — but once the previous
                // attempt's assistant is on disk (a rejected second PERFECT),
                // `bindableRun` binds THAT run, and the next PERFECT queries a
                // different run, so it always fails `ChallengeUnproven`
                // (measured: every dual-PERFECT flow on Host 1.18.10). Parking
                // unconditionally and binding the turn's run at `onTurn` is the
                // exact REVIEW-010 contract: transform → candidate → bind when
                // the assistant appears. First-PERFECT submissions never query a
                // seal (there is no pending challenge yet), so the deferred
                // binding is safe for them too.
                parkSeal pendingSeals sessionId physicalUserAddress transformed
                return ()
            else
                return ()
        }
