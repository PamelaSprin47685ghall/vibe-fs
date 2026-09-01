namespace Wanxiangshu.Interaction.Dispatch

open Wanxiangshu.Foundation.Identity

module PromptPhysicalAcceptance =
    val register: promptKey: PromptKey -> callback: (PhysicalUserMessageId -> unit) -> unit
    val cancel: promptKey: PromptKey -> unit
    val accepted: promptKey: PromptKey -> physicalUserMessageId: PhysicalUserMessageId -> unit
