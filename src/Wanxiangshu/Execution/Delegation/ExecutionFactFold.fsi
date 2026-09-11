namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Foundation.Identity

module ExecutionFactFold =
    val fold:
        sessionState: (SessionId -> DelegationSessionState option) ->
        fact: ExecutionFactCases ->
            Result<DelegationProjectionChange list, DelegationFoldRejection>
