namespace Wanxiangshu.Next.OpenCode

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
/// `STATUS/evidence/host-transform-run-binding.md`.
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
    let private bindableRun (physicalUserMessage: string) (messages: SessionMessage list) =
        let candidates =
            messages
            |> List.filter (fun message ->
                message.Role = "assistant"
                && not message.Completed
                && not message.IsCompaction
                && message.ParentId = Some physicalUserMessage)

        match candidates with
        | [] -> Error NoBindableRun
        | [ single ] -> Ok single
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
        (physicalUserAddress: string option)
        : Task<Result<ProviderRunIdentity, SealRejection>> =
        task {
            match journal, snapshot, physicalUserAddress with
            | None, _, _ -> return Error JournalUnavailable
            | _, None, _ -> return Error(SnapshotUnavailable "no session snapshot port")
            | _, _, None -> return Error NoPhysicalUserMessage
            | Some durable, Some port, Some physicalAddress ->
                match! port.GetMessages sessionId with
                | Error reason -> return Error(SnapshotUnavailable reason)
                | Ok messages ->
                    match bindableRun physicalAddress messages with
                    | Error rejection -> return Error rejection
                    | Ok assistant ->
                        let providerRun = ProviderRunIdentity.create assistant.Id

                        let fact =
                            AgentFact.ProviderInputSealed
                                {| SessionId = sessionId
                                   ProviderRun = providerRun
                                   PhysicalUserMessageId = PhysicalUserMessageId.create physicalAddress
                                   SealDigest = ProviderProjection.sealDigest sha256 transformed
                                   CanonicalVersion = ProviderProjection.CanonicalVersion
                                   IncludedToolResultDigests = ProviderProjection.toolResultDigests sha256 transformed |}

                        match AgentJournal.appendAgent (StreamId.Session sessionId) (Some providerRun) fact durable with
                        | Ok _ -> return Ok providerRun
                        | Error failure -> return Error(SnapshotUnavailable(sprintf "%A" failure.Failure))
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
    /// which rejection occurred.
    let sealTransform
        (snapshot: ISessionSnapshotPort option)
        (journal: AgentJournal option)
        (sessionId: SessionId)
        (rawMessages: obj list)
        : Task<unit> =
        task {
            let isReviewer =
                journal
                |> Option.bind (fun durable ->
                    PromptAuthorityLedger.activeProfile sessionId (AgentJournal.snapshot durable).AgentProjections)
                |> Option.exists (fun profile -> profile.CanonicalRole = Role.Reviewer)

            if isReviewer then
                let! _ =
                    seal
                        snapshot
                        journal
                        HostDigest.sha256Hex
                        sessionId
                        (Projection.decodeMessageView rawMessages)
                        (Projection.lastUserMessageId rawMessages)

                return ()
            else
                return ()
        }
