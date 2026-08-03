namespace Wanxiangshu.Next.Session

open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

/// Why a controlled consume refused to retire (EXEC-009).
type HandleConsumeRejection =
    /// Handle is already Retired — a concurrent join won, or a restart replay.
    | AlreadyRetired
    /// Handle is still Active — no completion cell yet.
    | NotJoinable of HandleTransitionRejection
    /// Journal append failed (includes CommitUnknown). Must not deliver.
    | AppendFailed of string

/// EXEC-009: the only writer of `HandleLinked`, `HandleCompleted` and
/// `HandleRetired`.
///
/// One lifecycle, one writer. The three facts are a single ordered progression —
/// `Active → CompletedAwaitingJoin → Retired` — and `HandleProjection` rejects any
/// out-of-order transition. Spreading the appends across the fork path, the
/// completion path and the cancel path meant three modules each knew part of that
/// order, and none of them could see whether the other two agreed.
module HandleController =

    /// An agent child's handle IS its runtime agent id.
    ///
    /// EXEC-009 requires the same handle id after a restart, and the agent id is
    /// what every runtime map is already keyed by. Minting a separate id would
    /// create a second identity for one resource and a mapping to keep in step.
    let agentHandle (agentId: string) =
        HandleId.Agent(AgentHandleId.create agentId)

    let private append (journal: AgentJournal) (parentId: SessionId) fact =
        match AgentJournal.appendAgent (StreamId.Session parentId) None fact journal with
        | Ok _ -> Ok()
        | Error failure -> Error(JournalAppendFailure.describe failure)

    /// EXEC-009: a fork bound a handle to a Host child session.
    ///
    /// `childSessionId` is recorded because only the Host can issue it: a recovered
    /// handle with no session points at nothing, and deriving one from the handle id
    /// would fabricate an identity every later operation silently no-ops against.
    let link
        (journal: AgentJournal option)
        (parentId: SessionId)
        (agentId: string)
        (childSessionId: SessionId)
        (targetAgent: string)
        (role: AgentRole)
        : Result<unit, string> =
        match journal with
        | None -> Ok()
        | Some durable ->
            append
                durable
                parentId
                (AgentFact.HandleLinked
                    {| ParentSessionId = parentId
                       ChildSessionId = childSessionId
                       Handle = agentHandle agentId
                       TargetAgent = targetAgent
                       CanonicalRole = AgentRoleIdentity.toRole role |})

    /// EXEC-004: claim the single-assignment completion cell.
    ///
    /// Terminal and send-failure write the join payload blob BEFORE the fact
    /// (PERSIST-007). Cancelled carries no body. The fold refuses a second claim,
    /// so a duplicate is a no-op rather than an overwrite.
    let recordCompletion
        (journal: AgentJournal option)
        (parentId: SessionId)
        (agentId: string)
        (kind: HandleCompletionKind)
        (body: string option)
        : Result<unit, string> =
        match journal with
        | None -> Ok()
        | Some durable ->
            let writeRefs () : Result<BlobRef option * BlobDigest option, string> =
                match body with
                | None -> Ok(None, None)
                | Some content ->
                    match durable.WriteBlob content with
                    | Error err -> Error err
                    | Ok receipt -> Ok(Some receipt.BlobRef, Some receipt.BlobDigest)

            writeRefs ()
            |> Result.bind (fun (completionRef, completionDigest) ->
                append
                    durable
                    parentId
                    (AgentFact.HandleCompleted
                        {| ParentSessionId = parentId
                           Handle = agentHandle agentId
                           Kind = kind
                           CompletionRef = completionRef
                           CompletionDigest = completionDigest |}))

    /// EXEC-004/EXEC-009: `join` consumed the completion, so write the tombstone.
    ///
    /// Retirement is what makes a consumed completion unreturnable. Without it the
    /// handle stays `CompletedAwaitingJoin` in the durable projection, so a restart
    /// restores it as joinable and the same completion is delivered twice.
    let retire (journal: AgentJournal option) (parentId: SessionId) (agentId: string) : Result<unit, string> =
        match journal with
        | None -> Ok()
        | Some durable ->
            append
                durable
                parentId
                (AgentFact.HandleRetired
                    {| ParentSessionId = parentId
                       Handle = agentHandle agentId |})

    /// EXEC-009: one controlled consume. Projection must already show
    /// `CompletedAwaitingJoin`; success writes `HandleRetired`. Concurrent callers
    /// race on the journal gate — the loser sees `AlreadyRetired`.
    ///
    /// CommitUnknown must not hand the payload out: the caller would treat the
    /// work as consumed while a later restart might still show it joinable.
    let consume
        (journal: AgentJournal)
        (parentId: SessionId)
        (handle: HandleId)
        : Result<HandleRecord, HandleConsumeRejection> =
        let projection = AgentJournal.handleProjection journal parentId

        match HandleProjection.tryFind handle projection with
        | None -> Error(NotJoinable UnknownHandle)
        | Some { Lifecycle = Retired } -> Error AlreadyRetired
        | Some { Lifecycle = Active } -> Error(NotJoinable NotCompleted)
        | Some record ->
            match
                AgentJournal.appendAgent
                    (StreamId.Session parentId)
                    None
                    (AgentFact.HandleRetired
                        {| ParentSessionId = parentId
                           Handle = handle |})
                    journal
            with
            | Ok _ -> Ok record
            | Error failure ->
                // Re-read: a concurrent winner may have retired between our check and
                // the append, or CommitUnknown may have actually landed.
                let after = AgentJournal.handleProjection journal parentId

                match HandleProjection.tryFind handle after with
                | Some { Lifecycle = Retired } -> Error AlreadyRetired
                | _ -> Error(AppendFailed(JournalAppendFailure.describe failure))

    /// Parent cancel: claim the cell as `Cancelled` (no blob), then retire.
    ///
    /// Both facts, in this order, per child. Writing only the tombstone would skip
    /// the state EXEC-005's `list` reports and make the transition unauditable;
    /// writing only the completion would leave the handle joinable after its parent
    /// is gone. Each child is retired individually — EXEC-009 requires parent cancel
    /// to cancel every owned resource one by one, so there is deliberately no bulk
    /// "retire all children" fact.
    let cancelChildren
        (journal: AgentJournal option)
        (parentId: SessionId)
        (agentIds: string list)
        : Result<unit, string> =
        let rec loop ids =
            match ids with
            | [] -> Ok()
            | agentId :: rest ->
                recordCompletion journal parentId agentId HandleCompletionKind.Cancelled None
                |> Result.bind (fun () -> retire journal parentId agentId)
                |> Result.bind (fun () -> loop rest)

        loop agentIds
