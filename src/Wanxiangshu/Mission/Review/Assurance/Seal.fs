namespace Wanxiangshu.Mission.Review.Assurance

open System.Collections.Generic
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Persistence.Journal
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Host
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Identity

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
/// exists in the transcript and is readable through the SDK (HOST-010).
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

    /// REVIEW-010: bind a parked seal to the provider run that will consume it.
    ///
    /// This is the ONLY binding point. The previous design also bound at the
    /// reconcile `onTurn` path, but on Host 1.18.10 the reconcile run and the
    /// tool's `context.ProviderRunId` disagree for challenge responses — the
    /// onTurn seal was keyed by a run the next PERFECT never queries, i.e. dead
    /// data written by a second writer (measured: every dual-PERFECT flow).
    /// The tool executing under the run is the only party that holds the run id
    /// `provenSeal` will ask for, so binding happens here, immediately before
    /// the verdict submit that consumes it.
    ///
    /// The parked candidate is removed by the caller once the submit succeeds.
    /// A stale candidate is harmless: the next transform of the same reviewer
    /// session overwrites the same key.
    type SealBindFailure =
        /// No transform parked a seal candidate for this reviewer session.
        | NoPendingSeal
        /// The candidate existed but could not be persisted.
        | AppendFailed of string

    let bindToRun
        (journal: AgentJournal)
        (pendingSeals: Dictionary<string, SharedState.PendingSeal>)
        (sessionId: SessionId)
        (providerRun: ProviderRunIdentity)
        : Task<Result<unit, SealBindFailure>> =
        task {
            let key = SessionId.value sessionId

            match pendingSeals.TryGetValue key with
            | false, _ -> return Error NoPendingSeal
            | true, pending ->
                let fact =
                    ReviewFact.ProviderInputSealed
                        {| SessionId = pending.SessionId
                           ProviderRun = providerRun
                           PhysicalUserMessageId = pending.PhysicalUserMessageId
                           SealDigest = pending.SealDigest
                           CanonicalVersion = pending.CanonicalVersion
                           IncludedToolResultDigests = pending.IncludedToolResultDigests |}

                match!
                    AgentJournal.appendAgent (StreamId.Session pending.SessionId) (Some providerRun) fact journal
                with
                | Ok _ -> return Ok()
                | Error failure -> return Error(AppendFailed(JournalAppendFailure.describe failure))
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
    /// binding is unavailable.
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
                // unconditionally and binding at VerdictTool via bindToRun with
                // the tool's ProviderRunId is the REVIEW-010 contract: transform
                // parks seal evidence → tool resolves/fail-closed (not an onTurn
                // stage bit). First-PERFECT submissions never query a
                // seal (there is no pending challenge yet), so the deferred
                // binding is safe for them too.
                parkSeal pendingSeals sessionId physicalUserAddress transformed
                return ()
            else
                return ()
        }
