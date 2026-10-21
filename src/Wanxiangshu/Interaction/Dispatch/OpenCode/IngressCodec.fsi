namespace Wanxiangshu.Interaction.Dispatch.OpenCode

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.OpenCode
open Wanxiangshu.Interaction.Admission

module PromptIngressCodec =
    type DecodedMessage = ChatAdmissionIntent.DecodedMessage

    val decodeWith: input: obj -> output: obj -> DecodedMessage
