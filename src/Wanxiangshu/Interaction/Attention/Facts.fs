namespace Wanxiangshu.Interaction.Attention

open Wanxiangshu.Foundation.Identity

type AttentionFactCases =
    | DeferredWorkRecorded of
        {| SessionId: SessionId
           OccurrenceId: string
           Text: string |}

