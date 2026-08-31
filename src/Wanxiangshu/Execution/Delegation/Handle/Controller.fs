namespace Wanxiangshu.Execution.Delegation.Handle

open Wanxiangshu.Composition.Durable
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger.Runtime
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Enforcer.Guidance
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Execution.Session.Wait
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength

open System.Threading.Tasks
open Wanxiangshu.Execution.Delegation.Fork.ChildRecovery
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Persistence.Journal

/// Why a controlled consume refused to retire (EXEC-009).
type HandleConsumeRejection =
    /// Handle is already Retired — a concurrent join won, or a restart replay.
    | AlreadyRetired
    /// Handle is still Active — no completion cell yet.
    | NotJoinable of HandleTransitionRejection
    /// Journal append failed (includes CommitUnknown). Must not deliver.
    | AppendFailed of string

/// EXEC-009: the only writer of `HandleLinked`, `HandleCompleted`,
/// `HandleAbandoned` and `HandleRetired`.
///
/// One lifecycle, one writer. Progressions are
/// `Active → CompletedAwaitingJoin → Retired` or `Active|CompletedAwaitingJoin →
/// Abandoned`. `HandleProjection` rejects any out-of-order transition. Spreading
/// the appends across the fork path, the completion path and the cancel path
/// meant three modules each knew part of that order, and none of them could see
/// whether the other two agreed.
///
/// P0-RECOVERY-JOIN-001: `recordCompletion` accepts only `JoinableCompletion`.
/// Raw Aborted / bare kind+body cannot claim the completion cell.
module HandleController =

    /// An agent child's handle IS its runtime agent id.
    ///
    /// EXEC-009 requires the same handle id after a restart, and the agent id is
    /// what every runtime map is already keyed by. Minting a separate id would
    /// create a second identity for one resource and a mapping to keep in step.
    let agentHandle (agentId: string) =
        HandleId.Agent(AgentHandleId.create agentId)

    let private append (journal: AgentJournal) (parentId: SessionId) fact =
        task {
            match! AgentJournal.appendAgent (StreamId.Session parentId) None fact journal with
            | Ok _ -> return Ok()
            | Error failure -> return Error(JournalAppendFailure.describe failure)
        }

    /// EXEC-009: a fork bound a handle to a Host child session.
    ///
    /// `childSessionId` is recorded because only the Host can issue it: a recovered
    /// handle with no session points at nothing, and deriving one from the handle id
    /// would fabricate an identity every later operation silently no-ops against.
    let linkNamed
        (journal: AgentJournal option)
        (parentId: SessionId)
        (agentId: string)
        (childSessionId: SessionId)
        (targetAgent: string)
        (byname: string)
        (role: Role)
        (ownership: HandleOwnership)
        : Task<Result<unit, string>> =
        match journal with
        | None -> Task.FromResult(Ok())
        | Some durable ->
            append
                durable
                parentId
                (ExecutionFact.HandleLinked
                    {| ParentSessionId = parentId
                       ChildSessionId = childSessionId
                       Handle = agentHandle agentId
                       TargetAgent = targetAgent
                       Byname = byname
                       CanonicalRole = role
                       Ownership = ownership |})

    /// Internal compatibility: when no distinct provider presentation identity
    /// exists, use the Host target name as the byname too.
    let link
        (journal: AgentJournal option)
        (parentId: SessionId)
        (agentId: string)
        (childSessionId: SessionId)
        (targetAgent: string)
        (role: Role)
        (ownership: HandleOwnership)
        : Task<Result<unit, string>> =
        linkNamed journal parentId agentId childSessionId targetAgent targetAgent role ownership

    /// EXEC-004 / P0-RECOVERY-JOIN-001: claim the single-assignment completion cell
    /// only with a proven `JoinableCompletion` (Succeeded | Failed finality).
    ///
    /// Blob write precedes the fact (PERSIST-007). Kind + body come from the proof;
    /// callers cannot pass raw Aborted or bare `HandleCompletionKind` + `"ABORTED"`.
    /// The fold refuses a second claim, so a duplicate is a no-op rather than overwrite.
    let private writeCompletionBlob
        (durable: AgentJournal)
        (content: string)
        : Task<Result<BlobRef option * BlobDigest option, string>> =
        task {
            match! durable.WriteBlob content with
            | Error err -> return Error err
            | Ok receipt -> return Ok(Some receipt.BlobRef, Some receipt.BlobDigest)
        }

    let private completionBlobRefs
        (durable: AgentJournal)
        (body: string option)
        : Task<Result<BlobRef option * BlobDigest option, string>> =
        task {
            match body with
            | None -> return Ok(None, None)
            | Some content -> return! writeCompletionBlob durable content
        }

    let private recordCompletionWithJournal
        durable
        parentId
        (completion: JoinableCompletion)
        : Task<Result<unit, string>> =
        task {
            let agentId = JoinableCompletion.agentId completion
            let kind = JoinableCompletion.kind completion
            let! refs = completionBlobRefs durable (JoinableCompletion.body completion)

            match refs with
            | Error err -> return Error err
            | Ok(completionRef, completionDigest) ->
                return!
                    append
                        durable
                        parentId
                        (ExecutionFact.HandleCompleted
                            {| ParentSessionId = parentId
                               Handle = agentHandle agentId
                               Kind = kind
                               CompletionRef = completionRef
                               CompletionDigest = completionDigest |})
        }

    let recordCompletion
        (journal: AgentJournal option)
        (parentId: SessionId)
        (completion: JoinableCompletion)
        : Task<Result<unit, string>> =
        task {
            match journal with
            | None -> return Ok()
            | Some durable -> return! recordCompletionWithJournal durable parentId completion
        }

    /// EXEC-009: durable abandon. Single-assignment via fold CAS.
    ///
    /// Does not write a completion cell and does not retire. Join must see
    /// Abandoned as non-joinable and return an explicit abandon outcome.
    ///
    /// Call only for irreversible loss (parent cancel, deadline, host session
    /// gone). Never from degeneration-guard interrupt, provider-retry wake, or any path
    /// that may continue the same handle through an independently owned control path.
    let recordAbandon
        (journal: AgentJournal option)
        (parentId: SessionId)
        (agentId: string)
        (reason: HandleAbandonReason)
        (abandonedAt: System.DateTimeOffset)
        : Task<Result<unit, string>> =
        match journal with
        | None -> Task.FromResult(Ok())
        | Some durable ->
            append
                durable
                parentId
                (ExecutionFact.HandleAbandoned
                    {| ParentSessionId = parentId
                       Handle = agentHandle agentId
                       Reason = reason
                       AbandonedAt = abandonedAt |})

    /// EXEC-004/EXEC-009: `join` consumed the completion, so write the tombstone.
    ///
    /// Retirement is what makes a consumed completion unreturnable. Without it the
    /// handle stays `CompletedAwaitingJoin` in the durable projection, so a restart
    /// restores it as joinable and the same completion is delivered twice.
    let retire (journal: AgentJournal option) (parentId: SessionId) (agentId: string) : Task<Result<unit, string>> =
        match journal with
        | None -> Task.FromResult(Ok())
        | Some durable ->
            append
                durable
                parentId
                (ExecutionFact.HandleRetired
                    {| ParentSessionId = parentId
                       Handle = agentHandle agentId |})

    /// EXEC-009: one controlled consume. Projection must show
    /// `CompletedAwaitingJoin` (completion report) or `Abandoned` (single batch
    /// report). Success writes `HandleRetired`. Concurrent callers race on the
    /// journal gate — the loser sees `AlreadyRetired`.
    ///
    /// CommitUnknown must not hand the payload out: the caller would treat the
    /// work as consumed while a later restart might still show it joinable.
    let private retirementFailure (journal: AgentJournal) (parentId: SessionId) (handle: HandleId) failure =
        let after = AgentJournal.handleProjection journal parentId

        match HandleProjection.tryFind handle after with
        | Some { Lifecycle = Retired } -> Error AlreadyRetired
        | _ -> Error(AppendFailed(JournalAppendFailure.describe failure))

    let private retireRecord
        (journal: AgentJournal)
        (parentId: SessionId)
        (handle: HandleId)
        (record: HandleRecord)
        : Task<Result<HandleRecord, HandleConsumeRejection>> =
        task {
            match!
                AgentJournal.appendAgent
                    (StreamId.Session parentId)
                    None
                    (ExecutionFact.HandleRetired
                        {| ParentSessionId = parentId
                           Handle = handle |})
                    journal
            with
            | Ok _ -> return Ok record
            | Error failure -> return retirementFailure journal parentId handle failure
        }

    let consume
        (journal: AgentJournal)
        (parentId: SessionId)
        (handle: HandleId)
        : Task<Result<HandleRecord, HandleConsumeRejection>> =
        task {
            let projection = AgentJournal.handleProjection journal parentId

            match HandleProjection.tryFind handle projection with
            | None -> return Error(NotJoinable UnknownHandle)
            | Some { Lifecycle = Retired } -> return Error AlreadyRetired
            | Some { Lifecycle = Active } -> return Error(NotJoinable NotCompleted)
            | Some({ Lifecycle = CompletedAwaitingJoin _ } as record)
            | Some({ Lifecycle = Abandoned _ } as record) -> return! retireRecord journal parentId handle record
        }

    /// Parent cancel: durable `HandleAbandoned` (ParentCancelled) per owned agent.
    ///
    /// Replaces the previous `Cancelled` completion + retire pair so abandon is an
    /// explicit durable terminal that is not joinable. Each child is abandoned
    /// individually — EXEC-009 requires parent cancel to cancel every owned resource
    /// one by one, so there is deliberately no bulk "abandon all children" fact.
    let private abandonChild journal parentId agentId abandonedAt =
        task {
            match! recordAbandon journal parentId agentId HandleAbandonReason.ParentCancelled abandonedAt with
            | Error err -> return Error err
            | Ok() -> return Ok()
        }

    let cancelChildren
        (journal: AgentJournal option)
        (parentId: SessionId)
        (agentIds: string list)
        (abandonedAt: System.DateTimeOffset)
        : Task<Result<unit, string>> =
        let rec loop ids =
            task {
                match ids with
                | [] -> return Ok()
                | agentId :: rest ->
                    let! result = abandonChild journal parentId agentId abandonedAt
                    return! continueAbandon rest result
            }

        and continueAbandon rest result =
            task {
                match result with
                | Error err -> return Error err
                | Ok() -> return! loop rest
            }

        loop agentIds
