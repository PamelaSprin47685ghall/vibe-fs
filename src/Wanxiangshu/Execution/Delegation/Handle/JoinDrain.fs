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

                let reasonText =
                    match reason with
                    | HandleAbandonReason.ParentCancelled -> "ParentCancelled"
                    | HandleAbandonReason.DeadlineExceeded -> "DeadlineExceeded"
                    | HandleAbandonReason.HostSessionGone -> "HostSessionGone"

                let role = AgentRoleIdentity.ofRole record.CanonicalRole

                match! HandleController.consume durable parentId record.Handle with
                | Ok _ ->
                    return
                        Some(
                            Ok
                                { RunId = "abandoned-" + agentId
                                  AgentId = agentId
                                  AgentName = record.TargetAgent
                                  Role = role
                                  Outcome = AgentCompletion.abandoned agentId reasonText
                                  CompletedAt = completedAt }
                        )
                | Error AlreadyRetired -> return None
                | Error(NotJoinable _) -> return None
                | Error(AppendFailed err) -> return Some(Error(ForkError.NotFound err))
            | _ -> return None
        }

    let private appendFact (durable: AgentJournal) (parentId: SessionId) (fact: AgentFact) =
        task {
            match! AgentJournal.appendAgent (StreamId.Session parentId) None fact durable with
            | Ok _ -> return Ok()
            | Error failure -> return Error(JournalAppendFailure.describe failure)
        }

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
                match!
                    appendFact
                        durable
                        parentId
                        (ExecutionFact.HandleFalseCompletionRejected
                            {| ParentSessionId = parentId
                               Handle = record.Handle
                               ExpectedCompletionRef = blobRef
                               ExpectedCompletionDigest = blobDigest
                               Reason = FalseCompletionReason.LegacyAbortWasObservation |})
                with
                | Ok() -> return None
                | Error err ->
                    let after = AgentJournal.handleProjection durable parentId

                    match HandleProjection.tryFind record.Handle after with
                    | Some { Lifecycle = HandleLifecycle.Active } -> return None
                    | _ -> return Some(Error(ForkError.NotFound err))
            | _ -> return None
        }

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
                match!
                    appendFact
                        durable
                        parentId
                        (ExecutionFact.HandleFalseTerminalReported
                            {| ParentSessionId = parentId
                               Handle = record.Handle
                               BadCompletionRef = blobRef
                               BadCompletionDigest = blobDigest
                               Reason = FalseCompletionReason.LegacyAbortWasObservation |})
                with
                | Error err -> return Some(Error(ForkError.NotFound err))
                | Ok() ->
                    match!
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
                    with
                    | Error err -> return Some(Error(ForkError.NotFound err))
                    | Ok() ->
                        match!
                            appendFact
                                durable
                                parentId
                                (ExecutionFact.ParentJoinCorrectionRequested
                                    {| ParentSessionId = parentId
                                       OriginalHandle = record.Handle
                                       ReplacementHandle = replacement
                                       BadCompletionDigest = blobDigest |})
                        with
                        | Ok() -> return None
                        | Error err -> return Some(Error(ForkError.NotFound err))
        }

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

                match! HandleCompletionCodec.tryReadBody durable record with
                | Error err -> return Some(Error(ForkError.NotFound err))
                | Ok(None, _, _) ->
                    match record.Lifecycle with
                    | HandleLifecycle.CompletedAwaitingJoin cell ->
                        match cell.Kind with
                        | HandleCompletionKind.Terminal
                        | HandleCompletionKind.SendFailure ->
                            return Some(Error(ForkError.TerminalMaterializationFailed agentId))
                        | HandleCompletionKind.Cancelled -> return None
                    | _ -> return None
                | Ok(Some body, Some blobRef, Some blobDigest) ->
                    match HandleCompletionCodec.decodeBody body with
                    | Current decoded ->
                        let proof =
                            JoinableCompletion.fromDecoded agentId record.Handle record.ChildSessionId decoded body

                        let completion =
                            HandleCompletionCodec.tryMaterialiseRunCompletion record agentId decoded completedAt

                        match JoinableCompletion.finality proof with
                        | ChildFinality.Succeeded _
                        | ChildFinality.Failed _ ->
                            match! HandleController.consume durable parentId record.Handle with
                            | Ok _ -> return Some(Ok completion)
                            | Error AlreadyRetired -> return None
                            | Error(NotJoinable _) -> return None
                            | Error(AppendFailed err) -> return Some(Error(ForkError.NotFound err))
                        | ChildFinality.Abandoned _ -> return None
                    | LegacyFalseAbort _ -> return! rejectUnretiredFalseAbort durable parentId record blobRef blobDigest
                    | Invalid _ -> return None
                | Ok(Some _, _, _) ->
                    return Some(Error(ForkError.NotFound "completion blob ref/digest pair is incomplete"))
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

    /// Core drain loop: ordered candidates, CAS consume, refresh on race/skip.
    /// Returns Ok list (may be empty) or Error when the first item fails hard.
    let drainJoinableBatch
        (maxCount: int)
        (projection: AgentLinkageProjection)
        (consumeOne: HandleRecord -> Task<Result<RunCompletion, ForkError> option>)
        (refresh: unit -> AgentLinkageProjection)
        : Task<Result<RunCompletion list, ForkError>> =
        task {
            let cap = min maxCount JoinBatch.Max

            if cap <= 0 then
                return Ok []
            else
                let rec consumeSafe
                    (acc: RunCompletion list)
                    (remaining: int)
                    (records: HandleRecord list)
                    : Task<Result<RunCompletion list, ForkError>> =
                    task {
                        match remaining, records with
                        | 0, _ -> return Ok(List.rev acc)
                        | _, [] -> return Ok(List.rev acc)
                        | n, record :: rest ->
                            match! consumeOne record with
                            | Some(Ok completion) -> return! consumeSafe (completion :: acc) (n - 1) rest
                            | Some(Error e) when List.isEmpty acc -> return Error e
                            | Some(Error _) -> return Ok(List.rev acc)
                            | None ->
                                let takenIds = acc |> List.map (fun c -> c.AgentId) |> Set.ofList
                                let refreshedProj = refresh ()

                                let refreshed =
                                    orderedCandidates refreshedProj
                                    |> List.filter (fun r ->
                                        match HandleId.tryAgent r.Handle with
                                        | Some id -> not (Set.contains (AgentHandleId.value id) takenIds)
                                        | None -> true)

                                if List.isEmpty refreshed then return Ok(List.rev acc)
                                elif refreshed = records then return! consumeSafe acc n rest
                                else return! consumeSafe acc n refreshed
                    }

                return! consumeSafe [] cap (orderedCandidates projection)
        }

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

                match! migrateRetiredFalseAbort durable parentId record agentId blobRef blobDigest with
                | None -> return Ok()
                | Some(Error e) ->
                    match e with
                    | ForkError.NotFound msg -> return Error msg
                    | other -> return Error(sprintf "%A" other)
                | Some(Ok _) -> return Ok()
        }

    /// Scan projection: reject unretired false aborts; migrate retired false aborts.
    /// O(handles) keyed lookups + blob reads for cells that already carry ref/digest.
    /// Idempotent. Does not return join items.
    let reconcileFalseAborts (durable: AgentJournal) (parentId: SessionId) : Task<unit> =
        task {
            let projection = AgentJournal.handleProjection durable parentId

            for record in HandleProjection.linkedChildren projection do
                match record.Lifecycle, HandleId.tryAgent record.Handle with
                | HandleLifecycle.CompletedAwaitingJoin cell, Some _ ->
                    match cell.CompletionRef, cell.CompletionDigest with
                    | Some blobRef, Some blobDigest ->
                        match! durable.Writer.BlobWriter.Read blobRef with
                        | Ok body when HostDigest.sha256Hex body = BlobDigest.value blobDigest ->
                            match HandleCompletionCodec.decodeBody body with
                            | LegacyFalseAbort _ ->
                                let! _ = rejectUnretiredFalseAbort durable parentId record blobRef blobDigest
                                ()
                            | _ -> ()
                        | _ -> ()
                    | _ -> ()
                | HandleLifecycle.Retired, Some _ ->
                    match record.LastCompletion with
                    | Some cell ->
                        match cell.CompletionRef, cell.CompletionDigest with
                        | Some blobRef, Some blobDigest ->
                            match! durable.Writer.BlobWriter.Read blobRef with
                            | Ok body when HostDigest.sha256Hex body = BlobDigest.value blobDigest ->
                                match HandleCompletionCodec.decodeBody body with
                                | LegacyFalseAbort _ ->
                                    let! _ = tryMigrateRetiredFalseAbort durable parentId record blobRef blobDigest
                                    ()
                                | _ -> ()
                            | _ -> ()
                        | _ -> ()
                    | None -> ()
                | _ -> ()
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
            { projection with Handles = projection.Handles |> Map.filter (fun _ record -> accept record) }

        task {
            do! reconcileFalseAborts durable parentId
            let projection = filtered ()

            return!
                drainJoinableBatch maxCount projection (tryConsumeOne durable parentId completedAt) filtered
        }
