namespace Wanxiangshu.Interaction.Attention

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

type AttentionAppendFailure = | DurabilityUnavailable

type AttentionJournalPort =
    { Read: unit -> AttentionProjectionState
      Append:
          SessionId -> ProviderRunIdentity option -> AttentionFactCases -> Task<Result<unit, AttentionAppendFailure>> }
