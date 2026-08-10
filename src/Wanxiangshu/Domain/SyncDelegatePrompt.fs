namespace Wanxiangshu.Domain

/// ARCH-010 renderers for SyncDelegate synthetic surfaces (EXEC-026 / EXEC-028).
[<RequireQualifiedAccess>]
module SyncDelegatePrompt =

    [<Literal>]
    let SyncDelegateReturnCompletion = "Sync delegate answer returned to caller."

    let returnResult =
        SyntheticToml.document
            [ "The answer is durably recorded and will be delivered to the caller after this turn completes."
              "Do not call another tool. Finish this turn now by outputting completion_text exactly." ]
            [ SyntheticToml.field "completion_text" (SyntheticToml.renderString SyncDelegateReturnCompletion) ]

    let idleNudge =
        SyntheticToml.document
            [ "The caller is still waiting. Continue reasoning and call return(message) with the answer."
              "Do not finish with ordinary prose." ]
            []
