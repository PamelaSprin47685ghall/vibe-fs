namespace Wanxiangshu.Interaction.Concern

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

type ConcernAppendFailure = | DurabilityUnavailable

type ConcernJournalPort =
    { ReadState: SessionId -> ConcernProjectionState
      Append: SessionId -> ProviderRunIdentity option -> ConcernFactCases -> Task<Result<unit, ConcernAppendFailure>> }
