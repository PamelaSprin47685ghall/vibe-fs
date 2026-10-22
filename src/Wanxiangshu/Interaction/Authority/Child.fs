namespace Wanxiangshu.Interaction.Authority

open Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Composition.Turn
open Wanxiangshu.Context.Companion
open Wanxiangshu.Execution.Delegation.SyncDelegate
open Wanxiangshu.Execution.Session.Recovery
open Wanxiangshu.Foundation
open Wanxiangshu.Host
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Participant.Provider
open Wanxiangshu.Participant.Provider.Attempt
open Wanxiangshu.Participant.Provider.Projection
open Wanxiangshu.Host
open Wanxiangshu.Composition.Turn
open Wanxiangshu.Interaction.Authority
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
        acceptedExecution
        : System.Threading.Tasks.Task<Result<unit, string>> =
        match handle, activeProfile, accepted, acceptedExecution with
        | None, _, _, _
        | Some _, Some _, _, _
        | Some _, None, Some _, true -> System.Threading.Tasks.Task.FromResult(Ok())
        | Some _, None, None, _ ->
            System.Threading.Tasks.Task.FromResult(
                Error(
                    sprintf
                        "Linked child %s has no accepted AgentOwnerRoot claim for physical message %s"
                        (SessionId.value turn.SessionId)
                        (PhysicalUserMessageId.value turn.PhysicalUserMessageId)
                )
            )
        | Some _, None, Some claim, false ->
            runtime.AcceptPhysicalAgentOwnerRoot
                claim.PromptKey
                turn.SessionId
                turn.PhysicalUserMessageId
                claim.IdentitySeed
                None
            |> TaskValue.map (Result.map ignore)

    let ensureForLinkedChild
        (prompts: IPromptJournal option)
        (turn: ReconciledTurn)
        : System.Threading.Tasks.Task<Result<unit, string>> =
        task {
            match prompts with
            | Some prompts ->
                let owner = prompts.ProjectionFor turn.SessionId
                let handle = prompts.HandleForChild turn.SessionId
                let activeProfile = owner.ActiveLogicalRun

                let accepted =
                    Map.tryFind turn.PhysicalUserMessageId owner.PhysicalLandings
                    |> Option.filter (fun dispatch ->
                        dispatch.Origin = PromptAuthority.PromptOrigin.AuthorityRoot
                            PromptAuthority.RootAuthorityKind.AgentOwnerRoot)

                let runtime = PromptDispatcher.forPrompts prompts

                let key: Wanxiangshu.Execution.Session.ChatExecution.ChatExecutionKey =
                    { SessionId = turn.SessionId
                      PhysicalUserMessageId = turn.PhysicalUserMessageId }

                let acceptedExecution =
                    (prompts.ChatAcceptancePersistence()).ReadExact key
                    |> Option.exists (fun execution ->
                        let evidence = execution.acceptedEvidence

                        evidence.AuthorityRootUserMessageId = turn.AuthorityRootUserMessageId
                        && evidence.AuthorityKind = PromptAuthority.RootAuthorityKind.AgentOwnerRoot
                        && (accepted
                            |> Option.exists (fun claim -> claim.IdentitySeed = evidence.IdentitySeed)))

                return! registerLinkedChildIfNeeded runtime turn handle activeProfile accepted acceptedExecution
            | None -> return Ok()
        }
