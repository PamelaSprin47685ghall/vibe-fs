namespace Wanxiangshu.Interaction.Dispatch

open Wanxiangshu.Foundation.Identity

module PromptMetadataCodec =
    val create: key: PromptKey -> origin: string -> logicalRunId: LogicalRunId option -> obj
