namespace Wanxiangshu.Execution.Delegation

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

type DelegatedToolEstimatePort =
    { TryState: SessionId -> DelegatedToolEstimateProjectionState option
      Append: SessionId -> DelegationFactCases -> Task<Result<unit, string>> }
