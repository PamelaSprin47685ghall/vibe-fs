namespace Wanxiangshu.Participant.Provider.Attempt.Fallback

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation

[<RequireQualifiedAccess>]
type ProviderFailureProjectionChange = ProviderFailuresSet of SessionId * ProviderFailureProjection

[<RequireQualifiedAccess>]
type ProviderFailureFoldRejection =
    | FailureRecordedWithoutBudget
    | RetryExhaustedWithoutBudget
    | SuccessRecordedWithoutBudget
    | InvalidTransition

[<RequireQualifiedAccess>]
module ProviderFailureFoldRejection =
    val fact: ProviderFailureFoldRejection -> string
    val message: ProviderFailureFoldRejection -> string

module ProviderFailureFactFold =
    val fold:
        providerFailuresOf: (SessionId -> ProviderFailureProjection option) ->
        fact: ProviderFailureFactCases ->
            Result<ProviderFailureProjectionChange list, ProviderFailureFoldRejection>
