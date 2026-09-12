namespace Wanxiangshu.Interaction.Dispatch.OpenCode

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode

module PromptIngressCodec =
    type DecodedMessage = ChatAdmissionIntent.DecodedMessage

    val decodeWith: tryResolveAgent: (SessionId -> string option) -> input: obj -> output: obj -> DecodedMessage
