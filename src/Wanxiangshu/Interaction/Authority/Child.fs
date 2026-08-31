namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Change
open Wanxiangshu.Mission.Obligation
open Wanxiangshu.Mission.Review.Barrier
open Wanxiangshu.Participant.Provider.Attempt.Fallback
open Wanxiangshu.Strength.Persistence

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Context.Companion.Blogger
open Wanxiangshu.Context.Prefix
open Wanxiangshu.Context.Trace
open Wanxiangshu.Enforcer
open Wanxiangshu.Enforcer.Cycle
open Wanxiangshu.Execution.Delegation.Fork
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Fission
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Host.Contract
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Mission.WorkRecord
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Persistence.EventStore
open Wanxiangshu.Repository.Investigation.WarmStart
open Wanxiangshu.Repository.Knowledge.Casebook
open Wanxiangshu.Repository.Programming.Js
open Wanxiangshu.Strength
open Wanxiangshu.Strength.Prediction
open Wanxiangshu.Strength.Projection
open Wanxiangshu.Strength.Replica
open Wanxiangshu.Host
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Foundation.Identity

/// Application ownership of linked-child prompt authority (rabbit §19).
/// Physical runtime cleanup must not decide who owns a Logical Run.
module ChildPromptAuthority =

    let private registerLinkedChildIfNeeded
        (runtime: PromptDispatcher.Runtime)
        (turn: ReconciledTurn)
        handle
        (activeProfile: PromptAuthority.AuthorityExecutionProfile option)
        (accepted: PromptAuthority.AcceptedDispatch option)
        : System.Threading.Tasks.Task<Result<unit, string>> =
        match handle, activeProfile, accepted with
        | None, _, _
        | Some _, Some _, _ -> System.Threading.Tasks.Task.FromResult(Ok())
        | Some _, None, None ->
            System.Threading.Tasks.Task.FromResult(
                Error(
                    sprintf
                        "Linked child %s has no accepted AgentOwnerRoot claim for physical message %s"
                        (SessionId.value turn.SessionId)
                        (PhysicalUserMessageId.value turn.PhysicalUserMessageId)
                )
            )
        | Some _, None, Some claim ->
            runtime.AcceptPhysicalAgentOwnerRoot
                claim.PromptKey
                turn.SessionId
                turn.PhysicalUserMessageId
                claim.IdentitySeed
            |> TaskValue.map (Result.map ignore)

    let ensureForLinkedChild
        (journal: AgentJournal option)
        (turn: ReconciledTurn)
        : System.Threading.Tasks.Task<Result<unit, string>> =
        task {
            match journal with
            | None -> return Ok()
            | Some durable ->
                let snapshot = AgentJournal.snapshot durable

                let handle =
                    Map.tryFind turn.SessionId snapshot.AgentProjections.HandleByChildSession

                let activeProfile =
                    PromptAuthorityLedger.activeProfile turn.SessionId snapshot.AgentProjections

                let accepted =
                    PromptAuthorityLedger.acceptedDispatchForPhysicalMessage
                        turn.SessionId
                        turn.PhysicalUserMessageId
                        snapshot.AgentProjections
                    |> Option.filter (fun claim ->
                        claim.Origin = PromptAuthority.PromptOrigin.AuthorityRoot
                                           PromptAuthority.RootAuthorityKind.AgentOwnerRoot)

                let runtime = PromptDispatcher.forJournal durable
                return! registerLinkedChildIfNeeded runtime turn handle activeProfile accepted
        }
