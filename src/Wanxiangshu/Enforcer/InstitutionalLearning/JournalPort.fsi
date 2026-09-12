namespace Wanxiangshu.Enforcer.InstitutionalLearning

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity

type InstitutionalLearningAppendFailure = | DurabilityUnavailable

type InstitutionalLearningJournalPort =
    { ReadState: SessionId -> InstitutionalLearningProjectionState
      PendingAttentionWorkPairs: SessionId -> (string * string) list
      Append:
          SessionId
              -> ProviderRunIdentity option
              -> InstitutionalLearningFactCases
              -> Task<Result<unit, InstitutionalLearningAppendFailure>> }
