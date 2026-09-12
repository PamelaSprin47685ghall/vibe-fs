namespace Wanxiangshu.OpenCode.Host.RequirementGrounding

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode.Host.RequirementGrounding
open Wanxiangshu.Requirement.Grounding

type RequirementGroundingDecision =
    { NeedsGrounding: bool
      Requested: int
      Packages: string list }

module RequirementGroundingRuntime =
    val pending: port: RequirementGroundingPort -> sessionId: SessionId -> GroundingSnapshot list
    val occurrences: port: RequirementGroundingPort -> sessionId: SessionId -> RequirementGroundingOccurrence list

    val historyOccurrences:
        port: RequirementGroundingPort -> sessionId: SessionId -> RequirementGroundingOccurrence list

    val groundedKeys: port: RequirementGroundingPort -> sessionId: SessionId -> string list
    val nextOrdinal: port: RequirementGroundingPort -> sessionId: SessionId -> int64

    val requestPaths:
        port: RequirementGroundingPort ->
        workspace: string ->
        sessionId: SessionId ->
        paths: string list ->
            Task<Result<RequirementGroundingDecision, string>>

    val observeReadPaths:
        port: RequirementGroundingPort ->
        workspace: string ->
        sessionId: SessionId ->
        paths: string list ->
            Task<Result<RequirementGroundingDecision, string>>

    val appendAnchored:
        port: RequirementGroundingPort ->
        sessionId: SessionId ->
        occurrence: RequirementGroundingOccurrence ->
            Task<Result<unit, string>>
