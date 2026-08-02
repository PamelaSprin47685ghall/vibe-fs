namespace Wanxiangshu.Next.Session

open Wanxiangshu.Next.Kernel.Fact
open Wanxiangshu.Next.Kernel.Identity
open Wanxiangshu.Next.Journal

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
    /// Terminal, send-failure and cancel all arrive here; `HandleProjection.complete`
    /// refuses the second claim, so a duplicate is a no-op rather than an overwrite.
    /// The rejection is absorbed at the fold, which is why this returns `unit` on a
    /// replayed claim instead of an error the caller would have to ignore.
    let recordCompletion
        (journal: AgentJournal option)
        (parentId: SessionId)
        (agentId: string)
        (kind: HandleCompletionKind)
        : Result<unit, string> =
        match journal with
        | None -> Ok()
        | Some durable ->
            append
                durable
                parentId
                (AgentFact.HandleCompleted
                    {| ParentSessionId = parentId
                       Handle = agentHandle agentId
                       Kind = kind |})

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

    /// Parent cancel: claim the cell as `Cancelled`, then retire.
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
                recordCompletion journal parentId agentId HandleCompletionKind.Cancelled
                |> Result.bind (fun () -> retire journal parentId agentId)
                |> Result.bind (fun () -> loop rest)

        loop agentIds
