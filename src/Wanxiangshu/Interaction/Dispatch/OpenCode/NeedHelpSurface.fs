namespace Wanxiangshu.Interaction.Dispatch.OpenCode

open Fable.Core.JsInterop
open Wanxiangshu.Interaction.Dispatch
open Wanxiangshu.OpenCode

/// JS-native HOST-027 boundary. Host stream events and persisted reasoning
/// text enter as plain objects/strings; the codec and sentinel stay private.
module NeedHelpSurface =

    let sentinel : string = AssistancePrompt.Sentinel

    let strip (text: string) : string =
        if isNull text then "" else AssistancePrompt.stripSentinel text

    let isRelevant (raw: obj) : bool = NeedHelpEventCodec.isNeedHelpRelevantEvent raw

    let isLegacyDelta (raw: obj) : bool = NeedHelpEventCodec.isNeedHelpDelta raw
