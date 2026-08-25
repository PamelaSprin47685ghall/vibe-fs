// primary_owner: change-integration — Change.Host.Surface/Git.Gateway — KEEP — Change fact orchestrator surface
namespace Wanxiangshu.Change

open Wanxiangshu.Composition.Durable.Fact

/// Orchestrator fact constructors — bridge from Change-owned OrchestratorFactCases
/// into the Composition-owned AgentFact outer routing union.
module OrchestratorFact =
    let inline ManagerJobCreated payload =
        AgentFact.Orchestrator(OrchestratorFactCases.ManagerJobCreated payload)

    let inline CandidateReady payload =
        AgentFact.Orchestrator(OrchestratorFactCases.CandidateReady payload)

    let inline ConflictDetected payload =
        AgentFact.Orchestrator(OrchestratorFactCases.ConflictDetected payload)

    let inline RebasedCandidateReady payload =
        AgentFact.Orchestrator(OrchestratorFactCases.RebasedCandidateReady payload)

    let inline PublishClaimed payload =
        AgentFact.Orchestrator(OrchestratorFactCases.PublishClaimed payload)

    let inline Published payload =
        AgentFact.Orchestrator(OrchestratorFactCases.Published payload)

    let inline JobFailed payload =
        AgentFact.Orchestrator(OrchestratorFactCases.JobFailed payload)

    let inline JobAbandoned payload =
        AgentFact.Orchestrator(OrchestratorFactCases.JobAbandoned payload)

    let inline WorktreeCreateRequested payload =
        AgentFact.Orchestrator(OrchestratorFactCases.WorktreeCreateRequested payload)

    let inline WorktreeCreated payload =
        AgentFact.Orchestrator(OrchestratorFactCases.WorktreeCreated payload)
