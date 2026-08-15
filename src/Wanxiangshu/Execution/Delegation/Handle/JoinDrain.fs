namespace Wanxiangshu.Execution.Delegation.Handle

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.Host
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Attachment
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Interaction.Repair
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

open System
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Execution.Delegation.Fork.ChildRecovery
open Wanxiangshu.Host
open Wanxiangshu.Foundation
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Persistence.Journal

/// EXEC-009 + EXEC-018 + clean-break: pure durable join drain.
///
/// Order: HandleRecord → blob body → DurableCompletionDecode → branch:
///   Current → fromDecoded → CAS HandleRetired → AgentJoinItem
///   LegacyFalseAbort + not retired → HandleFalseCompletionRejected → no result
///   LegacyFalseAbort + retired → deterministic replacement migration → no aborted
///   Invalid → keep waiting → no consume
/// Renderer never sees legacy blob.
module JoinDrain =

    /// EXEC-018: no durable completion sequence → CreationOrder (HandleLinked
    /// fold order) then TargetAgent. Forbidden: AgentHandleId dictionary order,
    /// Promise race, wall clock, Map hash order.
    let stableJoinKey (record: HandleRecord) : int * string =
        record.CreationOrder, record.TargetAgent

    /// Ordered candidates for one drain pass (Abandoned + CompletedAwaitingJoin).
    let orderedCandidates (projection: AgentLinkageProjection) : HandleRecord list =
        (HandleProjection.reportableAbandoned projection
         @ HandleProjection.joinable projection)
        |> List.sortBy stableJoinKey

    let private abandonReasonText (reason: HandleAbandonReason) =
        match reason with
        | HandleAbandonReason.ParentCancelled -> "ParentCancelled"
        | HandleAbandonReason.DeadlineExceeded -> "DeadlineExceeded"
        | HandleAbandonReason.HostSessionGone -> "HostSessionGone"

    let private afterConsumeCas
        (agentId: string)
        (record: HandleRecord)
        (reasonText: string)
        (completedAt: DateTimeOffset)
        (outcome: Result<HandleRecord, HandleConsumeRejection>)
        : Result<RunCompletion, ForkError> option =
        match outcome with
        | Ok _ ->
            Some(
                Ok
                    { RunId = "abandoned-" + agentId
                      AgentId = agentId
                      AgentName = record.TargetAgent
                      Role = AgentRoleIdentity.ofRole record.CanonicalRole
                      Outcome = AgentCompletion.abandoned agentId reasonText
                      CompletedAt = completedAt }
            )
        | Error AlreadyRetired
        | Error(NotJoinable _) -> None
        | Error(AppendFailed err) -> Some(Error(ForkError.NotFound err))

    /// Materialise Abandoned as a batch item and CAS-retire (single report).
    /// `completedAt` is caller-minted (IClockPort at composition).
    let tryConsumeOneAbandoned
        (durable: AgentJournal)
        (parentId: SessionId)
        (record: HandleRecord)
        (completedAt: DateTimeOffset)
        : Task<Result<RunCompletion, ForkError> option> =
        task {
            match record.Lifecycle, HandleId.tryAgent record.Handle with
            | HandleLifecycle.Abandoned reason, Some agentHandleId ->
                let agentId = AgentHandleId.value agentHandleId
                let! outcome = HandleController.consume durable parentId record.Handle
                return afterConsumeCas agentId record (abandonReasonText reason) completedAt outcome
            | _ -> return None
        }

    let private appendFact (durable: AgentJournal) (parentId: SessionId) (fact: AgentFact) =
        AgentJournal.appendAgent (StreamId.Session parentId) None fact durable
        |> TaskValue.map (Result.map ignore >> Result.mapError JournalAppendFailure.describe)

    let private afterRejectAppendFailure
        (durable: AgentJournal)
        (parentId: SessionId)
        (record: HandleRecord)
        (err: string)
        : Result<RunCompletion, ForkError> option =
        let after = AgentJournal.handleProjection durable parentId

        match HandleProjection.tryFind record.Handle after with
        | Some { Lifecycle = HandleLifecycle.Active } -> None
        | _ -> Some(Error(ForkError.NotFound err))

    let private rejectAppendOutcome
        (durable: AgentJournal)
        (parentId: SessionId)
        (record: HandleRecord)
        (appendResult: Result<unit, string>)
        : Result<RunCompletion, ForkError> option =
        match appendResult with
        | Ok() -> None
        | Error err -> afterRejectAppendFailure durable parentId record err

    /// Unretired legacy false abort: append rejection, fold reverts to Active, no join item.
    /// Idempotent when already Active (fold rejects AlreadyCompleted / NotCompleted → treat as done).
    let private rejectUnretiredFalseAbort
        (durable: AgentJournal)
        (parentId: SessionId)
        (record: HandleRecord)
        (blobRef: BlobRef)
        (blobDigest: BlobDigest)
        : Task<Result<RunCompletion, ForkError> option> =
        task {
            match record.Lifecycle with
            | HandleLifecycle.CompletedAwaitingJoin _ ->
                let! appendResult =
                    appendFact
                        durable
                        parentId
                        (ExecutionFact.HandleFalseCompletionRejected
                            {| ParentSessionId = parentId
                               Handle = record.Handle
                               ExpectedCompletionRef = blobRef
                               ExpectedCompletionDigest = blobDigest
                               Reason = FalseCompletionReason.LegacyAbortWasObservation |})

                return rejectAppendOutcome durable parentId record appendResult
            | _ -> return None
        }

    let private appendMigrationFacts
        (durable: AgentJournal)
        (parentId: SessionId)
        (record: HandleRecord)
        (replacement: HandleId)
        (blobRef: BlobRef)
        (blobDigest: BlobDigest)
        : Task<Result<unit, ForkError>> =
        taskResult {
            do!
                appendFact
                    durable
                    parentId
                    (ExecutionFact.HandleFalseTerminalReported
                        {| ParentSessionId = parentId
                           Handle = record.Handle
                           BadCompletionRef = blobRef
                           BadCompletionDigest = blobDigest
                           Reason = FalseCompletionReason.LegacyAbortWasObservation |})
                |> TaskValue.map (Result.mapError ForkError.NotFound)

            do!
                appendFact
                    durable
                    parentId
                    (ExecutionFact.HandleLinked
                        {| ParentSessionId = parentId
                           ChildSessionId = record.ChildSessionId
                           Handle = replacement
                           TargetAgent = record.TargetAgent
                           Byname = record.Byname
                           CanonicalRole = record.CanonicalRole
                           Ownership = record.Ownership |})
                |> TaskValue.map (Result.mapError ForkError.NotFound)

            do!
                appendFact
                    durable
                    parentId
                    (ExecutionFact.ParentJoinCorrectionRequested
                        {| ParentSessionId = parentId
                           OriginalHandle = record.Handle
                           ReplacementHandle = replacement
                           BadCompletionDigest = blobDigest |})
                |> TaskValue.map (Result.mapError ForkError.NotFound)
        }

    let private migrationJoinOutcome (result: Result<unit, ForkError>) : Result<RunCompletion, ForkError> option =
        match result with
        | Ok() -> None
        | Error e -> Some(Error e)

    /// Retired legacy false abort: deterministic replacement + correction (idempotent).
    let private migrateRetiredFalseAbort
        (durable: AgentJournal)
        (parentId: SessionId)
        (record: HandleRecord)
        (agentId: string)
        (blobRef: BlobRef)
        (blobDigest: BlobDigest)
        : Task<Result<RunCompletion, ForkError> option> =
        task {
            let replacement = FalseTerminalMigration.replacementHandle agentId blobDigest
            let projection = AgentJournal.handleProjection durable parentId

            match HandleProjection.tryFind replacement projection with
            | Some _ -> return None
            | None ->
                let! result = appendMigrationFacts durable parentId record replacement blobRef blobDigest
                return migrationJoinOutcome result
        }

    let private missingBodyOutcome (agentId: string) (lifecycle: HandleLifecycle) =
        match lifecycle with
        | HandleLifecycle.CompletedAwaitingJoin { Kind = HandleCompletionKind.Terminal }
        | HandleLifecycle.CompletedAwaitingJoin { Kind = HandleCompletionKind.SendFailure } ->
            Some(Error(ForkError.TerminalMaterializationFailed agentId))
        | HandleLifecycle.CompletedAwaitingJoin { Kind = HandleCompletionKind.Cancelled } -> None
        | _ -> None

    let private afterFinalityConsume
        (completion: RunCompletion)
        (outcome: Result<HandleRecord, HandleConsumeRejection>)
        : Result<RunCompletion, ForkError> option =
        match outcome with
        | Ok _ -> Some(Ok completion)
        | Error AlreadyRetired
        | Error(NotJoinable _) -> None
        | Error(AppendFailed err) -> Some(Error(ForkError.NotFound err))

    let private joinCurrentDecoded
        (durable: AgentJournal)
        (parentId: SessionId)
        (record: HandleRecord)
        (agentId: string)
        (decoded: DurableAgentCompletionV2)
        (body: string)
        (completedAt: DateTimeOffset)
        : Task<Result<RunCompletion, ForkError> option> =
        task {
            let proof =
                JoinableCompletion.fromDecoded agentId record.Handle record.ChildSessionId decoded body

            let completion =
                HandleCompletionCodec.tryMaterialiseRunCompletion record agentId decoded completedAt

            match JoinableCompletion.finality proof with
            | ChildFinality.Succeeded _
            | ChildFinality.Failed _ ->
                let! outcome = HandleController.consume durable parentId record.Handle
                return afterFinalityConsume completion outcome
            | ChildFinality.Abandoned _ -> return None
        }

    let private afterDecodeBody
        (durable: AgentJournal)
        (parentId: SessionId)
        (record: HandleRecord)
        (agentId: string)
        (blobRef: BlobRef)
        (blobDigest: BlobDigest)
        (body: string)
        (completedAt: DateTimeOffset)
        : Task<Result<RunCompletion, ForkError> option> =
        match HandleCompletionCodec.decodeBody body with
        | Current decoded -> joinCurrentDecoded durable parentId record agentId decoded body completedAt
        | LegacyFalseAbort _ -> rejectUnretiredFalseAbort durable parentId record blobRef blobDigest
        | Invalid _ -> Task.FromResult None

    let private afterReadBody
        (durable: AgentJournal)
        (parentId: SessionId)
        (record: HandleRecord)
        (agentId: string)
        (readResult: Result<string option * BlobRef option * BlobDigest option, string>)
        (completedAt: DateTimeOffset)
        : Task<Result<RunCompletion, ForkError> option> =
        match readResult with
        | Error err -> Task.FromResult(Some(Error(ForkError.NotFound err)))
        | Ok(None, _, _) -> Task.FromResult(missingBodyOutcome agentId record.Lifecycle)
        | Ok(Some body, Some blobRef, Some blobDigest) ->
            afterDecodeBody durable parentId record agentId blobRef blobDigest body completedAt
        | Ok(Some _, _, _) ->
            Task.FromResult(Some(Error(ForkError.NotFound "completion blob ref/digest pair is incomplete")))

    /// One durable completed handle: decode first, then prove, then CAS.
    /// `completedAt` is caller-minted (IClockPort at composition).
    let tryConsumeOneDurable
        (durable: AgentJournal)
        (parentId: SessionId)
        (record: HandleRecord)
        (completedAt: DateTimeOffset)
        : Task<Result<RunCompletion, ForkError> option> =
        task {
            match HandleId.tryAgent record.Handle with
            | None -> return None
            | Some agentHandleId ->
                let agentId = AgentHandleId.value agentHandleId
                let! readResult = HandleCompletionCodec.tryReadBody durable record
                return! afterReadBody durable parentId record agentId readResult completedAt
        }

    /// Dispatch one record through the correct consume path.
    let tryConsumeOne
        (durable: AgentJournal)
        (parentId: SessionId)
        (completedAt: DateTimeOffset)
        (record: HandleRecord)
        : Task<Result<RunCompletion, ForkError> option> =
        match record.Lifecycle with
        | HandleLifecycle.Abandoned _ -> tryConsumeOneAbandoned durable parentId record completedAt
        | HandleLifecycle.CompletedAwaitingJoin _ -> tryConsumeOneDurable durable parentId record completedAt
        | _ -> Task.FromResult None

    let private agentNotTaken (takenIds: Set<string>) (r: HandleRecord) =
        match HandleId.tryAgent r.Handle with
        | Some id -> not (Set.contains (AgentHandleId.value id) takenIds)
        | None -> true

    let private refreshCandidates
        (acc: RunCompletion list)
        (refresh: unit -> AgentLinkageProjection)
        (records: HandleRecord list)
        =
        let takenIds = acc |> List.map (fun c -> c.AgentId) |> Set.ofList

        orderedCandidates (refresh ())
        |> List.filter (agentNotTaken takenIds)
        |> fun refreshed ->
            if List.isEmpty refreshed then Choice1Of3()
            elif refreshed = records then Choice2Of3()
            else Choice3Of3 refreshed

    /// Core drain loop: ordered candidates, CAS consume, refresh on race/skip.
    /// Returns Ok list (may be empty) or Error when the first item fails hard.
    let drainJoinableBatch
        (maxCount: int)
        (projection: AgentLinkageProjection)
        (consumeOne: HandleRecord -> Task<Result<RunCompletion, ForkError> option>)
        (refresh: unit -> AgentLinkageProjection)
        : Task<Result<RunCompletion list, ForkError>> =
        let rec consumeSafe
            (acc: RunCompletion list)
            (remaining: int)
            (records: HandleRecord list)
            : Task<Result<RunCompletion list, ForkError>> =
            task {
                match remaining, records with
                | 0, _
                | _, [] -> return Ok(List.rev acc)
                | n, record :: rest -> return! afterConsumeOne acc n record rest
            }

        and afterConsumeOne
            (acc: RunCompletion list)
            (n: int)
            (record: HandleRecord)
            (rest: HandleRecord list)
            : Task<Result<RunCompletion list, ForkError>> =
            task {
                let! outcome = consumeOne record
                return! decideConsumeOutcome acc n record rest outcome
            }

        and continueAfterSkip
            (acc: RunCompletion list)
            (n: int)
            (record: HandleRecord)
            (rest: HandleRecord list)
            : Task<Result<RunCompletion list, ForkError>> =
            match refreshCandidates acc refresh (record :: rest) with
            | Choice1Of3() -> Task.FromResult(Ok(List.rev acc))
            | Choice2Of3() -> consumeSafe acc n rest
            | Choice3Of3 refreshed -> consumeSafe acc n refreshed

        and decideConsumeOutcome
            (acc: RunCompletion list)
            (n: int)
            (record: HandleRecord)
            (rest: HandleRecord list)
            (outcome: Result<RunCompletion, ForkError> option)
            : Task<Result<RunCompletion list, ForkError>> =
            match outcome with
            | Some(Ok completion) -> consumeSafe (completion :: acc) (n - 1) rest
            | Some(Error e) when List.isEmpty acc -> Task.FromResult(Error e)
            | Some(Error _) -> Task.FromResult(Ok(List.rev acc))
            | None -> continueAfterSkip acc n record rest

        let cap = min maxCount JoinBatch.Max

        if cap <= 0 then
            Task.FromResult(Ok [])
        else
            consumeSafe [] cap (orderedCandidates projection)

    let private forkErrorMessage (e: ForkError) =
        match e with
        | ForkError.NotFound msg -> msg
        | other -> sprintf "%A" other

    let private migrateOutcomeToUnit (outcome: Result<RunCompletion, ForkError> option) =
        match outcome with
        | None
        | Some(Ok _) -> Ok()
        | Some(Error e) -> Error(forkErrorMessage e)

    /// Execute replacement migration when blob identity is known.
    let tryMigrateRetiredFalseAbort
        (durable: AgentJournal)
        (parentId: SessionId)
        (record: HandleRecord)
        (blobRef: BlobRef)
        (blobDigest: BlobDigest)
        : Task<Result<unit, string>> =
        task {
            match HandleId.tryAgent record.Handle with
            | None -> return Error "not an agent handle"
            | Some agentHandleId ->
                let agentId = AgentHandleId.value agentHandleId
                let! outcome = migrateRetiredFalseAbort durable parentId record agentId blobRef blobDigest
                return migrateOutcomeToUnit outcome
        }

    let private completionBlobPair (cell: HandleCompletion) =
        match cell.CompletionRef, cell.CompletionDigest with
        | Some blobRef, Some blobDigest -> Some(blobRef, blobDigest)
        | _ -> None

    /// Unretired awaiting-join → reject path; retired → migrate path.
    let private tryFalseAbortCell (record: HandleRecord) =
        match record.Lifecycle, HandleId.tryAgent record.Handle, record.LastCompletion with
        | HandleLifecycle.CompletedAwaitingJoin cell, Some _, _ ->
            completionBlobPair cell |> Option.map (fun pair -> pair, false)
        | HandleLifecycle.Retired, Some _, Some cell -> completionBlobPair cell |> Option.map (fun pair -> pair, true)
        | _ -> None

    let private applyDecodedFalseAbort
        (durable: AgentJournal)
        (parentId: SessionId)
        (record: HandleRecord)
        (blobRef: BlobRef)
        (blobDigest: BlobDigest)
        (body: string)
        (retired: bool)
        : Task<unit> =
        match HandleCompletionCodec.decodeBody body, retired with
        | LegacyFalseAbort _, false ->
            rejectUnretiredFalseAbort durable parentId record blobRef blobDigest
            |> TaskValue.map ignore
        | LegacyFalseAbort _, true ->
            tryMigrateRetiredFalseAbort durable parentId record blobRef blobDigest
            |> TaskValue.map ignore
        | _ -> Task.FromResult()

    let private maybeApplyLegacyFalseAbort
        (durable: AgentJournal)
        (parentId: SessionId)
        (record: HandleRecord)
        (blobRef: BlobRef)
        (blobDigest: BlobDigest)
        (retired: bool)
        : Task<unit> =
        task {
            let! readResult = durable.Writer.BlobWriter.Read blobRef

            match readResult with
            | Ok body when HostDigest.sha256Hex body = BlobDigest.value blobDigest ->
                return! applyDecodedFalseAbort durable parentId record blobRef blobDigest body retired
            | _ -> return ()
        }

    let private reconcileOne (durable: AgentJournal) (parentId: SessionId) (record: HandleRecord) : Task<unit> =
        task {
            match tryFalseAbortCell record with
            | None -> return ()
            | Some((blobRef, blobDigest), retired) ->
                return! maybeApplyLegacyFalseAbort durable parentId record blobRef blobDigest retired
        }

    /// Scan projection: reject unretired false aborts; migrate retired false aborts.
    /// O(handles) keyed lookups + blob reads for cells that already carry ref/digest.
    /// Idempotent. Does not return join items.
    let reconcileFalseAborts (durable: AgentJournal) (parentId: SessionId) : Task<unit> =
        task {
            let projection = AgentJournal.handleProjection durable parentId

            for record in HandleProjection.linkedChildren projection do
                do! reconcileOne durable parentId record
        }

    /// Production entry: reconcile false aborts, then drain joinable.
    /// `completedAt` stamps every materialised RunCompletion (IClockPort at composition).
    let drainFromJournal
        (durable: AgentJournal)
        (parentId: SessionId)
        (maxCount: int)
        (completedAt: DateTimeOffset)
        : Task<Result<RunCompletion list, ForkError>> =
        task {
            do! reconcileFalseAborts durable parentId
            let projection = AgentJournal.handleProjection durable parentId

            return!
                drainJoinableBatch maxCount projection (tryConsumeOne durable parentId completedAt) (fun () ->
                    AgentJournal.handleProjection durable parentId)
        }

    /// Fission lane join: consume only completion cells whose logical active-run
    /// affinity belongs to this present. Candidates not accepted by the predicate
    /// stay untouched and remain joinable for their owning lane.
    let drainFromJournalWhere
        (durable: AgentJournal)
        (parentId: SessionId)
        (maxCount: int)
        (completedAt: DateTimeOffset)
        (accept: HandleRecord -> bool)
        : Task<Result<RunCompletion list, ForkError>> =
        let filtered () =
            let projection = AgentJournal.handleProjection durable parentId

            { projection with
                Handles = projection.Handles |> Map.filter (fun _ record -> accept record) }

        task {
            do! reconcileFalseAborts durable parentId
            let projection = filtered ()

            return! drainJoinableBatch maxCount projection (tryConsumeOne durable parentId completedAt) filtered
        }
