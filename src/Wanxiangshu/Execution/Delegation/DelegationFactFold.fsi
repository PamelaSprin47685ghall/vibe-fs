namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation

module DelegationFactFold =
    val fold:
        sessionState: (SessionId -> DelegationSessionState option) ->
        handoffFrontier: (string -> int64 option) ->
        fact: DelegationFactCases ->
            Result<DelegationProjectionChange list, DelegationFoldRejection>
