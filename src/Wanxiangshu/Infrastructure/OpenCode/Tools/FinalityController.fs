namespace Wanxiangshu.OpenCode

open System
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Journal
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// GLORY-003/040/042: the Manager Finality workflow, driven by the Host after a
/// legal `suicide`.
///
/// Ownership: the hidden Reviewer is forked on a private `HostForkRuntime`
/// (parent = the Manager session, but never registered into the Manager's own
/// fork surface), so `list`/`join` never see it (GLORY-002). The workflow
/// writes `FinalityReviewStarted`, runs the shared `HostReviewProgram`, then
/// lands one of: `FinalityRejected` + work-record tool result (GLORY-052/053),
/// `FinalityConfirmed` + `LifeCompleted` + last_words terminal (GLORY-060/061),
/// or `FinalityUndecided` + undecidable tool result (GLORY-057).
module FinalityController =

    type FinalityOutcome =
        | Confirmed of message: string
        | Rejected of prompt: string
        | Undecided of prompt: string

    let private appendLifecycle (journal: AgentJournal) (fact: ManagerLifecycleFact) =
        let sessionId =
            match fact with
            | ManagerLifecycleFact.LifeOpened payload -> payload.SessionId
            | ManagerLifecycleFact.WorkActivated payload -> payload.SessionId
            | ManagerLifecycleFact.FinalityRequested payload -> payload.SessionId
            | ManagerLifecycleFact.FinalityReviewStarted payload -> payload.SessionId
            | ManagerLifecycleFact.FinalityRejected payload -> payload.SessionId
            | ManagerLifecycleFact.FinalityConfirmed payload -> payload.SessionId
            | ManagerLifecycleFact.FinalityUndecided payload -> payload.SessionId
            | ManagerLifecycleFact.LifeCompleted payload -> payload.SessionId

        AgentJournal.appendManagerLifecycle (StreamId.Session sessionId) fact journal
        |> Result.mapError (fun failure ->
            raise (
                InvalidOperationException(sprintf "Finality append failed: %s" (JournalAppendFailure.describe failure))
            ))
        |> ignore

    /// The hidden Reviewer's completion as the HostReviewProgram await result.
    let private awaitReviewer (timeoutMs: int) (runtime: HostForkRuntime) (agentId: string) =
        task {
            match!
                runtime.AwaitAgent(
                    agentId,
                    ?timeoutMs = Some timeoutMs
                ) with
            | Error error -> return Error error
            | Ok run ->
                match run.Outcome with
                | AgentCompleted _ -> return Ok()
                | AgentFailed payload -> return Error payload.Message
                | AgentAbandoned(_, reason) -> return Error reason
        }

    /// GLORY-058/059: re-read the tree and require byte equality with the
    /// request's tree before any confirmation lands.
    let private treeUnchanged (scope: ToolRuntimeScope) (managerSessionId: SessionId) (expected: GitTreeHash) =
        try
            match scope.TreePortFor(SessionId.value managerSessionId) with
            | None -> false
            | Some port ->
                let current = port.GetTreeHash().Trim()
                not (String.IsNullOrWhiteSpace current) && GitTreeHash.create current = expected
        with _ ->
            false

    let private abortReviewer (scope: ToolRuntimeScope) (reviewerSessionId: SessionId) =
        scope.Sessions.AbortSession reviewerSessionId |> ignore
        scope.SessionParents.Remove(SessionId.value reviewerSessionId) |> ignore

    /// The Glory path (GLORY-059/060/061): revalidate the tree, confirm, complete
    /// the Life with last_words as the user-visible terminal, finish the Manager.
    let private concludeGlory
        (scope: ToolRuntimeScope)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (reviewerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        (requestTree: GitTreeHash)
        (lastWordsRef: BlobRef)
        (lastWordsDigest: BlobDigest)
        (providerRun: ProviderRunIdentity)
        : Task<FinalityOutcome> =
        task {
            if not (treeUnchanged scope managerSessionId requestTree) then
                // GLORY-059: the tree moved under the confirmed witness; this
                // success is void. Close the request fail-closed without a wound
                // record (GLORY-057).
                match scope.Journal with
                | Some journal ->
                    appendLifecycle
                        journal
                        (ManagerLifecycleFact.FinalityUndecided
                            {| SessionId = managerSessionId
                               LifeId = lifeId
                               RequestId = requestId
                               ReviewerSessionId = reviewerSessionId
                               BarrierId = barrierId
                               GitTreeHash = requestTree |})
                | None -> ()

                abortReviewer scope reviewerSessionId
                return Undecided ManagerLifecyclePrompt.FinalityUndecidable
            else
                match scope.Journal with
                | None ->
                    abortReviewer scope reviewerSessionId
                    return Undecided ManagerLifecyclePrompt.FinalityUndecidable
                | Some journal ->
                    // GLORY-062/067 idempotence: the XTrace terminal is a single
                    // per-session slot (GLORY-067 compatibility layer), and
                    // LifeCompleted archives the Life. A replay of this path (crash
                    // between LifeCompleted and terminal publish, or a later Life's
                    // glory) must neither append a second TerminalOutputCaptured
                    // (Fold rejects it → journal poison) nor re-complete.
                    let alreadyCompleted =
                        AgentProjection.tryFind managerSessionId (AgentJournal.snapshot journal).AgentProjections
                        |> Option.bind (fun session -> session.ManagerLife)
                        |> Option.exists (fun lifecycle ->
                            (lifecycle.CurrentLife
                             |> Option.exists (fun life -> life.LifeId = lifeId && life.Completed))
                            || lifecycle.CompletedLives |> List.exists (fun life -> life.LifeId = lifeId))

                    let terminalRecorded =
                        AgentProjection.tryFind managerSessionId (AgentJournal.snapshot journal).AgentProjections
                        |> Option.bind (fun session -> session.XTrace)
                        |> Option.exists (fun state -> state.Terminal.IsSome)

                    if not alreadyCompleted then
                        let lastWords =
                            journal.Writer.BlobWriter.Read lastWordsRef
                            |> Result.toOption
                            |> Option.defaultValue ""

                        appendLifecycle
                            journal
                            (ManagerLifecycleFact.FinalityConfirmed
                                {| SessionId = managerSessionId
                                   LifeId = lifeId
                                   RequestId = requestId
                                   ReviewerSessionId = reviewerSessionId
                                   BarrierId = barrierId
                                   GitTreeHash = requestTree |})

                        // GLORY-060: LifeCompleted BEFORE the terminal is published.
                        appendLifecycle
                            journal
                            (ManagerLifecycleFact.LifeCompleted
                                {| SessionId = managerSessionId
                                   LifeId = lifeId
                                   RequestId = requestId
                                   TerminalRef = lastWordsRef
                                   TerminalDigest = lastWordsDigest |})

                        // The last_words blob IS the terminal segment (GLORY-061).
                        // Only the FIRST Life may occupy the single XTrace terminal
                        // slot; later Lives' terminals live in LifeCompleted only.
                        if not terminalRecorded then
                            AgentJournal.appendAgent
                                (StreamId.Session managerSessionId)
                                (Some providerRun)
                                (CompanionFact.TerminalOutputCaptured
                                    {| SessionId = managerSessionId
                                       TextRef = lastWordsRef
                                       TextDigest = lastWordsDigest
                                       ProviderRun = providerRun |})
                                journal
                            |> ignore

                        match scope.EventPort with
                        | Some eventPort ->
                            let runResult: AgentRunResult =
                                { SessionId = managerSessionId
                                  AuthorityRootUserMessageId =
                                    PromptAuthorityLedger.activeProfile
                                        managerSessionId
                                        (AgentJournal.snapshot journal).AgentProjections
                                    |> Option.map (fun profile -> profile.AuthorityRootUserMessageId)
                                    |> Option.defaultValue (AuthorityRootUserMessageId.create "")
                                  ProviderRun = providerRun
                                  Role = Role.Manager
                                  Directory = scope.DirectoryFor(SessionId.value managerSessionId)
                                  TerminalText = lastWords
                                  TurnFormalText = lastWords }

                            eventPort.NotifyTerminal managerSessionId (TerminalOutcome.Completed runResult)
                            |> ignore
                        | None -> ()

                    abortReviewer scope reviewerSessionId
                    return Confirmed "Your final words have been received."
        }

    /// The wound path (GLORY-051/052/053): the reviewer's canonical LWR becomes
    /// the FinalityRejected suicide tool result; the same Life continues.
    let private concludeRejection
        (scope: ToolRuntimeScope)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (reviewerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        (requestTree: GitTreeHash)
        (workRecord: string)
        : Task<FinalityOutcome> =
        task {
            match scope.Journal with
            | None ->
                abortReviewer scope reviewerSessionId
                return Undecided ManagerLifecyclePrompt.FinalityUndecidable
            | Some journal ->
                match journal.WriteBlob workRecord with
                | Error _ ->
                    abortReviewer scope reviewerSessionId
                    return Undecided ManagerLifecyclePrompt.FinalityUndecidable
                | Ok blob ->
                    appendLifecycle
                        journal
                        (ManagerLifecycleFact.FinalityRejected
                            {| SessionId = managerSessionId
                               LifeId = lifeId
                               RequestId = requestId
                               ReviewerSessionId = reviewerSessionId
                               BarrierId = barrierId
                               GitTreeHash = requestTree
                               WorkRecordRef = blob.BlobRef
                               WorkRecordDigest = blob.BlobDigest |})

                    abortReviewer scope reviewerSessionId
                    return Rejected(FinalityPrompt.rejected workRecord)
        }

    /// GLORY-057: infrastructure failure — no verdict, no wound record.
    let private concludeUndecided
        (scope: ToolRuntimeScope)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (reviewerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        (requestTree: GitTreeHash)
        : Task<FinalityOutcome> =
        task {
            match scope.Journal with
            | None -> ()
            | Some journal ->
                appendLifecycle
                    journal
                    (ManagerLifecycleFact.FinalityUndecided
                        {| SessionId = managerSessionId
                           LifeId = lifeId
                           RequestId = requestId
                           ReviewerSessionId = reviewerSessionId
                           BarrierId = barrierId
                           GitTreeHash = requestTree |})

            abortReviewer scope reviewerSessionId
            return Undecided ManagerLifecyclePrompt.FinalityUndecidable
        }

    /// GLORY-040 step 6: start the Finality workflow after a legal suicide was
    /// accepted. Synchronous execution: every outcome lands on the journal before any side effect.
    let start
        (scope: ToolRuntimeScope)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (requestTree: GitTreeHash)
        (lastWordsRef: BlobRef)
        (lastWordsDigest: BlobDigest)
        (providerRun: ProviderRunIdentity)
        (reviewerTimeoutMs: int)
        : Task<FinalityOutcome> =
        task {
            match scope.Journal with
            | None -> return Undecided ManagerLifecyclePrompt.FinalityUndecidable
            | Some journal ->
                try
                    // GLORY-002: a private runtime; the reviewer never enters the
                    // Manager's Children map, so list/join cannot see it.
                    let runtime =
                        HostForkRuntime(
                            managerSessionId,
                            scope.Sessions,
                            ?journal = scope.Journal,
                            onChildCreated =
                                (fun _ _ childId ->
                                    scope.SessionParents.[SessionId.value childId] <- SessionId.value managerSessionId),
                            onChildCreatedDir =
                                (fun _ childId directory ->
                                    directory
                                    |> Option.iter (fun path -> scope.RegisterDirectory(SessionId.value childId, path))),
                            directoryFor = (fun _ -> scope.DirectoryFor(SessionId.value managerSessionId)),
                            onRunStarted = scope.RunStarted,
                            parentWorkRecordFor =
                                (fun _ -> XTraceCapture.lifecycleWorkRecord (Some journal) managerSessionId true),
                            childWorkRecordFor = (fun _ -> None),
                            ?sessionSnapshot = scope.Snapshot,
                            managerOpensReviewBarrier = false
                        )

                    let managerProfile = scope.ActiveProfileFor managerSessionId

                    let reviewerTier =
                        managerProfile
                        |> Option.map (fun profile -> profile.SelectedTier)
                        |> Option.defaultValue AgentTier.Deep

                    let reviewerAgentName = ManagedAgent.nameOf reviewerTier Role.Reviewer

                    let reviewerAgentId = sprintf "finality-%s" (FinalityRequestId.value requestId)

                    let! forkResult =
                        runtime.Fork(
                            reviewerAgentId,
                            Role.Reviewer,
                            reviewerAgentName,
                            HostReviewPrompt.OpeningAssignment,
                            None
                        )

                    match forkResult, runtime.TryChildSession reviewerAgentId with
                    | Error _, _
                    | Ok _, None ->
                        // GLORY-056/057: the Reviewer could not be created — an
                        // infrastructure failure. Close the request so the Manager
                        // may seek its end again; never fabricate a wound record.
                        let closureBarrier = ReviewBarrierId.create (Guid.NewGuid().ToString("N"))

                        appendLifecycle
                            journal
                            (ManagerLifecycleFact.FinalityUndecided
                                {| SessionId = managerSessionId
                                   LifeId = lifeId
                                   RequestId = requestId
                                   // No reviewer session exists; the closure is
                                   // addressed by the Manager session + barrier.
                                   ReviewerSessionId = managerSessionId
                                   BarrierId = closureBarrier
                                   GitTreeHash = requestTree |})

                        return Undecided ManagerLifecyclePrompt.FinalityUndecidable
                    | Ok _, Some reviewerSessionId ->
                        // GLORY-040: the barrier opens only now — the reviewer
                        // session exists (REVIEW-008).
                        let barrierId = ReviewBarrierId.create (Guid.NewGuid().ToString("N"))

                        appendLifecycle
                            journal
                            (ManagerLifecycleFact.FinalityReviewStarted
                                {| SessionId = managerSessionId
                                   LifeId = lifeId
                                   RequestId = requestId
                                   ReviewerSessionId = reviewerSessionId
                                   BarrierId = barrierId
                                   GitTreeHash = requestTree |})

                        let! outcome =
                            HostReviewProgram.reverify
                                (Some journal)
                                (fun () -> Task.FromResult(Ok reviewerSessionId))
                                (fun () -> awaitReviewer reviewerTimeoutMs runtime reviewerAgentId)
                                (fun () ->
                                    task {
                                        match!
                                            runtime.Fork(
                                                reviewerAgentId,
                                                Role.Reviewer,
                                                reviewerAgentName,
                                                ReviewChallenge.Prompt,
                                                None,
                                                firstPrompt = false
                                            )
                                        with
                                        | Error error -> return Error(sprintf "%A" error)
                                        | Ok _ -> return Ok()
                                    })
                                managerSessionId
                                barrierId
                                requestTree

                        match outcome with
                        | Ok(HostReviewProgram.HostReviewOutcome.Confirmed(reviewerId, barrier, _tree)) ->
                            return!
                                concludeGlory
                                    scope
                                    managerSessionId
                                    lifeId
                                    requestId
                                    reviewerId
                                    barrier
                                     requestTree
                                     lastWordsRef
                                     lastWordsDigest
                                     providerRun
                        | Ok(HostReviewProgram.HostReviewOutcome.RevisionRequired(reviewerId, barrier, _tree, record)) ->
                            return!
                                concludeRejection
                                    scope
                                    managerSessionId
                                    lifeId
                                    requestId
                                    reviewerId
                                    barrier
                                    requestTree
                                    record
                        | Error failure ->
                            match
                                (match failure with
                                 | HostReviewProgram.HostReviewFailure.CannotCreateReviewer _
                                 | HostReviewProgram.HostReviewFailure.CannotOpenBarrier _
                                 | HostReviewProgram.HostReviewFailure.CannotSendPrompt _
                                 | HostReviewProgram.HostReviewFailure.CannotAwaitReviewer _
                                 | HostReviewProgram.HostReviewFailure.ReviewerProducedNoVerdict
                                 | HostReviewProgram.HostReviewFailure.ConfirmationUnproven
                                 | HostReviewProgram.HostReviewFailure.WorkRecordUnavailable
                                 | HostReviewProgram.HostReviewFailure.JournalFailure _
                                 | HostReviewProgram.HostReviewFailure.CannotReadTree _ ->
                                     try
                                         let reviewerId = runtime.TryChildSession reviewerAgentId

                                         match reviewerId with
                                         | Some reviewerId -> Some reviewerId
                                         | None -> None
                                     with _ ->
                                         None)
                            with
                            | Some reviewerId ->
                                return!
                                    concludeUndecided
                                        scope
                                        managerSessionId
                                        lifeId
                                        requestId
                                        reviewerId
                                        barrierId
                                        requestTree
                            | None -> return Undecided ManagerLifecyclePrompt.FinalityUndecidable
                with ex ->
                    // Exception boundary: never leak an exception out of
                    // the tool call that accepted the suicide.
                    Diagnostic.emit "finality" [ "session_id", SessionId.value managerSessionId; "error", ex.Message ]
                    return Undecided ManagerLifecyclePrompt.FinalityUndecidable
        }
