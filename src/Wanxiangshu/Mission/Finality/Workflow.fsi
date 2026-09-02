namespace Wanxiangshu.Mission.Finality

open System.Threading.Tasks
open Wanxiangshu.Change
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Mission.Manager.Life
open Wanxiangshu.Persistence.Journal

/// Manager Finality story: enlist cohort → review → reject+steer OR bless.
module FinalityWorkflow =

    val start:
        reviewerPort: FinalityReviewerPort ->
        treePort: FinalityTreePort ->
        journal: AgentJournal option ->
        managerSessionId: SessionId ->
        lifeId: ManagerLifeId ->
        requestId: FinalityRequestId ->
        requestTree: GitTreeHash ->
        lastWordsRef: BlobRef ->
        lastWordsDigest: BlobDigest ->
        providerRun: ProviderRunIdentity ->
        toolCallId: ToolCallId ->
            Task<FinalityOutcome>

    val resume:
        reviewerPort: FinalityReviewerPort ->
        journal: AgentJournal option ->
        managerSessionId: SessionId ->
        lifeId: ManagerLifeId ->
        requestId: FinalityRequestId ->
            Task<FinalityOutcome option>
