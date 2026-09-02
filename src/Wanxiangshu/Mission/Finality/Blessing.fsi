namespace Wanxiangshu.Mission.Finality

open System.Threading.Tasks
open Wanxiangshu.Change
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Mission.Review.Judgement
open Wanxiangshu.Persistence.Journal

/// FinalityAdmission gate: verifies that the current manager tree matches the witness tree,
/// preventing stale witnesses from authorizing a current blessing (AGENTS.md §63).
module FinalityAdmission =

    val grantBlessing:
        currentTree: GitTreeHash -> witness: ConfirmedReviewWitness -> Result<BlessingPermit, BlessingAdmissionFailure>

    val permitTree: permit: BlessingPermit -> GitTreeHash

    val permitLifeId: permit: BlessingPermit -> ManagerLifeId

    val permitRequestId: permit: BlessingPermit -> FinalityRequestId

/// All-confirmed convergence: canonical records + stable tree → blessing.
module BlessingWorkflow =

    val blessIfAdmitted:
        reviewerPort: FinalityReviewerPort ->
        treePort: FinalityTreePort ->
        journal: AgentJournal ->
        managerSessionId: SessionId ->
        witness: ConfirmedReviewWitness ->
        members: EnlistedMember list ->
            Task<FinalityOutcome>

    val blessIfTreeUnchanged:
        reviewerPort: FinalityReviewerPort ->
        treePort: FinalityTreePort ->
        journal: AgentJournal ->
        managerSessionId: SessionId ->
        lifeId: ManagerLifeId ->
        requestId: FinalityRequestId ->
        members: EnlistedMember list ->
        requestTree: GitTreeHash ->
            Task<FinalityOutcome>
