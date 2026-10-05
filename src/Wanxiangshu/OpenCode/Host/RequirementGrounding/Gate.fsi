namespace Wanxiangshu.OpenCode.Host.RequirementGrounding

open System.Threading.Tasks
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Requirement.Grounding

module RequirementGroundingGate =
    val nativeReadObservations: toolName: string -> args: obj -> output: obj -> GroundingFileRead list

    val decideMutation:
        journal: AgentJournal option ->
        workspace: string ->
        sessionId: string ->
        paths: string list ->
            Task<Result<RequirementGroundingDecision, string>>

    val decideRead:
        journal: AgentJournal option ->
        workspace: string ->
        sessionId: string ->
        reads: GroundingFileRead list ->
            Task<Result<RequirementGroundingDecision, string>>

    val before:
        journal: AgentJournal option -> workspace: string option -> toolInput: obj -> toolOutput: obj -> Task<unit>

    val after:
        journal: AgentJournal option -> workspace: string option -> toolInput: obj -> toolOutput: obj -> Task<unit>

    val programObservation:
        journal: AgentJournal option ->
        workspace: string ->
        sessionId: string ->
        reads: GroundingFileRead list ->
        effectPaths: string list ->
            Task<unit>
