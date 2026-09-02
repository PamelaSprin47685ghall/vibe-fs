namespace Wanxiangshu.OpenCode.Host.RequirementGrounding

open System.Threading.Tasks
open Wanxiangshu.Composition.Durable
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Persistence.Journal
open Wanxiangshu.Requirement.Grounding

type RequirementGroundingDecision =
    { NeedsGrounding: bool
      Requested: int
      Packages: string list }

module RequirementGroundingRuntime =
    val pending: journal: AgentJournal -> sessionId: SessionId -> GroundingSnapshot list
    val occurrences: journal: AgentJournal -> sessionId: SessionId -> RequirementGroundingOccurrence list
    val historyOccurrences: journal: AgentJournal -> sessionId: SessionId -> RequirementGroundingOccurrence list
    val groundedKeys: journal: AgentJournal -> sessionId: SessionId -> string list
    val nextOrdinal: journal: AgentJournal -> sessionId: SessionId -> int64

    val requestPaths:
        journal: AgentJournal ->
        workspace: string ->
        sessionId: SessionId ->
        paths: string list ->
            Task<Result<RequirementGroundingDecision, string>>

    val observeReadPaths:
        journal: AgentJournal ->
        workspace: string ->
        sessionId: SessionId ->
        paths: string list ->
            Task<Result<RequirementGroundingDecision, string>>

    val appendAnchored:
        journal: AgentJournal ->
        sessionId: SessionId ->
        occurrence: RequirementGroundingOccurrence ->
            Task<Result<ProjectionSet, JournalAppendFailure>>
