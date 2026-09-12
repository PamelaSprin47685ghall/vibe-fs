namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open System.Threading.Tasks
open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Attempt

type ProviderFailureJournalPort =
    { CurrentState: SessionId -> ProviderFailureProjection option
      Append: SessionId -> ProviderRunIdentity -> ProviderFailureFactCases -> Task<Result<unit, string>> }
