namespace Wanxiangshu.Participant.Cognition

open Wanxiangshu.Foundation.Identity

[<RequireQualifiedAccess>]
type CognitiveProjectionChange = CognitiveSet of ownerKey: string * projection: CognitiveProjection

[<RequireQualifiedAccess>]
type CognitiveFoldRejection =
    | NonSequentialOrdinal of committed: int64 * proposed: int64
    | ToolCallIdentityConflict of toolCallId: ToolCallId

[<RequireQualifiedAccess>]
module CognitiveFoldRejection =
    val fact: rejection: CognitiveFoldRejection -> string
    val message: rejection: CognitiveFoldRejection -> string

module CognitiveFactFold =
    val fold:
        current: CognitiveProjection option ->
        fact: AssumeFactCases.T ->
            Result<CognitiveProjectionChange list, CognitiveFoldRejection>
