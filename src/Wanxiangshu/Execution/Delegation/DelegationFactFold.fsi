namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation

module DelegationFactFold =
    val fold:
        sessionState: (SessionId -> DelegationSessionState option) ->
        fact: DelegationFactCases ->
            Result<DelegationProjectionChange list, DelegationFoldRejection>
