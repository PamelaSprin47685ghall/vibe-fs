namespace Wanxiangshu.OpenCode

open System
open System.Collections.Generic
open System.Threading.Tasks
open Wanxiangshu.Domain
open Wanxiangshu.Journal
open Fable.Core.JsInterop
open Wanxiangshu.Kernel
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Session

/// GLORY-003/040/042/044/045/060/061: the Manager Finality workflow, driven by
/// the Host after a legal `suicide`.
///
/// One FinalityRequest enlists a cohort = every still-ungraduated historical
/// Reviewer of this Life + exactly one new Reviewer (GLORY-045). Every member
/// gets a fresh barrier on the request tree and must produce its own fresh
/// dual-PERFECT. Any REVISE closes the request immediately (`FinalityRejected`,
/// GLORY-044/055); only when ALL members have causally confirmed does the
/// request land `FinalityBlessed` with the stable-ordinal canonical
/// work-record bundle (GLORY-060). The Life stays open — the Manager keeps
/// working on minor problems until its second suicide (GLORY-061/062).
module FinalityController =

    type FinalityOutcome =
        | Rejected of prompt: string
        | Blessed of prompt: string
        | Undecided of prompt: string

    /// One enlisted cohort member with the durable identities the driver needs.
    type EnlistedMember =
        { ReviewerSessionId: SessionId
          BarrierId: ReviewBarrierId
          ReviewerOrdinal: int
          AgentId: string }

    /// Cooperative cancellation for sibling drivers on a REVISE short-circuit:
    /// stops their NEXT effect, never touches their durable sessions.
    /// Fable's Task does not expose `IsCompleted`; keep a local flag.
    type CancelToken() =
        // DSL-MUTABLE: cancellation — cooperative cancel latch for sibling drivers
        let mutable cancelled = false

        let tcs =
            TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

        member _.Task = tcs.Task
        member _.IsCancelled = cancelled

        member _.Cancel() =
            cancelled <- true
            AsyncSupport.trySetResult tcs () |> ignore

    let private raceWithCancel (cancel: CancelToken) (work: Task<'a>) : Task<'a option> =
        task {
            let taggedWork: Task<obj> =
                emitJsExpr work "$0.then(function (r) { return { kind: 0, r: r }; })"

            let taggedCancel: Task<obj> =
                emitJsExpr cancel.Task "$0.then(function () { return { kind: 1 }; })"

            let! winner = emitJsExpr (taggedWork, taggedCancel) "Promise.race([$0, $1])": Task<obj>

            let kind: int = emitJsExpr winner "$0.kind"

            if kind = 0 then
                return Some(emitJsExpr winner "$0.r": 'a)
            else
                return None
        }

    let private appendLifecycle (journal: AgentJournal) (fact: ManagerLifecycleFact) =
        let sessionId =
            match fact with
            | ManagerLifecycleFact.LifeOpened payload -> payload.SessionId
            | ManagerLifecycleFact.WorkActivated payload -> payload.SessionId
            | ManagerLifecycleFact.FinalityRequested payload -> payload.SessionId
            | ManagerLifecycleFact.FinalityReviewerEnlisted payload -> payload.SessionId
            | ManagerLifecycleFact.FinalityRejected payload -> payload.SessionId
            | ManagerLifecycleFact.FinalityBlessed payload -> payload.SessionId
            | ManagerLifecycleFact.FinalityUndecided payload -> payload.SessionId
            | ManagerLifecycleFact.LifeCompleted payload -> payload.SessionId

        AgentJournal.appendManagerLifecycle (StreamId.Session sessionId) fact journal
        |> Result.mapError (fun failure ->
            raise (
                InvalidOperationException(sprintf "Finality append failed: %s" (JournalAppendFailure.describe failure))
            ))
        |> ignore

    /// GLORY-049: the member's canonical LWR (includeOpening=false). `None`
    /// means the LWR is unavailable — an infrastructure failure, never a wound
    /// record (GLORY-051/056).
    let private workRecordOf (journal: AgentJournal) (reviewerSessionId: SessionId) =
        match XTraceCapture.lifecycleWorkRecord (Some journal) reviewerSessionId false with
        | Some record when not (System.String.IsNullOrWhiteSpace record) -> Some record
        | _ -> None

    let private readOutcome
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        (reviewerSessionId: SessionId)
        (tree: GitTreeHash)
        : Result<HostReviewProgram.HostReviewOutcome, HostReviewProgram.HostReviewFailure> =
        match OrchestratorReviewRead.read (Some journal) reviewerSessionId tree with
        | OrchestratorReviewRead.Confirmed ->
            Ok(HostReviewProgram.HostReviewOutcome.Confirmed(reviewerSessionId, barrierId, tree))
        | OrchestratorReviewRead.RevisionRequired ->
            match workRecordOf journal reviewerSessionId with
            | Some record ->
                Ok(HostReviewProgram.HostReviewOutcome.RevisionRequired(reviewerSessionId, barrierId, tree, record))
            | None -> Error HostReviewProgram.HostReviewFailure.WorkRecordUnavailable
        | OrchestratorReviewRead.PendingConfirmation -> Error HostReviewProgram.HostReviewFailure.ConfirmationUnproven
        | OrchestratorReviewRead.NeedsReview -> Error HostReviewProgram.HostReviewFailure.ReviewerProducedNoVerdict

    /// The hidden Reviewer's next terminal as the driver's await result.
    /// Must not use `AwaitAgent`: that cell is single-assignment and a second
    /// await after the first PERFECT would re-observe the assignment terminal,
    /// re-issue the challenge forever, and never see the second PERFECT.
    /// The hidden Reviewer's NEXT terminal. Sticky terminal replay (HostEventPort)
    /// re-delivers the previous completion to late subscribers so InstallRun cannot
    /// miss a finish; an await-for-next driver must arm AFTER subscribe returns so
    /// that synchronous sticky replay is ignored (otherwise dual PERFECT hangs:
    /// first PERFECT's sticky terminal would complete the challenge await early).
    let private awaitReviewer
        (scope: ToolRuntimeScope)
        (timeoutMs: int)
        (cancel: CancelToken)
        (reviewerSessionId: SessionId)
        =
        task {
            if cancel.IsCancelled then
                return Error "review attempt cancelled"
            else
                let completed =
                    TaskCompletionSource<TerminalOutcome>(TaskCreationOptions.RunContinuationsAsynchronously)

                let accepting = ref false

                use subscription =
                    scope.Sessions.SubscribeTerminal(
                        reviewerSessionId,
                        fun _ outcome ->
                            if accepting.Value then
                                AsyncSupport.trySetResult completed outcome |> ignore
                    )

                // Sticky replay ran inside Subscribe; only real future terminals count.
                accepting.Value <- true

                let finished =
                    task {
                        let! outcome = completed.Task

                        match outcome with
                        | TerminalOutcome.Completed _ -> return Ok()
                        | TerminalOutcome.Failed error -> return Error error
                        | TerminalOutcome.Aborted reason -> return Error reason
                    }

                let timedOut: Task<Result<unit, string>> =
                    emitJsExpr
                        timeoutMs
                        "new Promise(function (resolve) { var t = setTimeout(function () { resolve({ tag: 1, fields: ['await reviewer timed out'] }); }, $0); if (t && typeof t.unref === 'function') t.unref(); })"

                let cancelled =
                    task {
                        do! cancel.Task
                        return Error "review attempt cancelled"
                    }

                return!
                    (emitJsExpr (finished, timedOut, cancelled) "Promise.race([$0, $1, $2])": Task<Result<unit, string>>)
        }

    /// GLORY-058/059: re-read the tree and require byte equality with the
    /// request's tree before any blessing lands.
    let private treeUnchanged (scope: ToolRuntimeScope) (managerSessionId: SessionId) (expected: GitTreeHash) =
        try
            match scope.TreePortFor(SessionId.value managerSessionId) with
            | None -> false
            | Some port ->
                let current = port.GetTreeHash().Trim()
                not (String.IsNullOrWhiteSpace current) && GitTreeHash.create current = expected
        with _ ->
            false

    /// One member's protocol: Finality enlists and waits; ReviewerWorkflow owns
    /// every post-terminal continuation.
    let private driveMember
        (scope: ToolRuntimeScope)
        (journal: AgentJournal)
        (cancel: CancelToken)
        (runtime: HostForkRuntime)
        (managerSessionId: SessionId)
        (memberInfo: EnlistedMember)
        (tree: GitTreeHash)
        (timeoutMs: int)
        : Task<Result<HostReviewProgram.HostReviewOutcome, HostReviewProgram.HostReviewFailure>> =
        let awaitOrCancel () =
            task {
                match! awaitReviewer scope timeoutMs cancel memberInfo.ReviewerSessionId with
                | Error error -> return Error error
                | Ok() when cancel.IsCancelled -> return Error "review attempt cancelled"
                | Ok() -> return Ok()
            }

        HostReviewProgram.reverify
            (Some journal)
            (fun () -> Task.FromResult(Ok memberInfo.ReviewerSessionId))
            awaitOrCancel
            managerSessionId
            memberInfo.BarrierId
            tree

    /// GLORY-044: concurrent fan-out with immediate REVISE short-circuit. All
    /// drivers start together; the first Revision result wins the race, cancels
    /// siblings immediately, and remaining drivers stop before their next effect.
    /// Durable Reviewer sessions are never disposed here (GLORY-055).
    let private concurrentAllOrShortCircuit
        (cancel: CancelToken)
        (isShortCircuit: 'a -> bool)
        (tasks: Task<'a> list)
        : Task<Choice<'a, 'a list>> =
        task {
            let tcs =
                TaskCompletionSource<Choice<'a, 'a list>>(TaskCreationOptions.RunContinuationsAsynchronously)

            let remaining = ref (List.length tasks)
            let results = ResizeArray<'a>()

            let decide (result: 'a) =
                if isShortCircuit result then
                    // Cancel siblings before the outer CE resumes — otherwise a
                    // Perfect-pending sibling can still send its challenge after
                    // REVISE has already won the race.
                    cancel.Cancel()
                    AsyncSupport.trySetResult tcs (Choice1Of2 result) |> ignore
                else
                    results.Add result
                    remaining.Value <- remaining.Value - 1

                    if remaining.Value = 0 then
                        AsyncSupport.trySetResult tcs (Choice2Of2(List.ofSeq results)) |> ignore

            tasks
            |> List.iter (fun work ->
                async {
                    let! result = Async.AwaitTask work
                    decide result
                }
                |> Async.StartImmediate)

            return! tcs.Task
        }

    /// GLORY-057: infrastructure failure — no verdict, no wound record.
    let private concludeUndecided
        (scope: ToolRuntimeScope)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (requestTree: GitTreeHash)
        (reviewerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        : Task<FinalityOutcome> =
        task {
            appendLifecycle
                journal
                (ManagerLifecycleFact.FinalityUndecided
                    {| SessionId = managerSessionId
                       LifeId = lifeId
                       RequestId = requestId
                       ReviewerSessionId = reviewerSessionId
                       BarrierId = barrierId
                       GitTreeHash = requestTree |})

            return Undecided ManagerLifecyclePrompt.FinalityUndecidable
        }

    /// The wound path (GLORY-051/052/053): the rejecting reviewer's canonical
    /// LWR becomes the FinalityRejected suicide tool result; the same Life
    /// continues. The reviewer stays ungraduated and its session is preserved.
    let private concludeRejection
        (scope: ToolRuntimeScope)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (rejectingReviewer: SessionId)
        (barrierId: ReviewBarrierId)
        (requestTree: GitTreeHash)
        (workRecord: string)
        : Task<FinalityOutcome> =
        task {
            match journal.WriteBlob workRecord with
            | Error _ -> return Undecided ManagerLifecyclePrompt.FinalityUndecidable
            | Ok blob ->
                appendLifecycle
                    journal
                    (ManagerLifecycleFact.FinalityRejected
                        {| SessionId = managerSessionId
                           LifeId = lifeId
                           RequestId = requestId
                           RejectingReviewerSessionId = rejectingReviewer
                           BarrierId = barrierId
                           GitTreeHash = requestTree
                           WorkRecordRef = blob.BlobRef
                           WorkRecordDigest = blob.BlobDigest |})

                return Rejected(FinalityPrompt.rejected workRecord)
        }

    /// GLORY-060/061: every member confirmed. Re-validate the tree, materialize
    /// the stable-ordinal canonical LWR bundle, append `FinalityBlessed`, and
    /// hand the Manager the minor-work continuation. NO LifeCompleted, NO
    /// NotifyTerminal: the Life continues until the second suicide.
    let private concludeBlessing
        (scope: ToolRuntimeScope)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (members: EnlistedMember list)
        (requestTree: GitTreeHash)
        : Task<FinalityOutcome> =
        task {
            if not (treeUnchanged scope managerSessionId requestTree) then
                // GLORY-059: the tree moved under the confirmed witnesses; this
                // success is void. Close the request fail-closed.
                let reviewer, barrier =
                    match members with
                    | first :: _ -> first.ReviewerSessionId, first.BarrierId
                    | [] -> managerSessionId, ReviewBarrierId.create (Guid.NewGuid().ToString("N"))

                return! concludeUndecided scope journal managerSessionId lifeId requestId requestTree reviewer barrier
            else
                // GLORY-050/060: one canonical LWR per member, ordered by the
                // stable ReviewerOrdinal, concatenated into one bundle blob.
                let orderedRecords =
                    members
                    |> List.sortBy (fun m -> m.ReviewerOrdinal)
                    |> List.map (fun m ->
                        match workRecordOf journal m.ReviewerSessionId with
                        | Some record -> Some(m.ReviewerOrdinal, record)
                        | None -> None)
                    |> List.filter Option.isSome
                    |> List.map Option.get

                if List.length orderedRecords <> List.length members then
                    let reviewer, barrier =
                        match members with
                        | first :: _ -> first.ReviewerSessionId, first.BarrierId
                        | [] -> managerSessionId, ReviewBarrierId.create (Guid.NewGuid().ToString("N"))

                    return!
                        concludeUndecided scope journal managerSessionId lifeId requestId requestTree reviewer barrier
                else
                    // Semantic material only — no TOML / comment syntax here.
                    // Display ordinal is 1-based stable ReviewerOrdinal + 1.
                    let logs =
                        orderedRecords
                        |> List.map (fun (ordinal, record) -> ordinal + 1, SyntheticToml.normalizeNewlines record)

                    let material = logs |> List.map snd |> String.concat "\n\n"

                    match journal.WriteBlob material with
                    | Error _ -> return Undecided ManagerLifecyclePrompt.FinalityUndecidable
                    | Ok blob ->
                        appendLifecycle
                            journal
                            (ManagerLifecycleFact.FinalityBlessed
                                {| SessionId = managerSessionId
                                   LifeId = lifeId
                                   RequestId = requestId
                                   GitTreeHash = requestTree
                                   WorkRecordBundleRef = blob.BlobRef
                                   WorkRecordBundleDigest = blob.BlobDigest |})

                        // Graduated members may release their physical sessions;
                        // ungraduated ones stay alive for the next request.
                        for m in members do
                            scope.Sessions.AbortSession m.ReviewerSessionId |> ignore

                        return Blessed(FinalityPrompt.blessedFromLogs logs)
        }

    /// GLORY-040: enlist one cohort member in the forced causal order —
    /// hidden session → durable enlist → barrier → first assignment. Every
    /// step is an idempotent `ensure` (GLORY-057): a crash between steps
    /// re-enters the same CE and continues from the first missing fact.
    let private enlistMember
        (scope: ToolRuntimeScope)
        (journal: AgentJournal)
        (runtime: HostForkRuntime)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (requestTree: GitTreeHash)
        (reviewerAgentName: string)
        (slot: FinalityReviewCohort.CohortSlot)
        : Task<Result<EnlistedMember, string>> =
        task {
            let snapshot = (AgentJournal.snapshot journal).AgentProjections

            let lifecycle =
                AgentProjection.tryFind managerSessionId snapshot
                |> Option.bind (fun session -> session.ManagerLife)
                |> Option.defaultValue ManagerLifecycleProjection.empty

            let request =
                lifecycle.CurrentLife
                |> Option.bind (fun life -> life.ActiveFinality)
                |> Option.filter (fun r -> r.RequestId = requestId)

            // Barrier: the durable one when this member already enlisted
            // (crash re-entry), else a fresh one.
            let existingMember =
                request
                |> Option.bind (fun r -> slot.ReviewerSessionId |> Option.bind (fun sid -> Map.tryFind sid r.Members))

            let barrierId =
                match existingMember with
                | Some memberRef -> memberRef.BarrierId
                | None -> ReviewBarrierId.create (Guid.NewGuid().ToString("N"))

            // Step 1+2: hidden session + durable enlist (reuse for old).
            let! sessionResult =
                match slot.ReviewerSessionId with
                | Some existing ->
                    runtime.AdoptChild(slot.AgentId, existing)
                    Task.FromResult(Ok(existing, false))
                | None ->
                    // New reviewer: fork with deferred send — the session is
                    // created now, the first prompt waits for the barrier.
                    task {
                        let! forkResult =
                            runtime.Fork(
                                slot.AgentId,
                                Role.Reviewer,
                                reviewerAgentName,
                                HostReviewPrompt.OpeningAssignment,
                                None,
                                ownership = Fact.HandleOwnership.HostOwnedHidden,
                                deferSend = true
                            )

                        match forkResult with
                        | Error error -> return Error error
                        | Ok _ ->
                            match runtime.TryChildSession slot.AgentId with
                            | None -> return Error "reviewer session was not created"
                            | Some childId -> return Ok(childId, true)
                    }

            match sessionResult with
            | Error error -> return Error error
            | Ok(reviewerSessionId, isNew) ->
                if existingMember.IsNone then
                    appendLifecycle
                        journal
                        (ManagerLifecycleFact.FinalityReviewerEnlisted
                            {| SessionId = managerSessionId
                               LifeId = lifeId
                               RequestId = requestId
                               ReviewerSessionId = reviewerSessionId
                               ReviewerOrdinal = slot.ReviewerOrdinal
                               BarrierId = barrierId
                               GitTreeHash = requestTree
                               IsNewReviewer = isNew |})

                // Step 3: barrier — durable, before any assignment byte.
                match
                    ReviewBarrier.openBarrier (Some journal) managerSessionId reviewerSessionId barrierId requestTree
                with
                | Error error -> return Error error
                | Ok() ->
                    // Step 4: first assignment (GLORY-040: never before the barrier).
                    let! assignment =
                        if isNew then
                            task {
                                let! sent = runtime.SendDeferredFirstPrompt slot.AgentId

                                match sent with
                                | Error error -> return Error error
                                | Ok() -> return Ok()
                            }
                        else
                            // Reused session: Fork's existing-child path installs
                            // the fresh run and sends the same envelope the child
                            // would get as a new session (idempotent claim key).
                            task {
                                let! forked =
                                    runtime.Fork(
                                        slot.AgentId,
                                        Role.Reviewer,
                                        reviewerAgentName,
                                        HostReviewPrompt.OpeningAssignment,
                                        None
                                    )

                                match forked with
                                | Error error -> return Error error
                                | Ok _ -> return Ok()
                            }

                    match assignment with
                    | Error error -> return Error error
                    | Ok() ->
                        return
                            Ok
                                { ReviewerSessionId = reviewerSessionId
                                  BarrierId = barrierId
                                  ReviewerOrdinal = slot.ReviewerOrdinal
                                  AgentId = slot.AgentId }
        }

    /// GLORY-040 step 6: start the Finality workflow after a legal suicide was
    /// accepted. Synchronous execution: every outcome lands on the journal
    /// before any side effect.
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
                    let snapshot = AgentJournal.snapshot journal

                    let lifecycle =
                        AgentProjection.tryFind managerSessionId snapshot.AgentProjections
                        |> Option.bind (fun session -> session.ManagerLife)
                        |> Option.defaultValue ManagerLifecycleProjection.empty

                    let life = lifecycle.CurrentLife |> Option.filter (fun life -> life.LifeId = lifeId)

                    match life with
                    | None -> return Undecided ManagerLifecyclePrompt.FinalityUndecidable
                    | Some life ->
                        let request =
                            life.ActiveFinality |> Option.filter (fun r -> r.RequestId = requestId)

                        match request with
                        | None -> return Undecided ManagerLifecyclePrompt.FinalityUndecidable
                        | Some request ->
                            let slots = FinalityReviewCohort.rosterOf snapshot.AgentProjections life request

                            // GLORY-002: a private runtime whose handles are
                            // HostOwnedHidden — the Reviewer never enters the
                            // Manager's list/join/guard or parent recovery.
                            let runtime =
                                HostForkRuntime(
                                    managerSessionId,
                                    scope.Sessions,
                                    ?journal = scope.Journal,
                                    onChildCreated =
                                        (fun _ _ childId ->
                                            scope.SessionParents.[SessionId.value childId] <-
                                                SessionId.value managerSessionId),
                                    onChildCreatedDir =
                                        (fun _ childId directory ->
                                            directory
                                            |> Option.iter (fun path ->
                                                scope.RegisterDirectory(SessionId.value childId, path))),
                                    directoryFor = (fun _ -> scope.DirectoryFor(SessionId.value managerSessionId)),
                                    onRunStarted = scope.RunStarted,
                                    parentWorkRecordFor =
                                        (fun _ ->
                                            XTraceCapture.lifecycleWorkRecord (Some journal) managerSessionId true),
                                    childWorkRecordFor = (fun _ -> None),
                                    ?sessionSnapshot = scope.Snapshot,
                                    managerOpensReviewBarrier = false,
                                    ownership = Fact.HandleOwnership.HostOwnedHidden
                                )

                            let managerProfile = scope.ActiveProfileFor managerSessionId

                            let reviewerTier =
                                managerProfile
                                |> Option.map (fun profile -> profile.SelectedTier)
                                |> Option.defaultValue AgentTier.Deep

                            let reviewerAgentName = ManagedAgent.nameOf reviewerTier Role.Reviewer

                            let! enlisted =
                                slots
                                |> List.fold
                                    (fun (acc: Task<Result<EnlistedMember list, string>>) slot ->
                                        task {
                                            let! previous = acc

                                            match previous with
                                            | Error error -> return Error error
                                            | Ok members ->
                                                let! next =
                                                    enlistMember
                                                        scope
                                                        journal
                                                        runtime
                                                        managerSessionId
                                                        lifeId
                                                        requestId
                                                        requestTree
                                                        reviewerAgentName
                                                        slot

                                                match next with
                                                | Error error -> return Error error
                                                | Ok enlistedMember -> return Ok(members @ [ enlistedMember ])
                                        })
                                    (Task.FromResult(Ok []))

                            match enlisted with
                            | Error error ->
                                let reviewer, barrier =
                                    match slots with
                                    | first :: _ when first.ReviewerSessionId.IsSome ->
                                        first.ReviewerSessionId.Value,
                                        ReviewBarrierId.create (Guid.NewGuid().ToString("N"))
                                    | _ -> managerSessionId, ReviewBarrierId.create (Guid.NewGuid().ToString("N"))

                                return!
                                    concludeUndecided
                                        scope
                                        journal
                                        managerSessionId
                                        lifeId
                                        requestId
                                        requestTree
                                        reviewer
                                        barrier
                            | Ok members ->
                                let cancel = CancelToken()

                                let memberTasks =
                                    members
                                    |> List.map (fun memberInfo ->
                                        driveMember
                                            scope
                                            journal
                                            cancel
                                            runtime
                                            managerSessionId
                                            memberInfo
                                            requestTree
                                            reviewerTimeoutMs)

                                let! outcome =
                                    concurrentAllOrShortCircuit
                                        cancel
                                        (function
                                        | Ok(HostReviewProgram.HostReviewOutcome.RevisionRequired _) -> true
                                        | Ok(HostReviewProgram.HostReviewOutcome.Confirmed _) -> false
                                        | Error _ -> false)
                                        memberTasks

                                // Defensive: ensure cancel is set even if the
                                // short-circuit path never ran (all confirmed).
                                cancel.Cancel()

                                match outcome with
                                | Choice1Of2(Ok(HostReviewProgram.HostReviewOutcome.RevisionRequired(reviewerId,
                                                                                                     barrier,
                                                                                                     _tree,
                                                                                                     record))) ->
                                    return!
                                        concludeRejection
                                            scope
                                            journal
                                            managerSessionId
                                            lifeId
                                            requestId
                                            reviewerId
                                            barrier
                                            requestTree
                                            record
                                | Choice2Of2 results when
                                    List.forall
                                        (function
                                        | Ok(HostReviewProgram.HostReviewOutcome.Confirmed _) -> true
                                        | _ -> false)
                                        results
                                    ->
                                    return!
                                        concludeBlessing
                                            scope
                                            journal
                                            managerSessionId
                                            lifeId
                                            requestId
                                            members
                                            requestTree
                                | _ ->
                                    let reviewer, barrier =
                                        match members with
                                        | first :: _ -> first.ReviewerSessionId, first.BarrierId
                                        | [] -> managerSessionId, ReviewBarrierId.create (Guid.NewGuid().ToString("N"))

                                    return!
                                        concludeUndecided
                                            scope
                                            journal
                                            managerSessionId
                                            lifeId
                                            requestId
                                            requestTree
                                            reviewer
                                            barrier
                with ex ->
                    // Exception boundary: never leak an exception out of the
                    // tool call that accepted the suicide.
                    Diagnostic.emit
                        "finality"
                        [ "session_id", SessionId.value managerSessionId; "provider_error", ex.Message ]

                    return Undecided ManagerLifecyclePrompt.FinalityUndecidable
        }
