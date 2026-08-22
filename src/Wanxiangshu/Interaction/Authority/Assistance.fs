namespace Wanxiangshu.Interaction.Authority

open System
open System.Threading.Tasks
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Persona
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Execution.Delegation
open Wanxiangshu.Interaction.Dispatch.OpenCode

/// Domain identity of an assistance owner session and run.
[<RequireQualifiedAccess>]
type AssistanceOwner =
    { SessionId: SessionId
      LogicalRunId: LogicalRunId }

/// Pure classification of an admitted assistance request.
[<RequireQualifiedAccess>]
type AssistanceDecision =
    | EscalateFast of Profile: PromptAuthority.AuthorityExecutionProfile * Role: Role
    | ConsultDeep of Profile: PromptAuthority.AuthorityExecutionProfile * Requester: string
    | RejectOrUnresolved

module AssistanceDecision =

    /// Pure domain decision: Fast tier escalates to Deep peer in same session;
    /// Deep tier requests a bounded Consultation child.
    let decide (profile: PromptAuthority.AuthorityExecutionProfile) (requestingAgent: string) : AssistanceDecision =
        match PromptAuthority.parseAgentName requestingAgent with
        | Error _ -> AssistanceDecision.RejectOrUnresolved
        | Ok(_, role, _, _) when role <> profile.CanonicalRole -> AssistanceDecision.RejectOrUnresolved
        | Ok(_, role, AgentTier.Fast, _) -> AssistanceDecision.EscalateFast(profile, role)
        | Ok(requester, _, AgentTier.Deep, _) -> AssistanceDecision.ConsultDeep(profile, requester)

/// Pure witness established from durable handle and terminal evidence without new events.
[<RequireQualifiedAccess>]
type ConsultationWitness =
    private
        { Handle: AgentHandleId
          ChildSessionId: SessionId
          WorkRecord: string }

[<RequireQualifiedAccess>]
type InvalidConsultation =
    | HandleNotCompleted
    | WorkRecordMissing
    | OwnerMismatch

module ConsultationProjection =

    /// Pure projection: check HandleLinked + HandleCompleted + AuthorityRoot + ProviderRun
    /// terminal evidence directly without creating extra durable event records.
    let tryProject
        (expectedOwner: SessionId)
        (recordOwner: SessionId)
        (handleId: AgentHandleId)
        (childId: SessionId)
        (isCompletedOrActive: bool)
        (childWorkRecord: string option)
        : Result<ConsultationWitness, InvalidConsultation> =
        match expectedOwner = recordOwner, isCompletedOrActive, childWorkRecord with
        | false, _, _ -> Error InvalidConsultation.OwnerMismatch
        | _, false, _ -> Error InvalidConsultation.HandleNotCompleted
        | true, true, Some record when not (String.IsNullOrWhiteSpace record) ->
            Ok
                { Handle = handleId
                  ChildSessionId = childId
                  WorkRecord = record }
        | _ -> Error InvalidConsultation.WorkRecordMissing

/// Typed business capabilities required by the Assistance workflow CE.
/// No internal dependencies on Git, Strength, Review, or Todo.
type AssistancePorts =
    { CurrentAuthority: SessionId -> PromptAuthority.AuthorityExecutionProfile option
      StartConsultation: SessionId -> LogicalRunId -> string -> string option -> Task<Result<SessionId, string>>
      AwaitConsultation: SessionId -> HandleRecord -> Task<AssistanceTurnDisposition>
      DeliverAdvice: SessionId -> LogicalRunId -> string -> string -> string option -> Task<Result<unit, string>>
      DeliverFailedAdvice: SessionId -> LogicalRunId -> string -> string -> string option -> Task<Result<unit, string>>
      ConsumeClaim: AssistanceAbortClaim -> bool }

module AssistanceWorkflow =

    let private toDisposition (outcome: Result<'a, 'b>) : AssistanceTurnDisposition =
        match outcome with
        | Ok _ -> AssistanceTurnDisposition.Handled
        | Error _ -> AssistanceTurnDisposition.ClaimedButUnresolved

    /// Pure CE workflow orchestrating the assistance decision.
    let executeOwnerDecision
        (ports: AssistancePorts)
        (decision: AssistanceDecision)
        (sessionId: SessionId)
        (directory: string option)
        (escalationPrompt: string)
        (consultationDirectory: string option)
        : Task<AssistanceTurnDisposition> =
        task {
            match decision with
            | AssistanceDecision.RejectOrUnresolved -> return AssistanceTurnDisposition.ClaimedButUnresolved
            | AssistanceDecision.EscalateFast(profile, role) ->
                let deepAgent = ManagedAgentCatalog.nameOf AgentTier.Deep role
                let! outcome = ports.DeliverAdvice sessionId profile.LogicalRunId deepAgent escalationPrompt directory
                return toDisposition outcome
            | AssistanceDecision.ConsultDeep(profile, requester) ->
                let! outcome = ports.StartConsultation sessionId profile.LogicalRunId requester consultationDirectory
                return toDisposition outcome
        }
