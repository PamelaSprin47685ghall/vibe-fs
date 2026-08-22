namespace Wanxiangshu.Mission.Review.Judgement

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
open Wanxiangshu.Interaction.Authority
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.Mission.Finality
open Wanxiangshu.Mission.Manager
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Mission.Review
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
open Wanxiangshu.Foundation
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Obligation.Todo
open Wanxiangshu.Persistence.Journal

/// Reviewer-side reads of the durable review guard.
///
/// Keyed by reviewer session, which is where REVIEW-003's facts land.
module ReviewerEvidence =

    let private guard (journal: AgentJournal option) (reviewerKey: string) =
        match journal with
        | None -> None
        | Some durable ->
            AgentProjection.tryFind (SessionId.create reviewerKey) (AgentJournal.snapshot durable).AgentProjections
            |> Option.bind (fun session -> session.ReviewGuard)

    let private cohortHasRevision (projections: AgentProjectionSet) (request: FinalityRequestProjection) =
        request.Members
        |> Map.exists (fun reviewerSessionId memberRef ->
            AgentProjection.tryFind reviewerSessionId projections
            |> Option.bind (fun session -> session.ReviewGuard)
            |> Option.exists (fun reviewGuard ->
                reviewGuard.CurrentBarrierId = Some memberRef.BarrierId
                && ReviewWitness.isRevision reviewGuard.Witness))

    let private finalityMembership
        (snapshot: ProjectionSet)
        (request: FinalityRequestProjection)
        (reviewerKey: string)
        (barrierId: ReviewBarrierId)
        =
        match Map.tryFind (SessionId.create reviewerKey) request.Members with
        | None -> true
        | Some enlisted when enlisted.BarrierId <> barrierId -> true
        | Some _ ->
            ManagerLifecycleProjection.isOpen request
            && not (cohortHasRevision snapshot.AgentProjections request)

    let private reviewerFinality
        (durable: AgentJournal)
        (reviewGuard: ReviewGuardProjection)
        (reviewerKey: string)
        : bool option =
        match reviewGuard.CurrentManagerSessionId, reviewGuard.CurrentBarrierId with
        | Some managerSessionId, Some barrierId ->
            let snapshot = AgentJournal.snapshot durable

            AgentProjection.tryFind managerSessionId snapshot.AgentProjections
            |> Option.bind (fun session -> session.ManagerLife)
            |> Option.bind (fun lifecycle -> lifecycle.CurrentLife |> Option.bind (fun life -> life.ActiveFinality))
            |> Option.map (fun request -> finalityMembership snapshot request reviewerKey barrierId)
        | _ -> None

    /// Whether the barrier still authorizes reviewer continuation. Finality may
    /// stop waiting after a sibling REVISE; that closed request must also revoke
    /// the reviewer's challenge capability. Non-Finality review owners have no
    /// ManagerLife projection and remain eligible.
    let continuationOpen journal reviewerKey =
        match journal, guard journal reviewerKey with
        | Some durable, Some reviewGuard -> reviewerFinality durable reviewGuard reviewerKey |> Option.defaultValue true
        | _ -> true
