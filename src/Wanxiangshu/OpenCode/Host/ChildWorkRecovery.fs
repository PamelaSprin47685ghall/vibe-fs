namespace Wanxiangshu.OpenCode.Host

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable.Fact
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Execution.Session.ChatExecution
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Persistence.Journal

/// crash-reconciliation-017/018/020: a restart never replays interrupted work and
/// the tool call that was in flight stays failed in visible history. That leaves
/// child work — the road's fixed DevOps and any forked Engineer child — with a
/// logical run no live execution can ever satisfy: the previous runtime is gone,
/// so `ChatExecutionRecovery` treats its physical evidence as stale and writes
/// nothing, and the child authority stays open forever. The next handoff is then
/// refused with `ActiveRunIdentityConflict` while the parent's join waits on a
/// handle that will never complete.
///
/// The explicit reset happens here, once, at load: every child work run still
/// active at startup is settled as `HandleCompleted(Cancelled)` — the durable
/// evidence that closes the child authority (the delegation fold derives
/// `TerminatedChildHandle` from it) and answers the parent's join. The child's
/// transcript is untouched, and restarting the work remains the manager's own
/// explicit decision.
module ChildWorkRecovery =

    type OrphanedChildRun =
        { ParentSessionId: SessionId
          Handle: HandleId
          ChildSessionId: SessionId }

    let private isChildWorkRun (run: PromptAuthority.AuthorityExecutionProfile) : bool =
        run.AuthorityKind = PromptAuthority.RootAuthorityKind.AgentOwnerRoot && run.CanonicalRole <> Role.Manager

    let private activeChildRuns (projections: AgentProjectionSet) : SessionId list =
        projections.Sessions
        |> Map.toList
        |> List.choose (fun (sessionId, session) ->
            session.PromptAuthority
            |> Option.bind (fun authority -> authority.ActiveLogicalRun)
            |> Option.filter isChildWorkRun
            |> Option.map (fun _ -> sessionId))

    let private handleFor (projections: AgentProjectionSet) (childSessionId: SessionId) : OrphanedChildRun option =
        projections.HandleByChildSession
        |> Map.tryFind childSessionId
        |> Option.filter (fun record ->
            match record.Lifecycle with
            | HandleLifecycle.Active -> true
            | _ -> false)
        |> Option.bind (fun record ->
            projections.Sessions
            |> Map.toList
            |> List.tryPick (fun (parentSessionId, session) ->
                match session.Handles with
                | Some handles when HandleProjection.tryFind record.Handle handles |> Option.isSome ->
                    Some
                        { ParentSessionId = parentSessionId
                          Handle = record.Handle
                          ChildSessionId = childSessionId }
                | _ -> None))

    /// The child work runs a fresh process can no longer execute, with the parent
    /// handle that has to carry their settlement.
    let orphanedChildRuns (projections: AgentProjectionSet) : OrphanedChildRun list =
        activeChildRuns projections |> List.choose (handleFor projections)

    let settlementFact (orphaned: OrphanedChildRun) : ExecutionFactCases =
        ExecutionFactCases.HandleCompleted
            {| ParentSessionId = orphaned.ParentSessionId
               Handle = orphaned.Handle
               Kind = HandleCompletionKind.Cancelled
               CompletionRef = None
               CompletionDigest = None |}

    let settleOrphanedChildRuns (journal: AgentJournal) : Task<unit> =
        task {
            let projections = (AgentJournal.snapshot journal).AgentProjections

            for orphaned in orphanedChildRuns projections do
                let! _ =
                    AgentJournal.appendAgent
                        (StreamId.Session orphaned.ParentSessionId)
                        None
                        (AgentFact.Execution(settlementFact orphaned))
                        journal

                ()

            return ()
        }
