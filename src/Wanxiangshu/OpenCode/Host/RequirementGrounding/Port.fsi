namespace Wanxiangshu.OpenCode.Host.RequirementGrounding

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Requirement.Grounding

type RequirementGroundingPort =
    { ReadState: SessionId -> RequirementGroundingProjectionState
      AppendRequested: SessionId -> GroundingSnapshot -> Task<Result<unit, string>>
      AppendReadObserved: SessionId -> RequirementGroundingReadObserved -> Task<Result<unit, string>>
      AppendAnchored: SessionId -> RequirementGroundingOccurrence -> Task<Result<unit, string>> }
