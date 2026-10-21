namespace Wanxiangshu.Interaction.Dispatch

open Wanxiangshu.Foundation.Identity

module PromptMetadataCodec =
    [<Literal>]
    val PromptKeyField: string = "wanxiangshu_prompt_key"

    [<Literal>]
    val OriginField: string = "wanxiangshu_origin"

    val create: key: PromptKey -> origin: string -> logicalRunId: LogicalRunId option -> obj
