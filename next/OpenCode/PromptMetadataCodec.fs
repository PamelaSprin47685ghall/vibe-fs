namespace Wanxiangshu.Next.OpenCode

open Fable.Core.JsInterop
open Wanxiangshu.Next.Kernel.Identity

/// Encodes dispatcher correlation metadata for the Host prompt boundary.
module PromptMetadataCodec =

    let create
        (key: PromptKeyRef)
        (origin: string)
        (logicalRunId: string)
        (authorityRootUserMessageId: string)
        : obj =
        createObj
            [ "wanxiangshu_prompt_key", box (PromptKeyRef.value key)
              "wanxiangshu_origin", box origin
              "wanxiangshu_logical_run", box logicalRunId
              "wanxiangshu_authority_root", box authorityRootUserMessageId ]
