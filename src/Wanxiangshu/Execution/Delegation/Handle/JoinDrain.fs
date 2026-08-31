namespace Wanxiangshu.Execution.Delegation.Handle

open System
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Host
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.Fork.ChildRecovery

/// EXEC-009 + EXEC-018 + clean-break: pure durable join drain.
///
/// Order: HandleRecord → blob body → DurableCompletionDecode → branch:
///   Current → fromDecoded → CAS HandleRetired → AgentJoinItem
///   LegacyFalseAbort + not retired → HandleFalseCompletionRejected → no result
///   LegacyFalseAbort + retired → fail-closed refuse (no replacement) → Error
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
                      AgentName = record.TargetAgent
                      Role = record.CanonicalRole
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
        let takenIds =
            acc |> List.map (fun c -> AgentCompletion.agentId c.Outcome) |> Set.ofList

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

    let private completionBlobPair (cell: HandleCompletion) =
        match cell.CompletionRef, cell.CompletionDigest with
        | Some blobRef, Some blobDigest -> Some(blobRef, blobDigest)
        | _ -> None

    /// Unretired awaiting-join → reject path; retired → refuse path (fail-closed).
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
        : Task<Result<unit, ForkError>> =
        match HandleCompletionCodec.decodeBody body, retired with
        | LegacyFalseAbort _, false ->
            task {
                do!
                    rejectUnretiredFalseAbort durable parentId record blobRef blobDigest
                    |> TaskValue.map ignore

                return Ok()
            }
        | LegacyFalseAbort _, true ->
            // EFFECT-ACCOUNTING-007: retired handle with legacy false-abort tombstone.
            // Fail-closed refuse — do not mint a replacement. The bad-data set is
            // observably empty (48-journal census: zero fired); the writer is dead
            // (codec-encode-finality-aborted gate). Action: archive or remove the
            // affected journal.
            Task.FromResult(
                Error(
                    ForkError.NotFound
                        "legacy false-abort tombstone on retired handle; archive or remove the affected journal"
                )
            )
        | _ -> Task.FromResult(Ok())

    let private maybeApplyLegacyFalseAbort
        (durable: AgentJournal)
        (parentId: SessionId)
        (record: HandleRecord)
        (blobRef: BlobRef)
        (blobDigest: BlobDigest)
        (retired: bool)
        : Task<Result<unit, ForkError>> =
        task {
            let! readResult = durable.Writer.BlobWriter.Read blobRef

            match readResult with
            | Ok body when HostDigest.sha256Hex body = BlobDigest.value blobDigest ->
                return! applyDecodedFalseAbort durable parentId record blobRef blobDigest body retired
            | _ -> return Ok()
        }

    let private reconcileOne
        (durable: AgentJournal)
        (parentId: SessionId)
        (record: HandleRecord)
        : Task<Result<unit, ForkError>> =
        task {
            match tryFalseAbortCell record with
            | None -> return Ok()
            | Some((blobRef, blobDigest), retired) ->
                return! maybeApplyLegacyFalseAbort durable parentId record blobRef blobDigest retired
        }

    /// Scan projection: reject unretired false aborts; refuse retired false aborts.
    /// O(handles) keyed lookups + blob reads for cells that already carry ref/digest.
    /// Returns Error on the first retired false-abort tombstone (fail-closed refuse).
    let reconcileFalseAborts (durable: AgentJournal) (parentId: SessionId) : Task<Result<unit, ForkError>> =
        let projection = AgentJournal.handleProjection durable parentId

        let rec loop (remaining: HandleRecord list) : Task<Result<unit, ForkError>> =
            taskResult {
                match remaining with
                | [] -> return ()
                | record :: rest ->
                    do! reconcileOne durable parentId record
                    do! loop rest
            }

        loop (HandleProjection.linkedChildren projection)

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
            let! reconcileResult = reconcileFalseAborts durable parentId

            match reconcileResult with
            | Error e -> return Error e
            | Ok() ->
                let projection = filtered ()

                return! drainJoinableBatch maxCount projection (tryConsumeOne durable parentId completedAt) filtered
        }
