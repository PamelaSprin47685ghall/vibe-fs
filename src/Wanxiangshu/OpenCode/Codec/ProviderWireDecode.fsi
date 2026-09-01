namespace Wanxiangshu.OpenCode

open Wanxiangshu.Foundation.Identity
open Wanxiangshu.Participant.Provider.Projection.ProviderProjection

module ProviderWireDecode =
    val readField: value: obj -> name: string -> obj
    val firstString: value: obj -> names: string list -> string option
    val infoObject: rawObj: obj -> obj
    val rawArray: value: obj -> obj list
    val decodePart: partObj: obj -> WirePart option
    val messagesFromTransformOutput: output: obj -> obj list
    val hostMessageId: rawObj: obj -> string option
    val rawPartsOf: rawObj: obj -> obj list
    val promptKeyOfMessage: rawObj: obj -> PromptKey option
    val promptOriginOfMessage: rawObj: obj -> string option
    val projectionSessionIdFromMessages: output: obj -> string option
