namespace Wanxiangshu.Execution.Delegation

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Foundation

module ExecutionFactFold =
    val fold:
        sessionState: (SessionId -> DelegationSessionState option) ->
        fact: ExecutionFactCases ->
            Result<DelegationProjectionChange list, DelegationFoldRejection>
