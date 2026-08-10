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
open Wanxiangshu.Finality
open Wanxiangshu.Review

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

    // Temporary re-exports — Application/Finality owns these (rabbit §12.2).
    // Call sites keep `FinalityController.FinalityOutcome` until the controller
    // body itself moves and this module is deleted.
    type FinalityOutcome = Wanxiangshu.Finality.FinalityOutcome
    type EnlistedMember = Wanxiangshu.Finality.EnlistedMember

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
            | ManagerLifecycleFact.FinalitySiblingSteered payload -> payload.SessionId
            | ManagerLifecycleFact.FinalityBlessed payload -> payload.SessionId
            | ManagerLifecycleFact.FinalityUndecided payload -> payload.SessionId
            | ManagerLifecycleFact.LifeCompleted payload -> payload.SessionId

        AgentJournal.appendManagerLifecycle (StreamId.Session sessionId) fact journal
        |> Result.mapError (fun failure ->
            raise (
                InvalidOperationException(sprintf "Finality append failed: %s" (JournalAppendFailure.describe failure))
            ))
        |> ignore

    type private RecordReadiness =
        | RecordReady of string
        | AwaitJournal
        | RecordUnavailable of string

    let private hasRenderedWorkLog (record: string) =
        // Raw LWR titles are plain; `# ` is injected only by SyntheticToml.comment on wire.
        let marker = "Work log\n"
        let start = record.IndexOf(marker, StringComparison.Ordinal)

        start >= 0
        && not (String.IsNullOrWhiteSpace(record.Substring(start + marker.Length)))

    let private materializeRecord
        (journal: AgentJournal)
        (snapshot: ProjectionSet)
        (reviewerSessionId: SessionId)
        (terminalFrontier: ReviewTerminalFrontier option)
        (requiresWorkLog: bool)
        =
        let terminalOverride =
            terminalFrontier
            |> Option.map (fun frontier -> frontier.TerminalRef, frontier.TerminalDigest)

        // Finality cohort LWR (blessing bundle / rejection wound) must be the
        // full canonical record from openingEnd — not the coverage-trimmed
        // incremental gap. Historical reviewers reused after REVISE already
        // have blog.Coverage at a prior frontier; trimming would drop their
        // deliberation from the blessing bundle (GLORY-044/060).
        let fullCanonicalCoverage = Some { IngestedThrough = XTrace.originCursor }

        match
            XTraceCapture.lifecycleWorkRecordFromSnapshotWithTerminal
                journal
                snapshot
                reviewerSessionId
                false
                terminalOverride
                fullCanonicalCoverage
        with
        | Some record when
            not (String.IsNullOrWhiteSpace record)
            && (not requiresWorkLog || hasRenderedWorkLog record)
            ->
            RecordReady record
        | Some _ -> RecordUnavailable "canonical LWR has no rendered work log"
        | None -> RecordUnavailable "canonical LWR is unavailable"

    let private coverageCanAdvance (snapshot: ProjectionSet) (reviewerSessionId: SessionId) =
        match SessionAssociationProjection.tryBloggerOf reviewerSessionId snapshot.AgentProjections.Associations with
        | None -> true
        | Some bloggerSessionId ->
            match Map.tryFind bloggerSessionId snapshot.AgentProjections.HandleByChildSession with
            | Some { Lifecycle = HandleLifecycle.Abandoned _ }
            | Some { Lifecycle = HandleLifecycle.Retired } -> false
            | _ -> true

    let private recordReadiness
        (journal: AgentJournal)
        (snapshot: ProjectionSet)
        (reviewerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        (requiresTerminalFrontier: bool)
        =
        match AgentProjection.tryFind reviewerSessionId snapshot.AgentProjections with
        | None -> RecordUnavailable "reviewer projection is unavailable"
        | Some session ->
            match session.ReviewGuard with
            | None -> RecordUnavailable "review barrier is unavailable"
            | Some guard when guard.CurrentBarrierId <> Some barrierId ->
                RecordUnavailable "review barrier no longer matches the finality member"
            | Some guard ->
                match guard.TerminalFrontier with
                | Some frontier when frontier.BarrierId <> barrierId ->
                    RecordUnavailable "terminal frontier no longer matches the finality barrier"
                | Some frontier ->
                    // Readiness is materialize-of-work-log, not coverage >=
                    // frontier.Sequence. Frontier is exclusive (lastPart+1);
                    // real Blogger coverage tops out at lastPart, so the old
                    // gate hung forever when coverageCanAdvance stayed true
                    // (GLORY-073 / manager-unhappy-path FinalityRejected).
                    match materializeRecord journal snapshot reviewerSessionId (Some frontier) true with
                    | RecordReady record -> RecordReady record
                    | RecordUnavailable _ when coverageCanAdvance snapshot reviewerSessionId -> AwaitJournal
                    | RecordUnavailable reason -> RecordUnavailable reason
                    | AwaitJournal -> AwaitJournal
                | None when requiresTerminalFrontier -> AwaitJournal
                | None -> materializeRecord journal snapshot reviewerSessionId None false

    let private awaitRecordReady
        (journal: AgentJournal)
        (reviewerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        : Task<Result<string, string>> =
        let rec loop () =
            task {
                let snapshot, revision = AgentJournal.snapshotWithRevision journal

                match recordReadiness journal snapshot reviewerSessionId barrierId true with
                | RecordReady record -> return Ok record
                | RecordUnavailable reason -> return Error reason
                | AwaitJournal ->
                    let! _ = AgentJournal.awaitChangeFrom revision journal
                    return! loop ()
            }

        loop ()

    let private awaitBlessingRecords
        (journal: AgentJournal)
        (members: EnlistedMember list)
        : Task<Result<(int * string) list, string>> =
        let ordered = members |> List.sortBy (fun memberInfo -> memberInfo.ReviewerOrdinal)

        let rec loop () =
            task {
                let snapshot, revision = AgentJournal.snapshotWithRevision journal

                let readiness =
                    ordered
                    |> List.map (fun memberInfo ->
                        memberInfo,
                        recordReadiness journal snapshot memberInfo.ReviewerSessionId memberInfo.BarrierId false)

                let unavailable =
                    readiness
                    |> List.tryPick (fun (_, state) ->
                        match state with
                        | RecordUnavailable reason -> Some reason
                        | _ -> None)

                match unavailable with
                | Some reason -> return Error reason
                | None ->
                    let pending =
                        readiness
                        |> List.exists (fun (_, state) ->
                            match state with
                            | AwaitJournal -> true
                            | _ -> false)

                    if pending then
                        let pendingMembers =
                            readiness
                            |> List.choose (fun (memberInfo, state) ->
                                match state with
                                | AwaitJournal -> Some("reviewer", SessionId.value memberInfo.ReviewerSessionId)
                                | _ -> None)

                        let descriptor =
                            DiagnosticWait.create
                                "finality-blessing-records"
                                (CausalOwner.create "FinalityController" [])
                                pendingMembers
                                (ExternalProducer("journal-work-log", pendingMembers))
                                [ WaitEscape.ProcessLifetime; WaitEscape.OpenEndedExternal ]
                                "FinalityController.awaitBlessingRecords"

                        let! _ =
                            CausalAwait.awaitTask
                                CausalWaitHub.observer
                                descriptor
                                (AgentJournal.awaitChangeFrom revision journal)

                        return! loop ()
                    else
                        let records =
                            readiness
                            |> List.choose (fun (memberInfo, state) ->
                                match state with
                                | RecordReady record -> Some(memberInfo.ReviewerOrdinal, record)
                                | AwaitJournal
                                | RecordUnavailable _ -> None)

                        return Ok records
            }

        loop ()

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

                let descriptor =
                    DiagnosticWait.create
                        "reviewer-terminal"
                        (CausalOwner.create "finality-request" [ "reviewer", SessionId.value reviewerSessionId ])
                        [ "reviewer", SessionId.value reviewerSessionId ]
                        (WorkflowProducer(
                            CausalOwner.create "reviewer-workflow" [ "session", SessionId.value reviewerSessionId ]
                        ))
                        [ WaitEscape.DeadlineAt(DateTimeOffset.UtcNow.AddMilliseconds(float timeoutMs))
                          WaitEscape.CancelledBy(CausalOwner.create "finality-cancel" []) ]
                        "FinalityController.awaitReviewer"

                return!
                    CausalAwait.awaitTask
                        CausalWaitHub.observer
                        descriptor
                        (emitJsExpr (finished, timedOut, cancelled) "Promise.race([$0, $1, $2])"
                        : Task<Result<unit, string>>)
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

    /// True when this member already has a durable RevisionWitness for its
    /// barrier (GLORY-044): cancel must not erase an already-landed REVISE.
    let private hasDurableRevisionRequired
        (journal: AgentJournal)
        (reviewerSessionId: SessionId)
        (barrierId: ReviewBarrierId)
        =
        let snapshot = AgentJournal.snapshot journal

        AgentProjection.tryFind reviewerSessionId snapshot.AgentProjections
        |> Option.bind (fun session -> session.ReviewGuard)
        |> Option.exists (fun guard ->
            match guard.CurrentBarrierId, guard.Witness with
            | Some current, ReviewWitness.RevisionWitness _ when current = barrierId -> true
            | _ -> false)

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
        : Task<Result<ReviewBarrierOutcome, ReviewBarrierFailure>> =
        let awaitOrCancel () =
            task {
                // Cancel after a durable REVISE must still return Ok so reverify
                // can readOutcome → RevisionRequired (not CannotAwaitReviewer).
                let promoteCancelled error =
                    if hasDurableRevisionRequired journal memberInfo.ReviewerSessionId memberInfo.BarrierId then
                        Ok()
                    else
                        Error error

                match! awaitReviewer scope timeoutMs cancel memberInfo.ReviewerSessionId with
                | Error error when cancel.IsCancelled -> return promoteCancelled error
                | Error error -> return Error error
                | Ok() when cancel.IsCancelled -> return promoteCancelled "review attempt cancelled"
                | Ok() -> return Ok()
            }

        ReviewBarrierWorkflow.reverify
            (Some journal)
            { ForkReviewer = fun () -> Task.FromResult(Ok memberInfo.ReviewerSessionId)
              AwaitReviewer = awaitOrCancel }
            managerSessionId
            memberInfo.BarrierId
            tree

    /// GLORY-044: concurrent fan-out with immediate REVISE short-circuit. All
    /// drivers start together; the first Revision result wins the race, cancels
    /// siblings immediately, and remaining drivers stop before their next effect.
    /// Short-circuit results are still accumulated; the Choice resolves only after
    /// every started driver finishes so later durable REVISE can be steered.
    /// Durable Reviewer sessions are never disposed here (GLORY-055).
    let private concurrentAllOrShortCircuit
        (cancel: CancelToken)
        (isShortCircuit: 'a -> bool)
        (tasks: Task<'a> list)
        : Task<Choice<'a * 'a list, 'a list>> =
        task {
            let tcs =
                TaskCompletionSource<Choice<'a * 'a list, 'a list>>(TaskCreationOptions.RunContinuationsAsynchronously)

            let remaining = ref (List.length tasks)
            let results = ResizeArray<'a>()
            let shortCircuitWinner = ref None

            let decide (result: 'a) =
                results.Add result

                if isShortCircuit result then
                    // Cancel siblings before the outer CE resumes — otherwise a
                    // Perfect-pending sibling can still send its challenge after
                    // REVISE has already won the race.
                    match shortCircuitWinner.Value with
                    | None ->
                        shortCircuitWinner.Value <- Some result
                        cancel.Cancel()
                    | Some _ -> ()

                remaining.Value <- remaining.Value - 1

                if remaining.Value = 0 then
                    let all = List.ofSeq results

                    match shortCircuitWinner.Value with
                    | Some winner -> AsyncSupport.trySetResult tcs (Choice1Of2(winner, all)) |> ignore
                    | None -> AsyncSupport.trySetResult tcs (Choice2Of2 all) |> ignore

            if List.isEmpty tasks then
                AsyncSupport.trySetResult tcs (Choice2Of2 []) |> ignore
            else
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

    /// GLORY-044: rejecting-reviewer record-ready + WriteBlob must complete
    /// before any FinalitySiblingSteered. Hard failure → caller Undecided with
    /// zero sibling steered facts (不得 orphan SiblingSteered without steers).
    let private stagePrimaryRejectionRecord
        (journal: AgentJournal)
        (rejectingReviewer: SessionId)
        (barrierId: ReviewBarrierId)
        : Task<Result<string * BlobWriteReceipt, string>> =
        task {
            let! record = awaitRecordReady journal rejectingReviewer barrierId

            match record with
            | Error reason -> return Error reason
            | Ok workRecord ->
                match journal.WriteBlob workRecord with
                | Error reason -> return Error reason
                | Ok blob -> return Ok(workRecord, blob)
        }

    /// The wound path (GLORY-051/052/053): seal FinalityRejected from an already
    /// staged primary blob. The reviewer stays ungraduated; session preserved.
    let private sealFinalityRejected
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (rejectingReviewer: SessionId)
        (barrierId: ReviewBarrierId)
        (requestTree: GitTreeHash)
        (workRecord: string)
        (blob: BlobWriteReceipt)
        : FinalityOutcome =
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

        Rejected(FinalityPrompt.rejected workRecord)

    /// GLORY-044/REVIEW-002: wait until every durable sibling is RecordReady, or
    /// fail closed on hard RecordUnavailable (no silent drop; no AwaitJournal hang
    /// when coverageCannotAdvance — abandoned companion / retired blogger).
    let private awaitDurableSiblingRecords
        (journal: AgentJournal)
        (siblings: (SessionId * ReviewBarrierId) list)
        : Task<Result<(SessionId * ReviewBarrierId * string) list, string>> =
        let rec loop () =
            task {
                if List.isEmpty siblings then
                    return Ok []
                else
                    let snapshot, revision = AgentJournal.snapshotWithRevision journal

                    let readiness =
                        siblings
                        |> List.map (fun (sid, barrierId) ->
                            sid, barrierId, recordReadiness journal snapshot sid barrierId true)

                    let unavailable =
                        readiness
                        |> List.tryPick (fun (_, _, state) ->
                            match state with
                            | RecordUnavailable reason -> Some reason
                            | _ -> None)

                    match unavailable with
                    | Some reason -> return Error reason
                    | None ->
                        let pending =
                            readiness
                            |> List.exists (fun (_, _, state) ->
                                match state with
                                | AwaitJournal -> true
                                | _ -> false)

                        if pending then
                            let! _ = AgentJournal.awaitChangeFrom revision journal
                            return! loop ()
                        else
                            let records =
                                readiness
                                |> List.choose (fun (sid, barrierId, state) ->
                                    match state with
                                    | RecordReady record -> Some(sid, barrierId, record)
                                    | AwaitJournal
                                    | RecordUnavailable _ -> None)

                            if List.length records <> List.length siblings then
                                return Error "durable sibling record readiness is incomplete"
                            else
                                return Ok records
            }

        loop ()

    let private tryActiveFinality
        (snapshot: ProjectionSet)
        (managerSessionId: SessionId)
        (requestId: FinalityRequestId)
        =
        AgentProjection.tryFind managerSessionId snapshot.AgentProjections
        |> Option.bind (fun session -> session.ManagerLife)
        |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)
        |> Option.bind (fun life -> life.ActiveFinality)
        |> Option.filter (fun active -> active.RequestId = requestId)

    /// WriteBlob+prepare ALL durable siblings first; only then append ALL
    /// FinalitySiblingSteered (or none). Mid-list WriteBlob failure must not leave
    /// partial FinalitySiblingSteered before concludeUndecided (still Open).
    let private commitSiblingSteerFacts
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (requestTree: GitTreeHash)
        (records: (SessionId * ReviewBarrierId * string) list)
        : Result<(SessionId * string) list, string> =
        let snapshot = AgentJournal.snapshot journal

        let existingSteers =
            tryActiveFinality snapshot managerSessionId requestId
            |> Option.map (fun active -> active.SiblingSteers)
            |> Option.defaultValue Map.empty

        // Phase 1: materialize every blob (or reuse existing steer text). No facts yet.
        let preparedResult =
            records
            |> List.fold
                (fun acc (reviewerSessionId, barrierId, workRecord) ->
                    match acc with
                    | Error reason -> Error reason
                    | Ok prepared ->
                        match Map.tryFind reviewerSessionId existingSteers with
                        | Some evidence ->
                            match journal.Writer.BlobWriter.Read evidence.WorkRecordRef with
                            | Ok text -> Ok(prepared @ [ reviewerSessionId, barrierId, text, None ])
                            | Error reason -> Error reason
                        | None ->
                            match journal.WriteBlob workRecord with
                            | Error reason -> Error reason
                            | Ok blob -> Ok(prepared @ [ reviewerSessionId, barrierId, workRecord, Some blob ]))
                (Ok [])

        match preparedResult with
        | Error reason -> Error reason
        | Ok prepared ->
            // Phase 2: append every new FinalitySiblingSteered, or none if phase 1 failed.
            for reviewerSessionId, barrierId, _, blobOpt in prepared do
                match blobOpt with
                | None -> ()
                | Some blob ->
                    appendLifecycle
                        journal
                        (ManagerLifecycleFact.FinalitySiblingSteered
                            {| SessionId = managerSessionId
                               LifeId = lifeId
                               RequestId = requestId
                               ReviewerSessionId = reviewerSessionId
                               BarrierId = barrierId
                               GitTreeHash = requestTree
                               WorkRecordRef = blob.BlobRef
                               WorkRecordDigest = blob.BlobDigest |})

            Ok(prepared |> List.map (fun (sid, _, text, _) -> sid, text))

    let private sendSiblingSteerContinuations
        (scope: ToolRuntimeScope)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (prepared: (SessionId * string) list)
        : Task<unit> =
        task {
            let directory = scope.DirectoryFor(SessionId.value managerSessionId)

            for _, workRecord in prepared do
                let prompt = FinalityPrompt.steer workRecord

                let! _ =
                    HostSessionNudge.sendContinuation
                        scope.Sessions
                        managerSessionId
                        prompt
                        PromptAuthority.ContinuationKind.FinalitySteer
                        directory
                        (Some journal)

                ()
        }

    /// GLORY-044: seal FinalityRejected only after (1) every durable sibling is
    /// record-ready, (2) primary rejecting LWR is staged (record-ready+WriteBlob),
    /// then (3) atomic sibling WriteBlob + all FinalitySiblingSteered, (4) seal
    /// Rejected from the staged primary blob, (5) sendSiblingSteerContinuations.
    /// Primary hard-fail before sibling facts → Undecided with zero SiblingSteered
    /// (avoids orphaning steered facts when Manager suicides under a new ToolCallId).
    let private concludeRejectionAccountingSiblings
        (scope: ToolRuntimeScope)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        (rejectingReviewer: SessionId)
        (barrierId: ReviewBarrierId)
        (requestTree: GitTreeHash)
        (siblings: (SessionId * ReviewBarrierId) list)
        : Task<FinalityOutcome> =
        task {
            let! siblingRecords = awaitDurableSiblingRecords journal siblings

            match siblingRecords with
            | Error _ ->
                return!
                    concludeUndecided
                        scope
                        journal
                        managerSessionId
                        lifeId
                        requestId
                        requestTree
                        rejectingReviewer
                        barrierId
            | Ok records ->
                let! primaryStaged = stagePrimaryRejectionRecord journal rejectingReviewer barrierId

                match primaryStaged with
                | Error _ ->
                    // Primary hard-fail: no FinalitySiblingSteered yet.
                    return!
                        concludeUndecided
                            scope
                            journal
                            managerSessionId
                            lifeId
                            requestId
                            requestTree
                            rejectingReviewer
                            barrierId
                | Ok(workRecord, primaryBlob) ->
                    match commitSiblingSteerFacts journal managerSessionId lifeId requestId requestTree records with
                    | Error _ ->
                        return!
                            concludeUndecided
                                scope
                                journal
                                managerSessionId
                                lifeId
                                requestId
                                requestTree
                                rejectingReviewer
                                barrierId
                    | Ok prepared ->
                        let outcome =
                            sealFinalityRejected
                                journal
                                managerSessionId
                                lifeId
                                requestId
                                rejectingReviewer
                                barrierId
                                requestTree
                                workRecord
                                primaryBlob

                        // SiblingSteered already committed: always deliver steers
                        // (Rejected is the only seal path after primary preflight).
                        do! sendSiblingSteerContinuations scope journal managerSessionId prepared
                        return outcome
        }

    /// GLORY-073 resume: replay already-committed sibling steers. Blob first;
    /// on miss, rematerialize from journal. No silent drop of accounted siblings;
    /// unaccounted members are not invented here. Detached send is best-effort
    /// only once content is deliverable.
    let private replaySiblingSteer
        (scope: ToolRuntimeScope)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (requestId: FinalityRequestId)
        (reviewerSessionId: SessionId)
        : Task<unit> =
        task {
            let snapshot = AgentJournal.snapshot journal

            match
                tryActiveFinality snapshot managerSessionId requestId
                |> Option.bind (fun active -> Map.tryFind reviewerSessionId active.SiblingSteers)
            with
            | None -> ()
            | Some evidence ->
                let workRecordOpt =
                    match journal.Writer.BlobWriter.Read evidence.WorkRecordRef with
                    | Ok workRecord -> Some workRecord
                    | Error _ ->
                        // Blob gone: one-shot rematerialize from durable journal
                        // evidence (no await loop — request is already sealed).
                        match recordReadiness journal snapshot reviewerSessionId evidence.BarrierId true with
                        | RecordReady record -> Some record
                        | AwaitJournal
                        | RecordUnavailable _ -> None

                match workRecordOpt with
                | Some workRecord ->
                    do! sendSiblingSteerContinuations scope journal managerSessionId [ reviewerSessionId, workRecord ]
                | None ->
                    // Accounted sibling still undeliverable: Manager-visible
                    // comment-only failure — do not pretend the evidence arrived.
                    let directory = scope.DirectoryFor(SessionId.value managerSessionId)

                    let! _ =
                        HostSessionNudge.sendContinuation
                            scope.Sessions
                            managerSessionId
                            FinalityPrompt.steerUnavailable
                            PromptAuthority.ContinuationKind.FinalitySteer
                            directory
                            (Some journal)

                    ()
        }

    let private steerSiblingRevisions
        (scope: ToolRuntimeScope)
        (journal: AgentJournal)
        (managerSessionId: SessionId)
        (requestId: FinalityRequestId)
        (siblings: (SessionId * ReviewBarrierId) list)
        : Task<unit> =
        task {
            for reviewerSessionId, _ in siblings do
                do! replaySiblingSteer scope journal managerSessionId requestId reviewerSessionId
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
            let undecided () =
                let reviewer, barrier =
                    match members with
                    | first :: _ -> first.ReviewerSessionId, first.BarrierId
                    | [] -> managerSessionId, ReviewBarrierId.create (Guid.NewGuid().ToString("N"))

                concludeUndecided scope journal managerSessionId lifeId requestId requestTree reviewer barrier

            if not (treeUnchanged scope managerSessionId requestTree) then
                // GLORY-059: the tree moved under the confirmed witnesses; this
                // success is void. Close the request fail-closed.
                return! undecided ()
            else
                let! records = awaitBlessingRecords journal members

                match records with
                | Error _ -> return! undecided ()
                | Ok orderedRecords when List.length orderedRecords <> List.length members -> return! undecided ()
                | Ok orderedRecords when not (treeUnchanged scope managerSessionId requestTree) -> return! undecided ()
                | Ok orderedRecords ->
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

    let private pendingRevision (snapshot: ProjectionSet) (request: FinalityRequestProjection) =
        request.Members
        |> Map.toList
        |> List.tryPick (fun (reviewerSessionId, memberRef) ->
            AgentProjection.tryFind reviewerSessionId snapshot.AgentProjections
            |> Option.bind (fun session -> session.ReviewGuard)
            |> Option.bind (fun guard ->
                match guard.CurrentBarrierId, guard.Witness with
                | Some barrierId, ReviewWitness.RevisionWitness _ when barrierId = memberRef.BarrierId ->
                    Some(reviewerSessionId, barrierId)
                | _ -> None))

    /// Durable REVISE members other than the rejecting winner (GLORY-044 siblings).
    let private durableRevisionSiblings
        (snapshot: ProjectionSet)
        (request: FinalityRequestProjection)
        (rejectingReviewer: SessionId)
        =
        request.Members
        |> Map.toList
        |> List.choose (fun (reviewerSessionId, memberRef) ->
            if reviewerSessionId = rejectingReviewer then
                None
            else
                AgentProjection.tryFind reviewerSessionId snapshot.AgentProjections
                |> Option.bind (fun session -> session.ReviewGuard)
                |> Option.bind (fun guard ->
                    match guard.CurrentBarrierId, guard.Witness with
                    | Some barrierId, ReviewWitness.RevisionWitness _ when barrierId = memberRef.BarrierId ->
                        Some(reviewerSessionId, barrierId)
                    | _ -> None))

    /// GLORY-073: a replay resumes a durable REVISE at its frozen frontier. It
    /// never re-enlists the cohort or replays a Reviewer continuation.
    /// GLORY-044: Open path uses concludeRejectionAccountingSiblings (primary
    /// preflight → SiblingSteered → seal Rejected → send steers); resolved path
    /// best-effort replays already-committed FinalitySiblingSteered continuations.
    let resumeDurableRevise
        (scope: ToolRuntimeScope)
        (managerSessionId: SessionId)
        (lifeId: ManagerLifeId)
        (requestId: FinalityRequestId)
        : Task<FinalityOutcome option> =
        task {
            match scope.Journal with
            | None -> return None
            | Some journal ->
                try
                    let snapshot = AgentJournal.snapshot journal

                    let lifeOpt =
                        AgentProjection.tryFind managerSessionId snapshot.AgentProjections
                        |> Option.bind (fun session -> session.ManagerLife)
                        |> Option.bind (fun lifecycle -> lifecycle.CurrentLife)
                        |> Option.filter (fun life -> life.LifeId = lifeId)

                    let requestOpt =
                        lifeOpt
                        |> Option.bind (fun life -> life.ActiveFinality)
                        |> Option.filter (fun active -> active.RequestId = requestId)

                    match requestOpt with
                    | None -> return None
                    | Some activeRequest when ManagerLifecycleProjection.isOpen activeRequest ->
                        match pendingRevision snapshot activeRequest with
                        | None -> return None
                        | Some(reviewerSessionId, barrierId) ->
                            // BEFORE sealing Rejected: account durable siblings
                            // (journal ∪ prior SiblingSteers). Hard materialization
                            // failure → Undecided (FinalityUndecided is Open-only).
                            let siblings =
                                durableRevisionSiblings snapshot activeRequest reviewerSessionId
                                @ (activeRequest.SiblingSteers
                                   |> Map.toList
                                   |> List.map (fun (sid, evidence) -> sid, evidence.BarrierId)
                                   |> List.filter (fun (sid, _) -> sid <> reviewerSessionId))
                                |> List.distinctBy fst

                            let! outcome =
                                concludeRejectionAccountingSiblings
                                    scope
                                    journal
                                    managerSessionId
                                    lifeId
                                    requestId
                                    reviewerSessionId
                                    barrierId
                                    activeRequest.GitTreeHash
                                    siblings

                            return Some outcome
                    | Some activeRequest ->
                        // Request already resolved: replay committed sibling steers only
                        // (revise-close already fail-closed or fact-accounted them).
                        let siblings =
                            activeRequest.SiblingSteers
                            |> Map.toList
                            |> List.map (fun (sid, evidence) -> sid, evidence.BarrierId)

                        if not (List.isEmpty siblings) then
                            do! steerSiblingRevisions scope journal managerSessionId requestId siblings

                        return None
                with _ ->
                    return Some(Undecided ManagerLifecyclePrompt.FinalityUndecidable)
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
                                        | Ok(ReviewBarrierOutcome.RevisionRequired _) -> true
                                        | Ok(ReviewBarrierOutcome.Confirmed _) -> false
                                        | Error _ -> false)
                                        memberTasks

                                // Defensive: ensure cancel is set even if the
                                // short-circuit path never ran (all confirmed).
                                cancel.Cancel()

                                match outcome with
                                | Choice1Of2(Ok(ReviewBarrierOutcome.RevisionRequired(reviewerId,
                                                                                      barrier,
                                                                                      _tree)),
                                             allResults) ->
                                    // Race allResults alone drops cancelled-but-durable
                                    // siblings (awaitOrCancel Error). Union journal
                                    // durableRevisionSiblings BEFORE sealing Rejected —
                                    // FinalityUndecided is Open-only (GLORY-044 soft-drop).
                                    let fromRace =
                                        allResults
                                        |> List.choose (function
                                            | Ok(ReviewBarrierOutcome.RevisionRequired(sid, bid, _)) when
                                                sid <> reviewerId
                                                ->
                                                Some(sid, bid)
                                            | _ -> None)

                                    let before = AgentJournal.snapshot journal

                                    let siblings =
                                        match tryActiveFinality before managerSessionId requestId with
                                        | Some activeRequest ->
                                            durableRevisionSiblings before activeRequest reviewerId @ fromRace
                                            |> List.distinctBy fst
                                        | None -> fromRace

                                    return!
                                        concludeRejectionAccountingSiblings
                                            scope
                                            journal
                                            managerSessionId
                                            lifeId
                                            requestId
                                            reviewerId
                                            barrier
                                            requestTree
                                            siblings
                                | Choice2Of2 results when
                                    List.forall
                                        (function
                                        | Ok(ReviewBarrierOutcome.Confirmed _) -> true
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
