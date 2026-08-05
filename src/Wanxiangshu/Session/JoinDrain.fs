namespace Wanxiangshu.Session

open System
open Wanxiangshu.Domain.ChildRecovery
open Wanxiangshu.Host
open Wanxiangshu.Kernel.Fact
open Wanxiangshu.Kernel.Identity
open Wanxiangshu.Journal

/// EXEC-009 + EXEC-018 + clean-break: pure durable join drain.
///
/// Order: HandleRecord → blob body → DurableCompletionDecode → branch:
///   Current → fromDecoded → CAS HandleRetired → AgentJoinItem
///   LegacyFalseAbort + not retired → HandleFalseCompletionRejected → no result
///   LegacyFalseAbort + retired → deterministic replacement migration → no aborted
///   Invalid → RecoveryBlocked → no consume
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
    let tryConsumeOneAbandoned
        (durable: AgentJournal)
        (parentId: SessionId)
        (record: HandleRecord)
        : Result<RunCompletion, ForkError> option =
        match record.Lifecycle, HandleId.tryAgent record.Handle with
        | HandleLifecycle.Abandoned reason, Some agentHandleId ->
            let agentId = AgentHandleId.value agentHandleId

            let reasonText =
                match reason with
                | HandleAbandonReason.ParentCancelled -> "ParentCancelled"
                | HandleAbandonReason.DeadlineExceeded -> "DeadlineExceeded"
                | HandleAbandonReason.HostSessionGone -> "HostSessionGone"

            let role = AgentRoleIdentity.ofRole record.CanonicalRole

            match HandleController.consume durable parentId record.Handle with
            | Ok _ ->
                Some(
                    Ok
                        { RunId = "abandoned-" + agentId
                          AgentId = agentId
                          AgentName = record.TargetAgent
                          Role = role
                          Outcome = AgentCompletion.abandoned agentId reasonText
                          CompletedAt = DateTimeOffset.UtcNow }
                )
            | Error AlreadyRetired -> None
            | Error(NotJoinable _) -> None
            | Error(AppendFailed err) -> Some(Error(ForkError.NotFound err))
        | _ -> None

    let private appendFact (durable: AgentJournal) (parentId: SessionId) (fact: AgentFact) : Result<unit, string> =
        match AgentJournal.appendAgent (StreamId.Session parentId) None fact durable with
        | Ok _ -> Ok()
        | Error failure -> Error(JournalAppendFailure.describe failure)

    /// Unretired legacy false abort: append rejection, fold reverts to Active, no join item.
    /// Idempotent when already Active (fold rejects AlreadyCompleted / NotCompleted → treat as done).
    let private rejectUnretiredFalseAbort
        (durable: AgentJournal)
        (parentId: SessionId)
        (record: HandleRecord)
        (blobRef: BlobRef)
        (blobDigest: BlobDigest)
        : Result<RunCompletion, ForkError> option =
        match record.Lifecycle with
        | HandleLifecycle.CompletedAwaitingJoin _ ->
            match
                appendFact
                    durable
                    parentId
                    (AgentFact.HandleFalseCompletionRejected
                        {| ParentSessionId = parentId
                           Handle = record.Handle
                           ExpectedCompletionRef = blobRef
                           ExpectedCompletionDigest = blobDigest
                           Reason = FalseCompletionReason.LegacyAbortWasObservation |})
            with
            | Ok() -> None
            | Error err ->
                // Concurrent reject or fold refuse after state changed: recheck.
                let after = AgentJournal.handleProjection durable parentId

                match HandleProjection.tryFind record.Handle after with
                | Some { Lifecycle = HandleLifecycle.Active } -> None
                | _ -> Some(Error(ForkError.NotFound err))
        | _ -> None

    /// Retired legacy false abort: deterministic replacement + correction (idempotent).
    let private migrateRetiredFalseAbort
        (durable: AgentJournal)
        (parentId: SessionId)
        (record: HandleRecord)
        (agentId: string)
        (blobRef: BlobRef)
        (blobDigest: BlobDigest)
        : Result<RunCompletion, ForkError> option =
        let replacement = FalseTerminalMigration.replacementHandle agentId blobDigest
        let projection = AgentJournal.handleProjection durable parentId

        match HandleProjection.tryFind replacement projection with
        | Some _ ->
            // Replacement already linked (prior recovery). No second mint.
            None
        | None ->
            let steps =
                appendFact
                    durable
                    parentId
                    (AgentFact.HandleFalseTerminalReported
                        {| ParentSessionId = parentId
                           Handle = record.Handle
                           BadCompletionRef = blobRef
                           BadCompletionDigest = blobDigest
                           Reason = FalseCompletionReason.LegacyAbortWasObservation |})
                |> Result.bind (fun () ->
                    appendFact
                        durable
                        parentId
                        (AgentFact.HandleLinked
                            {| ParentSessionId = parentId
                               ChildSessionId = record.ChildSessionId
                               Handle = replacement
                               TargetAgent = record.TargetAgent
                               CanonicalRole = record.CanonicalRole |}))
                |> Result.bind (fun () ->
                    appendFact
                        durable
                        parentId
                        (AgentFact.ParentJoinCorrectionRequested
                            {| ParentSessionId = parentId
                               OriginalHandle = record.Handle
                               ReplacementHandle = replacement
                               BadCompletionDigest = blobDigest |}))

            match steps with
            | Ok() -> None
            | Error err -> Some(Error(ForkError.NotFound err))

    /// One durable completed handle: decode first, then prove, then CAS.
    let tryConsumeOneDurable
        (durable: AgentJournal)
        (parentId: SessionId)
        (record: HandleRecord)
        : Result<RunCompletion, ForkError> option =
        match HandleId.tryAgent record.Handle with
        | None -> None
        | Some agentHandleId ->
            let agentId = AgentHandleId.value agentHandleId

            match HandleCompletionCodec.tryReadBody durable record with
            | Error err -> Some(Error(ForkError.NotFound err))
            | Ok(None, _, _) ->
                match record.Lifecycle with
                | HandleLifecycle.CompletedAwaitingJoin cell ->
                    match cell.Kind with
                    | HandleCompletionKind.Terminal
                    | HandleCompletionKind.SendFailure -> Some(Error(ForkError.TerminalMaterializationFailed agentId))
                    | HandleCompletionKind.Cancelled -> None
                | _ -> None
            | Ok(Some body, Some blobRef, Some blobDigest) ->
                match HandleCompletionCodec.decodeBody body with
                | Current decoded ->
                    let proof =
                        JoinableCompletion.fromDecoded agentId record.Handle record.ChildSessionId decoded body

                    let completion =
                        HandleCompletionCodec.tryMaterialiseRunCompletion record agentId decoded

                    // Proof must agree with materialised finality before CAS.
                    match JoinableCompletion.finality proof with
                    | ChildFinality.Succeeded _
                    | ChildFinality.Failed _ ->
                        match HandleController.consume durable parentId record.Handle with
                        | Ok _ -> Some(Ok completion)
                        | Error AlreadyRetired -> None
                        | Error(NotJoinable _) -> None
                        | Error(AppendFailed err) -> Some(Error(ForkError.NotFound err))
                    | ChildFinality.Abandoned _ -> None
                | LegacyFalseAbort _ ->
                    // Not retired path: joinable projection only holds CompletedAwaitingJoin.
                    rejectUnretiredFalseAbort durable parentId record blobRef blobDigest
                | Invalid _ ->
                    // Fail closed: do not consume, do not surface to parent.
                    Some(Error(ForkError.TerminalMaterializationFailed agentId))
            | Ok(Some _, _, _) -> Some(Error(ForkError.NotFound "completion blob ref/digest pair is incomplete"))

    /// Dispatch one record through the correct consume path.
    let tryConsumeOne
        (durable: AgentJournal)
        (parentId: SessionId)
        (record: HandleRecord)
        : Result<RunCompletion, ForkError> option =
        match record.Lifecycle with
        | HandleLifecycle.Abandoned _ -> tryConsumeOneAbandoned durable parentId record
        | HandleLifecycle.CompletedAwaitingJoin _ -> tryConsumeOneDurable durable parentId record
        | _ -> None

    /// Core drain loop: ordered candidates, CAS consume, refresh on race/skip.
    /// Returns Ok list (may be empty) or Error when the first item fails hard.
    let drainJoinableBatch
        (maxCount: int)
        (projection: AgentLinkageProjection)
        (consumeOne: HandleRecord -> Result<RunCompletion, ForkError> option)
        (refresh: unit -> AgentLinkageProjection)
        : Result<RunCompletion list, ForkError> =
        let cap = min maxCount JoinBatch.Max

        if cap <= 0 then
            Ok []
        else
            let rec consumeSafe
                (acc: RunCompletion list)
                (remaining: int)
                (records: HandleRecord list)
                : Result<RunCompletion list, ForkError> =
                match remaining, records with
                | 0, _ -> Ok(List.rev acc)
                | _, [] -> Ok(List.rev acc)
                | n, record :: rest ->
                    match consumeOne record with
                    | Some(Ok completion) -> consumeSafe (completion :: acc) (n - 1) rest
                    | Some(Error e) when List.isEmpty acc -> Error e
                    | Some(Error _) -> Ok(List.rev acc)
                    | None ->
                        let takenIds = acc |> List.map (fun c -> c.AgentId) |> Set.ofList
                        let refreshedProj = refresh ()

                        let refreshed =
                            orderedCandidates refreshedProj
                            |> List.filter (fun r ->
                                match HandleId.tryAgent r.Handle with
                                | Some id -> not (Set.contains (AgentHandleId.value id) takenIds)
                                | None -> true)

                        if List.isEmpty refreshed then Ok(List.rev acc)
                        elif refreshed = records then consumeSafe acc n rest
                        else consumeSafe acc n refreshed

            consumeSafe [] cap (orderedCandidates projection)

    /// Execute replacement migration when blob identity is known.
    let tryMigrateRetiredFalseAbort
        (durable: AgentJournal)
        (parentId: SessionId)
        (record: HandleRecord)
        (blobRef: BlobRef)
        (blobDigest: BlobDigest)
        : Result<unit, string> =
        match HandleId.tryAgent record.Handle with
        | None -> Error "not an agent handle"
        | Some agentHandleId ->
            let agentId = AgentHandleId.value agentHandleId

            match migrateRetiredFalseAbort durable parentId record agentId blobRef blobDigest with
            | None -> Ok()
            | Some(Error e) ->
                match e with
                | ForkError.NotFound msg -> Error msg
                | other -> Error(sprintf "%A" other)
            | Some(Ok _) -> Ok()

    /// Scan projection: reject unretired false aborts; migrate retired false aborts.
    /// O(handles) keyed lookups + blob reads for cells that already carry ref/digest.
    /// Idempotent. Does not return join items.
    let reconcileFalseAborts (durable: AgentJournal) (parentId: SessionId) : unit =
        let projection = AgentJournal.handleProjection durable parentId

        for record in HandleProjection.linkedChildren projection do
            match record.Lifecycle, HandleId.tryAgent record.Handle with
            | HandleLifecycle.CompletedAwaitingJoin cell, Some _ ->
                match cell.CompletionRef, cell.CompletionDigest with
                | Some blobRef, Some blobDigest ->
                    match durable.Writer.BlobWriter.Read blobRef with
                    | Ok body when HostDigest.sha256Hex body = BlobDigest.value blobDigest ->
                        match HandleCompletionCodec.decodeBody body with
                        | LegacyFalseAbort _ ->
                            ignore (rejectUnretiredFalseAbort durable parentId record blobRef blobDigest)
                        | _ -> ()
                    | _ -> ()
                | _ -> ()
            | HandleLifecycle.Retired, Some _ ->
                match record.LastCompletion with
                | Some cell ->
                    match cell.CompletionRef, cell.CompletionDigest with
                    | Some blobRef, Some blobDigest ->
                        match durable.Writer.BlobWriter.Read blobRef with
                        | Ok body when HostDigest.sha256Hex body = BlobDigest.value blobDigest ->
                            match HandleCompletionCodec.decodeBody body with
                            | LegacyFalseAbort _ ->
                                ignore (
                                    tryMigrateRetiredFalseAbort durable parentId record blobRef blobDigest
                                )
                            | _ -> ()
                        | _ -> ()
                    | _ -> ()
                | None -> ()
            | _ -> ()

    /// Production entry: reconcile false aborts, then drain joinable.
    let drainFromJournal
        (durable: AgentJournal)
        (parentId: SessionId)
        (maxCount: int)
        : Result<RunCompletion list, ForkError> =
        reconcileFalseAborts durable parentId
        let projection = AgentJournal.handleProjection durable parentId

        drainJoinableBatch maxCount projection (tryConsumeOne durable parentId) (fun () ->
            AgentJournal.handleProjection durable parentId)
